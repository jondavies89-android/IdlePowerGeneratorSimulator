namespace IdlePowerGen.Data
{
    // Numeric constants for Era 1 — "The Workshop" (1 W → 1 kW, London 1821).
    // These values are the source of truth when creating ScriptableObject assets
    // in the Unity Editor. See todo/ERA1_Unity_Todo.md for the asset creation checklist.
    public static class Era1Data
    {
        public const int    EraNumber              = 1;
        public const double MinWatts               = 1.0;
        public const double MaxWatts               = 1_000.0;
        public const double InitialCrankTapValue   = 1.0;  // W per tap → 1 J per tap
        public const double FirstCustomerThreshold = 500.0; // stored ⚡ that unlocks first contract

        // §5.1.2 — One-time upgrades.  cost = prev × 5 (g = 5), each doubles CTV.
        // Layout: (id, displayName, costJoules, ctvMultiplier, prerequisiteId)
        public static readonly UpgradeSpec[] CrankUpgrades =
        {
            new("era1_better_crank",   "Better Crank",       5,     2.0, null),
            new("era1_iron_flywheel",  "Iron Flywheel",      25,    2.0, "era1_better_crank"),
            new("era1_gear_train",     "Gear Train",         125,   2.0, "era1_iron_flywheel"),
            new("era1_belt_drive",     "Leather Belt Drive", 600,   2.0, "era1_gear_train"),
            new("era1_cam_mechanism",  "Cam Mechanism",      3_000, 2.0, "era1_belt_drive"),
        };

        // §5.1.2 — Stackable passive generators. cost(n) = baseJ × 1.15^n.
        // Layout: (id, displayName, baseJoules, basePowerWatts, requiredUpgradeId)
        public static readonly GeneratorSpec[] PassiveGenerators =
        {
            new("spinning_jenny", "Spinning Jenny", 50,      1.0,   "era1_better_crank"),
            new("waterwheel",     "Waterwheel",     3_000,   20.0,  "era1_belt_drive"),
            new("steam_boiler",   "Steam Boiler",   15_000,  100.0, "era1_cam_mechanism"),
        };

        // §5.1.3 — Pacing milestones (stored joules → event)
        public const double Milestone_FirstPassiveGen = 50.0;
        public const double Milestone_FirstKilowatt   = 1_000.0;
        public const double Milestone_EraExit         = FirstCustomerThreshold;

        // §7.7 Era 1 offline flavour
        public const string OfflineFlavorText = "Your apprentice cranked while you were away.";

        // -------------------------------------------------------

        public readonly struct UpgradeSpec
        {
            public readonly string Id;
            public readonly string Name;
            public readonly double CostJoules;
            public readonly double CtvMultiplier;
            public readonly string PrerequisiteId;

            public UpgradeSpec(string id, string name, double cost, double ctv, string prereq)
            {
                Id = id; Name = name; CostJoules = cost; CtvMultiplier = ctv; PrerequisiteId = prereq;
            }
        }

        public readonly struct GeneratorSpec
        {
            public readonly string Id;
            public readonly string Name;
            public readonly double BaseJoules;
            public readonly double BasePowerWatts;
            public readonly string RequiredUpgradeId;

            public GeneratorSpec(string id, string name, double baseJ, double watts, string reqUpgrade)
            {
                Id = id; Name = name; BaseJoules = baseJ; BasePowerWatts = watts;
                RequiredUpgradeId = reqUpgrade;
            }
        }
    }
}
