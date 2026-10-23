using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace IdlePowerGen.UI
{
    // A temporary message panel. Messages queue so two milestones crossed in one tick
    // are both read rather than one overwriting the other.
    public class ToastView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private TMP_Text _label;
        [SerializeField] private float _holdSeconds = 3.5f;
        [SerializeField] private float _fadeSeconds = 0.35f;

        private readonly Queue<string> _queue = new();
        private bool _showing;

        public bool IsShowing => _showing;
        public string CurrentText => _label.text;

        private void Awake()
        {
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
        }

        public void Show(string message)
        {
            _queue.Enqueue(message);
            if (!_showing) StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            _showing = true;
            while (_queue.Count > 0)
            {
                _label.text = _queue.Dequeue();
                yield return UiTween.Fade(_group, 1f, _fadeSeconds);
                yield return new WaitForSecondsRealtime(_holdSeconds);
                yield return UiTween.Fade(_group, 0f, _fadeSeconds);
            }
            _showing = false;
        }
    }
}
