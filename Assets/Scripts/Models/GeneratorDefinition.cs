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

        [Header("Cost — Stackable  cost(n) = base × 1.15^n  §9.3.1")]
        public double baseCostJoules;
        public double baseCostPennies;

        [Header("Maintenance §9.4")]
        public float maintenanceBaseRate  = 0.03f;  // 3 % of capex / yr
        public float maintenanceAgeRate   = 0.005f; // +0.5 % / yr of age
        public float serviceIntervalHours = 720f;   // 30 in-game days

        [Header("Unlock Gate")]
        // Empty = always available; otherwise must be purchased before this generator can be built
        public string requiredUpgradeId;

        [Header("Visuals")]
        public Sprite icon;
    }
}
