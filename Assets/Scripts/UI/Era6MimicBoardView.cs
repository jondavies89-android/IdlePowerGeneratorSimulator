using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using IdlePowerGen.Data;

namespace IdlePowerGen.UI
{
    // The national mimic board: the regions of Britain as rings, roughly where they sit on the
    // map, joined by the supergrid's lines. Green lit, amber dimmed, red dropped, grey still to
    // connect. Tapping a region shows it on the card below.
    public class Era6MimicBoardView : MonoBehaviour
    {
        [SerializeField] private Controllers.Era6NationalGridController _grid;
        [SerializeField] private RectTransform _area;
        [SerializeField] private Image _nodeTemplate;  // a ring with a Button and a Code label
        [SerializeField] private Image _lineTemplate;
        [SerializeField] private Era6RegionCardView _card;
        [SerializeField] private Color _healthy = Color.green;
        [SerializeField] private Color _caution = Color.yellow;
        [SerializeField] private Color _alarm   = Color.red;
        [SerializeField] private Color _unlit   = Color.gray;
        [SerializeField] private Color _selected = Color.white;

        private const float NodeSize = 36f, LineWidth = 3f;

        private readonly Dictionary<string, (Image node, TMP_Text code, Outline ring)> _nodes = new();
        private readonly Dictionary<string, Image> _lines = new();
        private float _w = -1, _h = -1;

        private void Start()
        {
            foreach (var r in Era6Data.Regions)
            {
                if (r.Parent == null) continue;
                var line = Instantiate(_lineTemplate, _area);
                line.name = "Line_" + r.Id;
                line.gameObject.SetActive(true);
                line.transform.SetAsFirstSibling();
                _lines[r.Id] = line;
            }
            foreach (var r in Era6Data.Regions)
            {
                var node = Instantiate(_nodeTemplate, _area);
                node.name = "Region_" + r.Id;
                node.gameObject.SetActive(true);
                var code = node.GetComponentInChildren<TMP_Text>();
                code.text = r.Code;
                string id = r.Id;
                node.GetComponent<Button>().onClick.AddListener(() => Select(id));
                var ring = node.gameObject.AddComponent<Outline>();
                ring.effectColor = _selected;
                ring.effectDistance = new Vector2(2f, -2f);
                ring.enabled = false;
                _nodes[id] = (node, code, ring);
            }
            _nodeTemplate.gameObject.SetActive(false);
            _lineTemplate.gameObject.SetActive(false);
            Select(Era6Data.Regions[2].Id);
        }

        public void Select(string id)
        {
            foreach (var (key, n) in _nodes) n.ring.enabled = key == id;
            _card.Bind(id);
        }

        // Inside the board's edges, so a ring at 0 or 1 still shows whole.
        private Vector2 Pos(double x, double y) => new((float)(0.06 + x * 0.88) * _w, -(float)(0.06 + y * 0.88) * _h);

        private void LateUpdate()
        {
            float w = _area.rect.width, h = _area.rect.height;
            if (w > 0 && (!Mathf.Approximately(w, _w) || !Mathf.Approximately(h, _h)))
            {
                _w = w; _h = h;
                foreach (var r in Era6Data.Regions)
                {
                    var p = Pos(r.X, r.Y);
                    var rt = (RectTransform)_nodes[r.Id].node.transform;
                    rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.anchoredPosition = p;
                    rt.sizeDelta = new Vector2(NodeSize, NodeSize);
                    if (r.Parent == null) continue;
                    var parent = _grid.Spec(r.Parent);
                    Vector2 from = Pos(parent.X, parent.Y), d = p - from;
                    var lt = (RectTransform)_lines[r.Id].transform;
                    lt.anchorMin = lt.anchorMax = new Vector2(0, 1);
                    lt.pivot = new Vector2(0, 0.5f);
                    lt.anchoredPosition = from;
                    lt.sizeDelta = new Vector2(d.magnitude, LineWidth);
                    lt.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
                }
            }
            Refresh();
        }

        private void Refresh()
        {
            foreach (var r in Era6Data.Regions)
            {
                var a = _grid.Area(r.Id);
                var (node, code, _) = _nodes[r.Id];
                Color c = !a.Connected ? _unlit : a.Shed ? _alarm : a.ServedFraction < 0.95 ? _caution : _healthy;
                node.color = c;
                code.color = c;
                if (_lines.TryGetValue(r.Id, out var line))
                    line.color = a.Connected ? _healthy : _unlit * new Color(1, 1, 1, 0.35f);
            }
        }
    }
}
