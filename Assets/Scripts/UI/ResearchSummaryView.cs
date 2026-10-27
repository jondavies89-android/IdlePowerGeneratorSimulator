using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using IdlePowerGen.Models;

namespace IdlePowerGen.UI
{
    // "Research in progress" (bible Appendix A, Era 3), as the player's own choice: research
    // points pile up whatever happens, and this shows the topic the player is saving them for
    // (whichever is picked in the era's research tree), the whole balance against its price,
    // and roughly when, at the current rate. Nothing is learnt until the player learns it.
    public class ResearchSummaryView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _nameLabel;
        [SerializeField] private TMP_Text _pointsLabel;
        [SerializeField] private Image _fill;
        [SerializeField] private TMP_Text _etaLabel;
        [SerializeField] private int _era = 3;
        // What to say with no R&D running yet (Era 3 also has the galvanometer's readings).
        [SerializeField] private string _noRateHint = "build an R&D workshop to make research";
        // Where the choosing is done, as seen from here.
        [SerializeField] private string _chooseHint = "tap any topic below to save for it instead";

        private const float RefreshInterval = 0.25f;
        private float _since = RefreshInterval;
        private UpgradeTreeView _tree;

        private void Update()
        {
            _since += Time.unscaledDeltaTime;
            if (_since < RefreshInterval) return;
            _since = 0f;

            var gm = Managers.GameManager.Instance;
            if (gm == null) return;
            var target = Target(gm);
            double rp = gm.Ledger.ResearchPoints;
            var applier = Managers.UpgradeEffectApplier.Instance;
            double rate = applier != null ? applier.ResearchPerHour : 0;

            if (target == null)
            {
                _nameLabel.text = "Everything known for now";
                _pointsLabel.text = $"{rp:N0} RP";
                _fill.fillAmount = 1f;
                _etaLabel.text = "the next era's questions are still to come";
                return;
            }

            double cost = gm.Upgrades.NextCostResearch(target);
            bool open = gm.Upgrades.PrerequisitesMet(target);
            _nameLabel.text = target.displayName;
            _pointsLabel.text = $"{rp:N0} / {cost:N0} RP";
            _fill.fillAmount = cost > 0 ? Mathf.Clamp01((float)(rp / cost)) : 1f;
            if (!open) _etaLabel.text = "learn what it builds on first · " + _chooseHint;
            else if (rp >= cost) _etaLabel.text = "enough banked: learn it in the tree, or keep saving for another";
            else if (rate <= 0) _etaLabel.text = $"{_noRateHint} · {_chooseHint}";
            else _etaLabel.text = $"≈ {Duration((cost - rp) / rate)} at the current rate · {_chooseHint}";
        }

        // The topic picked in the era's research tree; the cheapest open one if there's no tree.
        private UpgradeDefinition Target(Managers.GameManager gm)
        {
            if (_tree == null)
                _tree = FindObjectsByType<UpgradeTreeView>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                            .FirstOrDefault(t => t.IsResearch && t.Era == _era);
            var picked = _tree != null ? _tree.Selected : null;
            return picked != null && !gm.Upgrades.IsMaxed(picked) ? picked : Next(gm, _era);
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
