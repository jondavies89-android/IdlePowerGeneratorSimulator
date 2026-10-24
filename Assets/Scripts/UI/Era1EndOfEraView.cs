using UnityEngine;
using UnityEngine.UI;
using IdlePowerGen.Utils;

namespace IdlePowerGen.UI
{
    // The title card between eras (todo Era 2 §8), shown when the first shilling arrives
    // (EraAdvancedEvent 1 → 2): "The Demonstration — 9th October 1851". Shown once, on the
    // transition itself; reopening the game goes straight to the Era 2 office.
    public class Era1EndOfEraView : MonoBehaviour
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private Button _continueButton;

        private void Awake()
        {
            _panel.SetActive(false);
            _continueButton.onClick.AddListener(() => _panel.SetActive(false));
            EventBus.Subscribe<EraAdvancedEvent>(OnEraAdvanced);
            EventBus.Subscribe<GameLoadedEvent>(OnGameLoaded);
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<EraAdvancedEvent>(OnEraAdvanced);
            EventBus.Unsubscribe<GameLoadedEvent>(OnGameLoaded);
        }

        private void OnEraAdvanced(EraAdvancedEvent e)
        {
            if (e.FromEra == 1) Show();
        }

        private void OnGameLoaded(GameLoadedEvent _) => _panel.SetActive(false);

        private void Show()
        {
            _panel.SetActive(true);
            _group.alpha = 0f;
            StartCoroutine(UiTween.Fade(_group, 1f, 0.6f));
        }
    }
}
