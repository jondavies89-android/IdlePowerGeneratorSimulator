using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using IdlePowerGen.Models;

namespace IdlePowerGen.Tests
{
    // Todo §9 testing checklist, one behaviour per test.
    public class Era1ChecklistTests : Era1SceneFixture
    {
        private static Managers.GameManager GM => Managers.GameManager.Instance;
        private static Controllers.Era1TapController Tap => UnityEngine.Object.FindAnyObjectByType<Controllers.Era1TapController>();
        private static Controllers.Era2GeneratorController Gens => UnityEngine.Object.FindAnyObjectByType<Controllers.Era2GeneratorController>();

        private static readonly string[] CrankChain =
            { "era1_better_crank", "era1_iron_flywheel", "era1_gear_train", "era1_belt_drive", "era1_cam_mechanism" };

        private static UpgradeDefinition Upgrade(string id)
        {
            foreach (var u in GM.UpgradeCatalogue) if (u.upgradeId == id) return u;
            throw new ArgumentException(id);
        }

        private static GeneratorDefinition Generator(GeneratorType type)
        {
            foreach (var g in GM.GeneratorCatalogue) if (g.generatorType == type) return g;
            throw new ArgumentException(type.ToString());
        }

        private static void Crank() => GameObject.Find("CrankButton").GetComponent<Button>().onClick.Invoke();

        private static string SavePath =>
            Path.Combine(Application.persistentDataPath, Save.SaveManager.SaveFileName);

        // --- Tapping and the crank chain ---

        [UnityTest]
        public IEnumerator AfterBetterCrankEachTapAddsTwoJoules()
        {
            GM.Ledger.AddJoules(5);
            GM.Upgrades.Purchase(Upgrade("era1_better_crank"));
            yield return null;
            Crank();
            Assert.That(GM.Ledger.Joules, Is.EqualTo(2.0));
        }

        [Test]
        public void EachCrankUpgradeIsLockedUntilThePreviousIsBought()
        {
            GM.Ledger.AddJoules(100_000);
            for (int i = 1; i < CrankChain.Length; i++)
            {
                GM.Upgrades.Purchase(Upgrade(CrankChain[i - 1]));
                Assume.That(GM.Upgrades.PrerequisitesMet(Upgrade(CrankChain[i])), Is.True, CrankChain[i]);
                if (i + 1 < CrankChain.Length)
                    Assert.That(GM.Upgrades.PrerequisitesMet(Upgrade(CrankChain[i + 1])), Is.False, CrankChain[i + 1]);
            }
        }

        [Test]
        public void AllFiveCrankUpgradesMakeEachTurnWorth32Joules()
        {
            GM.Ledger.AddJoules(100_000);
            foreach (var id in CrankChain) GM.Upgrades.Purchase(Upgrade(id));
            Assert.That(Tap.CrankTapValue, Is.EqualTo(32.0));
        }

        // --- Passive generation ---

        [UnityTest]
        public IEnumerator SpinningJennyAddsOneWattOfOutput()
        {
            GM.Upgrades.LoadFromSave(new[] { "era1_better_crank", "era1_unlock_spinning_jenny" });
            GM.Ledger.AddJoules(50);
            Assume.That(Gens.TryBuy(Generator(GeneratorType.SpinningJenny)), Is.True);
            yield return new WaitForSeconds(1.1f);
            Assert.That(Simulation.SimulationTick.Instance.Grid.TotalOutputWatts, Is.EqualTo(1.0));
        }

        [UnityTest]
        public IEnumerator SpinningJennyEarnsJoulesWithoutTapping()
        {
            GM.Upgrades.LoadFromSave(new[] { "era1_better_crank", "era1_unlock_spinning_jenny" });
            GM.Ledger.AddJoules(50);
            Gens.TryBuy(Generator(GeneratorType.SpinningJenny));
            yield return new WaitForSeconds(3.1f);
            Assert.That(GM.Ledger.Joules, Is.GreaterThanOrEqualTo(3.0));
        }

        // --- Save / restore ---

        private IEnumerator SaveThenWipeThenLoad()
        {
            GM.Ledger.AddJoules(5 + 30 + 50 + 1234);
            GM.Upgrades.Purchase(Upgrade("era1_better_crank"));
            GM.Upgrades.Purchase(Upgrade("era1_unlock_spinning_jenny"));
            Gens.TryBuy(Generator(GeneratorType.SpinningJenny));
            Save.SaveManager.Instance.Save();

            // What a killed process loses: every bit of in-memory state.
            GM.Ledger.SetFromSave(0, 0m, 0f);
            GM.Upgrades.LoadFromSave(Array.Empty<string>());
            Gens.LoadFromSave(new Dictionary<GeneratorType, int>(), new List<GeneratorDefinition>());
            Simulation.SimulationTick.Instance.Grid.ClearGenerators();

            Save.SaveManager.Instance.Load();
            yield return null;
        }

        [UnityTest]
        public IEnumerator SaveAndLoadRestoresJoules()
        {
            yield return SaveThenWipeThenLoad();
            Assert.That(GM.Ledger.Joules, Is.EqualTo(1234.0).Within(2.0)); // ≤1 s of offline catch-up on top
        }

        [UnityTest]
        public IEnumerator SaveAndLoadRestoresUpgrades()
        {
            yield return SaveThenWipeThenLoad();
            Assert.That(GM.Upgrades.IsPurchased("era1_unlock_spinning_jenny"), Is.True);
        }

        [UnityTest]
        public IEnumerator SaveAndLoadRestoresGeneratorCounts()
        {
            yield return SaveThenWipeThenLoad();
            Assert.That(Gens.GetOwned(GeneratorType.SpinningJenny), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator SaveAndLoadRestoresCrankValue()
        {
            yield return SaveThenWipeThenLoad();
            Assert.That(Tap.CrankTapValue, Is.EqualTo(2.0));
        }

        // --- Offline guard (bible §7.5) ---

        private static void WriteSaveWithJenny(long timestamp, double joules)
        {
            var data = new Save.SaveData
            {
                version = Save.SaveManager.CurrentVersion,
                saveTimestampUnix = timestamp,
                joules = joules,
                pennies = "0",
                currentEra = 1f,
                purchasedUpgradeIds = new List<string> { "era1_better_crank", "era1_unlock_spinning_jenny" },
                generators = new List<Save.SaveData.GeneratorEntry>
                    { new() { generatorType = nameof(GeneratorType.SpinningJenny), count = 1 } },
            };
            File.WriteAllText(SavePath, JsonUtility.ToJson(data));
        }

        [UnityTest]
        public IEnumerator ClockSetBackBeforeTheLastSaveAwardsNoOfflineJoules()
        {
            WriteSaveWithJenny(DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 3600, 100);
            Save.SaveManager.Instance.Load();
            yield return null;
            Assert.That(GM.Ledger.Joules, Is.EqualTo(100.0));
        }

        [UnityTest]
        public IEnumerator TenMinutesAwayAwardsAtMostSeventyPercentOfOnlineOutput()
        {
            WriteSaveWithJenny(DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 600, 100);
            Save.SaveManager.Instance.Load();
            yield return null;
            // 1 W for 600 s = 600 J online; offline pays at most 70 % of that (bible §7.2).
            Assert.That(GM.Ledger.Joules - 100, Is.InRange(1.0, 0.7 * 600 + 1));
        }

        // --- Performance proxy (not a device profile) ---

        [Test]
        public void AnHourOfFullEra1FleetTicksRunsWellUnderASecond()
        {
            Gens.LoadFromSave(new Dictionary<GeneratorType, int>
            {
                [GeneratorType.SpinningJenny] = 50,
                [GeneratorType.Waterwheel]    = 20,
                [GeneratorType.SteamBoiler]   = 10,
            }, new List<GeneratorDefinition>(GM.GeneratorCatalogue));

            var sw = Stopwatch.StartNew();
            Simulation.SimulationTick.Instance.CatchUpOffline(3600);
            sw.Stop();
            // 3600 ticks ≈ an hour of play. A desktop doing this in <1 s leaves a 1 Hz
            // tick using a tiny fraction of a frame even on a slow phone.
            Assert.That(sw.ElapsedMilliseconds, Is.LessThan(1000));
        }
    }
}
