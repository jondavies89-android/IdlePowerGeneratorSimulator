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
            _titleLabel.text = contract.clientName;
            _bodyLabel.text  = contract.description;

            if (stage == Stage.Supplying)
            {
                _statusLabel.text = $"Supplying {NumberFormatter.FormatWatts(contract.requiredWatts)}… " +
                                    $"{Mathf.FloorToInt(_controller.Progress * 100)}%";
                _progressFill.fillAmount = _controller.Progress;
                _button.interactable = false;
                _group.alpha = 1f;
                return;
            }

            _progressFill.fillAmount = 0f;
            _button.interactable = _controller.CanAccept;
            _group.alpha = _button.interactable ? 1f : 0.75f;
            _statusLabel.text = _controller.CanAccept
                ? "Shake on it"
                : $"He needs {NumberFormatter.FormatWatts(contract.requiredWatts)}. " +
                  $"Your machines make {NumberFormatter.FormatWatts(_controller.OutputWatts)}.";
        }
    }
}
