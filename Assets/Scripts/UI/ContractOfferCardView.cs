using TMPro;
using UnityEngine;
using UnityEngine.UI;
using IdlePowerGen.Models;
using IdlePowerGen.Utils;

namespace IdlePowerGen.UI
{
    // One client on the board. Tapping the card books it, whenever the player likes; the next
    // client up the repute ladder is shown dimmed with what it will take.
    public class ContractOfferCardView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _titleLabel;
        [SerializeField] private TMP_Text _bodyLabel;
        [SerializeField] private TMP_Text _detailLabel;
        [SerializeField] private TMP_Text _statusLabel;
        [SerializeField] private GameObject _badge;
        [SerializeField] private Button _button;
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private Color _statusColour = Color.white;
        [SerializeField] private Color _cautionColour = new(1.00f, 0.60f, 0.00f);

        private const float DimmedAlpha = 0.45f;

        public enum State { Hidden, Offered, Locked }

        private Controllers.ContractDesk _works;
        public ContractDefinition Definition { get; private set; }
        public State Current { get; private set; }

        public void Bind(ContractDefinition def, Controllers.ContractDesk works)
        {
            Definition = def;
            _works = works;
            _titleLabel.text = def.clientName;
            _bodyLabel.text  = def.description;
            _detailLabel.text =
                $"{NumberFormatter.FormatWatts(def.requiredWatts)} for {def.durationHours:0.#} hours · " +
                $"pays {Managers.Money.Format(Controllers.ContractDesk.Pay(def), works.EraNumber)} · " +
                $"+{def.reputationOnSuccess:0.#} repute";
            if (_badge != null) _badge.SetActive(def.tier == ContractTier.Reputation);
            _button.onClick.AddListener(() => _works.Book(Definition));
        }

        // Decides what the card says; the list decides whether and where it is shown.
        public void Evaluate(bool showLocked)
        {
            var gm = Managers.GameManager.Instance;
            if (gm.Ledger.Reputation < Definition.requiredReputation)
                Current = showLocked ? State.Locked : State.Hidden;
            else
                Current = _works.IsOffered(Definition) ? State.Offered : State.Hidden; // running now

            _statusLabel.color = _statusColour;
            switch (Current)
            {
                case State.Offered:
                    double spare = _works.SpareForContracts;
                    if (!_works.HasFreeSlot)
                        _statusLabel.text = "Every engagement is taken. Finish one first.";
                    else if (spare < Definition.requiredWatts)
                    {
                        string shortBy = NumberFormatter.FormatWatts(Definition.requiredWatts - System.Math.Max(0, spare));
                        // Within the leeway the client won't mind; past it they'd cancel once
                        // the countdown ran out, unless the works grows in time.
                        bool tolerated = spare >= Definition.requiredWatts * (1.0 - Definition.shortfallLeeway);
                        _statusLabel.text = tolerated
                            ? $"Book it, though you'd be {shortBy} short"
                            : $"Book it · {shortBy} short, they'd cancel in {Definition.shortfallCountdownMinutes:0} m";
                        _statusLabel.color = _cautionColour;
                    }
                    else
                        _statusLabel.text = "Book it";
                    break;
                case State.Locked:
                    _statusLabel.text = $"Will deal with you at {Definition.requiredReputation:0.#} repute";
                    break;
            }

            _button.interactable = Current == State.Offered && _works.HasFreeSlot;
            _group.alpha = _button.interactable ? 1f : DimmedAlpha;
        }
    }
}
