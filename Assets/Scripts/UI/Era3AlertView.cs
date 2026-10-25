using TMPro;
using UnityEngine;
using UnityEngine.UI;
using IdlePowerGen.Data;

namespace IdlePowerGen.UI
{
    // "Bond St lamps flickering — voltage sag on the long run — line upgrade £600" (bible
    // Appendix A, Era 3): the worst-served district, with the one-tap fix (CLAUDE.md: every
    // alert offers a way forward). Tapping it buys heavier copper for that district.
    public class Era3AlertView : MonoBehaviour
    {
        [SerializeField] private Controllers.Era3TownController _town;
        [SerializeField] private GameObject _panel;
        [SerializeField] private TMP_Text _titleLabel;
        [SerializeField] private TMP_Text _detailLabel;
        [SerializeField] private Button _button;

        private const float RefreshInterval = 0.25f;
        private float _since = RefreshInterval;
        private string _id;

        private void Awake() => _button.onClick.AddListener(() => { if (_id != null) _town.UpgradeCopper(_id); });

        private void Update()
        {
            _since += Time.unscaledDeltaTime;
            if (_since < RefreshInterval) return;
            _since = 0f;
            if (_town == null) return;

            var worst = _town.WorstFlicker;
            bool show = worst != null;
            if (_panel.activeSelf != show) _panel.SetActive(show);
            if (!show) { _id = null; return; }

            _id = worst.Id;
            var spec = _town.Spec(worst.Id);
            _titleLabel.text = $"{spec.Name} lamps flickering";
            if (_town.CanUpgradeCopper(worst.Id))
            {
                decimal cost = _town.CopperCost(worst.Id);
                var gm = Managers.GameManager.Instance;
                _detailLabel.text = $"voltage sag on the long run ({worst.LossFraction:0%} lost) — heavier copper " +
                                    Managers.Money.Format(cost, Era3Data.EraNumber);
                _button.interactable = gm != null && gm.Ledger.Pennies >= cost;
            }
            else
            {
                _detailLabel.text = $"voltage sag on the long run ({worst.LossFraction:0%} lost) — only higher voltage will help now";
                _button.interactable = false;
            }
        }
    }
}
