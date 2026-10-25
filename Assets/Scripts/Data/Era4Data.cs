using IdlePowerGen.Models;

namespace IdlePowerGen.Data
{
    // Numeric constants for Era 4 — "The Grid Awakens" (10 MW → 1 GW, 1928).
    // The source of truth for Era4AssetBuilder.
    //
    // The era's one new concept (bible §2.2): the grid has a heartbeat. Frequency wobbles
    // whenever load and generation part company; every unit is brought on by hand, matching
    // frequency and phase; and a grid run too close to its limits sags, then collapses.
    // Maintenance and fuel arrive here too (bible §14.2.1–2), each introduced by the first
    // failure it causes.
    public static class Era4Data
    {
        public const int    EraNumber = 4;
        public const double MinWatts  = 10e6;
        public const double MaxWatts  = 1e9;

        // What a player typically brings from Era 3: the Exhibition's takings and savings.
        public const double TypicalStartPounds     = 60_000;
        public const float  TypicalStartReputation = 50f;

        // A feeder pays this per watt-hour delivered (£0.025 a unit). Sized with the bible's
        // anchors (£15,000 for the first 20 MW coal unit) so a unit pays for itself in about
        // a day and a half of game time once it has load to carry.
        public const double TariffPenniesPerWattHour = 0.006;
        public const double ContractBaseRate         = 0.009;
        public const int    BaseContractSlots        = 2;

        public const float  OfflineEraFactor  = 0.9f;
        public const float  OfflineTauHours   = 12f; // bible §7.2: Eras 4–6
        public const string OfflineFlavorText = "The night shift kept the units on the bars while you were away.";

        // The bible's Era 4 mockup: Central Electric Station, 14 June 1928.
        public const float StartHour  = 18f;
        public const int   StartYear  = 1928;
        public const int   StartMonth = 6;
        public const int   StartDay   = 14;

        public const float ShortfallLeeway               = 0.1f;
        public const float ShortfallCountdownMinutes     = 30f;
        public const float ShortfallForgivenAfterMinutes = 60f;

        // -------------------------------------------------------------------------------
        // Frequency (bible §4.4, without the automatic relays: those come in Era 5).
        // Under Amber the desk shows caution; under Warn for long the cascade countdown runs
        // (bible §10.2, T-90 s) with its one-tap remedies; it clears back above Clear.
        // Under Collapse, or when the countdown runs out, the grid goes dark: black start.
        // -------------------------------------------------------------------------------

        public const double AmberHz    = 49.5;
        public const double WarnHz     = 49.0;
        public const double ClearHz    = 49.2;
        public const double CollapseHz = 48.0;
        public const double CascadeWarningSeconds = 90;

        // -------------------------------------------------------------------------------
        // Maintenance (bible §9.4, §14.2.1). A unit runs up game hours while online; past its
        // interval it may fail each hour with a chance that grows with how overdue it is.
        // A service takes the unit off for ServiceHours and costs a share of its price.
        // Failure means a repair: longer, dearer.
        // -------------------------------------------------------------------------------

        public const double ServiceHours        = 8;
        public const double ServiceCostShare    = 0.03;
        public const double RepairHours         = 24;
        public const double RepairCostShare     = 0.12;
        public const double FailureBasePerHour  = 0.01;  // just overdue
        public const double FailureSlopePerHour = 0.06;  // more for each interval overdue
        // A slammed breaker (out-of-phase close) bends the shaft: a short repair.
        public const double SlamRepairHours     = 6;

        // -------------------------------------------------------------------------------
        // Coal (bible §14.2.2). Coal units burn the stockpile; when it runs dry they trip.
        // Before the fuel manager the player orders by hand; after it, a target of days of
        // cover is kept topped up (carrying stock costs a little each day).
        // -------------------------------------------------------------------------------

        public const double CoalPoundsPerTonne     = 1.0;
        public const double StartCoalTonnes        = 1_500;
        public const double ManualOrderDays        = 3;   // a hand order buys this much cover
        public const float  DefaultTargetDays      = 5;
        public const float  MaxTargetDays          = 14;
        public const float  RecommendedCoverDays   = 7;   // FRP is 100 at this
        public const double CarryingSharePerDay    = 0.002;

        public const string MaintenanceScore = "MP";
        public const string FuelScore        = "FRP";

        // -------------------------------------------------------------------------------
        // Units — bible §9.7 anchors: £15k coal unit, £200k multi-unit, £2M regional.
        // Cost × 1.15ⁿ per kind; each kind dearer per kilowatt than the last.
        // -------------------------------------------------------------------------------

        public static readonly UnitSpec[] Units =
        {
            //  type                              name                     £          watts   governor s  H s  coal t/MWh  service h  black start  max  unlocked by
            new(GeneratorType.CoalUnit,          "Coal unit",             15_000,    20e6,   6,          5,   0.6,        240,       false,       0,   null,
                "A boiler and a turbo-alternator: the station's workhorse."),
            new(GeneratorType.HydroSet,          "Hydro set",             120_000,   80e6,   2,          3,   0,          480,       true,        4,   HydroId,
                "Water from the hills. Quick on the governor, and it can start a dead grid."),
            new(GeneratorType.TurboAlternator,   "Large turbo-alternator", 200_000,  100e6,  7,          6,   0.5,        300,       false,       0,   TurboId,
                "A hundred megawatts on one shaft."),
            new(GeneratorType.RegionalStation,   "Regional station",      2_000_000, 300e6,  8,          6,   0.45,       360,       false,       0,   GridId,
                "A whole station on the new 132 kV grid."),
        };

        // -------------------------------------------------------------------------------
        // Feeders: the city's districts and works, each a load on the daily curve (bible §3.4).
        // Connecting one adds its load at once (a jolt on the frequency) and its tariff.
        // -------------------------------------------------------------------------------

        public const double FeederLossPerMegawatt = 0.0005; // a steady few per cent: the lines are the town's
        public const double ConnectPoundsPerMegawatt = 300;

        public static readonly FeederSpec[] Feeders =
        {
            //  id                 name                         base MW  starts on  industrial
            new("f4_oldtown",      "Westgate old town",          6,      true,      false),
            new("f4_eastfield",    "Eastfield housing",          8,      false,     false),
            new("f4_tramways",     "Corporation tramways",       12,     false,     true),
            new("f4_cinemas",      "Cinemas and shops",          18,     false,     false),
            new("f4_railway",      "Railway electrification",    25,     false,     true),
            new("f4_suburbs",      "The new suburbs",            40,     false,     false),
            new("f4_mills",        "Cotton mills",               55,     false,     true),
            new("f4_docks",        "Docks and shipyard",         70,     false,     true),
            new("f4_hospitals",    "Hospitals and waterworks",   35,     false,     false),
            new("f4_steel",        "Steelworks",                 110,    false,     true),
            new("f4_county",       "The county towns",           140,    false,     false),
            new("f4_chemical",     "Chemical works",             160,    false,     true),
            new("f4_aircraft",     "Aircraft factory",           120,    false,     true),
            new("f4_neighbour",    "The neighbouring city",      300,    false,     false),
        };

        // -------------------------------------------------------------------------------
        // Research (bible §13). Era 3's is kept.
        // -------------------------------------------------------------------------------

        public const int UnitsBranch = 0, OperationsBranch = 1, CustomersBranch = 2;
        public static readonly string[] BranchNames         = { "Units", "Operations", "Customers" };
        public static readonly string[] ResearchBranchNames = { "Generation", "Controls", "Grid" };

        public const string HydroId      = "res4_hydro";
        public const string TurboId      = "res4_turbo";
        public const string PulverisedId = "res4_pulverised";
        public const string AutoSyncId   = "res4_auto_sync";
        public const string GovernorsId  = "res4_governors";
        public const string GridId       = "res4_grid";

        public const string AutoSyncFeatureId = "feature_auto_sync";

        public static readonly ResearchSpec[] Research =
        {
            //  id            name                          RP      effect                          unlocks                                branch  row  prereqs
            new(HydroId,      "Hydro-electric schemes",     800,    UpgradeEffect.UnlockGenerator,  nameof(GeneratorType.HydroSet),        0,      0,   new string[0],
                "Dams, pipelines and water turbines in the hills."),
            new(TurboId,      "Large turbo-alternators",    2_500,  UpgradeEffect.UnlockGenerator,  nameof(GeneratorType.TurboAlternator), 0,      1,   new[] { HydroId },
                "Bigger shafts, faster blades, hydrogen cooling."),
            new(PulverisedId, "Pulverised fuel",            2_000,  UpgradeEffect.UnlockFeature,    PulverisedId,                          0,      2,   new[] { TurboId },
                "Coal ground to dust and blown into the furnace: more steam per ton."),
            new(GovernorsId,  "Governor tuning",            1_200,  UpgradeEffect.UnlockFeature,    GovernorsId,                           1,      0,   new string[0],
                "Faster, steadier governors: the units answer a change of load sooner."),
            new(AutoSyncId,   "The auto-synchroniser",      3_000,  UpgradeEffect.UnlockFeature,    AutoSyncFeatureId,                     1,      1,   new[] { GovernorsId },
                "A relay that closes the breaker itself, when slip and phase are right."),
            new(GridId,       "The 132 kV grid",            8_000,  UpgradeEffect.UnlockGenerator,  nameof(GeneratorType.RegionalStation), 2,      0,   new string[0],
                "Pylons across the country: whole stations, shared between cities."),
        };

        // -------------------------------------------------------------------------------
        // Purchases (pounds).
        // -------------------------------------------------------------------------------

        public const string FeedHeatingId  = "era4_feed_heating";
        public const string PenstocksId    = "era4_penstocks";
        public const string ReheatId       = "era4_reheat";
        public const string MillsFitId     = "era4_coal_mills";
        public const string FittersId      = "era4_second_shift";
        public const string SidingsId      = "era4_coal_sidings";
        public const string TuneId         = "era4_fit_governors";
        public const string OfficeId       = "era4_engineering_office";
        public const string TelephonesId   = "era4_control_telephones";
        public const string ShowroomsId    = "era4_showrooms";

        // Bible §13.1.2: the engineering office, £20K, 50 RP an hour.
        public const double OfficeResearchPerHour = 50;

        public static readonly UpgradeSpec[] Upgrades =
        {
            //  id             name                          £           growth max rep  effect                                    value  unlocks                                 branch            row  prereqs
            new(FeedHeatingId, "Feed-water heating",         40_000,     1,     1,  0,   UpgradeEffect.MultiplyGeneratorOutput,    1.25,  nameof(GeneratorType.CoalUnit),        UnitsBranch,      0,   new string[0],
                "Bled steam warms the boiler water: more output from the same fire."),
            new(PenstocksId,   "Steel penstocks",            300_000,    1,     1,  0,   UpgradeEffect.MultiplyGeneratorOutput,    1.25,  nameof(GeneratorType.HydroSet),        UnitsBranch,      1,   new[] { FeedHeatingId, HydroId },
                "Wider pipes from the dam: more head at the turbines."),
            new(ReheatId,      "Reheat turbines",            800_000,    1,     1,  0,   UpgradeEffect.MultiplyGeneratorOutput,    1.25,  nameof(GeneratorType.TurboAlternator), UnitsBranch,      2,   new[] { PenstocksId, TurboId },
                "Steam back through the boiler between stages."),
            new(MillsFitId,    "Coal mills",                 150_000,    1,     1,  0,   UpgradeEffect.MultiplyFuelBurn,           0.8,   null,                                  UnitsBranch,      3,   new[] { ReheatId, PulverisedId },
                "Pulverised fuel in every boiler: a fifth less coal."),
            new(FittersId,     "A second shift of fitters",  25_000,     1,     1,  0,   UpgradeEffect.MultiplyServiceTime,        0.5,   null,                                  OperationsBranch, 0,   new string[0],
                "Services done in half the time."),
            new(SidingsId,     "Railway coal sidings",       60_000,     1,     1,  0,   UpgradeEffect.MultiplyFuelPrice,          0.8,   null,                                  OperationsBranch, 1,   new[] { FittersId },
                "Coal by the trainload, straight to the bunkers."),
            new(TuneId,        "Fit the tuned governors",    120_000,    1,     1,  0,   UpgradeEffect.MultiplyGovernorTime,       0.5,   null,                                  OperationsBranch, 2,   new[] { SidingsId, GovernorsId },
                "Every unit answers a change of load twice as fast."),
            new(OfficeId,      "Engineering office",         20_000,     1.15,  10, 0,   UpgradeEffect.AddResearchRate,            OfficeResearchPerHour, null,                  CustomersBranch,  0,   new string[0],
                "Draughtsmen and a test bay. Makes research."),
            new(TelephonesId,  "Control-room telephones",    80_000,     8,     2,  56,  UpgradeEffect.AddContractSlot,            1,     null,                                  CustomersBranch,  1,   new[] { OfficeId },
                "The big works ring the desk directly. One more contract at a time."),
            new(ShowroomsId,   "Electricity showrooms",      400_000,    1,     1,  62,  UpgradeEffect.MultiplyTariff,             1.2,   null,                                  CustomersBranch,  2,   new[] { TelephonesId },
                "Cookers, irons and wireless sets in every window."),
        };

        // -------------------------------------------------------------------------------
        // Contracts. Reputation carries on from Era 3 (≈50).
        // -------------------------------------------------------------------------------

        public static readonly ContractSpec[] Contracts =
        {
            //  id                   client                          watts    hours from to   rep   +rep pay   tier
            new("c4_railway",       "Southern Electric Railway",     30e6,    6,    6,   10,  0,    1,   1.0f, ContractTier.Standard,
                "The morning commuter trains, every one electric."),
            new("c4_cinemas",       "The Odeon circuit",             20e6,    5,    18,  23,  50,   1,   1.0f, ContractTier.Standard,
                "Every picture house in the county, all evening."),
            new("c4_arc",           "Steel arc furnaces",            80e6,    8,    0,   24,  54,   2,   1.0f, ContractTier.Standard,
                "Furnaces that pull a mighty load the moment they strike."),
            new("c4_bbc",           "BBC Daventry transmitter",      15e6,    24,   0,   24,  58,   3,   1.5f, ContractTier.Reputation,
                "The long-wave transmitter must never go off the air."),
            new("c4_aluminium",     "Aluminium rolling mill",        150e6,   12,   0,   24,  62,   3,   1.0f, ContractTier.Standard,
                "A rolling mill the size of a cathedral."),
            new("c4_royal_show",    "The Royal Show",                200e6,   6,    10,  16,  68,   4,   1.5f, ContractTier.Reputation,
                "The King opens the show. Every exhibit lit and turning."),
        };

        // -------------------------------------------------------------------------------

        public readonly struct UnitSpec
        {
            public readonly GeneratorType Type;
            public readonly string Name;
            public readonly double CostPounds, Watts;
            public readonly float GovernorSeconds, InertiaSeconds;
            public readonly double CoalTonnesPerMWh;
            public readonly float ServiceIntervalHours;
            public readonly bool BlackStart;
            public readonly int MaxUnits;
            public readonly string UnlockedBy, Description;
            public UnitSpec(GeneratorType type, string name, double cost, double watts, float governor, float inertia,
                            double coal, float service, bool blackStart, int max, string unlockedBy, string description)
            {
                Type = type; Name = name; CostPounds = cost; Watts = watts; GovernorSeconds = governor;
                InertiaSeconds = inertia; CoalTonnesPerMWh = coal; ServiceIntervalHours = service;
                BlackStart = blackStart; MaxUnits = max; UnlockedBy = unlockedBy; Description = description;
            }
        }

        public readonly struct FeederSpec
        {
            public readonly string Id, Name;
            public readonly double BaseMegawatts;
            public readonly bool StartsOn, Industrial;
            public FeederSpec(string id, string name, double mw, bool startsOn, bool industrial)
            {
                Id = id; Name = name; BaseMegawatts = mw; StartsOn = startsOn; Industrial = industrial;
            }
            public double ConnectPounds => StartsOn ? 0 : ConnectPoundsPerMegawatt * BaseMegawatts;
        }

        // Research, upgrades and contracts share Era 3's shapes.
        public readonly struct ResearchSpec
        {
            public readonly string Id, Name;
            public readonly double CostRP;
            public readonly UpgradeEffect Effect;
            public readonly string UnlocksId;
            public readonly int Branch, Row;
            public readonly string[] Prerequisites;
            public readonly string Description;
            public ResearchSpec(string id, string name, double cost, UpgradeEffect effect, string unlocksId,
                                int branch, int row, string[] prereqs, string description)
            {
                Id = id; Name = name; CostRP = cost; Effect = effect; UnlocksId = unlocksId;
                Branch = branch; Row = row; Prerequisites = prereqs; Description = description;
            }
        }

        public readonly struct UpgradeSpec
        {
            public readonly string Id, Name;
            public readonly double CostPounds, CostGrowth;
            public readonly int MaxLevel;
            public readonly float RequiredReputation;
            public readonly UpgradeEffect Effect;
            public readonly double Value;
            public readonly string UnlocksId;
            public readonly int Branch, Row;
            public readonly string[] Prerequisites;
            public readonly string Description;
            public UpgradeSpec(string id, string name, double cost, double growth, int maxLevel, float rep,
                               UpgradeEffect effect, double value, string unlocksId, int branch, int row,
                               string[] prereqs, string description)
            {
                Id = id; Name = name; CostPounds = cost; CostGrowth = growth; MaxLevel = maxLevel;
                RequiredReputation = rep; Effect = effect; Value = value; UnlocksId = unlocksId;
                Branch = branch; Row = row; Prerequisites = prereqs; Description = description;
            }
        }

        public readonly struct ContractSpec
        {
            public readonly string Id, Client;
            public readonly double Watts;
            public readonly float Hours, FromHour, ToHour, RequiredReputation, ReputationOnSuccess, PayMultiplier;
            public readonly ContractTier Tier;
            public readonly string Description;
            public ContractSpec(string id, string client, double watts, float hours, float from, float to,
                                float rep, float repOnSuccess, float payMult, ContractTier tier, string description)
            {
                Id = id; Client = client; Watts = watts; Hours = hours; FromHour = from; ToHour = to;
                RequiredReputation = rep; ReputationOnSuccess = repOnSuccess; PayMultiplier = payMult;
                Tier = tier; Description = description;
            }
        }
    }
}
