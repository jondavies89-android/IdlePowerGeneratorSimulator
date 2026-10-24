using TMPro;
using UnityEngine;

namespace IdlePowerGen.UI
{
    // A day from midnight to midnight with a marker at the hour (todo Era 2 §5.3), so the
    // player can see where in the day they are and when the next customers will ask.
    public class Era2DayBarView : MonoBehaviour
    {
        [SerializeField] private RectTransform _marker;
        [SerializeField] private TMP_Text _clockLabel;

        private void Update()
        {
            float hour = Economy.ContractBoard.HourOf(Controllers.Era2WorksController.Now);
            float x = hour / 24f;
            _marker.anchorMin = new Vector2(x, _marker.anchorMin.y);
            _marker.anchorMax = new Vector2(x, _marker.anchorMax.y);
            int h = Mathf.FloorToInt(hour);
            int m = Mathf.FloorToInt((hour - h) * 60f);
            _clockLabel.text = $"{h:00}:{m:00}";
        }
    }
}
