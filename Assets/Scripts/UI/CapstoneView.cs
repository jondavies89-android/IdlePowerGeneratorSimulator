using TMPro;
using UnityEngine;
using UnityEngine.UI;
using IdlePowerGen.Utils;

namespace IdlePowerGen.UI
{
    // The era's capstone, once its goal is met. A panel opens the moment it happens, saying
    // what has been done and what comes next, with the choice to go now or later. Until the
    // player goes, a gold banner waits above the tabs, on every page; tapping it closes the
    // era and the next one's title card opens.
    public class CapstoneView : MonoBehaviour
    {
        [SerializeField] private Controllers.ContractDesk _desk;
        [SerializeField] private GameObject _panel;
        [SerializeField] private CanvasGroup _panelGroup;
        [SerializeField] private Button _go;
        [SerializeField] private Button _later;
        [SerializeField] private GameObject _banner;
        [SerializeField] private Button _bannerButton;

        private void Awake()
        {
            _panel.SetActive(false);
            _banner.SetActive(false);
            _go.onClick.AddListener(Begin);
            _bannerButton.onClick.AddListener(Begin);
            _later.onClick.AddListener(() => _panel.SetActive(false));
            EventBus.Subscribe<CapstoneReadyEvent>(OnReady);
            EventBus.Subscribe<GameLoadedEvent>(OnLoaded);
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<CapstoneReadyEvent>(OnReady);
            EventBus.Unsubscribe<GameLoadedEvent>(OnLoaded);
        }

        private void OnReady(CapstoneReadyEvent e)
        {
            if (_desk == null || e.Era != _desk.EraNumber) return;
            _panel.SetActive(true);
            _panelGroup.alpha = 0f;
            StartCoroutine(UiTween.Fade(_panelGroup, 1f, 0.5f));
        }

        private void OnLoaded(GameLoadedEvent _) => _panel.SetActive(false);

        public bool PanelShowing => _panel.activeSelf;

        // The last era built has nowhere to go yet: once its end card has been seen, the banner goes.
        private bool _contentEndSeen;

        private void Begin()
        {
            _panel.SetActive(false);
            if (_desk.EraNumber >= Managers.EraManager.LastBuiltEra) _contentEndSeen = true;
            _desk.TakeCapstone();
        }

        private void Update()
        {
            bool show = _desk != null && _desk.IsCurrentEra && _desk.CapstoneReady && !_panel.activeSelf && !_contentEndSeen;
            if (_banner.activeSelf != show) _banner.SetActive(show);
        }
    }
}
