using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using IdlePowerGen.Utils;

namespace IdlePowerGen.UI
{
    // Spawns a row per generator; a row appears only once its unlock upgrade is bought,
    // so the first screen carries one concept only (the crank). The section header
    // stays hidden until there is a machine to show.
    public class GeneratorListView : MonoBehaviour
    {
        [SerializeField] private GeneratorRowView _rowPrefab;
        [SerializeField] private Transform _content;
        [SerializeField] private GameObject _sectionHeader;
        [SerializeField] private Controllers.Era2GeneratorController _controller;

        private readonly List<GeneratorRowView> _rows = new();

        private void Awake()
        {
            EventBus.Subscribe<UpgradePurchasedEvent>(OnUpgradePurchased);
            EventBus.Subscribe<GameLoadedEvent>(OnGameLoaded);
        }

        private void Start()
        {
            var gm = Managers.GameManager.Instance;
            if (gm == null) return;

            foreach (var def in gm.GeneratorCatalogue.OrderBy(g => g.baseCostJoules))
            {
                var row = Instantiate(_rowPrefab, _content);
                row.name = "Generator_" + def.generatorType;
                row.Bind(def, _controller);
                _rows.Add(row);
            }
            RefreshVisibility();
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<UpgradePurchasedEvent>(OnUpgradePurchased);
            EventBus.Unsubscribe<GameLoadedEvent>(OnGameLoaded);
        }

        private void OnUpgradePurchased(UpgradePurchasedEvent _) => RefreshVisibility();
        private void OnGameLoaded(GameLoadedEvent _)             => RefreshVisibility();

        private void RefreshVisibility()
        {
            bool any = false;
            foreach (var row in _rows)
            {
                bool show = _controller.IsUnlocked(row.Definition);
                row.gameObject.SetActive(show);
                any |= show;
            }
            if (_sectionHeader != null) _sectionHeader.SetActive(any);
        }
    }
}
