using UnityEngine;
using IdlePowerGen.Utils;

namespace IdlePowerGen.UI
{
    // One scene, one screen per era (todo Era 2 §2.2, option B): the workshop until the
    // first shilling, the demonstration office from then on, and each later era's screen
    // in turn. Index 0 is Era 1's screen; an era past the last screen keeps the last one.
    public class EraScreenSwitcher : MonoBehaviour
    {
        [SerializeField] private GameObject[] _screens;

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
            int index = Mathf.Clamp((era != null ? era.Era : 1) - 1, 0, _screens.Length - 1);
            for (int i = 0; i < _screens.Length; i++)
                if (_screens[i] != null && _screens[i].activeSelf != (i == index)) _screens[i].SetActive(i == index);
        }
    }
}
