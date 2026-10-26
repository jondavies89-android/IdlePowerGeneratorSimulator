using UnityEngine;

namespace IdlePowerGen.Models
{
    // Serialised by index in upgrade assets: append new values, never reorder.
    public enum UpgradeEffect
    {
        MultiplyCrankTapValue,   // effectValue = multiplier
        AddPassiveWatts,         // effectValue = watts added to auto-generators
        UnlockGenerator,         // unlocksId = GeneratorType name
        UnlockFeature,           // unlocksId = feature key
        MultiplyAllOutput,       // effectValue = multiplier applied to all generators

        // Era 1 crank and flywheel. Each applies once per level bought.
        AddCrankJoules,          // level n adds effectValue × n joules to every turn (+1, +2, +3 …)
        AddFlywheelCapacity,     // effectValue = watts of spin the flywheel can hold
        MultiplyFlywheelDecay,   // effectValue = multiplier on how fast the flywheel slows
        MultiplyCrankOutput,     // effectValue = crank and flywheel multiplier; secondaryValue = flywheel decay multiplier
        AddWaterWheelWatts,      // effectValue = steady watts from the water wheel
        MultiplyWaterWheelOutput, // effectValue = multiplier on the water wheel's output

        // Era 2
        MultiplyGeneratorOutput, // effectValue = multiplier; unlocksId = GeneratorType name
        AddContractSlot,         // effectValue = engagements that can run at once, per level
        MultiplyStreetTrade,     // effectValue = multiplier on the price the street pays

        // Era 3 on
        AddResearchRate,         // effectValue = research points per game hour, per level (an R&D building)
        MultiplyTariff,          // effectValue = multiplier on what load areas pay per unit
        MultiplyLineLosses,      // effectValue = multiplier on every line's losses
        SetMainsTier,            // effectValue = mains tier reached (Era 3: 1 three-wire DC, 2 AC, 3 high-voltage AC)

        // Era 4 on
        MultiplyFuelBurn,        // effectValue = multiplier on coal burned per MWh
        MultiplyServiceTime,     // effectValue = multiplier on how long a service or repair takes
        MultiplyFuelPrice,       // effectValue = multiplier on the price of coal
        MultiplyGovernorTime,    // effectValue = multiplier on every governor's lag (smaller is quicker)

        // Era 5 on
        AddCapacitorStrength,    // effectValue = extra share of a city's reactive power each bank supplies
        AddVoltageSupport,       // effectValue = per-unit voltage the tap changers add at every city

        // Era 2 on
        AddContractManager,      // one contract manager per level, each for one contract slot
    }

    [CreateAssetMenu(fileName = "UpgradeDef", menuName = "IdlePowerGen/Upgrade Definition")]
    public class UpgradeDefinition : ScriptableObject
    {
        public string upgradeId;
        public string displayName;
        [TextArea]
        public string description;
        public int    minEra;

        [Header("Cost — level n costs base × costGrowth^n  §9.3")]
        public double costJoules;
        // Money is held in pennies in every era; from Era 3 it shows as pounds (240 d = £1).
        public double costPennies;
        public double costResearch; // research points (bible §13)
        public double costGrowth = 1.0;

        [Header("Research (Era 3 on)")]
        // A research node rather than a purchase: shown on the Research tab, and kept from era
        // to era (bible §13: research is the long memory of the player's career).
        public bool isResearch;

        [Header("Levels (1 = one-time purchase)")]
        public int maxLevel = 1;

        [Header("Reputation Gate (required, not consumed)")]
        public float  requiredReputation;

        [Header("Effect")]
        public UpgradeEffect effect;
        public double        effectValue;
        public double        secondaryValue;
        public string        unlocksId;

        [Header("Prerequisites")]
        public string[] requiredUpgradeIds;
        public double   requiredStoredJoules; // minimum stored charge to unlock
        public double   requiredPennies;

        [Header("Upgrade tree (Era 2 on)")]
        public int treeBranch; // column, see EraConfig.upgradeBranchNames
        public int treeRow;    // 0 at the top; prerequisites sit on earlier rows

        [Header("Visuals")]
        public Sprite icon;
    }
}
