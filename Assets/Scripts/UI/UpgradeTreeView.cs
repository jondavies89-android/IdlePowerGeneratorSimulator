using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using IdlePowerGen.Models;
using IdlePowerGen.Utils;

namespace IdlePowerGen.UI
{
    // An era's upgrades as a tree: one column per branch (EraConfig.upgradeBranchNames), one
    // row per depth (UpgradeDefinition.treeRow), with a line from each prerequisite down to
    // what it opens. The whole tree is visible from the start, as in Antimatter Dimensions'
    // time studies, so the player can see where a branch leads and plan for it. Tapping a
    // node shows it on the detail card, which is the buy button.
    public class UpgradeTreeView : MonoBehaviour
    {
        [SerializeField] private UpgradeNodeView _nodePrefab;
        [SerializeField] private RectTransform _area;      // nodes and lines are placed in here
        [SerializeField] private TMP_Text _headingTemplate; // cloned for each branch's heading
        [SerializeField] private UpgradeCardView _detail;
        [SerializeField] private int _era = 2;
        // The era's research nodes rather than its purchases (Era 3 on).
        [SerializeField] private bool _research;
        [SerializeField] private Color _line      = new(0.35f, 0.27f, 0.19f);
        [SerializeField] private Color _ownedLine = new(0.54f, 0.42f, 0.23f);

        // Plaques are a little over the 44dp tap target; the gap between rows holds the lines.
        public const float HeadingHeight = 26f;
        public const float NodeHeight    = 64f;
        public const float RowPitch      = 92f;
        private const float ColumnGap    = 8f;
        private const float LineWidth    = 3f;

        private readonly List<UpgradeNodeView> _nodes = new();
        private readonly List<(UpgradeNodeView from, UpgradeNodeView to, Image line)> _links = new();
        private readonly List<TMP_Text> _headings = new();
        private int _branches = 1;
        private float _laidOutWidth = -1f;

        public UpgradeDefinition Selected { get; private set; }
        public int Era => _era;
        public bool IsResearch => _research;
        public IReadOnlyList<UpgradeNodeView> Nodes => _nodes;

        private void Awake()
        {
            EventBus.Subscribe<UpgradePurchasedEvent>(OnUpgradePurchased);
            EventBus.Subscribe<CurrencyChangedEvent>(OnCurrencyChanged);
            EventBus.Subscribe<GameLoadedEvent>(OnGameLoaded);
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<UpgradePurchasedEvent>(OnUpgradePurchased);
            EventBus.Unsubscribe<CurrencyChangedEvent>(OnCurrencyChanged);
            EventBus.Unsubscribe<GameLoadedEvent>(OnGameLoaded);
        }

        private void Start()
        {
            var gm = Managers.GameManager.Instance;
            if (gm == null) return;

            var defs = gm.UpgradeCatalogue.Where(u => Mathf.Max(1, u.minEra) == _era && u.isResearch == _research).ToList();
            if (defs.Count == 0) return;

            var cfg = gm.GetEraConfig(_era);
            var names = _research ? cfg?.researchBranchNames : cfg?.upgradeBranchNames;
            _branches = Mathf.Max(defs.Max(d => d.treeBranch) + 1, names?.Length ?? 0);
            int rows = defs.Max(d => d.treeRow) + 1;

            // Not ??: a missing component is a Unity fake-null in the Editor.
            var le = _area.GetComponent<LayoutElement>();
            if (le == null) le = _area.gameObject.AddComponent<LayoutElement>();
            le.minHeight = le.preferredHeight = HeadingHeight + rows * RowPitch;

            // Lines go first so the plaques draw over their ends.
            var lineLayer = new GameObject("Lines", typeof(RectTransform)).GetComponent<RectTransform>();
            lineLayer.SetParent(_area, false);
            lineLayer.anchorMin = Vector2.zero;
            lineLayer.anchorMax = Vector2.one;
            lineLayer.offsetMin = lineLayer.offsetMax = Vector2.zero;

            for (int b = 0; b < _branches; b++)
            {
                var heading = Instantiate(_headingTemplate, _area);
                heading.name = $"Branch_{b}";
                heading.text = names != null && b < names.Length ? names[b] : string.Empty;
                heading.gameObject.SetActive(true);
                var rt = heading.rectTransform;
                rt.anchorMin = new Vector2((float)b / _branches, 1);
                rt.anchorMax = new Vector2((float)(b + 1) / _branches, 1);
                rt.pivot = new Vector2(0.5f, 1);
                rt.offsetMin = new Vector2(ColumnGap / 2, -HeadingHeight);
                rt.offsetMax = new Vector2(-ColumnGap / 2, 0);
                _headings.Add(heading);
            }
            _headingTemplate.gameObject.SetActive(false);

            foreach (var def in defs.OrderBy(d => d.treeRow).ThenBy(d => d.treeBranch))
            {
                var node = Instantiate(_nodePrefab, _area);
                node.name = "Upgrade_" + def.upgradeId;
                var rt = (RectTransform)node.transform;
                rt.anchorMin = new Vector2((float)def.treeBranch / _branches, 1);
                rt.anchorMax = new Vector2((float)(def.treeBranch + 1) / _branches, 1);
                rt.pivot = new Vector2(0.5f, 1);
                float top = HeadingHeight + def.treeRow * RowPitch;
                rt.offsetMin = new Vector2(ColumnGap / 2, -(top + NodeHeight));
                rt.offsetMax = new Vector2(-ColumnGap / 2, -top);
                node.Bind(def, Select);
                _nodes.Add(node);
            }

            foreach (var node in _nodes)
            foreach (var id in node.Definition.requiredUpgradeIds ?? new string[0])
            {
                var from = _nodes.FirstOrDefault(n => n.Definition.upgradeId == id);
                if (from == null) continue;
                var line = new GameObject($"Line_{id}_{node.Definition.upgradeId}", typeof(RectTransform), typeof(Image));
                line.transform.SetParent(lineLayer, false);
                var image = line.GetComponent<Image>();
                image.raycastTarget = false;
                _links.Add((from, node, image));
            }

            Select(DefaultSelection());
            RefreshAll();
        }

        // The width is only known once the layout has run, and changes with the screen.
        private void LateUpdate()
        {
            float width = _area.rect.width;
            if (Mathf.Approximately(width, _laidOutWidth) || width <= 0) return;
            _laidOutWidth = width;
            LayoutLines(width);
        }

        private Vector2 Centre(UpgradeNodeView node, float width, bool bottom)
        {
            var def = node.Definition;
            float x = (def.treeBranch + 0.5f) / _branches * width;
            float y = -(HeadingHeight + def.treeRow * RowPitch + (bottom ? NodeHeight : 0f));
            return new Vector2(x, y);
        }

        private void LayoutLines(float width)
        {
            foreach (var (from, to, image) in _links)
            {
                Vector2 a = Centre(from, width, bottom: true);
                Vector2 b = Centre(to, width, bottom: false);
                Vector2 d = b - a;
                var rt = image.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
                rt.pivot = new Vector2(0, 0.5f);
                rt.anchoredPosition = a;
                rt.sizeDelta = new Vector2(d.magnitude, LineWidth);
                rt.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            }
        }

        // The cheapest thing the player could buy now, else the cheapest still to come.
        private UpgradeDefinition DefaultSelection()
        {
            var gm = Managers.GameManager.Instance;
            var open = _nodes.Select(n => n.Definition).Where(d => !gm.Upgrades.IsMaxed(d)).ToList();
            return open.Where(d => gm.Upgrades.PrerequisitesMet(d)).OrderBy(Cost).FirstOrDefault()
                ?? open.OrderBy(Cost).FirstOrDefault()
                ?? _nodes.FirstOrDefault()?.Definition;
        }

        private static double Cost(UpgradeDefinition d)
        {
            var u = Managers.GameManager.Instance.Upgrades;
            return u.NextCostPennies(d) + u.NextCostJoules(d) + u.NextCostResearch(d);
        }

        public void Select(UpgradeDefinition def)
        {
            if (def == null) return;
            Selected = def;
            foreach (var n in _nodes) n.SetSelected(n.Definition == def);
            if (_detail != null) _detail.Bind(def);
        }

        // A bought node moves the selection on, so the next purchase is one tap away.
        private void OnUpgradePurchased(UpgradePurchasedEvent e)
        {
            RefreshAll();
            var gm = Managers.GameManager.Instance;
            if (gm != null && Selected == e.Definition && gm.Upgrades.IsMaxed(e.Definition))
                Select(DefaultSelection());
        }

        private void OnCurrencyChanged(CurrencyChangedEvent _) => RefreshAll();

        private void OnGameLoaded(GameLoadedEvent _)
        {
            if (_nodes.Count == 0) return;
            RefreshAll();
            Select(DefaultSelection());
        }

        private void RefreshAll()
        {
            var gm = Managers.GameManager.Instance;
            if (gm == null) return;
            foreach (var n in _nodes) n.Refresh();
            foreach (var (from, to, image) in _links)
                image.color = gm.Upgrades.IsPurchased(from.Definition.upgradeId) &&
                              gm.Upgrades.IsPurchased(to.Definition.upgradeId) ? _ownedLine : _line;
        }

        // Anything in this tree the player could buy this moment (for the tab badge).
        public bool AnythingToBuy()
        {
            var gm = Managers.GameManager.Instance;
            return gm != null && _nodes.Any(n => gm.Upgrades.IsAvailable(n.Definition));
        }
    }
}
