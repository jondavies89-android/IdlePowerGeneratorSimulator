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

        // A must-hold contract whose output dipped: it ends there, unpaid.
        public void Break()
        {
            if (IsResolved) return;
            IsResolved = true;
            EventBus.Publish(new ContractFailedEvent(this));
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

        // Watts sold to customers on the last tick. That power goes down their wires, so
        // it isn't also banked as the workshop's own joules.
        public double DeliveredWatts { get; private set; }

        // Called every sim tick (1 Hz). outputWatts = total grid output at this moment.
        // Output is shared out oldest contract first, each taking at most what it asked
        // for: two customers can't both be paid for the same watts.
        public void Tick(double outputWatts, double deltaTime,
                         double gameTime, CurrencyLedger ledger, double eraBaseRate)
        {
            double remaining = Math.Max(0, outputWatts);
            DeliveredWatts = 0;
            var ended = new List<ContractInstance>();

            foreach (var c in _active)
            {
                double share = Math.Min(c.Definition.requiredWatts, remaining);
                remaining -= share;

                // Without this a must-hold contract paid on average energy, so a moment at
                // full output and a long dip below it could still pass the fail threshold.
                if (c.Definition.mustHoldOutput && share < c.Definition.requiredWatts)
                {
                    remaining += share;
                    c.Break();
                    ended.Add(c);
                    continue;
                }

                DeliveredWatts += share;
                c.RecordOutput(share, deltaTime);

                if (gameTime >= c.EndGameTime)
                {
                    c.Resolve(ledger, eraBaseRate);
                    ended.Add(c);
                }
            }

            foreach (var c in ended)
            {
                _active.Remove(c);
                _history.Add(c);
            }
        }

        public void AddFromSave(ContractInstance instance) => _active.Add(instance);
        public void ClearActive() => _active.Clear();
    }
}
