using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using IdlePowerGen.Utils;

namespace IdlePowerGen.UI
{
    // The top of an Era 3–5 screen: where and when, the cash (with a punch when it comes in),
    // the research points and the steady income. The date reads as each era's mockup does.
    public class GridHeaderView : MonoBehaviour
    {
        public enum DateStyle { Period, ControlDesk, Dispatch }

        [SerializeField] private Controllers.GridWorksController _works;
        [SerializeField] private TMP_Text _dateLabel;
        [SerializeField] private TMP_Text _cashLabel;
        [SerializeField] private TMP_Text _researchLabel;
        [SerializeField] private TMP_Text _incomeLabel;
        [SerializeField] private string _place = "Pearl Street";
        [SerializeField] private DateStyle _style;

        private const float RefreshInterval = 0.1f;
        private float _since = RefreshInterval;
        private decimal _lastCash = -1m;
        private Coroutine _punch;

        private void Update()
        {
            _since += Time.unscaledDeltaTime;
            if (_since < RefreshInterval) return;
            _since = 0f;

            var gm = Managers.GameManager.Instance;
            if (gm == null || _works == null) return;

            _dateLabel.text = DateText(_works.Date, Economy.ContractBoard.HourOf(Controllers.ContractDesk.Now));

            decimal cash = gm.Ledger.Pennies;
            _cashLabel.text = Managers.Money.Format(cash, _works.EraNumber);
            if (_lastCash >= 0 && cash > _lastCash + Managers.Money.PenniesPerPound * 10m)
            {
                if (_punch != null) StopCoroutine(_punch);
                _punch = StartCoroutine(UiTween.Punch(_cashLabel.transform, 1.12f, 0.35f));
            }
            _lastCash = cash;

            var applier = Managers.UpgradeEffectApplier.Instance;
            double rate = applier != null ? applier.ResearchPerHour : 0;
            if (_researchLabel != null)
                _researchLabel.text = rate > 0 ? $"{gm.Ledger.ResearchPoints:N0} RP  ·  +{rate:N0} an hour"
                                               : $"{gm.Ledger.ResearchPoints:N0} RP";

            if (_incomeLabel != null)
                _incomeLabel.text = $"+{Managers.Money.Format((decimal)Math.Max(0, _works.IncomePerHour), _works.EraNumber)} an hour";
        }

        private string DateText(DateTime date, float hour)
        {
            var en = CultureInfo.InvariantCulture;
            int h = Mathf.FloorToInt(hour), m = Mathf.FloorToInt((hour - h) * 60f);
            switch (_style)
            {
                case DateStyle.ControlDesk:
                    return $"{_place} · {date.Day} {date.ToString("MMMM yyyy", en).ToUpperInvariant()} · {h:00}:{m:00}";
                case DateStyle.Dispatch:
                    return $"{_place} · {date.Day} {date.ToString("MMM yyyy", en).ToLowerInvariant()} · {h:00}:{m:00}";
                default:
                    return $"{_place}, {date.ToString("dddd", en)} {Era2HeaderView.PartOfDay(hour)}, " +
                           $"{Ordinal(date.Day)} {date.ToString("MMMM yyyy", en)}";
            }
        }

        private static string Ordinal(int day)
        {
            string suffix = (day % 100) is 11 or 12 or 13 ? "th"
                          : (day % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" };
            return day + suffix;
        }
    }
}
