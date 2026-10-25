using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using IdlePowerGen.Data;

namespace IdlePowerGen.UI
{
    // The town map of Westgate (bible Appendix A, Era 3): the station on the left, the
    // districts on a grid of streets, lit ones glowing gold and unserved ones faint.
    // A lit district whose line loses too much flickers amber or red. Tapping a district
    // shows it on the card below the map.
    public class Era3TownMapView : MonoBehaviour
    {
        [SerializeField] private Controllers.Era3TownController _town;
        [SerializeField] private RectTransform _area;
        [SerializeField] private Image _nodeTemplate;   // a disc with a Button; cloned per district
        [SerializeField] private Image _lineTemplate;   // a thin bar; cloned per street
        [SerializeField] private RectTransform _station;
        [SerializeField] private TMP_Text _countLabel;
        [SerializeField] private Era3DistrictCardView _card;
        [SerializeField] private Sprite _litSprite;
        [SerializeField] private Sprite _unlitSprite;
        [SerializeField] private Color _lit       = new(0.95f, 0.76f, 0.31f);
        [SerializeField] private Color _unlit     = new(0.42f, 0.36f, 0.27f);
        [SerializeField] private Color _available = new(0.80f, 0.68f, 0.45f);
        [SerializeField] private Color _caution   = new(0.94f, 0.63f, 0.19f);
        [SerializeField] private Color _alarm     = new(0.88f, 0.35f, 0.29f);
        [SerializeField] private Color _litLine   = new(0.72f, 0.54f, 0.23f);
        [SerializeField] private Color _darkLine  = new(0.23f, 0.19f, 0.14f);
        [SerializeField] private Color _selected  = Color.white;

        private const float NodeSize = 26f, LineWidth = 3f;

        private readonly Dictionary<string, Image> _nodes = new();
        private readonly Dictionary<string, Image> _lines = new();
        private readonly Dictionary<string, Outline> _rings = new();
        private string _selectedId;
        private float _laidOutWidth = -1, _laidOutHeight = -1;

        public string SelectedId => _selectedId;

        private void Start()
        {
            foreach (var d in Era3Data.Districts)
            {
                var line = Instantiate(_lineTemplate, _area);
                line.name = "Street_" + d.Id;
                line.gameObject.SetActive(true);
                line.transform.SetAsFirstSibling();
                _lines[d.Id] = line;
            }
            foreach (var d in Era3Data.Districts)
            {
                var node = Instantiate(_nodeTemplate, _area);
                node.name = "District_" + d.Id;
                node.gameObject.SetActive(true);
                string id = d.Id;
                node.GetComponent<Button>().onClick.AddListener(() => Select(id));
                var ring = node.gameObject.AddComponent<Outline>();
                ring.effectColor = _selected;
                ring.effectDistance = new Vector2(2, -2);
                ring.enabled = false;
                _rings[id] = ring;
                _nodes[id] = node;
            }
            _nodeTemplate.gameObject.SetActive(false);
            _lineTemplate.gameObject.SetActive(false);
            Select(Era3Data.Districts[3].Id); // Bond Street: the first one to light
        }

        public void Select(string id)
        {
            _selectedId = id;
            foreach (var (key, ring) in _rings) ring.enabled = key == id;
            _card.Bind(id);
        }

        // Grid cell → position in the map area (columns across, rows down).
        private Vector2 Position(int col, int row, float w, float h)
        {
            float x = (col + 0.5f) / Era3Data.MapColumns * w;
            float y = -(row + 0.5f) / Era3Data.MapRows * h;
            return new Vector2(x, y);
        }

        private void LateUpdate()
        {
            float w = _area.rect.width, h = _area.rect.height;
            if (w > 0 && (!Mathf.Approximately(w, _laidOutWidth) || !Mathf.Approximately(h, _laidOutHeight)))
            {
                _laidOutWidth = w; _laidOutHeight = h;
                Layout(w, h);
            }
            Refresh();
        }

        private void Layout(float w, float h)
        {
            var stationPos = Position(Era3Data.StationColumn, Era3Data.StationRow, w, h);
            Place(_station, stationPos, _station.sizeDelta);
            foreach (var d in Era3Data.Districts)
            {
                var pos = Position(d.Column, d.Row, w, h);
                Place((RectTransform)_nodes[d.Id].transform, pos, new Vector2(NodeSize, NodeSize));

                string parent = _town.ParentOf(d.Id);
                Vector2 from;
                if (parent == null) from = stationPos;
                else
                {
                    var p = _town.Spec(parent);
                    from = Position(p.Column, p.Row, w, h);
                }
                var rt = (RectTransform)_lines[d.Id].transform;
                Vector2 delta = pos - from;
                rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
                rt.pivot = new Vector2(0, 0.5f);
                rt.anchoredPosition = from;
                rt.sizeDelta = new Vector2(delta.magnitude, LineWidth);
                rt.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            }
        }

        private static void Place(RectTransform rt, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        private void Refresh()
        {
            int lit = 0;
            foreach (var d in Era3Data.Districts)
            {
                var area = _town.Area(d.Id);
                var node = _nodes[d.Id];
                var state = _town.StateOf(d.Id);
                if (area.Connected)
                {
                    lit++;
                    node.sprite = _litSprite;
                    double loss = area.LossFraction;
                    // Dim lamps (short of power) read as a paler gold.
                    Color c = loss >= Era3Data.FlickerBadlyLoss ? _alarm
                            : loss >= Era3Data.FlickerLoss ? _caution : _lit;
                    if (area.ServedFraction < 0.95) c = Color.Lerp(c, _unlit, 0.45f);
                    node.color = c;
                    _lines[d.Id].color = _litLine;
                }
                else
                {
                    node.sprite = _unlitSprite;
                    node.color = state == Controllers.Era3TownController.ConnectState.Available ? _available : _unlit;
                    _lines[d.Id].color = _darkLine;
                }
            }
            _countLabel.text = $"{lit} of {Era3Data.Districts.Length} lit";
        }
    }
}
