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

        // v1 → v2: reachedMilestones added.
        // v2 → v3: upgrades have levels; the Era 1 tree and its machines were replaced.
        public const int CurrentVersion = 3;

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
                reachedMilestones = Managers.Era1MilestoneTracker.Instance != null
                                    ? new List<string>(Managers.Era1MilestoneTracker.Instance.Reached)
                                    : new List<string>(),
                lifetimeMWh       = 0, // updated by stats tracker in later eras
            };

            foreach (var (id, level) in gm.Upgrades.Levels)
                data.upgradeLevels.Add(new SaveData.UpgradeLevelEntry { upgradeId = id, level = level });

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

        // Each step upgrades exactly one version, so any old save walks the whole chain.
        public static void Migrate(SaveData data)
        {
            if (data.version < 2) MigrateV1ToV2(data);
            if (data.version < 3) MigrateV2ToV3(data);
        }

        // v2 upgrade ids that survive into the levelled tree, and what they became.
        // The rest (gear train, belt drive, cam, foreman, the machine patents) and the
        // Era 1 machines are gone; the stored joules are kept, so nothing earned is lost.
        private static readonly Dictionary<string, string> V2UpgradeRenames = new()
        {
            ["era1_better_crank"]      = Data.Era1Data.BetterCrankId,
            ["era1_iron_flywheel"]     = Data.Era1Data.IronFlywheelId,
            ["era1_unlock_waterwheel"] = Data.Era1Data.WaterWheelId,
        };

        private static readonly string[] V2Era1Machines = { "SpinningJenny", "Waterwheel", "SteamBoiler" };

        private static void MigrateV2ToV3(SaveData data)
        {
            data.upgradeLevels ??= new List<SaveData.UpgradeLevelEntry>();
            foreach (var id in data.purchasedUpgradeIds ?? new List<string>())
            {
                // Ids from later eras carry over unchanged; dropped Era 1 ids don't.
                string newId = V2UpgradeRenames.TryGetValue(id, out var renamed) ? renamed
                             : id.StartsWith("era1_") ? null : id;
                if (newId != null)
                    data.upgradeLevels.Add(new SaveData.UpgradeLevelEntry { upgradeId = newId, level = 1 });
            }
            data.purchasedUpgradeIds = new List<string>();
            data.generators?.RemoveAll(g => Array.IndexOf(V2Era1Machines, g.generatorType) >= 0);
            data.version = 3;
        }

        // v1 had no milestone record. Anything a v1 player had already passed is marked
        // reached, so loading an old save never replays toasts for ground already covered.
        private static void MigrateV1ToV2(SaveData data)
        {
            data.reachedMilestones ??= new List<string>();
            foreach (var m in Data.Era1Data.Milestones)
                if (data.joules >= m.StoredJoules && !data.reachedMilestones.Contains(m.Id))
                    data.reachedMilestones.Add(m.Id);
            data.version = 2;
        }

        // ---- Apply ----

        private static void ApplyToGame(SaveData data)
        {
            var gm = Managers.GameManager.Instance;
            if (gm == null) return;

            decimal pennies = decimal.TryParse(data.pennies, NumberStyles.Number,
                                               CultureInfo.InvariantCulture, out var p) ? p : 0m;
            gm.Ledger.SetFromSave(data.joules, pennies, data.reputation);
            var levels = new Dictionary<string, int>();
            foreach (var entry in data.upgradeLevels ?? new List<SaveData.UpgradeLevelEntry>())
                levels[entry.upgradeId] = entry.level;
            gm.Upgrades.LoadFromSave(levels);
            // Spin isn't saved; the flywheel restarts from its water-wheel floor.
            Simulation.SimulationTick.Instance?.Flywheel.Stop();
            Managers.Era1MilestoneTracker.Instance?.LoadFromSave(data.reachedMilestones);

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
                // Only what keeps running untended (the water wheel), never a flywheel
                // spinning from the last few turns.
                double onlineWatts = sim?.Grid.ComputeSustainedOutputWatts() ?? 0;
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
