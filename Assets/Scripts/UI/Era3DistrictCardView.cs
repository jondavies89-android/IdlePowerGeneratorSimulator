using TMPro;
using UnityEngine;
using UnityEngine.UI;
using IdlePowerGen.Data;
using IdlePowerGen.Utils;
using static IdlePowerGen.Controllers.Era3TownController;

namespace IdlePowerGen.UI
{
    // The district tapped on the town map: how much it wants, how far it is, what its line
    // loses, and the one thing to do about it: connect it, or give it heavier copper. The
    // whole card is the button (CLAUDE.md tap-target rule).
    public class Era3DistrictCardView : MonoBehaviour
    {
        [SerializeField] private Controllers.Era3TownController _town;
        [SerializeField] private TMP_Text _nameLabel;
        [SerializeField] private TMP_Text _detailLabel;
        [SerializeField] private TMP_Text _actionLabel;
        [SerializeField] private Button _button;
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private Color _text    = Color.white;
        [SerializeField] private Color _money   = new(0.95f, 0.76f, 0.31f);
        [SerializeField] private Color _muted   = Color.gray;
        [SerializeField] private Color _caution = new(0.94f, 0.63f, 0.19f);
        [SerializeField] private Color _alarm   = new(0.88f, 0.35f, 0.29f);

        private const float DimmedAlpha = 0.55f;
        private string _id;

        private void Awake() => _button.onClick.AddListener(Act);

        public void Bind(string id)
        {
            _id = id;
            Refresh();
        }

        private void Update() => Refresh();

        private void Act()
        {
            if (_id == null) return;
            var state = _town.StateOf(_id);
            if (state == ConnectState.Available) _town.Connect(_id);
            else if (state == ConnectState.Lit) _town.UpgradeCopper(_id);
        }

        private void Refresh()
        {
            if (_id == null || _town == null) return;
            var spec = _town.Spec(_id);
            var area = _town.Area(_id);
            var gm = Managers.GameManager.Instance;
            string blocks = spec.Blocks == 1 ? "1 block" : $"{spec.Blocks} blocks";
            _nameLabel.text = spec.Name;

            var state = _town.StateOf(_id);
            bool interactable = false;
            _actionLabel.color = _money;
            if (state == ConnectState.Lit)
            {
                double loss = area.LossFraction;
                string served = area.ServedFraction < 0.95 ? $" · lamps dim ({area.ServedFraction:0%})" : "";
                _detailLabel.text = $"draws {NumberFormatter.FormatWatts(area.DeliveredWatts)} · {blocks} out · " +
                                    $"the line loses {loss:0%}{served}";
                _detailLabel.color = loss >= Era3Data.FlickerBadlyLoss ? _alarm : loss >= Era3Data.FlickerLoss ? _caution : _muted;
                if (_town.CanUpgradeCopper(_id))
                {
                    decimal cost = _town.CopperCost(_id);
                    _actionLabel.text = $"Heavier copper {Managers.Money.Format(cost, 3)} · halves the loss";
                    interactable = gm != null && gm.Ledger.Pennies >= cost;
                }
                else
                {
                    _actionLabel.text = "Copper at its heaviest. Higher voltage would help.";
                    _actionLabel.color = _muted;
                }
            }
            else
            {
                double kw = spec.BaseKilowatts * 1e3;
                double wouldLose = _town.LossPerWatt(_id, 0) * kw;
                _detailLabel.text = $"wants about {NumberFormatter.FormatWatts(kw)} · {blocks} out · " +
                                    $"would lose ~{wouldLose:0%} on {ShortMains(_town.Mains.Name)}";
                _detailLabel.color = _muted;
                switch (state)
                {
                    case ConnectState.NeedsParent:
                        _actionLabel.text = $"Light {_town.Spec(_town.ParentOf(_id)).Name} first: the mains run through it";
                        _actionLabel.color = _muted;
                        break;
                    case ConnectState.TooFar:
                        _actionLabel.text = "Too far for direct current. It needs alternating current.";
                        _actionLabel.color = _caution;
                        break;
                    default:
                        _actionLabel.text = $"Connect {Managers.Money.Format(_town.ConnectCost(_id), 3)}";
                        interactable = state == ConnectState.Available;
                        break;
                }
            }
            _button.interactable = interactable;
            _group.alpha = interactable || state == ConnectState.Lit ? 1f : DimmedAlpha;
        }

        // "Edison direct current, 110 volts" → "Edison DC" for a one-line card.
        private static string ShortMains(string name) =>
            name.StartsWith("Edison") ? "Edison's DC"
          : name.StartsWith("Three-wire") ? "three-wire DC"
          : name.StartsWith("Alternating") ? "2,000-volt AC"
          : "6,600-volt AC";
    }
}
