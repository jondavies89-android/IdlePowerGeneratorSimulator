using UnityEngine;

namespace IdlePowerGen.Models
{
    [CreateAssetMenu(fileName = "EraConfig", menuName = "IdlePowerGen/Era Config")]
    public class EraConfig : ScriptableObject
    {
        public int    eraNumber;
        public string eraName;
        public string flavorYear;
        public string flavorLocation;

        [Header("Wattage Range")]
        public double minWatts;
        public double maxWatts;

        [Header("Currencies Active")]
        public bool useJoules     = true;
        public bool useCash       = false;
        public bool useReputation = false;

        [Header("Offline Progression §7.2")]
        [Range(0f, 1f)] public float offlineEraFactor = 1.0f;
        public float offlineTauHours = 4f;

        [Header("Economy")]
        public double contractBaseRate = 0.001; // pennies per W·s
        // Price of spare power sold to the street, as a fraction of contractBaseRate.
        // 0 = no street trade (Era 1, where spare output banks as joules instead).
        [Range(0f, 1f)] public float streetRateFactor;
        public float  xpScaleFactor    = 5f;

        [Header("Upgrade tree")]
        // Column headings of the era's upgrade tree, indexed by UpgradeDefinition.treeBranch.
        public string[] upgradeBranchNames;
        // The same for its research tree (Era 3 on).
        public string[] researchBranchNames;

        [Header("Era 3 on")]
        // Pennies paid per watt-hour delivered to a connected district, feeder or city
        // (shown in pounds: 240 d = £1). Contracts use contractBaseRate as before.
        public double tariffPenniesPerWattHour;

        [Header("Flavour")]
        [TextArea] public string offlineFlavorText;
    }
}
