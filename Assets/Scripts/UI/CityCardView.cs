using TMPro;
using UnityEngine;
using UnityEngine.UI;
using IdlePowerGen.Data;
using IdlePowerGen.Utils;

namespace IdlePowerGen.UI
{
    // The city picked on the mimic board: its load, power factor and voltage, its capacitor
    // banks, and the one thing to do: connect it, or install another bank.
    public class CityCardView : MonoBehaviour
    {
        [SerializeField] private Controllers.Era5DispatchController _grid;
        [SerializeField] private TMP_Text _nameLabel;
        [SerializeField] private TMP_Text _detailLabel;
        [SerializeField] private TMP_Text _actionLabel;
        [SerializeField] private Button _button;
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private Color _text    = Color.black;
        [SerializeField] private Color _muted   = Color.gray;
        [SerializeField] private Color _caution = new(0.72f, 0.38f, 0.13f);
        [SerializeField] private Color _alarm   = Color.red;

        private string _id;

        private void Awake() => _button.onClick.AddListener(Act);

        public void Bind(string id) { _id = id; Refresh(); }

        private void Update() => Refresh();

        private void Act()
        {
            if (_id == null) return;
            if (!_grid.Area(_id).Connected) _grid.Connect(_id);
            else _grid.AddBank(_id);
        }

        private void Refresh()
        {
            if (_id == null || _grid == null) return;
            var s = _grid.Spec(_id);
            var a = _grid.Area(_id);
            _nameLabel.text = s.Name;
            bool can;
            if (!a.Connected)
            {
                _detailLabel.text = $"about {NumberFormatter.FormatWatts(a.BaseWatts)} · power factor {s.PowerFactor:0.00} on its own";
                _detailLabel.color = _muted;
                bool parentOn = s.Parent == null || _grid.Area(s.Parent).Connected;
                _actionLabel.text = parentOn ? $"Connect {Managers.Money.Format(_grid.ConnectCost(_id))}"
                                             : $"Connect {_grid.Spec(s.Parent).Name} first: the line runs through it";
                can = _grid.CanConnect(_id);
            }
            else
            {
                double v = _grid.Voltage(_id);
                string tripped = _grid.VoltageTripped(_id) ? " · TRIPPED, low voltage" : "";
                _detailLabel.text = $"{NumberFormatter.FormatWatts(a.DeliveredWatts)} · PF {_grid.PowerFactor(_id):0.00} · " +
                                    $"{v * Era5Data.NominalKilovolts:0.0} kV · {_grid.Banks(_id)} bank{(_grid.Banks(_id) == 1 ? "" : "s")}{tripped}";
                _detailLabel.color = v >= Era5Data.GoodVoltage ? _muted : v >= Era5Data.PoorVoltage ? _caution : _alarm;
                if (_grid.Banks(_id) >= Era5Data.MaxBanks)
                {
                    _actionLabel.text = "Every bank it can take. Tap changers or condensers next.";
                    can = false;
                }
                else
                {
                    double after = _grid.VoltageAt(_id, _grid.Banks(_id) + 1);
                    _actionLabel.text = $"Install capacitor bank {Managers.Money.Format(_grid.BankCost(_id))} · " +
                                        $"{after * Era5Data.NominalKilovolts:0.0} kV after";
                    can = _grid.CanAddBank(_id);
                }
            }
            _button.interactable = can;
            _group.alpha = can ? 1f : 0.7f;
        }
    }
}
