using TMPro;
using UnityEngine;
using UnityEngine.UI;
using IdlePowerGen.Simulation;
using IdlePowerGen.Utils;

namespace IdlePowerGen.UI
{
    // The synchroscope (bible Appendix A, Era 4: the era's defining minigame moment). The
    // needle turns at the slip between the incoming unit and the grid; Slower and Faster nudge
    // the unit's governor; CLOSE shuts the breaker. Close it near twelve o'clock with the
    // needle barely turning. Real time: the grid's frequency moves under it as it would.
    public class SyncOverlayView : MonoBehaviour
    {
        [SerializeField] private Controllers.UnitGridController _grid;
        [SerializeField] private GameObject _panel;
        [SerializeField] private TMP_Text _titleLabel;
        [SerializeField] private RectTransform _needle;
        [SerializeField] private TMP_Text _slipLabel;
        [SerializeField] private TMP_Text _hintLabel;
        [SerializeField] private Button _slower;
        [SerializeField] private Button _faster;
        [SerializeField] private Button _close;
        [SerializeField] private Button _later;
        [SerializeField] private Image _zone;   // the clean-close arc round twelve o'clock
        [SerializeField] private Color _healthy = Color.green;
        [SerializeField] private Color _caution = Color.yellow;
        [SerializeField] private Color _alarm   = Color.red;

        private GridUnit _unit;
        private Synchroscope _scope;
        // Black start's phase 4 uses the same dial against the island.
        private Controllers.BlackStartSession _blackStart;
        private float _resultTimer;

        public bool IsOpen => _panel.activeSelf;
        public Synchroscope Scope => _scope;

        private void Awake()
        {
            _panel.SetActive(false);
            _slower.onClick.AddListener(() => _scope?.Slower());
            _faster.onClick.AddListener(() => _scope?.Faster());
            _close.onClick.AddListener(CloseBreaker);
            _later.onClick.AddListener(() => { if (_blackStart == null) _panel.SetActive(false); });
        }

        public void Open(GridUnit unit)
        {
            _scope = _grid.BeginSync(unit);
            if (_scope == null) return;
            _unit = unit;
            _blackStart = null;
            _titleLabel.text = $"Synchronise Unit {unit.Number} · {unit.Definition.displayName}";
            Show();
        }

        public void OpenForBlackStart(Controllers.BlackStartSession session)
        {
            _blackStart = session;
            _scope = session.Scope;
            _unit = session.SecondUnit;
            _titleLabel.text = $"Synchronise Unit {_unit.Number} to the island";
            Show();
        }

        private void Show()
        {
            _resultTimer = 0;
            // Trained operators (Era 6) close cleanly over a wider arc.
            if (_zone != null)
            {
                float half = (float)(Synchroscope.CleanPhaseDegrees * _scope.Forgiveness);
                _zone.fillAmount = 2 * half / 360f;
                _zone.rectTransform.localRotation = Quaternion.Euler(0, 0, half);
            }
            _hintLabel.text = "Slow the needle, then close as it reaches twelve o'clock.";
            _close.interactable = true;
            _later.gameObject.SetActive(_blackStart == null);
            _panel.SetActive(true);
        }

        private void Update()
        {
            if (!_panel.activeSelf || _scope == null) return;
            if (_resultTimer > 0)
            {
                _resultTimer -= Time.unscaledDeltaTime;
                if (_resultTimer <= 0) _panel.SetActive(false);
                return;
            }
            // The black start steps its own scope; on the grid this view does, at the live frequency.
            if (_blackStart == null) _scope.Step(_grid.Frequency, Time.unscaledDeltaTime);
            _needle.localRotation = Quaternion.Euler(0, 0, -(float)_scope.PhaseDegrees);

            double slip = _scope.SlipHz;
            _slipLabel.text = $"slip {(slip >= 0 ? "+" : "-")}{System.Math.Abs(slip):0.00} Hz · {(slip > 0 ? "unit fast" : "unit slow")}";
            var judge = _scope.Judge();
            _slipLabel.color = judge == Synchroscope.Close.Clean ? _healthy
                             : judge == Synchroscope.Close.Rough ? _caution : _alarm;
        }

        private void CloseBreaker()
        {
            if (_scope == null || _resultTimer > 0) return;
            Synchroscope.Close result;
            if (_blackStart != null) result = _blackStart.CloseBreaker();
            else
            {
                result = _scope.Judge();
                _grid.EndSync(_unit, result);
            }
            _hintLabel.text = result switch
            {
                Synchroscope.Close.Clean => "Clean close. The unit picks up its share.",
                Synchroscope.Close.Rough => "Closed with a jolt. It holds.",
                _ => "Out of phase: the breaker slams and the unit trips.",
            };
            _close.interactable = false;
            _resultTimer = 1.4f;
        }
    }
}
