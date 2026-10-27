using TMPro;
using UnityEngine;

namespace IdlePowerGen.UI
{
    // The FREQ panel (bible Appendix A, Era 6): green on black, system-generated. The reading,
    // how far it is off fifty, the band it should keep to, and RoCoF, the rate the needle is
    // moving at (from Era 6 the game may name it, in plain terms).
    public class Era6FrequencyView : MonoBehaviour
    {
        [SerializeField] private Controllers.UnitGridController _grid;
        [SerializeField] private TMP_Text _valueLabel;
        [SerializeField] private TMP_Text _offsetLabel;
        [SerializeField] private TMP_Text _rocofLabel;
        [SerializeField] private Color _healthy = Color.green;
        [SerializeField] private Color _caution = Color.yellow;
        [SerializeField] private Color _alarm   = Color.red;

        private const float RefreshInterval = 0.2f;
        private float _since = RefreshInterval;

        private void Update()
        {
            _since += Time.unscaledDeltaTime;
            if (_since < RefreshInterval) return;
            _since = 0f;
            if (_grid == null || _grid.Grid == null) return;

            var g = _grid.Grid;
            if (g.Dead)
            {
                _valueLabel.text = "--.-- Hz";
                _offsetLabel.text = "";
                _rocofLabel.text = "RoCoF --";
                _valueLabel.color = _alarm;
                return;
            }
            double f = g.Frequency, off = f - Simulation.GridDispatch.Nominal;
            _valueLabel.text = $"{f:0.00} Hz";
            _offsetLabel.text = $"{(off < -0.005 ? "▼" : off > 0.005 ? "▲" : "■")} {(off < 0 ? "−" : "+")}{System.Math.Abs(off):0.00}";
            // The sim steps once a second, so the last step's change is per second.
            double rocof = g.Trend;
            _rocofLabel.text = $"RoCoF {(rocof < 0 ? "−" : "+")}{System.Math.Abs(rocof):0.00}/s";
            double dev = System.Math.Abs(off);
            Color c = dev > 0.5 ? _alarm : dev > 0.2 ? _caution : _healthy;
            _valueLabel.color = c;
            _offsetLabel.color = c;
            _rocofLabel.color = System.Math.Abs(rocof) > 0.1 ? _caution : _healthy;
        }
    }
}
