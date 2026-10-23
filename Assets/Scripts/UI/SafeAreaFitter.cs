using UnityEngine;

namespace IdlePowerGen.UI
{
    // Keeps content clear of notches and gesture bars on Android. On Web the safe area is
    // the whole canvas, so this is a no-op there.
    [RequireComponent(typeof(RectTransform))]
    public class SafeAreaFitter : MonoBehaviour
    {
        private Rect _applied;

        private void OnEnable() => Apply();
        private void Update()   { if (Screen.safeArea != _applied) Apply(); }

        private void Apply()
        {
            _applied = Screen.safeArea;
            if (Screen.width <= 0 || Screen.height <= 0) return;

            var rt = (RectTransform)transform;
            rt.anchorMin = new Vector2(_applied.xMin / Screen.width, _applied.yMin / Screen.height);
            rt.anchorMax = new Vector2(_applied.xMax / Screen.width, _applied.yMax / Screen.height);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
