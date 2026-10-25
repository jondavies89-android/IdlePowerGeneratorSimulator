using TMPro;
using UnityEngine;
using UnityEngine.UI;
using IdlePowerGen.Utils;

namespace IdlePowerGen.UI
{
    // The coal yard: tonnes in stock and days of cover at the current burn, with a hand
    // order. Once the bunkers have run dry once (bible §14.2.2), the fuel manager's slider
    // appears: the days of cover to keep, topped up automatically, at a cost.
    public class FuelPanelView : MonoBehaviour
    {
        [SerializeField] private Controllers.UnitGridController _grid;
        [SerializeField] private GameObject _panel;
        [SerializeField] private TMP_Text _stockLabel;
        [SerializeField] private Image _fill;
        [SerializeField] private Button _order;
        [SerializeField] private TMP_Text _orderLabel;
        [SerializeField] private GameObject _manager;
        [SerializeField] private Slider _target;
        [SerializeField] private TMP_Text _targetLabel;
        [SerializeField] private Color _healthy = Color.green;
        [SerializeField] private Color _caution = Color.yellow;
        [SerializeField] private Color _alarm   = Color.red;

        private const float RefreshInterval = 0.25f;
        private float _since = RefreshInterval;

        private void Awake()
        {
            _order.onClick.AddListener(() => _grid.OrderCoal());
            _target.minValue = 0;
            _target.maxValue = Data.Era4Data.MaxTargetDays;
            _target.wholeNumbers = true;
            _target.onValueChanged.AddListener(v => _grid.SetCoalTarget(v));
        }

        private void Update()
        {
            _since += Time.unscaledDeltaTime;
            if (_since < RefreshInterval) return;
            _since = 0f;
            if (_grid == null) return;

            bool show = _grid.HasCoalUnits || _grid.CoalTonnes > 0 && _grid.FuelManagerOpen;
            if (_panel.activeSelf != show) _panel.SetActive(show);
            if (!show) return;

            double days = _grid.CoalDaysOfCover;
            _stockLabel.text = days >= 99
                ? $"Coal yard · {_grid.CoalTonnes:N0} t · nothing burning"
                : $"Coal yard · {_grid.CoalTonnes:N0} t · {days:0.#} days at this burn";
            float frac = Mathf.Clamp01((float)(days / Data.Era4Data.RecommendedCoverDays));
            _fill.fillAmount = frac;
            _fill.color = days >= 3 ? _healthy : days >= 1 ? _caution : _alarm;

            _orderLabel.text = $"Order coal {Managers.Money.Format(_grid.ManualOrderCost)}";
            _order.interactable = Managers.GameManager.Instance.Ledger.Pennies >= _grid.ManualOrderCost;

            bool manager = _grid.FuelManagerOpen;
            if (_manager.activeSelf != manager) _manager.SetActive(manager);
            if (manager)
            {
                _target.SetValueWithoutNotify(_grid.CoalTargetDays);
                _targetLabel.text = _grid.CoalTargetDays > 0
                    ? $"Keep {_grid.CoalTargetDays:0} days in the yard · the merchant tops it up"
                    : "No standing order: coal by hand";
            }
        }
    }
}
