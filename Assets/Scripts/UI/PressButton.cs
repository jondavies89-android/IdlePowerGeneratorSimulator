using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace IdlePowerGen.UI
{
    // Fires as the finger goes down, not when it lifts: for taps that are about timing (the
    // galvanometer's reading) or rhythm (stoking), where a Button's release comes too late.
    // The Button stays for its look and its interactable state; don't also listen to onClick.
    [RequireComponent(typeof(Button))]
    public class PressButton : MonoBehaviour, IPointerDownHandler
    {
        public event Action Pressed;

        private Button _button;

        private void Awake() => _button = GetComponent<Button>();

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || !_button.IsInteractable()) return;
            Pressed?.Invoke();
        }

        // Tests and anything else that needs to press it.
        public void Press()
        {
            if (_button == null) _button = GetComponent<Button>();
            if (_button.IsInteractable()) Pressed?.Invoke();
        }

        public static PressButton On(Button button) =>
            button.TryGetComponent<PressButton>(out var press) ? press : button.gameObject.AddComponent<PressButton>();
    }
}
