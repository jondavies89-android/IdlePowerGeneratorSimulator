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
    //
    // Only the current era's purchases apply, plus research from any era: a multiplier
    // bought for Era 2's engines has no business multiplying Era 3's turbines, and the old
    // works it improved are now the inherited station, which no multiplier touches.
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

        private void OnUpgradePurchased(UpgradePurchasedEvent evt)
        {
            if (Applies(evt.Definition)) Apply(evt.Definition);
        }

        private void OnGameLoaded(GameLoadedEvent _)              => ReapplyFromPurchasedSet();
        private void OnEraAdvanced(EraAdvancedEvent _)            => ReapplyFromPurchasedSet();

        private static int CurrentEra => EraManager.Instance != null ? EraManager.Instance.Era : 1;

        public static bool Applies(UpgradeDefinition def) =>
            def.isResearch || Mathf.Max(1, def.minEra) >= CurrentEra;

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

                // Magnets sit in the crank's dynamo; the water wheel has its own drive.
                // (Era1TapController applies the same multiplier to each turn.)
                case UpgradeEffect.MultiplyCrankOutput:
                    flywheel?.MultiplyOutput(def.effectValue);
                    flywheel?.MultiplyDecay(def.secondaryValue);
                    break;

                case UpgradeEffect.AddWaterWheelWatts:
                    waterWheel?.AddWatts(def.effectValue);
                    break;

                case UpgradeEffect.MultiplyWaterWheelOutput:
                    waterWheel?.MultiplyOutput(def.effectValue);
                    break;

                case UpgradeEffect.MultiplyGeneratorOutput:
                    if (grid != null && System.Enum.TryParse<GeneratorType>(def.unlocksId, out var type))
                        grid.MultiplyType(type, def.effectValue);
                    break;

                case UpgradeEffect.MultiplyCrankTapValue:
                case UpgradeEffect.AddCrankJoules:
                    // Era1TapController handles these directly.
                    break;

                case UpgradeEffect.AddContractSlot:
                case UpgradeEffect.AddContractManager:
                    // The era's contract desk counts slots and managers from the levels owned.
                    break;

                case UpgradeEffect.MultiplyStreetTrade:
                    StreetTradeMultiplier *= def.effectValue;
                    break;

                case UpgradeEffect.AddResearchRate:
                    ResearchPerHour += def.effectValue;
                    break;

                case UpgradeEffect.MultiplyTariff:
                    TariffMultiplier *= def.effectValue;
                    break;

                case UpgradeEffect.MultiplyLineLosses:
                    LineLossMultiplier *= def.effectValue;
                    break;

                case UpgradeEffect.SetMainsTier:
                    MainsTier = Mathf.Max(MainsTier, (int)def.effectValue);
                    break;

                case UpgradeEffect.MultiplyFuelBurn:     FuelBurnMultiplier     *= def.effectValue; break;
                case UpgradeEffect.MultiplyServiceTime:  ServiceTimeMultiplier  *= def.effectValue; break;
                case UpgradeEffect.MultiplyFuelPrice:    FuelPriceMultiplier    *= def.effectValue; break;
                case UpgradeEffect.MultiplyGovernorTime: GovernorTimeMultiplier *= def.effectValue; break;
                case UpgradeEffect.AddCapacitorStrength: CapacitorStrengthBonus += def.effectValue; break;
                case UpgradeEffect.AddVoltageSupport:    VoltageSupport         += def.effectValue; break;
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
                grid.ClearTypeMultipliers();
            }
            sim?.Flywheel.ResetUpgrades();
            sim?.WaterWheel.ResetUpgrades();
            _unlockedFeatures.Clear();
            _unlockedGenerators.Clear();
            StreetTradeMultiplier = 1.0;
            ResearchPerHour       = 0.0;
            TariffMultiplier      = 1.0;
            LineLossMultiplier    = 1.0;
            MainsTier             = 0;
            FuelBurnMultiplier = ServiceTimeMultiplier = FuelPriceMultiplier = GovernorTimeMultiplier = 1.0;
            CapacitorStrengthBonus = VoltageSupport = 0.0;

            foreach (var def in gm.UpgradeCatalogue)
            {
                if (!Applies(def)) continue;
                int level = gm.Upgrades.Level(def.upgradeId);
                for (int i = 0; i < level; i++) Apply(def);
            }
        }

        // Public queries used by UI / controllers
        public bool IsFeatureUnlocked(string id)   => _unlockedFeatures.Contains(id);
        public bool IsGeneratorUnlocked(string id) => _unlockedGenerators.Contains(id);

        // Applied to the street's price for spare power.
        public double StreetTradeMultiplier { get; private set; } = 1.0;

        // Research points per game hour from the current era's R&D buildings.
        public double ResearchPerHour { get; private set; }

        // On what every connected load area pays per unit delivered (Era 3 on).
        public double TariffMultiplier { get; private set; } = 1.0;

        // On every line's losses (Era 3 on).
        public double LineLossMultiplier { get; private set; } = 1.0;

        // How far the current era's mains have come (Era 3: 0 DC, 1 three-wire, 2 AC, 3 HV AC).
        public int MainsTier { get; private set; }

        // Eras 4–5: running the units.
        public double FuelBurnMultiplier     { get; private set; } = 1.0;
        public double ServiceTimeMultiplier  { get; private set; } = 1.0;
        public double FuelPriceMultiplier    { get; private set; } = 1.0;
        public double GovernorTimeMultiplier { get; private set; } = 1.0;

        // Era 5: reactive power.
        public double CapacitorStrengthBonus { get; private set; }
        public double VoltageSupport         { get; private set; }
    }
}
