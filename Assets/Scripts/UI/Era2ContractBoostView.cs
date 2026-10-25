using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdlePowerGen.UI
{
    // The "contracts complete 2× faster" button on the Era 2 office screen. In the published
    // game this is where a rewarded ad would play; for now a tap starts (or tops up) the boost.
    public class Era2ContractBoostView : MonoBehaviour
    {
        [SerializeField] private Button _button;
        [SerializeField] private TMP_Text _label;
        [SerializeField] private Image _background;
        [SerializeField] private Color _idle   = new(0.14f, 0.09f, 0.06f);
        [SerializeField] private Color _active = new(0.10f, 0.16f, 0.08f);
        [SerializeField] private Color _idleText   = new(0.83f, 0.63f, 0.09f);
        [SerializeField] private Color _activeText = new(0.30f, 0.69f, 0.31f);

        private const float RefreshInterval = 0.25f;
        private float _since = RefreshInterval;

        private void Awake() =>
            _button.onClick.AddListener(() => Managers.GameManager.Instance?.StartContractBoost());

        private void Update()
        {
            _since += Time.unscaledDeltaTime;
            if (_since < RefreshInterval) return;
            _since = 0f;

            var gm = Managers.GameManager.Instance;
            if (gm == null) return;

            if (gm.ContractBoostActive)
            {
                int left = Mathf.CeilToInt((float)gm.ContractBoostSecondsLeft);
                _label.text = $"Contracts {gm.ContractSpeed:0}× faster · {left / 60}:{left % 60:00} left";
                _label.color = _activeText;
                _background.color = _active;
            }
            else
            {
                _label.text = $"Contracts complete {Managers.GameManager.ContractBoostSpeed:0}× faster";
                _label.color = _idleText;
                _background.color = _idle;
            }
        }
    }
}
