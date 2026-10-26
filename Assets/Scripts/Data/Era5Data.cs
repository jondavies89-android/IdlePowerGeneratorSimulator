using IdlePowerGen.Models;

namespace IdlePowerGen.Data
{
    // Numeric constants for Era 5 — "The Interconnected Grid" (1 GW → 50 GW, Midlands 1952).
    // The source of truth for Era5AssetBuilder.
    //
    // The era's one new concept (bible §2.2): power isn't just power. Each city's motors draw
    // reactive power as well as real; the lines carry both, so a city with a poor power factor
    // costs more current, loses more on the way, eats into the units' rating, and sees its
    // voltage sag. Capacitor banks at the city are the affordable fix (bible Appendix A).
    // The grid now sheds load by itself as frequency falls (bible §4.4): brownouts, not
    // blackouts, and the redundancy score arrives with the first one (bible §14.2.3).
    public static class Era5Data
    {
        public const int    EraNumber = 5;
        public const double MinWatts  = 1e9;
        public const double MaxWatts  = 50e9;

        // What a player typically brings from Era 4.
        public const double TypicalStartPounds     = 4_000_000;
        public const float  TypicalStartReputation = 60f;

        // A city pays this per watt-hour (£0.25 a unit). The bible's anchors put a 500 MW
        // coal unit at £8M, so a unit pays for itself in about two and a half game days:
        // the era is meant to run three to ten real days (bible §2.1).
        public const double TariffPenniesPerWattHour = 0.06;
        public const double ContractBaseRate         = 0.09;
        public const int    BaseContractSlots        = 2;

        public const float  OfflineEraFactor  = 0.85f;
        public const float  OfflineTauHours   = 12f;
        public const string OfflineFlavorText = "Central Dispatch held the region together while you were away.";

        // The bible's Era 5 mockup: Midlands region, 12 November 1952.
        public const float StartHour  = 17f;
        public const int   StartYear  = 1952;
        public const int   StartMonth = 11;
        public const int   StartDay   = 12;

        public const float ShortfallLeeway               = 0.1f;
        public const float ShortfallCountdownMinutes     = 30f;
        public const float ShortfallForgivenAfterMinutes = 60f;

        public const string RedundancyScore = "RP";

        // -------------------------------------------------------------------------------
        // Reactive power and voltage. A city at the end of its line sees its voltage drop by
        // (P·R + Q·X), in per unit of 132 kV; lines are built to the size of the city, so the
        // drop depends on how loaded it is, its distance and its power factor. A capacitor
        // bank supplies a share of the city's reactive power on the spot.
        // -------------------------------------------------------------------------------

        public const double NominalKilovolts = 132;
        public const double ResistancePerLength = 0.01; // per unit, at the city's own rating
        public const double ReactancePerLength  = 0.04;
        public const double BankShare = 0.15;           // of a city's base load, as reactive power
        public const int    MaxBanks  = 6;
        public const double BankPoundsPerGigawatt = 85_000 / 1.5; // Leicester's bank: £85k (mockup)

        public const double GoodVoltage     = 0.95;  // under this, amber; the city pays less
        public const double PoorVoltage     = 0.90;  // under this, red: lamps dim, motors overheat
        public const double CollapseVoltage = 0.86;  // under this the city's load trips off
        public const double ReconnectAfterHours = 0.5;
        public const double TargetPowerFactor = 0.95;

        // -------------------------------------------------------------------------------
        // Every city lit draws about 47 GW on average, nearer 60 GW at the evening peak: the
        // era's 50 GW goal needs most of the region at its busiest.
        //
        // The two cities lit at the start draw 0.7 GW on average, 1 GW at the evening peak: within
        // the inherited gigawatt once it has made their reactive power too.
        //
        // The region (the bible's mimic board: Central, Birmingham, Coventry, Leicester,
        // Northampton and on). Each city hangs off a parent nearer Central; a city can only
        // be connected once its parent is. Position is on the board's four-by-four grid, 0–1 across and down.
        // -------------------------------------------------------------------------------

        public static readonly CitySpec[] Cities =
        {
            //  id               name              parent           GW    PF     length  x     y      starts
            new("c5_birmingham", "Birmingham",     null,            0.45, 0.93,  1,      0.0,  0.33,  true),
            new("c5_coventry",   "Coventry",       null,            0.25, 0.92,  1,      0.0,  0.67,  true),
            new("c5_leicester",  "Leicester",      "c5_birmingham", 1.5,  0.80,  2,      0.33, 0.33,  false),
            new("c5_northampton","Northampton",    "c5_coventry",   1.2,  0.92,  2,      0.33, 0.67,  false),
            new("c5_wolverhampton","Wolverhampton","c5_birmingham", 2.5,  0.89,  2,      0.0,  0.0,  false),
            new("c5_oxford",     "Oxford",         "c5_coventry",   2.0,  0.94,  2,      0.0,  1.0,  false),
            new("c5_stoke",      "Stoke",          "c5_wolverhampton", 3.0, 0.88, 3,     0.33, 0.0,  false),
            new("c5_nottingham", "Nottingham",     "c5_leicester",  5.0,  0.90,  3,      0.67, 0.33,  false),
            new("c5_cambridge",  "Cambridge",      "c5_northampton",2.0,  0.95,  3,      0.67, 0.67,  false),
            new("c5_derby",      "Derby",          "c5_nottingham", 3.5,  0.89,  3,      1.0,  0.33,  false),
            new("c5_sheffield",  "Sheffield",      "c5_derby",      10.0, 0.86,  3,      1.0,  0.67,  false),
            new("c5_manchester", "Manchester",     "c5_stoke",      16.0, 0.85,  3,      0.67, 0.0,  false),
        };

        public const double ConnectPoundsPerGigawatt = 1_500_000;

        // -------------------------------------------------------------------------------
        // Units — bible §9.7 anchors: £8M large coal, £80M hydro, £400M first nuclear. The
        // first coal unit is half the anchor at half the size, so the Era 4 takings buy one.
        // -------------------------------------------------------------------------------

        public static readonly UnitSpec[] Units =
        {
            //  type                             name                     £            watts   governor H   coal t/MWh service h black max unlocked by
            new(GeneratorType.LargeCoalUnit,    "Large coal unit",       4_000_000,   250e6,  7,       6,  0.4,       400,      false, 0,  null,
                "Two hundred and fifty megawatts of pulverised-fuel boiler and turbine."),
            new(GeneratorType.OilPeaker,        "Oil-fired peaker",      3_000_000,   200e6,  2,       2,  0,         300,      true,  0,  PeakersId,
                "Quick to start and quick on the governor. Can start a dead grid."),
            new(GeneratorType.HydroScheme,      "Hydro scheme",          80_000_000,  2e9,    2,       3,  0,         600,      true,  3,  HydroBoardId,
                "A whole Highland valley's water, wired south."),
            new(GeneratorType.MagnoxReactor,    "Magnox reactor",        400_000_000, 5e9,    15,      7,  0,         800,      false, 0,  AtomicId,
                "Atomic power. Steady as a rock, slow to answer."),
        };

        // -------------------------------------------------------------------------------
        // Research.
        // -------------------------------------------------------------------------------

        public const int UnitsBranch = 0, ReactiveBranch = 1, OperationsBranch = 2;
        public static readonly string[] BranchNames         = { "Units", "Reactive power", "Operations" };
        public static readonly string[] ResearchBranchNames = { "Generation", "Transmission", "Safety" };

        public const string PeakersId     = "res5_peakers";
        public const string HydroBoardId  = "res5_hydro_board";
        public const string AtomicId      = "res5_atomic";
        public const string CondensersId  = "res5_condensers";
        public const string TapChangersId = "res5_tap_changers";
        public const string SupergridId   = "res5_supergrid";
        public const string BlackStartId  = "res5_black_start";

        public static readonly ResearchSpec[] Research =
        {
            new(PeakersId,     "Oil-fired peakers",        20_000,  UpgradeEffect.UnlockGenerator, nameof(GeneratorType.OilPeaker),     0, 0, new string[0],
                "Small fast stations for the evening peak."),
            new(HydroBoardId,  "The Hydro-Electric Board", 60_000,  UpgradeEffect.UnlockGenerator, nameof(GeneratorType.HydroScheme),   0, 1, new[] { PeakersId },
                "Dams across the Highlands and a line all the way south."),
            new(AtomicId,      "Atomic power",             250_000, UpgradeEffect.UnlockGenerator, nameof(GeneratorType.MagnoxReactor), 0, 2, new[] { HydroBoardId },
                "Calder Hall's reactors, built for the grid."),
            new(CondensersId,  "Synchronous condensers",   30_000,  UpgradeEffect.UnlockFeature,   CondensersId,                        1, 0, new string[0],
                "Spinning machines that make reactive power and nothing else."),
            new(TapChangersId, "On-load tap changers",     45_000,  UpgradeEffect.UnlockFeature,   TapChangersId,                       1, 1, new[] { CondensersId },
                "Transformers that raise the voltage without switching off."),
            new(SupergridId,   "The 275 kV supergrid",     150_000, UpgradeEffect.UnlockFeature,   SupergridId,                         1, 2, new[] { TapChangersId },
                "Twice the voltage on the trunk lines: a quarter of the loss."),
            new(BlackStartId,  "Black-start procedures",   25_000,  UpgradeEffect.UnlockFeature,   BlackStartId,                        2, 0, new string[0],
                "Diesels and drills: any big unit can be fitted to start a dead grid."),
        };

        // -------------------------------------------------------------------------------
        // Purchases (pounds).
        // -------------------------------------------------------------------------------

        public const string ReheatId     = "era5_reheat";
        public const string IntakesId    = "era5_intakes";
        public const string FitCondId    = "era5_fit_condensers";
        public const string FitTapsId    = "era5_fit_tap_changers";
        public const string BuildSuperId = "era5_build_supergrid";
        public const string LabId        = "era5_rd_laboratory";
        public const string ManagerId    = "era5_contract_manager";
        public const string ControlId    = "era5_grid_control";
        public const string CouncilId    = "era5_electricity_council";

        // Bible §13.1.2: the R&D laboratory, £500K, 500 RP an hour.
        public const double LabResearchPerHour = 500;

        // A fitted black-start set costs this share of the unit's price.
        public const double BlackStartFitShare = 0.05;

        public static readonly UpgradeSpec[] Upgrades =
        {
            new(ReheatId,     "Reheat cycles",            40_000_000,  1,    1,  0,  UpgradeEffect.MultiplyGeneratorOutput, 1.25, nameof(GeneratorType.LargeCoalUnit), UnitsBranch, 0, new string[0],
                "Steam back through the boiler between turbine stages."),
            new(IntakesId,    "Deeper intakes",           200_000_000, 1,    1,  0,  UpgradeEffect.MultiplyGeneratorOutput, 1.25, nameof(GeneratorType.HydroScheme),   UnitsBranch, 1, new[] { ReheatId, HydroBoardId },
                "More head on every hydro turbine."),
            new(FitCondId,    "Install synchronous condensers", 30_000_000, 1, 1, 0, UpgradeEffect.AddCapacitorStrength,    0.6,  null,                                 ReactiveBranch, 0, new[] { CondensersId },
                "At every substation: each capacitor bank goes further."),
            new(FitTapsId,    "Fit tap changers",         60_000_000,  1,    1,  0,  UpgradeEffect.AddVoltageSupport,       0.03, null,                                 ReactiveBranch, 1, new[] { FitCondId, TapChangersId },
                "Every city's voltage held three per cent higher."),
            new(BuildSuperId, "Build the supergrid",      250_000_000, 1,    1,  70, UpgradeEffect.MultiplyLineLosses,      0.35, null,                                 ReactiveBranch, 2, new[] { FitTapsId, SupergridId },
                "275 kV trunk lines across the region."),
            new(LabId,        "R&D laboratory",           500_000,     1.15, 10, 0,  UpgradeEffect.AddResearchRate,         LabResearchPerHour, null,                   OperationsBranch, 0, new string[0],
                "Scientists, a test hall and a reactor model. Makes research."),
            new(ManagerId,    "Contract manager",         2_000_000,   3,    4,  0,  UpgradeEffect.AddContractManager,      0,    null,                                 OperationsBranch, 1, new[] { LabId },
                "Books a client for one contract slot, for a cut of the fee. One per slot."),
            new(ControlId,    "Grid control centre",      20_000_000,  8,    2,  66, UpgradeEffect.AddContractSlot,         1,    null,                                 OperationsBranch, 2, new[] { ManagerId },
                "A national control room. One more contract at a time."),
            new(CouncilId,    "The Electricity Council",  120_000_000, 1,    1,  72, UpgradeEffect.MultiplyTariff,          1.2,  null,                                 OperationsBranch, 3, new[] { ControlId },
                "Showrooms, all-electric homes, and a tariff to suit."),
        };

        // -------------------------------------------------------------------------------
        // Contracts. Reputation carries on (≈60).
        // -------------------------------------------------------------------------------

        public static readonly ContractSpec[] Contracts =
        {
            new("c5_tv",         "BBC television transmitters",  1e9,   5,  0,  1, 1.0f, ContractTier.Standard,
                "Sutton Coldfield's mast, and every set in the Midlands."),
            new("c5_motor",      "The motor works",              4e9,   8,  60, 2, 1.0f, ContractTier.Standard,
                "Longbridge and Cowley, every line running."),
            new("c5_smelter",    "Aluminium smelter",            2e9,   24, 64, 3, 1.0f, ContractTier.Standard,
                "Pot lines that must never cool."),
            new("c5_steel",      "Steel strip mill",             6e9,   12, 68, 3, 1.0f, ContractTier.Standard,
                "Hot strip at a mile a minute."),
            new("c5_coronation", "Coronation Day",               10e9,  6,  74, 5, 1.6f, ContractTier.Reputation,
                "Every television in the country switched on at once. Don't let the picture go."),
        };

        // -------------------------------------------------------------------------------

        public readonly struct CitySpec
        {
            public readonly string Id, Name, Parent;
            public readonly double BaseGigawatts, PowerFactor, Length;
            public readonly double X, Y;
            public readonly bool StartsOn;
            public CitySpec(string id, string name, string parent, double gw, double pf, double length,
                            double x, double y, bool startsOn)
            {
                Id = id; Name = name; Parent = parent; BaseGigawatts = gw; PowerFactor = pf; Length = length;
                X = x; Y = y; StartsOn = startsOn;
            }
            public double ConnectPounds => StartsOn ? 0 : ConnectPoundsPerGigawatt * BaseGigawatts;
            // Reactive watts per real watt (tan φ).
            public double ReactivePerWatt => System.Math.Tan(System.Math.Acos(PowerFactor));
            public double BankPounds => BankPoundsPerGigawatt * BaseGigawatts;
        }

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
            public readonly float Hours, RequiredReputation, ReputationOnSuccess, PayMultiplier;
            public readonly ContractTier Tier;
            public readonly string Description;
            public ContractSpec(string id, string client, double watts, float hours,
                                float rep, float repOnSuccess, float payMult, ContractTier tier, string description)
            {
                Id = id; Client = client; Watts = watts; Hours = hours;
                RequiredReputation = rep; ReputationOnSuccess = repOnSuccess; PayMultiplier = payMult;
                Tier = tier; Description = description;
            }
        }
    }
}
