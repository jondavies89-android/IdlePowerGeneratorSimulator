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
        public float  xpScaleFactor    = 5f;

        [Header("Flavour")]
        [TextArea] public string offlineFlavorText;
    }
}
