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

        // --- Boosts (v6) ---
        public double contractBoostSecondsLeft; // real seconds of the 2× contracts boost

        // --- Eras 3–5 (v8) ---
        public double researchPoints;      // bible §13; kept from era to era
        public long   eraStartDay;         // game day the current era (3 on) began; its date counts from it
        public List<LoadAreaEntry> loadAreas = new(); // districts, feeders, cities
        public List<UnitEntry>     units     = new(); // Era 4–5 generating units, one each
        public double coalStockTonnes;     // Era 4–5 coal stockpile
        public float  coalTargetDays;      // the fuel manager's target cover, once introduced
        public List<string> introducedScores = new(); // preparedness scores on the dashboard
        public bool   capstoneOffered;     // the current era's goal was reached

        // --- Contract managers (v10), the current era's ---
        public List<ContractManagerEntry> contractManagers = new();

        // --- Era 6 (v11): the last black-start drill, for operator training (OTP) ---
        public double drillScore;          // points the drill earned, before it fades
        public double drillGameTime;       // game seconds when it ended

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
        public class ContractManagerEntry
        {
            public bool   on;
            public string contractId;     // "" = the best-paying client it can supply
        }

        [Serializable]
        public class GeneratorEntry
        {
            public string generatorType; // maps to GeneratorType enum name
            public int    count;
        }

        [Serializable]
        public class LoadAreaEntry
        {
            public string id;
            public bool   connected;
            public int    copperLevel;    // Era 3: heavier mains to this district
            public int    capacitorBanks; // Era 5: banks fitted at this city
            public bool   shed;           // Era 4–5: disconnected by hand to save the grid
        }

        [Serializable]
        public class UnitEntry
        {
            public string generatorType;  // GeneratorType name
            public int    number;         // "UNIT 3" on the board
            public string state;          // GridUnit.UnitState name
            public double hoursSinceService;
            public double hoursLeft;      // in service or under repair
            public bool   blackStart;     // fitted with black-start capability (Era 5)
            // v11, Era 6:
            public string mode;           // GridUnit.UnitMode name (BASE or AGC)
            public bool   backToStandby;  // in service from standby: returns there, not to the bars
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
            // v10: the contract manager who booked it (1, 2, …), 0 if the player did.
            public int    managerNumber;
        }
    }
}
