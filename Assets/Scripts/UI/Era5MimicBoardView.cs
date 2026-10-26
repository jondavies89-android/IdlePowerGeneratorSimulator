using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using IdlePowerGen.Data;

namespace IdlePowerGen.UI
{
    // The regional mimic board (bible Appendix A, Era 5): Central's square, the cities as
    // rings joined by painted lines, each ring green, amber or red by its voltage; cities not
    // yet connected are dashed outlines. Tapping a city shows it on the card below.
    public class Era5MimicBoardView : MonoBehaviour
    {
        [SerializeField] private Controllers.Era5DispatchController _grid;
        [SerializeField] private RectTransform _area;
        [SerializeField] private RectTransform _central;
        [SerializeField] private Image _nodeTemplate;  // a ring with a Button and two labels (Initial, Name)
        [SerializeField] private Image _lineTemplate;
        [SerializeField] private CityCardView _card;
        [SerializeField] private Color _healthy = new(0.24f, 0.42f, 0.27f);
        [SerializeField] private Color _caution = new(0.72f, 0.38f, 0.13f);
        [SerializeField] private Color _alarm   = new(0.7f, 0.15f, 0.12f);
        [SerializeField] private Color _unlit   = new(0.5f, 0.5f, 0.46f);
        [SerializeField] private Color _selected = Color.black;

        private const float NodeSize = 34f, LineWidth = 4f;
        // Central sits at the left of the board, as in the mockup.
        public static readonly Vector2 CentralPosition = new(0.08f, 0.49f);

        private readonly Dictionary<string, (Image node, TMP_Text name, Outline ring)> _nodes = new();
        private readonly Dictionary<string, Image> _lines = new();
        private float _w = -1, _h = -1;
        private string _selectedId;

        private void Start()
        {
            foreach (var c in Era5Data.Cities)
            {
                var line = Instantiate(_lineTemplate, _area);
                line.name = "Line_" + c.Id;
                line.gameObject.SetActive(true);
                line.transform.SetAsFirstSibling();
                _lines[c.Id] = line;
            }
            foreach (var c in Era5Data.Cities)
            {
                var node = Instantiate(_nodeTemplate, _area);
                node.name = "City_" + c.Id;
                node.gameObject.SetActive(true);
                var labels = node.GetComponentsInChildren<TMP_Text>();
                labels[0].text = c.Name.Substring(0, 1);
                labels[1].text = c.Name.ToUpperInvariant();
                string id = c.Id;
                node.GetComponent<Button>().onClick.AddListener(() => Select(id));
                var ring = node.gameObject.AddComponent<Outline>();
                ring.effectColor = _selected;
                ring.effectDistance = new Vector2(1.5f, -1.5f);
                ring.enabled = false;
                _nodes[id] = (node, labels[1], ring);
            }
            _nodeTemplate.gameObject.SetActive(false);
            _lineTemplate.gameObject.SetActive(false);
            Select("c5_leicester");
        }

        public void Select(string id)
        {
            _selectedId = id;
            foreach (var (key, n) in _nodes) n.ring.enabled = key == id;
            _card.Bind(id);
        }

        private Vector2 Pos(float x, float y) => new(x * _w, -y * _h);

        private void LateUpdate()
        {
            float w = _area.rect.width, h = _area.rect.height;
            if (w > 0 && (!Mathf.Approximately(w, _w) || !Mathf.Approximately(h, _h)))
            {
                _w = w; _h = h;
                        Place(_central, Pos(CentralPosition.x, CentralPosition.y), _central.sizeDelta);
                foreach (var c in Era5Data.Cities)
                {
                    var p = Inner((float)c.X, (float)c.Y);
                    Place((RectTransform)_nodes[c.Id].node.transform, p, new Vector2(NodeSize, NodeSize));
                    Vector2 from;
                    if (c.Parent == null) from = Pos(CentralPosition.x, CentralPosition.y);
                    else { var parent = _grid.Spec(c.Parent); from = Inner((float)parent.X, (float)parent.Y); }
                    var rt = (RectTransform)_lines[c.Id].transform;
                    Vector2 d = p - from;
                    rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
                    rt.pivot = new Vector2(0, 0.5f);
                    rt.anchoredPosition = from;
                    rt.sizeDelta = new Vector2(d.magnitude, LineWidth);
                    rt.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
                }
            }
            Refresh();
        }

        // The cities' grid sits right of Central, with room above each ring for its name.
        private Vector2 Inner(float x, float y) => Pos(0.27f + x * 0.66f, 0.12f + y * 0.76f);

        private static void Place(RectTransform rt, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        private void Refresh()
        {
            foreach (var c in Era5Data.Cities)
            {
                var a = _grid.Area(c.Id);
                var (node, name, _) = _nodes[c.Id];
                Color col;
                if (!a.Connected) col = _unlit;
                else
                {
                    double v = _grid.Voltage(c.Id);
                    col = a.Shed || v < Era5Data.PoorVoltage ? _alarm : v < Era5Data.GoodVoltage ? _caution : _healthy;
                }
                node.color = col;
                name.color = a.Connected ? col : _unlit;
                name.text = a.Connected && col != _healthy ? $"{c.Name.ToUpperInvariant()} !" : c.Name.ToUpperInvariant();
                _lines[c.Id].color = a.Connected ? _healthy : _unlit * new Color(1, 1, 1, 0.35f);
            }
        }
    }
}
