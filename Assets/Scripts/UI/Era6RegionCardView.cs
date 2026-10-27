using TMPro;
using UnityEngine;
using UnityEngine.UI;
using IdlePowerGen.Utils;

namespace IdlePowerGen.UI
{
    // The region picked on the national mimic board: what it draws or would, and the one thing
    // to do with it: connect it, or drop it and pick it up again.
    public class Era6RegionCardView : MonoBehaviour
    {
        [SerializeField] private Controllers.Era6NationalGridController _grid;
        [SerializeField] private TMP_Text _nameLabel;
        [SerializeField] private TMP_Text _detailLabel;
        [SerializeField] private TMP_Text _actionLabel;
        [SerializeField] private Button _button;
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private Color _muted   = Color.gray;
        [SerializeField] private Color _caution = Color.yellow;
        [SerializeField] private Color _alarm   = Color.red;

        private string _id;

        private void Awake() => _button.onClick.AddListener(Act);

        public void Bind(string id) { _id = id; Refresh(); }

        private void Update() => Refresh();

        private void Act()
        {
            if (_id == null) return;
            var a = _grid.Area(_id);
            if (!a.Connected) _grid.Connect(_id);
            else _grid.SetShed(_id, !a.Shed);
        }

        private void Refresh()
        {
            if (_id == null || _grid == null) return;
            var s = _grid.Spec(_id);
            var a = _grid.Area(_id);
            _nameLabel.text = s.Name + (s.Industrial ? "  · heavy industry" : "");
            bool can;
            if (!a.Connected)
            {
                _detailLabel.text = $"about {NumberFormatter.FormatWatts(a.BaseWatts)} on average · more at the evening peak, less at night";
                _detailLabel.color = _muted;
                bool parentOn = s.Parent == null || _grid.Area(s.Parent).Connected;
                _actionLabel.text = parentOn ? $"Connect {Managers.Money.Format(_grid.ConnectCost(_id))}"
                                             : $"Connect {_grid.Spec(s.Parent).Name} first: the line runs through it";
                can = _grid.CanConnect(_id);
            }
            else
            {
                _detailLabel.text = a.Shed ? "dropped to save the grid"
                                  : $"drawing {NumberFormatter.FormatWatts(a.DeliveredWatts)} · {a.LossFraction:0.#%} lost on the lines" +
                                    (a.ServedFraction < 0.95 ? $" · only {a.ServedFraction:0%} of what it wants" : "");
                _detailLabel.color = a.Shed ? _alarm : a.ServedFraction < 0.95 ? _caution : _muted;
                _actionLabel.text = a.Shed ? "Pick it up again" : "Drop it to save the grid";
                can = _grid.BlackStart == null && _grid.IsCurrentEra;
            }
            _button.interactable = can;
            _group.alpha = can ? 1f : 0.7f;
        }
    }
}
