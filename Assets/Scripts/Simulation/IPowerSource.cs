namespace IdlePowerGen.Simulation
{
    // Something on the grid that isn't a stackable generator, such as Era 1's flywheel.
    public interface IPowerSource
    {
        // Advances by deltaTime and returns the average watts delivered over it.
        double Tick(double deltaTime);

        // What it delivers right now.
        double CurrentOutputWatts { get; }

        // What it would keep delivering untended; offline progress pays on this.
        double SustainedOutputWatts { get; }

        // Energy delivered since the last tick, which that tick hasn't paid out yet.
        // Lets the UI show charge building between 1 Hz ticks.
        double PendingJoules(double secondsIntoTick);
    }
}
