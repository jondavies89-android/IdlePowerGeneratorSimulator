using UnityEngine;
using UnityEngine.EventSystems;

namespace IdlePowerGen.UI
{
    // A button that counts while it's held down (the black start's hydro gates).
    public class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public bool Held { get; private set; }

        public void OnPointerDown(PointerEventData eventData) => Held = true;
        public void OnPointerUp(PointerEventData eventData)   => Held = false;
        public void OnPointerExit(PointerEventData eventData) => Held = false;
        private void OnDisable() => Held = false;
    }
}
