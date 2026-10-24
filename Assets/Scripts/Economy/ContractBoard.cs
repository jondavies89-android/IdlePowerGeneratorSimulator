using System;
using System.Collections.Generic;
using IdlePowerGen.Models;

namespace IdlePowerGen.Economy
{
    // Which contracts are on offer right now (bible §2.2, Era 2: "scheduled contracts vary
    // by time of day"). A client asks during their window of the day, once a day: a
    // contract booked, finished or failed today comes back tomorrow.
    public class ContractBoard
    {
        private readonly Dictionary<string, long> _bookedDay = new();

        public const double SecondsPerDay = 86400.0;

        public static long DayOf(double gameTime) => (long)Math.Floor(gameTime / SecondsPerDay);
        public static float HourOf(double gameTime) => (float)(gameTime % SecondsPerDay / 3600.0);

        public static bool InWindow(ContractDefinition def, float hour) =>
            def.availableStartHour <= def.availableEndHour
                ? hour >= def.availableStartHour && hour < def.availableEndHour
                // A window past midnight, e.g. 22:00 → 02:00.
                : hour >= def.availableStartHour || hour < def.availableEndHour;

        public bool BookedToday(ContractDefinition def, double gameTime) =>
            _bookedDay.TryGetValue(def.contractId, out var day) && day == DayOf(gameTime);

        // Open to this player now, slots aside.
        public bool IsOffered(ContractDefinition def, double gameTime, float reputation,
                              IReadOnlyList<ContractInstance> active)
        {
            if (reputation < def.requiredReputation) return false;
            if (!InWindow(def, HourOf(gameTime)) || BookedToday(def, gameTime)) return false;
            foreach (var c in active)
                if (c.Definition.contractId == def.contractId) return false;
            return true;
        }

        public bool TryBook(ContractDefinition def, double gameTime, float reputation,
                            ContractLedger ledger, int slots)
        {
            if (!IsOffered(def, gameTime, reputation, ledger.ActiveContracts)) return false;
            if (!ledger.TryAccept(def, gameTime, slots)) return false;
            _bookedDay[def.contractId] = DayOf(gameTime);
            return true;
        }

        // Game hours until the window next opens (0 if open now).
        public static float HoursUntilOpen(ContractDefinition def, float hour)
        {
            if (InWindow(def, hour)) return 0f;
            float h = def.availableStartHour - hour;
            return h < 0 ? h + 24f : h;
        }

        // Save support
        public IReadOnlyDictionary<string, long> BookedDays => _bookedDay;

        public void LoadFromSave(IEnumerable<(string id, long day)> entries)
        {
            _bookedDay.Clear();
            if (entries == null) return;
            foreach (var (id, day) in entries) _bookedDay[id] = day;
        }
    }
}
