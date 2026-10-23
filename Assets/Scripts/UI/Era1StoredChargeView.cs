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
            double machines = sim != null ? sim.Grid.ComputeOutputWatts() : 0;
            string turn = $"Each turn of the crank: {NumberFormatter.FormatJoules(_tapController.CrankTapValue)}";
            _rateLabel.text = machines > 0
                ? $"Machines: {NumberFormatter.FormatWatts(machines)}\n{turn}"
                : turn;
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
