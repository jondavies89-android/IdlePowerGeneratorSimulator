using TMPro;
using UnityEngine;
using UnityEngine.UI;
using IdlePowerGen.Data;

namespace IdlePowerGen.UI
{
    // "Leicester power factor below 0.85 — cotton mills drawing heavy reactive load — install
    // capacitor bank £85k" (bible Appendix A, Era 5): the worst city, the cause in plain
    // English, and the one-tap fix.
    public class Era5AlertView : MonoBehaviour
    {
        [SerializeField] private Controllers.Era5DispatchController _grid;
        [SerializeField] private GameObject _panel;
        [SerializeField] private TMP_Text _titleLabel;
        [SerializeField] private TMP_Text _detailLabel;
        [SerializeField] private Button _button;

        private string _id;
        private const float RefreshInterval = 0.3f;
        private float _since = RefreshInterval;

        private void Awake() => _button.onClick.AddListener(() => { if (_id != null) _grid.AddBank(_id); });

        private void Update()
        {
            _since += Time.unscaledDeltaTime;
            if (_since < RefreshInterval) return;
            _since = 0f;
            if (_grid == null) return;

            var worst = _grid.WorstCity;
            bool show = worst != null && _grid.Voltage(worst.Id) < Era5Data.GoodVoltage;
            if (_panel.activeSelf != show) _panel.SetActive(show);
            if (!show) { _id = null; return; }
            _id = worst.Id;
            var s = _grid.Spec(worst.Id);
            double pf = _grid.PowerFactor(worst.Id);
            _titleLabel.text = pf < 0.9 ? $"{s.Name} power factor below {System.Math.Ceiling(pf * 20) / 20:0.00}"
                                        : $"{s.Name} voltage low: {_grid.Voltage(worst.Id) * Era5Data.NominalKilovolts:0.0} kV";
            string cause = s.PowerFactor < 0.84 ? "cotton mills drawing heavy reactive load"
                         : s.PowerFactor < 0.87 ? "steelworks and heavy motors on the line"
                         : s.PowerFactor < 0.90 ? "potteries and factories drawing reactive load"
                         : "a long line under heavy load";
            bool can = _grid.Banks(worst.Id) < Era5Data.MaxBanks;
            _detailLabel.text = can ? $"{cause} — install capacitor bank {Managers.Money.Format(_grid.BankCost(worst.Id))}"
                                    : $"{cause} — every bank fitted: tap changers or condensers next";
            _button.interactable = can && _grid.CanAddBank(worst.Id);
        }
    }
}
