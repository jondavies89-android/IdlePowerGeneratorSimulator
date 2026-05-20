namespace IdlePowerGen
{
    // --- Simulation ---

    public readonly struct SimTickEvent
    {
        public readonly Simulation.PowerGrid Grid;
        public readonly double DeltaTime;
        public SimTickEvent(Simulation.PowerGrid grid, double dt) { Grid = grid; DeltaTime = dt; }
    }

    public readonly struct GeneratorTripEvent
    {
        public readonly Simulation.GeneratorInstance Generator;
        public readonly string Reason;
        public GeneratorTripEvent(Simulation.GeneratorInstance gen, string reason) { Generator = gen; Reason = reason; }
    }

    public readonly struct GeneratorPurchasedEvent
    {
        public readonly Models.GeneratorDefinition Definition;
        public readonly int TotalOwned;
        public GeneratorPurchasedEvent(Models.GeneratorDefinition def, int count) { Definition = def; TotalOwned = count; }
    }

    // --- Economy ---

    public enum CurrencyType { Joules, Pennies, Reputation }

    public readonly struct CurrencyChangedEvent
    {
        public readonly CurrencyType Currency;
        public readonly double NewValue;
        public CurrencyChangedEvent(CurrencyType type, double value) { Currency = type; NewValue = value; }
    }

    public readonly struct UpgradePurchasedEvent
    {
        public readonly Models.UpgradeDefinition Definition;
        public UpgradePurchasedEvent(Models.UpgradeDefinition def) { Definition = def; }
    }

    public readonly struct ContractAcceptedEvent
    {
        public readonly Models.ContractDefinition Definition;
        public ContractAcceptedEvent(Models.ContractDefinition def) { Definition = def; }
    }

    public readonly struct ContractCompletedEvent
    {
        public readonly Economy.ContractInstance Contract;
        public readonly decimal PenniesEarned;
        public ContractCompletedEvent(Economy.ContractInstance c, decimal pay) { Contract = c; PenniesEarned = pay; }
    }

    public readonly struct ContractFailedEvent
    {
        public readonly Economy.ContractInstance Contract;
        public ContractFailedEvent(Economy.ContractInstance c) { Contract = c; }
    }

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
