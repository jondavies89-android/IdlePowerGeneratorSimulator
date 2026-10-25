using System;
using System.Collections.Generic;
using IdlePowerGen.Models;
using IdlePowerGen.Utils;

namespace IdlePowerGen.Economy
{
    // Tracks the level bought of each upgrade; enforces prerequisites and cost checks.
    // A one-time upgrade is one with maxLevel 1.
    public class UpgradeRegistry
    {
        private readonly Dictionary<string, int> _levels = new();
        private readonly List<UpgradeDefinition>  _catalogue;
        private readonly CurrencyLedger           _ledger;

        public UpgradeRegistry(List<UpgradeDefinition> catalogue, CurrencyLedger ledger)
        {
            _catalogue = catalogue;
            _ledger    = ledger;
        }

        public int  Level(string upgradeId)       => _levels.GetValueOrDefault(upgradeId, 0);
        public bool IsPurchased(string upgradeId) => Level(upgradeId) > 0;
        public bool IsMaxed(UpgradeDefinition def) => Level(def.upgradeId) >= Math.Max(1, def.maxLevel);

        // Cost of the next level: base × growth^(levels owned).
        public double NextCostJoules(UpgradeDefinition def) =>
            def.costJoules * Math.Pow(Growth(def), Level(def.upgradeId));

        public double NextCostPennies(UpgradeDefinition def) =>
            def.costPennies * Math.Pow(Growth(def), Level(def.upgradeId));

        public double NextCostResearch(UpgradeDefinition def) =>
            def.costResearch * Math.Pow(Growth(def), Level(def.upgradeId));

        // Assets made before levels existed deserialise growth as 0.
        private static double Growth(UpgradeDefinition def) => def.costGrowth > 0 ? def.costGrowth : 1.0;

        public bool CanAfford(UpgradeDefinition def) =>
            _ledger.Joules           >= NextCostJoules(def)
            && _ledger.Pennies       >= (decimal)NextCostPennies(def)
            && _ledger.ResearchPoints >= NextCostResearch(def)
            && _ledger.Reputation    >= def.requiredReputation;

        public bool PrerequisitesMet(UpgradeDefinition def)
        {
            foreach (var req in def.requiredUpgradeIds)
                if (!IsPurchased(req)) return false;

            return _ledger.Joules  >= def.requiredStoredJoules
                && _ledger.Pennies >= (decimal)def.requiredPennies;
        }

        public bool IsAvailable(UpgradeDefinition def) =>
            !IsMaxed(def) && CanAfford(def) && PrerequisitesMet(def);

        public bool Purchase(UpgradeDefinition def)
        {
            if (!IsAvailable(def)) return false;

            double joules   = NextCostJoules(def);
            double pennies  = NextCostPennies(def);
            double research = NextCostResearch(def);
            if (joules   > 0) _ledger.SpendJoules(joules);
            if (pennies  > 0) _ledger.SpendPennies((decimal)pennies);
            if (research > 0) _ledger.SpendResearch(research);
            // Reputation is a gate, not a cost — not deducted on purchase.

            int level = Level(def.upgradeId) + 1;
            _levels[def.upgradeId] = level;
            EventBus.Publish(new UpgradePurchasedEvent(def, level));
            return true;
        }

        public IReadOnlyDictionary<string, int> Levels => _levels;

        public void LoadFromSave(IReadOnlyDictionary<string, int> levels)
        {
            _levels.Clear();
            foreach (var (id, level) in levels)
                if (level > 0) _levels[id] = level;
        }

        // One level of each: what a one-time purchase list means.
        public void LoadFromSave(IEnumerable<string> ids)
        {
            _levels.Clear();
            foreach (var id in ids) _levels[id] = 1;
        }
    }
}
