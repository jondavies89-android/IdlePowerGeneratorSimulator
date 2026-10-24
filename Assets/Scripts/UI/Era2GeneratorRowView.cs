using TMPro;
using UnityEngine;
using UnityEngine.UI;
using IdlePowerGen.Models;
using IdlePowerGen.Utils;

namespace IdlePowerGen.UI
{
    // One kind of machine: how many, what they make, what they cost to run (in coral, bible
    // Appendix A: "passive income is no longer free"), and the price of another. The whole
    // row is the buy button.
    public class Era2GeneratorRowView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _nameLabel;
        [SerializeField] private TMP_Text _runningLabel;
        [SerializeField] private TMP_Text _outputLabel;
        [SerializeField] private TMP_Text _costLabel;
        [SerializeField] private Button _button;
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private Color _runningColour = new(0.88f, 0.44f, 0.31f);
        [SerializeField] private Color _alarmColour   = new(0.90f, 0.22f, 0.21f);

        private const float DimmedAlpha = 0.45f;

        private Controllers.Era2GeneratorController _generators;
        private Controllers.Era2WorksController _works;
        public GeneratorDefinition Definition { get; private set; }

        public void Bind(GeneratorDefinition def, Controllers.Era2GeneratorController generators,
                         Controllers.Era2WorksController works)
        {
            Definition  = def;
            _generators = generators;
            _works      = works;
            _button.onClick.AddListener(() => _generators.TryBuy(Definition));
            Refresh();
        }

        public void Refresh()
        {
            if (Definition == null) return;
            int owned  = _generators.GetOwned(Definition.generatorType);
            int online = _generators.CountOnline(Definition.generatorType);

            _nameLabel.text   = owned > 0 ? $"{Definition.displayName}  ×{owned}" : Definition.displayName;
            _outputLabel.text = NumberFormatter.FormatWatts(_generators.OutputWatts(Definition));
            _costLabel.text   = "Build\n" + NumberFormatter.FormatPennies(_generators.NextCostPennies(Definition));

            _runningLabel.color = _runningColour;
            if (Definition.wagePenniesPerHour > 0)
            {
                _runningLabel.text = $"wage {Definition.wagePenniesPerHour:0.#} d/hr each";
                if (owned > online)
                {
                    _runningLabel.text = "unpaid: they've downed tools";
                    _runningLabel.color = _alarmColour;
                }
            }
            else if (Definition.coalPenniesPerHour > 0)
            {
                float fraction = _works.Hopper.Fraction(_works.CoalBurnCapacityBasis);
                _runningLabel.text = $"coal {Definition.coalPenniesPerHour:0.#} d/hr each · {fraction:0%} hopper";
                if (owned > online)
                {
                    _runningLabel.text = "stopped: the hopper is empty";
                    _runningLabel.color = _alarmColour;
                }
            }
            else
                _runningLabel.text = "no fuel, no wages";

            _button.interactable = _generators.CanAfford(Definition);
            _group.alpha = _button.interactable ? 1f : DimmedAlpha;
        }
    }
}
