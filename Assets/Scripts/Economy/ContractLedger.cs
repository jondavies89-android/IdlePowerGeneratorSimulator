using System;
using System.Collections.Generic;
using IdlePowerGen.Models;
using IdlePowerGen.Utils;

namespace IdlePowerGen.Economy
{
    // Runtime state for one active contract.
    public class ContractInstance
    {
        public ContractDefinition Definition      { get; }
        public double             StartGameTime   { get; }
        public double             EndGameTime     { get; }
        public bool               IsResolved      { get; private set; }

        private double _accumulatedWattSeconds;

        public ContractInstance(ContractDefinition def, double startGameTime)
        {
            Definition    = def;
            StartGameTime = startGameTime;
            EndGameTime   = startGameTime + def.durationHours * 3600.0;
        }

        public void RecordOutput(double outputWatts, double deltaTime)
        {
            _accumulatedWattSeconds += outputWatts * deltaTime;
        }

        public float FulfillmentRatio()
        {
            double required = Definition.requiredWatts * Definition.durationHours * 3600.0;
            return required <= 0 ? 1f : (float)(_accumulatedWattSeconds / required);
        }

        // §5.1.6 — resolve pay and reputation, publish outcome event.
        public void Resolve(CurrencyLedger ledger, double eraBaseRate)
        {
            if (IsResolved) return;
            IsResolved = true;

            float  ratio   = FulfillmentRatio();
            double basePay = Definition.durationHours * Definition.requiredWatts * eraBaseRate;

            if (ratio >= Definition.failThreshold)
            {
                decimal pay = (decimal)(basePay * Definition.payRateMultiplier * ratio);
                ledger.AddPennies(pay);
                ledger.AddReputation(Definition.reputationOnSuccess * ratio);
                EventBus.Publish(new ContractCompletedEvent(this, pay));
            }
            else
            {
                decimal pay = (decimal)(basePay * ratio);
                ledger.AddPennies(pay);
                ledger.AddReputation(Definition.reputationOnFail);
                EventBus.Publish(new ContractFailedEvent(this));
            }
        }

        // Save support
        public double AccumulatedWattSeconds => _accumulatedWattSeconds;
        public void   SetAccumulatedFromSave(double v) => _accumulatedWattSeconds = v;
    }

    // -------------------------------------------------------------------------

    // Manages the list of active and completed contracts.
    // Ticked by SimulationTick via GameManager.
    public class ContractLedger
    {
        private readonly List<ContractInstance> _active  = new();
        private readonly List<ContractInstance> _history = new();

        public IReadOnlyList<ContractInstance> ActiveContracts => _active;
        public IReadOnlyList<ContractInstance> History         => _history;

        public bool TryAccept(ContractDefinition def, double gameTime, int maxConcurrent = 3)
        {
            if (_active.Count >= maxConcurrent) return false;
            var instance = new ContractInstance(def, gameTime);
            _active.Add(instance);
            EventBus.Publish(new ContractAcceptedEvent(def));
            return true;
        }

        // Called every sim tick (1 Hz). outputWatts = total grid output at this moment.
        public void Tick(double outputWatts, double deltaTime,
                         double gameTime, CurrencyLedger ledger, double eraBaseRate)
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var c = _active[i];
                c.RecordOutput(outputWatts, deltaTime);

                if (gameTime >= c.EndGameTime)
                {
                    c.Resolve(ledger, eraBaseRate);
                    _history.Add(c);
                    _active.RemoveAt(i);
                }
            }
        }

        public void AddFromSave(ContractInstance instance) => _active.Add(instance);
        public void ClearActive() => _active.Clear();
    }
}
