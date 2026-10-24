using NUnit.Framework;
using UnityEngine;
using IdlePowerGen.Economy;
using IdlePowerGen.Models;

namespace IdlePowerGen.Tests
{
    // Must-hold contracts on their own, with no scene: one dip below the wattage ends them.
    public class ContractHoldTests
    {
        private ContractDefinition _def;
        private ContractLedger _contracts;
        private CurrencyLedger _ledger;

        [SetUp]
        public void SetUp()
        {
            _def = ScriptableObject.CreateInstance<ContractDefinition>();
            _def.contractId        = "hold_test";
            _def.requiredWatts     = 1000;
            _def.durationHours     = 1f;
            _def.payRateMultiplier = 12f;
            _def.failThreshold     = 0.5f;
            _def.mustHoldOutput    = true;
            _contracts = new ContractLedger();
            _ledger    = new CurrencyLedger();
            _contracts.TryAccept(_def, 0);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_def);

        // One tick per game minute; the hold rule doesn't depend on the step.
        private void Supply(double watts, int fromMinute, int toMinute)
        {
            for (int m = fromMinute; m < toMinute; m++)
                _contracts.Tick(watts, 60, (m + 1) * 60.0, _ledger, 0.001);
        }

        [Test]
        public void OneDipBelowTheWattageBreaksTheContract()
        {
            Supply(1000, 0, 10);
            Supply(999, 10, 11);
            Assert.That(_contracts.ActiveContracts, Is.Empty);
        }

        [Test]
        public void ABrokenContractPaysNothing()
        {
            // Over the fail threshold on average energy: the old rule would have paid this.
            Supply(2000, 0, 30);
            Supply(0, 30, 60);
            Assert.That(_ledger.Pennies, Is.EqualTo(0m));
        }

        [Test]
        public void HoldingTheWattageForTheWholeHourPays()
        {
            Supply(1000, 0, 60);
            Assert.That(_ledger.Pennies, Is.GreaterThan(0m));
        }

        [Test]
        public void AHeldContractTakesOnlyTheWattsItAskedFor()
        {
            Supply(1500, 0, 1);
            Assert.That(_contracts.DeliveredWatts, Is.EqualTo(1000));
        }

        [Test]
        public void ContractsThatNeedNotHoldSurviveADip()
        {
            _def.mustHoldOutput = false;
            Supply(1000, 0, 10);
            Supply(0, 10, 11);
            Assert.That(_contracts.ActiveContracts, Has.Count.EqualTo(1));
        }
    }
}
