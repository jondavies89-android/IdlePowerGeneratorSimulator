using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace IdlePowerGen.UI
{
    // The contract board: who is asking for power now, then who asks later today, then the
    // next client the works' repute would open. Clients further up the reputation ladder
    // stay hidden, so the board never shows the whole era at once.
    public class Era2OfferListView : MonoBehaviour
    {
        [SerializeField] private Controllers.Era2WorksController _works;
        [SerializeField] private Era2OfferCardView _cardPrefab;
        [SerializeField] private Transform _content;

        private readonly List<Era2OfferCardView> _cards = new();

        private const float RefreshInterval = 0.25f;
        private float _since = RefreshInterval;

        private void Start()
        {
            foreach (var def in _works.Era2Contracts.OrderBy(c => c.requiredReputation).ThenBy(c => c.requiredWatts))
            {
                var card = Instantiate(_cardPrefab, _content);
                card.name = "Offer_" + def.contractId;
                card.Bind(def, _works);
                _cards.Add(card);
            }
        }

        private void Update()
        {
            _since += Time.unscaledDeltaTime;
            if (_since < RefreshInterval) return;
            _since = 0f;

            var gm = Managers.GameManager.Instance;
            if (gm == null) return;

            // Only the cheapest locked client is teased.
            float rep = gm.Ledger.Reputation;
            var nextLocked = _cards.Select(c => c.Definition)
                                   .Where(d => d.requiredReputation > rep)
                                   .OrderBy(d => d.requiredReputation)
                                   .FirstOrDefault();

            foreach (var card in _cards) card.Evaluate(card.Definition == nextLocked);

            var order = _cards.Where(c => c.Current != Era2OfferCardView.State.Hidden)
                              .OrderBy(c => c.Current)
                              .ThenBy(c => c.HoursUntilOpen)
                              .ToList();
            foreach (var card in _cards)
            {
                bool show = card.Current != Era2OfferCardView.State.Hidden;
                if (card.gameObject.activeSelf != show) card.gameObject.SetActive(show);
            }
            for (int i = 0; i < order.Count; i++)
                if (order[i].transform.GetSiblingIndex() != i) order[i].transform.SetSiblingIndex(i);
        }
    }
}
