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
        // v1–v2: one-time purchases. v3 moves them into upgradeLevels and leaves this empty.
        public List<string> purchasedUpgradeIds = new();
        public List<UpgradeLevelEntry> upgradeLevels = new();

        // --- Generators (Era 2+) ---
        public List<GeneratorEntry> generators = new();

        // --- Contracts ---
        public List<ContractEntry> activeContracts = new();

        // --- Milestones (v2) ---
        public List<string> reachedMilestones = new();

        // --- Era 2 works (v4) ---
        public long   era2StartDay;        // game day Era 2 began; the shown date counts from it
        public double coalLevel;           // pennies' worth of coal in the hopper
        public List<BookedDayEntry> contractBookedDays = new();

        // --- Boosts (v6) ---
        public double contractBoostSecondsLeft; // real seconds of the 2× contracts boost

        // --- Lifetime stats (prestige §12) ---
        public double lifetimeMWh;
        public int    engineeringXP;

        // -------------------------------------------------------

        [Serializable]
        public class UpgradeLevelEntry
        {
            public string upgradeId;
            public int    level;
        }

        [Serializable]
        public class BookedDayEntry
        {
            public string contractId;
            public long   day;
        }

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
            // v5: pennies already paid while it ran (decimal as string, like the purse).
            public string paidPennies;
            // v6: contract time served, in game seconds (contracts can run faster than the clock).
            public double progressSeconds;
            // v7: the shortfall countdown, in game seconds: time short so far, and time
            // supplied in a row towards clearing it.
            public double shortSeconds;
            public double suppliedSeconds;
        }
    }
}
