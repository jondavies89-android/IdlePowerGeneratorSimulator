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
        AddCrankJoules,          // effectValue = joules added to every turn of the crank
        AddFlywheelCapacity,     // effectValue = watts of spin the flywheel can hold
        MultiplyFlywheelDecay,   // effectValue = multiplier on how fast the flywheel slows
        MultiplyCrankOutput,     // effectValue = output multiplier; secondaryValue = decay multiplier
        AddFlywheelFloor,        // effectValue = watts of spin the flywheel never drops below
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
        public double costPennies;
        public double costGrowth = 1.0;

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

        [Header("Visuals")]
        public Sprite icon;
    }
}
