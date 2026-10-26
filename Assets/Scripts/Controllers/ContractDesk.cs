using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using IdlePowerGen.Models;
using IdlePowerGen.Utils;

namespace IdlePowerGen.Controllers
{
    // The contract board of one era (Era 2's demonstration office, then every later era's
    // big clients): which contracts are this era's, how many can run at once, and booking.
    // The contract cards and lists work against this, whichever era owns them.
    public abstract class ContractDesk : MonoBehaviour
    {
        public Economy.ContractBoard Board { get; } = new();

        public abstract int EraNumber { get; }
        protected abstract int BaseSlots { get; }

        protected static Managers.GameManager GM => Managers.GameManager.Instance;

        public static double Now => GameClock.Instance != null ? GameClock.Instance.GameTimeSeconds : 0;

        public bool IsCurrentEra => Managers.EraManager.Instance != null && Managers.EraManager.Instance.Era == EraNumber;

        public IEnumerable<ContractDefinition> Contracts =>
            GM == null ? Enumerable.Empty<ContractDefinition>()
                       : GM.ContractCatalogue.Where(c => c != null && c.minEra == EraNumber);

        public IEnumerable<Economy.ContractInstance> Engagements =>
            GM == null ? Enumerable.Empty<Economy.ContractInstance>()
                       : GM.Contracts.ActiveContracts.Where(c => c.Definition.minEra == EraNumber);

        // Slots from the era's own upgrades only: Era 2's telegraph lines don't carry over.
        public int Slots
        {
            get
            {
                int slots = BaseSlots;
                var gm = GM;
                if (gm == null) return slots;
                foreach (var def in gm.UpgradeCatalogue)
                    if (def.effect == UpgradeEffect.AddContractSlot && Managers.UpgradeEffectApplier.Applies(def))
                        slots += (int)System.Math.Round(def.effectValue) * gm.Upgrades.Level(def.upgradeId);
                return slots;
            }
        }

        public bool HasFreeSlot => GM != null && Engagements.Count() < Slots;

        // Watts the works has promised to its contract clients right now.
        public double CommittedWatts => Engagements.Sum(c => c.Definition.requiredWatts);

        // What the works could give a contract client: Era 2's whole output; from Era 3 what
        // is left after the connected customers, at the customers' end of the lines.
        public abstract double SpareForContracts { get; }

        public bool IsOffered(ContractDefinition def) =>
            IsCurrentEra && GM != null && Board.IsOffered(def, GM.Ledger.Reputation, GM.Contracts.ActiveContracts);

        public bool Book(ContractDefinition def) =>
            IsCurrentEra && GM != null && HasFreeSlot &&
            Board.TryBook(def, Now, GM.Ledger.Reputation, GM.Contracts, GM.Contracts.ActiveContracts.Count + 1);

        // The whole fee, premium included, in pennies.
        public static decimal Pay(ContractDefinition def) =>
            GM == null ? 0m
                       : (decimal)(def.durationHours * def.requiredWatts * GM.ContractBaseRate(def) * def.payRateMultiplier);
    }
}
