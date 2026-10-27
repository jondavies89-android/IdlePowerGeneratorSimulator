using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using IdlePowerGen.Utils;

namespace IdlePowerGen.UI
{
    // The strip above every tab of an Era 4–5 screen: the frequency, the load against what is
    // online, and one line on what is coming — the grid short now, the evening peak outrunning
    // the units, a unit overdue that the rest couldn't cover. The cascade warning can't be
    // missed on another tab, and the drop is seen coming before the needle moves.
    // Tapping it opens the desk, where the gauge and the sync panel are.
    public class GridStatusView : MonoBehaviour
    {
        [SerializeField] private Controllers.UnitGridController _grid;
        [SerializeField] private TabBarView _tabs;
        [SerializeField] private Button _button;
        [SerializeField] private Image _background;
        [SerializeField] private TMP_Text _frequencyLabel;
        [SerializeField] private TMP_Text _loadLabel;
        [SerializeField] private Image _fill;
        [SerializeField] private TMP_Text _messageLabel;
        [SerializeField] private string _areaNoun = "feeder";
        [SerializeField] private string _areasTab = "Feeders";
        [SerializeField] private Color _panel        = Color.black;
        [SerializeField] private Color _cautionPanel = new(0.3f, 0.2f, 0.05f);
        [SerializeField] private Color _alarmPanel   = new(0.35f, 0.08f, 0.06f);
        [SerializeField] private Color _healthy = Color.green;
        [SerializeField] private Color _caution = Color.yellow;
        [SerializeField] private Color _alarm   = Color.red;
        [SerializeField] private Color _text    = Color.white;
        [SerializeField] private Color _muted   = Color.gray;

        private enum Level { Healthy, Note, Caution, Alarm }

        private const float RefreshInterval = 0.25f;
        private float _since = RefreshInterval;

        private void Awake()
        {
            if (_button != null) _button.onClick.AddListener(() => _tabs?.Show(0));
        }

        private void Update()
        {
            _since += Time.unscaledDeltaTime;
            if (_since < RefreshInterval) return;
            _since = 0f;
            if (_grid == null || _grid.Grid == null || _grid.BlackStart != null) return;

            var g = _grid.Grid;
            double capacity = g.OnlineCapacity, demand = g.Demand;
            var alarm = _grid.AlarmLevel;
            string arrow = g.Trend < -0.004 ? " ▼" : g.Trend > 0.004 ? " ▲" : "";
            _frequencyLabel.text = g.Dead ? "-- Hz" : $"{g.Frequency:0.00} Hz{arrow}";
            _frequencyLabel.color = alarm == Controllers.UnitGridController.Alarm.Red ? _alarm
                                  : alarm == Controllers.UnitGridController.Alarm.Amber ? _caution : _text;

            _loadLabel.text = $"{NumberFormatter.FormatWatts(demand)} of {NumberFormatter.FormatWatts(capacity)} online";
            double used = capacity > 0 ? demand / capacity : 1;
            _fill.fillAmount = Mathf.Clamp01((float)used);
            _fill.color = used > 1 ? _alarm : used > 0.92 ? _caution : _healthy;

            var (level, message) = Advice(capacity, demand);
            _messageLabel.text = message;
            _messageLabel.color = level switch
            {
                Level.Alarm => _alarm, Level.Caution => _caution, Level.Note => _muted, _ => _healthy,
            };
            _background.color = level == Level.Alarm ? _alarmPanel : level == Level.Caution ? _cautionPanel : _panel;
        }

        // The one thing most worth knowing, worst first.
        private (Level, string) Advice(double capacity, double demand)
        {
            double left = _grid.CascadeSecondsLeft;
            if (left >= 0)
                return (Level.Alarm, $"The grid goes dark in {Mathf.CeilToInt((float)left)} s. " +
                                     (demand > capacity
                                         ? $"{NumberFormatter.FormatWatts(demand - capacity)} short: sync a unit or drop a {_areaNoun}."
                                         : "Enough is off now: hold on while it climbs back."));
            if (demand > capacity * 1.001)
                return (Level.Alarm, $"Short by {NumberFormatter.FormatWatts(demand - capacity)}: the frequency is falling. " +
                                     $"Under 49 Hz the countdown to a blackout starts.");
            if (_grid.SheddingStage > 0)
                return (Level.Caution, $"The relays are shedding {Controllers.UnitGridController.ShedFractions[_grid.SheddingStage]:0%} " +
                                       "of the load to hold the grid. More units online gives it back.");
            // Era 6: too much on the bars, the night trough, starting units in time, the margin.
            if (_grid.EraAdvice() is { } era) return ((Level)era.Level, era.Message);

            var (peak, ahead) = _grid.PeakAhead();
            if (peak > capacity)
            {
                string fix = _grid.ReadyCapacity >= peak ? "Sync a waiting unit before then."
                                                         : $"Build and sync another unit, or drop a {_areaNoun}.";
                return (Level.Caution, $"{When(ahead)} demand reaches {NumberFormatter.FormatWatts(peak)}, " +
                                       $"{NumberFormatter.FormatWatts(peak - capacity)} more than is online. {fix}");
            }

            double reserve = capacity - demand;
            var risky = _grid.OverdueUnits.OrderByDescending(u => u.OutputWatts).FirstOrDefault();
            if (risky != null && risky.OutputWatts > reserve)
                return (Level.Caution, $"Unit {risky.Number} is overdue for service. If it fails, the rest can't carry its " +
                                       $"{NumberFormatter.FormatWatts(risky.OutputWatts)}. Service it from the desk.");
            if (_grid.HasCoalUnits && _grid.CoalBurnPerHour > 0 && _grid.CoalDaysOfCover < 0.5)
                return (Level.Caution, $"Coal for about {_grid.CoalDaysOfCover * 24:0} hours. When it runs out, every coal unit trips. " +
                                       "Order more on the Works tab.");

            int dropped = _grid.Areas.Count(a => a.Connected && a.Shed);
            if (dropped > 0)
                return (Level.Note, $"{dropped} {_areaNoun}{(dropped == 1 ? "" : "s")} dropped. Pick {(dropped == 1 ? "it" : "them")} up " +
                                    $"on the {_areasTab} tab once there's reserve.");
            return (Level.Healthy, $"Reserve {NumberFormatter.FormatWatts(reserve)}. " +
                                   $"Covers the peak of {NumberFormatter.FormatWatts(peak)} {When(ahead).ToLowerInvariant()}.");
        }

        // A game time ahead as the clock will read it, and roughly how long that is to wait.
        private static string When(double hoursAhead)
        {
            if (hoursAhead < 0.25) return "Now";
            var clock = GameClock.Instance;
            double now = clock != null ? clock.GameTimeSeconds / 3600.0 : 0;
            double at = (now + hoursAhead) % 24.0;
            int h = (int)at, m = (int)((at - h) * 60) / 15 * 15;
            double scale = (clock != null ? clock.GameTimeScale : 60f) * DevTime.Multiplier;
            double realMinutes = hoursAhead * 3600.0 / Math.Max(1, scale) / 60.0;
            string wait = realMinutes < 1 ? "under a minute" : $"{Math.Round(realMinutes):0} min";
            return $"At {h:00}:{m:00} (in {wait})";
        }
    }
}
