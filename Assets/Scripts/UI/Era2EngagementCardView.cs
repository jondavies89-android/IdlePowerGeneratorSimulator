using TMPro;
using UnityEngine;
using UnityEngine.UI;
using IdlePowerGen.Utils;

namespace IdlePowerGen.UI
{
    // One running engagement (bible Appendix A, Era 2: "the contract progress bar is the
    // era's spotlight mechanic"). The bar is the hours passing; its colour is how well the
    // customer is being supplied so far: green holding, amber short, red failing.
    public class Era2EngagementCardView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _titleLabel;
        [SerializeField] private TMP_Text _bodyLabel;
        [SerializeField] private TMP_Text _windowLabel;
        [SerializeField] private TMP_Text _needLabel;
        [SerializeField] private Image _progressFill;
        [SerializeField] private TMP_Text _timeLabel;
        [SerializeField] private TMP_Text _stateLabel;
        [SerializeField] private TMP_Text _payLabel;
        [SerializeField] private Color _healthy = new(0.30f, 0.69f, 0.31f);
        [SerializeField] private Color _caution = new(1.00f, 0.60f, 0.00f);
        [SerializeField] private Color _alarm   = new(0.90f, 0.22f, 0.21f);

        // Todo Era 2 §5.2: green from 80 %, amber from the 50 % fail line, red below it.
        private const float HoldingRatio = 0.8f;

        public Economy.ContractInstance Contract { get; private set; }

        public void Bind(Economy.ContractInstance contract)
        {
            Contract = contract;
            var def = contract.Definition;
            _titleLabel.text  = def.clientName;
            _bodyLabel.text   = def.description;
            _windowLabel.text = $"{Clock(contract.StartGameTime)} → {Clock(contract.EndGameTime)}";
            _needLabel.text   = $"need {NumberFormatter.FormatWatts(def.requiredWatts)}";
            Refresh();
        }

        private void Update() => Refresh();

        private void Refresh()
        {
            if (Contract == null) return;
            double now = Controllers.Era2WorksController.Now;
            float span = (float)(Contract.EndGameTime - Contract.StartGameTime);
            _progressFill.fillAmount = span <= 0 ? 1f : Mathf.Clamp01((float)(now - Contract.StartGameTime) / span);

            double left = System.Math.Max(0, Contract.EndGameTime - now);
            int hours = (int)(left / 3600), minutes = (int)(left % 3600 / 60);
            _timeLabel.text = hours > 0 ? $"{hours} h {minutes:00} m left" : $"{minutes} m left";

            float ratio = Contract.RatioSoFar(now);
            var def = Contract.Definition;

            // The client pays as the power arrives; any premium and the repute wait for the end.
            _payLabel.text = $"paid {NumberFormatter.FormatPennies(Contract.PaidSoFar)} of " +
                             $"{NumberFormatter.FormatPennies(Controllers.Era2WorksController.Pay(def))} · " +
                             $"+{def.reputationOnSuccess:0.#} repute on finishing";
            if (ratio >= 0.999f)
            {
                _stateLabel.text = "holding";
                _progressFill.color = _healthy;
            }
            else
            {
                _stateLabel.text = ratio >= def.failThreshold ? $"{ratio:0%} supplied" : $"{ratio:0%} supplied · failing";
                _progressFill.color = ratio >= HoldingRatio ? _healthy : ratio >= def.failThreshold ? _caution : _alarm;
            }
        }

        public static string Clock(double gameTime)
        {
            float hour = Economy.ContractBoard.HourOf(gameTime);
            int h = Mathf.FloorToInt(hour);
            int m = Mathf.FloorToInt((hour - h) * 60f);
            return $"{h:00}:{m:00}";
        }
    }
}
