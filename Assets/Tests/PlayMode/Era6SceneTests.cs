using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using IdlePowerGen.Controllers;
using IdlePowerGen.Models;
using IdlePowerGen.Simulation;
using static IdlePowerGen.Data.Era6Data;
using UnitMode = IdlePowerGen.Simulation.GridUnit.UnitMode;
using UnitState = IdlePowerGen.Simulation.GridUnit.UnitState;

namespace IdlePowerGen.Tests
{
    // Era 6, the Nuclear Age, in the real scene, from a fresh start at its opening.
    public class Era6SceneTests : Era1SceneFixture
    {
        private static Managers.GameManager GM => Managers.GameManager.Instance;
        private static Era6NationalGridController National => Object.FindAnyObjectByType<Era6NationalGridController>();

        [TearDown]
        public void Restore()
        {
            Time.timeScale = 1f;
            if (National != null) National.LoadOverrideForTests = null;
        }

        private static IEnumerator StartEra6()
        {
            Save.SaveManager.Instance.StartAtEra(EraNumber);
            yield return null;
            yield return null;
            Time.timeScale = 0f;
            // A steady load well inside the old fleet, whatever the hour.
            National.LoadOverrideForTests = 30e9;
        }

        private static void Tick(double seconds = 1) => SimulationTick.Instance.CatchUpOfflineAsIfHere(seconds);

        private static void Pay(double pounds) => GM.Ledger.AddPennies(Managers.Money.FromPounds(pounds));

        // Learns or buys these on top of everything already owned (earlier eras' research included).
        private static void Own(params string[] ids)
        {
            var levels = new Dictionary<string, int>(GM.Upgrades.Levels);
            foreach (var id in ids) levels[id] = 1;
            GM.Upgrades.LoadFromSave(levels);
            Utils.EventBus.Publish(new GameLoadedEvent());
        }

        private static GridUnit Build(GeneratorType type)
        {
            Pay(5_000_000_000);
            return National.BuyUnit(GM.GeneratorCatalogue.First(g => g.generatorType == type));
        }

        // Built and run up to the bars (its start time, at a game minute a real second).
        private static GridUnit Running(GeneratorType type)
        {
            var unit = Build(type);
            Tick(unit.Definition.startHours * 60 + 3);
            return unit;
        }

        // Just synchronised: on the bars, still climbing to full.
        private static GridUnit Reactor()
        {
            Own(PwrId, LicenceId);
            return Running(GeneratorType.PressurisedWater);
        }

        // The grid restarted at this load: every unit straight to where the dispatch wants it,
        // with no ramping on the way (a reactor climbing at a 30 GW load would trip itself first).
        private static void SettleAt(double watts)
        {
            National.LoadOverrideForTests = watts;
            National.Grid.Reset();
            Tick();
        }

        [UnityTest]
        public IEnumerator StartingEra6OpensTheEra6Screen()
        {
            yield return StartEra6();
            Assert.That(GameObject.Find("Era6Screen"), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator Era6InheritsFiftyGigawatts()
        {
            yield return StartEra6();
            Assert.That(National.InheritedUnit.InheritedWatts, Is.EqualTo(IdlePowerGen.Data.Era5Data.MaxWatts));
        }

        [UnityTest]
        public IEnumerator Era6OpensWithTheMidlandsAndSouthWales()
        {
            yield return StartEra6();
            Assert.That(National.Areas.Count(a => a.Connected), Is.EqualTo(2));
        }

        // --- Big machines are slow ---

        [UnityTest]
        public IEnumerator ANewUnitStartsBeforeItCarriesLoad()
        {
            yield return StartEra6();
            Assert.That(Build(GeneratorType.CoalStation6).State, Is.EqualTo(UnitState.Starting));
        }

        [UnityTest]
        public IEnumerator ACoalStationIsOnTheBarsAfterItsStartTime()
        {
            yield return StartEra6();
            Assert.That(Running(GeneratorType.CoalStation6).IsOnline, Is.True);
        }

        [UnityTest]
        public IEnumerator ACoalStationIsStillStartingHalfWayThrough()
        {
            yield return StartEra6();
            var unit = Build(GeneratorType.CoalStation6);
            Tick(unit.Definition.startHours * 30);
            Assert.That(unit.State, Is.EqualTo(UnitState.Starting));
        }

        [UnityTest]
        public IEnumerator AReactorCantGoOnAgc()
        {
            yield return StartEra6();
            Own(AgcId);
            Assert.That(National.CanSetMode(Reactor(), UnitMode.Agc), Is.False);
        }

        [UnityTest]
        public IEnumerator WithoutAgcResearchANewCoalStationRunsOnBase()
        {
            yield return StartEra6();
            Assert.That(Build(GeneratorType.CoalStation6).Mode, Is.EqualTo(UnitMode.Base));
        }

        [UnityTest]
        public IEnumerator AUnitOnBaseRunsFlatOutWhateverTheLoad()
        {
            yield return StartEra6();
            var coal = Running(GeneratorType.CoalStation6);
            National.LoadOverrideForTests = 20e9;
            Tick(120);
            Assert.That(coal.OutputWatts, Is.EqualTo(coal.Capacity(SimulationTick.Instance.Grid)).Within(1e6));
        }

        [UnityTest]
        public IEnumerator AUnitOnAgcFollowsTheLoadDown()
        {
            yield return StartEra6();
            Own(AgcId);
            var ccgt = Running(GeneratorType.CoalStation6);
            // Cheapest to run, so it is loaded before the old fleet: give it less than it could carry.
            National.LoadOverrideForTests = 6e9;
            Tick(120);
            Assert.That(ccgt.OutputWatts, Is.LessThan(ccgt.Capacity(SimulationTick.Instance.Grid) * 0.7));
        }

        [UnityTest]
        public IEnumerator AUnitOnAgcMustStillSendItsMinimum()
        {
            yield return StartEra6();
            Own(AgcId);
            var coal = Running(GeneratorType.CoalStation6);
            Assert.That(National.FloorWatts, Is.EqualTo(coal.Capacity(SimulationTick.Instance.Grid) * coal.Definition.minStableShare).Within(1e6));
        }

        [UnityTest]
        public IEnumerator AUnitMovesNoFasterThanItsRampRate()
        {
            yield return StartEra6();
            Own(AgcId);
            var coal = Running(GeneratorType.CoalStation6);
            National.LoadOverrideForTests = 5e9;
            Tick(120);
            double before = coal.Scheduled;
            National.LoadOverrideForTests = 45e9;
            Tick(1);
            double cap = coal.Capacity(SimulationTick.Instance.Grid);
            // Its scheduled output; the governor's primary response comes on top, within a band.
            Assert.That(coal.Scheduled - before, Is.LessThanOrEqualTo(coal.Definition.rampPerMinute * cap * 1.001));
        }

        [UnityTest]
        public IEnumerator TheMeritOrderLoadsTheCheaperUnitFirst()
        {
            yield return StartEra6();
            Own(AgcId);
            var coal = Running(GeneratorType.CoalStation6);
            // Coal runs at £70 a MWh, the old fleet at £90: coal is full before the fleet takes the rest.
            National.LoadOverrideForTests = 30e9;
            Tick(120);
            Assert.That(coal.OutputWatts, Is.EqualTo(coal.Capacity(SimulationTick.Instance.Grid)).Within(1e7));
        }

        // --- The night trough and over-frequency ---

        private static IEnumerator TooMuchOnBase()
        {
            yield return StartEra6();
            Reactor();
            // A night's load under what the reactor sends flat out.
            SettleAt(40e9);
        }

        [UnityTest]
        public IEnumerator TooMuchOnBaseRaisesTheFrequency()
        {
            yield return TooMuchOnBase();
            Tick(5);
            Assert.That(National.Frequency, Is.GreaterThan(HighHz));
        }

        [UnityTest]
        public IEnumerator HeldOverFiftyOneHertzProtectionTripsTheReactor()
        {
            yield return TooMuchOnBase();
            var reactor = National.Units.First(u => !u.Inherited);
            Tick(TripSeconds + 10);
            Assert.That(reactor.State, Is.EqualTo(UnitState.Standby));
        }

        [UnityTest]
        public IEnumerator ATrippedReactorCantRestartForADay()
        {
            yield return TooMuchOnBase();
            var reactor = National.Units.First(u => !u.Inherited);
            Tick(TripSeconds + 10);
            Assert.That(National.CanStart(reactor), Is.False);
        }

        [UnityTest]
        public IEnumerator StandingAUnitDownTakesItOffTheBars()
        {
            yield return StartEra6();
            var coal = Running(GeneratorType.CoalStation6);
            National.StandDown(coal);
            Assert.That(coal.IsOnline, Is.False);
        }

        [UnityTest]
        public IEnumerator AReactorScramsInABlackout()
        {
            yield return StartEra6();
            var reactor = Reactor();
            National.ForceBlackout();
            Assert.That(reactor.HoursLeft, Is.EqualTo(ReactorHoldHours));
        }

        // --- Fuel, reserve, automation ---

        [UnityTest]
        public IEnumerator GasIsPaidForAsItBurns()
        {
            yield return StartEra6();
            Own(AgcId, GasTurbinesId, CombinedId);
            Running(GeneratorType.CombinedCycle);
            Tick(5);
            Assert.That(National.FuelPoundsPerHour, Is.GreaterThan(0));
        }

        [UnityTest]
        public IEnumerator TheReserveIsLowWhenTheBiggestUnitOutweighsIt()
        {
            yield return StartEra6();
            Reactor();
            // 60 GW on BASE and the old fleet near full: nothing could cover the reactor tripping.
            SettleAt(105e9);
            Assert.That(National.ReserveLow, Is.True);
        }

        [UnityTest]
        public IEnumerator TheCommitmentComputerStartsAUnitForThePeak()
        {
            yield return StartEra6();
            Own(AgcId, CommitmentId);
            var coal = Running(GeneratorType.CoalStation6);
            National.StandDown(coal);
            // Connected regions large enough that the evening peak outruns the old fleet.
            Pay(1_000_000_000);
            National.Connect("r6_north_wales");
            Tick(2);
            Assert.That(coal.State, Is.Not.EqualTo(UnitState.Standby));
        }

        [UnityTest]
        public IEnumerator EconomySevenLowersTheEveningPeak()
        {
            yield return StartEra6();
            double before = National.PeakAhead(24).Watts;
            Own(Economy7Id);
            Assert.That(National.PeakAhead(24).Watts, Is.LessThan(before));
        }

        [UnityTest]
        public IEnumerator ModesSurviveSaveAndLoad()
        {
            yield return StartEra6();
            Own(AgcId);
            var coal = Running(GeneratorType.CoalStation6);
            National.SetMode(coal, UnitMode.Base);
            Save.SaveManager.Instance.Save();
            Save.SaveManager.Instance.Load();
            yield return null;
            Assert.That(National.Units.First(u => !u.Inherited).Mode, Is.EqualTo(UnitMode.Base));
        }

        // --- Operator training and drills ---

        private static IEnumerator Trained()
        {
            yield return StartEra6();
            GM.Ledger.AddResearch(1_000_000);
            GM.Upgrades.Purchase(Upgrade(TrainingId));
        }

        [UnityTest]
        public IEnumerator LearningOperatorTrainingIntroducesTheScore()
        {
            yield return Trained();
            Assert.That(UnitGridController.IsIntroduced(TrainingScore), Is.True);
        }

        [UnityTest]
        public IEnumerator TrainedOperatorsCloseCleanOverAWiderWindow()
        {
            yield return Trained();
            Assert.That(National.SyncForgiveness, Is.GreaterThan(1));
        }

        [UnityTest]
        public IEnumerator TrainedOperatorsGetALongerCascadeWarning()
        {
            yield return Trained();
            // A little more than the old fleet can carry: the frequency sinks until the warning starts.
            National.LoadOverrideForTests = 56e9;
            for (int i = 0; i < 120 && National.CascadeSecondsLeft < 0; i++) Tick();
            Assert.That(National.CascadeSecondsLeft, Is.GreaterThan(IdlePowerGen.Data.Era4Data.CascadeWarningSeconds));
        }

        private static IEnumerator Drilled()
        {
            yield return Trained();
            Pay(DrillPounds);
            National.StartDrill();
        }

        [UnityTest]
        public IEnumerator ADrillTakesTheGridDown()
        {
            yield return Drilled();
            Assert.That(National.BlackStart, Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator ADrillsStepsNeverFail()
        {
            yield return Drilled();
            Assert.That(National.BlackStart.StepFailureChance, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator ADrillHoldsTheContracts()
        {
            yield return Drilled();
            Tick();
            Assert.That(GM.Contracts.Held, Is.True);
        }

        // Straight to the drill's post-mortem and closed.
        private static void FinishDrill()
        {
            var session = National.BlackStart;
            typeof(BlackStartSession).GetMethod("Go", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Invoke(session, new object[] { BlackStartSession.Phase.PostMortem });
            session.Close();
        }

        [UnityTest]
        public IEnumerator AFinishedDrillRaisesOperatorTraining()
        {
            yield return Drilled();
            double before = National.TrainingScore;
            FinishDrill();
            Assert.That(National.TrainingScore, Is.GreaterThan(before));
        }

        [UnityTest]
        public IEnumerator ADrillCostsNoRepute()
        {
            yield return Drilled();
            float before = GM.Ledger.Reputation;
            FinishDrill();
            Assert.That(GM.Ledger.Reputation, Is.EqualTo(before));
        }

        [UnityTest]
        public IEnumerator AFinishedDrillReleasesTheContracts()
        {
            yield return Drilled();
            FinishDrill();
            Tick();
            Assert.That(GM.Contracts.Held, Is.False);
        }

        // --- The end ---

        [UnityTest]
        public IEnumerator TheLastCapstoneShowsTheEndCard()
        {
            yield return StartEra6();
            typeof(GridWorksController).GetProperty("CapstoneReady").SetValue(National, true);
            National.TakeCapstone();
            yield return null;
            Assert.That(GameObject.Find("Era7TitleCard").transform.Find("Panel").gameObject.activeSelf, Is.True);
        }

        [UnityTest]
        public IEnumerator TheLastCapstoneKeepsTheEra()
        {
            yield return StartEra6();
            typeof(GridWorksController).GetProperty("CapstoneReady").SetValue(National, true);
            National.TakeCapstone();
            Assert.That(Managers.EraManager.Instance.Era, Is.EqualTo(6));
        }
    }
}
