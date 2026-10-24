using UnityEngine;
using IdlePowerGen.Utils;

namespace IdlePowerGen.UI
{
    // One scene, one screen per era (todo Era 2 §2.2, option B): the workshop until the
    // first shilling, the demonstration office from then on.
    public class EraScreenSwitcher : MonoBehaviour
    {
        [SerializeField] private GameObject _era1Screen;
        [SerializeField] private GameObject _era2Screen;

        private void Awake()
        {
            EventBus.Subscribe<EraAdvancedEvent>(OnEraAdvanced);
            EventBus.Subscribe<GameLoadedEvent>(OnGameLoaded);
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<EraAdvancedEvent>(OnEraAdvanced);
            EventBus.Unsubscribe<GameLoadedEvent>(OnGameLoaded);
        }

        private void Start() => Apply();

        private void OnEraAdvanced(EraAdvancedEvent _) => Apply();
        private void OnGameLoaded(GameLoadedEvent _)   => Apply();

        private void Apply()
        {
            var era = Managers.EraManager.Instance;
            bool era2 = era != null && era.CurrentEra >= 2f;
            _era1Screen.SetActive(!era2);
            _era2Screen.SetActive(era2);
        }
    }
}
