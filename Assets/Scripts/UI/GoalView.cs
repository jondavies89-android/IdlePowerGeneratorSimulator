using TMPro;
using UnityEngine;
using UnityEngine.UI;
using IdlePowerGen.Utils;

namespace IdlePowerGen.UI
{
    // The era's goal bar: the most it has delivered at once against the era's end output
    // (bible §2.1). Once reached, the bar gives way to the capstone button that closes the era.
    public class GoalView : MonoBehaviour
    {
        [SerializeField] private Controllers.GridWorksController _works;
        [SerializeField] private TMP_Text _label;
        [SerializeField] private Image _fill;
        [SerializeField] private GameObject _barRow;
        [SerializeField] private Button _capstone;
        [SerializeField] private string _goalText = "Lit at once";

        private const float RefreshInterval = 0.2f;
        private float _since = RefreshInterval;

        private void Awake() => _capstone.onClick.AddListener(() => _works.TakeCapstone());

        private void Update()
        {
            _since += Time.unscaledDeltaTime;
            if (_since < RefreshInterval) return;
            _since = 0f;
            if (_works == null) return;

            double goal = _works.GoalWatts;
            double best = _works.BestDeliveredWatts;
            bool ready = _works.CapstoneReady;
            _label.text = ready
                ? $"{_goalText}: {NumberFormatter.FormatWatts(goal)}. The town is waiting."
                : $"{_goalText}: best {NumberFormatter.FormatWatts(best)} of {NumberFormatter.FormatWatts(goal)}";
            _fill.fillAmount = goal > 0 ? Mathf.Clamp01((float)(best / goal)) : 0f;
            if (_capstone.gameObject.activeSelf != ready) _capstone.gameObject.SetActive(ready);
            if (_barRow != null && _barRow.activeSelf == ready) _barRow.SetActive(!ready);
        }
    }
}
