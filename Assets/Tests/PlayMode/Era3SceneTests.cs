using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using IdlePowerGen.Controllers;
using IdlePowerGen.Models;
using static IdlePowerGen.Data.Era3Data;

namespace IdlePowerGen.Tests
{
    // Era 3, the War of Currents, in the real scene, reached as the DEV strip does: a fresh
    // start at the opening of the era. One behaviour per test.
    public class Era3SceneTests : Era1SceneFixture
    {
        private static Managers.GameManager GM => Managers.GameManager.Instance;
        private static Era3TownController Town => Object.FindAnyObjectByType<Era3TownController>();
        private static Simulation.PowerGrid Grid => Simulation.SimulationTick.Instance.Grid;

        private static IEnumerator StartEra3()
        {
            Save.SaveManager.Instance.StartAtEra(EraNumber);
            yield return null;
            yield return null;
        }

        // One real second of sim with nothing else running (Time.timeScale 0 stops the frame ticks).
        private static void Tick(double seconds = 1)
        {
            Time.timeScale = 0f;
            Simulation.SimulationTick.Instance.CatchUpOffline(seconds);
        }

        [TearDown]
        public void RestoreTimeScale() => Time.timeScale = 1f;

        private static void Pay(double pounds) => GM.Ledger.AddPennies(Managers.Money.FromPounds(pounds));

        private static void Own(params string[] ids) =>
            SetLevels(ids.Select(id => (id, 1)).Concat(GM.Upgrades.Levels.Select(kv => (kv.Key, kv.Value))).ToArray());

        // --- The way in ---

        [UnityTest]
        public IEnumerator StartingEra3OpensTheEra3Screen()
        {
            yield return StartEra3();
            Assert.That(GameObject.Find("Era3Screen"), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator Era3InheritsEra2sFullOutput()
        {
            yield return StartEra3();
            Assert.That(Grid.InheritedWatts, Is.EqualTo(IdlePowerGen.Data.Era2Data.MaxWatts));
        }

        [UnityTest]
        public IEnumerator Era3OpensWithThreeDistrictsLit()
        {
            yield return StartEra3();
            Assert.That(Town.Areas.Count(a => a.Connected), Is.EqualTo(3));
        }

        [UnityTest]
        public IEnumerator Era3OpensWithTheTitleCard()
        {
            yield return StartEra3();
            Assert.That(GameObject.Find("Era3TitleCard").transform.Find("Panel").gameObject.activeSelf, Is.True);
        }

        [UnityTest]
        public IEnumerator TheCashShowsInPounds()
        {
            yield return StartEra3();
            var cash = GameObject.Find("Era3Screen").GetComponentsInChildren<TMPro.TMP_Text>()
                                 .First(t => t.name == "Value" && t.transform.parent.name == "Cash");
            Assert.That(cash.text, Does.StartWith("£"));
        }

        [Test]
        public void TheEra2PurseConvertsAtTwoHundredAndFortyPence() =>
            Assert.That(Managers.Money.Format(2400m, 3), Is.EqualTo("£10"));

        [UnityTest]
        public IEnumerator Era2MachinesRetireAtTheExhibition()
        {
            Save.SaveManager.Instance.StartAtEra(2);
            yield return null;
            Object.FindAnyObjectByType<Era2GeneratorController>().TryBuy(
                GM.GeneratorCatalogue.First(g => g.generatorType == GeneratorType.SmallDynamo));
            Managers.EraManager.Instance.AdvanceTo(3);
            Assert.That(Object.FindAnyObjectByType<Era2GeneratorController>().GetOwned(GeneratorType.SmallDynamo), Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator Era2MultipliersDoNotReachEra3()
        {
            yield return StartEra3();
            SetLevels((IdlePowerGen.Data.Era2Data.RoyalWarrantId, 1));
            Assert.That(Grid.GlobalOutputMultiplier, Is.EqualTo(1.0));
        }

        // --- Districts ---

        [UnityTest]
        public IEnumerator ConnectingADistrictCostsItsPrice()
        {
            yield return StartEra3();
            decimal before = GM.Ledger.Pennies;
            Town.Connect("d3_bond");
            Assert.That(before - GM.Ledger.Pennies, Is.EqualTo(Managers.Money.FromPounds(3 * 120 * 2)));
        }

        [UnityTest]
        public IEnumerator ADistrictPastTheNextOneCannotBeConnected()
        {
            yield return StartEra3();
            Assert.That(Town.StateOf("d3_railway"), Is.EqualTo(Era3TownController.ConnectState.NeedsParent));
        }

        [UnityTest]
        public IEnumerator DirectCurrentCannotReachThreeBlocks()
        {
            yield return StartEra3();
            Pay(10_000);
            Town.Connect("d3_high");
            Assert.That(Town.StateOf("d3_railway"), Is.EqualTo(Era3TownController.ConnectState.TooFar));
        }

        [UnityTest]
        public IEnumerator AlternatingCurrentReachesThreeBlocks()
        {
            yield return StartEra3();
            Pay(10_000);
            Town.Connect("d3_high");
            Own(ThreeWireId, TransformerId, AlternatorsId);
            Assert.That(Town.StateOf("d3_railway"), Is.EqualTo(Era3TownController.ConnectState.Available));
        }

        [UnityTest]
        public IEnumerator TappingADistrictAndItsCardConnectsIt()
        {
            yield return StartEra3();
            Pay(1_000);
            Object.FindAnyObjectByType<UI.Era3TownMapView>().Select("d3_bond");
            GameObject.Find("DistrictCard").GetComponent<Button>().onClick.Invoke();
            Assert.That(Town.Area("d3_bond").Connected, Is.True);
        }

        [UnityTest]
        public IEnumerator HeavierCopperHalvesALinesLoss()
        {
            yield return StartEra3();
            Pay(10_000);
            Town.Connect("d3_bond");
            double before = Town.LossPerWatt("d3_bond");
            Town.UpgradeCopper("d3_bond");
            Assert.That(Town.LossPerWatt("d3_bond"), Is.EqualTo(before / 2).Within(1e-15));
        }

        [UnityTest]
        public IEnumerator ThreeWireMainsQuarterTheLoss()
        {
            yield return StartEra3();
            double before = Town.LossPerWatt("d3_market");
            Own(ThreeWireId);
            Assert.That(Town.LossPerWatt("d3_market"), Is.EqualTo(before / 4).Within(1e-15));
        }

        // --- Money and research ---

        [UnityTest]
        public IEnumerator LitDistrictsPayForWhatReachesThem()
        {
            yield return StartEra3();
            decimal before = GM.Ledger.Pennies;
            Tick();
            Assert.That(GM.Ledger.Pennies, Is.GreaterThan(before));
        }

        [UnityTest]
        public IEnumerator TheLinesLoseSomeOfWhatIsSent()
        {
            yield return StartEra3();
            Tick();
            Assert.That(Town.LossFraction, Is.GreaterThan(0).And.LessThan(0.2));
        }

        [UnityTest]
        public IEnumerator WorkshopsMakeResearch()
        {
            yield return StartEra3();
            Own(WorkshopId);
            Tick(60); // an hour of game time
            Assert.That(GM.Ledger.ResearchPoints, Is.EqualTo(WorkshopResearchPerHour).Within(0.01));
        }

        [UnityTest]
        public IEnumerator ResearchCostsResearchPoints()
        {
            yield return StartEra3();
            GM.Ledger.AddResearch(100);
            GM.Upgrades.Purchase(Upgrade(TransformerId));
            Assert.That(GM.Ledger.ResearchPoints, Is.EqualTo(20).Within(1e-9));
        }

        [UnityTest]
        public IEnumerator AMachineWaitsForItsResearch()
        {
            yield return StartEra3();
            var turbine = GM.GeneratorCatalogue.First(g => g.generatorType == GeneratorType.ParsonsTurbine);
            Assert.That(Object.FindAnyObjectByType<Era2GeneratorController>().IsUnlocked(turbine), Is.False);
        }

        [UnityTest]
        public IEnumerator AJumboDynamoCostsTwoHundredPounds()
        {
            yield return StartEra3();
            var jumbo = GM.GeneratorCatalogue.First(g => g.generatorType == GeneratorType.JumboDynamo);
            Assert.That(Object.FindAnyObjectByType<Era2GeneratorController>().NextCostPennies(jumbo),
                        Is.EqualTo(Managers.Money.FromPounds(200)));
        }

        // --- Contracts ---

        // --- Active play ---

        private static void StokeIntoTheGreen()
        {
            while (!Town.Boiler.InGreen) Town.Stoke();
        }

        [UnityTest]
        public IEnumerator SteamInTheGreenPaysABonus()
        {
            yield return StartEra3();
            Time.timeScale = 0f;
            StokeIntoTheGreen();
            yield return null; // a frame of real time in the green
            Simulation.SimulationTick.Instance.CatchUpOfflineAsIfHere(1);
            Assert.That(Town.StokeEarned, Is.GreaterThan(0m));
        }

        [UnityTest]
        public IEnumerator TimeAwayEarnsNoStokingBonus()
        {
            yield return StartEra3();
            StokeIntoTheGreen();
            Tick(60);
            Assert.That(Town.StokeEarned, Is.EqualTo(0m));
        }

        [UnityTest]
        public IEnumerator AGoodReadingAddsResearch()
        {
            yield return StartEra3();
            double before = GM.Ledger.ResearchPoints;
            Town.Galvanometer.AimForTests(intoBand: true);
            Town.TakeReading();
            Assert.That(GM.Ledger.ResearchPoints, Is.GreaterThan(before));
        }

        [UnityTest]
        public IEnumerator TheStokeButtonRaisesThePressure()
        {
            yield return StartEra3();
            var stoke = GameObject.Find("Stoke");
            stoke.GetComponent<UI.PressButton>().OnPointerDown(
                new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current));
            Assert.That(Town.Boiler.Pressure, Is.GreaterThan(0f));
        }

        // The reading is taken as the finger goes down, before it lifts.
        [UnityTest]
        public IEnumerator AReadingIsTakenOnThePress()
        {
            yield return StartEra3();
            GameObject.Find("Era3Screen").GetComponentInChildren<UI.TabBarView>().Show(3); // Research
            Town.Galvanometer.AimForTests(intoBand: true);
            GameObject.Find("TakeReading").GetComponent<UI.PressButton>().OnPointerDown(
                new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current));
            Assert.That(Town.Galvanometer.Readings, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator TheGalvanometerIsOnTheResearchTab()
        {
            yield return StartEra3();
            Assert.That(GameObject.Find("Era3Screen").transform.Find("SafeArea/Column/Pages/ResearchPage/Galvanometer"), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator TheHeaderShowsTheRepute()
        {
            yield return StartEra3();
            yield return new WaitForSecondsRealtime(0.2f);
            var label = GameObject.Find("Era3Screen").transform.Find("SafeArea/Column/Header/Repute/Value");
            Assert.That(label.GetComponent<TMPro.TMP_Text>().text, Is.EqualTo($"{GM.Ledger.Reputation:0.#}"));
        }

        [UnityTest]
        public IEnumerator TheTramwaysCanBeBookedAtAnyHour()
        {
            yield return StartEra3();
            Utils.GameClock.Instance.SetGameTime(Town.StartDay * 86400.0 + 23 * 3600.0);
            Assert.That(Town.IsOffered(GM.FindContract("c3_tramway")), Is.True);
        }

        // --- The goal ---

        private static void LightTheWholeTown()
        {
            Own(ThreeWireId, TransformerId, AlternatorsId, HardCopperId, HighVoltageId, SixKilovoltId);
            foreach (var a in Town.Areas) a.Connected = true;
            Grid.InheritedWatts = 30e6;
            Utils.GameClock.Instance.SetGameTime(Town.StartDay * 86400.0 + 18 * 3600.0);
        }

        [UnityTest]
        public IEnumerator DeliveringTenMegawattsOffersTheCapstone()
        {
            yield return StartEra3();
            LightTheWholeTown();
            Tick(Controllers.GridWorksController.RampSeconds + 2); // newly lit districts come on over half a minute
            Assert.That(Town.CapstoneReady, Is.True);
        }

        [UnityTest]
        public IEnumerator TakingTheCapstoneEndsEra3()
        {
            yield return StartEra3();
            LightTheWholeTown();
            Tick(Controllers.GridWorksController.RampSeconds + 2); // newly lit districts come on over half a minute
            Town.TakeCapstone();
            Assert.That(Managers.EraManager.Instance.Era, Is.EqualTo(4));
        }

        // --- Saves ---

        [UnityTest]
        public IEnumerator ALitDistrictSurvivesSaveAndLoad()
        {
            yield return StartEra3();
            Pay(1_000);
            Town.Connect("d3_bond");
            Save.SaveManager.Instance.Save();
            Save.SaveManager.Instance.Load();
            yield return null;
            Assert.That(Town.Area("d3_bond").Connected, Is.True);
        }

        [UnityTest]
        public IEnumerator CopperSurvivesSaveAndLoad()
        {
            yield return StartEra3();
            Pay(1_000);
            Town.Connect("d3_bond");
            Town.UpgradeCopper("d3_bond");
            Save.SaveManager.Instance.Save();
            Save.SaveManager.Instance.Load();
            yield return null;
            Assert.That(Town.CopperLevel("d3_bond"), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ResearchPointsSurviveSaveAndLoad()
        {
            yield return StartEra3();
            GM.Ledger.AddResearch(42);
            Save.SaveManager.Instance.Save();
            Save.SaveManager.Instance.Load();
            yield return null;
            Assert.That(GM.Ledger.ResearchPoints, Is.EqualTo(42).Within(0.5));
        }

        [Test]
        public void AV7SaveMigratesToV8()
        {
            var v7 = new Save.SaveData { version = 7, loadAreas = null };
            Save.SaveManager.Migrate(v7);
            Assert.That(v7.loadAreas, Is.Not.Null);
        }

        // --- The DEV strip ---

        [UnityTest]
        public IEnumerator TheDevStripJumpsToAnEra()
        {
            GameObject.Find("DevToggle").GetComponent<Button>().onClick.Invoke();
            GameObject.Find("Era3").GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.That(Managers.EraManager.Instance.Era, Is.EqualTo(3));
        }

        [UnityTest]
        public IEnumerator TheDevSpeedRunsTheClockFaster()
        {
            yield return StartEra3();
            double before = Utils.GameClock.Instance.GameTimeSeconds;
            Utils.DevTime.Multiplier = 100f;
            yield return new WaitForSecondsRealtime(0.5f);
            // Half a real second at 100× is about 50 game minutes; at 1× it would be 30 s.
            Assert.That(Utils.GameClock.Instance.GameTimeSeconds - before, Is.GreaterThan(20 * 60));
        }
    }
}
