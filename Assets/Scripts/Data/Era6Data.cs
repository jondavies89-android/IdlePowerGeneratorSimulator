using IdlePowerGen.Models;

namespace IdlePowerGen.Data
{
    // Numeric constants for Era 6 — "The Nuclear Age" (50 GW → 1 TW, the National Grid, 1978).
    // The source of truth for Era6AssetBuilder.
    //
    // The era's one new concept (bible §2.2): big machines are slow. A reactor runs flat out
    // on BASE and can't follow the load; coal follows it slowly and can't run below its
    // minimum; gas turbines and pumped storage are quick but dear to run. Every unit takes
    // time to start from standby, and a tripped reactor takes a day. The dispatch loads the
    // units on AGC cheapest first (the merit order), so the evening rise needs fast units
    // ready ahead of it, and the night trough needs baseload that fits under it.
    // Operator training and black-start drills arrive here (bible §14.2.5), as research and
    // cash rather than the prestige loop's XP, which is deferred.
    public static class Era6Data
    {
        public const int    EraNumber = 6;
        public const double MinWatts  = 50e9;
        public const double MaxWatts  = 1e12;

        // What a player typically brings from Era 5.
        public const double TypicalStartPounds     = 300_000_000;
        public const float  TypicalStartReputation = 75f;

        // A region pays this per watt-hour delivered (£62.50 a megawatt-hour). Fuel takes a real
        // share of it now: coal about £45, gas £38, the old fleet nothing, a reactor £6. So the
        // reactors pay best, if the load can take them, and the peakers barely pay at all.
        // At £100 a megawatt-hour a greedy pacing bot finished the era in 33 real hours; the
        // bible asks one to three weeks of play (bible §2.1).
        public const double TariffPenniesPerWattHour = 0.015;
        public const double ContractBaseRate         = 0.025;
        public const int    BaseContractSlots        = 2;

        public const float  OfflineEraFactor  = 0.8f;
        public const float  OfflineTauHours   = 12f; // bible §7.2: Eras 4–6
        // Bible §7.7's Era 6 line.
        public const string OfflineFlavorText = "AGC handled load following while you were away.";

        // The bible's Era 6 mockup: National Grid, 17:42, Tuesday 14 November 1978.
        public const float StartHour  = 17.7f;
        public const int   StartYear  = 1978;
        public const int   StartMonth = 11;
        public const int   StartDay   = 14;

        public const float ShortfallLeeway               = 0.1f;
        public const float ShortfallCountdownMinutes     = 30f;
        public const float ShortfallForgivenAfterMinutes = 60f;

        // -------------------------------------------------------------------------------
        // Too much on the bars (bible §4.4's over-frequency). Over HighHz the desk shows
        // caution; held over TripHz for TripSeconds, overspeed protection trips the biggest
        // unit on BASE. A reactor tripped like that (or scrammed in a blackout) can't be
        // restarted for ReactorHoldHours: the xenon has to clear.
        // -------------------------------------------------------------------------------

        public const double HighHz       = 50.5;
        public const double TripHz       = 51.0;
        public const double TripSeconds  = 10;
        public const double ReactorHoldHours = 24;
        public const double TripHoldHours    = 1;

        // A unit counts as fast reserve on standby if it can be on the bars within this.
        public const double FastStartHours = 0.1;

        // A reactor's refuelling outage takes this many times an ordinary service.
        public const double ReactorServiceFactor = 3;

        // -------------------------------------------------------------------------------
        // Coal, at last a real cost (bible §9.8: fuel is its own line).
        // -------------------------------------------------------------------------------

        public const double CoalPoundsPerTonne = 130;
        public const double StartCoalTonnes    = 400_000;

        // The inherited fleet's place in the merit order (its fuel is the old boards' affair:
        // it costs the player nothing, but the dispatch loads cheaper new units first).
        public const double OldFleetMeritPounds = 56;

        // -------------------------------------------------------------------------------
        // Operator training (bible §11.1 OTP, §14.2.5). Training gives a base; the simulator
        // adds to it; a drill adds up to DrillPoints, fading over DrillFreshDays (drill recency).
        // A drill is a planned black start: customers warned, no repute lost, tutorial-grade
        // (no step fails), and the contracts wait it out.
        // -------------------------------------------------------------------------------

        public const string TrainingScore   = "OTP";
        public const double TrainingBase    = 30;
        public const double DrillPoints     = 40;
        public const double DrillFreshDays  = 10;
        public const double DrillPounds     = 25_000_000;

        // What OTP buys (bible §11.2), only ever as a bonus: up to this much longer a cascade
        // warning, this much wider a clean close, this share fewer start-sequence failures.
        public const double CascadeBonusSeconds = 30;
        public const double SyncForgiveness     = 0.6;
        public const double StepFailureCut      = 0.5;

        // -------------------------------------------------------------------------------
        // The regions of Britain on the national mimic board. Every region lit draws about
        // 850 GW on average, near 1.2 TW at the evening peak: the era's terawatt needs most
        // of the country at its busiest. The two lit at the start draw 35 GW on average,
        // about 50 GW at the peak: what the inherited fleet can carry.
        // Each hangs off a parent nearer the Midlands; position is on the board, 0–1 across
        // and down, roughly where the region sits on the map.
        // -------------------------------------------------------------------------------

        public static readonly RegionSpec[] Regions =
        {
            //  id                   name                 code   parent             GW    length  x     y     starts  industrial
            new("r6_midlands",       "Midlands",          "MID", null,              20,   1,      0.58, 0.60, true,   false),
            new("r6_south_wales",    "South Wales",       "SWA", "r6_midlands",     15,   2,      0.34, 0.75, true,   true),
            new("r6_north_wales",    "North Wales",       "NWA", "r6_midlands",     25,   2,      0.36, 0.55, false,  false),
            new("r6_east_anglia",    "East Anglia",       "EAN", "r6_midlands",     30,   2,      0.86, 0.62, false,  false),
            new("r6_n_ireland",      "Northern Ireland",  "NIR", "r6_s_scotland",   35,   4,      0.10, 0.36, false,  false),
            new("r6_n_scotland",     "Northern Scotland", "NSC", "r6_s_scotland",   40,   4,      0.40, 0.06, false,  false),
            new("r6_yorkshire",      "Yorkshire",         "YOR", "r6_midlands",     50,   2,      0.66, 0.44, false,  true),
            new("r6_north_east",     "North East",        "NEA", "r6_yorkshire",    55,   3,      0.66, 0.30, false,  true),
            new("r6_north_west",     "North West",        "NWE", "r6_midlands",     60,   2,      0.46, 0.43, false,  true),
            new("r6_south_west",     "South West",        "SWE", "r6_south_wales",  60,   3,      0.30, 0.92, false,  false),
            new("r6_s_scotland",     "Southern Scotland", "SSC", "r6_north_east",   70,   3,      0.48, 0.21, false,  true),
            new("r6_the_south",      "The South",         "STH", "r6_midlands",     80,   2,      0.56, 0.87, false,  false),
            new("r6_south_east",     "South East",        "SEA", "r6_london",       110,  2,      0.90, 0.86, false,  false),
            new("r6_london",         "London",            "LDN", "r6_east_anglia",  200,  2,      0.76, 0.76, false,  false),
        };

        // A region's lines lose this share of its base load per length (275/400 kV: little).
        public const double LossPerLength = 0.012;
        public const double ConnectPoundsPerGigawatt = 2_000_000;

        // -------------------------------------------------------------------------------
        // Units — bible §9.7 anchors: £200M CCGT, £1.5B modern nuclear, £4B mega-plant.
        // Ramp is the share of rating a game minute; start is game hours from standby.
        // -------------------------------------------------------------------------------

        public static readonly UnitSpec[] Units =
        {
            //  type                              name                        £              watts   ramp   follows min   start  fuel £/MWh coal t/MWh service H  black max unlocked by
            new(GeneratorType.CoalStation6,      "Coal-fired station",       150_000_000,   10e9,   0.015f, true,  0.40f, 1.5f,  0,         0.35,      600,    6, false, 0, null,
                "Two thousand megawatts a set, Drax-style. Slow to turn up, and slower to start."),
            new(GeneratorType.GasTurbine,        "Gas turbine peaker",       45_000_000,    3e9,    0.25f,  true,  0.10f, 0.05f, 80,        0,         300,    2, true,  0, GasTurbinesId,
                "Jet engines on a slab. Running in three minutes, and dear to keep running. Can start a dead grid."),
            new(GeneratorType.PumpedStorage,     "Pumped-storage station",   300_000_000,   12e9,   0.6f,   true,  0f,    0.02f, 62,        0,         800,    3, true,  3, PumpedId,
                "A mountain full of water, pumped up at night and let down in seconds at the peak."),
            new(GeneratorType.CombinedCycle,     "Combined-cycle station",   200_000_000,   20e9,   0.05f,  true,  0.45f, 0.5f,  38,        0,         500,    5, false, 0, CombinedId,
                "Gas turbines with a steam cycle on their exhaust. Cheap to build and quick to follow."),
            new(GeneratorType.PressurisedWater,  "PWR nuclear station",      1_500_000_000, 60e9,   0.02f,  false, 1f,    8f,    6,         0,         1000,   7, false, 0, LicenceId,
                "A pressurised-water reactor. The cheapest power there is, as long as it runs flat out."),
            new(GeneratorType.MegaStation,       "Twin-reactor mega-station", 4_000_000_000, 200e9,  0.02f,  false, 1f,    12f,   5,         0,         1200,   8, false, 0, MegaId,
                "Two reactors, one site, two hundred gigawatts. The biggest thing on the grid, and the biggest loss if it trips."),
        };

        // The stations' names on the board (bible Appendix A, Era 6: "NUC-04 HARTLEPOOL").
        public static string Prefix(GeneratorType type) => type switch
        {
            GeneratorType.CoalStation6     => "COAL",
            GeneratorType.GasTurbine       => "OCGT",
            GeneratorType.PumpedStorage    => "HYDR",
            GeneratorType.CombinedCycle    => "CCGT",
            GeneratorType.PressurisedWater => "NUC",
            GeneratorType.MegaStation      => "NUC",
            _                              => "UNIT",
        };

        public static string[] Sites(GeneratorType type) => type switch
        {
            GeneratorType.CoalStation6     => new[] { "DRAX", "RATCLIFFE", "FERRYBRIDGE", "DIDCOT", "COTTAM", "WEST BURTON",
                                                      "EGGBOROUGH", "KINGSNORTH", "LONGANNET", "FIDDLERS FERRY", "IRONBRIDGE", "RUGELEY" },
            GeneratorType.GasTurbine       => new[] { "ABERTHAW", "LETCHWORTH", "TAYLORS LANE", "OCKER HILL", "COWES", "LITTLEBROOK",
                                                      "GRAIN", "WATFORD", "NORWICH", "BELVEDERE" },
            GeneratorType.PumpedStorage    => new[] { "DINORWIG", "CRUACHAN", "FFESTINIOG" },
            GeneratorType.CombinedCycle    => new[] { "HUNTERSTON", "KILLINGHOLME", "SALTEND", "CONNAHS QUAY", "SEABANK", "PETERHEAD",
                                                      "KEADBY", "STAYTHORPE", "PEMBROKE", "TEESSIDE", "DAMHEAD CREEK", "SPALDING" },
            GeneratorType.PressurisedWater => new[] { "HARTLEPOOL", "HEYSHAM", "TORNESS", "SIZEWELL", "DUNGENESS", "HINKLEY POINT",
                                                      "WYLFA", "BRADWELL", "OLDBURY", "TRAWSFYNYDD", "HUNTERSTON B", "CHAPELCROSS" },
            GeneratorType.MegaStation      => new[] { "SIZEWELL C", "HINKLEY C", "WYLFA NEWYDD", "MOORSIDE", "OLDBURY B", "BRADWELL B" },
            _                              => new[] { "STATION" },
        };

        // -------------------------------------------------------------------------------
        // Research. Earlier eras' research is kept; the reactor builds on Era 5's atomic power.
        // -------------------------------------------------------------------------------

        public const int UnitsBranch = 0, OperationsBranch = 1, CustomersBranch = 2;
        public static readonly string[] BranchNames         = { "Units", "Operations", "Customers" };
        public static readonly string[] ResearchBranchNames = { "Generation", "Controls", "Safety" };

        public const string GasTurbinesId = "res6_gas_turbines";
        public const string PumpedId      = "res6_pumped_storage";
        public const string CombinedId    = "res6_combined_cycle";
        public const string PwrId         = "res6_pwr";
        public const string MegaId        = "res6_mega_stations";
        public const string AgcId         = "res6_agc";
        public const string CommitmentId  = "res6_unit_commitment";
        public const string TrainingId    = "res6_operator_training";
        public const string OutagesId     = "res6_outage_planning";

        public static readonly ResearchSpec[] Research =
        {
            //  id              name                               RP         effect                          unlocks                                   branch row prereqs
            new(GasTurbinesId,  "Open-cycle gas turbines",         300_000,   UpgradeEffect.UnlockGenerator,  nameof(GeneratorType.GasTurbine),         0, 0, new string[0],
                "Aero engines turned to the grid: peakers that run up in minutes."),
            new(PumpedId,       "Pumped storage",                  600_000,   UpgradeEffect.UnlockGenerator,  nameof(GeneratorType.PumpedStorage),      0, 1, new[] { GasTurbinesId },
                "Reversible turbines in a hollowed-out mountain."),
            new(CombinedId,     "Combined-cycle gas turbines",     1_200_000, UpgradeEffect.UnlockGenerator,  nameof(GeneratorType.CombinedCycle),      0, 2, new[] { GasTurbinesId },
                "A steam cycle on the gas turbine's exhaust: half as much fuel again from the same gas."),
            new(PwrId,          "Pressurised-water reactors",      3_000_000, UpgradeEffect.UnlockFeature,    PwrId,                                    0, 3, new[] { CombinedId, Era5Data.AtomicId },
                "The next reactor after Magnox. Licensed to build once the grid's repute will carry it."),
            new(MegaId,         "Mega-stations",                   8_000_000, UpgradeEffect.UnlockGenerator,  nameof(GeneratorType.MegaStation),        0, 4, new[] { PwrId, LicenceId },
                "Two reactors on one site, built as one."),
            new(AgcId,          "Automatic Generation Control",    150_000,   UpgradeEffect.UnlockFeature,    AgcId,                                    1, 0, new string[0],
                "A computer that trims the units on AGC to the load, second by second."),
            new(CommitmentId,   "Unit commitment computer",        1_500_000, UpgradeEffect.UnlockFeature,    CommitmentId,                             1, 1, new[] { AgcId },
                "Starts units from standby in time for the peak and stands them down for the night."),
            new(TrainingId,     "Operator training",               250_000,   UpgradeEffect.UnlockFeature,    TrainingId,                               2, 0, new string[0],
                "Written procedures for every emergency, and drills to practise them."),
            new(OutagesId,      "Outage planning",                 1_000_000, UpgradeEffect.UnlockFeature,    OutagesId,                                2, 1, new[] { TrainingId },
                "Units serviced when the grid can spare them, before they run overdue."),
        };

        // -------------------------------------------------------------------------------
        // Purchases (pounds).
        // -------------------------------------------------------------------------------

        public const string BoilersId    = "era6_supercritical";
        public const string LicenceId    = "era6_nuclear_licence";
        public const string UpratesId    = "era6_gas_uprates";
        public const string ReactorUpId  = "era6_reactor_uprates";
        public const string InstituteId  = "era6_research_institute";
        public const string SimulatorId  = "era6_simulator_training";
        public const string FuelDealId   = "era6_fuel_contracts";
        public const string FittersId    = "era6_outage_crews";
        public const string ManagerId    = "era6_contract_manager";
        public const string ControlId    = "era6_national_control";
        public const string Economy7Id   = "era6_economy_seven";
        public const string TariffsId    = "era6_area_tariffs";

        // Bible §13.1.2: the national research institute, £20M, 5,000 RP an hour.
        public const double InstituteResearchPerHour = 5_000;

        // Bible §9.9: a reactor needs a licence, and the licence needs repute.
        public const float LicenceReputation = 85;

        public static readonly UpgradeSpec[] Upgrades =
        {
            //  id           name                            £              growth max rep  effect                                  value  unlocks                                  branch            row prereqs
            new(BoilersId,   "Supercritical boilers",        400_000_000,   1,     1,  0,   UpgradeEffect.MultiplyGeneratorOutput,  1.25,  nameof(GeneratorType.CoalStation6),     UnitsBranch,      0, new string[0],
                "Steam past its critical point: a quarter more from every coal set."),
            new(LicenceId,   "Nuclear site licence",         100_000_000,   1,     1,  LicenceReputation, UpgradeEffect.UnlockGenerator, 0, nameof(GeneratorType.PressurisedWater), UnitsBranch, 1, new[] { PwrId },
                "The inspectorate's say-so to build reactors. It goes to operators the country trusts."),
            new(UpratesId,   "Uprated gas turbines",         600_000_000,   1,     1,  0,   UpgradeEffect.MultiplyGeneratorOutput,  1.25,  nameof(GeneratorType.CombinedCycle),    UnitsBranch,      2, new[] { BoilersId, CombinedId },
                "Hotter blades, more air: a quarter more from every combined-cycle station."),
            new(ReactorUpId, "Reactor uprates",              3_000_000_000, 1,     1,  0,   UpgradeEffect.MultiplyGeneratorOutput,  1.15,  nameof(GeneratorType.PressurisedWater), UnitsBranch,      3, new[] { UpratesId, LicenceId },
                "Better fuel and a bigger turbine: more from every PWR."),
            new(InstituteId, "National research institute",  20_000_000,    1.15,  10, 0,   UpgradeEffect.AddResearchRate,          InstituteResearchPerHour, null,                 OperationsBranch, 0, new string[0],
                "A campus of laboratories and test reactors. Makes research."),
            new(SimulatorId, "Control-room simulator",       50_000_000,    3,     2,  0,   UpgradeEffect.AddTrainingScore,         15,    null,                                     OperationsBranch, 1, new[] { InstituteId, TrainingId },
                "A full-size control room that fails on purpose. Raises operator training."),
            new(FuelDealId,  "Long-term fuel contracts",     300_000_000,   1,     1,  0,   UpgradeEffect.MultiplyFuelPrice,        0.8,   null,                                     OperationsBranch, 2, new[] { SimulatorId },
                "Coal and gas bought by the decade: a fifth off the price."),
            new(FittersId,   "Outage crews",                 150_000_000,   1,     1,  0,   UpgradeEffect.MultiplyServiceTime,      0.5,   null,                                     OperationsBranch, 3, new[] { FuelDealId },
                "Specialist teams that move from station to station: services in half the time."),
            new(ManagerId,   "Contract manager",             40_000_000,    3,     4,  0,   UpgradeEffect.AddContractManager,       0,     null,                                     CustomersBranch,  0, new string[0],
                "Books a client for one contract slot, for a cut of the fee. One per slot."),
            new(ControlId,   "National Control Centre",      300_000_000,   8,     2,  80,  UpgradeEffect.AddContractSlot,          1,     null,                                     CustomersBranch,  1, new[] { ManagerId },
                "The whole country on one wall. One more contract at a time."),
            new(Economy7Id,  "Economy 7",                    500_000_000,   1,     1,  82,  UpgradeEffect.FlattenDemand,            0.3,   null,                                     CustomersBranch,  2, new[] { ControlId },
                "Seven hours of cheap electricity every night, for storage heaters and hot water. The trough fills; the peak eases."),
            new(TariffsId,   "The area boards' new tariffs", 1_500_000_000, 1,     1,  90,  UpgradeEffect.MultiplyTariff,           1.2,   null,                                     CustomersBranch,  3, new[] { Economy7Id },
                "Standing charges and a unit rate to match the times."),
        };

        // -------------------------------------------------------------------------------
        // Contracts. Reputation carries on (≈75).
        // -------------------------------------------------------------------------------

        public static readonly ContractSpec[] Contracts =
        {
            new("c6_railway",   "British Rail electrification",   8e9,   6,  0,  1, 1.0f, ContractTier.Standard,
                "The West Coast main line, wired from Euston to Glasgow."),
            new("c6_oil",       "North Sea oil terminals",        20e9,  8,  77, 2, 1.0f, ContractTier.Standard,
                "Sullom Voe and Flotta: the pumps never stop."),
            new("c6_smelters",  "Aluminium smelters",             15e9,  24, 80, 3, 1.0f, ContractTier.Standard,
                "Anglesey and Lynemouth. Their pot lines freeze solid if the power goes."),
            new("c6_steel",     "British Steel",                  40e9,  12, 84, 3, 1.0f, ContractTier.Standard,
                "Ravenscraig, Port Talbot and Scunthorpe, all three furnaces at once."),
            new("c6_christmas", "The Christmas special",          80e9,  3,  88, 5, 1.6f, ContractTier.Reputation,
                "Twenty-eight million watching the same show, and every kettle in the land on at the end of it."),
        };

        // -------------------------------------------------------------------------------

        public readonly struct RegionSpec
        {
            public readonly string Id, Name, Code, Parent;
            public readonly double BaseGigawatts, Length, X, Y;
            public readonly bool StartsOn, Industrial;
            public RegionSpec(string id, string name, string code, string parent, double gw, double length, double x, double y,
                              bool startsOn, bool industrial)
            {
                Id = id; Name = name; Code = code; Parent = parent; BaseGigawatts = gw; Length = length; X = x; Y = y;
                StartsOn = startsOn; Industrial = industrial;
            }
            public double ConnectPounds => StartsOn ? 0 : ConnectPoundsPerGigawatt * BaseGigawatts;
        }

        public readonly struct UnitSpec
        {
            public readonly GeneratorType Type;
            public readonly string Name;
            public readonly double CostPounds, Watts;
            public readonly float RampPerMinute, MinStable, StartHours;
            public readonly bool Follows;
            public readonly double FuelPoundsPerMWh, CoalTonnesPerMWh;
            public readonly float ServiceIntervalHours, InertiaSeconds;
            public readonly bool BlackStart;
            public readonly int MaxUnits;
            public readonly string UnlockedBy, Description;
            public UnitSpec(GeneratorType type, string name, double cost, double watts, float ramp, bool follows, float min,
                            float start, double fuel, double coal, float service, float inertia, bool blackStart, int max,
                            string unlockedBy, string description)
            {
                Type = type; Name = name; CostPounds = cost; Watts = watts; RampPerMinute = ramp; Follows = follows;
                MinStable = min; StartHours = start; FuelPoundsPerMWh = fuel; CoalTonnesPerMWh = coal;
                ServiceIntervalHours = service; InertiaSeconds = inertia; BlackStart = blackStart; MaxUnits = max;
                UnlockedBy = unlockedBy; Description = description;
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
