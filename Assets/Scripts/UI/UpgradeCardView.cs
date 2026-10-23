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

        public void Bind(UpgradeDefinition def)
        {
            Definition = def;
            _nameLabel.text        = def.displayName;
            _descriptionLabel.text = def.description;
            _costLabel.text        = NumberFormatter.FormatJoules(def.costJoules);
            _button.onClick.AddListener(OnClicked);
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

        private void OnClicked() => Managers.GameManager.Instance?.Upgrades.Purchase(Definition);

        private void OnCurrencyChanged(CurrencyChangedEvent e)
        {
            if (e.Currency == CurrencyType.Joules) Refresh();
        }

        private void OnUpgradePurchased(UpgradePurchasedEvent _) => Refresh();
        private void OnGameLoaded(GameLoadedEvent _)             => Refresh();

        public void Refresh()
        {
            var registry = Managers.GameManager.Instance?.Upgrades;
            if (registry == null || Definition == null) return;

            bool prereqsMet = registry.PrerequisitesMet(Definition);
            bool affordable = registry.CanAfford(Definition);

            _effectLabel.text = prereqsMet ? EffectText(Definition) : NeedsText(Definition);
            _button.interactable = prereqsMet && affordable && !registry.IsPurchased(Definition.upgradeId);
            _group.alpha = _button.interactable ? 1f : DimmedAlpha;
        }

        // Plain-English effect line; no "CTV".
        private static string EffectText(UpgradeDefinition def)
        {
            switch (def.effect)
            {
                case UpgradeEffect.MultiplyCrankTapValue: return $"Each turn gives ×{def.effectValue:0.##}";
                case UpgradeEffect.MultiplyAllOutput:     return $"Every machine ×{def.effectValue:0.##}";
                case UpgradeEffect.AddPassiveWatts:       return $"+{NumberFormatter.FormatWatts(def.effectValue)} without cranking";
                case UpgradeEffect.UnlockGenerator:       return $"New machine: {GeneratorName(def.unlocksId)}";
                default:                                  return string.Empty;
            }
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
