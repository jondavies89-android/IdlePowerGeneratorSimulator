using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using IdlePowerGen.Data;
using UnitState = IdlePowerGen.Simulation.GridUnit.UnitState;

namespace IdlePowerGen.UI
{
    // The ALARMS panel (bible Appendix A, Era 6: "RESERVE LOW 340 < 500 MW", "COAL-12 SVC OVD
    // +47 DAYS"): the machine's own list, red on dark red, worst first. All clear, it turns
    // the CRT's green and says so.
    public class Era6AlarmsView : MonoBehaviour
    {
        [SerializeField] private Controllers.Era6NationalGridController _grid;
        [SerializeField] private Image _background;
        [SerializeField] private TMP_Text _title;
        [SerializeField] private TMP_Text[] _left;
        [SerializeField] private TMP_Text[] _right;
        [SerializeField] private Color _clearPanel = Color.black;
        [SerializeField] private Color _alarmPanel = new(0.3f, 0.08f, 0.08f);
        [SerializeField] private Color _healthy = Color.green;
        [SerializeField] private Color _alarm   = Color.red;

        private const float RefreshInterval = 0.5f;
        private float _since = RefreshInterval;
        private readonly List<(string, string)> _alarms = new();

        private void Update()
        {
            _since += Time.unscaledDeltaTime;
            if (_since < RefreshInterval) return;
            _since = 0f;
            if (_grid == null || _grid.Grid == null) return;

            Collect();
            bool clear = _alarms.Count == 0;
            _background.color = clear ? _clearPanel : _alarmPanel;
            if (_background.TryGetComponent<Outline>(out var rim)) rim.effectColor = clear ? _healthy : _alarm;
            _title.color = clear ? _healthy : _alarm;
            for (int i = 0; i < _left.Length; i++)
            {
                bool on = clear ? i == 0 : i < _alarms.Count;
                _left[i].transform.parent.gameObject.SetActive(on);
                if (!on) continue;
                var (l, r) = clear ? ("ALL CLEAR", "") : _alarms[i];
                _left[i].text = Mono(l);
                _right[i].text = Mono(r);
                _left[i].color = _right[i].color = clear ? _healthy : _alarm;
            }
        }

        private static string Mono(string s) => $"<mspace=0.58em>{s}</mspace>";

        private void Collect()
        {
            _alarms.Clear();
            var g = _grid.Grid;
            if (g.Dead) { _alarms.Add(("GRID DARK", "BLACK START")); return; }
            if (g.Frequency > Era6Data.HighHz) _alarms.Add(("FREQ HIGH", $"{g.Frequency:0.00} HZ"));
            else if (g.Frequency < Era4Data.AmberHz) _alarms.Add(("FREQ LOW", $"{g.Frequency:0.00} HZ"));
            if (_grid.ReserveLow)
                _alarms.Add(("RESERVE LOW", $"{_grid.TotalReserveWatts / 1e9:0.0} < {_grid.MarginWatts / 1e9:0.0} GW"));
            foreach (var u in _grid.Units.Where(u => !u.Inherited))
            {
                string name = _grid.BoardName(u);
                if (u.State == UnitState.Faulted) _alarms.Add(($"{name} TRIP", $"{u.HoursLeft:0} H"));
                else if (u.State == UnitState.Standby && u.HoursLeft > 0) _alarms.Add(($"{name} HOLD", $"{u.HoursLeft:0} H"));
                else if (u.State == UnitState.NoFuel) _alarms.Add(($"{name} NO COAL", ""));
                else if (_grid.IsOverdue(u) && u.State != UnitState.InService)
                    _alarms.Add(($"{name} SVC OVD", $"+{(u.HoursSinceService - u.Definition.serviceIntervalHours) / 24:0} DAYS"));
            }
            if (_grid.HasCoalUnits && _grid.CoalBurnPerHour > 0 && _grid.CoalDaysOfCover < 1)
                _alarms.Add(("COAL STOCK LOW", $"{_grid.CoalDaysOfCover * 24:0} H"));
        }
    }
}
