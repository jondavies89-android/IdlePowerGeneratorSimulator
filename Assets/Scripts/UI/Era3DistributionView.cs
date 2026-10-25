using System.Linq;
using TMPro;
using UnityEngine;
using IdlePowerGen.Data;
using IdlePowerGen.Utils;

namespace IdlePowerGen.UI
{
    // Distribution (bible Appendix A, Era 3): "Generated 3.6 MW → losses 14 % → 3.1 MW
    // delivered", and a line of advice under it. The loss share is the era's one new number.
    public class Era3DistributionView : MonoBehaviour
    {
        [SerializeField] private Controllers.Era3TownController _town;
        [SerializeField] private TMP_Text _generatedLabel;
        [SerializeField] private TMP_Text _lossLabel;
        [SerializeField] private TMP_Text _deliveredLabel;
        [SerializeField] private TMP_Text _hintLabel;
        [SerializeField] private Color _healthy = new(0.44f, 0.75f, 0.35f);
        [SerializeField] private Color _caution = new(0.94f, 0.63f, 0.19f);
        [SerializeField] private Color _alarm   = new(0.88f, 0.35f, 0.29f);
        [SerializeField] private Color _muted   = Color.gray;

        private const float RefreshInterval = 0.2f;
        private float _since = RefreshInterval;

        private void Update()
        {
            _since += Time.unscaledDeltaTime;
            if (_since < RefreshInterval) return;
            _since = 0f;
            if (_town == null) return;

            double loss = _town.LossFraction;
            _generatedLabel.text = NumberFormatter.FormatWatts(_town.GeneratedWatts);
            _lossLabel.text = $"→ losses {loss:0%} →";
            _lossLabel.color = loss >= 0.15 ? _alarm : loss >= 0.08 ? _caution : _healthy;
            _deliveredLabel.text = NumberFormatter.FormatWatts(_town.DeliveredWatts);
            _hintLabel.text = Hint(loss);
        }

        private string Hint(double loss)
        {
            var share = _town.LastShare;
            if (share.Demand > 0 && share.Scale < 0.97)
                return $"short of power: the lamps are dim at {share.Scale:0%}. Build more machines.";
            double capacity = Simulation.SimulationTick.Instance?.Grid.TotalOutputWatts ?? 0;
            if (_town.GeneratedWatts < capacity * 0.6 && _town.Areas.Any(a => !a.Connected))
                return "machines idle: there's power to spare. Light another district.";
            if (loss >= 0.10)
                return _town.MainsTier < 2 ? "copper lines run hot at peak — consider higher voltage transmission"
                                           : "the long runs lose most: heavier copper, or higher voltage";
            return $"{_town.Mains.Name}";
        }
    }
}
