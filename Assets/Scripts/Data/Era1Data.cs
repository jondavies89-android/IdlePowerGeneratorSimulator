using IdlePowerGen.Models;

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
        public const double InitialCrankTapValue   = 1.0;  // J per turn before any upgrade
        public const double FirstCustomerThreshold = 500.0; // stored ⚡ that unlocks first contract

        // Without bearings the flywheel loses a quarter of its spin each second, so a turn's
        // charge is mostly spent within ~10 s: the player feels it run down and keeps cranking.
        public const double FlywheelBaseDecayPerSecond = 0.25;

        // Upgrade ids. Saved, so never rename one.
        public const string BetterCrankId  = "era1_better_crank";
        public const string IronFlywheelId = "era1_iron_flywheel";
        public const string BearingsId     = "era1_bearings";
        public const string MagnetsId      = "era1_magnets";
        public const string WaterWheelId   = "era1_water_wheel";
        public const string BeltsGearingId = "era1_belts_gearing";

        // The whole Era 1 tree: crank → flywheel → bearings, magnets, water wheel → gearing.
        // The water wheel is the era's only passive income. It generates separately from
        // the flywheel; both feed the one dynamo, so the magnets multiply both.
        //
        // Levelled upgrades grow ×1.5 a level rather than the generators' ×1.15: their
        // effects compound through crank → flywheel → magnets, and at ×1.15 a greedy player
        // cleared the era in about a minute. Bearings and magnets are tiers, so they use the
        // one-time g = 5. A pacing sim (always buying the cheapest) reaches a tapped 1 kW and
        // 500 W untended at ~6 min tapping 4 times a second, ~9 min at 2 a second.
        public static readonly UpgradeSpec[] Upgrades =
        {
            //  id              name                 effect                              value secondary cost   growth max prerequisite
            new(BetterCrankId,  "Better Crank",      UpgradeEffect.AddCrankJoules,        1,    0,        15,    1.5,  20, null),
            new(IronFlywheelId, "Iron Flywheel",     UpgradeEffect.AddFlywheelCapacity,   5,    0,        50,    1.5,  20, BetterCrankId),
            new(BearingsId,     "Brass Bearings",    UpgradeEffect.MultiplyFlywheelDecay, 0.8,  0,        200,   5,    4,  IronFlywheelId),
            // Each magnet doubles what the crank and flywheel deliver, but the stronger
            // field drags on the wheel: 25 % faster decay, which bearings can buy back.
            new(MagnetsId,      "Lodestone Magnets", UpgradeEffect.MultiplyCrankOutput,   2,    1.25,     750,   5,    4,  IronFlywheelId),
            new(WaterWheelId,   "Water Wheel",       UpgradeEffect.AddWaterWheelWatts,    25,   0,        5_000, 1,    1,  IronFlywheelId),
            new(BeltsGearingId, "Belts & Gearing",   UpgradeEffect.AddWaterWheelWatts,    10,   0,        6_000, 1.5,  20, WaterWheelId),
        };

        // §5.1.3 — Pacing milestones (stored joules → event)
        public const double Milestone_FirstPassiveGen = 50.0;
        public const double Milestone_FirstKilowatt   = 1_000.0;
        public const double Milestone_EraExit         = FirstCustomerThreshold;

        // Todo §6 — stored-charge toasts. Ids are saved, so never rename one.
        public const string FirstMachineMilestoneId  = "era1_first_machine";
        public const string FirstCustomerMilestoneId = "era1_neighbour_knocks";

        public static readonly MilestoneSpec[] Milestones =
        {
            new(FirstMachineMilestoneId,  Milestone_FirstPassiveGen, "Your first machine hums to life."),
            new(FirstCustomerMilestoneId, FirstCustomerThreshold,    "A neighbour knocks — they want your power."),
        };

        // The neighbour's contract closes the era. Bible §5.1.3: Era 2 unlocks at 1 kW
        // sustained, so the contract asks for the era's top wattage for one in-game hour
        // (one real minute at GameClock's 60×). Pay multiplier 12 makes the pay
        // 1 h × 1000 W × 0.001 × 12 = 12d — one shilling, the first cash of bible §9.2.
        public const string FirstCustomerContractId = "era1_first_customer";
        public const float  FirstCustomerHours      = 1f;
        public const float  FirstCustomerPayMult    = 12f;

        // §7.7 Era 1 offline flavour
        public const string OfflineFlavorText = "Your apprentice cranked while you were away.";

        // -------------------------------------------------------

        public readonly struct UpgradeSpec
        {
            public readonly string        Id;
            public readonly string        Name;
            public readonly UpgradeEffect Effect;
            public readonly double        Value;
            public readonly double        SecondaryValue;
            public readonly double        CostJoules;
            public readonly double        CostGrowth;
            public readonly int           MaxLevel;
            public readonly string        PrerequisiteId;

            public UpgradeSpec(string id, string name, UpgradeEffect effect, double value, double secondary,
                               double cost, double growth, int maxLevel, string prereq)
            {
                Id = id; Name = name; Effect = effect; Value = value; SecondaryValue = secondary;
                CostJoules = cost; CostGrowth = growth; MaxLevel = maxLevel; PrerequisiteId = prereq;
            }
        }

        public readonly struct MilestoneSpec
        {
            public readonly string Id;
            public readonly double StoredJoules;
            public readonly string Message;

            public MilestoneSpec(string id, double joules, string message)
            {
                Id = id; StoredJoules = joules; Message = message;
            }
        }
    }
}
