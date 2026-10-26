using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdlePowerGen.UI
{
    // The boiler house on Era 3's Town page: the pressure gauge with its green band and the
    // red at the top where the valve lifts, a big Stoke button, what the steam is doing, and
    // what stoking has earned. Read every frame so the gauge moves smoothly as it falls.
    public class Era3BoilerView : MonoBehaviour
    {
        [SerializeField] private Controllers.Era3TownController _town;
        [SerializeField] private Image _fill;
        [SerializeField] private TMP_Text _statusLabel;
        [SerializeField] private TMP_Text _earnedLabel;
        [SerializeField] private Button _stoke;
        [SerializeField] private TMP_Text _stokeLabel;
        [SerializeField] private Color _healthy = Color.green;
        [SerializeField] private Color _caution = Color.yellow;
        [SerializeField] private Color _alarm   = Color.red;
        [SerializeField] private Color _muted   = Color.gray;
        [SerializeField] private Color _cold    = Color.gray;

        private Coroutine _punch;

        // On the press, so fast stoking keeps up with the finger.
        private void Awake() => PressButton.On(_stoke).Pressed += Stoke;

        private void Stoke()
        {
            if (!_town.Stoke()) return;
            if (_punch != null) StopCoroutine(_punch);
            _punch = StartCoroutine(UiTween.Punch(_stoke.transform, 1.05f, 0.18f));
        }

        private void Update()
        {
            if (_town == null) return;
            var b = _town.Boiler;
            _fill.fillAmount = b.Pressure;
            _fill.color = b.ValveLifted ? _alarm : b.InGreen ? _healthy
                        : b.Pressure > Data.Era3Data.GreenHigh ? _caution : _cold;

            int era = _town.EraNumber;
            string bonus = Managers.Money.Format((decimal)_town.StokeBonusPerHour, era);
            if (b.ValveLifted)
                Status($"The safety valve has lifted! Stand clear… {Mathf.CeilToInt(b.LockedSeconds)} s", _alarm);
            else if (b.InGreen)
                Status($"Steam in the green: the works earns twice as much, +{bonus} an hour.", _healthy);
            else if (b.Pressure > Data.Era3Data.GreenHigh)
                Status("Too much steam. Ease off, or the safety valve will lift.", _caution);
            else if (b.Pressure < 0.05f)
                Status($"The boilers are banked. Stoke them into the green for +{bonus} an hour.", _muted);
            else
                Status("Raising steam. Keep stoking into the green.", _muted);

            _stoke.interactable = !b.ValveLifted;
            _stokeLabel.text = b.ValveLifted ? "Valve lifted" : "Stoke the boilers";
            _earnedLabel.text = _town.StokeEarned > 0
                ? $"Stoking has earned {Managers.Money.Format(_town.StokeEarned, era)} this session"
                : "";
        }

        private void Status(string text, Color colour)
        {
            _statusLabel.text = text;
            _statusLabel.color = colour;
        }
    }
}
