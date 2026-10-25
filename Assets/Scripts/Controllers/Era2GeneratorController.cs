using System;
using System.Collections.Generic;
using UnityEngine;
using IdlePowerGen.Models;
using IdlePowerGen.Utils;

namespace IdlePowerGen.Controllers
{
    // Manages generator purchasing and the stackable cost curve for Era 2 (§9.3.1).
    // cost(n) = base × 1.15^n  where n = units already owned. Machines are paid for in
    // pennies (bible §9.2: infrastructure costs cash); a definition with no penny price
    // falls back to joules.
    public class Era2GeneratorController : MonoBehaviour
    {
        public const double CostGrowth = 1.15;

        private readonly Dictionary<GeneratorType, int> _owned = new();

        // Looked up on use rather than cached in Start: the save loads during
        // GameManager.Start, which can run before this component's Start.
        private Economy.CurrencyLedger Ledger => Managers.GameManager.Instance?.Ledger;
        private Simulation.PowerGrid   Grid   => Simulation.SimulationTick.Instance?.Grid;

        private static bool PaidInPennies(GeneratorDefinition def) => def.baseCostPennies > 0;

        private void Awake()  => EventBus.Subscribe<EraAdvancedEvent>(OnEraAdvanced);
        private void OnDestroy() => EventBus.Unsubscribe<EraAdvancedEvent>(OnEraAdvanced);

        // The old era's machines become part of the inherited station (PowerGrid.InheritedWatts).
        private void OnEraAdvanced(EraAdvancedEvent e)
        {
            if (e.ToEra >= 3) RetireBefore(e.ToEra);
        }

        public void RetireBefore(int era)
        {
            var grid = Grid;
            if (grid != null)
                foreach (var g in new List<Simulation.GeneratorInstance>(grid.Generators))
                    if (g.Definition.minEra < era) grid.RemoveGenerator(g);

            var gm = Managers.GameManager.Instance;
            if (gm == null) return;
            foreach (var def in gm.GeneratorCatalogue)
                if (def.minEra < era) _owned.Remove(def.generatorType);
        }

        // Cost of the next unit of this generator type, in its own currency.
        public double NextCost(GeneratorDefinition def)
        {
            int n = _owned.GetValueOrDefault(def.generatorType, 0);
            double baseCost = PaidInPennies(def) ? def.baseCostPennies : def.baseCostJoules;
            return baseCost * Math.Pow(CostGrowth, n);
        }

        // Pennies are whole coins at the till.
        public decimal NextCostPennies(GeneratorDefinition def) => (decimal)Math.Ceiling(NextCost(def));

        public bool CanAfford(GeneratorDefinition def)
        {
            var ledger = Ledger;
            if (ledger == null) return false;
            return PaidInPennies(def) ? ledger.Pennies >= NextCostPennies(def) : ledger.Joules >= NextCost(def);
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
            if (!IsUnlocked(def) || Ledger == null) return false;
            bool paid = PaidInPennies(def) ? Ledger.SpendPennies(NextCostPennies(def))
                                           : Ledger.SpendJoules(NextCost(def));
            if (!paid) return false;

            Grid?.AddGenerator(new Simulation.GeneratorInstance(def));

            _owned[def.generatorType] = _owned.GetValueOrDefault(def.generatorType, 0) + 1;
            EventBus.Publish(new GeneratorPurchasedEvent(def, _owned[def.generatorType]));
            return true;
        }

        public int GetOwned(GeneratorType type) => _owned.GetValueOrDefault(type, 0);

        // Units of this type working right now (not stopped for coal or wages).
        public int CountOnline(GeneratorType type)
        {
            int n = 0;
            var grid = Grid;
            if (grid == null) return 0;
            foreach (var g in grid.Generators)
                if (g.Definition.generatorType == type && g.IsOnline) n++;
            return n;
        }

        // Watts all units of this type are delivering now.
        public double OutputWatts(GeneratorDefinition def)
        {
            var grid = Grid;
            return grid == null ? 0 : CountOnline(def.generatorType) * grid.UnitOutputWatts(def);
        }

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
                    Grid?.AddGenerator(new Simulation.GeneratorInstance(def));
            }
        }
    }
}
