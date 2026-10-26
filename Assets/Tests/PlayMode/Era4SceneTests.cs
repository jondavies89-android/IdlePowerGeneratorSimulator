using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using IdlePowerGen.Controllers;
using IdlePowerGen.Models;
using IdlePowerGen.Simulation;
using static IdlePowerGen.Data.Era4Data;
using UnitState = IdlePowerGen.Simulation.GridUnit.UnitState;

namespace IdlePowerGen.Tests
{
    // Era 4, the Grid Awakens, in the real scene, from a fresh start at the era's opening.
    public class Era4SceneTests : Era1SceneFixture
    {
        private static Managers.GameManager GM => Managers.GameManager.Instance;
        private static Era4StationController Station => Object.FindAnyObjectByType<Era4StationController>();

        [TearDown]
        public void RestoreTimeScale() => Time.timeScale = 1f;

        private static IEnumerator StartEra4()
        {
            Save.SaveManager.Instance.StartAtEra(EraNumber);
            yield return null;
            yield return null;
            Time.timeScale = 0f;
        }

        private static void Tick(double seconds = 1) => SimulationTick.Instance.CatchUpOfflineAsIfHere(seconds);

        private static GeneratorDefinition CoalUnit => GM.GeneratorCatalogue.First(g => g.generatorType == GeneratorType.CoalUnit);

        private static GridUnit OnlineCoalUnit()
        {
            var u = Station.BuyUnit(CoalUnit);
            Station.EndSync(u, Synchroscope.Close.Clean);
            return u;
        }

        [UnityTest]
        public IEnumerator StartingEra4OpensTheEra4Screen()
        {
            yield return StartEra4();
            Assert.That(GameObject.Find("Era4Screen"), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator Era4InheritsTenMegawatts()
        {
            yield return StartEra4();
            Assert.That(Station.InheritedUnit.InheritedWatts, Is.EqualTo(IdlePowerGen.Data.Era3Data.MaxWatts));
        }

        [UnityTest]
        public IEnumerator ABoughtUnitWaitsToBeSynchronised()
        {
            yield return StartEra4();
            Assert.That(Station.BuyUnit(CoalUnit).State, Is.EqualTo(UnitState.AwaitingSync));
        }

        [UnityTest]
        public IEnumerator ACleanCloseBringsTheUnitOnline()
        {
            yield return StartEra4();
            Assert.That(OnlineCoalUnit().State, Is.EqualTo(UnitState.Online));
        }

        [UnityTest]
        public IEnumerator ASlammedBreakerFaultsTheUnit()
        {
            yield return StartEra4();
            var u = Station.BuyUnit(CoalUnit);
            Station.EndSync(u, Synchroscope.Close.Slammed);
            Assert.That(u.State, Is.EqualTo(UnitState.Faulted));
        }

        [UnityTest]
        public IEnumerator WithReserveTheFrequencyHolds()
        {
            yield return StartEra4();
            Tick(120);
            Assert.That(Station.Frequency, Is.EqualTo(50).Within(0.2));
        }

        // The old station's 10 MW against a fixed load: short by this share.
        private static void Short(double share) =>
            Station.LoadOverrideForTests = IdlePowerGen.Data.Era3Data.MaxWatts * (1 + share);

        [TearDown]
        public void ClearLoad() { if (Station != null) Station.LoadOverrideForTests = null; }

        [UnityTest]
        public IEnumerator ShortOfUnitsTheFrequencyFalls()
        {
            yield return StartEra4();
            Short(0.03);
            Tick(90);
            Assert.That(Station.Frequency, Is.LessThan(49.5));
        }

        [UnityTest]
        public IEnumerator FarShortTheCascadeWarningRuns()
        {
            yield return StartEra4();
            Short(0.06);
            Tick(60);
            Assert.That(Station.CascadeSecondsLeft, Is.GreaterThanOrEqualTo(0));
        }

        [UnityTest]
        public IEnumerator TheCascadeRunningOutDarkensTheGrid()
        {
            yield return StartEra4();
            Short(0.06);
            Tick(200);
            Assert.That(Station.BlackStart, Is.Not.Null);
        }

        // A third over: straight past 48 Hz. The countdown runs fast, but it runs.
        [UnityTest]
        public IEnumerator AFarOverloadStillGivesAWarning()
        {
            yield return StartEra4();
            Short(0.33);
            Tick(8);
            Assert.That(Station.BlackStart, Is.Null);
        }

        [UnityTest]
        public IEnumerator PastTheBrinkTheCountdownRunsFast()
        {
            yield return StartEra4();
            Short(0.33);
            Tick(30);
            Assert.That(Station.BlackStart, Is.Not.Null);
        }

        // Load dropped in time: the frequency's slow climb back doesn't burn the fast clock.
        [UnityTest]
        public IEnumerator DroppingEnoughPastTheBrinkSavesTheGrid()
        {
            yield return StartEra4();
            Short(0.33);
            Tick(10);
            Short(-0.1);
            Tick(60);
            Assert.That(Station.BlackStart, Is.Null);
        }

        [UnityTest]
        public IEnumerator TheStatusStripCountsDownToTheBlackout()
        {
            yield return StartEra4();
            Short(0.06);
            Tick(60);
            yield return new WaitForSecondsRealtime(0.3f);
            var message = GameObject.Find("Era4Screen").GetComponentInChildren<UI.GridStatusView>()
                                    .transform.Find("Message").GetComponent<TMPro.TMP_Text>().text;
            Assert.That(message, Does.Contain("goes dark in"));
        }

        [UnityTest]
        public IEnumerator TheCascadeWarningIsOutsideTheTabs()
        {
            yield return StartEra4();
            var banner = GameObject.Find("Era4Screen").GetComponentInChildren<UI.CascadeBannerView>(true);
            Assert.That(banner.GetComponentsInParent<Transform>(true).Any(t => t.name == "Pages"), Is.False);
        }

        [UnityTest]
        public IEnumerator CatchingUpOnTimeAwayNeverDarkensTheGrid()
        {
            yield return StartEra4();
            Short(0.06);
            SimulationTick.Instance.CatchUpOffline(200);
            Assert.That(Station.BlackStart, Is.Null);
        }

        // --- Maintenance ---

        [UnityTest]
        public IEnumerator AUnitFarPastItsServiceFails()
        {
            yield return StartEra4();
            var u = OnlineCoalUnit();
            u.HoursSinceService = CoalUnit.serviceIntervalHours * 1000;
            Tick();
            Assert.That(u.State, Is.EqualTo(UnitState.Faulted));
        }

        [UnityTest]
        public IEnumerator TheFirstFailureIntroducesMaintenance()
        {
            yield return StartEra4();
            var u = OnlineCoalUnit();
            u.HoursSinceService = CoalUnit.serviceIntervalHours * 1000;
            Tick();
            Assert.That(UnitGridController.IsIntroduced(MaintenanceScore), Is.True);
        }

        [UnityTest]
        public IEnumerator AServiceTakesTheUnitOff()
        {
            yield return StartEra4();
            var u = OnlineCoalUnit();
            Station.Service(u);
            Assert.That(u.State, Is.EqualTo(UnitState.InService));
        }

        [UnityTest]
        public IEnumerator AfterItsServiceAUnitIsAsNew()
        {
            yield return StartEra4();
            var u = OnlineCoalUnit();
            u.HoursSinceService = 100;
            Station.Service(u);
            Tick(ServiceHours * 60 + 2);
            Assert.That(u.HoursSinceService, Is.LessThan(1));
        }

        // --- Coal ---

        [UnityTest]
        public IEnumerator AnEmptyCoalYardTripsTheCoalUnits()
        {
            yield return StartEra4();
            var u = OnlineCoalUnit();
            Station.Area("f4_eastfield").Connected = true;
            Tick(5);
            typeof(UnitGridController).GetProperty("CoalTonnes").SetValue(Station, 0.0);
            Tick();
            Assert.That(u.State, Is.EqualTo(UnitState.NoFuel));
        }

        [UnityTest]
        public IEnumerator OrderingCoalFillsTheYard()
        {
            yield return StartEra4();
            OnlineCoalUnit();
            double before = Station.CoalTonnes;
            Station.OrderCoal();
            Assert.That(Station.CoalTonnes, Is.GreaterThan(before));
        }

        [UnityTest]
        public IEnumerator TheAutoSynchroniserClosesItself()
        {
            yield return StartEra4();
            Own(AutoSyncId);
            var u = Station.BuyUnit(CoalUnit);
            Tick();
            Assert.That(u.State, Is.EqualTo(UnitState.Online));
        }

        private static void Own(params string[] ids) =>
            SetLevels(ids.Select(id => (id, 1)).Concat(GM.Upgrades.Levels.Select(kv => (kv.Key, kv.Value))).ToArray());

        // --- Saves ---

        [UnityTest]
        public IEnumerator UnitsSurviveSaveAndLoad()
        {
            yield return StartEra4();
            OnlineCoalUnit();
            Save.SaveManager.Instance.Save();
            Save.SaveManager.Instance.Load();
            yield return null;
            Assert.That(Station.Owned(GeneratorType.CoalUnit), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator AUnitsStateSurvivesSaveAndLoad()
        {
            yield return StartEra4();
            var u = OnlineCoalUnit();
            Station.Service(u);
            Save.SaveManager.Instance.Save();
            Save.SaveManager.Instance.Load();
            yield return null;
            Assert.That(Station.Units.First(x => !x.Inherited).State, Is.EqualTo(UnitState.InService));
        }

        // --- Black start ---

        private static BlackStartSession Dark()
        {
            Station.ForceBlackout();
            return Station.BlackStart;
        }

        [UnityTest]
        public IEnumerator ABlackoutSwitchesTheContractManagersOff()
        {
            yield return StartEra4();
            SetLevels((ManagerId, 2));
            Station.Manager(0).On = Station.Manager(1).On = true;
            Dark();
            Assert.That(Station.Manager(0).On || Station.Manager(1).On, Is.False);
        }

        [UnityTest]
        public IEnumerator ABlackoutKeepsEachManagersChosenClient()
        {
            yield return StartEra4();
            SetLevels((ManagerId, 1));
            Station.Manager(0).ContractId = "chosen";
            Dark();
            Assert.That(Station.Manager(0).ContractId, Is.EqualTo("chosen"));
        }

        [UnityTest]
        public IEnumerator ABlackoutOpensTheBlackStart()
        {
            yield return StartEra4();
            Dark();
            yield return null;
            Assert.That(GameObject.Find("Era4BlackStart").transform.Find("Panel").gameObject.activeSelf, Is.True);
        }

        [UnityTest]
        public IEnumerator TriageGivesWayToCranking()
        {
            yield return StartEra4();
            var s = Dark();
            s.Tick(BlackStartSession.TriageSeconds + 0.1);
            Assert.That(s.Current, Is.EqualTo(BlackStartSession.Phase.Cranking));
        }

        private static BlackStartSession Cranked()
        {
            var s = Dark();
            s.Tick(BlackStartSession.TriageSeconds + 0.1);
            s.Choose(0);
            for (int i = 0; i < 12; i++) s.Crank();
            return s;
        }

        [UnityTest]
        public IEnumerator CrankingTheDieselStartsAnIsland()
        {
            yield return StartEra4();
            Assert.That(Cranked().Current, Is.EqualTo(BlackStartSession.Phase.Island));
        }

        [UnityTest]
        public IEnumerator TheIslandTakesASubstationItCanCarry()
        {
            yield return StartEra4();
            var s = Cranked();
            Assert.That(s.Energise("f4_oldtown"), Is.True);
        }

        [UnityTest]
        public IEnumerator TooMuchOnTheIslandPullsItDown()
        {
            yield return StartEra4();
            Station.Area("f4_neighbour").Connected = true; // 300 MW: far past a 5 MW diesel
            var s = Cranked();
            s.Energise("f4_neighbour");
            Assert.That(s.Current, Is.EqualTo(BlackStartSession.Phase.Cranking));
        }

        // Two substations on the diesel, then the old station back, then the rest.
        private static BlackStartSession Restored()
        {
            var s = Cranked();
            s.Energise("f4_oldtown");
            s.Energise("f4_oldtown");
            s.IslandHolding(); // no other unit: straight to restoration
            s.BringOnNext();   // the old station
            s.Energise("f4_oldtown");
            s.Energise("f4_oldtown");
            s.FinishRestoration();
            return s;
        }

        [UnityTest]
        public IEnumerator RestoringTheCityEndsInAPostMortem()
        {
            yield return StartEra4();
            Assert.That(Restored().Current, Is.EqualTo(BlackStartSession.Phase.PostMortem));
        }

        [UnityTest]
        public IEnumerator ClosingThePostMortemBringsTheGridBack()
        {
            yield return StartEra4();
            Restored().Close();
            Assert.That(Station.BlackStart, Is.Null);
        }

        [UnityTest]
        public IEnumerator ABlackStartCostsRepute()
        {
            yield return StartEra4();
            float before = GM.Ledger.Reputation;
            Restored().Close();
            Assert.That(GM.Ledger.Reputation, Is.LessThan(before));
        }

        // A blackout for want of units: every feeder lit on the old station's 10 MW. The black
        // start can't get back to 95 %, so it plays out everything the island can do.
        private static BlackStartSession ShortAndPlayedOut()
        {
            foreach (var a in Station.Areas) a.Connected = true;
            var s = Cranked();
            for (int i = 0; i < 500; i++)
            {
                var next = s.LitBefore.FirstOrDefault(s.Fits);
                if (next != null) s.Energise(next);
                else if (s.Current == BlackStartSession.Phase.Island) s.IslandHolding();
                else if (s.CanBringOn) s.BringOnNext();
                else break;
            }
            return s;
        }

        [UnityTest]
        public IEnumerator ShortOfUnitsTheBlackStartCanStillBeFinished()
        {
            yield return StartEra4();
            Assert.That(ShortAndPlayedOut().CanFinish, Is.True);
        }

        [UnityTest]
        public IEnumerator ShortOfUnitsWhatCantBeCarriedStaysDropped()
        {
            yield return StartEra4();
            var s = ShortAndPlayedOut();
            s.FinishRestoration();
            Assert.That(s.LeftDark, Is.Not.Empty);
        }

        [UnityTest]
        public IEnumerator AfterAShortBlackStartTheGridStaysUp()
        {
            yield return StartEra4();
            var s = ShortAndPlayedOut();
            s.FinishRestoration();
            s.Close();
            Tick(150);
            Assert.That(Station.BlackStart, Is.Null);
        }

        [UnityTest]
        public IEnumerator ASecondUnitRunsItsStartSequence()
        {
            yield return StartEra4();
            OnlineCoalUnit();
            var s = Cranked();
            s.Energise("f4_oldtown");
            s.IslandHolding();
            Assert.That(s.Current, Is.EqualTo(BlackStartSession.Phase.SecondUnit));
        }
    }
}
