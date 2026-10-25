using System;
using System.Collections.Generic;
using IdlePowerGen.Models;
using IdlePowerGen.Utils;

namespace IdlePowerGen.Economy
{
    // Runtime state for one active contract.
    // A contract keeps its own clock (ProgressSeconds) rather than ending at a fixed game
    // time, so it can run faster than the day around it (the 2× contracts boost).
    public class ContractInstance
    {
        public ContractDefinition Definition      { get; }
        public double             StartGameTime   { get; }
        public bool               IsResolved      { get; private set; }

        private double _accumulatedWattSeconds;

        public ContractInstance(ContractDefinition def, double startGameTime)
        {
            Definition    = def;
            StartGameTime = startGameTime;
        }

        public double DurationSeconds => Definition.durationHours * 3600.0;

        // Contract time served so far, in game seconds.
        public double ProgressSeconds { get; private set; }

        public double SecondsLeft => Math.Max(0, DurationSeconds - ProgressSeconds);
        public float  Progress01  => DurationSeconds <= 0 ? 1f : (float)Math.Min(1.0, ProgressSeconds / DurationSeconds);
        public bool   IsDue       => ProgressSeconds >= DurationSeconds;

        // Runs the contract on by up to `contractSeconds`, never past its end, so the last
        // step can't credit watts beyond the hours asked for. Returns the seconds served.
        public double Advance(double contractSeconds)
        {
            double step = Math.Max(0, Math.Min(contractSeconds, SecondsLeft));
            ProgressSeconds += step;
            return step;
        }

        public void RecordOutput(double outputWatts, double deltaTime)
        {
            _accumulatedWattSeconds += outputWatts * deltaTime;
        }

        // Pennies already handed over while it ran; Resolve pays only what is still owed.
        public decimal PaidSoFar { get; private set; }

        // A client who is judged on average pays for each watt as it arrives, at the plain
        // rate, so the purse moves every second instead of in one lump hours later. The
        // multiplier and reputation still wait for the end: they reward finishing the job.
        // A must-hold contract can still break unpaid, so it keeps paying in one go.
        public decimal PayAsDelivered(double watts, double deltaTime, CurrencyLedger ledger, double eraBaseRate)
        {
            if (Definition.mustHoldOutput || watts <= 0) return 0m;
            decimal pay = (decimal)(watts * deltaTime / 3600.0 * eraBaseRate);
            PaidSoFar += pay;
            ledger.AddPennies(pay);
            return pay;
        }

        public float FulfillmentRatio()
        {
            double required = Definition.requiredWatts * Definition.durationHours * 3600.0;
            return required <= 0 ? 1f : (float)(_accumulatedWattSeconds / required);
        }

        // How well it has been supplied so far: 1 = every watt asked for, so far.
        // FulfillmentRatio only reaches 1 at the end, so it can't say "holding" mid-way.
        public float RatioSoFar()
        {
            double required = Definition.requiredWatts * ProgressSeconds;
            return required <= 0 ? 1f : (float)Math.Min(1.0, _accumulatedWattSeconds / required);
        }

        // ---- Shortfall countdown (contracts judged on average) ----
        // A shortfall within the leeway goes unremarked. Past it, a countdown runs while the
        // client is short. It holds, not resets, when the power comes back, and clears only
        // after the client has been supplied for a good while in a row. When it runs out the
        // client cancels.

        // Game seconds of shortfall counted against the countdown.
        public double ShortSeconds    { get; private set; }
        // Game seconds supplied in a row since the last shortfall, towards clearing it.
        public double SuppliedSeconds { get; private set; }
        // Whether the last tick's supply fell past the leeway.
        public bool   IsShort         { get; private set; }

        public double CountdownSeconds   => Definition.shortfallCountdownMinutes * 60.0;
        public double ForgivenAfterSeconds => Definition.shortfallForgivenAfterMinutes * 60.0;
        public double MinimumWatts       => Definition.requiredWatts * (1.0 - Definition.shortfallLeeway);
        public bool   CountdownRunning   => ShortSeconds > 0;
        public double FailsInSeconds     => Math.Max(0, CountdownSeconds - ShortSeconds);
        public double ClearsInSeconds    => Math.Max(0, ForgivenAfterSeconds - SuppliedSeconds);

        // Counts a tick's supply against the countdown; true once it has run out. Game seconds,
        // not contract seconds: the 2× boost speeds the job, not the client's patience.
        public bool TrackShortfall(double watts, double deltaTime)
        {
            if (Definition.mustHoldOutput) return false;
            // A hair of tolerance so float sums at exactly the minimum don't read as short.
            IsShort = watts < MinimumWatts - 1e-6;
            if (IsShort)
            {
                ShortSeconds += deltaTime;
                SuppliedSeconds = 0;
            }
            else if (ShortSeconds > 0)
            {
                SuppliedSeconds += deltaTime;
                if (SuppliedSeconds >= ForgivenAfterSeconds) ShortSeconds = SuppliedSeconds = 0;
            }
            return ShortSeconds >= CountdownSeconds;
        }

        // The client cancels after the countdown ran out. What they paid as the power arrived
        // stays paid; the premium doesn't come, and the repute is lost as for any failure.
        public void Cancel(CurrencyLedger ledger)
        {
            if (IsResolved) return;
            IsResolved = true;
            ledger.AddReputation(Definition.reputationOnFail);
            EventBus.Publish(new ContractFailedEvent(this));
        }

        // §5.1.6 — resolve pay and reputation, publish outcome event.
        public void Resolve(CurrencyLedger ledger, double eraBaseRate)
        {
            if (IsResolved) return;
            IsResolved = true;

            // Over-supply is the works' loss, not the client's: pay tops out at what was asked.
            float  ratio   = Math.Min(1f, FulfillmentRatio());
            double basePay = Definition.durationHours * Definition.requiredWatts * eraBaseRate;

            // The event carries the whole fee, including what was paid along the way.
            if (ratio >= Definition.failThreshold)
            {
                decimal pay = (decimal)(basePay * Definition.payRateMultiplier * ratio);
                PayBalance(pay, ledger);
                // A kept engagement earns its whole repute. Scaling it by the share supplied
                // turned a client who was briefly short a few watts into "+0.9 repute" against
                // the "+1" the board promised; the pay already reflects any shortfall.
                ledger.AddReputation(Definition.reputationOnSuccess);
                EventBus.Publish(new ContractCompletedEvent(this, pay));
            }
            else
            {
                PayBalance((decimal)(basePay * ratio), ledger);
                ledger.AddReputation(Definition.reputationOnFail);
                EventBus.Publish(new ContractFailedEvent(this));
            }
        }

        private void PayBalance(decimal total, CurrencyLedger ledger)
        {
            decimal owed = total - PaidSoFar;
            if (owed > 0) ledger.AddPennies(owed);
            PaidSoFar = Math.Max(PaidSoFar, total);
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
        public void   SetPaidFromSave(decimal v) => PaidSoFar = v;
        public void   SetProgressFromSave(double seconds) => ProgressSeconds = Math.Max(0, seconds);
        public void   SetShortfallFromSave(double shortSeconds, double suppliedSeconds)
        {
            ShortSeconds    = Math.Max(0, shortSeconds);
            SuppliedSeconds = Math.Max(0, suppliedSeconds);
        }
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

        // Pennies paid on the last tick by clients paying as they go (not lump sums at the end).
        public decimal PaidLastTick { get; private set; }

        // Set while catching up on time away: a client never cancels over a shortfall the
        // player wasn't there to fix (bible §10.5), so the countdown neither runs nor clears.
        public bool ShortfallPaused { get; set; }

        // Called every sim tick (1 Hz). outputWatts = total grid output at this moment.
        // Output is shared out oldest contract first, each taking at most what it asked
        // for: two customers can't both be paid for the same watts.
        public void Tick(double outputWatts, double deltaTime,
                         double gameTime, CurrencyLedger ledger, double eraBaseRate) =>
            Tick(outputWatts, deltaTime, gameTime, ledger, _ => eraBaseRate);

        // baseRateFor: the pay rate of the era a contract belongs to, so a contract still
        // running when the era changes is paid at the rate it was offered at.
        // contractSpeed: contract seconds served per game second (2 under the 2× boost). The
        // watts drawn don't change; a faster contract takes the same power for less time, so
        // it pays out faster too. gameTime is the clock at this tick, kept for callers' sake:
        // contracts now end on their own progress, not on the clock.
        public void Tick(double outputWatts, double deltaTime, double gameTime,
                         CurrencyLedger ledger, Func<ContractDefinition, double> baseRateFor,
                         double contractSpeed = 1.0)
        {
            double remaining = Math.Max(0, outputWatts);
            DeliveredWatts = 0;
            PaidLastTick = 0;
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
                double served = c.Advance(deltaTime * Math.Max(0, contractSpeed));
                c.RecordOutput(share, served);
                PaidLastTick += c.PayAsDelivered(share, served, ledger, baseRateFor(c.Definition));

                if (!ShortfallPaused && c.TrackShortfall(share, deltaTime) && !c.IsDue)
                {
                    c.Cancel(ledger);
                    ended.Add(c);
                }
                else if (c.IsDue)
                {
                    c.Resolve(ledger, baseRateFor(c.Definition));
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
