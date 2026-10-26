using TMPro;
using UnityEngine;
using UnityEngine.UI;
using IdlePowerGen.Utils;

namespace IdlePowerGen.UI
{
    // The era's goal bar: the most it has delivered at once against the era's end output
    // (bible §2.1). Once reached it stays full; the capstone itself is the banner above the
    // tabs (CapstoneView).
    public class GoalView : MonoBehaviour
    {
        [SerializeField] private Controllers.GridWorksController _works;
        [SerializeField] private TMP_Text _label;
        [SerializeField] private Image _fill;
        [SerializeField] private string _goalText = "Lit at once";

        private const float RefreshInterval = 0.2f;
        private float _since = RefreshInterval;

        private void Update()
        {
            _since += Time.unscaledDeltaTime;
            if (_since < RefreshInterval) return;
            _since = 0f;
            if (_works == null) return;

            double goal = _works.GoalWatts;
            double best = _works.BestDeliveredWatts;
            _label.text = _works.CapstoneReady
                ? $"{_goalText}: {NumberFormatter.FormatWatts(goal)} reached. The next era waits on you."
                : $"{_goalText}: best {NumberFormatter.FormatWatts(best)} of {NumberFormatter.FormatWatts(goal)}";
            _fill.fillAmount = _works.CapstoneReady ? 1f : goal > 0 ? Mathf.Clamp01((float)(best / goal)) : 0f;
        }
    }
}
