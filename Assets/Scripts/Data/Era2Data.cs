namespace IdlePowerGen.Data
{
    // Numeric constants for Era 2 — "The Demonstration" (1 kW → 100 kW, London 1851).
    // See todo/ERA2_Unity_Todo.md for Unity asset creation checklist.
    public static class Era2Data
    {
        public const int    EraNumber            = 2;
        public const double MinWatts             = 1_000.0;
        public const double MaxWatts             = 100_000.0;
        public const double ContractBaseRate     = 0.001; // pennies per W·s  §5.1.6
        public const float  OfflineEraFactor     = 1.0f;
        public const float  OfflineTauHours      = 4.0f;

        // §5.1.5 — Era 2 generators.  cost(n) = baseJ × 1.15^n.
        // Fuel consumption > 0 means the generator requires re-fuelling.
        // Layout: (id, displayName, baseJoules, basePowerWatts, fuelPerSec)
        public static readonly GeneratorSpec[] Generators =
        {
            new("era2_small_dynamo",       "Small Dynamo",        50,      10.0,   0.0),
            new("era2_gas_lamp_gen",       "Gas Lamp Generator",  500,     100.0,  0.0002),
            new("era2_coal_boiler",        "Coal-Fired Boiler",   2_500,   500.0,  0.0005),
            new("era2_steam_engine",       "Steam Engine",        12_500,  1_000.0, 0.001),
            new("era2_large_steam_engine", "Large Steam Engine",  62_500,  5_000.0, 0.002),
        };

        // §5.1.6 — Era 2 contract definitions.
        // pay = durationHours × requiredWatts × ContractBaseRate × payMult
        // Layout: (id, clientName, reqW, durationH, payMult, repSuccess, tier, startH, endH)
        public static readonly ContractSpec[] Contracts =
        {
            // Standard — time-of-day constrained
            new("c2_street_lamp",  "Street Lamp Row",       50,    4f,  1.0f, 0.5f, 0, 18f, 22f),
            new("c2_bakery",       "Whitmore's Bakery",    200,    2f,  1.0f, 0.5f, 0,  5f,  8f),
            new("c2_print_works",  "City Print Works",     500,    8f,  1.1f, 1.0f, 0,  6f, 18f),
            new("c2_hotel",        "The Grand Hotel",    1_000,   12f,  1.2f, 1.5f, 0,  0f, 24f),
            // Prestige — higher pay × 1.3–1.6, reputation bonus §5.1.6
            new("c2_exhibition",   "The Great Exhibition", 5_000,  6f,  1.5f, 5.0f, 1,  9f, 17f),
        };

        // §3.1 — Era 2 base demand that the contract system draws from
        public const double BaseDemandWatts = 1_000.0; // 1 kW initial city load

        // ---------------------------------------------------------------
        // Era 2 upgrades.
        // Bible §5.1.4: pennies are spent on upgrades; joules buy equipment.
        // All Era 2 upgrades are paid in pennies (decimal).
        // ---------------------------------------------------------------

        // Generator unlock gates — UpgradeEffect.UnlockGenerator with unlocksId = GeneratorType name.
        // Player starts Era 2 with SmallDynamo already accessible (free upgrade purchased on transition).
        // Layout: (id, displayName, costPennies, unlocksGeneratorTypeName, prerequisiteId)
        public static readonly UnlockSpec[] GeneratorUnlocks =
        {
            new("era2_unlock_gas_lamp",      "Gas Generator Schematic",   5m,    "GasLampGenerator",  null),
            new("era2_unlock_coal_boiler",   "Coal Boiler Licence",       25m,   "CoalFiredBoiler",   "era2_unlock_gas_lamp"),
            new("era2_unlock_steam_engine",  "Watt Steam Engine Patent",  125m,  "SteamEngine",       "era2_unlock_coal_boiler"),
            new("era2_unlock_large_steam",   "Boulton & Watt Partnership", 625m, "LargeSteamEngine",  "era2_unlock_steam_engine"),
        };

        // Efficiency upgrades — UpgradeEffect.MultiplyAllOutput.
        // §9.3 one-time cost growth g = 5.
        // Layout: (id, displayName, costPennies, multiplier, prerequisiteId)
        public static readonly EfficiencySpec[] EfficiencyUpgrades =
        {
            new("era2_apprentice_training", "Apprentice Training",     10m,    1.10, null),
            new("era2_reinforced_boilers",  "Reinforced Boilers",      50m,    1.15, "era2_unlock_coal_boiler"),
            new("era2_pressure_control",    "Steam Pressure Control",  250m,   1.25, "era2_unlock_steam_engine"),
            new("era2_master_engineer",     "Master Engineer",         1_250m, 1.50, "era2_pressure_control"),
        };

        // Reputation-gated prestige upgrades — paid in pennies, with a reputation requirement.
        // Layout: (id, displayName, costPennies, reputationRequired, multiplier, featureId)
        public static readonly PrestigeUpgradeSpec[] PrestigeUpgrades =
        {
            // Telegraph: unlocks a feature flag the UI reads to display more contracts at once.
            new("era2_telegraph_lines", "Telegraph Lines",     500m,   10f, 0.0, "feature_extra_contract_slot"),
            // Royal warrant: global x2 output multiplier; the Era 2 capstone.
            new("era2_royal_warrant",   "Royal Warrant",       5_000m, 25f, 2.0, null),
        };

        // -------------------------------------------------------

        // -------------------------------------------------------

        public readonly struct GeneratorSpec
        {
            public readonly string Id;
            public readonly string Name;
            public readonly double BaseJoules;
            public readonly double BasePowerWatts;
            public readonly double FuelPerSecond;

            public GeneratorSpec(string id, string name, double baseJ,
                                 double watts, double fuelPerSec)
            {
                Id = id; Name = name; BaseJoules = baseJ;
                BasePowerWatts = watts; FuelPerSecond = fuelPerSec;
            }
        }

        public readonly struct UnlockSpec
        {
            public readonly string  Id;
            public readonly string  Name;
            public readonly decimal CostPennies;
            public readonly string  UnlocksGeneratorType;
            public readonly string  PrerequisiteId;

            public UnlockSpec(string id, string name, decimal cost, string unlocks, string prereq)
            {
                Id = id; Name = name; CostPennies = cost;
                UnlocksGeneratorType = unlocks; PrerequisiteId = prereq;
            }
        }

        public readonly struct EfficiencySpec
        {
            public readonly string  Id;
            public readonly string  Name;
            public readonly decimal CostPennies;
            public readonly double  Multiplier;
            public readonly string  PrerequisiteId;

            public EfficiencySpec(string id, string name, decimal cost, double mult, string prereq)
            {
                Id = id; Name = name; CostPennies = cost;
                Multiplier = mult; PrerequisiteId = prereq;
            }
        }

        public readonly struct PrestigeUpgradeSpec
        {
            public readonly string  Id;
            public readonly string  Name;
            public readonly decimal CostPennies;
            public readonly float   ReputationRequired;
            public readonly double  Multiplier;     // 0 = no output effect (feature unlock only)
            public readonly string  UnlocksFeatureId;

            public PrestigeUpgradeSpec(string id, string name, decimal cost, float rep,
                                       double mult, string featureId)
            {
                Id = id; Name = name; CostPennies = cost; ReputationRequired = rep;
                Multiplier = mult; UnlocksFeatureId = featureId;
            }
        }

        public readonly struct ContractSpec
        {
            public readonly string Id;
            public readonly string ClientName;
            public readonly double RequiredWatts;
            public readonly float  DurationHours;
            public readonly float  PayMultiplier;
            public readonly float  ReputationOnSuccess;
            public readonly int    Tier;             // 0 = Standard, 1 = Prestige
            public readonly float  StartHour;
            public readonly float  EndHour;

            public ContractSpec(string id, string client, double reqW, float hours,
                                float payMult, float rep, int tier, float start, float end)
            {
                Id = id; ClientName = client; RequiredWatts = reqW; DurationHours = hours;
                PayMultiplier = payMult; ReputationOnSuccess = rep; Tier = tier;
                StartHour = start; EndHour = end;
            }
        }
    }
}
