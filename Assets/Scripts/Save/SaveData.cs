using System;
using System.Collections.Generic;

namespace IdlePowerGen.Save
{
    // §7.3 — full save-state structure.
    // Serialised as JSON (JsonUtility). decimal stored as string to avoid float drift.
    // Increment CurrentVersion and add a migration function whenever the schema changes.
    [Serializable]
    public class SaveData
    {
        public int    version              = 1;
        public long   saveTimestampUnix;   // §7.5 cheat-resistance anchor

        // --- Currencies ---
        public double joules;
        public string pennies;             // decimal serialised as string
        public float  reputation;

        // --- Time ---
        public float  currentEra;
        public double gameTimeSeconds;

        // --- Upgrades ---
        public List<string> purchasedUpgradeIds = new();

        // --- Generators (Era 2+) ---
        public List<GeneratorEntry> generators = new();

        // --- Contracts ---
        public List<ContractEntry> activeContracts = new();

        // --- Lifetime stats (prestige §12) ---
        public double lifetimeMWh;
        public int    engineeringXP;

        // -------------------------------------------------------

        [Serializable]
        public class GeneratorEntry
        {
            public string generatorType; // maps to GeneratorType enum name
            public int    count;
        }

        [Serializable]
        public class ContractEntry
        {
            public string contractId;
            public double startGameTime;
            public double accumulatedWattSeconds;
        }
    }
}
