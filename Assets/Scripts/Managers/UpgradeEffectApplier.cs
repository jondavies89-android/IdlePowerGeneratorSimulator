using System.Collections.Generic;
using UnityEngine;
using IdlePowerGen.Models;
using IdlePowerGen.Utils;

namespace IdlePowerGen.Managers
{
    // Central dispatcher for UpgradeEffect.
    // Subscribes to UpgradePurchasedEvent and applies the effect globally.
    // MultiplyCrankTapValue is intentionally left to Era1TapController, since CTV
    // is per-controller state.
    public class UpgradeEffectApplier : MonoBehaviour
    {
        public static UpgradeEffectApplier Instance { get; private set; }

        private readonly HashSet<string> _unlockedFeatures   = new();
        private readonly HashSet<string> _unlockedGenerators = new();

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            EventBus.Subscribe<UpgradePurchasedEvent>(OnUpgradePurchased);
            // Re-apply effects after save load
            EventBus.Subscribe<EraAdvancedEvent>(_ => ReapplyFromPurchasedSet());
            ReapplyFromPurchasedSet();
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<UpgradePurchasedEvent>(OnUpgradePurchased);
        }

        private void OnUpgradePurchased(UpgradePurchasedEvent evt) => Apply(evt.Definition);

        private void Apply(UpgradeDefinition def)
        {
            var grid = Simulation.SimulationTick.Instance?.Grid;

            switch (def.effect)
            {
                case UpgradeEffect.MultiplyAllOutput:
                    if (grid != null) grid.GlobalOutputMultiplier *= def.effectValue;
                    break;

                case UpgradeEffect.AddPassiveWatts:
                    if (grid != null) grid.VirtualPassiveWatts += def.effectValue;
                    break;

                case UpgradeEffect.UnlockGenerator:
                    if (!string.IsNullOrEmpty(def.unlocksId))
                        _unlockedGenerators.Add(def.unlocksId);
                    break;

                case UpgradeEffect.UnlockFeature:
                    if (!string.IsNullOrEmpty(def.unlocksId))
                        _unlockedFeatures.Add(def.unlocksId);
                    break;

                case UpgradeEffect.MultiplyCrankTapValue:
                    // Era1TapController handles this directly.
                    break;
            }
        }

        // After load: rebuild applied effects from the purchased-upgrade set.
        // Required because effects mutate runtime state and the save only stores upgrade IDs.
        public void ReapplyFromPurchasedSet()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            var grid = Simulation.SimulationTick.Instance?.Grid;
            if (grid != null)
            {
                grid.GlobalOutputMultiplier = 1.0;
                grid.VirtualPassiveWatts    = 0.0;
            }
            _unlockedFeatures.Clear();
            _unlockedGenerators.Clear();

            foreach (var id in gm.Upgrades.PurchasedIds)
            {
                foreach (var def in gm.UpgradeCatalogue)
                {
                    if (def.upgradeId == id) { Apply(def); break; }
                }
            }
        }

        // Public queries used by UI / controllers
        public bool IsFeatureUnlocked(string id)   => _unlockedFeatures.Contains(id);
        public bool IsGeneratorUnlocked(string id) => _unlockedGenerators.Contains(id);
    }
}
