using UnityEngine;
using UnityEngine.UI;

namespace IdlePowerGen.UI
{
    // Bible Appendix A, Era 2: "the weather glass hint plants the seed of forecasting".
    // It does nothing yet; tapping it only wonders aloud.
    public class Era2WeatherGlassView : MonoBehaviour
    {
        [SerializeField] private Button _button;
        [SerializeField] private ToastView _toast;

        public const string Musing = "A barometer. Might the weather affect demand someday?";

        private void Awake() => _button.onClick.AddListener(() => _toast.Show(Musing));
    }
}
