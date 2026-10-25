namespace IdlePowerGen.Data
{
    // Numeric constants for Era 5 — "The Interconnected Grid" (1 GW → 50 GW, 1952).
    public static class Era5Data
    {
        public const int    EraNumber = 5;
        public const double MinWatts  = 1e9;
        public const double MaxWatts  = 50e9;

        // What a player typically brings from Era 4.
        public const double TypicalStartPounds     = 4_000_000;
        public const float  TypicalStartReputation = 55f;
    }
}
