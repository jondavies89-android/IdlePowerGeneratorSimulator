using NUnit.Framework;
using UnityEngine;
using IdlePowerGen.Economy;
using IdlePowerGen.Models;

namespace IdlePowerGen.Tests
{
    // Era 2's systems on their own, with no scene: the contract board, the coal hopper,
    // shared output, pay rates and per-machine multipliers.
    public class Era2HeadlessTests
    {
        private const double Hour = 3600;
        private const double Day  = 86400;

        private static ContractDefinition Contract(string id, double watts, float from, float to,
                                                   float hours = 1f, float rep = 0f)
        {
            var def = ScriptableObject.CreateInstance<ContractDefinition>();
            def.contractId         = id;
            def.requiredWatts      = watts;
            def.durationHours      = hours;
            def.availableStartHour = from;
            def.availableEndHour   = to;
            def.requiredReputation = rep;
            def.payRateMultiplier  = 1f;
            def.failThreshold      = 0.5f;
            def.minEra             = 2;
            return def;
        }

        // --- Contract board ---

        [Test]
        public void AClientIsOfferedInsideTheirWindow()
        {
            var board = new ContractBoard();
            var lamps = Contract("lamps", 500, 17, 21);
            Assert.That(board.IsOffered(lamps, Day + 18 * Hour, 0, new ContractInstance[0]), Is.True);
        }

        [Test]
        public void AClientIsNotOfferedOutsideTheirWindow()
        {
            var board = new ContractBoard();
            var lamps = Contract("lamps", 500, 17, 21);
            Assert.That(board.IsOffered(lamps, Day + 12 * Hour, 0, new ContractInstance[0]), Is.False);
        }

        [Test]
        public void AWindowPastMidnightIsOpenAfterMidnight()
        {
            var board = new ContractBoard();
            var late = Contract("late", 500, 22, 2);
            Assert.That(board.IsOffered(late, Day + 1 * Hour, 0, new ContractInstance[0]), Is.True);
        }

        [Test]
        public void AClientBelowTheirReputationIsNotOffered()
        {
            var board = new ContractBoard();
            var royal = Contract("royal", 500, 0, 24, rep: 10);
            Assert.That(board.IsOffered(royal, Day, 9.9f, new ContractInstance[0]), Is.False);
        }

        [Test]
        public void AClientBookedTodayIsNotOfferedAgainToday()
        {
            var board = new ContractBoard();
            var ledger = new ContractLedger();
            var lamps = Contract("lamps", 500, 17, 21, hours: 1);
            board.TryBook(lamps, Day + 17 * Hour, 0, ledger, 2);
            ledger.Tick(1000, 2 * Hour, Day + 19 * Hour, new CurrencyLedger(), 0.4); // finishes it
            Assert.That(board.IsOffered(lamps, Day + 20 * Hour, 0, ledger.ActiveContracts), Is.False);
        }

        [Test]
        public void AClientBookedTodayIsOfferedAgainTomorrow()
        {
            var board = new ContractBoard();
            var ledger = new ContractLedger();
            var lamps = Contract("lamps", 500, 17, 21, hours: 1);
            board.TryBook(lamps, Day + 17 * Hour, 0, ledger, 2);
            ledger.Tick(1000, 2 * Hour, Day + 19 * Hour, new CurrencyLedger(), 0.4);
            Assert.That(board.IsOffered(lamps, 2 * Day + 18 * Hour, 0, ledger.ActiveContracts), Is.True);
        }

        [Test]
        public void BookingStopsAtTheSlotLimit()
        {
            var board = new ContractBoard();
            var ledger = new ContractLedger();
            board.TryBook(Contract("a", 100, 0, 24), Day, 0, ledger, 2);
            board.TryBook(Contract("b", 100, 0, 24), Day, 0, ledger, 2);
            Assert.That(board.TryBook(Contract("c", 100, 0, 24), Day, 0, ledger, 2), Is.False);
        }

        // --- Shared output and pay ---

        [Test]
        public void TheLaterContractGetsWhatTheEarlierLeaves()
        {
            var ledger = new ContractLedger();
            ledger.TryAccept(Contract("first", 800, 0, 24), 0);
            var second = Contract("second", 800, 0, 24);
            ledger.TryAccept(second, 0);
            ledger.Tick(1000, 60, 60, new CurrencyLedger(), 0.4);
            Assert.That(ledger.ActiveContracts[1].AccumulatedWattSeconds, Is.EqualTo(200 * 60).Within(1e-6));
        }

        [Test]
        public void AFullySuppliedContractPaysHoursTimesWattsTimesItsRate()
        {
            var ledger = new ContractLedger();
            var wallet = new CurrencyLedger();
            ledger.TryAccept(Contract("lamps", 500, 0, 24, hours: 2), 0);
            ledger.Tick(500, 2 * Hour, 2 * Hour, wallet, _ => 0.4);
            Assert.That(wallet.Pennies, Is.EqualTo(400m)); // 2 h × 500 W × 0.4
        }

        [Test]
        public void OverSupplyDoesNotRaiseThePay()
        {
            var ledger = new ContractLedger();
            var wallet = new CurrencyLedger();
            ledger.TryAccept(Contract("lamps", 500, 0, 24, hours: 2), 0);
            ledger.Tick(5000, 2 * Hour, 2 * Hour, wallet, _ => 0.4);
            Assert.That(wallet.Pennies, Is.EqualTo(400m));
        }

        [Test]
        public void SupplyBelowHalfFailsWithTwoReputationLost()
        {
            var ledger = new ContractLedger();
            var wallet = new CurrencyLedger();
            wallet.AddReputation(5);
            var def = Contract("lamps", 500, 0, 24, hours: 2);
            def.reputationOnFail = -2f;
            ledger.TryAccept(def, 0);
            ledger.Tick(200, 2 * Hour, 2 * Hour, wallet, _ => 0.4);
            Assert.That(wallet.Reputation, Is.EqualTo(3f));
        }

        [Test]
        public void AnEngagementPaysForEachWattAsItArrives()
        {
            var ledger = new ContractLedger();
            var wallet = new CurrencyLedger();
            ledger.TryAccept(Contract("lamps", 500, 0, 24, hours: 2), 0);
            ledger.Tick(500, Hour, Hour, wallet, _ => 0.4);
            Assert.That(wallet.Pennies, Is.EqualTo(200m)); // one of its two hours
        }

        [Test]
        public void TheFeeMultiplierIsPaidOnFinishing()
        {
            var ledger = new ContractLedger();
            var wallet = new CurrencyLedger();
            var def = Contract("royal", 500, 0, 24, hours: 2);
            def.payRateMultiplier = 1.5f;
            ledger.TryAccept(def, 0);
            ledger.Tick(500, Hour, Hour, wallet, _ => 0.4);
            ledger.Tick(500, Hour, 2 * Hour, wallet, _ => 0.4);
            Assert.That(wallet.Pennies, Is.EqualTo(600m)); // 2 h × 500 W × 0.4 × 1.5
        }

        [Test]
        public void AFailedEngagementPaysNoMoreThanItsWatts()
        {
            var ledger = new ContractLedger();
            var wallet = new CurrencyLedger();
            ledger.TryAccept(Contract("lamps", 500, 0, 24, hours: 2), 0);
            ledger.Tick(200, 2 * Hour, 2 * Hour, wallet, _ => 0.4);
            Assert.That(wallet.Pennies, Is.EqualTo(160m).Within(0.01m)); // 2 h × 200 W × 0.4; the ratio is a float
        }

        [Test]
        public void AMustHoldContractPaysOnlyAtTheEnd()
        {
            var ledger = new ContractLedger();
            var wallet = new CurrencyLedger();
            var def = Contract("neighbour", 500, 0, 24, hours: 2);
            def.mustHoldOutput = true;
            ledger.TryAccept(def, 0);
            ledger.Tick(500, Hour, Hour, wallet, _ => 0.4);
            Assert.That(wallet.Pennies, Is.EqualTo(0m));
        }

        // --- Street trade ---

        [Test]
        public void TheStreetPaysForSpareWattsByTheHour()
        {
            var wallet = new CurrencyLedger();
            new StreetTrade().Sell(100, Hour, 0.1, wallet);
            Assert.That(wallet.Pennies, Is.EqualTo(10m));
        }

        [Test]
        public void WithNoStreetPriceNothingIsSold()
        {
            var street = new StreetTrade();
            street.Sell(100, Hour, 0, new CurrencyLedger());
            Assert.That(street.LastWatts, Is.EqualTo(0));
        }

        [Test]
        public void TheStreetPaysMoreAtLampLightingThanBeforeDawn() =>
            Assert.That(Simulation.DemandModel.DailyShape(18), Is.GreaterThan(Simulation.DemandModel.DailyShape(4)));

        [Test]
        public void RatioSoFarIsOneWhileFullySupplied()
        {
            var ledger = new ContractLedger();
            ledger.TryAccept(Contract("hotel", 1000, 0, 24, hours: 8), 0);
            ledger.Tick(1500, Hour, Hour, new CurrencyLedger(), 0.4);
            Assert.That(ledger.ActiveContracts[0].RatioSoFar(), Is.EqualTo(1f).Within(1e-6));
        }

        [Test]
        public void AKeptEngagementEarnsItsWholeRepute()
        {
            var ledger = new ContractLedger();
            var wallet = new CurrencyLedger();
            var def = Contract("hotel", 1200, 0, 24, hours: 2);
            def.reputationOnSuccess = 1f;
            ledger.TryAccept(def, 0);
            ledger.Tick(1100, 2 * Hour, 2 * Hour, wallet, _ => 0.4); // 92 % supplied: kept, a little short
            Assert.That(wallet.Reputation, Is.EqualTo(1f));
        }

        [Test]
        public void TheLastStepDoesNotCreditWattsPastTheEnd()
        {
            var ledger = new ContractLedger();
            ledger.TryAccept(Contract("lamps", 500, 0, 24, hours: 2), 0);
            var c = ledger.ActiveContracts[0];
            ledger.Tick(500, 3 * Hour, 3 * Hour, new CurrencyLedger(), 0.4);
            Assert.That(c.AccumulatedWattSeconds, Is.EqualTo(500 * 2 * Hour));
        }

        // --- 2× contracts boost ---

        [Test]
        public void AtDoubleSpeedATwoHourContractEndsInOne()
        {
            var ledger = new ContractLedger();
            ledger.TryAccept(Contract("lamps", 500, 0, 24, hours: 2), 0);
            ledger.Tick(500, Hour, Hour, new CurrencyLedger(), _ => 0.4, contractSpeed: 2);
            Assert.That(ledger.ActiveContracts, Is.Empty);
        }

        [Test]
        public void AtDoubleSpeedTheFeeIsUnchanged()
        {
            var ledger = new ContractLedger();
            var wallet = new CurrencyLedger();
            ledger.TryAccept(Contract("lamps", 500, 0, 24, hours: 2), 0);
            ledger.Tick(500, Hour, Hour, wallet, _ => 0.4, contractSpeed: 2);
            Assert.That(wallet.Pennies, Is.EqualTo(400m)); // 2 h × 500 W × 0.4, in half the time
        }

        [Test]
        public void AtDoubleSpeedAContractIsStillFullySupplied()
        {
            var ledger = new ContractLedger();
            ledger.TryAccept(Contract("lamps", 500, 0, 24, hours: 2), 0);
            ledger.Tick(500, 0.5 * Hour, 0.5 * Hour, new CurrencyLedger(), _ => 0.4, contractSpeed: 2);
            Assert.That(ledger.ActiveContracts[0].RatioSoFar(), Is.EqualTo(1f).Within(1e-6));
        }

        // --- Shortfall countdown ---

        // Runs the ledger in one-minute game ticks, as the sim does (1 real second = 1 game minute).
        private static void Run(ContractLedger ledger, double watts, double minutes,
                                CurrencyLedger wallet = null, double speed = 1)
        {
            wallet ??= new CurrencyLedger();
            for (int i = 0; i < minutes; i++)
                ledger.Tick(watts, 60, 0, wallet, _ => 0.4, speed);
        }

        private static ContractLedger OneHotel(out ContractInstance hotel)
        {
            var ledger = new ContractLedger();
            ledger.TryAccept(Contract("hotel", 1000, 0, 24, hours: 8), 0);
            hotel = ledger.ActiveContracts[0];
            return ledger;
        }

        [Test]
        public void AShortfallWithinTheLeewayStartsNoCountdown()
        {
            var ledger = OneHotel(out var hotel);
            Run(ledger, 950, 10); // 5 % short, inside the 10 % leeway
            Assert.That(hotel.CountdownRunning, Is.False);
        }

        [Test]
        public void AShortfallPastTheLeewayStartsTheCountdown()
        {
            var ledger = OneHotel(out var hotel);
            Run(ledger, 800, 1);
            Assert.That(hotel.FailsInSeconds, Is.EqualTo(29 * 60).Within(1e-6));
        }

        [Test]
        public void AClientShortForTheWholeCountdownCancels()
        {
            var ledger = OneHotel(out _);
            Run(ledger, 0, 30);
            Assert.That(ledger.ActiveContracts, Is.Empty);
        }

        [Test]
        public void AClientShortForLessThanTheCountdownStays()
        {
            var ledger = OneHotel(out _);
            Run(ledger, 0, 29);
            Assert.That(ledger.ActiveContracts, Has.Count.EqualTo(1));
        }

        [Test]
        public void ACancelledClientCostsRepute()
        {
            var ledger = OneHotel(out var hotel);
            var wallet = new CurrencyLedger();
            wallet.AddReputation(5);
            hotel.Definition.reputationOnFail = -2f;
            Run(ledger, 0, 30, wallet);
            Assert.That(wallet.Reputation, Is.EqualTo(3f));
        }

        [Test]
        public void ACancelledClientKeepsWhatItPaidAlongTheWay()
        {
            var ledger = OneHotel(out _);
            var wallet = new CurrencyLedger();
            Run(ledger, 1000, 60, wallet); // 1 h × 1 kW × 0.4 = 400
            Run(ledger, 0, 30, wallet);
            Assert.That(wallet.Pennies, Is.EqualTo(400m).Within(0.01m));
        }

        [Test]
        public void TheCountdownHoldsWhenThePowerComesBack()
        {
            var ledger = OneHotel(out var hotel);
            Run(ledger, 0, 10);
            Run(ledger, 1000, 10);
            Assert.That(hotel.FailsInSeconds, Is.EqualTo(20 * 60).Within(1e-6));
        }

        [Test]
        public void TheCountdownClearsAfterAnHourSupplied()
        {
            var ledger = OneHotel(out var hotel);
            Run(ledger, 0, 10);
            Run(ledger, 1000, 60);
            Assert.That(hotel.CountdownRunning, Is.False);
        }

        [Test]
        public void GoingShortAgainBeforeItClearsCarriesOnTheCountdown()
        {
            var ledger = OneHotel(out _);
            Run(ledger, 0, 20);
            Run(ledger, 1000, 59); // a minute short of clearing
            Run(ledger, 0, 10);
            Assert.That(ledger.ActiveContracts, Is.Empty);
        }

        [Test]
        public void TheCountdownRunsOnTheClockNotTheBoost()
        {
            var ledger = OneHotel(out _);
            Run(ledger, 0, 20, speed: 2); // 20 game minutes, though 40 contract minutes
            Assert.That(ledger.ActiveContracts, Has.Count.EqualTo(1));
        }

        [Test]
        public void TheCountdownIsPausedWhileCatchingUpOnTimeAway()
        {
            var ledger = OneHotel(out _);
            ledger.ShortfallPaused = true;
            Run(ledger, 0, 60);
            Assert.That(ledger.ActiveContracts, Has.Count.EqualTo(1));
        }

        [Test]
        public void AMustHoldContractHasNoCountdown()
        {
            var ledger = new ContractLedger();
            var def = Contract("neighbour", 500, 0, 24, hours: 2);
            def.mustHoldOutput = true;
            ledger.TryAccept(def, 0);
            // It breaks on the first dip instead (ContractHoldTests).
            Assert.That(ledger.ActiveContracts[0].TrackShortfall(0, Hour), Is.False);
        }

        // --- Coal hopper ---

        [Test]
        public void TheHopperHoldsADayOfBurn()
        {
            var hopper = new Era2CoalHopper();
            hopper.Fill(4);
            Assert.That(hopper.HoursLeft(4), Is.EqualTo(24).Within(1e-9));
        }

        [Test]
        public void BurningPastTheLevelEmptiesTheHopper()
        {
            var hopper = new Era2CoalHopper();
            hopper.Fill(1);
            Assert.That(hopper.Burn(1, 25), Is.False);
        }

        [Test]
        public void AnOrderCostsWhatWasBurned()
        {
            var hopper = new Era2CoalHopper();
            hopper.Fill(2);
            hopper.Burn(2, 10); // 20 d of coal
            Assert.That(hopper.OrderCost(2), Is.EqualTo(20m));
        }

        [Test]
        public void AnOrderRefillsTheHopper()
        {
            var hopper = new Era2CoalHopper();
            var wallet = new CurrencyLedger();
            wallet.AddPennies(100);
            hopper.Fill(2);
            hopper.Burn(2, 10);
            hopper.Order(2, wallet);
            Assert.That(hopper.Fraction(2), Is.EqualTo(1f));
        }

        [Test]
        public void AnOrderNeedsThePennies()
        {
            var hopper = new Era2CoalHopper();
            var wallet = new CurrencyLedger();
            wallet.AddPennies(5);
            hopper.Fill(2);
            hopper.Burn(2, 10);
            Assert.That(hopper.Order(2, wallet), Is.False);
        }

        // --- Machines ---

        [Test]
        public void ATypeMultiplierReachesMachinesBoughtAfterIt()
        {
            var grid = new Simulation.PowerGrid();
            grid.MultiplyType(GeneratorType.SmallDynamo, 2);
            var def = ScriptableObject.CreateInstance<GeneratorDefinition>();
            def.generatorType  = GeneratorType.SmallDynamo;
            def.basePowerWatts = 50;
            grid.AddGenerator(new Simulation.GeneratorInstance(def));
            grid.Tick(1, 0);
            Assert.That(grid.TotalOutputWatts, Is.EqualTo(100));
        }

        [Test]
        public void ATypeMultiplierLeavesOtherMachinesAlone()
        {
            var grid = new Simulation.PowerGrid();
            grid.MultiplyType(GeneratorType.SteamEngine, 2);
            var def = ScriptableObject.CreateInstance<GeneratorDefinition>();
            def.generatorType  = GeneratorType.SmallDynamo;
            def.basePowerWatts = 50;
            grid.AddGenerator(new Simulation.GeneratorInstance(def));
            grid.Tick(1, 0);
            Assert.That(grid.TotalOutputWatts, Is.EqualTo(50));
        }

        // --- Display ---

        [TestCase(412.7, "412 d")]
        [TestCase(8.56, "8.5 d")]
        [TestCase(12500, "12.5k d")]
        [TestCase(1250000, "1.25M d")]
        public void PenniesFormatAsTheMockupDoes(double amount, string expected) =>
            Assert.That(Utils.NumberFormatter.FormatPennies((decimal)amount), Is.EqualTo(expected));
    }
}
