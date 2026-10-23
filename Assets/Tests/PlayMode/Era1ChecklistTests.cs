using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using static IdlePowerGen.Data.Era1Data;

namespace IdlePowerGen.Tests
{
    // Todo §9 testing checklist and the levelled crank / flywheel tree, one behaviour per test.
    public class Era1ChecklistTests : Era1SceneFixture
    {
        private static Managers.GameManager GM => Managers.GameManager.Instance;
        private static Controllers.Era1TapController Tap => UnityEngine.Object.FindAnyObjectByType<Controllers.Era1TapController>();

        private static void Crank() => GameObject.Find("CrankButton").GetComponent<Button>().onClick.Invoke();

        private static string SavePath =>
            Path.Combine(Application.persistentDataPath, Save.SaveManager.SaveFileName);

        // --- Better Crank ---

        [UnityTest]
        public IEnumerator AfterBetterCrankEachTapAddsTwoJoules()
        {
            GM.Ledger.AddJoules(15);
            GM.Upgrades.Purchase(Upgrade(BetterCrankId));
            yield return null;
            Crank();
            Assert.That(GM.Ledger.Joules, Is.EqualTo(2.0));
        }

        [Test]
        public void BetterCrankLevelsAddOneJouleEach()
        {
            SetLevels((BetterCrankId, 3));
            Assert.That(Tap.CrankTapValue, Is.EqualTo(4.0));
        }

        [Test]
        public void BetterCrankCostsHalfAgainEachLevel()
        {
            SetLevels((BetterCrankId, 1));
            Assert.That(GM.Upgrades.NextCostJoules(Upgrade(BetterCrankId)), Is.EqualTo(22.5));
        }

        [Test]
        public void BetterCrankCannotBeBoughtPastItsMaxLevel()
        {
            SetLevels((BetterCrankId, Upgrade(BetterCrankId).maxLevel));
            GM.Ledger.AddJoules(1e12);
            Assert.That(GM.Upgrades.Purchase(Upgrade(BetterCrankId)), Is.False);
        }

        // --- Iron Flywheel ---

        [Test]
        public void WithoutAFlywheelTurnsStoreNoSpin()
        {
            Crank();
            Assert.That(Flywheel.SpinWatts, Is.EqualTo(0.0));
        }

        [Test]
        public void TurnsSpinTheFlywheelUpToItsCapacity()
        {
            SetLevels((BetterCrankId, 1), (IronFlywheelId, 1));
            for (int i = 0; i < 10; i++) Crank();
            Assert.That(Flywheel.SpinWatts, Is.EqualTo(5.0).Within(1e-9));
        }

        [Test]
        public void EachFlywheelLevelAddsFiveWattsOfCapacity()
        {
            SetLevels((BetterCrankId, 1), (IronFlywheelId, 3));
            Assert.That(Flywheel.CapacityWatts, Is.EqualTo(15.0));
        }

        [UnityTest]
        public IEnumerator SpinningFlywheelEarnsJoulesAfterTheTurnsStop()
        {
            SetLevels((BetterCrankId, 1), (IronFlywheelId, 4));
            for (int i = 0; i < 10; i++) Crank();
            double afterTurns = GM.Ledger.Joules;
            yield return new WaitForSeconds(2.1f);
            Assert.That(GM.Ledger.Joules, Is.GreaterThan(afterTurns + 5));
        }

        [UnityTest]
        public IEnumerator UntendedFlywheelWindsDown()
        {
            SetLevels((BetterCrankId, 1), (IronFlywheelId, 4));
            for (int i = 0; i < 10; i++) Crank();
            yield return new WaitForSeconds(2.1f);
            Assert.That(Flywheel.SpinWatts, Is.LessThan(20.0 * 0.8));
        }

        // --- Bearings ---

        [Test]
        public void BearingsAreLockedUntilTheFlywheelIsBought()
        {
            SetLevels((BetterCrankId, 1));
            Assert.That(GM.Upgrades.PrerequisitesMet(Upgrade(BearingsId)), Is.False);
        }

        [Test]
        public void BuyingTheFlywheelUnlocksBearings()
        {
            SetLevels((BetterCrankId, 1), (IronFlywheelId, 1));
            Assert.That(GM.Upgrades.PrerequisitesMet(Upgrade(BearingsId)), Is.True);
        }

        [Test]
        public void EachBearingLevelSlowsTheDecayByAFifth()
        {
            SetLevels((BetterCrankId, 1), (IronFlywheelId, 1), (BearingsId, 2));
            Assert.That(Flywheel.DecayPerSecond, Is.EqualTo(FlywheelBaseDecayPerSecond * 0.64).Within(1e-12));
        }

        // --- Magnets ---

        [Test]
        public void MagnetsDoubleWhatATurnPays()
        {
            SetLevels((BetterCrankId, 1), (IronFlywheelId, 1), (MagnetsId, 1));
            Assert.That(Tap.CrankTapValue, Is.EqualTo(4.0));
        }

        [Test]
        public void MagnetsDoubleTheFlywheelOutput()
        {
            SetLevels((BetterCrankId, 1), (IronFlywheelId, 1), (MagnetsId, 1));
            for (int i = 0; i < 10; i++) Crank();
            Assert.That(Flywheel.CurrentOutputWatts, Is.EqualTo(10.0).Within(1e-9));
        }

        [Test]
        public void MagnetsMakeTheFlywheelSlowFaster()
        {
            SetLevels((BetterCrankId, 1), (IronFlywheelId, 1), (MagnetsId, 1));
            Assert.That(Flywheel.DecayPerSecond, Is.GreaterThan(FlywheelBaseDecayPerSecond));
        }

        // --- Water wheel and gearing ---

        [UnityTest]
        public IEnumerator WaterWheelKeepsOutputWithoutTurning()
        {
            SetLevels((BetterCrankId, 1), (IronFlywheelId, 1), (WaterWheelId, 1));
            yield return new WaitForSeconds(2.1f);
            Assert.That(Simulation.SimulationTick.Instance.Grid.TotalOutputWatts, Is.EqualTo(25.0).Within(1e-9));
        }

        [Test]
        public void GearingIsLockedUntilTheWaterWheel()
        {
            SetLevels((BetterCrankId, 1), (IronFlywheelId, 1));
            Assert.That(GM.Upgrades.PrerequisitesMet(Upgrade(BeltsGearingId)), Is.False);
        }

        [Test]
        public void EachGearingLevelAddsTenWattsToTheWaterWheel()
        {
            SetLevels((BetterCrankId, 1), (IronFlywheelId, 1), (WaterWheelId, 1), (BeltsGearingId, 2));
            Assert.That(WaterWheel.BaseWatts, Is.EqualTo(45.0));
        }

        [Test]
        public void MagnetsMultiplyTheWaterWheelToo()
        {
            SetLevels((BetterCrankId, 1), (IronFlywheelId, 1), (MagnetsId, 2), (WaterWheelId, 1));
            Assert.That(WaterWheel.CurrentOutputWatts, Is.EqualTo(100.0));
        }

        [Test]
        public void FlywheelAndWaterWheelOutputsAdd()
        {
            SetLevels((BetterCrankId, 1), (IronFlywheelId, 1), (WaterWheelId, 1));
            for (int i = 0; i < 10; i++) Crank();
            Assert.That(Simulation.SimulationTick.Instance.Grid.ComputeOutputWatts(), Is.EqualTo(30.0).Within(1e-9));
        }

        [Test]
        public void TheWaterWheelDoesNotSpinTheFlywheel()
        {
            SetLevels((BetterCrankId, 1), (IronFlywheelId, 1), (WaterWheelId, 1));
            Simulation.SimulationTick.Instance.CatchUpOffline(5);
            Assert.That(Flywheel.SpinWatts, Is.EqualTo(0.0));
        }

        [Test]
        public void WithoutAWaterWheelNothingIsSustained()
        {
            SetLevels((BetterCrankId, 5), (IronFlywheelId, 5), (MagnetsId, 2));
            for (int i = 0; i < 10; i++) Crank();
            Assert.That(Simulation.SimulationTick.Instance.Grid.ComputeSustainedOutputWatts(), Is.EqualTo(0.0));
        }

        // --- Save / restore ---

        private IEnumerator SaveThenWipeThenLoad()
        {
            SetLevels((BetterCrankId, 3), (IronFlywheelId, 2), (WaterWheelId, 1));
            GM.Ledger.AddJoules(1234);
            Save.SaveManager.Instance.Save();

            // What a killed process loses: every bit of in-memory state.
            GM.Ledger.SetFromSave(0, 0m, 0f);
            SetLevels();

            Save.SaveManager.Instance.Load();
            yield return null;
        }

        [UnityTest]
        public IEnumerator SaveAndLoadRestoresJoules()
        {
            yield return SaveThenWipeThenLoad();
            Assert.That(GM.Ledger.Joules, Is.EqualTo(1234.0).Within(60.0)); // ≤1 s of 25 W catch-up on top
        }

        [UnityTest]
        public IEnumerator SaveAndLoadRestoresUpgradeLevels()
        {
            yield return SaveThenWipeThenLoad();
            Assert.That(GM.Upgrades.Level(BetterCrankId), Is.EqualTo(3));
        }

        [UnityTest]
        public IEnumerator SaveAndLoadRestoresCrankValue()
        {
            yield return SaveThenWipeThenLoad();
            Assert.That(Tap.CrankTapValue, Is.EqualTo(4.0));
        }

        [UnityTest]
        public IEnumerator SaveAndLoadRestoresTheWaterWheel()
        {
            yield return SaveThenWipeThenLoad();
            Assert.That(WaterWheel.BaseWatts, Is.EqualTo(25.0));
        }

        // --- v2 → v3 migration ---

        private static Save.SaveData V2Save() => new()
        {
            version = 2,
            joules = 300,
            purchasedUpgradeIds = new List<string>
                { "era1_better_crank", "era1_gear_train", "era1_unlock_waterwheel", "era2_some_upgrade" },
            generators = new List<Save.SaveData.GeneratorEntry>
                { new() { generatorType = "SpinningJenny", count = 4 } },
        };

        [Test]
        public void V2SaveKeepsSurvivingUpgradesAsLevelOne()
        {
            var data = V2Save();
            Save.SaveManager.Migrate(data);
            Assert.That(data.upgradeLevels.ConvertAll(e => $"{e.upgradeId}:{e.level}"),
                        Is.EquivalentTo(new[] { $"{BetterCrankId}:1", $"{WaterWheelId}:1", "era2_some_upgrade:1" }));
        }

        [Test]
        public void V2SaveDropsTheRemovedEra1Machines()
        {
            var data = V2Save();
            Save.SaveManager.Migrate(data);
            Assert.That(data.generators, Is.Empty);
        }

        // --- Offline guard (bible §7.5) ---

        private static void WriteSaveWithWaterWheel(long timestamp, double joules)
        {
            var data = new Save.SaveData
            {
                version = Save.SaveManager.CurrentVersion,
                saveTimestampUnix = timestamp,
                joules = joules,
                pennies = "0",
                currentEra = 1f,
                upgradeLevels = new List<Save.SaveData.UpgradeLevelEntry>
                {
                    new() { upgradeId = BetterCrankId,  level = 1 },
                    new() { upgradeId = IronFlywheelId, level = 1 },
                    new() { upgradeId = WaterWheelId,   level = 1 },
                },
            };
            File.WriteAllText(SavePath, JsonUtility.ToJson(data));
        }

        // No frame in between: a live tick of the water wheel would muddy the reading.
        [Test]
        public void ClockSetBackBeforeTheLastSaveAwardsNoOfflineJoules()
        {
            WriteSaveWithWaterWheel(DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 3600, 100);
            Save.SaveManager.Instance.Load();
            Assert.That(GM.Ledger.Joules, Is.EqualTo(100.0));
        }

        [UnityTest]
        public IEnumerator TenMinutesAwayAwardsAtMostSeventyPercentOfTheWaterWheel()
        {
            WriteSaveWithWaterWheel(DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 600, 100);
            Save.SaveManager.Instance.Load();
            yield return null;
            // 25 W for 600 s = 15 kJ untended; offline pays at most 70 % of that (bible §7.2).
            Assert.That(GM.Ledger.Joules - 100, Is.InRange(1.0, 0.7 * 25 * 600 + 30));
        }

        // --- Performance proxy (not a device profile) ---

        [Test]
        public void AnHourOfFullEra1TicksRunsWellUnderASecond()
        {
            SetLevels((BetterCrankId, 20), (IronFlywheelId, 20), (BearingsId, 4), (MagnetsId, 4),
                      (WaterWheelId, 1), (BeltsGearingId, 20));
            for (int i = 0; i < 50; i++) Crank();

            var sw = Stopwatch.StartNew();
            Simulation.SimulationTick.Instance.CatchUpOffline(3600);
            sw.Stop();
            // 3600 ticks ≈ an hour of play. A desktop doing this in <1 s leaves a 1 Hz
            // tick using a tiny fraction of a frame even on a slow phone.
            Assert.That(sw.ElapsedMilliseconds, Is.LessThan(1000));
        }
    }
}
