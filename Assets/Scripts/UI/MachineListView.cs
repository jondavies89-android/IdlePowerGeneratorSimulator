using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using IdlePowerGen.Utils;

namespace IdlePowerGen.UI
{
    // An era's stackable machines (Era 3): the inherited station first, then each machine
    // once its research is done. The rows are Era 2's, in the era's look.
    public class MachineListView : MonoBehaviour
    {
        [SerializeField] private Controllers.Era2GeneratorController _generators;
        [SerializeField] private Era2GeneratorRowView _rowPrefab;
        [SerializeField] private Transform _content;
        [SerializeField] private TMP_Text _inheritedLabel;
        [SerializeField] private TMP_Text _totalLabel;
        [SerializeField] private GameObject _lockedHint;
        [SerializeField] private int _era = 3;

        private readonly List<Era2GeneratorRowView> _rows = new();

        private const float RefreshInterval = 0.2f;
        private float _since = RefreshInterval;

        private void Start()
        {
            var gm = Managers.GameManager.Instance;
            if (gm == null) return;
            foreach (var def in gm.GeneratorCatalogue.Where(g => g.minEra == _era).OrderBy(g => g.baseCostPennies))
            {
                var row = Instantiate(_rowPrefab, _content);
                row.name = "Generator_" + def.generatorType;
                row.Bind(def, _generators, null);
                _rows.Add(row);
            }
        }

        private void Update()
        {
            _since += Time.unscaledDeltaTime;
            if (_since < RefreshInterval) return;
            _since = 0f;

            bool anyLocked = false;
            foreach (var row in _rows)
            {
                bool show = _generators.IsUnlocked(row.Definition);
                anyLocked |= !show;
                if (row.gameObject.activeSelf != show) row.gameObject.SetActive(show);
                if (show) row.Refresh();
            }
            if (_lockedHint != null && _lockedHint.activeSelf != anyLocked) _lockedHint.SetActive(anyLocked);

            var grid = Simulation.SimulationTick.Instance?.Grid;
            if (grid == null) return;
            _inheritedLabel.text = NumberFormatter.FormatWatts(grid.InheritedWatts);
            if (_totalLabel != null) _totalLabel.text = $"Station output {NumberFormatter.FormatWatts(grid.TotalOutputWatts)}";
        }
    }
}
