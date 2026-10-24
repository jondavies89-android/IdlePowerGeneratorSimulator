using UnityEngine;
using UnityEngine.UI;
using IdlePowerGen.Utils;

namespace IdlePowerGen.UI
{
    // Shown when the Great Exhibition is lit (EraAdvancedEvent 2 → 3). Era 3 isn't built,
    // so this is the end of the playable slice: keep running the works, or start again.
    public class Era2EndOfEraView : MonoBehaviour
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
            if (e.FromEra == 2) Show();
        }

        // Reopening a finished game lands on the same card rather than a silent office.
        private void OnGameLoaded(GameLoadedEvent _)
        {
            var era = Managers.EraManager.Instance;
            if (era != null && era.CurrentEra >= 3f) Show();
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
