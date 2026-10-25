using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using IdlePowerGen.Controllers;
using IdlePowerGen.Models;
using IdlePowerGen.Simulation;
using static IdlePowerGen.Data.Era5Data;

namespace IdlePowerGen.Tests
{
    // Era 5, the Interconnected Grid, in the real scene, from a fresh start at its opening.
    public class Era5SceneTests : Era1SceneFixture
    {
        private static Managers.GameManager GM => Managers.GameManager.Instance;
        private static Era5DispatchController Dispatch => Object.FindAnyObjectByType<Era5DispatchController>();

        [TearDown]
        public void Restore()
        {
            Time.timeScale = 1f;
            if (Dispatch != null) Dispatch.LoadOverrideForTests = null;
        }

        private static IEnumerator StartEra5()
        {
            Save.SaveManager.Instance.StartAtEra(EraNumber);
            yield return null;
            yield return null;
            Time.timeScale = 0f;
        }

        private static void Tick(double seconds = 1) => SimulationTick.Instance.CatchUpOfflineAsIfHere(seconds);

        private static void Pay(double pounds) => GM.Ledger.AddPennies(Managers.Money.FromPounds(pounds));

        [UnityTest]
        public IEnumerator StartingEra5OpensTheEra5Screen()
        {
            yield return StartEra5();
            Assert.That(GameObject.Find("Era5Screen"), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator Era5InheritsAGigawatt()
        {
            yield return StartEra5();
            Assert.That(Dispatch.InheritedUnit.InheritedWatts, Is.EqualTo(IdlePowerGen.Data.Era4Data.MaxWatts));
        }

        [UnityTest]
        public IEnumerator Era5OpensWithBirminghamAndCoventry()
        {
            yield return StartEra5();
            Assert.That(Dispatch.Areas.Count(a => a.Connected), Is.EqualTo(2));
        }

        // --- Reactive power ---

        private static IEnumerator WithLeicester()
        {
            yield return StartEra5();
            Pay(10_000_000);
            Dispatch.Connect("c5_leicester");
            Tick();
        }

        [UnityTest]
        public IEnumerator LeicestersMillsSagItsVoltage()
        {
            yield return WithLeicester();
            Assert.That(Dispatch.Voltage("c5_leicester"), Is.LessThan(GoodVoltage));
        }

        [UnityTest]
        public IEnumerator ACapacitorBankRaisesTheVoltage()
        {
            yield return WithLeicester();
            double before = Dispatch.Voltage("c5_leicester");
            Dispatch.AddBank("c5_leicester");
            Tick();
            Assert.That(Dispatch.Voltage("c5_leicester"), Is.GreaterThan(before));
        }

        [UnityTest]
        public IEnumerator ACapacitorBankRaisesThePowerFactor()
        {
            yield return WithLeicester();
            double before = Dispatch.PowerFactor("c5_leicester");
            Dispatch.AddBank("c5_leicester");
            Assert.That(Dispatch.PowerFactor("c5_leicester"), Is.GreaterThan(before));
        }

        [UnityTest]
        public IEnumerator APoorPowerFactorRaisesTheLosses()
        {
            yield return WithLeicester();
            var leicester = Dispatch.Area("c5_leicester");
            double withMills = TransmissionNetwork.EffectiveLoss(leicester);
            Assert.That(withMills, Is.GreaterThan(leicester.LossPerWatt));
        }

        [UnityTest]
        public IEnumerator ReactivePowerEatsIntoTheUnitsRating()
        {
            yield return WithLeicester();
            Assert.That(Dispatch.Grid.CapacityFactor, Is.LessThan(1));
        }

        [UnityTest]
        public IEnumerator AConnectedCityPaysLessWhenItsVoltageSags()
        {
            yield return WithLeicester();
            // At peak, with no bank: under 0.95 per unit.
            Assert.That(Dispatch.Voltage("c5_leicester"), Is.LessThan(GoodVoltage));
        }

        [UnityTest]
        public IEnumerator BanksSurviveSaveAndLoad()
        {
            yield return WithLeicester();
            Dispatch.AddBank("c5_leicester");
            Save.SaveManager.Instance.Save();
            Save.SaveManager.Instance.Load();
            yield return null;
            Assert.That(Dispatch.Banks("c5_leicester"), Is.EqualTo(1));
        }

        // --- Brownouts and redundancy ---

        private static void Short(double share) =>
            Dispatch.LoadOverrideForTests = IdlePowerGen.Data.Era4Data.MaxWatts * (1 + share);

        [UnityTest]
        public IEnumerator TheRelaysShedLoadUnderFortyNinePointFive()
        {
            yield return StartEra5();
            Short(0.04);
            Tick(60);
            Assert.That(Dispatch.SheddingStage, Is.GreaterThan(0));
        }

        [UnityTest]
        public IEnumerator TheFirstBrownoutIntroducesRedundancy()
        {
            yield return StartEra5();
            Short(0.04);
            Tick(60);
            Assert.That(UnitGridController.IsIntroduced(RedundancyScore), Is.True);
        }

        [UnityTest]
        public IEnumerator FittingAUnitForBlackStartRaisesRedundancy()
        {
            yield return StartEra5();
            Pay(20_000_000);
            var unit = Dispatch.BuyUnit(GM.GeneratorCatalogue.First(g => g.generatorType == GeneratorType.LargeCoalUnit));
            Dispatch.EndSync(unit, Synchroscope.Close.Clean);
            SetLevels((BlackStartId, 1));
            double before = Dispatch.RedundancyScore;
            Dispatch.FitBlackStart();
            Assert.That(Dispatch.RedundancyScore, Is.GreaterThan(before));
        }

        // --- The end ---

        [UnityTest]
        public IEnumerator TheLastCapstoneShowsTheEndCard()
        {
            yield return StartEra5();
            foreach (var a in Dispatch.Areas) a.Connected = true;
            typeof(GridWorksController).GetProperty("CapstoneReady").SetValue(Dispatch, true);
            Dispatch.TakeCapstone();
            yield return null;
            Assert.That(GameObject.Find("Era6TitleCard").transform.Find("Panel").gameObject.activeSelf, Is.True);
        }

        [UnityTest]
        public IEnumerator TheLastCapstoneKeepsTheEra()
        {
            yield return StartEra5();
            typeof(GridWorksController).GetProperty("CapstoneReady").SetValue(Dispatch, true);
            Dispatch.TakeCapstone();
            Assert.That(Managers.EraManager.Instance.Era, Is.EqualTo(5));
        }
    }
}
