using System;

namespace IdlePowerGen.Simulation
{
    // The Era 1 crank's flywheel. Each turn of the crank adds spin, measured as the watts
    // the spin would deliver; spin is held to a capacity and decays exponentially to rest.
    // Magnets multiply what the spin delivers.
    //
    // Upgrade-derived parameters are rebuilt from the purchased levels on load, so only
    // the spin itself is state, and it's not saved: it runs down within seconds anyway.
    public class Era1Flywheel : IPowerSource
    {
        private readonly double _baseDecayPerSecond;

        // Turns land between 1 Hz ticks. The wheel is advanced to the moment of each turn
        // and the energy banked, so a turn's spin starts decaying when it was made.
        private double _advancedThisTick;
        private double _bankedJoules;

        public double CapacityWatts    { get; private set; }
        public double DecayMultiplier  { get; private set; } = 1.0;
        public double OutputMultiplier { get; private set; } = 1.0;

        // Spin before the magnets' multiplier, as of the last turn or tick.
        public double SpinWatts { get; private set; }

        public Era1Flywheel(double baseDecayPerSecond) => _baseDecayPerSecond = baseDecayPerSecond;

        public double DecayPerSecond => _baseDecayPerSecond * DecayMultiplier;

        public double CurrentOutputWatts   => SpinWatts * OutputMultiplier;

        // A flywheel left alone stops; it is never income while the player is away.
        public double SustainedOutputWatts => 0;

        // ---- Upgrade effects (each call is one level) ----

        public void ResetUpgrades()
        {
            CapacityWatts = 0; DecayMultiplier = 1.0; OutputMultiplier = 1.0;
            SpinWatts = 0;
        }

        public void AddCapacity(double watts)     => CapacityWatts    += watts;
        public void MultiplyDecay(double factor)  => DecayMultiplier  *= factor;
        public void MultiplyOutput(double factor) => OutputMultiplier *= factor;

        // ---- Play ----

        // secondsIntoTick: how far the current 1 Hz tick has run when the turn is made.
        public void AddSpin(double watts, double secondsIntoTick = 0)
        {
            AdvanceTo(secondsIntoTick);
            SpinWatts = Math.Min(CapacityWatts, SpinWatts + watts);
        }

        public void Stop()
        {
            SpinWatts = 0;
            _advancedThisTick = 0;
            _bankedJoules = 0;
        }

        // Spin at a point in the current tick, with no more turns. The UI uses this to
        // run the reading down between ticks; it doesn't change state.
        public double SpinAt(double secondsIntoTick) =>
            SpinAfter(SpinWatts, Math.Max(0, secondsIntoTick - _advancedThisTick));

        // Energy delivered so far this tick, magnets included; the next Tick pays it out.
        public double PendingJoules(double secondsIntoTick) =>
            (_bankedJoules + EnergyOver(SpinWatts, Math.Max(0, secondsIntoTick - _advancedThisTick)))
            * OutputMultiplier;

        public double Tick(double deltaTime)
        {
            AdvanceTo(deltaTime);
            double watts = deltaTime > 0 ? _bankedJoules / deltaTime * OutputMultiplier : 0;
            _advancedThisTick = 0;
            _bankedJoules = 0;
            return watts;
        }

        private void AdvanceTo(double secondsIntoTick)
        {
            double dt = secondsIntoTick - _advancedThisTick;
            if (dt <= 0) return;
            _advancedThisTick = secondsIntoTick;
            _bankedJoules += EnergyOver(SpinWatts, dt);
            SpinWatts = SpinAfter(SpinWatts, dt);
        }

        private double SpinAfter(double start, double seconds) =>
            start * Math.Exp(-DecayPerSecond * seconds);

        // Exact integral of the decaying spin, so the energy delivered doesn't depend on
        // step size (offline catch-up and live play agree).
        private double EnergyOver(double start, double seconds)
        {
            double k = DecayPerSecond;
            return k <= 0 ? start * seconds : start * (1 - Math.Exp(-k * seconds)) / k;
        }
    }
}
