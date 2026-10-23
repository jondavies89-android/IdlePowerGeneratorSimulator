using UnityEngine;
using IdlePowerGen.Utils;

namespace IdlePowerGen.UI
{
    // Routes milestone and welcome-back messages to the toast (todo §6, bible §7.6).
    public class Era1MilestoneToastPresenter : MonoBehaviour
    {
        [SerializeField] private ToastView _toast;

        // Short catch-ups are a few seconds of ticking; a toast for those would be noise.
        private const double MinSecondsAwayWorthMentioning = 60;

        private void Awake()
        {
            EventBus.Subscribe<MilestoneReachedEvent>(OnMilestone);
            EventBus.Subscribe<OfflineProgressEvent>(OnOffline);
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<MilestoneReachedEvent>(OnMilestone);
            EventBus.Unsubscribe<OfflineProgressEvent>(OnOffline);
        }

        private void OnMilestone(MilestoneReachedEvent e) => _toast.Show(e.Message);

        private void OnOffline(OfflineProgressEvent e)
        {
            if (e.SecondsAway < MinSecondsAwayWorthMentioning || e.JoulesEarned <= 0) return;
            string flavor = string.IsNullOrEmpty(e.FlavorText) ? "While you were away" : e.FlavorText;
            _toast.Show($"{flavor}\n+{NumberFormatter.FormatJoules(e.JoulesEarned)}");
        }
    }
}
