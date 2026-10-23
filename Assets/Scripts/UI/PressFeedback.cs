using UnityEngine;
using UnityEngine.EventSystems;

namespace IdlePowerGen.UI
{
    // Brief scale-down while a button is held, so every tap feels physical.
    public class PressFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private float _pressedScale = 0.92f;
        [SerializeField] private float _duration = 0.06f;

        private Coroutine _running;

        public void OnPointerDown(PointerEventData _) => Animate(_pressedScale);
        public void OnPointerUp(PointerEventData _)   => Animate(1f);

        private void OnDisable() => transform.localScale = Vector3.one;

        private void Animate(float scale)
        {
            if (_running != null) StopCoroutine(_running);
            _running = StartCoroutine(UiTween.ScaleTo(transform, scale, _duration));
        }
    }
}
