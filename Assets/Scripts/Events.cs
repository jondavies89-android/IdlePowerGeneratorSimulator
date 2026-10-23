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

    // --- Persistence ---

    // Raised after a save (or a reset) has been applied. Owners of derived runtime state
    // (upgrade effects, crank value) rebuild it from the purchased-upgrade set.
    public readonly struct GameLoadedEvent { }

    public readonly struct OfflineProgressEvent
    {
        public readonly double SecondsAway;
        public readonly double JoulesEarned;
        public readonly string FlavorText;
        public OfflineProgressEvent(double seconds, double joules, string flavor)
        {
            SecondsAway = seconds; JoulesEarned = joules; FlavorText = flavor;
        }
    }

    // --- Era ---

    public readonly struct EraAdvancedEvent
    {
        public readonly int FromEra;
        public readonly int ToEra;
        public EraAdvancedEvent(int from, int to) { FromEra = from; ToEra = to; }
    }
}
