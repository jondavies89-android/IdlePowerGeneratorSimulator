using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdlePowerGen.UI
{
    // A horizontal gauge for the black start's hands-on steps: a reading (as a fill, or as a
    // moving marker), the band to keep it in, and the red where it goes wrong. The bands are
    // placed each frame, so one gauge serves the crank, the gates, the oil and the frequency.
    public class BandGaugeView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _label;
        [SerializeField] private TMP_Text _value;
        [SerializeField] private Image _fill;
        [SerializeField] private RectTransform _band;
        [SerializeField] private RectTransform _red;
        [SerializeField] private RectTransform _marker;

        public void Show(string label, string value, float fraction, Color colour,
                         float bandFrom = 0, float bandTo = 0, float redFrom = 0, float redTo = 0, bool asMarker = false)
        {
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            _label.text = label;
            _value.text = value;
            _value.color = colour;
            fraction = Mathf.Clamp01(fraction);
            _fill.enabled = !asMarker;
            _fill.fillAmount = fraction;
            _fill.color = colour;
            _marker.gameObject.SetActive(asMarker);
            if (asMarker) _marker.anchorMin = _marker.anchorMax = new Vector2(fraction, 0.5f);
            Place(_band, bandFrom, bandTo);
            Place(_red, redFrom, redTo);
        }

        public void Hide()
        {
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        private static void Place(RectTransform zone, float from, float to)
        {
            bool show = to > from;
            if (zone.gameObject.activeSelf != show) zone.gameObject.SetActive(show);
            if (!show) return;
            zone.anchorMin = new Vector2(Mathf.Clamp01(from), 0);
            zone.anchorMax = new Vector2(Mathf.Clamp01(to), 1);
            zone.offsetMin = zone.offsetMax = Vector2.zero;
        }
    }
}
