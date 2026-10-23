using TMPro;
using UnityEngine;
using IdlePowerGen.Utils;

namespace IdlePowerGen.UI
{
    // The dominant Era 1 reading: stored charge, which is both capability and currency.
    // Refreshed every UI frame (60 Hz) so taps show instantly between 1 Hz sim ticks.
    public class Era1StoredChargeView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _chargeLabel;
        [SerializeField] private TMP_Text _rateLabel;
        [SerializeField] private Controllers.Era1TapController _tapController;

        private string _lastUnit;
        private Coroutine _punch;

        private void Update()
        {
            var gm  = Managers.GameManager.Instance;
            var sim = Simulation.SimulationTick.Instance;
            if (gm == null) return;

            string charge = NumberFormatter.FormatJoules(gm.Ledger.Joules);
            _chargeLabel.text = charge;
            PunchOnPrefixChange(charge);

            if (_rateLabel == null) return;
            string turn = $"Each turn of the crank: {NumberFormatter.FormatJoules(_tapController.CrankTapValue)}";
            var flywheel = sim != null ? sim.Flywheel : null;
            if (flywheel == null || flywheel.MaxSpinWatts <= 0)
            {
                _rateLabel.text = turn;
                return;
            }
            // Spin decays continuously but the sim ticks at 1 Hz; project it forward so the
            // reading runs down smoothly instead of stepping once a second.
            double watts = flywheel.SpinAt(sim.SecondsSinceTick) * flywheel.OutputMultiplier;
            _rateLabel.text = $"Flywheel: {NumberFormatter.FormatWatts(watts)}\n{turn}";
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
