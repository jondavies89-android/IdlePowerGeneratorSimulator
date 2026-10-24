using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace IdlePowerGen.UI
{
    // The engagements running now, one card each, and how many more the works can take.
    public class Era2EngagementListView : MonoBehaviour
    {
        [SerializeField] private Controllers.Era2WorksController _works;
        [SerializeField] private Era2EngagementCardView _cardPrefab;
        [SerializeField] private Transform _content;
        [SerializeField] private TMP_Text _headerLabel;
        [SerializeField] private GameObject _emptyHint;

        private readonly List<Era2EngagementCardView> _cards = new();

        // Polled: contracts start and end inside the sim tick, and a handful of cards is cheap.
        private void Update()
        {
            var running = _works.Engagements.ToList();

            while (_cards.Count < running.Count)
            {
                var card = Instantiate(_cardPrefab, _content);
                card.name = "Engagement_" + _cards.Count;
                _cards.Add(card);
            }
            for (int i = 0; i < _cards.Count; i++)
            {
                bool used = i < running.Count;
                if (_cards[i].gameObject.activeSelf != used) _cards[i].gameObject.SetActive(used);
                if (used && _cards[i].Contract != running[i]) _cards[i].Bind(running[i]);
            }

            _headerLabel.text = $"Engagements  ·  {running.Count} of {_works.Slots}";
            _emptyHint.SetActive(running.Count == 0);
        }
    }
}
