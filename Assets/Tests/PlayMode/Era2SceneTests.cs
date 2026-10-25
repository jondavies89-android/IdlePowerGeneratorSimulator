using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using IdlePowerGen.Models;
using static IdlePowerGen.Data.Era2Data;

namespace IdlePowerGen.Tests
{
    // The Era 2 screen in the real scene, reached by loading an Era 2 save (or, once, by
    // finishing Era 1). One behaviour per test.
    public class Era2SceneTests : Era1SceneFixture
    {
        private static Managers.GameManager GM => Managers.GameManager.Instance;
        private static Controllers.Era2WorksController Works =>
            UnityEngine.Object.FindAnyObjectByType<Controllers.Era2WorksController>();
        private static Controllers.Era2GeneratorController Generators =>
            UnityEngine.Object.FindAnyObjectByType<Controllers.Era2GeneratorController>();

        private const double Day = 86400, Hour = 3600;

        [TearDown]
        public void RestoreTimeScale() => Time.timeScale = 1f;

        private float? _streetRate;

        [TearDown]
        public void RestoreStreet()
        {
            if (_streetRate is float rate) GM.GetEraConfig(EraNumber).streetRateFactor = rate;
            _streetRate = null;
        }

        // The street buys more from an apprentice than his wage, so a purse only runs dry,
        // and wages only show, with the street shut.
        private void NoStreet()
        {
            var cfg = GM.GetEraConfig(EraNumber);
            _streetRate ??= cfg.streetRateFactor;
            cfg.streetRateFactor = 0f;
        }

        private static string SavePath =>
            Path.Combine(Application.persistentDataPath, Save.SaveManager.SaveFileName);

        // An Era 2 save, Era 2's first day at the given hour, then loaded as on opening the game.
        private static Save.SaveData Era2Save(decimal pennies, float reputation = 0f, float hour = StartHour,
                                              params (GeneratorType type, int count)[] generators)
        {
            var data = new Save.SaveData
            {
                version           = Save.SaveManager.CurrentVersion,
                saveTimestampUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                pennies           = pennies.ToString(System.Globalization.CultureInfo.InvariantCulture),
                reputation        = reputation,
                currentEra        = 2f,
                gameTimeSeconds   = Day + hour * Hour,
                era2StartDay      = 1,
            };
            foreach (var (type, count) in generators)
                data.generators.Add(new Save.SaveData.GeneratorEntry { generatorType = type.ToString(), count = count });
            return data;
        }

        private static IEnumerator Load(Save.SaveData data)
        {
            File.WriteAllText(SavePath, JsonUtility.ToJson(data));
            Save.SaveManager.Instance.Load();
            yield return null;
            yield return null; // the Era 2 screen's Start
        }

        private static IEnumerator Tick() { yield return new WaitForSeconds(1.1f); }

        private static ContractDefinition Contract(string id) => GM.FindContract(id);
        private static GeneratorDefinition Machine(GeneratorType type) =>
            GM.GeneratorCatalogue.First(g => g.generatorType == type);

        private static void Click(string name) => GameObject.Find(name).GetComponent<Button>().onClick.Invoke();

        // --- The way in ---

        [UnityTest]
        public IEnumerator FinishingEra1OpensTheEra2Screen()
        {
            GM.Ledger.AddJoules(500);
            SetLevels((Data.Era1Data.BetterCrankId, 1), (Data.Era1Data.IronFlywheelId, 1),
                      (Data.Era1Data.WaterWheelId, 1), (Data.Era1Data.BeltsGearingId, 5));
            yield return Tick();
            UnityEngine.Object.FindAnyObjectByType<Controllers.Era1FirstCustomerController>().Accept();
            Time.timeScale = 20f;
            float deadline = Time.realtimeSinceStartup + 15f;
            while (Managers.EraManager.Instance.CurrentEra < 2f && Time.realtimeSinceStartup < deadline)
                yield return null;
            Time.timeScale = 1f;
            yield return null;
            Assert.That(GameObject.Find("Era2Screen"), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator AnEra2SaveOpensOnTheOfficeNotTheWorkshop()
        {
            yield return Load(Era2Save(0));
            Assert.That(GameObject.Find("Era1Screen"), Is.Null);
        }

        [Test]
        public void Era2StartsInTheEvening()
        {
            Utils.EventBus.Publish(new EraAdvancedEvent(1, 2));
            Assert.That(Economy.ContractBoard.HourOf(Utils.GameClock.Instance.GameTimeSeconds), Is.EqualTo(StartHour));
        }

        [Test]
        public void Era2StartsOnTheNinthOfOctober1851()
        {
            Utils.EventBus.Publish(new EraAdvancedEvent(1, 2));
            Assert.That(Works.Date, Is.EqualTo(new DateTime(1851, 10, 9)));
        }

        [Test]
        public void Era2DoesNotWindTheClockBack()
        {
            Utils.GameClock.Instance.SetGameTime(3 * Day + 20 * Hour);
            Utils.EventBus.Publish(new EraAdvancedEvent(1, 2));
            Assert.That(Utils.GameClock.Instance.GameTimeSeconds, Is.GreaterThan(3 * Day + 20 * Hour));
        }

        // --- Machines ---

        [UnityTest]
        public IEnumerator BuyingAnApprenticeCostsFiftyPence()
        {
            yield return Load(Era2Save(100));
            Click("Generator_SmallDynamo");
            Assert.That(GM.Ledger.Pennies, Is.EqualTo(50m));
        }

        [UnityTest]
        public IEnumerator AnApprenticeAddsFiftyWatts()
        {
            yield return Load(Era2Save(100));
            yield return Tick();
            double before = Simulation.SimulationTick.Instance.Grid.TotalOutputWatts;
            Click("Generator_SmallDynamo");
            yield return Tick();
            Assert.That(Simulation.SimulationTick.Instance.Grid.TotalOutputWatts - before, Is.EqualTo(50).Within(1e-6));
        }

        [UnityTest]
        public IEnumerator TheSecondApprenticeCostsFifteenPercentMore()
        {
            yield return Load(Era2Save(100, 0, StartHour, (GeneratorType.SmallDynamo, 1)));
            Assert.That(Generators.NextCostPennies(Machine(GeneratorType.SmallDynamo)), Is.EqualTo(58m)); // ⌈57.5⌉
        }

        // The Era 1 crank, its pops and its click are hidden with the workshop screen.
        [UnityTest]
        public IEnumerator Era1MilestonesStayQuietInEra2()
        {
            yield return Load(Era2Save(0));
            int fired = 0;
            void Count(MilestoneReachedEvent _) => fired++;
            Utils.EventBus.Subscribe<MilestoneReachedEvent>(Count);
            GM.Ledger.AddJoules(1000);
            Utils.EventBus.Unsubscribe<MilestoneReachedEvent>(Count);
            Assert.That(fired, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator TheStatsPanelKeepsItsHeight()
        {
            yield return Load(Era2Save(0));
            yield return null;
            Assert.That(((RectTransform)GameObject.Find("Era2Screen/SafeArea/Column/Stats").transform).rect.height,
                        Is.EqualTo(86).Within(0.5f));
        }

        [UnityTest]
        public IEnumerator TheOldWorkshopRowTurnsTheCrank()
        {
            yield return Load(Era2Save(0));
            double before = GM.Ledger.Joules;
            Click("Workshop");
            yield return null;
            Assert.That(GM.Ledger.Joules, Is.GreaterThan(before));
        }

        [UnityTest]
        public IEnumerator SteamEnginesAreHiddenUntilThePatent()
        {
            yield return Load(Era2Save(0));
            yield return new WaitForSeconds(0.3f);
            Assert.That(GameObject.Find("Generator_SteamEngine"), Is.Null);
        }

        [UnityTest]
        public IEnumerator WagesComeOutOfThePurse()
        {
            NoStreet();
            yield return Load(Era2Save(100, 0, StartHour, (GeneratorType.SmallDynamo, 6)));
            yield return Tick();
            // Six apprentices at 1 d a game hour: 0.1 d a real second.
            Assert.That(GM.Ledger.Pennies, Is.LessThan(100m));
        }

        [UnityTest]
        public IEnumerator AnEmptyPurseStopsTheApprentices()
        {
            NoStreet();
            yield return Load(Era2Save(0, 0, StartHour, (GeneratorType.SmallDynamo, 2)));
            yield return Tick();
            Assert.That(Generators.CountOnline(GeneratorType.SmallDynamo), Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator PayingThePurseBringsTheApprenticesBack()
        {
            NoStreet();
            yield return Load(Era2Save(0, 0, StartHour, (GeneratorType.SmallDynamo, 2)));
            yield return Tick();
            GM.Ledger.AddPennies(10);
            yield return Tick();
            Assert.That(Generators.CountOnline(GeneratorType.SmallDynamo), Is.EqualTo(2));
        }

        // --- Coal ---

        private static Save.SaveData WithSteam(decimal pennies, double coal)
        {
            var data = Era2Save(pennies, 3, StartHour, (GeneratorType.SteamEngine, 1));
            data.upgradeLevels.Add(new Save.SaveData.UpgradeLevelEntry { upgradeId = WattPatentId, level = 1 });
            data.coalLevel = coal;
            return data;
        }

        [UnityTest]
        public IEnumerator AnEmptyHopperStopsTheSteamEngines()
        {
            yield return Load(WithSteam(0, 0));
            yield return Tick();
            Assert.That(Generators.CountOnline(GeneratorType.SteamEngine), Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator OrderingCoalRestartsTheSteamEngines()
        {
            yield return Load(WithSteam(100, 0));
            yield return Tick();
            Click("Order");
            Assert.That(Generators.CountOnline(GeneratorType.SteamEngine), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator OrderingCoalChargesTheMissingCoal()
        {
            yield return Load(WithSteam(100, 0));
            yield return Tick();
            decimal before = GM.Ledger.Pennies;
            Click("Order");
            Assert.That(before - GM.Ledger.Pennies, Is.EqualTo((decimal)Economy.Era2CoalHopper.MinCapacity));
        }

        [UnityTest]
        public IEnumerator TheHopperWarnsWhenLow()
        {
            yield return Load(WithSteam(100, 2)); // 2 d of a 24 d hopper
            yield return null;
            var title = GameObject.Find("HopperPanel").transform.Find("Texts/Title").GetComponent<TMPro.TMP_Text>();
            Assert.That(title.text, Is.EqualTo("Coal hopper running low"));
        }

        [UnityTest]
        public IEnumerator TheCoalMerchantOrdersWhenLow()
        {
            var data = WithSteam(100, 2);
            data.upgradeLevels.Add(new Save.SaveData.UpgradeLevelEntry { upgradeId = CoalMerchantId, level = 1 });
            yield return Load(data);
            yield return Tick();
            Assert.That(Works.Hopper.Fraction(Works.CoalBurnCapacityBasis), Is.GreaterThan(0.9f));
        }

        // --- Contracts ---

        [UnityTest]
        public IEnumerator TappingAnOfferBooksIt()
        {
            yield return Load(Era2Save(0));
            yield return new WaitForSeconds(0.3f);
            Click("Offer_c2_street_lamps");
            Assert.That(GM.Contracts.ActiveContracts.Select(c => c.Definition.contractId), Does.Contain("c2_street_lamps"));
        }

        [UnityTest]
        public IEnumerator ABookedOfferShowsAnEngagementCard()
        {
            yield return Load(Era2Save(0));
            Works.Book(Contract("c2_street_lamps"));
            yield return null;
            Assert.That(GameObject.Find("Engagement_0"), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator TheBakeryIsNotOfferedInTheEvening()
        {
            yield return Load(Era2Save(0));
            Assert.That(Works.IsOffered(Contract("c2_bakery")), Is.False);
        }

        [UnityTest]
        public IEnumerator AThirdEngagementWaitsForTelegraphLines()
        {
            yield return Load(Era2Save(0, 6));
            Works.Book(Contract("c2_street_lamps"));
            Works.Book(Contract("c2_hotel"));
            Assert.That(Works.Book(Contract("c2_theatre")), Is.False);
        }

        [UnityTest]
        public IEnumerator TelegraphLinesAddAnEngagement()
        {
            var data = Era2Save(0, 6);
            data.upgradeLevels.Add(new Save.SaveData.UpgradeLevelEntry { upgradeId = TelegraphId, level = 1 });
            yield return Load(data);
            Assert.That(Works.Slots, Is.EqualTo(BaseContractSlots + 1));
        }

        [UnityTest]
        public IEnumerator TheClerkBooksWorkTheWorksCanSupply()
        {
            var data = Era2Save(10, 4, StartHour, (GeneratorType.SmallDynamo, 20)); // 1 kW and more
            data.upgradeLevels.Add(new Save.SaveData.UpgradeLevelEntry { upgradeId = ClerkId, level = 1 });
            yield return Load(data);
            yield return Tick();
            yield return Tick();
            Assert.That(GM.Contracts.ActiveContracts, Is.Not.Empty);
        }

        [UnityTest]
        public IEnumerator AHeldEngagementPaysAtTheEra2Rate()
        {
            // Twelve apprentices (600 W) hold the 500 W street lamps for their five hours.
            yield return Load(Era2Save(1000, 0, StartHour, (GeneratorType.SmallDynamo, 12)));
            Works.Book(Contract("c2_street_lamps"));
            decimal paid = 0;
            void Record(ContractCompletedEvent e) => paid = e.PenniesEarned;
            Utils.EventBus.Subscribe<ContractCompletedEvent>(Record);
            Time.timeScale = 50f;
            float deadline = Time.realtimeSinceStartup + 20f;
            while (paid == 0 && Time.realtimeSinceStartup < deadline) yield return null;
            Utils.EventBus.Unsubscribe<ContractCompletedEvent>(Record);
            Assert.That(paid, Is.EqualTo((decimal)(5 * 500 * ContractBaseRate)).Within(1m));
        }

        [UnityTest]
        public IEnumerator AnEngagementPaysWhileItRuns()
        {
            yield return Load(Era2Save(1000, 0, StartHour, (GeneratorType.SmallDynamo, 12)));
            Works.Book(Contract("c2_street_lamps"));
            yield return Tick();
            Assert.That(GM.Contracts.ActiveContracts[0].PaidSoFar, Is.GreaterThan(0m));
        }

        [UnityTest]
        public IEnumerator TheStreetBuysPowerNoClientIsTaking()
        {
            yield return Load(Era2Save(0, 0, StartHour, (GeneratorType.Waterwheel, 1)));
            yield return Tick();
            Assert.That(GM.Ledger.Pennies, Is.GreaterThan(0m));
        }

        [UnityTest]
        public IEnumerator TheStreetOnlyGetsWhatTheClientsLeave()
        {
            yield return Load(Era2Save(1000, 0, StartHour, (GeneratorType.SmallDynamo, 12)));
            Works.Book(Contract("c2_street_lamps"));
            yield return Tick();
            Assert.That(GM.Street.LastWatts, Is.EqualTo(100).Within(1e-6)); // 600 W made, 500 W booked
        }

        [UnityTest]
        public IEnumerator SoldPowerIsNotAlsoBankedAsJoules()
        {
            yield return Load(Era2Save(0, 0, StartHour, (GeneratorType.Waterwheel, 1)));
            yield return Tick();
            double before = GM.Ledger.Joules;
            yield return Tick();
            Assert.That(GM.Ledger.Joules, Is.EqualTo(before));
        }

        [UnityTest]
        public IEnumerator EngagementsTakeTheWattsTheyPayFor()
        {
            yield return Load(Era2Save(1000, 0, StartHour, (GeneratorType.SmallDynamo, 12)));
            Works.Book(Contract("c2_street_lamps"));
            yield return Tick();
            Assert.That(GM.Contracts.DeliveredWatts, Is.EqualTo(500));
        }

        // --- Upgrades ---

        [UnityTest]
        public IEnumerator Era2UpgradesArePaidInPennies()
        {
            yield return Load(Era2Save(200));
            Click("Upgrade_" + ApprenticeTrainingId);
            Click("UpgradeDetail");
            Assert.That(GM.Ledger.Pennies, Is.EqualTo(50m));
        }

        [UnityTest]
        public IEnumerator TappingATreeNodeShowsItOnTheDetailCard()
        {
            yield return Load(Era2Save(0));
            Click("Upgrade_" + WattPatentId);
            Assert.That(GameObject.Find("UpgradeDetail").GetComponent<UI.UpgradeCardView>().Definition.upgradeId,
                        Is.EqualTo(WattPatentId));
        }

        [UnityTest]
        public IEnumerator TappingALockedNodeDoesNotBuyIt()
        {
            yield return Load(Era2Save(10000, 30));
            Click("Upgrade_" + ClerkId); // needs the street counter first
            Click("UpgradeDetail");
            Assert.That(GM.Upgrades.IsPurchased(ClerkId), Is.False);
        }

        [UnityTest]
        public IEnumerator EveryEra2UpgradeIsANodeInTheTree()
        {
            yield return Load(Era2Save(0));
            var tree = UnityEngine.Object.FindAnyObjectByType<UI.UpgradeTreeView>();
            Assert.That(tree.Nodes.Count, Is.EqualTo(Upgrades.Length));
        }

        [UnityTest]
        public IEnumerator TheStreetCounterDoublesWhatTheStreetPays()
        {
            var data = Era2Save(0);
            data.upgradeLevels.Add(new Save.SaveData.UpgradeLevelEntry { upgradeId = StreetCounterId, level = 1 });
            yield return Load(data);
            double withCounter = GM.StreetPricePerWattHour(2f, Day + StartHour * Hour);
            Assert.That(withCounter, Is.EqualTo(2 * ContractBaseRate * StreetRateFactor *
                                                Simulation.DemandModel.DailyShape(StartHour)).Within(1e-9));
        }

        // --- Tabs ---

        [UnityTest]
        public IEnumerator Era2OpensOnTheClientsTab()
        {
            yield return Load(Era2Save(0));
            Assert.That(GameObject.Find("ClientsPage").GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));
        }

        [UnityTest]
        public IEnumerator TappingATabHidesTheOtherPages()
        {
            yield return Load(Era2Save(0));
            Click("Tab_Works");
            Assert.That(GameObject.Find("ClientsPage").GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
        }

        [UnityTest]
        public IEnumerator TappingATabShowsItsPage()
        {
            yield return Load(Era2Save(0));
            Click("Tab_Improvements");
            Assert.That(GameObject.Find("ImprovementsPage").GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));
        }

        [UnityTest]
        public IEnumerator AnEmptyHopperPutsARedDotOnTheWorksTab()
        {
            yield return Load(WithSteam(0, 0));
            yield return new WaitForSeconds(0.4f);
            Assert.That(GameObject.Find("Tab_Works/Badge"), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator ApprenticeTrainingDoublesApprentices()
        {
            var data = Era2Save(100, 0, StartHour, (GeneratorType.SmallDynamo, 1));
            data.upgradeLevels.Add(new Save.SaveData.UpgradeLevelEntry { upgradeId = ApprenticeTrainingId, level = 1 });
            yield return Load(data);
            Assert.That(Generators.OutputWatts(Machine(GeneratorType.SmallDynamo)), Is.EqualTo(100));
        }

        [UnityTest]
        public IEnumerator ReputationGatedUpgradesWaitForReputation()
        {
            yield return Load(Era2Save(10000, 2));
            Assert.That(GM.Upgrades.Purchase(GM.UpgradeCatalogue.First(u => u.upgradeId == WattPatentId)), Is.False);
        }

        [UnityTest]
        public IEnumerator ReputationIsNotSpentOnUpgrades()
        {
            yield return Load(Era2Save(10000, 3));
            GM.Upgrades.Purchase(GM.UpgradeCatalogue.First(u => u.upgradeId == WattPatentId));
            Assert.That(GM.Ledger.Reputation, Is.EqualTo(3f));
        }

        [UnityTest]
        public IEnumerator Era1UpgradesStayOffTheEra2Screen()
        {
            yield return Load(Era2Save(0));
            var nodes = GameObject.Find("Era2Screen").GetComponentsInChildren<UI.UpgradeNodeView>(true);
            Assert.That(nodes.Select(c => c.Definition.minEra), Is.All.EqualTo(EraNumber));
        }

        // --- Saving ---

        [UnityTest]
        public IEnumerator TheHopperLevelSurvivesSaveAndLoad()
        {
            yield return Load(WithSteam(100, 12));
            Save.SaveManager.Instance.Save();
            yield return Load(JsonUtility.FromJson<Save.SaveData>(File.ReadAllText(SavePath)));
            Assert.That(Works.Hopper.Level, Is.EqualTo(12).Within(0.5));
        }

        [UnityTest]
        public IEnumerator ABookingSurvivesSaveAndLoad()
        {
            yield return Load(Era2Save(0));
            Works.Book(Contract("c2_street_lamps"));
            Save.SaveManager.Instance.Save();
            yield return Load(JsonUtility.FromJson<Save.SaveData>(File.ReadAllText(SavePath)));
            Assert.That(Works.Board.BookedToday(Contract("c2_street_lamps"), Controllers.Era2WorksController.Now), Is.True);
        }

        [Test]
        public void AV3SaveMigratesToV4()
        {
            var v3 = new Save.SaveData { version = 3, contractBookedDays = null };
            Save.SaveManager.Migrate(v3);
            Assert.That(v3.contractBookedDays, Is.Not.Null);
        }

        // --- 2× contracts boost ---

        [TearDown]
        public void EndBoost() => GM.SetContractBoostFromSave(0);

        [UnityTest]
        public IEnumerator TheBoostButtonDoublesContractSpeed()
        {
            yield return Load(Era2Save(0));
            Click("ContractBoost");
            Assert.That(GM.ContractSpeed, Is.EqualTo(2.0));
        }

        [UnityTest]
        public IEnumerator TheBoostWearsOff()
        {
            yield return Load(Era2Save(0));
            Click("ContractBoost");
            GM.TickContractBoost(Managers.GameManager.ContractBoostSeconds);
            Assert.That(GM.ContractSpeed, Is.EqualTo(1.0));
        }

        [UnityTest]
        public IEnumerator ABoostedEngagementRunsAheadOfTheClock()
        {
            yield return Load(Era2Save(1000, 0, StartHour, (GeneratorType.SmallDynamo, 12)));
            Works.Book(Contract("c2_street_lamps"));
            Click("ContractBoost");
            Time.timeScale = 0f; // no ticks but the one below
            Simulation.SimulationTick.Instance.CatchUpOffline(1.0); // one real second: one game minute
            Assert.That(GM.Contracts.ActiveContracts[0].ProgressSeconds, Is.EqualTo(2 * 60).Within(1e-6));
        }

        [UnityTest]
        public IEnumerator TheBoostSurvivesSaveAndLoad()
        {
            yield return Load(Era2Save(0));
            Click("ContractBoost");
            Save.SaveManager.Instance.Save();
            yield return Load(JsonUtility.FromJson<Save.SaveData>(File.ReadAllText(SavePath)));
            Assert.That(GM.ContractBoostActive, Is.True);
        }

        [UnityTest]
        public IEnumerator AnEngagementsProgressSurvivesSaveAndLoad()
        {
            yield return Load(Era2Save(1000, 0, StartHour, (GeneratorType.SmallDynamo, 12)));
            Works.Book(Contract("c2_street_lamps"));
            yield return Tick();
            Time.timeScale = 0f;
            double progress = GM.Contracts.ActiveContracts[0].ProgressSeconds;
            Save.SaveManager.Instance.Save();
            yield return Load(JsonUtility.FromJson<Save.SaveData>(File.ReadAllText(SavePath)));
            Assert.That(GM.Contracts.ActiveContracts[0].ProgressSeconds, Is.EqualTo(progress));
        }

        [Test]
        public void AV5SaveTakesItsEngagementsProgressFromTheClock()
        {
            var v5 = new Save.SaveData { version = 5, gameTimeSeconds = Day + 20 * Hour };
            v5.activeContracts.Add(new Save.SaveData.ContractEntry { contractId = "c2_street_lamps", startGameTime = Day + 18 * Hour });
            Save.SaveManager.Migrate(v5);
            Assert.That(v5.activeContracts[0].progressSeconds, Is.EqualTo(2 * Hour));
        }

        // --- The ?era=2 shortcut ---

        [UnityTest]
        public IEnumerator TheEra2ShortcutOpensTheOffice()
        {
            Save.SaveManager.Instance.StartAtEra2();
            yield return null;
            Assert.That(GameObject.Find("Era2Screen"), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator TheEra2ShortcutStartsWithAKilowattWorkshop()
        {
            Save.SaveManager.Instance.StartAtEra2();
            yield return Tick();
            Assert.That(Simulation.SimulationTick.Instance.Grid.TotalOutputWatts, Is.GreaterThanOrEqualTo(1000));
        }

        [UnityTest]
        public IEnumerator TheEra2ShortcutReplacesTheSave()
        {
            yield return Load(Era2Save(5000, 20));
            Save.SaveManager.Instance.StartAtEra2();
            Assert.That(GM.Ledger.Reputation, Is.EqualTo(0f));
        }

        [Test]
        public void AV4SaveOwesItsEngagementsTheirWholeFee()
        {
            var v4 = new Save.SaveData { version = 4 };
            v4.activeContracts.Add(new Save.SaveData.ContractEntry { contractId = "c2_hotel" });
            Save.SaveManager.Migrate(v4);
            Assert.That(v4.activeContracts[0].paidPennies, Is.EqualTo("0"));
        }

        [UnityTest]
        public IEnumerator WhatAnEngagementHasPaidSurvivesSaveAndLoad()
        {
            yield return Load(Era2Save(1000, 0, StartHour, (GeneratorType.SmallDynamo, 12)));
            Works.Book(Contract("c2_street_lamps"));
            yield return Tick();
            Time.timeScale = 0f; // no tick between save and check to pay it again
            decimal paid = GM.Contracts.ActiveContracts[0].PaidSoFar;
            Save.SaveManager.Instance.Save();
            yield return Load(JsonUtility.FromJson<Save.SaveData>(File.ReadAllText(SavePath)));
            Assert.That(GM.Contracts.ActiveContracts[0].PaidSoFar, Is.EqualTo(paid));
        }

        [UnityTest]
        public IEnumerator TheStreetPaysWhileThePlayerIsAway()
        {
            var data = Era2Save(0, 0, StartHour, (GeneratorType.Waterwheel, 2));
            data.saveTimestampUnix -= 3600; // an hour away: a medium absence
            yield return Load(data);
            Assert.That(GM.Ledger.Pennies, Is.GreaterThan(0m));
        }

        [UnityTest]
        public IEnumerator CustomersPayWhileThePlayerIsAway()
        {
            var data = Era2Save(0, 0, StartHour, (GeneratorType.SmallDynamo, 12));
            data.saveTimestampUnix -= 3600; // an hour away: a medium absence
            data.activeContracts.Add(new Save.SaveData.ContractEntry
                { contractId = "c2_street_lamps", startGameTime = Day + StartHour * Hour });
            yield return Load(data);
            Assert.That(GM.Ledger.Pennies, Is.GreaterThan(0m));
        }

        // --- The way out ---

        [UnityTest]
        public IEnumerator LightingTheExhibitionEndsEra2()
        {
            yield return Load(Era2Save(0, 30));
            var expo = Contract(ExhibitionContractId);
            Utils.EventBus.Publish(new ContractCompletedEvent(new Economy.ContractInstance(expo, 0), 1m));
            yield return null;
            Assert.That(GameObject.Find("Era2EndOfEra").transform.Find("Panel").gameObject.activeSelf, Is.True);
        }

        [Test]
        public void TheStreetKeepsBuyingAfterEra2Ends() =>
            Assert.That(GM.StreetPricePerWattHour(3f, Day + StartHour * Hour), Is.GreaterThan(0));

        [UnityTest]
        public IEnumerator TheOfficeStaysUpAfterEra2Ends()
        {
            yield return Load(Era2Save(0, 30));
            var expo = Contract(ExhibitionContractId);
            Utils.EventBus.Publish(new ContractCompletedEvent(new Economy.ContractInstance(expo, 0), 1m));
            yield return null;
            Assert.That(GameObject.Find("Era2Screen"), Is.Not.Null);
        }
    }
}
