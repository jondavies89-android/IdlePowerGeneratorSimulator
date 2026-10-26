using IdlePowerGen.Models;

namespace IdlePowerGen.Data
{
    // Numeric constants for Era 3 — "The War of Currents" (100 kW → 10 MW, Westgate 1893).
    // The source of truth for Era3AssetBuilder, which writes the ScriptableObjects.
    //
    // The era's one new concept (bible §2.2): distance has a cost. The town's districts pay
    // for every unit that reaches them, and the further and the bigger a district, the more
    // of what the station sends is lost on the way. Edison's direct current can only reach
    // the streets next to the station; transformers and alternating current reach the town.
    //
    // Money is held in pennies (240 d = £1); prices here are in pounds, converted by the
    // builder.
    public static class Era3Data
    {
        public const int    EraNumber = 3;
        public const double MinWatts  = 100e3;
        public const double MaxWatts  = 10e6;

        // A district pays this per watt-hour it receives (£0.42 a unit). With the bible's
        // capex anchors (£10 a kilowatt for the first machines) a machine pays for itself in
        // about a day of game time, a real half hour; Era 2's 0.4 made that four game hours,
        // and a greedy pacing run finished the era in under two real hours against the
        // bible's 8–24 (bible §2.1). The Exhibition's takings carry the first purchases.
        public const double TariffPenniesPerWattHour = 0.1;

        // Contract clients pay half as much again per watt as the town, for being booked.
        public const double ContractBaseRate = 0.15;
        public const int    BaseContractSlots = 2;

        public const float  OfflineEraFactor  = 1.0f;
        public const float  OfflineTauHours   = 24f; // bible §7.2: Eras 1–3
        public const string OfflineFlavorText = "The night engineer kept the dynamos turning while you were away.";

        // The bible's Era 3 mockup: Pearl Street, 7th April 1893, the lamps coming on.
        public const float StartHour  = 17f;
        public const int   StartYear  = 1893;
        public const int   StartMonth = 4;
        public const int   StartDay   = 7;

        // What a player typically brings from Era 2: the Exhibition's £2,133 and change, and
        // its repute.
        public const double TypicalStartPounds     = 2_500;
        public const float  TypicalStartReputation = 33f;

        // -------------------------------------------------------------------------------
        // Active play (real seconds, only while the player is here; nothing happens offline).
        //
        // Stoking the boilers (the Town page): each stoke raises the steam pressure, which
        // falls on its own, so holding it in the green takes a stoke every second or two. In
        // the green the works earns twice as much: half as much again was +£10 a game hour at
        // the era's start, twenty minutes' stoking for one Jumbo. Past the top the safety valve lifts:
        // the pressure drops and the boilers can't be stoked for a few seconds.
        public const float  StokePerTap           = 0.08f;
        public const float  PressureFallPerSecond = 0.05f;
        public const float  GreenLow              = 0.60f;
        public const float  GreenHigh             = 0.85f;
        public const float  ValveResetPressure    = 0.30f;
        public const float  ValveLiftSeconds      = 4f;
        public const double StokeIncomeBonus      = 1.0;

        // The bench galvanometer (the Research tab): a needle swings across the scale; a
        // reading taken while it is in the marked band earns research. A reading is worth
        // 5 % of an hour of the works' R&D (at least 1 RP), and each one in a run adds 15 %
        // more, up to double. With one workshop that is several times its rate while the
        // player keeps at it, so the 80 RP to transformers (and AC) comes in minutes.
        public const double NeedleRadiansPerSecond = 2.2;
        public const double NeedleSpeedUpPerRun    = 0.06;  // the needle quickens as a run goes on
        public const double BandHalfWidth          = 0.12;  // of the scale's half-width
        public const double MinBandHalfWidth       = 0.07;
        public const double BandNarrowsPerRun      = 0.008;
        public const float  HitSettleSeconds       = 0.8f;
        public const float  MissSettleSeconds      = 2.0f;
        public const double ReadingMinResearch     = 1;
        public const double ReadingShareOfHour     = 0.05;
        public const double RunBonusPerReading     = 0.15;
        public const double MaxRunBonus            = 1.0;

        // Same leeway and countdown as Era 2's clients.
        public const float ShortfallLeeway               = 0.1f;
        public const float ShortfallCountdownMinutes     = 30f;
        public const float ShortfallForgivenAfterMinutes = 60f;

        // -------------------------------------------------------------------------------
        // Mains. Loss is the fraction of a district's power lost per megawatt delivered per
        // block of distance (so a district twice as big, or twice as far, loses twice the
        // share). Raising the voltage cuts it hard: the square law, without the equation.
        // Direct current can't reach past Reach blocks at all.
        // -------------------------------------------------------------------------------

        public static readonly MainsSpec[] Mains =
        {
            new("Edison direct current, 110 volts",  0.8,   2),
            new("Three-wire direct current, 220 volts", 0.2, 2),
            new("Alternating current, 2,000 volts",  0.01,  99),
            new("High-voltage mains, 6,600 volts",   0.001, 99),
        };

        // Heavier copper to one district: each level halves its losses.
        public const int    MaxCopperLevel  = 2;
        public const double CopperLossFactor = 0.5;
        // A copper level costs this share of the district's connection price, per level.
        public const double CopperCostShare = 0.5;

        // A district this lossy flickers (amber); past FlickerBadly it's red.
        public const double FlickerLoss      = 0.15;
        public const double FlickerBadlyLoss = 0.30;

        // -------------------------------------------------------------------------------
        // The town of Westgate: a 6 × 3 grid of districts, the station at the left of the
        // middle row (column 0, row 1). Distance is in blocks along the streets. Lines run
        // out along the middle row with spurs up and down, so a district can only be
        // connected once the one between it and the station is. Connection costs £3 per
        // kilowatt per block: the same copper, further.
        // -------------------------------------------------------------------------------

        public const int StationColumn = 0, StationRow = 1;
        public const int MapColumns = 6, MapRows = 3;
        public const double ConnectPoundsPerKilowattBlock = 3;

        public static readonly DistrictSpec[] Districts =
        {
            //  id                 name                 col row  base kW  starts lit
            new("d3_pearl",        "Pearl Street",      0,  0,   30,     true),
            new("d3_church",       "Church Row",        0,  2,   25,     true),
            new("d3_market",       "Market Square",     1,  1,   45,     true),
            new("d3_bond",         "Bond Street",       1,  0,   120,    false),
            new("d3_mill",         "Mill Lane",         1,  2,   150,    false),
            new("d3_high",         "High Street",       2,  1,   200,    false),
            new("d3_theatre",      "Theatre Quarter",   2,  0,   300,    false),
            new("d3_canal",        "Canal Wharf",       2,  2,   350,    false),
            new("d3_railway",      "Railway Station",   3,  1,   450,    false),
            new("d3_park",         "Westgate Park",     3,  0,   500,    false),
            new("d3_ironworks",    "Ironworks Row",     3,  2,   700,    false),
            new("d3_townhall",     "Town Hall",         4,  1,   800,    false),
            new("d3_northgate",    "Northgate",         4,  0,   900,    false),
            new("d3_docks",        "The Docks",         4,  2,   1_100,  false),
            new("d3_tramdepot",    "Tram Depot",        5,  1,   1_300,  false),
            new("d3_hillside",     "Hillside",          5,  0,   1_500,  false),
            new("d3_southgate",    "Southgate Mills",   5,  2,   2_000,  false),
        };

        // -------------------------------------------------------------------------------
        // Machines — bible §9.7: £200 first, £8,000 mid-era, £40,000 capstone. Stackable,
        // cost × 1.15ⁿ. Coal is reckoned into the tariff: one concept at a time.
        // Each tier costs a little more per kilowatt than the last (£10, £15, £20, £27): the
        // first of a new kind beats the tenth of the old, without one purchase being half the
        // era. A pacing run with a 5 MW city station jumped 1.5 → 6.5 MW in one buy.
        // -------------------------------------------------------------------------------

        public static readonly GeneratorSpec[] Generators =
        {
            //  type                             name                        £        watts      unlocked by
            new(GeneratorType.JumboDynamo,      "Jumbo dynamo",             200,     20e3,      null,
                "A steam engine coupled to one of Mr Edison's 'Jumbo' dynamos."),
            new(GeneratorType.ParsonsTurbine,   "Parsons turbo-generator",  1_500,   100e3,     SteamTurbineId,
                "Steam straight onto turbine blades: smaller, faster, smoother."),
            new(GeneratorType.CoalStation,      "Coal-fired station",       8_000,   400e3,     WaterTubeId,
                "A boiler house, a chimney, and a row of turbo-generators."),
            new(GeneratorType.CityStation,      "City power station",       40_000,  1.5e6,     CondensingId,
                "The town's own power station, built to last fifty years."),
        };

        // -------------------------------------------------------------------------------
        // Research (bible §13). Era-gated: each node opens a machine or a mains upgrade.
        // Transformer principles at 80 RP is the bible's mockup.
        // -------------------------------------------------------------------------------

        public const int GenerationBranch = 0, MainsBranch = 1, CustomersBranch = 2;
        public static readonly string[] BranchNames         = { "Generation", "Mains", "Customers" };
        public static readonly string[] ResearchBranchNames = { "Generation", "Mains", "Customers" };

        // Ids are saved, so never rename one.
        public const string TransformerId  = "res3_transformer";
        public const string HighVoltageId  = "res3_high_voltage";
        public const string SteamTurbineId = "res3_steam_turbine";
        public const string WaterTubeId    = "res3_water_tube";
        public const string CondensingId   = "res3_condensing";
        public const string MetersId       = "res3_meters";

        public static readonly ResearchSpec[] Research =
        {
            //  id               name                        RP      effect                          unlocks                              branch            row  prereqs
            new(SteamTurbineId, "The steam turbine",         150,    UpgradeEffect.UnlockGenerator,  nameof(GeneratorType.ParsonsTurbine), GenerationBranch, 0,  new string[0],
                "Mr Parsons' turbine: steam on blades, not pistons."),
            new(WaterTubeId,    "Water-tube boilers",        500,    UpgradeEffect.UnlockGenerator,  nameof(GeneratorType.CoalStation),    GenerationBranch, 1,  new[] { SteamTurbineId },
                "Boilers that raise steam fast enough for a whole station."),
            new(CondensingId,   "Condensing turbines",       2_500,  UpgradeEffect.UnlockGenerator,  nameof(GeneratorType.CityStation),    GenerationBranch, 2,  new[] { WaterTubeId },
                "Exhaust into a vacuum and the same coal does half as much again."),
            new(TransformerId,  "Transformer principles",    80,     UpgradeEffect.UnlockFeature,    TransformerId,                        MainsBranch,      0,  new string[0],
                "Step the voltage up to send it far, and down again to use it."),
            new(HighVoltageId,  "High-voltage mains",        1_200,  UpgradeEffect.UnlockFeature,    HighVoltageId,                        MainsBranch,      1,  new[] { TransformerId },
                "Insulation good enough for six and a half thousand volts."),
            new(MetersId,       "Electricity meters",        300,    UpgradeEffect.UnlockFeature,    MetersId,                             CustomersBranch,  0,  new string[0],
                "Mr Shallenberger's meter: customers pay for what they use."),
        };

        // -------------------------------------------------------------------------------
        // Purchases (pounds). Reputation is a gate, not a cost.
        // -------------------------------------------------------------------------------

        public const string JumboBoilersId  = "era3_jumbo_boilers";
        public const string BladingId       = "era3_reaction_blading";
        public const string StokersId       = "era3_mechanical_stokers";
        public const string SuperheatId     = "era3_superheat";
        public const string ThreeWireId     = "era3_three_wire";
        public const string AlternatorsId   = "era3_alternators";
        public const string SixKilovoltId   = "era3_6600_volt_mains";
        public const string HardCopperId    = "era3_hard_drawn_copper";
        public const string WorkshopId      = "era3_apprentice_workshop";
        public const string FitMetersId     = "era3_fit_meters";
        public const string ManagerId       = "era3_contract_manager";
        public const string TelephoneId     = "era3_telephone";
        public const string SignsId         = "era3_electric_signs";

        // Bible §13.1.2: the Apprentice's workshop, £500, 5 RP an hour. Stackable (× 1.15).
        public const double WorkshopResearchPerHour = 5;

        public static readonly UpgradeSpec[] Upgrades =
        {
            //  id               name                         £          growth max rep   effect                                 value  unlocks                              branch            row  prereqs
            // Generation
            new(JumboBoilersId, "Bigger Jumbo boilers",       600,       1,     1,  0,    UpgradeEffect.MultiplyGeneratorOutput, 2,     nameof(GeneratorType.JumboDynamo),    GenerationBranch, 0,  new string[0],
                "More steam to every Jumbo."),
            new(BladingId,      "Reaction blading",           8_000,     1,     1,  0,    UpgradeEffect.MultiplyGeneratorOutput, 2,     nameof(GeneratorType.ParsonsTurbine), GenerationBranch, 1,  new[] { JumboBoilersId, SteamTurbineId },
                "Rows of blades that each take a little of the steam's push."),
            new(StokersId,      "Mechanical stokers",         50_000,    1,     1,  0,    UpgradeEffect.MultiplyGeneratorOutput, 2,     nameof(GeneratorType.CoalStation),    GenerationBranch, 2,  new[] { BladingId, WaterTubeId },
                "Chain grates feed the fires evenly, night and day."),
            new(SuperheatId,    "Superheated steam",          250_000,   1,     1,  40,   UpgradeEffect.MultiplyAllOutput,       1.5,   null,                                 GenerationBranch, 3,  new[] { StokersId },
                "Steam heated past boiling: every machine runs harder."),
            // Mains
            new(ThreeWireId,    "Three-wire mains",           400,       1,     1,  0,    UpgradeEffect.SetMainsTier,            1,     null,                                 MainsBranch,      0,  new string[0],
                "A third wire doubles the voltage on the same copper."),
            new(AlternatorsId,  "Westinghouse alternators",   3_000,     1,     1,  0,    UpgradeEffect.SetMainsTier,            2,     null,                                 MainsBranch,      1,  new[] { ThreeWireId, TransformerId },
                "Alternating current at 2,000 volts, stepped down in every street."),
            new(HardCopperId,   "Hard-drawn copper",          15_000,    1,     1,  0,    UpgradeEffect.MultiplyLineLosses,      0.7,   null,                                 MainsBranch,      2,  new[] { AlternatorsId },
                "Stronger, purer wire on every line."),
            new(SixKilovoltId,  "6,600-volt mains",           25_000,    1,     1,  0,    UpgradeEffect.SetMainsTier,            3,     null,                                 MainsBranch,      3,  new[] { HardCopperId, HighVoltageId },
                "Higher still: the far side of town for a fraction of the loss."),
            // Customers
            new(WorkshopId,     "Apprentice's workshop",      500,       1.15,  10, 0,    UpgradeEffect.AddResearchRate,         WorkshopResearchPerHour, null,                 CustomersBranch,  0,  new string[0],
                "A bench, some instruments and a bright apprentice. Makes research."),
            new(FitMetersId,    "Fit Shallenberger meters",   2_000,     1,     1,  0,    UpgradeEffect.MultiplyTariff,          1.25,  null,                                 CustomersBranch,  1,  new[] { WorkshopId, MetersId },
                "Every house metered: nobody gets light for nothing."),
            new(ManagerId,      "Contract manager",           1_500,     3,     4,  0,    UpgradeEffect.AddContractManager,      0,     null,                                 CustomersBranch,  2,  new[] { FitMetersId },
                "Books a client for one contract slot, for a cut of the fee. One per slot."),
            new(TelephoneId,    "Telephone exchange",         5_000,     8,     2,  36,   UpgradeEffect.AddContractSlot,         1,     null,                                 CustomersBranch,  3,  new[] { ManagerId },
                "Big clients can ring through. One more contract at a time."),
            new(SignsId,        "Electric signs",             30_000,    1,     1,  44,   UpgradeEffect.MultiplyTariff,          1.2,   null,                                 CustomersBranch,  4,  new[] { TelephoneId },
                "Every shop wants its name in lights, and pays for them."),
        };

        // -------------------------------------------------------------------------------
        // Contracts: the town's big clients, booked whenever there's a slot, as in Era 2.
        // Reputation carries on from Era 2 (≈33 at the Exhibition), so the gates start there.
        // -------------------------------------------------------------------------------

        public static readonly ContractSpec[] Contracts =
        {
            //  id                   client                          watts    hours rep   +rep pay   tier
            new("c3_tramway",       "Westgate Tramways",             150e3,   6,    0,    1,   1.0f, ContractTier.Standard,
                "The first electric trams want running for the morning crowds."),
            new("c3_theatre",       "The Electric Theatre",          300e3,   4,    30,   1,   1.0f, ContractTier.Standard,
                "Footlights, house lights and a lift to the gallery."),
            new("c3_mill",          "Pennine Cotton Mill",           800e3,   10,   34,   2,   1.0f, ContractTier.Standard,
                "Electric motors for the spinning frames, from first light."),
            new("c3_infirmary",     "Royal Infirmary",               600e3,   24,   38,   3,   1.5f, ContractTier.Reputation,
                "Light in every ward, all day and all night. They will not forgive a dark night."),
            new("c3_alkali",        "Castner Alkali Works",          2.5e6,   12,   42,   3,   1.0f, ContractTier.Standard,
                "Electrolysis cells by the hundred. Hungry, steady work."),
            new("c3_illuminations", "The Promenade Illuminations",   4e6,     5,    48,   4,   1.5f, ContractTier.Reputation,
                "A mile of coloured lamps along the front. The whole county will come."),
        };

        // -------------------------------------------------------------------------------

        public readonly struct MainsSpec
        {
            public readonly string Name;
            public readonly double LossPerMegawattBlock;
            public readonly int    Reach;
            public MainsSpec(string name, double loss, int reach) { Name = name; LossPerMegawattBlock = loss; Reach = reach; }
        }

        public readonly struct DistrictSpec
        {
            public readonly string Id;
            public readonly string Name;
            public readonly int    Column;
            public readonly int    Row;
            public readonly double BaseKilowatts;
            public readonly bool   StartsLit;
            public DistrictSpec(string id, string name, int col, int row, double kw, bool startsLit)
            {
                Id = id; Name = name; Column = col; Row = row; BaseKilowatts = kw; StartsLit = startsLit;
            }
            public int Blocks => System.Math.Abs(Column - StationColumn) + System.Math.Abs(Row - StationRow);
            public double ConnectPounds => StartsLit ? 0 : ConnectPoundsPerKilowattBlock * BaseKilowatts * Blocks;
        }

        public readonly struct GeneratorSpec
        {
            public readonly GeneratorType Type;
            public readonly string        Name;
            public readonly double        CostPounds;
            public readonly double        Watts;
            public readonly string        UnlockedBy;
            public readonly string        Description;
            public GeneratorSpec(GeneratorType type, string name, double cost, double watts, string unlockedBy, string description)
            {
                Type = type; Name = name; CostPounds = cost; Watts = watts; UnlockedBy = unlockedBy; Description = description;
            }
        }

        public readonly struct ResearchSpec
        {
            public readonly string        Id;
            public readonly string        Name;
            public readonly double        CostRP;
            public readonly UpgradeEffect Effect;
            public readonly string        UnlocksId;
            public readonly int           Branch;
            public readonly int           Row;
            public readonly string[]      Prerequisites;
            public readonly string        Description;
            public ResearchSpec(string id, string name, double cost, UpgradeEffect effect, string unlocksId,
                                int branch, int row, string[] prereqs, string description)
            {
                Id = id; Name = name; CostRP = cost; Effect = effect; UnlocksId = unlocksId;
                Branch = branch; Row = row; Prerequisites = prereqs; Description = description;
            }
        }

        public readonly struct UpgradeSpec
        {
            public readonly string        Id;
            public readonly string        Name;
            public readonly double        CostPounds;
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
                Id = id; Name = name; CostPounds = cost; CostGrowth = growth; MaxLevel = maxLevel;
                RequiredReputation = rep; Effect = effect; Value = value; UnlocksId = unlocksId;
                Branch = branch; Row = row; Prerequisites = prereqs; Description = description;
            }
        }

        public readonly struct ContractSpec
        {
            public readonly string       Id;
            public readonly string       Client;
            public readonly double       Watts;
            public readonly float        Hours;
            public readonly float        RequiredReputation;
            public readonly float        ReputationOnSuccess;
            public readonly float        PayMultiplier;
            public readonly ContractTier Tier;
            public readonly string       Description;
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
