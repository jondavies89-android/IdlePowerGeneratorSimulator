using UnityEngine;

namespace IdlePowerGen.Models
{
    public enum UpgradeEffect
    {
        MultiplyCrankTapValue,   // effectValue = multiplier
        AddPassiveWatts,         // effectValue = watts added to auto-generators
        UnlockGenerator,         // unlocksId = GeneratorType name
        UnlockFeature,           // unlocksId = feature key
        MultiplyAllOutput,       // effectValue = multiplier applied to all generators
    }

    [CreateAssetMenu(fileName = "UpgradeDef", menuName = "IdlePowerGen/Upgrade Definition")]
    public class UpgradeDefinition : ScriptableObject
    {
        public string upgradeId;
        public string displayName;
        [TextArea]
        public string description;
        public int    minEra;

        [Header("Cost — One-time, next tier = prev × 5  §9.3")]
        public double costJoules;
        public double costPennies;
        public float  costReputation;

        [Header("Effect")]
        public UpgradeEffect effect;
        public double        effectValue;
        public string        unlocksId;

        [Header("Prerequisites")]
        public string[] requiredUpgradeIds;
        public double   requiredStoredJoules; // minimum stored charge to unlock
        public double   requiredPennies;

        [Header("Visuals")]
        public Sprite icon;
    }
}
