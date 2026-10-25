using UnityEngine;

namespace IdlePowerGen.Models
{
    public enum GeneratorType
    {
        // Era 1
        HandCrank,
        SpinningJenny,
        Waterwheel,
        SteamBoiler,
        // Era 2
        SmallDynamo,
        GasLampGenerator,
        CoalFiredBoiler,
        SteamEngine,
        LargeSteamEngine,
        // Era 3 (saved by name, so append only)
        JumboDynamo,
        ParsonsTurbine,
        CoalStation,
        CityStation,
        // Era 4 units
        CoalUnit,
        HydroSet,
        TurboAlternator,
        RegionalStation,
        // Era 5 units
        LargeCoalUnit,
        OilPeaker,
        HydroScheme,
        MagnoxReactor,
    }

    [CreateAssetMenu(fileName = "GeneratorDef", menuName = "IdlePowerGen/Generator Definition")]
    public class GeneratorDefinition : ScriptableObject
    {
        public GeneratorType generatorType;
        public string        displayName;
        [TextArea]
        public string        description;
        public int           minEra;

        [Header("Output")]
        public double basePowerWatts;
        public double fuelConsumptionPerSecond; // 0 = no fuel (manual / water / steam boiler)

        [Header("Running Costs (pennies per game hour)")]
        public double wagePenniesPerHour; // paid from the purse; unpaid hands stop work
        public double coalPenniesPerHour; // burned from the coal hopper; an empty hopper stops it

        [Header("Cost — Stackable  cost(n) = base × 1.15^n  §9.3.1")]
        public double baseCostJoules;
        public double baseCostPennies;

        [Header("Maintenance §9.4")]
        public float maintenanceBaseRate  = 0.03f;  // 3 % of capex / yr
        public float maintenanceAgeRate   = 0.005f; // +0.5 % / yr of age
        public float serviceIntervalHours = 720f;   // 30 in-game days

        [Header("Grid unit (Era 4–5)")]
        // How fast its governor picks up load (seconds to ~63 %): hydro is quick, coal slow.
        public float governorSeconds = 5f;
        // Spinning mass, as the inertia constant H in seconds (bible §4.1).
        public float inertiaSeconds = 5f;
        // Coal burned per megawatt-hour sent (0 = no coal: hydro, the inherited station).
        public double coalTonnesPerMWh;
        // Can start a dead grid on its own (bible §10.3.2): hydro does; coal needs fitting.
        public bool blackStartCapable;
        // Most that can be built (hydro: the valleys run out). 0 = no limit.
        public int maxUnits;

        [Header("Unlock Gate")]
        // Empty = always available; otherwise must be purchased before this generator can be built
        public string requiredUpgradeId;

        [Header("Visuals")]
        public Sprite icon;
    }
}
