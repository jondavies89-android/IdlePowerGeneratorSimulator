using UnityEngine;

namespace IdlePowerGen.Models
{
    public enum ContractTier { Standard, Reputation }

    [CreateAssetMenu(fileName = "ContractDef", menuName = "IdlePowerGen/Contract Definition")]
    public class ContractDefinition : ScriptableObject
    {
        public string       contractId;
        public string       clientName;
        [TextArea]
        public string       description;
        public ContractTier tier;
        public int          minEra;

        [Header("Supply Requirements")]
        public double requiredWatts;
        public float  durationHours;

        [Header("Scheduling  §3.4")]
        public float availableStartHour; // in-game hour of day contract becomes available
        public float availableEndHour;   // expires if not accepted before this hour

        [Header("Reward  §5.1.6")]
        public float payRateMultiplier  = 1f;   // multiplied against era base rate
        public float reputationOnSuccess = 1f;
        public float reputationOnFail    = -2f;
        public float failThreshold       = 0.5f; // below 50 % average output = fail

        [Header("Reputation-Tier Bonus")]
        public float reputationTierPayMultiplier = 1f; // 1.3–1.6× for reputation-tier contracts
    }
}
