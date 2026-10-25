using IdlePowerGen.Models;

namespace IdlePowerGen.Data
{
    // Numeric constants for Era 2 — "The Demonstration" (1 kW → 100 kW, London 1851).
    // The source of truth for Era2AssetBuilder, which writes the ScriptableObjects.
    //
    // The era's one new concept (bible §2.2): demand isn't constant. Customers ask for power
    // in their own window of the day, so what the works can sell rises and falls with the
    // clock. Pennies from contracts pay for everything physical (bible §9.2: infrastructure
    // costs cash); reputation from contracts opens bigger clients and some upgrades.
    public static class Era2Data
    {
        public const int    EraNumber = 2;
        public const double MinWatts  = 1_000.0;
        public const double MaxWatts  = 100_000.0;

        // Pennies per watt per game hour: a contract pays hours × watts × this × its multiplier.
        // The bible's 0.001 was set before running costs and penny-priced machines existed; at
        // that rate an apprentice's 50 W couldn't cover his own 1 d/hr wage. A pacing sim
        // (greedy bookings, best watts per penny) finishes the era in ~4 hours at 0.3 and
        // ~2 hours at 0.6, inside the bible's 2–8 hour band; 0.4 sits near three.
        public const double ContractBaseRate = 0.4;

        // Spare power sold to the street, as a fraction of ContractBaseRate (then shaped by the
        // hour: dearer at the evening lamp-lighting, cheap before dawn). Without it, between
        // engagements the purse stood still and the first hour had ten-minute waits. The pacing
        // sim at 0.2 brings the longest first-hour wait to ~3 min (bible §6.2: 1–3) and the era
        // to ~2.4 h, still in the 2–8 h band. Kept well under 1 even with both street upgrades
        // (×2, ×1.5 → 0.6) so a booked client always pays better, and only clients bring repute.
        public const float StreetRateFactor = 0.2f;

        public const float  OfflineEraFactor  = 1.0f;
        public const float  OfflineTauHours   = 4.0f;
        public const string OfflineFlavorText = "Your apprentice kept the fires burning while you were away.";

        // Era 2 opens in the evening (bible Appendix A), when the street lamps want lighting.
        public const float  StartHour  = 18f;
        public const int    StartYear  = 1851;
        public const int    StartMonth = 10;
        public const int    StartDay   = 9;

        // Engagements that can run at once before Telegraph Lines.
        public const int BaseContractSlots = 2;

        // Repute shows as five stars, one per this much reputation.
        public const float ReputationPerStar = 6f;

        // Feature flags (UpgradeEffect.UnlockFeature). Saved via the upgrade ids that set them.
        public const string ClerkFeatureId        = "feature_clerk";
        public const string CoalMerchantFeatureId = "feature_coal_merchant";

        // -------------------------------------------------------------------------------
        // Generators — bible §5.1.5. Stackable: cost(n) = base × 1.15^n, in pennies.
        // Running costs are per game hour (one real minute): wages come out of the purse,
        // coal out of the hopper.
        // -------------------------------------------------------------------------------

        public static readonly GeneratorSpec[] Generators =
        {
            //  type                             name                     cost     watts   wage coal unlocked by
            new(GeneratorType.SmallDynamo,      "Apprentice on dynamo",  50,      50,     1,   0,   null,
                "A lad on a treadle dynamo. Paid by the hour."),
            new(GeneratorType.Waterwheel,       "Water wheel",           500,     200,    0,   0,   null,
                "Another wheel on the mill-race. No fuel, no wages."),
            new(GeneratorType.SteamEngine,      "Steam engine",          2_000,   800,    0,   1,   WattPatentId,
                "A beam engine and a boiler. It eats coal."),
            new(GeneratorType.LargeSteamEngine, "Large steam engine",    10_000,  4_000,  0,   4,   BoultonWattId,
                "Boulton & Watt's best. It eats a great deal of coal."),
        };

        // -------------------------------------------------------------------------------
        // Contracts — bible §5.1.6. Pay = hours × watts × ContractBaseRate × pay multiplier.
        // Below 50 % of the watts asked for on average: −2 reputation, pro-rated pay.
        // Windows follow the shape of a day's demand (bible §3.4): bakeries before dawn,
        // mills and presses in the morning, lamps and theatres in the evening.
        // -------------------------------------------------------------------------------

        public const string ExhibitionContractId = "c2_exhibition";

        public static readonly ContractSpec[] Contracts =
        {
            //  id                 client                          watts    hours from  to   rep   +rep pay  tier
            new("c2_street_lamps", "Street Lamp Row",              500,     5,    17,   21,  0,    1,   1.0f, ContractTier.Standard,
                "The parish wants Fleet Street lit from dusk."),
            new("c2_bakery",       "Whitmore's Bakery",            800,     3,    3,    6,   0,    1,   1.0f, ContractTier.Standard,
                "Lamps and a mixer before dawn. The bread won't wait."),
            new("c2_hotel",        "The Grand Hotel",              1_200,   8,    0,    24,  0,    1,   1.0f, ContractTier.Standard,
                "Lamps in every corridor, day and night."),
            new("c2_print_works",  "City Print Works",             3_000,   8,    6,    10,  3,    1.5f, 1.0f, ContractTier.Standard,
                "The morning presses need driving."),
            new("c2_theatre",      "Theatre Royal, Drury Lane",    6_000,   4,    17,   20,  6,    2,   1.0f, ContractTier.Standard,
                "The manager wants electric light for tonight's play."),
            // Bible §5.1.6: reputation-tier contracts pay 1.3–1.6× and add a reputation bonus.
            new("c2_royal_society","Royal Society demonstration",  10_000,  2,    18,   20,  10,   3,   1.5f, ContractTier.Reputation,
                "Mr Faraday himself will attend."),
            new("c2_silk_mill",    "Spitalfields Silk Mill",       20_000,  10,   5,    8,   14,   2,   1.0f, ContractTier.Standard,
                "Looms from first light."),
            new("c2_station",      "Great Western Railway",        40_000,  12,   0,    24,  18,   3,   1.0f, ContractTier.Standard,
                "The new terminus at Paddington wants its platforms lit."),
            new("c2_strand",       "Shopfronts of the Strand",     60_000,  5,    16,   19,  22,   3,   1.0f, ContractTier.Standard,
                "Every window lit for the evening crowds."),
            // The era's capstone: holding it ends Era 2.
            new(ExhibitionContractId, "The Great Exhibition",      100_000, 8,    8,    10,  28,   5,   1.6f, ContractTier.Reputation,
                "Light the Crystal Palace for its closing week. All London will see it."),
        };

        // -------------------------------------------------------------------------------
        // Upgrades, paid in pennies. Reputation is a gate, not a cost (CLAUDE.md).
        // One-time tiers grow roughly ×5–×8 apart (bible §9.3) and each about doubles
        // what it acts on (bible §6.2).
        //
        // Laid out as a tree in three branches (columns), each row deeper than its
        // prerequisites. The two late generalists sit where branches meet: the Master
        // Engineer needs both water and steam, the Royal Warrant crowns the tree.
        // -------------------------------------------------------------------------------

        public const int HandsBranch = 0, SteamBranch = 1, TradeBranch = 2;
        public static readonly string[] BranchNames = { "Hands & Water", "Steam", "Trade" };

        // Ids are saved, so never rename one.
        public const string StreetCounterId      = "era2_street_counter";
        public const string ArcLampsId           = "era2_arc_lamps";
        public const string ApprenticeTrainingId = "era2_apprentice_training";
        public const string MillRaceWeirId       = "era2_millrace_weir";
        public const string WattPatentId         = "era2_watt_patent";
        public const string ClerkId              = "era2_clerk";
        public const string TelegraphId          = "era2_telegraph";
        public const string PressureControlId    = "era2_pressure_control";
        public const string CoalMerchantId       = "era2_coal_merchant";
        public const string BoultonWattId        = "era2_boulton_watt";
        public const string CompoundCylindersId  = "era2_compound_cylinders";
        public const string MasterEngineerId     = "era2_master_engineer";
        public const string RoyalWarrantId       = "era2_royal_warrant";

        public static readonly UpgradeSpec[] Upgrades =
        {
            //  id                    name                          cost       growth max rep  effect                                 value unlocks                               branch       row
            // Hands & Water
            new(ApprenticeTrainingId, "Apprentice Training",        150,       1,     1,  0,   UpgradeEffect.MultiplyGeneratorOutput, 2,    nameof(GeneratorType.SmallDynamo),      HandsBranch, 0, new string[0],
                "A month at the Polytechnic. They pedal smarter."),
            new(MillRaceWeirId,       "Mill-race Weir",             2_000,     1,     1,  0,   UpgradeEffect.MultiplyGeneratorOutput, 2,    nameof(GeneratorType.Waterwheel),       HandsBranch, 1, new[] { ApprenticeTrainingId },
                "A weir upstream gives the wheels a steadier head of water."),
            new(MasterEngineerId,     "Master Engineer",            300_000,   1,     1,  18,  UpgradeEffect.MultiplyAllOutput,       1.5,  null,                                   HandsBranch, 4, new[] { MillRaceWeirId, BoultonWattId },
                "A man who has run a works before. Everything runs better."),

            // Steam. The merchant comes before the pressure gauges: no point running the
            // engines harder on a hopper that keeps running dry.
            new(WattPatentId,         "Watt's Engine Patent",       1_000,     1,     1,  3,   UpgradeEffect.UnlockGenerator,         0,    nameof(GeneratorType.SteamEngine),      SteamBranch, 0, new string[0],
                "The licence to build a proper steam engine."),
            new(CoalMerchantId,       "Coal Merchant's Account",    4_000,     1,     1,  0,   UpgradeEffect.UnlockFeature,           0,    CoalMerchantFeatureId,                  SteamBranch, 1, new[] { WattPatentId },
                "The merchant tops up the hopper whenever it runs low."),
            new(PressureControlId,    "Steam Pressure Control",     6_000,     1,     1,  0,   UpgradeEffect.MultiplyGeneratorOutput, 2,    nameof(GeneratorType.SteamEngine),      SteamBranch, 2, new[] { CoalMerchantId },
                "Governors and gauges. The engines run harder without bursting."),
            new(BoultonWattId,        "Boulton & Watt Partnership", 25_000,    1,     1,  10,  UpgradeEffect.UnlockGenerator,         0,    nameof(GeneratorType.LargeSteamEngine), SteamBranch, 3, new[] { PressureControlId },
                "The Soho works will build you their largest engines."),
            new(CompoundCylindersId,  "Compound Cylinders",         120_000,   1,     1,  0,   UpgradeEffect.MultiplyGeneratorOutput, 2,    nameof(GeneratorType.LargeSteamEngine), SteamBranch, 4, new[] { BoultonWattId },
                "The steam does its work twice before it leaves."),
            new(RoyalWarrantId,       "Royal Warrant",              1_000_000, 1,     1,  24,  UpgradeEffect.MultiplyAllOutput,       2,    null,                                   SteamBranch, 5, new[] { MasterEngineerId, CompoundCylindersId },
                "By appointment to Her Majesty. Every client wants the best."),

            // Trade
            new(StreetCounterId,      "A Counter on the Street",    300,       1,     1,  0,   UpgradeEffect.MultiplyStreetTrade,     2,    null,                                   TradeBranch, 0, new string[0],
                "A shopfront where the neighbours buy their power by the hour."),
            new(ClerkId,              "A Clerk at the Desk",        600,       1,     1,  4,   UpgradeEffect.UnlockFeature,           0,    ClerkFeatureId,                         TradeBranch, 1, new[] { StreetCounterId },
                "Books any engagement the works can fully supply while you see to the machines."),
            // Two levels, ×10 apart: a third and a fourth engagement at once.
            new(TelegraphId,          "Telegraph Lines",            1_500,     10,    2,  5,   UpgradeEffect.AddContractSlot,         1,    null,                                   TradeBranch, 2, new[] { ClerkId },
                "More clients can reach you by wire."),
            new(ArcLampsId,           "Arc Lamps for the Parish",   40_000,    1,     1,  12,  UpgradeEffect.MultiplyStreetTrade,     1.5,  null,                                   TradeBranch, 3, new[] { TelegraphId },
                "The vestry lights the whole street from your works, and pays for it."),
        };

        // -------------------------------------------------------

        public readonly struct GeneratorSpec
        {
            public readonly GeneratorType Type;
            public readonly string        Name;
            public readonly double        CostPennies;
            public readonly double        Watts;
            public readonly double        WagePerHour;
            public readonly double        CoalPerHour;
            public readonly string        UnlockedBy;
            public readonly string        Description;

            public GeneratorSpec(GeneratorType type, string name, double cost, double watts,
                                 double wage, double coal, string unlockedBy, string description)
            {
                Type = type; Name = name; CostPennies = cost; Watts = watts;
                WagePerHour = wage; CoalPerHour = coal; UnlockedBy = unlockedBy; Description = description;
            }
        }

        public readonly struct ContractSpec
        {
            public readonly string       Id;
            public readonly string       Client;
            public readonly double       Watts;
            public readonly float        Hours;
            public readonly float        FromHour;
            public readonly float        ToHour;
            public readonly float        RequiredReputation;
            public readonly float        ReputationOnSuccess;
            public readonly float        PayMultiplier;
            public readonly ContractTier Tier;
            public readonly string       Description;

            public ContractSpec(string id, string client, double watts, float hours, float from, float to,
                                float rep, float repOnSuccess, float payMult, ContractTier tier, string description)
            {
                Id = id; Client = client; Watts = watts; Hours = hours; FromHour = from; ToHour = to;
                RequiredReputation = rep; ReputationOnSuccess = repOnSuccess; PayMultiplier = payMult;
                Tier = tier; Description = description;
            }
        }

        public readonly struct UpgradeSpec
        {
            public readonly string        Id;
            public readonly string        Name;
            public readonly double        CostPennies;
            public readonly double        CostGrowth;
            public readonly int           MaxLevel;
            public readonly float         RequiredReputation;
            public readonly UpgradeEffect Effect;
            public readonly double        Value;
            public readonly string        UnlocksId;
            public readonly int           Branch;
            public readonly int           Row;
            public readonly string[]      Prerequisites;
            public readonly string        Description;

            public UpgradeSpec(string id, string name, double cost, double growth, int maxLevel, float rep,
                               UpgradeEffect effect, double value, string unlocksId, int branch, int row,
                               string[] prereqs, string description)
            {
                Id = id; Name = name; CostPennies = cost; CostGrowth = growth; MaxLevel = maxLevel;
                RequiredReputation = rep; Effect = effect; Value = value; UnlocksId = unlocksId;
                Branch = branch; Row = row; Prerequisites = prereqs; Description = description;
            }
        }
    }
}
