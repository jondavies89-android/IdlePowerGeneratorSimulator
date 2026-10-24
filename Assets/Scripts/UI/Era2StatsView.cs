using TMPro;
using UnityEngine;
using UnityEngine.UI;
using IdlePowerGen.Utils;

namespace IdlePowerGen.UI
{
    // Capacity · Purse · Repute (bible Appendix A, Era 2), and under them how the works'
    // output compares with what its customers are drawing: the era's one new concept.
    public class Era2StatsView : MonoBehaviour
    {
        [SerializeField] private Controllers.Era2WorksController _works;
        [SerializeField] private TMP_Text _capacityLabel;
        [SerializeField] private TMP_Text _purseLabel;
        [SerializeField] private Image[] _stars;
        [SerializeField] private TMP_Text _reputeLabel;
        [SerializeField] private TMP_Text _supplyLabel;
        [SerializeField] private Color _healthy = new(0.30f, 0.69f, 0.31f);
        [SerializeField] private Color _caution = new(1.00f, 0.60f, 0.00f);
        [SerializeField] private Color _alarm   = new(0.90f, 0.22f, 0.21f);
        [SerializeField] private Color _muted   = Color.gray;

        private const float RefreshInterval = 0.1f;
        private float _since = RefreshInterval;
        private decimal _lastPurse = -1m;
        private Coroutine _punch;

        private void Update()
        {
            _since += Time.unscaledDeltaTime;
            if (_since < RefreshInterval) return;
            _since = 0f;

            var gm = Managers.GameManager.Instance;
            if (gm == null) return;

            double capacity = Controllers.Era2WorksController.CapacityWatts;
            _capacityLabel.text = NumberFormatter.FormatWatts(capacity);

            decimal purse = gm.Ledger.Pennies;
            _purseLabel.text = NumberFormatter.FormatPennies(purse);
            // A punch on earning, not on every wage paid out of it.
            if (_lastPurse >= 0 && purse > _lastPurse + 0.5m)
            {
                if (_punch != null) StopCoroutine(_punch);
                _punch = StartCoroutine(UiTween.Punch(_purseLabel.transform, 1.2f, 0.4f));
            }
            _lastPurse = purse;

            float rep = gm.Ledger.Reputation;
            for (int i = 0; i < _stars.Length; i++)
                _stars[i].fillAmount = Mathf.Clamp01(rep / Data.Era2Data.ReputationPerStar - i);
            _reputeLabel.text = $"{rep:0.#} repute";

            double drawn = _works.CommittedWatts;
            if (drawn <= 0)
            {
                _supplyLabel.text = "No customers drawing power. Book an engagement below.";
                _supplyLabel.color = _muted;
            }
            else if (capacity >= drawn)
            {
                _supplyLabel.text = $"Customers draw {NumberFormatter.FormatWatts(drawn)} · " +
                                    $"{NumberFormatter.FormatWatts(capacity - drawn)} to spare";
                _supplyLabel.color = _healthy;
            }
            else
            {
                double ratio = capacity / drawn;
                _supplyLabel.text = $"Customers draw {NumberFormatter.FormatWatts(drawn)} · " +
                                    $"{NumberFormatter.FormatWatts(drawn - capacity)} short. Engagements at risk.";
                _supplyLabel.color = ratio >= 0.5 ? _caution : _alarm;
            }
        }
    }
}
