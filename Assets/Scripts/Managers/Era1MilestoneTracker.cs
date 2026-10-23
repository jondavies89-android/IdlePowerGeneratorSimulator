using System.Collections.Generic;
using UnityEngine;
using IdlePowerGen.Utils;

namespace IdlePowerGen.Managers
{
    // Todo §6 — fires each Era 1 milestone once, when stored charge first reaches it.
    // Reached ids are saved so a reload above 500 J doesn't knock on the door again.
    public class Era1MilestoneTracker : MonoBehaviour
    {
        public static Era1MilestoneTracker Instance { get; private set; }

        private readonly HashSet<string> _reached = new();

        public IReadOnlyCollection<string> Reached => _reached;
        public bool HasReached(string id) => _reached.Contains(id);

        private void Awake()
        {
            if (Instance != null) { Destroy(this); return; }
            Instance = this;
            EventBus.Subscribe<CurrencyChangedEvent>(OnCurrencyChanged);
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            EventBus.Unsubscribe<CurrencyChangedEvent>(OnCurrencyChanged);
        }

        private void OnCurrencyChanged(CurrencyChangedEvent e)
        {
            if (e.Currency != CurrencyType.Joules) return;

            foreach (var m in Data.Era1Data.Milestones)
            {
                if (e.NewValue < m.StoredJoules || !_reached.Add(m.Id)) continue;
                EventBus.Publish(new MilestoneReachedEvent(m.Id, m.Message));
            }
        }

        public void LoadFromSave(IEnumerable<string> ids)
        {
            _reached.Clear();
            if (ids == null) return;
            foreach (var id in ids) _reached.Add(id);
        }
    }
}
