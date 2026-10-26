using TMPro;
using UnityEngine;
using UnityEngine.UI;
using IdlePowerGen.Utils;

namespace IdlePowerGen.UI
{
    // The DEV strip for play-testing: a small tab at the top of the screen that opens a
    // panel to jump to the start of any era (replacing the save) and to run time faster.
    // Not part of the game; remove from the scene builder for a release build.
    public class DevToolsView : MonoBehaviour
    {
        [SerializeField] private Button _toggle;
        [SerializeField] private GameObject _panel;
        [SerializeField] private Button[] _eraButtons; // Era 1, Era 2, …
        [SerializeField] private Slider _speed;
        [SerializeField] private TMP_Text _speedLabel;
        [SerializeField] private Button _realTime;
        [SerializeField] private TMP_Text _eraLabel;
        [SerializeField] private Button _blackout;        // Eras 4–5: the grid goes dark now
        [SerializeField] private Button _meetGoal;        // Eras 2–5: the era's goal met, capstone offered

        private void Awake()
        {
            _panel.SetActive(false);
            _toggle.onClick.AddListener(() => _panel.SetActive(!_panel.activeSelf));
            for (int i = 0; i < _eraButtons.Length; i++)
            {
                int era = i + 1;
                _eraButtons[i].onClick.AddListener(() => JumpTo(era));
                _eraButtons[i].interactable = era <= Managers.EraManager.LastBuiltEra;
            }
            _speed.minValue = 0f;
            _speed.maxValue = 1f;
            _speed.SetValueWithoutNotify(ToSlider(DevTime.Multiplier));
            _speed.onValueChanged.AddListener(v => SetSpeed(FromSlider(v)));
            _realTime.onClick.AddListener(() => { _speed.SetValueWithoutNotify(0f); SetSpeed(1f); });
            SetSpeed(DevTime.Multiplier);
            if (_blackout != null)
                _blackout.onClick.AddListener(() => { CurrentGrid?.ForceBlackout(); _panel.SetActive(false); });
            if (_meetGoal != null)
                _meetGoal.onClick.AddListener(() => { CurrentDesk?.MeetGoalForDev(); _panel.SetActive(false); });
        }

        private static Controllers.ContractDesk CurrentDesk =>
            System.Array.Find(FindObjectsByType<Controllers.ContractDesk>(FindObjectsSortMode.None), d => d.IsCurrentEra);

        private static Controllers.UnitGridController CurrentGrid =>
            System.Array.Find(FindObjectsByType<Controllers.UnitGridController>(FindObjectsSortMode.None), g => g.IsCurrentEra);

        private void Update()
        {
            if (!_panel.activeSelf) return;
            var era = Managers.EraManager.Instance;
            _eraLabel.text = era != null ? $"Now in Era {era.Era}" : string.Empty;
            if (_blackout != null)
            {
                var grid = CurrentGrid;
                _blackout.interactable = grid != null && grid.BlackStart == null;
            }
            if (_meetGoal != null)
            {
                var desk = CurrentDesk;
                _meetGoal.interactable = desk != null && !desk.CapstoneReady;
            }
        }

        private void JumpTo(int era)
        {
            Save.SaveManager.Instance?.StartAtEra(era);
            _panel.SetActive(false);
        }

        private void SetSpeed(float multiplier)
        {
            DevTime.Multiplier = multiplier;
            _speedLabel.text = $"Time ×{DevTime.Multiplier:0}";
        }

        // Logarithmic: the slider's middle is about 30×, so both slow and fast are reachable.
        private static float FromSlider(float v) =>
            Mathf.Round(Mathf.Pow(DevTime.MaxMultiplier, Mathf.Clamp01(v)));

        private static float ToSlider(float multiplier) =>
            Mathf.Log(Mathf.Max(1f, multiplier)) / Mathf.Log(DevTime.MaxMultiplier);
    }
}
