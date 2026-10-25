using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using IdlePowerGen.Models;
using IdlePowerGen.Utils;

namespace IdlePowerGen.UI
{
    // One plaque in the upgrade tree. Tapping it selects it, and the tree's detail card
    // below shows what it does and buys it. Every node stays tappable, locked or not, so the
    // player can always read what a far branch needs.
    public class UpgradeNodeView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _nameLabel;
        [SerializeField] private TMP_Text _costLabel;
        [SerializeField] private Button _button;
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private Image _background;
        [SerializeField] private Outline _selectedRing;
        [SerializeField] private Color _panel      = new(0.11f, 0.07f, 0.05f);
        [SerializeField] private Color _ready      = new(0.14f, 0.09f, 0.06f);
        [SerializeField] private Color _owned      = new(0.23f, 0.16f, 0.06f);
        [SerializeField] private Color _text       = Color.white;
        [SerializeField] private Color _muted      = Color.gray;
        [SerializeField] private Color _price      = new(0.83f, 0.63f, 0.09f);
        [SerializeField] private Color _achievement = new(0.83f, 0.63f, 0.09f);

        // Same dimming as a locked card, so a locked node is still legible.
        private const float LockedAlpha = 0.45f;

        public enum State { Locked, Open, Affordable, Owned }

        public UpgradeDefinition Definition { get; private set; }
        public State Current { get; private set; }

        private Action<UpgradeDefinition> _onSelect;

        public void Bind(UpgradeDefinition def, Action<UpgradeDefinition> onSelect)
        {
            Definition = def;
            _onSelect = onSelect;
            _nameLabel.text = def.displayName;
            _button.onClick.AddListener(() => _onSelect?.Invoke(Definition));
            Refresh();
        }

        private bool _selected;

        // The node being read is drawn at full strength even if locked: the ring is an
        // Outline, which would show through a dimmed plaque as a gold wash.
        public void SetSelected(bool selected)
        {
            _selected = selected;
            if (_selectedRing != null) _selectedRing.enabled = selected;
            Refresh();
        }

        public void Refresh()
        {
            var gm = Managers.GameManager.Instance;
            if (gm == null || Definition == null) return;
            var registry = gm.Upgrades;

            int  level     = registry.Level(Definition.upgradeId);
            bool maxed     = registry.IsMaxed(Definition);
            bool reachable = registry.PrerequisitesMet(Definition) && gm.Ledger.Reputation >= Definition.requiredReputation;

            Current = maxed ? State.Owned
                    : !reachable ? State.Locked
                    : registry.CanAfford(Definition) ? State.Affordable
                    : State.Open;

            string cost = Definition.costPennies > 0
                ? NumberFormatter.FormatPennies((decimal)registry.NextCostPennies(Definition))
                : NumberFormatter.FormatJoules(registry.NextCostJoules(Definition));
            string levels = Definition.maxLevel > 1 ? $"{level}/{Definition.maxLevel}" : null;

            switch (Current)
            {
                case State.Owned:
                    _costLabel.text = levels ?? "Bought";
                    _costLabel.color = _achievement;
                    _nameLabel.color = _achievement;
                    _background.color = _owned;
                    break;
                case State.Locked:
                    // Repute is the usual missing piece once the line above is bought.
                    _costLabel.text = registry.PrerequisitesMet(Definition)
                        ? $"{Definition.requiredReputation:0.#} repute" : cost;
                    _costLabel.color = _muted;
                    _nameLabel.color = _text;
                    _background.color = _panel;
                    break;
                default:
                    _costLabel.text = levels != null ? $"{levels} · {cost}" : cost;
                    _costLabel.color = Current == State.Affordable ? _price : _muted;
                    _nameLabel.color = _text;
                    _background.color = Current == State.Affordable ? _ready : _panel;
                    break;
            }
            _group.alpha = Current == State.Locked && !_selected ? LockedAlpha : 1f;
        }
    }
}
