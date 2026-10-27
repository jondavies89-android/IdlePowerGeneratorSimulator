using TMPro;
using UnityEngine;
using IdlePowerGen.Utils;

namespace IdlePowerGen.UI
{
    // DEMAND and GENERATION (bible Appendix A, Era 6): cream tiles, human-readable. Under the
    // generation, the gap between them: a deficit pulls the frequency down, a surplus pushes it
    // up, and either means the units can't follow the load fast enough or far enough.
    public class Era6BalanceView : MonoBehaviour
    {
        [SerializeField] private Controllers.Era6NationalGridController _grid;
        [SerializeField] private TMP_Text _demandLabel;
        [SerializeField] private TMP_Text _demandNote;
        [SerializeField] private TMP_Text _generationLabel;
        [SerializeField] private TMP_Text _generationNote;
        [SerializeField] private Color _muted   = Color.gray;
        [SerializeField] private Color _caution = Color.yellow;
        [SerializeField] private Color _alarm   = Color.red;

        private const float RefreshInterval = 0.25f;
        private float _since = RefreshInterval;

        private void Update()
        {
            _since += Time.unscaledDeltaTime;
            if (_since < RefreshInterval) return;
            _since = 0f;
            if (_grid == null || _grid.Grid == null) return;

            var g = _grid.Grid;
            _demandLabel.text = NumberFormatter.FormatWatts(g.Demand);
            _generationLabel.text = NumberFormatter.FormatWatts(g.Generation);
            double fuel = _grid.FuelPoundsPerHour;
            _demandNote.text = fuel > 0 ? $"fuel {Managers.Money.Format(Managers.Money.FromPounds(fuel))} an hour" : "fuel: coal from the yard";
            _demandNote.color = _muted;

            double gap = g.Generation - g.Demand;
            bool small = System.Math.Abs(gap) < System.Math.Max(1e6, g.Demand * 0.002);
            _generationNote.text = small ? "matched to the load"
                                 : gap < 0 ? $"deficit {NumberFormatter.FormatWatts(-gap)}"
                                           : $"surplus {NumberFormatter.FormatWatts(gap)}";
            _generationNote.color = small ? _muted : System.Math.Abs(gap) > g.Demand * 0.02 ? _alarm : _caution;
        }
    }
}
