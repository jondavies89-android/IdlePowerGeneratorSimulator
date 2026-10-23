namespace IdlePowerGen
{
    // Events raised by the headless simulation. They live in the Simulation assembly so
    // that GeneratorInstance can publish them without referencing the runtime layer.

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
}
