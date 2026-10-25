using System.Linq;
using UnityEngine;

namespace IdlePowerGen.UI
{
    // What earns a dot on each Era 2 tab. Clients: someone is asking and there's a slot to
    // book them in. Works: only trouble (coal, unpaid hands), in red, since a machine to buy
    // is nearly always there and a dot that never goes away means nothing. Improvements:
    // something in the tree can be bought now.
    public class Era2TabBadgesView : MonoBehaviour
    {
        public const int ClientsTab = 0, WorksTab = 1, ImprovementsTab = 2;

        [SerializeField] private TabBarView _tabs;
        [SerializeField] private Controllers.Era2WorksController _works;
        [SerializeField] private UpgradeTreeView _tree;
        [SerializeField] private Color _opportunity = new(0.83f, 0.63f, 0.09f);
        [SerializeField] private Color _alarm       = new(0.90f, 0.22f, 0.21f);

        private const float RefreshInterval = 0.25f;
        private float _since = RefreshInterval;

        private void Update()
        {
            _since += Time.unscaledDeltaTime;
            if (_since < RefreshInterval) return;
            _since = 0f;

            var gm = Managers.GameManager.Instance;
            if (gm == null) return;

            bool clients = _works.HasFreeSlot && _works.Era2Contracts.Any(_works.IsOffered);
            _tabs.SetBadge(ClientsTab, clients, _opportunity);

            bool trouble = _works.ApprenticesUnpaid ||
                           _works.HasCoalEngines &&
                           _works.Hopper.Fraction(_works.CoalBurnCapacityBasis) < Economy.Era2CoalHopper.LowFraction;
            _tabs.SetBadge(WorksTab, trouble, _alarm);

            _tabs.SetBadge(ImprovementsTab, _tree != null && _tree.AnythingToBuy(), _opportunity);
        }
    }
}
