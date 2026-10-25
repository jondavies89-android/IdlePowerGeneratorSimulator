using TMPro;
using UnityEngine;
using UnityEngine.UI;
using IdlePowerGen.Utils;

namespace IdlePowerGen.UI
{
    // The cascade warning (bible §10.2): the frequency is low and falling, and the countdown
    // to the grid going dark is running. Two one-tap remedies (CLAUDE.md): bring on a unit
    // that's waiting, or drop the biggest feeder. Amber with time in hand, red near the end.
    public class CascadeBannerView : MonoBehaviour
    {
        [SerializeField] private Controllers.UnitGridController _grid;
        [SerializeField] private GameObject _panel;
        [SerializeField] private Image _background;
        [SerializeField] private TMP_Text _titleLabel;
        [SerializeField] private TMP_Text _detailLabel;
        [SerializeField] private Button _syncButton;
        [SerializeField] private TMP_Text _syncLabel;
        [SerializeField] private Button _shedButton;
        [SerializeField] private TMP_Text _shedLabel;
        [SerializeField] private SyncOverlayView _sync;
        [SerializeField] private Color _cautionPanel = new(0.3f, 0.2f, 0.05f);
        [SerializeField] private Color _alarmPanel   = new(0.35f, 0.08f, 0.06f);

        private void Awake()
        {
            _syncButton.onClick.AddListener(() => { var u = _grid.ReserveToSync; if (u != null) _sync.Open(u); });
            _shedButton.onClick.AddListener(() => { var a = _grid.BiggestLit; if (a != null) _grid.SetShed(a.Id, true); });
        }

        private void Update()
        {
            if (_grid == null) return;
            double left = _grid.CascadeSecondsLeft;
            bool show = left >= 0 && _grid.BlackStart == null;
            if (_panel.activeSelf != show) _panel.SetActive(show);
            if (!show) return;

            bool late = left < 30;
            _background.color = late ? _alarmPanel : _cautionPanel;
            _titleLabel.text = $"UNDER-FREQUENCY · {Mathf.CeilToInt((float)left)} s to cascade";
            _detailLabel.text = $"{_grid.Frequency:0.00} Hz and falling. Bring on reserve, or drop load, now.";

            var unit = _grid.ReserveToSync;
            _syncButton.interactable = unit != null;
            _syncLabel.text = unit != null ? $"Sync Unit {unit.Number}" : "No unit waiting";
            var biggest = _grid.BiggestLit;
            _shedButton.interactable = biggest != null;
            _shedLabel.text = biggest != null ? $"Drop {_grid.AreaName(biggest.Id)}" : "Nothing to drop";
        }
    }
}
