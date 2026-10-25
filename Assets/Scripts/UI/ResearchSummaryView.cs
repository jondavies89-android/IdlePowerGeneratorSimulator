using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using IdlePowerGen.Models;

namespace IdlePowerGen.UI
{
    // "Research in progress" (bible Appendix A, Era 3): the next piece of research the
    // points are going towards, how far along, and roughly when, at the current rate.
    // The next target is the cheapest one whose prerequisites are known.
    public class ResearchSummaryView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _nameLabel;
        [SerializeField] private TMP_Text _pointsLabel;
        [SerializeField] private Image _fill;
        [SerializeField] private TMP_Text _etaLabel;
        [SerializeField] private int _era = 3;

        private const float RefreshInterval = 0.25f;
        private float _since = RefreshInterval;

        private void Update()
        {
            _since += Time.unscaledDeltaTime;
            if (_since < RefreshInterval) return;
            _since = 0f;

            var gm = Managers.GameManager.Instance;
            if (gm == null) return;
            var next = Next(gm, _era);
            double rp = gm.Ledger.ResearchPoints;
            var applier = Managers.UpgradeEffectApplier.Instance;
            double rate = applier != null ? applier.ResearchPerHour : 0;

            if (next == null)
            {
                _nameLabel.text = "Everything known for now";
                _pointsLabel.text = $"{rp:N0} RP";
                _fill.fillAmount = 1f;
                _etaLabel.text = "the next era's questions are still to come";
                return;
            }

            double cost = gm.Upgrades.NextCostResearch(next);
            _nameLabel.text = next.displayName;
            _pointsLabel.text = $"{System.Math.Min(rp, cost):N0} / {cost:N0} RP";
            _fill.fillAmount = cost > 0 ? Mathf.Clamp01((float)(rp / cost)) : 1f;
            if (rp >= cost) _etaLabel.text = "ready: learn it on the Research tab";
            else if (rate <= 0) _etaLabel.text = "build an R&D workshop to make research";
            else _etaLabel.text = $"≈ {Duration((cost - rp) / rate)} at the current rate";
        }

        public static UpgradeDefinition Next(Managers.GameManager gm, int era) =>
            gm.UpgradeCatalogue.Where(u => u.isResearch && u.minEra == era && !gm.Upgrades.IsMaxed(u) &&
                                           gm.Upgrades.PrerequisitesMet(u))
                               .OrderBy(u => gm.Upgrades.NextCostResearch(u)).FirstOrDefault();

        // Game hours, as the bible's mockup reads: "4h 20m".
        private static string Duration(double hours)
        {
            int h = (int)hours, m = (int)((hours - h) * 60);
            return h > 0 ? $"{h}h {m:00}m" : $"{System.Math.Max(1, m)}m";
        }
    }
}
