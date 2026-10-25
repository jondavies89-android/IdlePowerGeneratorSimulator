using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using IdlePowerGen.Utils;

namespace IdlePowerGen.UI
{
    // The feeders (Era 4) or cities (Era 5) to connect, one row each: what it draws, and the
    // button — connect it, or (once lit) drop it to save the grid and pick it up again.
    public class AreaListView : MonoBehaviour
    {
        [SerializeField] private Controllers.UnitGridController _grid;
        [SerializeField] private Button _rowTemplate;   // labels: Name, Detail, Action
        [SerializeField] private Transform _content;
        [SerializeField] private TMP_Text _summaryLabel;
        [SerializeField] private Color _text    = Color.white;
        [SerializeField] private Color _muted   = Color.gray;
        [SerializeField] private Color _money   = Color.yellow;
        [SerializeField] private Color _caution = Color.yellow;

        private readonly List<(string id, Button button, TMP_Text name, TMP_Text detail, TMP_Text action, CanvasGroup group)> _rows = new();
        private const float RefreshInterval = 0.25f;
        private float _since = RefreshInterval;

        private void Start()
        {
            _rowTemplate.gameObject.SetActive(false);
            foreach (var a in _grid.Areas)
            {
                var b = Instantiate(_rowTemplate, _content);
                b.name = "Area_" + a.Id;
                b.gameObject.SetActive(true);
                string id = a.Id;
                b.onClick.AddListener(() => Act(id));
                var labels = b.GetComponentsInChildren<TMP_Text>();
                _rows.Add((id, b, labels[0], labels[1], labels[2], b.GetComponent<CanvasGroup>()));
            }
        }

        private void Act(string id)
        {
            var area = _grid.Area(id);
            if (!area.Connected) Connect(id);
            else _grid.SetShed(id, !area.Shed);
        }

        private void Connect(string id)
        {
            if (_grid is Controllers.Era4StationController e4) e4.Connect(id);
            else if (_grid is Controllers.Era5DispatchController e5) e5.Connect(id);
        }

        private decimal Cost(string id) =>
            _grid is Controllers.Era4StationController e4 ? e4.ConnectCost(id)
          : _grid is Controllers.Era5DispatchController e5 ? e5.ConnectCost(id) : 0m;

        private bool CanConnect(string id) =>
            _grid is Controllers.Era4StationController e4 ? e4.CanConnect(id)
          : _grid is Controllers.Era5DispatchController e5 && e5.CanConnect(id);

        private void Update()
        {
            _since += Time.unscaledDeltaTime;
            if (_since < RefreshInterval) return;
            _since = 0f;
            if (_grid == null) return;

            // Lit first, then the cheapest still to connect; only the next few unlit show.
            int unlitShown = 0;
            foreach (var r in _rows.OrderBy(r => _grid.Area(r.id).Connected ? 0 : 1).ThenBy(r => (double)Cost(r.id)))
            {
                var a = _grid.Area(r.id);
                bool show = a.Connected || unlitShown++ < 4;
                if (r.button.gameObject.activeSelf != show) r.button.gameObject.SetActive(show);
                if (!show) continue;
                r.button.transform.SetAsLastSibling();
                r.name.text = _grid.AreaName(r.id) + (_grid.IsIndustrial(r.id) ? "  · works" : "");
                if (a.Connected)
                {
                    r.detail.text = a.Shed ? "dropped to save the grid" : $"drawing {NumberFormatter.FormatWatts(a.DeliveredWatts)}";
                    r.detail.color = a.Shed ? _caution : _muted;
                    r.action.text = a.Shed ? "Pick up" : "Drop";
                    r.action.color = a.Shed ? _money : _muted;
                    r.button.interactable = _grid.BlackStart == null;
                    r.group.alpha = 1f;
                }
                else
                {
                    r.detail.text = $"about {NumberFormatter.FormatWatts(a.BaseWatts)} · more at the evening peak";
                    r.detail.color = _muted;
                    r.action.text = $"Connect\n{Managers.Money.Format(Cost(r.id))}";
                    r.action.color = _money;
                    bool can = CanConnect(r.id);
                    r.button.interactable = can;
                    r.group.alpha = can ? 1f : 0.5f;
                }
            }

            double lit = _grid.Areas.Where(a => a.Connected).Sum(a => a.BaseWatts);
            double online = _grid.Grid.OnlineCapacity;
            _summaryLabel.text = $"{NumberFormatter.FormatWatts(lit)} connected · peaks near {NumberFormatter.FormatWatts(lit * 1.42)} · " +
                                 $"{NumberFormatter.FormatWatts(online)} online";
            _summaryLabel.color = online >= lit * 1.45 ? _muted : _caution;
        }
    }
}
