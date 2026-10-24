using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using IdlePowerGen.Models;
using IdlePowerGen.Utils;

namespace IdlePowerGen.UI
{
    // Spawns one card per upgrade in the catalogue and decides which are on screen.
    // Fully levelled upgrades disappear. The rest show once they are at most one step
    // away (their prerequisites' prerequisites are bought), so a locked "Water Wheel"
    // can hint at the river without the whole tree appearing at minute one.
    public class UpgradeListView : MonoBehaviour
    {
        [SerializeField] private UpgradeCardView _cardPrefab;
        [SerializeField] private Transform _content;
        [SerializeField] private GameObject _emptyHint;

        private readonly List<UpgradeCardView> _cards = new();

        private void Awake()
        {
            EventBus.Subscribe<UpgradePurchasedEvent>(OnUpgradePurchased);
            EventBus.Subscribe<GameLoadedEvent>(OnGameLoaded);
        }

        private void Start()
        {
            var gm = Managers.GameManager.Instance;
            if (gm == null) return;

            foreach (var def in gm.UpgradeCatalogue.OrderBy(u => u.costJoules))
            {
                var card = Instantiate(_cardPrefab, _content);
                card.name = "Upgrade_" + def.upgradeId;
                card.Bind(def);
                _cards.Add(card);
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
            var gm = Managers.GameManager.Instance;
            if (gm == null) return;

            bool any = false;
            foreach (var card in _cards)
            {
                bool show = !gm.Upgrades.IsMaxed(card.Definition) && IsNear(card.Definition, gm);
                card.gameObject.SetActive(show);
                any |= show;
            }
            if (_emptyHint != null) _emptyHint.SetActive(!any);
        }

        private static bool IsNear(UpgradeDefinition def, Managers.GameManager gm)
        {
            foreach (var id in def.requiredUpgradeIds)
            {
                if (gm.Upgrades.IsPurchased(id)) continue;
                var prereq = gm.UpgradeCatalogue.FirstOrDefault(u => u.upgradeId == id);
                if (prereq == null || !prereq.requiredUpgradeIds.All(gm.Upgrades.IsPurchased))
                    return false;
            }
            return true;
        }
    }
}
