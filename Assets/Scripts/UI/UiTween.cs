using System.Collections;
using UnityEngine;

namespace IdlePowerGen.UI
{
    // The handful of tweens Era 1 needs (press, punch, fade), as coroutines.
    // Stands in for DOTween so the project has no Asset Store dependency.
    public static class UiTween
    {
        // Scales up to `peak` and settles back to 1, e.g. when the SI prefix changes.
        public static IEnumerator Punch(Transform target, float peak, float duration)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / duration);
                // Fast rise over the first quarter, eased fall over the rest.
                float s = k < 0.25f
                    ? Mathf.Lerp(1f, peak, k / 0.25f)
                    : Mathf.Lerp(peak, 1f, 1f - Mathf.Pow(1f - (k - 0.25f) / 0.75f, 3f));
                target.localScale = Vector3.one * s;
                yield return null;
            }
            target.localScale = Vector3.one;
        }

        public static IEnumerator ScaleTo(Transform target, float scale, float duration)
        {
            Vector3 from = target.localScale, to = Vector3.one * scale;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                target.localScale = Vector3.Lerp(from, to, Mathf.Clamp01(t / duration));
                yield return null;
            }
            target.localScale = to;
        }

        public static IEnumerator Fade(CanvasGroup group, float to, float duration)
        {
            float from = group.alpha, t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                group.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / duration));
                yield return null;
            }
            group.alpha = to;
        }
    }
}
