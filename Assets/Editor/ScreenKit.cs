using System.Linq;
using IdlePowerGen.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static Era1SceneBuilder;

// Building blocks for the Era 3–5 screens, in whichever era's ScreenTheme: the screen's
// column, panels, text, tabbed pages, the upgrade and research trees, and the contract
// card prefabs. The same layout rules as Era 2 (44dp taps, 16dp gutters, tabs at the foot).
public static class ScreenKit
{
    public static RectTransform Screen(Transform canvas, string name, ScreenTheme t, out RectTransform column)
    {
        var root = Rect(name, canvas);
        Stretch(root);
        var bg = Rect("Background", root);
        Stretch(bg);
        AddImage(bg, null, t.Background).raycastTarget = false;

        var safe = Rect("SafeArea", root);
        Stretch(safe);
        safe.gameObject.AddComponent<SafeAreaFitter>();

        column = Rect("Column", safe);
        Stretch(column);
        var vlg = column.gameObject.AddComponent<VerticalLayoutGroup>();
        // Room at the top for the DEV tab.
        vlg.padding = new RectOffset((int)Gutter, (int)Gutter, 10, 0);
        vlg.spacing = 6;
        vlg.childControlWidth = vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        root.gameObject.SetActive(false);
        return root;
    }

    public static TextMeshProUGUI Text(ScreenTheme t, Transform parent, string name, string text, float size, Color colour,
                                       TextAlignmentOptions align = TextAlignmentOptions.Left,
                                       FontStyles style = FontStyles.Normal, bool numerals = false)
    {
        var rt = Rect(name, parent);
        var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        var font = numerals ? t.Numerals : t.Body;
        if (font != null) tmp.font = font;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = colour;
        tmp.alignment = align;
        tmp.fontStyle = style;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.raycastTarget = false;
        return tmp;
    }

    // Spaced capitals, the mockups' panel captions ("TOWN MAP · WESTGATE").
    public static TextMeshProUGUI Caption(ScreenTheme t, Transform parent, string name, string text, float size = 13,
                                          TextAlignmentOptions align = TextAlignmentOptions.Left)
    {
        var c = Text(t, parent, name, text, size, t.TextMuted, align);
        c.characterSpacing = 10;
        c.fontStyle = FontStyles.UpperCase;
        return c;
    }

    public static Image Panel(ScreenTheme t, RectTransform rt, Color? colour = null)
    {
        var image = AddImage(rt, t.PanelSprite, colour ?? t.Panel);
        image.type = Image.Type.Sliced;
        return image;
    }

    // A panel laid out as a column with padding, sized to its content.
    public static RectTransform Box(ScreenTheme t, Transform parent, string name, int padding = 14, float spacing = 4,
                                    Color? colour = null)
    {
        var box = Stack(name, parent, spacing);
        box.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(padding, padding, padding - 2, padding - 2);
        Panel(t, box, colour);
        return box;
    }

    public static RectTransform Row(Transform parent, string name, float spacing = 8)
    {
        var rt = Rect(name, parent);
        var hlg = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = spacing;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = hlg.childControlHeight = true;
        hlg.childForceExpandWidth = hlg.childForceExpandHeight = false;
        return rt;
    }

    public static LayoutElement Flexible(Component c)
    {
        var le = c.gameObject.GetComponent<LayoutElement>();
        if (le == null) le = c.gameObject.AddComponent<LayoutElement>();
        le.flexibleWidth = 1;
        return le;
    }

    // A thin bar; returns the fill (Image.Type.Filled, horizontal).
    public static Image Bar(ScreenTheme t, Transform parent, string name, float height, Color fillColour)
    {
        var bar = Rect(name, parent);
        Fixed(bar, height);
        AddImage(bar, Era1SpriteFactory.White, t.Track).raycastTarget = false;
        var fillRt = Rect("Fill", bar);
        Stretch(fillRt);
        var fill = AddImage(fillRt, Era1SpriteFactory.White, fillColour);
        fill.raycastTarget = false;
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillAmount = 0f;
        return fill;
    }

    public static Button Button(ScreenTheme t, Transform parent, string name, string label, float height = 48,
                                Color? colour = null, float size = 17)
    {
        var rt = Rect(name, parent);
        Fixed(rt, height);
        var image = Panel(t, rt, colour ?? t.PanelRaised);
        var button = rt.gameObject.AddComponent<UnityEngine.UI.Button>();
        button.targetGraphic = image;
        var colors = button.colors;
        colors.disabledColor = new Color(1, 1, 1, 0.45f);
        button.colors = colors;
        rt.gameObject.AddComponent<PressFeedback>();
        var text = Text(t, rt, "Label", label, size, t.Text, TextAlignmentOptions.Center);
        Stretch(text.rectTransform);
        text.rectTransform.offsetMin = new Vector2(10, 2);
        text.rectTransform.offsetMax = new Vector2(-10, -2);
        return button;
    }

    public static TextMeshProUGUI SectionHeader(ScreenTheme t, Transform parent, string name, string text)
    {
        var h = Caption(t, parent, name, text, 14);
        h.margin = new Vector4(0, 8, 0, 0);
        return h;
    }

    public static RectTransform Scroll(Transform parent)
    {
        var scrollRt = Rect("Lists", parent);
        var le = scrollRt.gameObject.AddComponent<LayoutElement>();
        le.flexibleHeight = 1;
        le.minHeight = 160;

        var scroll = scrollRt.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Elastic;
        scroll.scrollSensitivity = 30;

        var viewport = Rect("Viewport", scrollRt);
        Stretch(viewport);
        viewport.gameObject.AddComponent<RectMask2D>();
        AddImage(viewport, null, new Color(0, 0, 0, 0));

        var content = Rect("Content", viewport);
        content.anchorMin = new Vector2(0, 1);
        content.anchorMax = new Vector2(1, 1);
        content.pivot = new Vector2(0.5f, 1);
        content.sizeDelta = Vector2.zero;
        var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 8;
        vlg.padding = new RectOffset(0, 0, 4, 24);
        vlg.childControlWidth = vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = viewport;
        scroll.content = content;
        return content;
    }

    public static RectTransform Pages(Transform parent)
    {
        var pagesRt = Rect("Pages", parent);
        var le = pagesRt.gameObject.AddComponent<LayoutElement>();
        le.flexibleHeight = 1;
        le.minHeight = 200;
        return pagesRt;
    }

    public static RectTransform Page(Transform parent, string name)
    {
        var page = Rect(name, parent);
        Stretch(page);
        page.gameObject.AddComponent<CanvasGroup>();
        var vlg = page.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 6;
        vlg.childControlWidth = vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        return page;
    }

    public static TabBarView TabBar(ScreenTheme t, Transform parent, string[] names, RectTransform[] pages)
    {
        var bar = Rect("TabBar", parent);
        Fixed(bar, 56);
        var hlg = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(0, 0, 4, 8);
        hlg.spacing = 5;
        hlg.childControlWidth = hlg.childControlHeight = true;
        hlg.childForceExpandWidth = hlg.childForceExpandHeight = true;

        var buttons = new UnityEngine.UI.Button[names.Length];
        var backgrounds = new Image[names.Length];
        var labels = new TextMeshProUGUI[names.Length];
        var badges = new Image[names.Length];
        for (int i = 0; i < names.Length; i++)
        {
            var tab = Rect("Tab_" + names[i], bar);
            var image = Panel(t, tab);
            var button = tab.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            tab.gameObject.AddComponent<PressFeedback>();
            var label = Text(t, tab, "Label", names[i], names.Length > 4 ? 14 : 16, t.TextMuted, TextAlignmentOptions.Center);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            Stretch(label.rectTransform);

            var badge = Rect("Badge", tab);
            badge.anchorMin = badge.anchorMax = new Vector2(1, 1);
            badge.pivot = new Vector2(1, 1);
            badge.sizeDelta = new Vector2(10, 10);
            badge.anchoredPosition = new Vector2(-6, -6);
            var dot = AddImage(badge, Era1SpriteFactory.Disc, t.Money);
            dot.raycastTarget = false;
            badge.gameObject.SetActive(false);

            buttons[i] = button; backgrounds[i] = image; labels[i] = label; badges[i] = dot;
        }

        var view = bar.gameObject.AddComponent<TabBarView>();
        var so = new SerializedObject(view);
        SetArray(so.FindProperty("_pages"), pages.Select(p => (Object)p.GetComponent<CanvasGroup>()).ToArray());
        SetArray(so.FindProperty("_tabs"), buttons);
        SetArray(so.FindProperty("_tabBackgrounds"), backgrounds);
        SetArray(so.FindProperty("_tabLabels"), labels);
        SetArray(so.FindProperty("_badges"), badges);
        so.FindProperty("_selected").colorValue = t.PanelRaised;
        so.FindProperty("_unselected").colorValue = t.Panel;
        so.FindProperty("_selectedText").colorValue = t.Text;
        so.FindProperty("_unselectedText").colorValue = t.TextMuted;
        so.ApplyModifiedPropertiesWithoutUndo();
        return view;
    }

    public static void SetColours(Object target, params (string field, Color colour)[] colours)
    {
        var so = new SerializedObject(target);
        foreach (var (field, colour) in colours)
        {
            var prop = so.FindProperty(field);
            if (prop == null) throw new System.ArgumentException($"{target.GetType().Name} has no field {field}");
            prop.colorValue = colour;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    public static void SetInt(Object target, string field, int value)
    {
        var so = new SerializedObject(target);
        so.FindProperty(field).intValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    public static void SetBool(Object target, string field, bool value)
    {
        var so = new SerializedObject(target);
        so.FindProperty(field).boolValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    public static void SetString(Object target, string field, string value)
    {
        var so = new SerializedObject(target);
        so.FindProperty(field).stringValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ---- The day and the boost (as on Era 2's screen) ----

    public static void DayBar(ScreenTheme t, Transform parent, Color daylight)
    {
        var row = Rect("DayBar", parent);
        Fixed(row, 22);
        var hlg = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 10;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = true;

        var clock = Text(t, row, "Clock", "18:00", 16, t.Text, numerals: true);
        clock.gameObject.AddComponent<LayoutElement>().preferredWidth = 48;

        var bar = Rect("Day", row);
        bar.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
        var track = Rect("Track", bar);
        track.anchorMin = new Vector2(0, 0.5f);
        track.anchorMax = new Vector2(1, 0.5f);
        track.sizeDelta = new Vector2(0, 8);
        AddImage(track, Era1SpriteFactory.White, t.Track).raycastTarget = false;
        var day = Rect("Daylight", track);
        day.anchorMin = new Vector2(6f / 24f, 0);
        day.anchorMax = new Vector2(20f / 24f, 1);
        day.offsetMin = day.offsetMax = Vector2.zero;
        AddImage(day, Era1SpriteFactory.White, daylight).raycastTarget = false;

        var marker = Rect("Now", bar);
        marker.anchorMin = new Vector2(0.75f, 0);
        marker.anchorMax = new Vector2(0.75f, 1);
        marker.sizeDelta = new Vector2(3, 0);
        AddImage(marker, Era1SpriteFactory.White, t.Money).raycastTarget = false;

        var view = row.gameObject.AddComponent<Era2DayBarView>();
        Wire(view, ("_marker", marker), ("_clockLabel", clock));
    }

    public static void Boost(ScreenTheme t, Transform parent, Color activePanel)
    {
        var rt = Rect("ContractBoost", parent);
        Fixed(rt, 44);
        var image = Panel(t, rt, t.PanelRaised);
        var button = rt.gameObject.AddComponent<UnityEngine.UI.Button>();
        button.targetGraphic = image;
        rt.gameObject.AddComponent<PressFeedback>();
        var label = Text(t, rt, "Label", "Contracts complete 2× faster", 16, t.Money, TextAlignmentOptions.Center);
        Stretch(label.rectTransform);

        var view = rt.gameObject.AddComponent<Era2ContractBoostView>();
        Wire(view, ("_button", button), ("_label", label), ("_background", image));
        SetColours(view, ("_idle", t.PanelRaised), ("_active", activePanel), ("_idleText", t.Money), ("_activeText", t.Healthy));
    }

    // ---- Trees ----

    // An era's purchases (research false) or research (true) as a tree, with the detail
    // card pinned beneath it. The node prefab is built per era so it carries the era's look.
    public static UpgradeTreeView Tree(ScreenTheme t, RectTransform page, int era, bool research, UpgradeNodeView nodePrefab)
    {
        var content = Scroll(page);
        var area = Rect(research ? "ResearchTree" : "Tree", content);
        area.gameObject.AddComponent<LayoutElement>();

        var heading = Text(t, area, "BranchHeading", "", 12, t.TextMuted, TextAlignmentOptions.Center);
        heading.characterSpacing = 6;
        heading.fontStyle = FontStyles.UpperCase;
        heading.textWrappingMode = TextWrappingModes.NoWrap;
        heading.overflowMode = TextOverflowModes.Ellipsis;

        var detail = UpgradeCard(t, research ? "ResearchDetail" : "UpgradeDetail");
        detail.transform.SetParent(page, false);
        detail.GetComponent<LayoutElement>().minHeight = 92;

        var tree = area.gameObject.AddComponent<UpgradeTreeView>();
        Wire(tree, ("_nodePrefab", nodePrefab), ("_area", area), ("_headingTemplate", heading),
                   ("_detail", detail.GetComponent<UpgradeCardView>()));
        SetColours(tree, ("_line", t.Edge), ("_ownedLine", research ? t.Research : t.Money));
        SetInt(tree, "_era", era);
        SetBool(tree, "_research", research);
        return tree;
    }

    public static GameObject UpgradeCard(ScreenTheme t, string name)
    {
        var root = Card(name, out var button, out var group);
        ((Image)button.targetGraphic).sprite = t.PanelSprite;
        ((Image)button.targetGraphic).color = t.PanelRaised;

        var texts = Stack("Texts", root, spacing: 2);
        Flexible(texts);
        var nameLabel = Text(t, texts, "Name", "Upgrade", 18, t.Text);
        var desc = Text(t, texts, "Description", "", 13, t.TextMuted, style: FontStyles.Italic);
        var effect = Text(t, texts, "Effect", "", 14, t.Money);

        var cost = Text(t, root, "Cost", "", 17, t.Money, TextAlignmentOptions.Right, numerals: true);
        cost.gameObject.AddComponent<LayoutElement>().preferredWidth = 92;

        var view = root.gameObject.AddComponent<UpgradeCardView>();
        Wire(view, ("_nameLabel", nameLabel), ("_descriptionLabel", desc), ("_effectLabel", effect),
                   ("_costLabel", cost), ("_button", button), ("_group", group));
        return root.gameObject;
    }

    public static GameObject UpgradeNode(ScreenTheme t, string name, Color price)
    {
        var root = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        var image = Panel(t, root);
        var button = root.gameObject.AddComponent<UnityEngine.UI.Button>();
        button.targetGraphic = image;
        var colors = button.colors;
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
        button.colors = colors;
        root.gameObject.AddComponent<PressFeedback>();
        var group = root.gameObject.AddComponent<CanvasGroup>();
        var ring = root.gameObject.AddComponent<Outline>();
        ring.effectColor = price;
        ring.effectDistance = new Vector2(2, -2);
        ring.enabled = false;

        var vlg = root.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(6, 6, 6, 6);
        vlg.spacing = 2;
        vlg.childAlignment = TextAnchor.MiddleCenter;
        vlg.childControlWidth = vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        var nameLabel = Text(t, root, "Name", "Upgrade", 14, t.Text, TextAlignmentOptions.Center);
        var cost = Text(t, root, "Cost", "", 13, price, TextAlignmentOptions.Center, numerals: true);

        var view = root.gameObject.AddComponent<UpgradeNodeView>();
        Wire(view, ("_nameLabel", nameLabel), ("_costLabel", cost), ("_button", button), ("_group", group),
                   ("_background", image), ("_selectedRing", ring));
        SetColours(view, ("_panel", t.Panel), ("_ready", t.PanelRaised), ("_owned", Color.Lerp(t.PanelRaised, price, 0.18f)),
                         ("_text", t.Text), ("_muted", t.TextMuted), ("_price", price), ("_achievement", price));
        return root.gameObject;
    }

    // ---- Machines ----

    // Era 2's machine row in this era's look: name and count, what each adds, the output,
    // and the price of another. The whole row is the buy button.
    public static GameObject GeneratorRow(ScreenTheme t, string name)
    {
        var root = Card(name, out var button, out var group);
        var image = (Image)button.targetGraphic;
        image.sprite = t.PanelSprite;
        image.color = t.Panel;

        var texts = Stack("Texts", root, spacing: 2);
        Flexible(texts);
        var nameLabel = Text(t, texts, "Name", "Machine", 18, t.Text);
        var running = Text(t, texts, "Running", "", 13, t.TextMuted, style: FontStyles.Italic);
        var output = Text(t, texts, "Output", "0 W", 15, t.TextMuted, numerals: true);

        var cost = Text(t, root, "Cost", "Build\n£0", 16, t.Money, TextAlignmentOptions.Right, numerals: true);
        cost.gameObject.AddComponent<LayoutElement>().preferredWidth = 92;

        var view = root.gameObject.AddComponent<Era2GeneratorRowView>();
        Wire(view, ("_nameLabel", nameLabel), ("_runningLabel", running), ("_outputLabel", output), ("_costLabel", cost),
                   ("_button", button), ("_group", group));
        SetColours(view, ("_runningColour", t.TextMuted), ("_alarmColour", t.Alarm));
        return root.gameObject;
    }

    // A fixed row naming the inherited station; returns its output label.
    public static TextMeshProUGUI InheritedRow(ScreenTheme t, Transform parent, string title, string subtitle)
    {
        var row = Card("Inherited", out var button, out _);
        row.SetParent(parent, false);
        var image = (Image)button.targetGraphic;
        image.sprite = t.PanelSprite;
        image.color = t.PanelRaised;
        button.interactable = false;

        var texts = Stack("Texts", row, spacing: 2);
        Flexible(texts);
        Text(t, texts, "Name", title, 18, t.Text);
        Text(t, texts, "Running", subtitle, 13, t.TextMuted, style: FontStyles.Italic);
        var output = Text(t, row, "Output", "0 W", 18, t.Text, TextAlignmentOptions.Right, numerals: true);
        output.gameObject.AddComponent<LayoutElement>().preferredWidth = 92;
        return output;
    }

    // ---- Contract cards (Era 2's, in this era's look) ----

    public static GameObject EngagementCard(ScreenTheme t, string name)
    {
        var root = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        Panel(t, root);
        var vlg = root.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(14, 14, 12, 12);
        vlg.spacing = 4;
        vlg.childControlWidth = vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        var title = Text(t, root, "Title", "", 19, t.Text);
        var body = Text(t, root, "Body", "", 13, t.TextMuted, style: FontStyles.Italic);

        var box = Stack("Box", root, spacing: 4);
        box.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(12, 12, 8, 8);
        Panel(t, box, t.PanelRaised).raycastTarget = false;

        var top = Row(box, "Top");
        var window = Text(t, top, "Window", "", 15, t.TextMuted, numerals: true);
        Flexible(window);
        var need = Text(t, top, "Need", "", 15, t.Text, TextAlignmentOptions.Right, numerals: true);

        var fill = Bar(t, box, "Progress", 8, t.Healthy);

        var bottom = Row(box, "Bottom");
        var time = Text(t, bottom, "Time", "", 13, t.TextMuted, style: FontStyles.Italic);
        Flexible(time);
        var state = Text(t, bottom, "State", "", 14, t.Text, TextAlignmentOptions.Right);

        var pay = Text(t, root, "Pay", "", 14, t.Money, style: FontStyles.Italic);

        var view = root.gameObject.AddComponent<ContractEngagementCardView>();
        Wire(view, ("_titleLabel", title), ("_bodyLabel", body), ("_windowLabel", window), ("_needLabel", need),
                   ("_progressFill", fill), ("_timeLabel", time), ("_stateLabel", state), ("_payLabel", pay));
        SetColours(view, ("_healthy", t.Healthy), ("_caution", t.Caution), ("_alarm", t.Alarm));
        return root.gameObject;
    }

    public static GameObject OfferCard(ScreenTheme t, string name)
    {
        var root = Card(name, out var button, out var group);
        var image = (Image)button.targetGraphic;
        image.sprite = t.PanelSprite;
        image.color = t.Panel;
        Object.DestroyImmediate(root.GetComponent<HorizontalLayoutGroup>());
        var vlg = root.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(14, 14, 10, 10);
        vlg.spacing = 3;
        vlg.childControlWidth = vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        var top = Row(root, "Top");
        var title = Text(t, top, "Title", "", 18, t.Text);
        Flexible(title);
        var badge = Rect("Badge", top);
        var badgeLe = badge.gameObject.AddComponent<LayoutElement>();
        badgeLe.preferredWidth = badgeLe.preferredHeight = 18;
        AddImage(badge, Era1SpriteFactory.Star, t.Achievement).raycastTarget = false;

        var body = Text(t, root, "Body", "", 13, t.TextMuted, style: FontStyles.Italic);
        var detail = Text(t, root, "Detail", "", 14, t.Money);
        var status = Text(t, root, "Status", "", 15, t.Text);

        var view = root.gameObject.AddComponent<ContractOfferCardView>();
        Wire(view, ("_titleLabel", title), ("_bodyLabel", body), ("_detailLabel", detail), ("_statusLabel", status),
                   ("_badge", badge.gameObject), ("_button", button), ("_group", group));
        SetColours(view, ("_statusColour", t.Text), ("_cautionColour", t.Caution));
        return root.gameObject;
    }

    // The contract managers hired (ContractManagersView), one card each, from a template:
    // name and on/off; the client it books, stepped with the arrows; what it's doing, and a
    // Start button for when it's off.
    public static void Managers(ScreenTheme t, Transform content, IdlePowerGen.Controllers.ContractDesk desk)
    {
        var header = SectionHeader(t, content, "ManagersHeader", "Contract managers");
        var rows = Stack("ManagerPanel", content);

        var card = Box(t, rows, "ManagerTemplate", 12, 6);
        var top = Row(card, "Top");
        Flexible(Text(t, top, "Name", "Manager 1", 17, t.Text));
        var toggle = Button(t, top, "Toggle", "On", 36, t.PanelRaised, 15);
        toggle.GetComponent<LayoutElement>().preferredWidth = 72;

        var choose = Row(card, "Choose", 6);
        var prev = Button(t, choose, "Prev", "<", 44, t.PanelRaised, 18);
        prev.GetComponent<LayoutElement>().preferredWidth = 48;
        var choice = Text(t, choose, "Choice", "", 15, t.Money, TextAlignmentOptions.Center);
        Flexible(choice);
        var next = Button(t, choose, "Next", ">", 44, t.PanelRaised, 18);
        next.GetComponent<LayoutElement>().preferredWidth = 48;

        var bottom = Row(card, "Bottom");
        Flexible(Text(t, bottom, "Status", "", 13, t.TextMuted, style: FontStyles.Italic));
        var start = Button(t, bottom, "Start", "Start", 40, t.PanelRaised, 15);
        start.GetComponent<LayoutElement>().preferredWidth = 84;

        var view = rows.gameObject.AddComponent<ContractManagersView>();
        Wire(view, ("_desk", desk), ("_rowTemplate", card.gameObject), ("_content", rows), ("_headerLabel", header));
        SetColours(view, ("_text", t.Text), ("_muted", t.TextMuted), ("_healthy", t.Healthy), ("_caution", t.Caution));
        header.gameObject.SetActive(false);
    }

    // Engagements running, then the board, on one scrolling page.
    public static void Contracts(ScreenTheme t, Transform content, IdlePowerGen.Controllers.ContractDesk desk,
                                 string engagementPrefabPath, string offerPrefabPath)
    {
        Managers(t, content, desk);
        var header = SectionHeader(t, content, "ContractsHeader", "Contracts");
        var running = Stack("EngagementPanel", content);
        var none = Text(t, content, "NoEngagements", "None running. Book a client from the board below.", 14,
                        t.TextMuted, style: FontStyles.Italic);
        var list = running.gameObject.AddComponent<ContractEngagementListView>();
        Wire(list, ("_works", desk),
             ("_cardPrefab", AssetDatabase.LoadAssetAtPath<ContractEngagementCardView>(engagementPrefabPath)),
             ("_content", running), ("_headerLabel", header), ("_emptyHint", none.gameObject));
        SetString(list, "_heading", "Contracts");

        SectionHeader(t, content, "BoardHeader", "On the board");
        var offers = Stack("OfferPanel", content);
        var offerList = offers.gameObject.AddComponent<ContractOfferListView>();
        Wire(offerList, ("_works", desk),
             ("_cardPrefab", AssetDatabase.LoadAssetAtPath<ContractOfferCardView>(offerPrefabPath)), ("_content", offers));
    }

    // ---- The era's capstone ----

    // The capstone (CapstoneView): a gold banner in the column (put it just above the tab
    // bar) and the panel that opens when the goal is met, over the whole screen.
    public static void Capstone(ScreenTheme t, Transform column, Transform screenRoot,
                                IdlePowerGen.Controllers.ContractDesk desk,
                                string kicker, string title, string story, string goLabel)
    {
        var holder = Stack("Capstone", column, spacing: 0);
        var banner = Button(t, holder, "Banner", goLabel, 52, Color.Lerp(t.PanelRaised, t.Achievement, 0.28f), 16);
        var bannerLabel = banner.GetComponentInChildren<TextMeshProUGUI>();
        bannerLabel.color = t.Text;
        bannerLabel.fontStyle = FontStyles.Bold;
        var ring = banner.gameObject.AddComponent<Outline>();
        ring.effectColor = t.Achievement;
        ring.effectDistance = new Vector2(2, -2);

        var panel = Rect("CapstonePanel", screenRoot);
        Stretch(panel);
        AddImage(panel, Era1SpriteFactory.White, t.Overlay);
        var group = panel.gameObject.AddComponent<CanvasGroup>();
        var box = Stack("Box", panel, spacing: 12);
        box.anchorMin = new Vector2(0, 0.5f);
        box.anchorMax = new Vector2(1, 0.5f);
        box.pivot = new Vector2(0.5f, 0.5f);
        box.offsetMin = new Vector2(Gutter, 0);
        box.offsetMax = new Vector2(-Gutter, 0);
        box.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(20, 20, 24, 20);
        box.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        Panel(t, box, t.PanelRaised);
        Text(t, box, "Kicker", kicker, 14, t.TextMuted, TextAlignmentOptions.Center, FontStyles.Italic);
        Text(t, box, "Title", title, 24, t.Achievement, TextAlignmentOptions.Center);
        Text(t, box, "Story", story, 15, t.Text, TextAlignmentOptions.Center);
        var go = Button(t, box, "Go", goLabel, 52, Color.Lerp(t.PanelRaised, t.Achievement, 0.28f), 16);
        go.GetComponentInChildren<TextMeshProUGUI>().color = t.Text;
        var later = Button(t, box, "Later", "Not yet · finish up here first", 44, t.Panel, 14);
        later.GetComponentInChildren<TextMeshProUGUI>().color = t.TextMuted;

        var view = holder.gameObject.AddComponent<CapstoneView>();
        Wire(view, ("_desk", desk), ("_panel", panel.gameObject), ("_panelGroup", group), ("_go", go), ("_later", later),
                   ("_banner", banner.gameObject), ("_bannerButton", banner));
        panel.gameObject.SetActive(false);
        banner.gameObject.SetActive(false);
    }

    // ---- Era title card ----

    // The card between eras, shown when the era opens (EraTitleCardView).
    public static void TitleCard(ScreenTheme t, Transform canvas, int toEra, string kicker, string title, string date,
                                 string body, string button)
    {
        var holder = Rect($"Era{toEra}TitleCard", canvas);
        Stretch(holder);

        var panel = Rect("Panel", holder);
        Stretch(panel);
        AddImage(panel, Era1SpriteFactory.White, t.Overlay);
        var group = panel.gameObject.AddComponent<CanvasGroup>();

        var box = Stack("Box", panel, spacing: 12);
        box.anchorMin = new Vector2(0, 0.5f);
        box.anchorMax = new Vector2(1, 0.5f);
        box.pivot = new Vector2(0.5f, 0.5f);
        box.offsetMin = new Vector2(Gutter, 0);
        box.offsetMax = new Vector2(-Gutter, 0);
        box.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(20, 20, 24, 20);
        box.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        Panel(t, box, t.PanelRaised);

        Text(t, box, "Kicker", kicker, 14, t.TextMuted, TextAlignmentOptions.Center, FontStyles.Italic);
        Text(t, box, "Title", title, 28, t.Achievement, TextAlignmentOptions.Center);
        Text(t, box, "Date", date, 18, t.Text, TextAlignmentOptions.Center, FontStyles.Italic);
        Text(t, box, "Body", body, 16, t.Text, TextAlignmentOptions.Center);
        var open = Button(t, box, "Continue", button, 52);

        var view = holder.gameObject.AddComponent<EraTitleCardView>();
        Wire(view, ("_panel", panel.gameObject), ("_group", group), ("_continueButton", open));
        SetInt(view, "_toEra", toEra);
        panel.gameObject.SetActive(false);
    }
}
