using System.Collections.Generic;
using IdlePowerGen.Models;
using IdlePowerGen.Utils;

namespace IdlePowerGen.Economy
{
    // Tracks purchased upgrades; enforces prerequisites and cost checks.
    public class UpgradeRegistry
    {
        private readonly HashSet<string>       _purchased = new();
        private readonly List<UpgradeDefinition> _catalogue;
        private readonly CurrencyLedger          _ledger;

        public UpgradeRegistry(List<UpgradeDefinition> catalogue, CurrencyLedger ledger)
        {
            _catalogue = catalogue;
            _ledger    = ledger;
        }

        public bool IsPurchased(string upgradeId) => _purchased.Contains(upgradeId);

        public bool CanAfford(UpgradeDefinition def) =>
            _ledger.Joules     >= def.costJoules
            && _ledger.Pennies >= (decimal)def.costPennies
            && _ledger.Reputation >= def.costReputation;

        public bool PrerequisitesMet(UpgradeDefinition def)
        {
            foreach (var req in def.requiredUpgradeIds)
                if (!_purchased.Contains(req)) return false;

            return _ledger.Joules  >= def.requiredStoredJoules
                && _ledger.Pennies >= (decimal)def.requiredPennies;
        }

        public bool IsAvailable(UpgradeDefinition def) =>
            !IsPurchased(def.upgradeId) && CanAfford(def) && PrerequisitesMet(def);

        public bool Purchase(UpgradeDefinition def)
        {
            if (!IsAvailable(def)) return false;

            if (def.costJoules     > 0) _ledger.SpendJoules(def.costJoules);
            if (def.costPennies    > 0) _ledger.SpendPennies((decimal)def.costPennies);
            if (def.costReputation > 0) _ledger.AddReputation(-def.costReputation);

            _purchased.Add(def.upgradeId);
            EventBus.Publish(new UpgradePurchasedEvent(def));
            return true;
        }

        public IReadOnlyCollection<string> PurchasedIds => _purchased;

        public void LoadFromSave(IEnumerable<string> ids)
        {
            _purchased.Clear();
            foreach (var id in ids) _purchased.Add(id);
        }
    }
}
