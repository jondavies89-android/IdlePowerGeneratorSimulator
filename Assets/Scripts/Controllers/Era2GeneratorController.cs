using System;
using System.Collections.Generic;
using UnityEngine;
using IdlePowerGen.Models;
using IdlePowerGen.Utils;

namespace IdlePowerGen.Controllers
{
    // Manages generator purchasing and the stackable cost curve for Era 2 (§9.3.1).
    // cost(n) = base × 1.15^n  where n = units already owned.
    public class Era2GeneratorController : MonoBehaviour
    {
        private readonly Dictionary<GeneratorType, int> _owned = new();

        private Economy.CurrencyLedger _ledger;
        private Simulation.PowerGrid   _grid;

        private void Start()
        {
            _ledger = Managers.GameManager.Instance?.Ledger;
            _grid   = Simulation.SimulationTick.Instance?.Grid;
        }

        // Cost of the next unit of this generator type.
        public double NextCost(GeneratorDefinition def)
        {
            int n = _owned.GetValueOrDefault(def.generatorType, 0);
            return def.baseCostJoules * Math.Pow(1.15, n);
        }

        // Returns true if this generator type is available right now
        // (no unlock prerequisite, or the unlocking upgrade has been purchased).
        public bool IsUnlocked(GeneratorDefinition def)
        {
            if (string.IsNullOrEmpty(def.requiredUpgradeId)) return true;
            var gm = Managers.GameManager.Instance;
            return gm != null && gm.Upgrades.IsPurchased(def.requiredUpgradeId);
        }

        // Returns false if the player cannot afford the purchase or it is locked.
        public bool TryBuy(GeneratorDefinition def)
        {
            if (!IsUnlocked(def)) return false;
            double cost = NextCost(def);
            if (_ledger == null || !_ledger.SpendJoules(cost)) return false;

            var instance = new Simulation.GeneratorInstance(def);
            _grid?.AddGenerator(instance);

            _owned[def.generatorType] = _owned.GetValueOrDefault(def.generatorType, 0) + 1;
            EventBus.Publish(new GeneratorPurchasedEvent(def, _owned[def.generatorType]));
            return true;
        }

        public int GetOwned(GeneratorType type) => _owned.GetValueOrDefault(type, 0);

        // Rebuilds runtime generator instances from a saved count dictionary.
        public void LoadFromSave(Dictionary<GeneratorType, int> counts,
                                 List<GeneratorDefinition> catalogue)
        {
            _owned.Clear();
            foreach (var (type, count) in counts)
            {
                _owned[type] = count;
                var def = catalogue.Find(d => d.generatorType == type);
                if (def == null) continue;
                for (int i = 0; i < count; i++)
                    _grid?.AddGenerator(new Simulation.GeneratorInstance(def));
            }
        }
    }
}
