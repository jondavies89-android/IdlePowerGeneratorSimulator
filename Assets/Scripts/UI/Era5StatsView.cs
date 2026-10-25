using TMPro;
using UnityEngine;
using IdlePowerGen.Data;
using IdlePowerGen.Utils;

namespace IdlePowerGen.UI
{
    // Demand · Freq · PF · V (bible Appendix A, Era 5): power factor and voltage join the
    // headline stats. PF is amber under 0.95 (bible §4.5's target) and red under 0.85;
    // voltage is the lowest city's.
    public class Era5StatsView : MonoBehaviour
    {
        [SerializeField] private Controllers.Era5DispatchController _grid;
        [SerializeField] private TMP_Text _demandLabel;
        [SerializeField] private TMP_Text _frequencyLabel;
        [SerializeField] private TMP_Text _pfLabel;
        [SerializeField] private TMP_Text _voltageLabel;
        [SerializeField] private Color _text    = Color.black;
        [SerializeField] private Color _caution = new(0.75f, 0.4f, 0.1f);
        [SerializeField] private Color _alarm   = Color.red;

        private const float RefreshInterval = 0.2f;
        private float _since = RefreshInterval;

        private void Update()
        {
            _since += Time.unscaledDeltaTime;
            if (_since < RefreshInterval) return;
            _since = 0f;
            if (_grid == null) return;

            _demandLabel.text = NumberFormatter.FormatWatts(_grid.Grid.Load);
            _frequencyLabel.text = _grid.Grid.Dead ? "--.--" : $"{_grid.Frequency:0.00}";
            _frequencyLabel.color = _grid.AlarmLevel switch
            {
                Controllers.UnitGridController.Alarm.Red => _alarm,
                Controllers.UnitGridController.Alarm.Amber => _caution,
                _ => _text,
            };
            double pf = _grid.SystemPowerFactor;
            _pfLabel.text = $"{pf:0.00}";
            _pfLabel.color = pf >= Era5Data.TargetPowerFactor ? _text : pf >= 0.85 ? _caution : _alarm;
            double kv = _grid.WorstKilovolts;
            _voltageLabel.text = $"{kv:0.0}";
            double v = kv / Era5Data.NominalKilovolts;
            _voltageLabel.color = v >= Era5Data.GoodVoltage ? _text : v >= Era5Data.PoorVoltage ? _caution : _alarm;
        }
    }
}
