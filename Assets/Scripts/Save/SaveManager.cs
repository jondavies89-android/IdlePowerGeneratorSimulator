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
        // v3 → v4: the Era 2 works (start day, coal hopper, contracts booked per day).
        // v4 → v5: engagements pay as they run, so each records what it has paid so far.
        // v5 → v6: contracts keep their own progress (for the 2× boost); the boost's time left.
        // v6 → v7: each engagement's shortfall countdown.
        public const int CurrentVersion = 7;

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
                contractBoostSecondsLeft = gm.ContractBoostSecondsLeft,
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

            var works = FindAnyObjectByType<Controllers.Era2WorksController>();
            if (works != null)
            {
                data.era2StartDay = works.StartDay;
                data.coalLevel    = works.Hopper.Level;
                foreach (var (id, day) in works.Board.BookedDays)
                    data.contractBookedDays.Add(new SaveData.BookedDayEntry { contractId = id, day = day });
            }

            // Active contracts
            foreach (var contract in gm.Contracts.ActiveContracts)
            {
                data.activeContracts.Add(new SaveData.ContractEntry
                {
                    contractId             = contract.Definition.contractId,
                    startGameTime          = contract.StartGameTime,
                    accumulatedWattSeconds = contract.AccumulatedWattSeconds,
                    paidPennies            = contract.PaidSoFar.ToString(CultureInfo.InvariantCulture),
                    progressSeconds        = contract.ProgressSeconds,
                    shortSeconds           = contract.ShortSeconds,
                    suppliedSeconds        = contract.SuppliedSeconds,
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

        // The ?era=2 link (EraShortcutLink): wipes the save and starts a fresh run at the
        // opening of Era 2, with the workshop a player has when the neighbour pays up. The
        // neighbour wants 1 kW held, and only a water wheel on full belts (50 W × 2⁵) gets
        // there, so that is the workshop; the crank and flywheel are a middling build.
        private static readonly (string id, int level)[] Era1FinishedWorkshop =
        {
            (Data.Era1Data.BetterCrankId, 5), (Data.Era1Data.IronFlywheelId, 3), (Data.Era1Data.BearingsId, 1),
            (Data.Era1Data.MagnetsId, 1), (Data.Era1Data.WaterWheelId, 1), (Data.Era1Data.BeltsGearingId, 5),
        };

        public void StartAtEra2()
        {
            if (File.Exists(SavePath)) File.Delete(SavePath);
            var data = new SaveData
            {
                version = CurrentVersion,
                saveTimestampUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                pennies = "12", // Mr Hale's shilling
                currentEra = 1f,
            };
            foreach (var (id, level) in Era1FinishedWorkshop)
                data.upgradeLevels.Add(new SaveData.UpgradeLevelEntry { upgradeId = id, level = level });
            foreach (var m in Data.Era1Data.Milestones) data.reachedMilestones.Add(m.Id);

            ApplyToGame(data);
            // Advancing (rather than loading an Era 2 save) runs the era's own start: the
            // title card, the clock wound on to the evening, the hopper filled.
            Managers.EraManager.Instance?.AdvanceTo(2);
            Save();
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
            if (data.version < 4) MigrateV3ToV4(data);
            if (data.version < 5) MigrateV4ToV5(data);
            if (data.version < 6) MigrateV5ToV6(data);
            if (data.version < 7) MigrateV6ToV7(data);
        }

        // Before v7 there was no countdown: every engagement starts with a clean slate.
        private static void MigrateV6ToV7(SaveData data)
        {
            foreach (var c in data.activeContracts ?? new List<SaveData.ContractEntry>())
                c.shortSeconds = c.suppliedSeconds = 0;
            data.version = 7;
        }

        // Before v6 a contract's progress was simply the clock since it started.
        private static void MigrateV5ToV6(SaveData data)
        {
            foreach (var c in data.activeContracts ?? new List<SaveData.ContractEntry>())
                c.progressSeconds = Math.Max(0, data.gameTimeSeconds - c.startGameTime);
            data.contractBoostSecondsLeft = 0;
            data.version = 6;
        }

        // A v4 engagement was paid nothing until it ended, so all of its fee is still owed.
        private static void MigrateV4ToV5(SaveData data)
        {
            foreach (var c in data.activeContracts ?? new List<SaveData.ContractEntry>())
                c.paidPennies = "0";
            data.version = 5;
        }

        // Nobody reached Era 2 before v4, so its state starts empty.
        private static void MigrateV3ToV4(SaveData data)
        {
            data.contractBookedDays ??= new List<SaveData.BookedDayEntry>();
            data.version = 4;
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
            // Spin isn't saved; the flywheel starts the session at rest.
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

            var works = FindAnyObjectByType<Controllers.Era2WorksController>();
            if (works != null)
            {
                var booked = new List<(string, long)>();
                foreach (var e in data.contractBookedDays ?? new List<SaveData.BookedDayEntry>())
                    booked.Add((e.contractId, e.day));
                works.LoadFromSave(data.era2StartDay, data.coalLevel, booked);
            }

            // Restore contracts
            gm.Contracts.ClearActive();
            foreach (var entry in data.activeContracts)
            {
                var def = gm.FindContract(entry.contractId);
                if (def == null) continue;
                var instance = new Economy.ContractInstance(def, entry.startGameTime);
                instance.SetAccumulatedFromSave(entry.accumulatedWattSeconds);
                if (decimal.TryParse(entry.paidPennies, NumberStyles.Number, CultureInfo.InvariantCulture, out var paid))
                    instance.SetPaidFromSave(paid);
                instance.SetProgressFromSave(entry.progressSeconds);
                instance.SetShortfallFromSave(entry.shortSeconds, entry.suppliedSeconds);
                gm.Contracts.AddFromSave(instance);
            }
            gm.SetContractBoostFromSave(data.contractBoostSecondsLeft);

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
            double  before        = gm?.Ledger.Joules ?? 0;
            decimal penniesBefore = gm?.Ledger.Pennies ?? 0m;

            var tier = Managers.OfflineProgressionManager.ClassifyAbsence(realSeconds);
            if (tier == Managers.OfflineProgressionManager.OfflineTier.Short)
            {
                sim?.CatchUpOffline(realSeconds);
            }
            else
            {
                // Only what keeps running untended (the water wheel), never a flywheel
                // still spinning from the last few turns.
                double onlineWatts = sim?.Grid.ComputeSustainedOutputWatts() ?? 0;
                double offlineJoules = Managers.OfflineProgressionManager.ComputeOfflineJoules(
                    onlineWatts, realSeconds, eraFactor, tauHours);
                gm?.Ledger.AddJoules(offlineJoules);
                if (data.currentEra >= 2f && gm != null) PayOfflineCustomers(gm, data, realSeconds, eraFactor, tauHours);
            }

            double earned = (gm?.Ledger.Joules ?? 0) - before;
            decimal earnedPennies = (gm?.Ledger.Pennies ?? 0m) - penniesBefore;
            EventBus.Publish(new OfflineProgressEvent(realSeconds, earned,
                                                      cfg != null ? cfg.offlineFlavorText : null, earnedPennies));
        }

        // Era 2, a medium or long absence: the game clock waits for the player, so booked
        // engagements resume where they were. Meanwhile the customers who were drawing
        // power at save time keep paying for it, on the same decaying curve as Era 1's joules.
        // No wages or coal are charged: nothing goes wrong while the player is away (bible §10.5).
        private static void PayOfflineCustomers(Managers.GameManager gm, SaveData data, double realSeconds,
                                                float eraFactor, float tauHours)
        {
            double committed = 0, payRate = 0;
            foreach (var entry in data.activeContracts)
            {
                var def = gm.FindContract(entry.contractId);
                if (def == null) continue;
                committed += def.requiredWatts;
                payRate   += def.requiredWatts * gm.ContractBaseRate(def) * def.payRateMultiplier;
            }
            double sustained = Simulation.SimulationTick.Instance?.Grid.ComputeSustainedOutputWatts() ?? 0;
            double sold = Math.Min(committed, sustained);
            double scale = Utils.GameClock.Instance != null ? Utils.GameClock.Instance.BaseTimeScale : 60f;

            // Watt-seconds of offline effort; pay is per watt per game hour, and a real
            // second is BaseTimeScale game seconds (a boost doesn't pay while away).
            decimal Earn(double watts, double penniesPerWattGameHour) =>
                (decimal)(Managers.OfflineProgressionManager.ComputeOfflineJoules(watts, realSeconds, eraFactor, tauHours)
                          * scale / 3600.0 * penniesPerWattGameHour);

            decimal pay = 0m;
            if (sold > 0) pay += Earn(sold, payRate / committed);

            // The rest went to the street, priced over whole days rather than the hour it left at.
            double spare = sustained - sold;
            double streetPrice = gm.StreetPricePerWattHour(data.currentEra, data.gameTimeSeconds, averageDay: true);
            if (spare > 0 && streetPrice > 0) pay += Earn(spare, streetPrice);

            if (pay > 0) gm.Ledger.AddPennies(pay);
        }
    }
}
