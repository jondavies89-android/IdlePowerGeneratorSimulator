using TMPro;
using UnityEngine;
using UnityEngine.UI;
using IdlePowerGen.Models;
using IdlePowerGen.Utils;

namespace IdlePowerGen.UI
{
    // One machine row: name, icon, how many you own, and what the next one costs.
    // The whole row is the buy button.
    public class GeneratorRowView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _nameLabel;
        [SerializeField] private TMP_Text _ownedLabel;
        [SerializeField] private TMP_Text _costLabel;
        [SerializeField] private Image _icon;
        [SerializeField] private Button _button;
        [SerializeField] private CanvasGroup _group;

        private const float DimmedAlpha = 0.45f;

        private Controllers.Era2GeneratorController _controller;
        public GeneratorDefinition Definition { get; private set; }

        public void Bind(GeneratorDefinition def, Controllers.Era2GeneratorController controller)
        {
            Definition  = def;
            _controller = controller;
            _nameLabel.text = def.displayName;
            if (def.icon != null) _icon.sprite = def.icon;
            _button.onClick.AddListener(OnClicked);
            Refresh();
        }

        private void OnEnable()
        {
            EventBus.Subscribe<CurrencyChangedEvent>(OnCurrencyChanged);
            EventBus.Subscribe<GeneratorPurchasedEvent>(OnGeneratorPurchased);
            EventBus.Subscribe<GameLoadedEvent>(OnGameLoaded);
            if (Definition != null) Refresh();
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<CurrencyChangedEvent>(OnCurrencyChanged);
            EventBus.Unsubscribe<GeneratorPurchasedEvent>(OnGeneratorPurchased);
            EventBus.Unsubscribe<GameLoadedEvent>(OnGameLoaded);
        }

        private void OnClicked() => _controller.TryBuy(Definition);

        private void OnCurrencyChanged(CurrencyChangedEvent e)
        {
            if (e.Currency == CurrencyType.Joules) Refresh();
        }

        private void OnGeneratorPurchased(GeneratorPurchasedEvent _) => Refresh();
        private void OnGameLoaded(GameLoadedEvent _)                 => Refresh();

        public void Refresh()
        {
            var gm = Managers.GameManager.Instance;
            if (gm == null || Definition == null) return;

            double cost = _controller.NextCost(Definition);
            int owned   = _controller.GetOwned(Definition.generatorType);

            _ownedLabel.text = $"Owned: {owned}  ·  {NumberFormatter.FormatWatts(Definition.basePowerWatts)} each";
            _costLabel.text  = "Build\n" + NumberFormatter.FormatJoules(cost);

            _button.interactable = gm.Ledger.Joules >= cost;
            _group.alpha = _button.interactable ? 1f : DimmedAlpha;
        }
    }
}
