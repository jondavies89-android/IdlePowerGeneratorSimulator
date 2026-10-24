using TMPro;
using UnityEngine;
using UnityEngine.UI;
using IdlePowerGen.Utils;
using Stage = IdlePowerGen.Controllers.Era1FirstCustomerController.Stage;

namespace IdlePowerGen.UI
{
    // The neighbour's card at the top of the list. Visible from the 500 J knock until the
    // contract is done; it always says what's needed next, so there's never a dead end.
    public class Era1FirstCustomerView : MonoBehaviour
    {
        [SerializeField] private Controllers.Era1FirstCustomerController _controller;
        [SerializeField] private GameObject _card;
        [SerializeField] private TMP_Text _titleLabel;
        [SerializeField] private TMP_Text _bodyLabel;
        [SerializeField] private TMP_Text _statusLabel;
        [SerializeField] private Button _button;
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private Image _progressFill;
        // Amber while the workshop builds towards his wattage, green while it supplies him.
        [SerializeField] private Color _buildingColour = new(0.88f, 0.63f, 0.25f);
        [SerializeField] private Color _supplyingColour = new(0.61f, 0.75f, 0.35f);

        // The output figure moves once a second; easing the bar hides the step.
        private const float BarEaseRate = 2f; // bar-widths per second

        private void Awake() => _button.onClick.AddListener(() => _controller.Accept());

        // Polled rather than event-driven: progress moves every frame while supplying,
        // and the card is a single object.
        private void Update()
        {
            var stage = _controller.Current;
            bool show = stage == Stage.Offered || stage == Stage.Supplying;
            if (_card.activeSelf != show) _card.SetActive(show);
            if (!show) return;

            var contract = _controller.Contract;
            string needed = NumberFormatter.FormatWatts(contract.requiredWatts);
            _titleLabel.text = contract.clientName;
            _bodyLabel.text  = contract.description;

            if (stage == Stage.Supplying)
            {
                _statusLabel.text = $"Supplying {needed}… {Mathf.FloorToInt(_controller.Progress * 100)}%. " +
                                    "Don't let it drop.";
                // Supplying starts the bar again from empty, so no easing across the switch.
                _progressFill.color = _supplyingColour;
                _progressFill.fillAmount = _controller.Progress;
                _button.interactable = false;
                _group.alpha = 1f;
                return;
            }

            if (_progressFill.color != _buildingColour)
            {
                _progressFill.color = _buildingColour;
                _progressFill.fillAmount = 0f;
            }
            _progressFill.fillAmount = Mathf.MoveTowards(_progressFill.fillAmount, _controller.BuildProgress,
                                                         BarEaseRate * Time.unscaledDeltaTime);
            _button.interactable = _controller.CanAccept;
            _group.alpha = _button.interactable ? 1f : 0.75f;

            string status = _controller.CanAccept
                ? $"Shake on it, then hold {needed} for the hour"
                : $"He needs {needed}. Your machines make {NumberFormatter.FormatWatts(_controller.OutputWatts)}.";
            _statusLabel.text = _controller.WasBroken
                ? $"The lamps dimmed below {needed}, so he called it off. {status}"
                : status;
        }
    }
}
