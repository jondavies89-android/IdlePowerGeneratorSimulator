using TMPro;
using UnityEngine;
using UnityEngine.UI;
using IdlePowerGen.Simulation;
using IdlePowerGen.Utils;
using UnitState = IdlePowerGen.Simulation.GridUnit.UnitState;

namespace IdlePowerGen.UI
{
    // The unit picked on the board: what it is, how long since its service, and the one
    // thing to do with it now — synchronise it, service it, or get it coal.
    public class UnitCardView : MonoBehaviour
    {
        [SerializeField] private Controllers.UnitGridController _grid;
        [SerializeField] private TMP_Text _titleLabel;
        [SerializeField] private TMP_Text _detailLabel;
        [SerializeField] private TMP_Text _actionLabel;
        [SerializeField] private Button _button;
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private SyncOverlayView _sync;
        [SerializeField] private Color _text    = Color.white;
        [SerializeField] private Color _muted   = Color.gray;
        [SerializeField] private Color _caution = Color.yellow;
        [SerializeField] private Color _alarm   = Color.red;

        private GridUnit _unit;

        private void Awake() => _button.onClick.AddListener(Act);

        public void Bind(GridUnit unit)
        {
            _unit = unit;
            Refresh();
        }

        private void Act()
        {
            if (_unit == null) return;
            switch (_unit.State)
            {
                case UnitState.AwaitingSync: _sync.Open(_unit); break;
                case UnitState.Online:       _grid.Service(_unit); break;
                case UnitState.NoFuel:       _grid.OrderCoal(); break;
            }
        }

        private void Refresh()
        {
            if (_unit == null)
            {
                _titleLabel.text = "No units yet";
                _detailLabel.text = "Build one on the Works tab.";
                _actionLabel.text = "";
                _button.interactable = false;
                return;
            }
            var grid = SimulationTick.Instance?.Grid;
            string cap = NumberFormatter.FormatWatts(_unit.Capacity(grid));
            if (_unit.Inherited)
            {
                _titleLabel.text = "Unit 0 · the old station";
                _detailLabel.text = $"{cap}, kept running by the old hands. It never needs you.";
                _detailLabel.color = _muted;
                _actionLabel.text = "";
                _button.interactable = false;
                _group.alpha = 1f;
                return;
            }

            _titleLabel.text = $"Unit {_unit.Number} · {_unit.Definition.displayName}";
            double interval = _unit.Definition.serviceIntervalHours;
            bool overdue = _grid.IsOverdue(_unit);
            string black = _unit.BlackStartCapable ? " · can black-start" : "";
            _detailLabel.text = $"{cap} · serviced {_unit.HoursSinceService:0} h ago (every {interval:0} h){black}";
            _detailLabel.color = overdue ? _alarm : _unit.HoursSinceService > interval * 0.8 ? _caution : _muted;

            bool can = false;
            _actionLabel.color = _text;
            switch (_unit.State)
            {
                case UnitState.AwaitingSync:
                    _actionLabel.text = _grid.Grid.Dead ? "The grid is dark: black start first" : "Synchronise: match it to the grid";
                    can = !_grid.Grid.Dead;
                    break;
                case UnitState.Online:
                    var cost = _grid.ServiceCost(_unit);
                    _actionLabel.text = $"Service {Managers.Money.Format(cost)} · off for a few hours";
                    _actionLabel.color = overdue ? _alarm : _text;
                    can = _grid.CanService(_unit);
                    break;
                case UnitState.InService:
                    _actionLabel.text = $"In service · {_unit.HoursLeft:0} h to go";
                    break;
                case UnitState.Faulted:
                    _actionLabel.text = $"Under repair · {_unit.HoursLeft:0} h to go";
                    _actionLabel.color = _alarm;
                    break;
                case UnitState.NoFuel:
                    _actionLabel.text = $"Out of coal · order {Managers.Money.Format(_grid.ManualOrderCost)}";
                    _actionLabel.color = _alarm;
                    can = Managers.GameManager.Instance.Ledger.Pennies >= _grid.ManualOrderCost;
                    break;
            }
            _button.interactable = can;
            _group.alpha = can ? 1f : 0.75f;
        }
    }
}
