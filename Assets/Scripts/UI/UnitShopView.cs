using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using IdlePowerGen.Models;
using IdlePowerGen.Utils;

namespace IdlePowerGen.UI
{
    // The kinds of unit to build (Eras 4–5), one row each, the whole row the buy button: what
    // it is, how big, how many there are, and the price of the next. A new unit runs up and
    // waits on the board to be synchronised.
    public class UnitShopView : MonoBehaviour
    {
        [SerializeField] private Controllers.UnitGridController _grid;
        [SerializeField] private Button _rowTemplate;  // labels: Name, Detail, Cost
        [SerializeField] private Transform _content;
        [SerializeField] private GameObject _lockedHint;

        private readonly List<(GeneratorDefinition def, Button button, TMP_Text name, TMP_Text detail, TMP_Text cost, CanvasGroup group)> _rows = new();
        private const float RefreshInterval = 0.25f;
        private float _since = RefreshInterval;

        private void Start()
        {
            _rowTemplate.gameObject.SetActive(false);
            foreach (var def in _grid.UnitKinds)
            {
                var b = Instantiate(_rowTemplate, _content);
                b.name = "Unit_" + def.generatorType;
                var d = def;
                b.onClick.AddListener(() => _grid.BuyUnit(d));
                var labels = b.GetComponentsInChildren<TMP_Text>();
                _rows.Add((def, b, labels[0], labels[1], labels[2], b.GetComponent<CanvasGroup>()));
            }
        }

        private void Update()
        {
            _since += Time.unscaledDeltaTime;
            if (_since < RefreshInterval) return;
            _since = 0f;
            if (_grid == null) return;

            bool anyLocked = false;
            var grid = Simulation.SimulationTick.Instance?.Grid;
            foreach (var r in _rows)
            {
                bool unlocked = _grid.IsUnlocked(r.def);
                anyLocked |= !unlocked;
                if (r.button.gameObject.activeSelf != unlocked) r.button.gameObject.SetActive(unlocked);
                if (!unlocked) continue;
                int owned = _grid.Owned(r.def.generatorType);
                r.name.text = owned > 0 ? $"{r.def.displayName}  ×{owned}" : r.def.displayName;
                string extra = r.def.coalTonnesPerMWh > 0 ? "burns coal" : "no coal";
                if (_grid is Controllers.Era6NationalGridController)
                {
                    // Era 6: what it costs to run, how quick it is, and the reactor's catch.
                    double run = r.def.fuelPoundsPerMWh + r.def.coalTonnesPerMWh * Data.Era6Data.CoalPoundsPerTonne;
                    extra = $"£{run:0}/MWh · starts in {Controllers.Era6NationalGridController.Hours(r.def.startHours)} · " +
                            (r.def.canFollow ? $"ramps {r.def.rampPerMinute:0.#%}/min" : "BASE only: the night's load must take it all");
                }
                if (r.def.blackStartCapable) extra += " · can black-start";
                if (r.def.maxUnits > 0) extra += $" · {r.def.maxUnits - owned} sites left";
                r.detail.text = $"{NumberFormatter.FormatWatts(grid != null ? grid.UnitOutputWatts(r.def) : r.def.basePowerWatts)} each · {extra}";
                r.cost.text = _grid.AtLimit(r.def) ? "All built" : $"Build\n{Managers.Money.Format(_grid.NextCost(r.def))}";
                bool can = _grid.CanBuy(r.def);
                r.button.interactable = can;
                r.group.alpha = can ? 1f : 0.5f;
            }
            if (_lockedHint != null && _lockedHint.activeSelf != anyLocked) _lockedHint.SetActive(anyLocked);
        }
    }
}
