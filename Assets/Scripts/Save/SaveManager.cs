using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using IdlePowerGen.Models;

namespace IdlePowerGen.Save
{
    // Â§7 â€” save, load, migration, and offline-progression dispatch.
    public class SaveManager : MonoBehaviour
    {
        public static SaveManager Instance { get; private set; }

        private const int CurrentVersion = 1;
        private string SavePath => Path.Combine(Application.persistentDataPath, "save.json");

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
                pennies           = gm.Ledger.Pennies.ToString(),
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

            decimal pennies = decimal.TryParse(data.pennies, out var p) ? p : 0m;
            gm.Ledger.SetFromSave(data.joules, pennies, data.reputation);
            gm.Upgrades.LoadFromSave(data.purchasedUpgradeIds);

            Managers.EraManager.Instance?.SetEraFromSave(data.currentEra);
            Utils.GameClock.Instance?.SetGameTime(data.gameTimeSeconds);

            // Restore generators
            var genCtrl  = FindAnyObjectByType<Controllers.Era2GeneratorController>();
            if (genCtrl != null && data.generators.Count > 0)
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
                var def = FindContractDef(entry.contractId);
                if (def == null) continue;
                var instance = new Economy.ContractInstance(def, entry.startGameTime);
                instance.SetAccumulatedFromSave(entry.accumulatedWattSeconds);
                gm.Contracts.AddFromSave(instance);
            }
        }

        // ---- Offline progression ----

        private static void DispatchOfflineProgression(SaveData data)
        {
            long nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (data.saveTimestampUnix > nowUnix) return; // Â§7.5 tamper guard

            var clock = Utils.GameClock.Instance;
            float scale = clock != null ? clock.GameTimeScale : 60f;
            double realSeconds   = nowUnix - data.saveTimestampUnix;
            double gameSeconds   = realSeconds * scale;

            var gm  = Managers.GameManager.Instance;
            var cfg = gm?.GetEraConfig((int)data.currentEra);
            float eraFactor = cfg != null ? cfg.offlineEraFactor : 1f;
            float tauHours  = cfg != null ? cfg.offlineTauHours  : 4f;

            var tier = Managers.OfflineProgressionManager.ClassifyAbsence(gameSeconds);

            if (tier == Managers.OfflineProgressionManager.OfflineTier.Short)
            {
                Simulation.SimulationTick.Instance?.CatchUpOffline(gameSeconds);
            }
            else
            {
                var sim = Simulation.SimulationTick.Instance;
                double onlineWatts = sim?.Grid.TotalOutputWatts ?? 0;
                double offlineJoules = Managers.OfflineProgressionManager.ComputeOfflineJoules(
                    onlineWatts, gameSeconds, eraFactor, tauHours);
                gm?.Ledger.AddJoules(offlineJoules);
                sim?.Grid.AddTapEnergy(offlineJoules);
            }
        }

        // Simple linear search â€” catalogue is small.
        private static ContractDefinition FindContractDef(string id)
        {
            foreach (var obj in Resources.FindObjectsOfTypeAll<ContractDefinition>())
                if (obj.contractId == id) return obj;
            return null;
        }
    }
}
