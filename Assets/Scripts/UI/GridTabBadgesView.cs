using System.Linq;
using UnityEngine;

namespace IdlePowerGen.UI
{
    // Dots on an Era 3–5 screen's tabs, as on Era 2's: a client asking with a slot free, a
    // district that could be connected, something to buy or learn in a tree. Trouble on the
    // first tab is the era's own and set by its views (the tab shows red).
    public class GridTabBadgesView : MonoBehaviour
    {
        [SerializeField] private TabBarView _tabs;
        [SerializeField] private Controllers.GridWorksController _works;
        [SerializeField] private int _clientsTab = 1;
        [SerializeField] private UpgradeTreeView[] _trees;
        [SerializeField] private int[] _treeTabs;
        [SerializeField] private Controllers.Era3TownController _town; // Era 3: a district to light
        [SerializeField] private int _townTab;
        [SerializeField] private Color _opportunity = new(0.95f, 0.76f, 0.31f);

        private const float RefreshInterval = 0.3f;
        private float _since = RefreshInterval;

        private void Update()
        {
            _since += Time.unscaledDeltaTime;
            if (_since < RefreshInterval) return;
            _since = 0f;
            if (_works == null || Managers.GameManager.Instance == null) return;

            bool clients = _works.HasFreeSlot && _works.Contracts.Any(_works.IsOffered);
            _tabs.SetBadge(_clientsTab, clients, _opportunity);

            for (int i = 0; i < _trees.Length && i < _treeTabs.Length; i++)
                _tabs.SetBadge(_treeTabs[i], _trees[i] != null && _trees[i].AnythingToBuy(), _opportunity);

            if (_town != null)
            {
                bool district = _town.Areas.Any(a => _town.StateOf(a.Id) == Controllers.Era3TownController.ConnectState.Available);
                _tabs.SetBadge(_townTab, district, _opportunity);
            }
        }
    }
}
