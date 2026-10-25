using UnityEngine;
using UnityEngine.UI;
using IdlePowerGen.Utils;

namespace IdlePowerGen.UI
{
    // The title card that opens an era (Era 3 on): the era's name, date and one paragraph
    // of story, shown once on the transition itself. Reopening the game goes straight to
    // the era's screen.
    public class EraTitleCardView : MonoBehaviour
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private Button _continueButton;
        [SerializeField] private int _toEra = 3;
        // The card after the last built era's capstone ("Era 6 is still being built").
        [SerializeField] private bool _contentEnd;

        private void Awake()
        {
            _panel.SetActive(false);
            _continueButton.onClick.AddListener(() => _panel.SetActive(false));
            EventBus.Subscribe<EraAdvancedEvent>(OnEraAdvanced);
            EventBus.Subscribe<GameLoadedEvent>(OnGameLoaded);
            EventBus.Subscribe<ContentEndEvent>(OnContentEnd);
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<EraAdvancedEvent>(OnEraAdvanced);
            EventBus.Unsubscribe<GameLoadedEvent>(OnGameLoaded);
            EventBus.Unsubscribe<ContentEndEvent>(OnContentEnd);
        }

        private void OnContentEnd(ContentEndEvent _)
        {
            if (_contentEnd) Show();
        }

        private void OnEraAdvanced(EraAdvancedEvent e)
        {
            if (!_contentEnd && e.ToEra == _toEra) Show();
        }

        private void OnGameLoaded(GameLoadedEvent _) => _panel.SetActive(false);

        public bool Showing => _panel.activeSelf;

        private void Show()
        {
            _panel.SetActive(true);
            _group.alpha = 0f;
            StartCoroutine(UiTween.Fade(_group, 1f, 0.6f));
        }
    }
}
