using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using IdlePowerGen.Utils;

namespace IdlePowerGen.UI
{
    // "+2.00 J" rising from the crank on each tap: the Era 1 concept (effort makes power)
    // shown at the point of effort. Labels are pooled because taps arrive at 3–5 Hz.
    public class Era1TapPopView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _template;
        [SerializeField] private float _rise = 90f;
        [SerializeField] private float _duration = 0.7f;

        private readonly Queue<TMP_Text> _pool = new();

        private void Awake()
        {
            _template.gameObject.SetActive(false);
            EventBus.Subscribe<TapEvent>(OnTap);
        }

        private void OnDestroy() => EventBus.Unsubscribe<TapEvent>(OnTap);

        private void OnTap(TapEvent evt)
        {
            // From Era 2 the crank is turned from the old-workshop row and this view is hidden.
            if (!isActiveAndEnabled) return;
            var label = _pool.Count > 0 ? _pool.Dequeue() : Instantiate(_template, _template.transform.parent);
            label.text = "+" + NumberFormatter.FormatJoules(evt.JoulesProduced);
            label.gameObject.SetActive(true);
            StartCoroutine(Rise(label));
        }

        private IEnumerator Rise(TMP_Text label)
        {
            var rt = (RectTransform)label.transform;
            // A little sideways scatter so rapid taps don't stack into one blur.
            Vector2 start = _template.rectTransform.anchoredPosition + new Vector2(Random.Range(-40f, 40f), 0f);
            float t = 0f;
            while (t < _duration)
            {
                t += Time.unscaledDeltaTime;
                float k = t / _duration;
                rt.anchoredPosition = start + Vector2.up * (_rise * k);
                label.alpha = 1f - k * k;
                yield return null;
            }
            label.gameObject.SetActive(false);
            _pool.Enqueue(label);
        }
    }
}
