using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using IdlePowerGen.Utils;

namespace IdlePowerGen.UI
{
    // One running engagement (bible Appendix A, Era 2: "the contract progress bar is the
    // era's spotlight mechanic"). The bar is the hours passing; its colour is how well the
    // customer is being supplied so far: green holding, amber short, red failing.
    public class ContractEngagementCardView : MonoBehaviour
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

        private Color _stateColour;
        private bool _stateColourKept;

        public void Bind(Economy.ContractInstance contract)
        {
            if (!_stateColourKept) { _stateColour = _stateLabel.color; _stateColourKept = true; }
            Contract = contract;
            var def = contract.Definition;
            _titleLabel.text  = def.clientName;
            _bodyLabel.text   = def.description;
            _needLabel.text   = $"need {NumberFormatter.FormatWatts(def.requiredWatts)}";
            Refresh();
        }

        private void Update() => Refresh();

        private void Refresh()
        {
            if (Contract == null) return;
            double now = Controllers.ContractDesk.Now;
            _progressFill.fillAmount = Contract.Progress01;

            // Game time, so the 2× boost (which runs the clock faster) leaves the end hour alone.
            double left = Contract.SecondsLeft;
            _windowLabel.text = $"{Clock(Contract.StartGameTime)} → {Clock(now + left)}";
            int hours = (int)(left / 3600), minutes = (int)(left % 3600 / 60);
            _timeLabel.text = hours > 0 ? $"{hours} h {minutes:00} m left" : $"{minutes} m left";

            float ratio = Contract.RatioSoFar();
            var def = Contract.Definition;

            // The client pays as the power arrives; any premium and the repute wait for the end.
            _payLabel.text = $"paid {Managers.Money.Format(Contract.PaidSoFar, def.minEra)} of " +
                             $"{Managers.Money.Format(Controllers.ContractDesk.Pay(def), def.minEra)} · " +
                             $"+{def.reputationOnSuccess:0.#} repute on finishing";
            // The shortfall countdown outranks the running average: it is what can end the job.
            if (Contract.IsShort)
            {
                _stateLabel.text = $"short of power · cancels in {Minutes(Contract.FailsInSeconds)}";
                _stateLabel.color = _alarm;
                _progressFill.color = _alarm;
            }
            else if (Contract.CountdownRunning)
            {
                // Same width as the red line it replaces, so it doesn't crowd the time left.
                _stateLabel.text = $"held at {Minutes(Contract.FailsInSeconds)} · clears in {Minutes(Contract.ClearsInSeconds)}";
                _stateLabel.color = _caution;
                _progressFill.color = _caution;
            }
            else if (ratio >= 0.999f)
            {
                _stateLabel.text = "holding";
                _stateLabel.color = _stateColour;
                _progressFill.color = _healthy;
            }
            else
            {
                _stateLabel.text = $"{ratio:0%} supplied";
                _stateLabel.color = _stateColour;
                _progressFill.color = ratio >= HoldingRatio ? _healthy : ratio >= def.failThreshold ? _caution : _alarm;
            }
        }

        // Game minutes, rounded up so the countdown reads "1 m" until it actually runs out.
        private static string Minutes(double seconds) => $"{Math.Ceiling(seconds / 60.0):0} m";

        public static string Clock(double gameTime)
        {
            float hour = Economy.ContractBoard.HourOf(gameTime);
            int h = Mathf.FloorToInt(hour);
            int m = Mathf.FloorToInt((hour - h) * 60f);
            return $"{h:00}:{m:00}";
        }
    }
}
