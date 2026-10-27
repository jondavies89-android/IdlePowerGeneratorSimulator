using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using IdlePowerGen.Simulation;
using IdlePowerGen.Utils;
using UnitState = IdlePowerGen.Simulation.GridUnit.UnitState;

namespace IdlePowerGen.UI
{
    // "Generators on line" (bible Appendix A, Era 4): a monospaced status board, one row a
    // unit — "UNIT 3 HYDRO 80MW   76 ON" — and the reserve beneath. Tapping a row shows that
    // unit on the card below the board.
    public class UnitBoardView : MonoBehaviour
    {
        [SerializeField] private Controllers.UnitGridController _grid;
        [SerializeField] private Button _rowTemplate;   // two labels: Name and State
        [SerializeField] private Transform _content;
        [SerializeField] private TMP_Text _reserveLabel;
        [SerializeField] private UnitCardView _card;
        [SerializeField] private Color _on      = Color.green;
        [SerializeField] private Color _ramp    = Color.yellow;
        [SerializeField] private Color _off     = Color.gray;
        [SerializeField] private Color _ready   = Color.yellow;
        [SerializeField] private Color _fault   = Color.red;
        [SerializeField] private Color _text    = Color.white;
        [SerializeField] private Color _highlight = new(1, 1, 1, 0.06f);
        // Era 5's fleet is many units: the board groups them by kind (bible Appendix A, Era 5).
        [SerializeField] private bool _grouped;

        private readonly List<(Button button, TMP_Text name, TMP_Text state, Image bg)> _rows = new();
        private GridUnit _selected;
        private readonly List<GridUnit> _shown = new();

        private const float RefreshInterval = 0.25f;
        private float _since = RefreshInterval;

        private void Awake() => _rowTemplate.gameObject.SetActive(false);

        private static string Mono(string s) => $"<mspace=0.58em>{s}</mspace>";

        private void Update()
        {
            _since += Time.unscaledDeltaTime;
            if (_since < RefreshInterval) return;
            _since = 0f;
            if (_grid == null) return;

            var grid = SimulationTick.Instance?.Grid;
            var units = _grouped ? _grid.Units.Where(u => u.Inherited || u.State != UnitState.Online).ToList()
                                 : _grid.Units.OrderBy(u => u.Number).ToList();
            var groups = _grouped
                ? _grid.Units.Where(u => !u.Inherited && u.State == UnitState.Online)
                       .GroupBy(u => u.Definition.generatorType).ToList()
                : null;
            int count = units.Count + (groups?.Count ?? 0);

            while (_rows.Count < count)
            {
                var b = Instantiate(_rowTemplate, _content);
                b.gameObject.SetActive(true);
                int index = _rows.Count;
                b.onClick.AddListener(() => Pick(index));
                var labels = b.GetComponentsInChildren<TMP_Text>();
                _rows.Add((b, labels[0], labels[1], b.GetComponent<Image>()));
            }
            _shown.Clear();

            int row = 0;
            if (groups != null)
                foreach (var g in groups)
                {
                    var first = g.First();
                    double cap = g.Sum(u => u.Capacity(grid)), sent = g.Sum(u => u.OutputWatts);
                    SetRow(row++, $"{first.Definition.displayName.ToUpperInvariant()}: {g.Count()} ON",
                           $"{Mw(sent)} / {Mw(cap)}", _on, first);
                }
            var national = _grid as Controllers.Era6NationalGridController;
            foreach (var u in units)
            {
                double cap = u.Capacity(grid);
                string name = _grid.BoardName(u) ?? (u.Inherited ? $"UNIT 0 OLD STATION {Mw(cap)}MW"
                                                                 : $"UNIT {u.Number} {Kind(u)} {Mw(cap)}MW");
                (string state, Color colour) = national != null ? ModeText(u, cap) : StateText(u, cap);
                SetRow(row++, name, state, colour, u);
            }
            for (int i = 0; i < _rows.Count; i++)
                if (_rows[i].button.gameObject.activeSelf != (i < row)) _rows[i].button.gameObject.SetActive(i < row);

            if (national != null)
            {
                // Bible Appendix A, Era 6: the reserve against the biggest unit on the bars.
                _reserveLabel.text = Mono($"RESERVE {Gw(national.TotalReserveWatts)} · MARGIN {Gw(national.MarginWatts)} GW");
                _reserveLabel.color = national.ReserveLow ? _fault : _text;
            }
            else _reserveLabel.text = Mono($"RESERVE {Mw(_grid.ReserveWatts),12} MW");
            if (_selected != null && !_grid.Units.Contains(_selected)) _selected = null;
            if (_card != null) _card.Bind(_selected ?? _grid.NextToSync ?? _grid.Units.FirstOrDefault(u => !u.Inherited));
        }

        private void SetRow(int i, string name, string state, Color colour, GridUnit unit)
        {
            var r = _rows[i];
            r.name.text = Mono(name);
            r.state.text = Mono(state);
            r.state.color = colour;
            r.name.color = unit.State == UnitState.Online || unit.Inherited ? _text : _off;
            r.bg.color = unit == _selected ? _highlight : new Color(0, 0, 0, 0);
            _shown.Add(unit);
        }

        private void Pick(int index)
        {
            if (index < _shown.Count) _selected = _shown[index];
            _since = RefreshInterval;
        }

        private static string Kind(GridUnit u) => u.Definition.generatorType switch
        {
            Models.GeneratorType.CoalUnit => "COAL",
            Models.GeneratorType.HydroSet => "HYDRO",
            Models.GeneratorType.TurboAlternator => "STEAM",
            Models.GeneratorType.RegionalStation => "REGIONAL",
            Models.GeneratorType.LargeCoalUnit => "COAL",
            Models.GeneratorType.OilPeaker => "OIL",
            Models.GeneratorType.HydroScheme => "HYDRO",
            Models.GeneratorType.MagnoxReactor => "NUCLEAR",
            _ => "UNIT",
        };

        public static string Gw(double watts) => $"{watts / 1e9:0.0}";

        // Era 6's dispatch board (bible Appendix A): "660 RMP↑", "1240 BASE", "1680 STBY", "0 HOLD!".
        private (string, Color) ModeText(GridUnit u, double cap)
        {
            switch (u.State)
            {
                case UnitState.Online:
                    // Moving more than half a per cent of its rating a second: still ramping.
                    bool up = u.Rise > cap * 0.005, down = u.Rise < -cap * 0.005;
                    string mode = up ? "RMP↑" : down ? "RMP↓" : u.Inherited || u.Following ? "AGC " : "BASE";
                    return ($"{Gw(u.OutputWatts),6} {mode}", up || down ? _ramp : _on);
                case UnitState.Standby:
                    return u.HoursLeft > 0 ? ($"0 HOLD {u.HoursLeft:0}h", _fault) : ($"{Gw(cap),6} STBY", _off);
                case UnitState.Starting:     return ($"START {Controllers.Era6NationalGridController.Hours(u.HoursLeft)}", _ready);
                case UnitState.AwaitingSync: return ("  0 SYNC", _ready);
                case UnitState.InService:    return ($"0 HOLD! SVC {u.HoursLeft:0}h", _fault);
                case UnitState.Faulted:      return ($"0 TRIP {u.HoursLeft:0}h", _fault);
                default:                     return ("0 NO COAL", _fault);
            }
        }

        public static string Mw(double watts)
        {
            double mw = watts / 1e6;
            return mw >= 10000 ? $"{mw / 1000:0.0}k" : mw >= 100 ? $"{mw:0}" : mw >= 10 ? $"{mw:0}" : $"{mw:0.#}";
        }

        private (string, Color) StateText(GridUnit u, double cap)
        {
            switch (u.State)
            {
                case UnitState.Online:
                    // Picking up or shedding load: the governor is still moving it.
                    bool ramping = cap > 0 && Mathf.Abs((float)(u.OutputWatts / cap) - (float)(_grid.Grid.Load / System.Math.Max(1, _grid.Grid.OnlineCapacity))) > 0.03f;
                    return ($"{Mw(u.OutputWatts),5} {(ramping ? "RMP" : "ON ")}", ramping ? _ramp : _on);
                case UnitState.AwaitingSync: return ("   -- SYNC", _ready);
                case UnitState.InService:    return ($" SERV {u.HoursLeft:0}h", _off);
                case UnitState.Faulted:      return ($"FAULT {u.HoursLeft:0}h", _fault);
                case UnitState.NoFuel:       return ("   NO COAL", _fault);
                default:                     return ("   -- OFF", _off);
            }
        }
    }
}
