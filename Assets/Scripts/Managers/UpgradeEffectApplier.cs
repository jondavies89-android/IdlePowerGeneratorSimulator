using System.Collections.Generic;
using UnityEngine;
using IdlePowerGen.Models;
using IdlePowerGen.Utils;

namespace IdlePowerGen.Managers
{
    // Central dispatcher for UpgradeEffect.
    // Subscribes to UpgradePurchasedEvent and applies one level of the effect each time.
    // Crank effects (MultiplyCrankTapValue, AddCrankJoules) are left to Era1TapController,
    // since the value of a turn is per-controller state.
    public class UpgradeEffectApplier : MonoBehaviour
    {
        public static UpgradeEffectApplier Instance { get; private set; }

        private readonly HashSet<string> _unlockedFeatures   = new();
        private readonly HashSet<string> _unlockedGenerators = new();

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;

            // Subscribed in Awake: GameManager.Start loads the save, and that may run
            // before this component's Start.
            EventBus.Subscribe<UpgradePurchasedEvent>(OnUpgradePurchased);
            EventBus.Subscribe<GameLoadedEvent>(OnGameLoaded);
            EventBus.Subscribe<EraAdvancedEvent>(OnEraAdvanced);
        }

        private void Start() => ReapplyFromPurchasedSet();

        private void OnDestroy()
        {
            if (Instance != this) return;
            EventBus.Unsubscribe<UpgradePurchasedEvent>(OnUpgradePurchased);
            EventBus.Unsubscribe<GameLoadedEvent>(OnGameLoaded);
            EventBus.Unsubscribe<EraAdvancedEvent>(OnEraAdvanced);
        }

        private void OnUpgradePurchased(UpgradePurchasedEvent evt) => Apply(evt.Definition);
        private void OnGameLoaded(GameLoadedEvent _)              => ReapplyFromPurchasedSet();
        private void OnEraAdvanced(EraAdvancedEvent _)            => ReapplyFromPurchasedSet();

        // Applies one level.
        private void Apply(UpgradeDefinition def)
        {
            var sim        = Simulation.SimulationTick.Instance;
            var grid       = sim?.Grid;
            var flywheel   = sim?.Flywheel;
            var waterWheel = sim?.WaterWheel;

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

                case UpgradeEffect.AddFlywheelCapacity:
                    flywheel?.AddCapacity(def.effectValue);
                    break;

                case UpgradeEffect.MultiplyFlywheelDecay:
                    flywheel?.MultiplyDecay(def.effectValue);
                    break;

                // Magnets sit in the dynamo that the flywheel and water wheel both drive.
                case UpgradeEffect.MultiplyCrankOutput:
                    flywheel?.MultiplyOutput(def.effectValue);
                    flywheel?.MultiplyDecay(def.secondaryValue);
                    waterWheel?.MultiplyOutput(def.effectValue);
                    break;

                case UpgradeEffect.AddWaterWheelWatts:
                    waterWheel?.AddWatts(def.effectValue);
                    break;

                case UpgradeEffect.MultiplyCrankTapValue:
                case UpgradeEffect.AddCrankJoules:
                    // Era1TapController handles these directly.
                    break;
            }
        }

        // After load: rebuild applied effects from the purchased levels.
        // Required because effects mutate runtime state and the save only stores levels.
        public void ReapplyFromPurchasedSet()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            var sim  = Simulation.SimulationTick.Instance;
            var grid = sim?.Grid;
            if (grid != null)
            {
                grid.GlobalOutputMultiplier = 1.0;
                grid.VirtualPassiveWatts    = 0.0;
            }
            sim?.Flywheel.ResetUpgrades();
            sim?.WaterWheel.ResetUpgrades();
            _unlockedFeatures.Clear();
            _unlockedGenerators.Clear();

            foreach (var def in gm.UpgradeCatalogue)
            {
                int level = gm.Upgrades.Level(def.upgradeId);
                for (int i = 0; i < level; i++) Apply(def);
            }
        }

        // Public queries used by UI / controllers
        public bool IsFeatureUnlocked(string id)   => _unlockedFeatures.Contains(id);
        public bool IsGeneratorUnlocked(string id) => _unlockedGenerators.Contains(id);
    }
}
