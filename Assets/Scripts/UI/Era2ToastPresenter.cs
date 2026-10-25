using UnityEngine;
using IdlePowerGen.Utils;

namespace IdlePowerGen.UI
{
    // Era 2's news on the shared toast: engagements paid or failed, trouble at the works,
    // and what the customers paid while the player was away.
    public class Era2ToastPresenter : MonoBehaviour
    {
        [SerializeField] private ToastView _toast;

        private const double MinSecondsAwayWorthMentioning = 60;

        private void Awake()
        {
            EventBus.Subscribe<ContractCompletedEvent>(OnCompleted);
            EventBus.Subscribe<ContractFailedEvent>(OnFailed);
            EventBus.Subscribe<WorksNoticeEvent>(OnNotice);
            EventBus.Subscribe<OfflineProgressEvent>(OnOffline);
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<ContractCompletedEvent>(OnCompleted);
            EventBus.Unsubscribe<ContractFailedEvent>(OnFailed);
            EventBus.Unsubscribe<WorksNoticeEvent>(OnNotice);
            EventBus.Unsubscribe<OfflineProgressEvent>(OnOffline);
        }

        private static bool IsEra2Contract(Economy.ContractInstance c) =>
            c.Definition.minEra == Data.Era2Data.EraNumber;

        private void OnCompleted(ContractCompletedEvent e)
        {
            if (!IsEra2Contract(e.Contract)) return;
            var def = e.Contract.Definition;
            float rep = def.reputationOnSuccess; // a kept engagement earns all of it
            // Most of the fee came in while it ran; the toast gives the whole of it.
            _toast.Show($"{def.clientName} paid {NumberFormatter.FormatPennies(e.PenniesEarned)} in all  ·  +{rep:0.#} repute");
        }

        private void OnFailed(ContractFailedEvent e)
        {
            if (!IsEra2Contract(e.Contract)) return;
            var def = e.Contract.Definition;
            // Cancelled part-way when the shortfall countdown ran out; otherwise judged at the end.
            _toast.Show(e.Contract.IsDue
                ? $"{def.clientName} went short of power and paid only part.  {def.reputationOnFail:0.#} repute"
                : $"{def.clientName} cancelled after going short of power too long.  {def.reputationOnFail:0.#} repute");
        }

        private void OnNotice(WorksNoticeEvent e) => _toast.Show(e.Message);

        private void OnOffline(OfflineProgressEvent e)
        {
            if (e.SecondsAway < MinSecondsAwayWorthMentioning || e.PenniesEarned <= 0) return;
            string flavor = string.IsNullOrEmpty(e.FlavorText) ? "While you were away" : e.FlavorText;
            _toast.Show($"{flavor}\nYour customers paid {NumberFormatter.FormatPennies(e.PenniesEarned)}");
        }
    }
}
