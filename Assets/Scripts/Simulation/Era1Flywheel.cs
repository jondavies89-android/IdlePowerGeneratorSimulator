using System;

namespace IdlePowerGen.Simulation
{
    // The Era 1 crank's flywheel. Each turn of the crank adds spin, measured as the watts
    // the spin would deliver; spin is held to a capacity and decays exponentially. A water
    // wheel sets a floor the spin never falls below, which is the era's only income that
    // doesn't need the player. Magnets multiply what the spin delivers.
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
        public double FloorWatts       { get; private set; }
        public double DecayMultiplier  { get; private set; } = 1.0;
        public double OutputMultiplier { get; private set; } = 1.0;

        // Spin before the magnets' multiplier, as of the last turn or tick.
        public double SpinWatts { get; private set; }

        public Era1Flywheel(double baseDecayPerSecond) => _baseDecayPerSecond = baseDecayPerSecond;

        public double DecayPerSecond => _baseDecayPerSecond * DecayMultiplier;

        // The floor can exceed the capacity: the water wheel turns the flywheel regardless.
        public double MaxSpinWatts => Math.Max(CapacityWatts, FloorWatts);

        public double CurrentOutputWatts   => Math.Max(SpinWatts, FloorWatts) * OutputMultiplier;
        public double SustainedOutputWatts => FloorWatts * OutputMultiplier;

        // ---- Upgrade effects (each call is one level) ----

        public void ResetUpgrades()
        {
            CapacityWatts = 0; FloorWatts = 0; DecayMultiplier = 1.0; OutputMultiplier = 1.0;
            SpinWatts = Math.Min(SpinWatts, MaxSpinWatts);
        }

        public void AddCapacity(double watts)     => CapacityWatts    += watts;
        public void AddFloor(double watts)        => FloorWatts       += watts;
        public void MultiplyDecay(double factor)  => DecayMultiplier  *= factor;
        public void MultiplyOutput(double factor) => OutputMultiplier *= factor;

        // ---- Play ----

        // secondsIntoTick: how far the current 1 Hz tick has run when the turn is made.
        public void AddSpin(double watts, double secondsIntoTick = 0)
        {
            AdvanceTo(secondsIntoTick);
            SpinWatts = Math.Min(MaxSpinWatts, Math.Max(SpinWatts, FloorWatts) + watts);
        }

        public void Stop()
        {
            SpinWatts = 0;
            _advancedThisTick = 0;
            _bankedJoules = 0;
        }

        // Spin at a point in the current tick, with no more turns. The UI uses this to
        // run the reading down smoothly; it doesn't change state.
        public double SpinAt(double secondsIntoTick)
        {
            double ahead = Math.Max(0, secondsIntoTick - _advancedThisTick);
            return SpinAfter(Math.Max(SpinWatts, FloorWatts), ahead);
        }

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

            double start = Math.Max(SpinWatts, FloorWatts);
            _bankedJoules += EnergyOver(start, dt);
            SpinWatts = SpinAfter(start, dt);
        }

        private double SpinAfter(double start, double seconds) =>
            Math.Max(FloorWatts, start * Math.Exp(-DecayPerSecond * seconds));

        // Exact integral of the decaying spin, so the energy delivered doesn't depend on
        // step size (offline catch-up and live play agree).
        private double EnergyOver(double start, double seconds)
        {
            double k = DecayPerSecond;
            if (start <= FloorWatts || k <= 0) return start * seconds;

            // Time until the spin decays to the floor (never, with no floor).
            double tFloor = FloorWatts > 0 ? Math.Log(start / FloorWatts) / k : double.PositiveInfinity;
            double tDecay = Math.Min(seconds, tFloor);
            return start * (1 - Math.Exp(-k * tDecay)) / k + FloorWatts * (seconds - tDecay);
        }
    }
}
