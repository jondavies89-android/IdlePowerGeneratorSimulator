using TMPro;
using UnityEngine;
using UnityEngine.UI;
using IdlePowerGen.Utils;

namespace IdlePowerGen.UI
{
    // The coal hopper, shown once there is a steam engine to feed. Amber when low, red when
    // nearly or quite empty, and always one tap from an order (CLAUDE.md: every warning
    // carries its remedy).
    public class Era2CoalHopperView : MonoBehaviour
    {
        [SerializeField] private Controllers.Era2WorksController _works;
        [SerializeField] private GameObject _panel;
        [SerializeField] private Image _background;
        [SerializeField] private TMP_Text _titleLabel;
        [SerializeField] private TMP_Text _detailLabel;
        [SerializeField] private Image _fill;
        [SerializeField] private Button _orderButton;
        [SerializeField] private TMP_Text _orderLabel;
        [SerializeField] private CanvasGroup _orderGroup;
        [SerializeField] private Color _normalPanel = new(0.11f, 0.07f, 0.05f);
        [SerializeField] private Color _alarmPanel  = new(0.19f, 0.08f, 0.07f);
        [SerializeField] private Color _healthy = new(0.30f, 0.69f, 0.31f);
        [SerializeField] private Color _caution = new(1.00f, 0.60f, 0.00f);
        [SerializeField] private Color _alarm   = new(0.90f, 0.22f, 0.21f);

        private void Awake() => _orderButton.onClick.AddListener(() => _works.OrderCoal());

        private void Update()
        {
            bool show = _works.HasCoalEngines;
            if (_panel.activeSelf != show) _panel.SetActive(show);
            if (!show) return;

            double basis = _works.CoalBurnCapacityBasis;
            float fraction = _works.Hopper.Fraction(basis);
            double hoursLeft = _works.Hopper.HoursLeft(_works.CoalBurnPerHour);

            bool empty = _works.Hopper.IsEmpty;
            bool veryLow = fraction < Economy.Era2CoalHopper.VeryLowFraction;
            bool low = fraction < Economy.Era2CoalHopper.LowFraction;

            _titleLabel.text = empty ? "Coal hopper empty" : low ? "Coal hopper running low" : $"Coal hopper {fraction:0%} full";
            _detailLabel.text = empty ? "The steam engines have stopped."
                              : double.IsInfinity(hoursLeft) ? "Nothing burning."
                              : $"~{hoursLeft:0} hours at current burn";
            _fill.fillAmount = fraction;
            _fill.color = veryLow || empty ? _alarm : low ? _caution : _healthy;
            _background.color = veryLow || empty ? _alarmPanel : _normalPanel;

            decimal cost = _works.CoalOrderCost;
            _orderLabel.text = cost > 0 ? $"order {NumberFormatter.FormatPennies(cost)}" : "full";
            var gm = Managers.GameManager.Instance;
            _orderButton.interactable = cost > 0 && gm != null && gm.Ledger.Pennies >= cost;
            _orderGroup.alpha = _orderButton.interactable ? 1f : 0.45f;
        }
    }
}
