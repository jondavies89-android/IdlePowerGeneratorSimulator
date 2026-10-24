using TMPro;
using UnityEngine;
using IdlePowerGen.Utils;

namespace IdlePowerGen.UI
{
    // The dominant Era 1 reading: stored charge, which is both capability and currency.
    // Refreshed 10 times a second, fast enough to watch the flywheel run down and the
    // charge build between 1 Hz sim ticks, and at once on a turn of the crank.
    public class Era1StoredChargeView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _chargeLabel;
        [SerializeField] private TMP_Text _rateLabel;
        [SerializeField] private Controllers.Era1TapController _tapController;

        private const float RefreshInterval = 0.1f;

        private float _sinceRefresh = RefreshInterval;
        private string _lastUnit;
        private Coroutine _punch;

        private void OnEnable()  => EventBus.Subscribe<TapEvent>(OnTap);
        private void OnDisable() => EventBus.Unsubscribe<TapEvent>(OnTap);

        private void OnTap(TapEvent _) => Refresh();

        private void Update()
        {
            _sinceRefresh += Time.unscaledDeltaTime;
            if (_sinceRefresh >= RefreshInterval) Refresh();
        }

        private void Refresh()
        {
            _sinceRefresh = 0f;
            var gm  = Managers.GameManager.Instance;
            var sim = Simulation.SimulationTick.Instance;
            if (gm == null) return;

            // The ledger is credited once a second; add what has been made since, so the
            // reading climbs smoothly instead of in one-second jumps.
            double pending = sim != null ? sim.Grid.ComputePendingJoules(sim.SecondsSinceTick) : 0;
            string charge = NumberFormatter.FormatJoules(gm.Ledger.Joules + pending);
            _chargeLabel.text = charge;
            PunchOnPrefixChange(charge);

            if (_rateLabel == null) return;
            string turn = $"Each turn of the crank: {NumberFormatter.FormatJoules(_tapController.CrankTapValue)}";
            _rateLabel.text = sim != null ? $"{SourcesLine(sim)}{turn}" : turn;
        }

        // "Flywheel 4.63 W · Water wheel 25.0 W", showing only what has been built.
        private static string SourcesLine(Simulation.SimulationTick sim)
        {
            var flywheel = sim.Flywheel;
            var wheel    = sim.WaterWheel;
            bool hasFlywheel = flywheel.CapacityWatts > 0;
            bool hasWheel    = wheel.BaseWatts > 0;
            if (!hasFlywheel && !hasWheel) return string.Empty;

            string fly = hasFlywheel
                ? $"Flywheel {NumberFormatter.FormatWatts(flywheel.SpinAt(sim.SecondsSinceTick) * flywheel.OutputMultiplier)}"
                : null;
            string river = hasWheel ? $"Water wheel {NumberFormatter.FormatWatts(wheel.CurrentOutputWatts)}" : null;
            return (fly != null && river != null ? $"{fly}  ·  {river}" : fly ?? river) + "\n";
        }

        // §6.5 — the prefix (J → kJ) must never change silently.
        private void PunchOnPrefixChange(string formatted)
        {
            string unit = formatted.Substring(formatted.LastIndexOf(' ') + 1);
            if (_lastUnit != null && unit != _lastUnit)
            {
                if (_punch != null) StopCoroutine(_punch);
                _punch = StartCoroutine(UiTween.Punch(_chargeLabel.transform, 1.3f, 0.45f));
            }
            _lastUnit = unit;
        }
    }
}
