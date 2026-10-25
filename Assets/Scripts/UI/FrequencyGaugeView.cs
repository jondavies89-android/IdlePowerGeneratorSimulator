using TMPro;
using UnityEngine;
using IdlePowerGen.Utils;

namespace IdlePowerGen.UI
{
    // The frequency needle (bible Appendix A, Era 4: "the frequency needle is now real — it
    // wobbles"): 49 to 51 Hz across the dial, the reading beside it, which way it's heading,
    // and the load. Smoothed between 1 Hz sim ticks so the needle swings rather than jumps.
    public class FrequencyGaugeView : MonoBehaviour
    {
        [SerializeField] private Controllers.UnitGridController _grid;
        [SerializeField] private RectTransform _needle;
        [SerializeField] private TMP_Text _valueLabel;
        [SerializeField] private TMP_Text _trendLabel;
        [SerializeField] private TMP_Text _loadLabel;
        [SerializeField] private Color _healthy = Color.green;
        [SerializeField] private Color _caution = Color.yellow;
        [SerializeField] private Color _alarm   = Color.red;
        [SerializeField] private Color _text    = Color.white;
        [SerializeField] private float _halfSpanHz = 1f;    // 50 ± 1 Hz across the dial
        [SerializeField] private float _halfSweepDegrees = 70f;

        private float _shown = 50f;
        private float _trendAverage;

        private void Update()
        {
            if (_grid == null) return;
            var grid = _grid.Grid;
            float target = grid.Dead ? 49f - _halfSpanHz : (float)grid.Frequency;
            _shown = Mathf.Lerp(_shown, target, 1 - Mathf.Exp(-Time.unscaledDeltaTime * 4f));
            float x = Mathf.Clamp((_shown - 50f) / _halfSpanHz, -1.15f, 1.15f);
            _needle.localRotation = Quaternion.Euler(0, 0, -x * _halfSweepDegrees);

            _trendAverage = Mathf.Lerp(_trendAverage, (float)grid.Trend, 1 - Mathf.Exp(-Time.unscaledDeltaTime * 1.5f));
            if (grid.Dead)
            {
                _valueLabel.text = "--.--";
                _trendLabel.text = "grid dark";
            }
            else
            {
                _valueLabel.text = $"{_shown:0.00}";
                _trendLabel.text = _trendAverage < -0.004f ? "▼ trending down" : _trendAverage > 0.004f ? "▲ trending up" : "steady";
            }
            var alarm = _grid.AlarmLevel;
            _valueLabel.color = alarm == Controllers.UnitGridController.Alarm.Red ? _alarm
                              : alarm == Controllers.UnitGridController.Alarm.Amber ? _caution : _text;
            _trendLabel.color = alarm == Controllers.UnitGridController.Alarm.Normal ? _healthy : _valueLabel.color;
            _loadLabel.text = NumberFormatter.FormatWatts(grid.Load);
        }
    }
}
