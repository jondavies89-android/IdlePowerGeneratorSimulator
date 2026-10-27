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
        // Era 6: BASE · AGC · STBY, the unit's orders. Absent in Eras 4–5.
        [SerializeField] private GameObject _modes;
        [SerializeField] private Button _base;
        [SerializeField] private Button _agc;
        [SerializeField] private Button _standby;
        [SerializeField] private Color _text    = Color.white;
        [SerializeField] private Color _muted   = Color.gray;
        [SerializeField] private Color _caution = Color.yellow;
        [SerializeField] private Color _alarm   = Color.red;

        private GridUnit _unit;
        private Controllers.Era6NationalGridController National => _grid as Controllers.Era6NationalGridController;

        private void Awake()
        {
            _button.onClick.AddListener(Act);
            if (_modes == null) return;
            _base.onClick.AddListener(() => { National?.SetMode(_unit, GridUnit.UnitMode.Base); Refresh(); });
            _agc.onClick.AddListener(() => { National?.SetMode(_unit, GridUnit.UnitMode.Agc); Refresh(); });
            _standby.onClick.AddListener(() => { National?.StandDown(_unit); Refresh(); });
        }

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
                case UnitState.Standby:      National?.StartUnit(_unit); break;
            }
        }

        private void Refresh()
        {
            var national = National;
            if (_modes != null) _modes.SetActive(national != null && _unit != null && !_unit.Inherited);
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
                _titleLabel.text = national != null ? "The old fleet" : "Unit 0 · the old station";
                _detailLabel.text = national != null
                    ? $"{cap} of older stations, always on AGC, loaded after cheaper new units. It never needs you."
                    : $"{cap}, kept running by the old hands. It never needs you.";
                _detailLabel.color = _muted;
                _actionLabel.text = "";
                _button.interactable = false;
                _group.alpha = 1f;
                return;
            }

            double interval = _unit.Definition.serviceIntervalHours;
            bool overdue = _grid.IsOverdue(_unit);
            string black = _unit.BlackStartCapable ? " · can black-start" : "";
            if (national != null)
            {
                var def = _unit.Definition;
                _titleLabel.text = $"{national.BoardName(_unit)} · {def.displayName}";
                string follows = def.canFollow ? $"ramps {def.rampPerMinute:0.#%} a minute, min {def.minStableShare:0%}"
                                               : "BASE only: a reactor can't follow the load";
                _detailLabel.text = $"{cap} · £{national.RunCost(_unit):0}/MWh to run · {follows} · starts in " +
                                    $"{Controllers.Era6NationalGridController.Hours(def.startHours)} · serviced {_unit.HoursSinceService:0} h ago " +
                                    $"(every {interval:0} h){black}";
                Mode(_base, national.CanSetMode(_unit, GridUnit.UnitMode.Base), _unit.IsOnline && !_unit.Following);
                Mode(_agc, national.CanSetMode(_unit, GridUnit.UnitMode.Agc), _unit.IsOnline && _unit.Following);
                Mode(_standby, national.CanStandDown(_unit), _unit.State == UnitState.Standby);
            }
            else
            {
                _titleLabel.text = $"Unit {_unit.Number} · {_unit.Definition.displayName}";
                _detailLabel.text = $"{cap} · serviced {_unit.HoursSinceService:0} h ago (every {interval:0} h){black}";
            }
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
                case UnitState.Standby:
                    can = National != null && National.CanStart(_unit);
                    _actionLabel.text = _unit.HoursLeft > 0
                        ? $"Tripped · can't restart for {Controllers.Era6NationalGridController.Hours(_unit.HoursLeft)}"
                        : $"Start · on the bars in {Controllers.Era6NationalGridController.Hours(_unit.Definition.startHours)}";
                    _actionLabel.color = _unit.HoursLeft > 0 ? _alarm : _text;
                    break;
                case UnitState.Starting:
                    _actionLabel.text = $"Starting · {Controllers.Era6NationalGridController.Hours(_unit.HoursLeft)} to go";
                    break;
            }
            _button.interactable = can;
            _group.alpha = can ? 1f : 0.75f;
        }

        // A mode button: lit when it is the unit's order, dimmed when it can't be chosen.
        private void Mode(Button b, bool can, bool current)
        {
            b.interactable = can;
            var label = b.GetComponentInChildren<TMP_Text>();
            label.color = current ? _text : _muted;
            label.fontStyle = current ? FontStyles.Bold | FontStyles.Underline : FontStyles.Normal;
        }
    }
}
