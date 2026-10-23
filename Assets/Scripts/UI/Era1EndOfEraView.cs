using UnityEngine;
using UnityEngine.UI;
using IdlePowerGen.Utils;

namespace IdlePowerGen.UI
{
    // Shown when the first shilling arrives (EraAdvancedEvent 1 → 2). Era 2 isn't built
    // yet, so this is the end of the playable slice: keep tinkering, or start again.
    public class Era1EndOfEraView : MonoBehaviour
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private Button _continueButton;
        [SerializeField] private Button _restartButton;

        private void Awake()
        {
            _panel.SetActive(false);
            _continueButton.onClick.AddListener(() => _panel.SetActive(false));
            _restartButton.onClick.AddListener(Restart);
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

        // Reopening a finished game lands on the same screen rather than a silent workshop.
        private void OnGameLoaded(GameLoadedEvent _)
        {
            var era = Managers.EraManager.Instance;
            if (era != null && era.CurrentEra >= 2f) Show();
            else _panel.SetActive(false);
        }

        private void Show()
        {
            _panel.SetActive(true);
            _group.alpha = 0f;
            StartCoroutine(UiTween.Fade(_group, 1f, 0.6f));
        }

        private void Restart()
        {
            Save.SaveManager.Instance?.ResetProgress();
            _panel.SetActive(false);
        }
    }
}
