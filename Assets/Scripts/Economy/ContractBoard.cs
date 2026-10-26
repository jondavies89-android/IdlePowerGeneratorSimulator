using System;
using System.Collections.Generic;
using IdlePowerGen.Models;

namespace IdlePowerGen.Economy
{
    // Which contracts are on offer. Every client the works' repute opens is on the board
    // whenever the player wants them, and back as soon as they finish: clients asking only in
    // their own hours, once a day, made for a lot of waiting on the clock with nothing to do.
    public class ContractBoard
    {
        public const double SecondsPerDay = 86400.0;

        public static long DayOf(double gameTime) => (long)Math.Floor(gameTime / SecondsPerDay);
        public static float HourOf(double gameTime) => (float)(gameTime % SecondsPerDay / 3600.0);

        // Open to this player now, slots aside.
        public bool IsOffered(ContractDefinition def, float reputation, IReadOnlyList<ContractInstance> active)
        {
            if (reputation < def.requiredReputation) return false;
            foreach (var c in active)
                if (c.Definition.contractId == def.contractId) return false;
            return true;
        }

        public bool TryBook(ContractDefinition def, double gameTime, float reputation,
                            ContractLedger ledger, int slots, int managerNumber = 0, float commission = 0f) =>
            IsOffered(def, reputation, ledger.ActiveContracts) &&
            ledger.TryAccept(def, gameTime, slots, managerNumber, commission);
    }
}
