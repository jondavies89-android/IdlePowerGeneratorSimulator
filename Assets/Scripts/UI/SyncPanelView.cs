using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdlePowerGen.UI
{
    // "Unit 5 ready to synchronise — match frequency & phase to close breaker [SYNC]" (bible
    // Appendix A, Era 4). Shown while any unit is waiting; the auto-synchroniser does it
    // itself once researched.
    public class SyncPanelView : MonoBehaviour
    {
        [SerializeField] private Controllers.UnitGridController _grid;
        [SerializeField] private GameObject _panel;
        [SerializeField] private TMP_Text _titleLabel;
        [SerializeField] private TMP_Text _hintLabel;
        [SerializeField] private Button _sync;
        [SerializeField] private SyncOverlayView _overlay;

        private void Awake() => _sync.onClick.AddListener(() => { var u = _grid.NextToSync; if (u != null) _overlay.Open(u); });

        private void Update()
        {
            if (_grid == null) return;
            var unit = _grid.NextToSync;
            bool show = unit != null && !_grid.Grid.Dead && _grid.BlackStart == null;
            if (_panel.activeSelf != show) _panel.SetActive(show);
            if (!show) return;
            int waiting = 0; foreach (var _ in _grid.AwaitingSync) waiting++;
            _titleLabel.text = waiting > 1 ? $"Unit {unit.Number} ready to synchronise (+{waiting - 1} more)"
                                           : $"Unit {unit.Number} ready to synchronise";
            _hintLabel.text = "match frequency & phase to close the breaker";
        }
    }
}
