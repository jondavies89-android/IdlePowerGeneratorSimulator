using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using IdlePowerGen.Models;
using IdlePowerGen.Utils;

namespace IdlePowerGen.Save
{
    // §7 — save, load, migration, and offline-progression dispatch.
    public class SaveManager : MonoBehaviour
    {
        public static SaveManager Instance { get; private set; }

        private const int CurrentVersion = 1;

        // Tests point this at a scratch file so they never touch a real player's save.
        public static string SaveFileName = "save.json";
        private static string SavePath => Path.Combine(Application.persistentDataPath, SaveFileName);

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // ---- Save ----

        public void Save()
        {
            var gm = Managers.GameManager.Instance;
            if (gm == null) return;

            var data = new SaveData
            {
                version           = CurrentVersion,
                saveTimestampUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                joules            = gm.Ledger.Joules,
                pennies           = gm.Ledger.Pennies.ToString(CultureInfo.InvariantCulture),
                reputation        = gm.Ledger.Reputation,
                currentEra        = Managers.EraManager.Instance?.CurrentEra ?? 1f,
                gameTimeSeconds   = Utils.GameClock.Instance?.GameTimeSeconds ?? 0,
                purchasedUpgradeIds = new List<string>(gm.Upgrades.PurchasedIds),
                lifetimeMWh       = 0, // updated by stats tracker in later eras
            };

            // Generators
            var genCtrl = FindAnyObjectByType<Controllers.Era2GeneratorController>();
            if (genCtrl != null)
            {
                foreach (GeneratorType type in Enum.GetValues(typeof(GeneratorType)))
                {
                    int count = genCtrl.GetOwned(type);
                    if (count > 0)
                        data.generators.Add(new SaveData.GeneratorEntry
                            { generatorType = type.ToString(), count = count });
                }
            }

            // Active contracts
            foreach (var contract in gm.Contracts.ActiveContracts)
            {
                data.activeContracts.Add(new SaveData.ContractEntry
                {
                    contractId             = contract.Definition.contractId,
                    startGameTime          = contract.StartGameTime,
                    accumulatedWattSeconds = contract.AccumulatedWattSeconds,
                });
            }

            File.WriteAllText(SavePath, JsonUtility.ToJson(data));
            PersistentStorage.Flush();
        }

        // Wipes the save and returns every system to a fresh Era 1 start, in place.
        // Reloading the scene wouldn't work: the managers are DontDestroyOnLoad singletons.
        public void ResetProgress()
        {
            if (File.Exists(SavePath)) File.Delete(SavePath);
            PersistentStorage.Flush();
            ApplyToGame(new SaveData { pennies = "0", currentEra = 1f });
        }

        // ---- Load ----

        public void Load()
        {
            if (!File.Exists(SavePath)) return;

            try
            {
                var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath));
                Migrate(data);
                ApplyToGame(data);
                DispatchOfflineProgression(data);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveManager] Load failed: {e.Message}");
            }
        }

        // ---- Migration chain ----

        private static void Migrate(SaveData data)
        {
            // Add version-to-version migration functions here as the game evolves.
            // Example: if (data.version < 2) MigrateV1ToV2(data);
        }

        // ---- Apply ----

        private static void ApplyToGame(SaveData data)
        {
            var gm = Managers.GameManager.Instance;
            if (gm == null) return;

            decimal pennies = decimal.TryParse(data.pennies, NumberStyles.Number,
                                               CultureInfo.InvariantCulture, out var p) ? p : 0m;
            gm.Ledger.SetFromSave(data.joules, pennies, data.reputation);
            gm.Upgrades.LoadFromSave(data.purchasedUpgradeIds);

            Managers.EraManager.Instance?.SetEraFromSave(data.currentEra);
            Utils.GameClock.Instance?.SetGameTime(data.gameTimeSeconds);

            // Restore generators. The grid is cleared first so a reset or a second load
            // can't stack a duplicate fleet on top of the current one.
            Simulation.SimulationTick.Instance?.Grid.ClearGenerators();
            var genCtrl  = FindAnyObjectByType<Controllers.Era2GeneratorController>();
            if (genCtrl != null)
            {
                var counts = new Dictionary<GeneratorType, int>();
                foreach (var entry in data.generators)
                {
                    if (Enum.TryParse<GeneratorType>(entry.generatorType, out var t))
                        counts[t] = entry.count;
                }
                genCtrl.LoadFromSave(counts,
                    new List<GeneratorDefinition>(gm.GeneratorCatalogue));
            }

            // Restore contracts
            gm.Contracts.ClearActive();
            foreach (var entry in data.activeContracts)
            {
                var def = gm.FindContract(entry.contractId);
                if (def == null) continue;
                var instance = new Economy.ContractInstance(def, entry.startGameTime);
                instance.SetAccumulatedFromSave(entry.accumulatedWattSeconds);
                gm.Contracts.AddFromSave(instance);
            }

            // Upgrade effects and crank value are runtime state derived from the purchased
            // set; their owners rebuild them on this event.
            EventBus.Publish(new GameLoadedEvent());
        }

        // ---- Offline progression ----

        private static void DispatchOfflineProgression(SaveData data)
        {
            long nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (data.saveTimestampUnix > nowUnix) return; // §7.5 tamper guard

            // Generators produce per real second (the sim tick runs on real time), so the
            // absence is measured in real seconds. GameClock's 60× scale is only for
            // time-of-day and contract durations.
            double realSeconds = nowUnix - data.saveTimestampUnix;
            if (realSeconds <= 0) return;

            var gm  = Managers.GameManager.Instance;
            var cfg = gm?.GetEraConfig((int)data.currentEra);
            float eraFactor = cfg != null ? cfg.offlineEraFactor : 1f;
            float tauHours  = cfg != null ? cfg.offlineTauHours  : 4f;

            var sim = Simulation.SimulationTick.Instance;
            double before = gm?.Ledger.Joules ?? 0;

            var tier = Managers.OfflineProgressionManager.ClassifyAbsence(realSeconds);
            if (tier == Managers.OfflineProgressionManager.OfflineTier.Short)
            {
                sim?.CatchUpOffline(realSeconds);
            }
            else
            {
                double onlineWatts = sim?.Grid.ComputeOutputWatts() ?? 0;
                double offlineJoules = Managers.OfflineProgressionManager.ComputeOfflineJoules(
                    onlineWatts, realSeconds, eraFactor, tauHours);
                gm?.Ledger.AddJoules(offlineJoules);
            }

            double earned = (gm?.Ledger.Joules ?? 0) - before;
            EventBus.Publish(new OfflineProgressEvent(realSeconds, earned,
                                                      cfg != null ? cfg.offlineFlavorText : null));
        }
    }
}
