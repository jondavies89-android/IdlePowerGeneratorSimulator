namespace IdlePowerGen
{
    // Events raised by the economy layer; kept in the Economy assembly for the same
    // reason SimulationEvents live in Simulation.

    public enum CurrencyType { Joules, Pennies, Reputation, Research }

    public readonly struct CurrencyChangedEvent
    {
        public readonly CurrencyType Currency;
        public readonly double NewValue;
        public CurrencyChangedEvent(CurrencyType type, double value) { Currency = type; NewValue = value; }
    }

    public readonly struct UpgradePurchasedEvent
    {
        public readonly Models.UpgradeDefinition Definition;
        public readonly int NewLevel;
        public UpgradePurchasedEvent(Models.UpgradeDefinition def, int newLevel) { Definition = def; NewLevel = newLevel; }
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
}
