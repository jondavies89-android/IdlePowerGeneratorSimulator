using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using IdlePowerGen.Models;
using IdlePowerGen.Utils;

namespace IdlePowerGen.UI
{
    // One upgrade card. The whole card is the button, so the cost label and the body
    // do the same thing (CLAUDE.md tap-target rule).
    public class UpgradeCardView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _nameLabel;
        [SerializeField] private TMP_Text _descriptionLabel;
        [SerializeField] private TMP_Text _effectLabel;
        [SerializeField] private TMP_Text _costLabel;
        [SerializeField] private Button _button;
        [SerializeField] private CanvasGroup _group;

        // Greyed cards stay readable: the player should still see what's coming.
        private const float DimmedAlpha = 0.45f;

        public UpgradeDefinition Definition { get; private set; }
        public string CostText => _costLabel.text;

        private void Awake() => _button.onClick.AddListener(OnClicked);

        // Can be called again with another upgrade: the upgrade tree's detail card is one
        // card that shows whichever node is selected.
        public void Bind(UpgradeDefinition def)
        {
            Definition = def;
            _descriptionLabel.text = def != null ? def.description : string.Empty;
            Refresh();
        }

        private void OnEnable()
        {
            EventBus.Subscribe<CurrencyChangedEvent>(OnCurrencyChanged);
            EventBus.Subscribe<UpgradePurchasedEvent>(OnUpgradePurchased);
            EventBus.Subscribe<GameLoadedEvent>(OnGameLoaded);
            if (Definition != null) Refresh();
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<CurrencyChangedEvent>(OnCurrencyChanged);
            EventBus.Unsubscribe<UpgradePurchasedEvent>(OnUpgradePurchased);
            EventBus.Unsubscribe<GameLoadedEvent>(OnGameLoaded);
        }

        private void OnClicked()
        {
            if (Definition != null) Managers.GameManager.Instance?.Upgrades.Purchase(Definition);
        }

        // Era 1 cards cost joules; Era 2 cards cost pennies and may wait on reputation.
        private void OnCurrencyChanged(CurrencyChangedEvent _) => Refresh();

        private void OnUpgradePurchased(UpgradePurchasedEvent _) => Refresh();
        private void OnGameLoaded(GameLoadedEvent _)             => Refresh();

        public void Refresh()
        {
            var registry = Managers.GameManager.Instance?.Upgrades;
            if (registry == null || Definition == null) return;

            bool prereqsMet = registry.PrerequisitesMet(Definition);
            bool affordable = registry.CanAfford(Definition);
            bool maxed      = registry.IsMaxed(Definition);
            int  level      = registry.Level(Definition.upgradeId);

            bool reputable = Managers.GameManager.Instance.Ledger.Reputation >= Definition.requiredReputation;

            _nameLabel.text   = NameText(Definition, level);
            _costLabel.text   = maxed ? (Definition.isResearch ? "Known" : "Bought") : PriceText(Definition);
            _effectLabel.text = maxed       ? EffectText(Definition, level - 1)
                              : !prereqsMet ? NeedsText(Definition)
                              : !reputable  ? $"Needs {Definition.requiredReputation:0.#} repute"
                              : EffectText(Definition, level);
            _button.interactable = prereqsMet && affordable && !maxed;
            // A bought upgrade reads at full strength: it is an achievement, not a locked door.
            _group.alpha = _button.interactable || maxed ? 1f : DimmedAlpha;
        }

        // The next level's price in whatever it costs: research points, money (pence or
        // pounds by era), or Era 1's joules.
        public static string PriceText(UpgradeDefinition def)
        {
            var registry = Managers.GameManager.Instance.Upgrades;
            if (def.costResearch > 0) return $"{registry.NextCostResearch(def):N0} RP";
            if (def.costPennies > 0)
                return Managers.Money.Format((decimal)registry.NextCostPennies(def), Mathf.Max(1, def.minEra));
            return NumberFormatter.FormatJoules(registry.NextCostJoules(def));
        }

        // Levelled upgrades show how far along they are; one-time ones just their name.
        private static string NameText(UpgradeDefinition def, int level) =>
            def.maxLevel > 1 && level > 0 ? $"{def.displayName}  {level}/{def.maxLevel}" : def.displayName;

        // Plain-English effect line; no "CTV", no "decay".
        private static string EffectText(UpgradeDefinition def, int level)
        {
            string watts = NumberFormatter.FormatWatts(def.effectValue);
            switch (def.effect)
            {
                case UpgradeEffect.MultiplyCrankTapValue: return $"Each turn gives ×{def.effectValue:0.##}";
                case UpgradeEffect.AddCrankJoules:
                    return $"Each turn gives +{NumberFormatter.FormatJoules(def.effectValue * (level + 1))}";
                case UpgradeEffect.AddFlywheelCapacity:
                    return level == 0 ? $"Turns keep a flywheel spinning, up to {watts}"
                                      : $"Flywheel holds +{watts}";
                case UpgradeEffect.MultiplyFlywheelDecay: return $"Flywheel slows {1 - def.effectValue:0%} less";
                case UpgradeEffect.MultiplyCrankOutput:   return $"Crank and flywheel ×{def.effectValue:0.##}, but the flywheel slows faster";
                case UpgradeEffect.AddWaterWheelWatts:
                    return level == 0 && def.maxLevel == 1 ? $"{watts} from the river, even untended"
                                                           : $"Water wheel +{watts}";
                case UpgradeEffect.MultiplyWaterWheelOutput: return $"Water wheel ×{def.effectValue:0.##}";
                case UpgradeEffect.MultiplyAllOutput:     return $"Every machine ×{def.effectValue:0.##}";
                case UpgradeEffect.AddPassiveWatts:       return $"+{watts} without cranking";
                case UpgradeEffect.UnlockGenerator:
                    string opens = def.isResearch ? OpensText(def) : null;
                    return string.IsNullOrEmpty(opens) ? $"New machine: {GeneratorName(def.unlocksId)}" : opens;
                case UpgradeEffect.MultiplyGeneratorOutput:
                    return $"Every {GeneratorName(def.unlocksId).ToLowerInvariant()} ×{def.effectValue:0.##}";
                case UpgradeEffect.AddContractSlot:
                    return $"One more engagement at a time ({Data.Era2Data.BaseContractSlots + level + 1} in all)";
                case UpgradeEffect.MultiplyStreetTrade:   return $"The street pays ×{def.effectValue:0.##} for spare power";
                case UpgradeEffect.UnlockFeature:
                    return def.unlocksId == Data.Era2Data.CoalMerchantFeatureId ? "Coal ordered for you" : OpensText(def);
                case UpgradeEffect.AddContractManager:
                    return $"Books a client for one slot, for {Controllers.ContractDesk.ManagerCommission:0%} of the fee" +
                           (level > 0 ? $" ({level} hired)" : "");
                case UpgradeEffect.AddResearchRate:
                    return $"+{def.effectValue:0.#} research points an hour" + (level > 0 ? $" ({def.effectValue * level:0.#} now)" : "");
                case UpgradeEffect.MultiplyTariff:        return $"Customers pay ×{def.effectValue:0.##} per unit";
                case UpgradeEffect.MultiplyLineLosses:    return $"Every line loses {1 - def.effectValue:0%} less";
                case UpgradeEffect.MultiplyFuelBurn:      return $"Coal units burn {1 - def.effectValue:0%} less";
                case UpgradeEffect.MultiplyServiceTime:   return $"Services and repairs take {1 - def.effectValue:0%} less time";
                case UpgradeEffect.MultiplyFuelPrice:     return $"Coal costs {1 - def.effectValue:0%} less";
                case UpgradeEffect.MultiplyGovernorTime:  return $"Units pick up load {1 / def.effectValue:0.#}× as fast";
                case UpgradeEffect.AddCapacitorStrength:  return $"Each capacitor bank supplies {def.effectValue:0%} more";
                case UpgradeEffect.AddVoltageSupport:     return $"Every city's voltage held {def.effectValue:0.#%} higher";
                case UpgradeEffect.SetMainsTier:
                    int tier = (int)def.effectValue;
                    return tier < Data.Era3Data.Mains.Length ? $"The mains become {Data.Era3Data.Mains[tier].Name.ToLowerInvariant()}" : string.Empty;
                default:                                  return string.Empty;
            }
        }

        // A research node's effect is what it opens: the purchases and machines that need it.
        private static string OpensText(UpgradeDefinition def)
        {
            var gm = Managers.GameManager.Instance;
            var names = gm.UpgradeCatalogue.Where(u => u.requiredUpgradeIds != null && u.requiredUpgradeIds.Contains(def.upgradeId))
                                           .Select(u => u.displayName)
                          .Concat(gm.GeneratorCatalogue.Where(g => g.requiredUpgradeId == def.upgradeId).Select(g => g.displayName))
                          .ToList();
            return names.Count == 0 ? string.Empty : "Opens " + string.Join(", ", names);
        }

        private static string NeedsText(UpgradeDefinition def)
        {
            var gm = Managers.GameManager.Instance;
            var missing = def.requiredUpgradeIds
                .Where(id => !gm.Upgrades.IsPurchased(id))
                .Select(id => gm.UpgradeCatalogue.FirstOrDefault(u => u.upgradeId == id)?.displayName ?? id);
            return "Needs " + string.Join(", ", missing);
        }

        private static string GeneratorName(string typeName)
        {
            var gm = Managers.GameManager.Instance;
            var def = gm?.GeneratorCatalogue.FirstOrDefault(g => g.generatorType.ToString() == typeName);
            return def != null ? def.displayName : typeName;
        }
    }
}
