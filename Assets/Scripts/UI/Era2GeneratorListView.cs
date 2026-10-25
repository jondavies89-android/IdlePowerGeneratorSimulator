using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using IdlePowerGen.Utils;

namespace IdlePowerGen.UI
{
    // The works' machines: the old workshop from Era 1 first (a steady kilowatt), then each
    // Era 2 machine once it is unlocked, and the running costs of the lot.
    public class Era2GeneratorListView : MonoBehaviour
    {
        [SerializeField] private Controllers.Era2GeneratorController _generators;
        [SerializeField] private Controllers.Era2WorksController _works;
        [SerializeField] private Era2GeneratorRowView _rowPrefab;
        [SerializeField] private Transform _content;
        [SerializeField] private TMP_Text _workshopOutputLabel;
        [SerializeField] private TMP_Text _runningCostsLabel;

        private readonly List<Era2GeneratorRowView> _rows = new();

        private const float RefreshInterval = 0.2f;
        private float _since = RefreshInterval;

        private void Start()
        {
            var gm = Managers.GameManager.Instance;
            if (gm == null) return;
            foreach (var def in gm.GeneratorCatalogue.Where(g => g.minEra == Data.Era2Data.EraNumber)
                                                     .OrderBy(g => g.baseCostPennies))
            {
                var row = Instantiate(_rowPrefab, _content);
                row.name = "Generator_" + def.generatorType;
                row.Bind(def, _generators, _works);
                _rows.Add(row);
            }
        }

        private void Update()
        {
            _since += Time.unscaledDeltaTime;
            if (_since < RefreshInterval) return;
            _since = 0f;

            foreach (var row in _rows)
            {
                bool show = _generators.IsUnlocked(row.Definition);
                if (row.gameObject.activeSelf != show) row.gameObject.SetActive(show);
                if (show) row.Refresh();
            }

            var sim = Simulation.SimulationTick.Instance;
            if (sim != null)
                // The Era 1 workshop, carried over as one steady kilowatt (the inherited station).
                _workshopOutputLabel.text = NumberFormatter.FormatWatts(sim.Grid.InheritedWatts);

            double wages = _works.WagesPerHour, coal = _works.CoalBurnPerHour;
            _runningCostsLabel.text = wages + coal <= 0 ? "Nothing to pay out yet."
                : $"Paying out {(wages + coal):0.#} d/hr  ·  wages {wages:0.#}  ·  coal {coal:0.#}";
        }
    }
}
