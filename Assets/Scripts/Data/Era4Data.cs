namespace IdlePowerGen.Data
{
    // Numeric constants for Era 4 — "The Grid Awakens" (10 MW → 1 GW, 1928).
    public static class Era4Data
    {
        public const int    EraNumber = 4;
        public const double MinWatts  = 10e6;
        public const double MaxWatts  = 1e9;

        // What a player typically brings from Era 3: the Exhibition's takings and savings.
        public const double TypicalStartPounds     = 60_000;
        public const float  TypicalStartReputation = 40f;
    }
}
