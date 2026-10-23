namespace IdlePowerGen
{
    // Runtime-layer events. Simulation and economy events live beside their assemblies
    // (Simulation/Events/SimulationEvents.cs, Economy/EconomyEvents.cs).

    // --- Interaction ---

    public readonly struct TapEvent
    {
        public readonly double WattsPerTap;
        public readonly double JoulesProduced;
        public TapEvent(double wpt, double j) { WattsPerTap = wpt; JoulesProduced = j; }
    }

    // --- Era ---

    public readonly struct EraAdvancedEvent
    {
        public readonly int FromEra;
        public readonly int ToEra;
        public EraAdvancedEvent(int from, int to) { FromEra = from; ToEra = to; }
    }
}
