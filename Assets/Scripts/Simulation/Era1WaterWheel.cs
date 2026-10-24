namespace IdlePowerGen.Simulation
{
    // The Era 1 water wheel: steady power from the river, generated separately from the
    // flywheel and added to it on the grid. The magnets don't act on it; belts and
    // gearing multiply it. It is the era's only income that keeps coming untended.
    public class Era1WaterWheel : IPowerSource
    {
        // Before the gearing's multiplier.
        public double BaseWatts        { get; private set; }
        public double OutputMultiplier { get; private set; } = 1.0;

        public double CurrentOutputWatts   => BaseWatts * OutputMultiplier;
        public double SustainedOutputWatts => CurrentOutputWatts;

        public void ResetUpgrades() { BaseWatts = 0; OutputMultiplier = 1.0; }

        public void AddWatts(double watts)        => BaseWatts        += watts;
        public void MultiplyOutput(double factor) => OutputMultiplier *= factor;

        public double Tick(double deltaTime) => CurrentOutputWatts;

        public double PendingJoules(double secondsIntoTick) => CurrentOutputWatts * secondsIntoTick;
    }
}
