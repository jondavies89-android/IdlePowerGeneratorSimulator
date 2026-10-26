using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdlePowerGen.UI
{
    // The bench galvanometer on Era 3's Research tab: the needle swinging across the scale,
    // the band to catch it in, a Take a reading button, and how the last reading went.
    public class Era3GalvanometerView : MonoBehaviour
    {
        [SerializeField] private Controllers.Era3TownController _town;
        [SerializeField] private RectTransform _needle;
        [SerializeField] private RectTransform _band;
        [SerializeField] private Image _bandImage;
        [SerializeField] private TMP_Text _statusLabel;
        [SerializeField] private TMP_Text _earnedLabel;
        [SerializeField] private Button _read;
        [SerializeField] private Color _healthy = Color.green;
        [SerializeField] private Color _caution = Color.yellow;
        [SerializeField] private Color _muted   = Color.gray;
        [SerializeField] private Color _money   = Color.yellow;

        private string _last;
        private Color _lastColour;
        private float _lastShown;

        private void Awake() => _read.onClick.AddListener(Read);

        private void Read()
        {
            var g = _town.Galvanometer;
            if (!g.Ready) return;
            var r = _town.TakeReading();
            _lastShown = 2.5f;
            if (r.Hit)
            {
                _last = $"A clean reading: +{r.Research:0.#} RP" + (g.Run > 1 ? $" · {g.Run} in a row" : "");
                _lastColour = _healthy;
            }
            else
            {
                _last = "Missed. The needle is settling, and the run starts again.";
                _lastColour = _caution;
            }
        }

        private void Update()
        {
            if (_town == null) return;
            var g = _town.Galvanometer;
            float x = (float)(g.Needle + 1) / 2f;
            _needle.anchorMin = new Vector2(x, 0);
            _needle.anchorMax = new Vector2(x, 1);
            float lo = (float)(g.BandCentre - g.BandHalfWidth + 1) / 2f, hi = (float)(g.BandCentre + g.BandHalfWidth + 1) / 2f;
            _band.anchorMin = new Vector2(lo, 0);
            _band.anchorMax = new Vector2(hi, 1);
            _bandImage.color = g.InBand && g.Ready ? _money : _healthy;

            _read.interactable = g.Ready;
            _lastShown -= Time.unscaledDeltaTime;
            if (_lastShown > 0)
            {
                _statusLabel.text = _last;
                _statusLabel.color = _lastColour;
            }
            else
            {
                _statusLabel.text = $"Take a reading as the needle crosses the band: +{_town.ReadingWorth:0.#} RP. " +
                                    "Readings in a row are worth more.";
                _statusLabel.color = _muted;
            }
            _earnedLabel.text = g.Readings > 0 ? $"{g.Readings} readings · {g.Earned:0.#} RP this session" : "";
        }
    }
}
