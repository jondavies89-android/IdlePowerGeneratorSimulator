using TMPro;
using UnityEngine;

namespace IdlePowerGen.UI
{
    // "Thursday evening, 9th October 1851": the date and the part of the day, because in
    // Era 2 what customers want depends on the hour.
    public class Era2HeaderView : MonoBehaviour
    {
        [SerializeField] private Controllers.Era2WorksController _works;
        [SerializeField] private TMP_Text _dateLabel;

        private const float RefreshInterval = 0.25f;
        private float _since = RefreshInterval;

        private void Update()
        {
            _since += Time.unscaledDeltaTime;
            if (_since < RefreshInterval) return;
            _since = 0f;

            var date = _works.Date;
            float hour = Economy.ContractBoard.HourOf(Controllers.Era2WorksController.Now);
            var en = System.Globalization.CultureInfo.InvariantCulture;
            _dateLabel.text = $"{date.ToString("dddd", en)} {PartOfDay(hour)}, " +
                              $"{Ordinal(date.Day)} {date.ToString("MMMM yyyy", en)}";
        }

        public static string PartOfDay(float hour) =>
            hour < 5f  ? "night"
          : hour < 12f ? "morning"
          : hour < 17f ? "afternoon"
          : hour < 21f ? "evening"
                       : "night";

        private static string Ordinal(int day)
        {
            string suffix = (day % 100) is 11 or 12 or 13 ? "th"
                          : (day % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" };
            return day + suffix;
        }
    }
}
