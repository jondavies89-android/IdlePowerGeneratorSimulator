using IdlePowerGen.Controllers;
using IdlePowerGen.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static Era1SceneBuilder;
using static ScreenKit;

// The pieces the Era 4 and Era 5 screens share, in either era's theme: the frequency gauge,
// the unit board and its card, the sync panel and the synchroscope, the cascade warning, the
// preparedness dashboard, the coal yard, the feeder or city list, the unit shop, and the
// black-start overlay.
public static class UnitGridScreenKit
{
    // Title and date at the top; returns the date label for GridHeaderView.
    public static TextMeshProUGUI Header(ScreenTheme t, Transform parent, string title)
    {
        var header = Stack("Header", parent, spacing: 2);
        Fixed(header, 50);
        header.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
        var name = Text(t, header, "Title", title, 17, t.Text, TextAlignmentOptions.Center);
        name.characterSpacing = 18;
        name.textWrappingMode = TextWrappingModes.NoWrap;
        var date = Text(t, header, "Date", "", 13, t.TextMuted, TextAlignmentOptions.Center);
        date.characterSpacing = 6;
        return date;
    }

    // Two boxes, CASH and RESEARCH (the Era 4 mockup's foot), and the header view that runs them.
    public static void CashRow(ScreenTheme t, Transform parent, GridWorksController works, TextMeshProUGUI date,
                               string place, GridHeaderView.DateStyle style)
    {
        var row = Rect("CashRow", parent);
        Fixed(row, 58);
        var hlg = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 8;
        hlg.childControlWidth = hlg.childControlHeight = true;
        hlg.childForceExpandWidth = hlg.childForceExpandHeight = true;

        TextMeshProUGUI Box(string name, string caption, Color colour, out TextMeshProUGUI sub)
        {
            var b = Stack(name, row, spacing: 0);
            b.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
            b.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(6, 6, 4, 4);
            Panel(t, b);
            Caption(t, b, "Caption", caption, 11, TextAlignmentOptions.Center);
            var v = Text(t, b, "Value", "", 20, colour, TextAlignmentOptions.Center, numerals: true);
            v.textWrappingMode = TextWrappingModes.NoWrap;
            sub = Text(t, b, "Sub", "", 10, t.TextMuted, TextAlignmentOptions.Center);
            return v;
        }
        var cash = Box("Cash", "Cash", t.Money, out var income);
        var research = Box("Research", "Research", t.Text, out var rate);
        income.color = t.Healthy;

        var view = row.gameObject.AddComponent<GridHeaderView>();
        Wire(view, ("_works", works), ("_dateLabel", date), ("_cashLabel", cash), ("_researchLabel", rate),
                   ("_incomeLabel", income));
        SetString(view, "_place", place);
        var so = new SerializedObject(view);
        so.FindProperty("_style").enumValueIndex = (int)style;
        so.ApplyModifiedPropertiesWithoutUndo();
        // The research box's big number: points held.
        var points = research.gameObject.AddComponent<ResearchPointsLabel>();
        Wire(points, ("_label", research));
    }

    public static void Gauge(ScreenTheme t, Transform parent, UnitGridController grid, Color face, Color needleColour)
    {
        var panel = Rect("FrequencyPanel", parent);
        Fixed(panel, 150);
        Panel(t, panel);
        var hlg = panel.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(12, 12, 12, 12);
        hlg.spacing = 14;
        hlg.childControlWidth = hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = true;

        var dial = Rect("Dial", panel);
        var dialLe = dial.gameObject.AddComponent<LayoutElement>();
        dialLe.preferredWidth = 170;
        AddImage(dial, Era1SpriteFactory.Panel, face).type = Image.Type.Sliced;
        var arc = Rect("Arc", dial);
        arc.anchorMin = arc.anchorMax = new Vector2(0.5f, 0f);
        arc.pivot = new Vector2(0.5f, 0.5f);
        arc.sizeDelta = new Vector2(150, 150);
        arc.anchoredPosition = new Vector2(0, 28);
        AddImage(arc, GridSpriteFactory.HalfRing, t.Text * new Color(1, 1, 1, 0.8f)).raycastTarget = false;
        // The green band round 50 Hz.
        var band = Rect("Band", dial);
        band.anchorMin = band.anchorMax = new Vector2(0.5f, 0f);
        band.pivot = new Vector2(0.5f, 0.5f);
        band.sizeDelta = new Vector2(150, 150);
        band.anchoredPosition = new Vector2(0, 28);
        var bandImg = AddImage(band, GridSpriteFactory.HalfRing, t.Healthy);
        bandImg.raycastTarget = false;
        bandImg.type = Image.Type.Filled;
        bandImg.fillMethod = Image.FillMethod.Radial360;
        bandImg.fillOrigin = (int)Image.Origin360.Top;
        bandImg.fillAmount = 0.12f;
        // Filled clockwise from twelve; turned back by half its width so it sits round 50 Hz.
        band.localRotation = Quaternion.Euler(0, 0, 0.06f * 360);

        var needle = Rect("Needle", dial);
        needle.anchorMin = needle.anchorMax = new Vector2(0.5f, 0f);
        needle.pivot = new Vector2(0.5f, 0f);
        needle.sizeDelta = new Vector2(4, 66);
        needle.anchoredPosition = new Vector2(0, 28);
        AddImage(needle, Era1SpriteFactory.White, needleColour).raycastTarget = false;
        var hub = Rect("Hub", dial);
        hub.anchorMin = hub.anchorMax = new Vector2(0.5f, 0f);
        hub.sizeDelta = new Vector2(14, 14);
        hub.anchoredPosition = new Vector2(0, 28);
        AddImage(hub, Era1SpriteFactory.Disc, needleColour).raycastTarget = false;
        var lo = Text(t, dial, "Lo", "49", 11, t.TextMuted, TextAlignmentOptions.Center);
        Corner(lo.rectTransform, new Vector2(0.14f, 0), new Vector2(0, 12));
        var hi = Text(t, dial, "Hi", "51", 11, t.TextMuted, TextAlignmentOptions.Center);
        Corner(hi.rectTransform, new Vector2(0.86f, 0), new Vector2(0, 12));
        var hz = Text(t, dial, "Hz", "50.0 Hz", 14, t.Text, TextAlignmentOptions.Center);
        Corner(hz.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 6));

        var right = Stack("Readout", panel, spacing: 2);
        Flexible(right);
        Caption(t, right, "Caption", "System frequency", 11);
        var value = Text(t, right, "Value", "50.00", 34, t.Money, numerals: true);
        var trend = Text(t, right, "Trend", "steady", 13, t.Healthy);
        var rule = Rect("Rule", right);
        Fixed(rule, 1);
        AddImage(rule, Era1SpriteFactory.White, t.Edge).raycastTarget = false;
        Caption(t, right, "LoadCaption", "Load", 11);
        var load = Text(t, right, "Load", "0 W", 20, t.Text, numerals: true);

        var view = panel.gameObject.AddComponent<FrequencyGaugeView>();
        Wire(view, ("_grid", grid), ("_needle", needle), ("_valueLabel", value), ("_trendLabel", trend), ("_loadLabel", load));
        SetColours(view, ("_healthy", t.Healthy), ("_caution", t.Caution), ("_alarm", t.Alarm), ("_text", t.Money));
    }

    private static void Corner(RectTransform rt, Vector2 anchor, Vector2 pos)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0f);
        rt.sizeDelta = new Vector2(60, 16);
        rt.anchoredPosition = pos;
    }

    // A full-width button row used by lists: Name, Detail (left), Action (right).
    public static UnityEngine.UI.Button RowTemplate(ScreenTheme t, Transform parent, string name, bool withAction = true)
    {
        var root = Card(name, out var button, out _);
        root.SetParent(parent, false);
        var image = (Image)button.targetGraphic;
        image.sprite = t.PanelSprite;
        image.color = t.Panel;
        var texts = Stack("Texts", root, spacing: 2);
        Flexible(texts);
        Text(t, texts, "Name", "", 16, t.Text);
        Text(t, texts, "Detail", "", 12, t.TextMuted);
        if (withAction)
        {
            var action = Text(t, root, "Action", "", 14, t.Money, TextAlignmentOptions.Right, numerals: true);
            action.gameObject.AddComponent<LayoutElement>().preferredWidth = 96;
        }
        return button;
    }

    public static UnitCardView Board(ScreenTheme t, Transform parent, UnitGridController grid, SyncOverlayView sync,
                                     bool grouped, string caption)
    {
        var box = Box(t, parent, "UnitBoard", 12, 2);
        Caption(t, box, "Caption", caption, 13);
        var rows = Stack("Rows", box, spacing: 0);

        var template = Rect("RowTemplate", rows);
        Fixed(template, 30);
        var bg = AddImage(template, Era1SpriteFactory.White, new Color(0, 0, 0, 0));
        var button = template.gameObject.AddComponent<UnityEngine.UI.Button>();
        button.targetGraphic = bg;
        var hlg = template.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.childControlWidth = hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = true;
        var n = Text(t, template, "Name", "", 14, t.Text);
        n.textWrappingMode = TextWrappingModes.NoWrap;
        n.overflowMode = TextOverflowModes.Ellipsis;
        Flexible(n);
        var s = Text(t, template, "State", "", 14, t.Healthy, TextAlignmentOptions.Right);
        s.textWrappingMode = TextWrappingModes.NoWrap;

        var reserve = Text(t, box, "Reserve", "", 14, t.Text);
        reserve.textWrappingMode = TextWrappingModes.NoWrap;

        // The card under the board.
        var cardRt = Card("UnitCard", out var cardButton, out var cardGroup);
        cardRt.SetParent(box, false);
        ((Image)cardButton.targetGraphic).sprite = t.PanelSprite;
        ((Image)cardButton.targetGraphic).color = t.PanelRaised;
        Object.DestroyImmediate(cardRt.GetComponent<HorizontalLayoutGroup>());
        var cvlg = cardRt.gameObject.AddComponent<VerticalLayoutGroup>();
        cvlg.padding = new RectOffset(12, 12, 8, 8);
        cvlg.spacing = 2;
        cvlg.childControlWidth = cvlg.childControlHeight = true;
        cvlg.childForceExpandWidth = true;
        cvlg.childForceExpandHeight = false;
        var title = Text(t, cardRt, "Title", "", 16, t.Text);
        var detail = Text(t, cardRt, "Detail", "", 12, t.TextMuted);
        var action = Text(t, cardRt, "Action", "", 14, t.Money);
        var card = cardRt.gameObject.AddComponent<UnitCardView>();
        Wire(card, ("_grid", grid), ("_titleLabel", title), ("_detailLabel", detail), ("_actionLabel", action),
                   ("_button", cardButton), ("_group", cardGroup), ("_sync", sync));
        SetColours(card, ("_text", t.Text), ("_muted", t.TextMuted), ("_caution", t.Caution), ("_alarm", t.Alarm));

        var view = box.gameObject.AddComponent<UnitBoardView>();
        Wire(view, ("_grid", grid), ("_rowTemplate", button), ("_content", rows), ("_reserveLabel", reserve), ("_card", card));
        SetColours(view, ("_on", t.Healthy), ("_ramp", t.Caution), ("_off", t.TextMuted), ("_ready", t.Money),
                         ("_fault", t.Alarm), ("_text", t.Text), ("_highlight", t.Text * new Color(1, 1, 1, 0.07f)));
        SetBool(view, "_grouped", grouped);
        return card;
    }

    public static void SyncPanel(ScreenTheme t, Transform parent, UnitGridController grid, SyncOverlayView overlay)
    {
        var holder = Stack("SyncPanel", parent, spacing: 0);
        var panel = Row(holder, "Panel", 10);
        panel.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(14, 12, 10, 10);
        Panel(t, panel);
        var texts = Stack("Texts", panel, spacing: 1);
        Flexible(texts);
        var title = Text(t, texts, "Title", "", 15, t.Text);
        var hint = Text(t, texts, "Hint", "", 12, t.TextMuted, style: FontStyles.Italic);
        var sync = ScreenKit.Button(t, panel, "Sync", "SYNC", 44, t.PanelRaised, 16);
        sync.gameObject.GetComponent<LayoutElement>().preferredWidth = 96;
        sync.GetComponentInChildren<TextMeshProUGUI>().characterSpacing = 10;
        var view = holder.gameObject.AddComponent<SyncPanelView>();
        Wire(view, ("_grid", grid), ("_panel", panel.gameObject), ("_titleLabel", title), ("_hintLabel", hint),
                   ("_sync", sync), ("_overlay", overlay));
        panel.gameObject.SetActive(false);
    }

    public static SyncOverlayView SyncOverlay(ScreenTheme t, Transform canvas, UnitGridController grid, string name)
    {
        var holder = Rect(name, canvas);
        Stretch(holder);
        var panel = Rect("Panel", holder);
        Stretch(panel);
        AddImage(panel, Era1SpriteFactory.White, t.Overlay);

        var box = Stack("Box", panel, spacing: 10);
        box.anchorMin = new Vector2(0, 0.5f);
        box.anchorMax = new Vector2(1, 0.5f);
        box.pivot = new Vector2(0.5f, 0.5f);
        box.offsetMin = new Vector2(Gutter, 0);
        box.offsetMax = new Vector2(-Gutter, 0);
        box.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(16, 16, 18, 16);
        box.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        Panel(t, box, t.PanelRaised);

        var title = Text(t, box, "Title", "", 17, t.Text, TextAlignmentOptions.Center);
        var dialHolder = Rect("DialHolder", box);
        Fixed(dialHolder, 220);
        var dial = Rect("Dial", dialHolder);
        dial.anchorMin = dial.anchorMax = new Vector2(0.5f, 0.5f);
        dial.sizeDelta = new Vector2(210, 210);
        AddImage(dial, Era1SpriteFactory.Disc, Era4Theme.GaugeFace).raycastTarget = false;
        var ring = Rect("Ring", dial);
        Stretch(ring);
        AddImage(ring, GridSpriteFactory.ThinRing, t.Text * new Color(1, 1, 1, 0.7f)).raycastTarget = false;
        // The clean-close zone at twelve o'clock.
        var zone = Rect("Zone", dial);
        Stretch(zone);
        var zoneImg = AddImage(zone, GridSpriteFactory.Ring, t.Healthy);
        zoneImg.raycastTarget = false;
        zoneImg.type = Image.Type.Filled;
        zoneImg.fillMethod = Image.FillMethod.Radial360;
        zoneImg.fillOrigin = (int)Image.Origin360.Top;
        zoneImg.fillAmount = 30f / 360f;
        zone.localRotation = Quaternion.Euler(0, 0, 15f);
        var needle = Rect("Needle", dial);
        needle.anchorMin = needle.anchorMax = new Vector2(0.5f, 0.5f);
        needle.pivot = new Vector2(0.5f, 0f);
        needle.sizeDelta = new Vector2(5, 92);
        AddImage(needle, Era1SpriteFactory.White, Era4Theme.Needle).raycastTarget = false;
        var hub = Rect("Hub", dial);
        hub.anchorMin = hub.anchorMax = new Vector2(0.5f, 0.5f);
        hub.sizeDelta = new Vector2(16, 16);
        AddImage(hub, Era1SpriteFactory.Disc, Era4Theme.Needle).raycastTarget = false;

        var slip = Text(t, box, "Slip", "", 16, t.Text, TextAlignmentOptions.Center, numerals: true);
        var hint = Text(t, box, "Hint", "", 13, t.TextMuted, TextAlignmentOptions.Center, FontStyles.Italic);
        var row = Rect("Controls", box);
        Fixed(row, 52);
        var hlg = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 8;
        hlg.childControlWidth = hlg.childControlHeight = true;
        hlg.childForceExpandWidth = hlg.childForceExpandHeight = true;
        var slower = ScreenKit.Button(t, row, "Slower", "Slower", 52, t.Panel);
        var close = ScreenKit.Button(t, row, "Close", "CLOSE", 52, t.AlarmPanel);
        close.GetComponentInChildren<TextMeshProUGUI>().characterSpacing = 10;
        var faster = ScreenKit.Button(t, row, "Faster", "Faster", 52, t.Panel);
        var later = ScreenKit.Button(t, box, "Later", "Not now", 44, t.Panel, 15);

        var view = holder.gameObject.AddComponent<SyncOverlayView>();
        Wire(view, ("_grid", grid), ("_panel", panel.gameObject), ("_titleLabel", title), ("_needle", needle),
                   ("_slipLabel", slip), ("_hintLabel", hint), ("_slower", slower), ("_faster", faster),
                   ("_close", close), ("_later", later));
        SetColours(view, ("_healthy", t.Healthy), ("_caution", t.Caution), ("_alarm", t.Alarm));
        panel.gameObject.SetActive(false);
        return view;
    }

    public static void Cascade(ScreenTheme t, Transform parent, UnitGridController grid, SyncOverlayView sync)
    {
        var holder = Stack("Cascade", parent, spacing: 0);
        var panel = Stack("Panel", holder, spacing: 4);
        panel.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(14, 14, 10, 10);
        var bg = Panel(t, panel, t.AlarmPanel);
        var title = Text(t, panel, "Title", "", 15, t.Alarm);
        title.characterSpacing = 4;
        var detail = Text(t, panel, "Detail", "", 13, t.Text);
        var row = Rect("Remedies", panel);
        Fixed(row, 44);
        var hlg = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 8;
        hlg.childControlWidth = hlg.childControlHeight = true;
        hlg.childForceExpandWidth = hlg.childForceExpandHeight = true;
        var syncButton = ScreenKit.Button(t, row, "SyncReserve", "", 44, t.PanelRaised, 14);
        var shedButton = ScreenKit.Button(t, row, "Shed", "", 44, t.PanelRaised, 14);
        var view = holder.gameObject.AddComponent<CascadeBannerView>();
        Wire(view, ("_grid", grid), ("_panel", panel.gameObject), ("_background", bg), ("_titleLabel", title),
                   ("_detailLabel", detail), ("_syncButton", syncButton), ("_syncLabel", syncButton.GetComponentInChildren<TextMeshProUGUI>()),
                   ("_shedButton", shedButton), ("_shedLabel", shedButton.GetComponentInChildren<TextMeshProUGUI>()), ("_sync", sync));
        SetColours(view, ("_cautionPanel", Color.Lerp(t.AlarmPanel, t.Caution, 0.25f)), ("_alarmPanel", t.AlarmPanel));
        panel.gameObject.SetActive(false);
    }

    public static void Dashboard(ScreenTheme t, Transform parent, Transform modalParent, UnitGridController grid)
    {
        var holder = Stack("Preparedness", parent, spacing: 0);
        var panel = Box(t, holder, "Panel", 12, 6);
        Caption(t, panel, "Caption", "Preparedness", 13);
        string[] names = { "Maintenance", "Fuel reserve", "Redundancy" };
        var rows = new Object[3]; var values = new Object[3]; var fills = new Object[3]; var actions = new Object[3]; var labels = new Object[3];
        for (int i = 0; i < 3; i++)
        {
            var r = Stack(names[i], panel, spacing: 2);
            var top = Row(r, "Top");
            Flexible(Text(t, top, "Name", names[i], 15, t.Text));
            var v = Text(t, top, "Value", "100", 17, t.Healthy, TextAlignmentOptions.Right, numerals: true);
            var f = Bar(t, r, "Bar", 6, t.Healthy);
            var a = ScreenKit.Button(t, r, "Action", "", 40, t.PanelRaised, 13);
            rows[i] = r.gameObject; values[i] = v; fills[i] = f; actions[i] = a; labels[i] = a.GetComponentInChildren<TextMeshProUGUI>();
            r.gameObject.SetActive(false);
        }

        // The introduction (bible §14.1): the story, the name, the first thing to do.
        var intro = Rect("ScoreIntro", modalParent);
        Stretch(intro);
        AddImage(intro, Era1SpriteFactory.White, t.Overlay);
        var box = Stack("Box", intro, spacing: 10);
        box.anchorMin = new Vector2(0, 0.5f);
        box.anchorMax = new Vector2(1, 0.5f);
        box.pivot = new Vector2(0.5f, 0.5f);
        box.offsetMin = new Vector2(Gutter, 0);
        box.offsetMax = new Vector2(-Gutter, 0);
        box.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(18, 18, 20, 18);
        box.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        Panel(t, box, t.PanelRaised);
        var introTitle = Text(t, box, "Title", "", 20, t.Achievement, TextAlignmentOptions.Center);
        var story = Text(t, box, "Story", "", 15, t.Text, TextAlignmentOptions.Center);
        var ok = ScreenKit.Button(t, box, "Ok", "To the dashboard", 50);

        var view = holder.gameObject.AddComponent<PreparednessView>();
        Wire(view, ("_grid", grid), ("_panel", panel.gameObject), ("_introPanel", intro.gameObject),
                   ("_introTitle", introTitle), ("_introStory", story), ("_introButton", ok));
        var so = new SerializedObject(view);
        SetArray(so.FindProperty("_rows"), rows);
        SetArray(so.FindProperty("_values"), values);
        SetArray(so.FindProperty("_fills"), fills);
        SetArray(so.FindProperty("_actions"), actions);
        SetArray(so.FindProperty("_actionLabels"), labels);
        so.ApplyModifiedPropertiesWithoutUndo();
        SetColours(view, ("_healthy", t.Healthy), ("_caution", t.Caution), ("_alarm", t.Alarm));
        panel.gameObject.SetActive(false);
        intro.gameObject.SetActive(false);
    }

    public static void Fuel(ScreenTheme t, Transform parent, UnitGridController grid)
    {
        var holder = Stack("Fuel", parent, spacing: 0);
        var panel = Box(t, holder, "Panel", 12, 4);
        var stock = Text(t, panel, "Stock", "", 15, t.Text);
        var fill = Bar(t, panel, "Bar", 6, t.Healthy);
        var order = ScreenKit.Button(t, panel, "Order", "", 44, t.PanelRaised, 15);
        var manager = Stack("Manager", panel, spacing: 2);
        var label = Text(t, manager, "Label", "", 13, t.TextMuted);
        var sliderRt = Rect("Target", manager);
        Fixed(sliderRt, 32);
        var slider = sliderRt.gameObject.AddComponent<Slider>();
        var track = Rect("Background", sliderRt);
        track.anchorMin = new Vector2(0, 0.5f); track.anchorMax = new Vector2(1, 0.5f); track.sizeDelta = new Vector2(0, 6);
        AddImage(track, Era1SpriteFactory.White, t.Track).raycastTarget = false;
        var fillArea = Rect("Fill Area", sliderRt);
        fillArea.anchorMin = new Vector2(0, 0.5f); fillArea.anchorMax = new Vector2(1, 0.5f); fillArea.sizeDelta = new Vector2(-20, 6);
        var sfill = Rect("Fill", fillArea);
        sfill.sizeDelta = new Vector2(10, 0);
        AddImage(sfill, Era1SpriteFactory.White, t.Money).raycastTarget = false;
        var handleArea = Rect("Handle Slide Area", sliderRt);
        Stretch(handleArea);
        handleArea.offsetMin = new Vector2(10, 0); handleArea.offsetMax = new Vector2(-10, 0);
        var handle = Rect("Handle", handleArea);
        handle.sizeDelta = new Vector2(26, 0);
        var hImg = AddImage(handle, Era1SpriteFactory.Disc, t.Money);
        slider.fillRect = sfill; slider.handleRect = handle; slider.targetGraphic = hImg;

        var view = holder.gameObject.AddComponent<FuelPanelView>();
        Wire(view, ("_grid", grid), ("_panel", panel.gameObject), ("_stockLabel", stock), ("_fill", fill), ("_order", order),
                   ("_orderLabel", order.GetComponentInChildren<TextMeshProUGUI>()), ("_manager", manager.gameObject),
                   ("_target", slider), ("_targetLabel", label));
        SetColours(view, ("_healthy", t.Healthy), ("_caution", t.Caution), ("_alarm", t.Alarm));
        manager.gameObject.SetActive(false);
        panel.gameObject.SetActive(false);
    }

    public static void Areas(ScreenTheme t, Transform content, UnitGridController grid, string heading)
    {
        SectionHeader(t, content, "AreasHeader", heading);
        var summary = Text(t, content, "Summary", "", 13, t.TextMuted, style: FontStyles.Italic);
        var rows = Stack("AreaPanel", content);
        var template = RowTemplate(t, rows, "AreaTemplate");
        var view = rows.gameObject.AddComponent<AreaListView>();
        Wire(view, ("_grid", grid), ("_rowTemplate", template), ("_content", rows), ("_summaryLabel", summary));
        SetColours(view, ("_text", t.Text), ("_muted", t.TextMuted), ("_money", t.Money), ("_caution", t.Caution));
    }

    public static void Shop(ScreenTheme t, Transform content, UnitGridController grid)
    {
        SectionHeader(t, content, "ShopHeader", "Build a unit");
        var rows = Stack("ShopPanel", content);
        var template = RowTemplate(t, rows, "UnitTemplate");
        var locked = Text(t, content, "Locked", "More kinds of unit come with research.", 13, t.TextMuted, style: FontStyles.Italic);
        var view = rows.gameObject.AddComponent<UnitShopView>();
        Wire(view, ("_grid", grid), ("_rowTemplate", template), ("_content", rows), ("_lockedHint", locked.gameObject));
    }

    public static void BlackStart(Transform canvas, UnitGridController grid, SyncOverlayView sync, string name)
    {
        var t = BlackStartTheme.Theme;
        var holder = Rect(name, canvas);
        Stretch(holder);
        var panel = Rect("Panel", holder);
        Stretch(panel);
        AddImage(panel, Era1SpriteFactory.White, t.Overlay);
        var safe = Rect("SafeArea", panel);
        Stretch(safe);
        safe.gameObject.AddComponent<SafeAreaFitter>();
        var column = Stack("Column", safe, spacing: 8);
        Stretch(column);
        column.GetComponent<VerticalLayoutGroup>().padding = new RectOffset((int)Gutter, (int)Gutter, 30, 16);

        var caption = Caption(t, column, "Caption", "Black start", 14, TextAlignmentOptions.Center);
        caption.color = t.Caution;
        var timer = Text(t, column, "Timer", "T+00:00", 44, t.Alarm, TextAlignmentOptions.Center, numerals: true);
        var situation = Text(t, column, "Situation", "", 14, t.Text, TextAlignmentOptions.Center);
        var phase = Text(t, column, "Phase", "", 17, t.Caution, TextAlignmentOptions.Center);

        var barRow = Stack("BarRow", column, spacing: 2);
        var bar = Bar(t, barRow, "Bar", 10, t.Caution);
        var barLabel = Text(t, barRow, "Label", "", 12, t.TextMuted, TextAlignmentOptions.Center);

        var crank = Rect("Crank", column);
        Fixed(crank, 160);
        var crankImg = Rect("Wheel", crank);
        crankImg.anchorMin = crankImg.anchorMax = new Vector2(0.5f, 0.5f);
        crankImg.sizeDelta = new Vector2(130, 130);
        crankImg.anchoredPosition = new Vector2(0, 12);
        var wheel = AddImage(crankImg, Era1SpriteFactory.Crank, Color.white);
        var crankButton = crankImg.gameObject.AddComponent<UnityEngine.UI.Button>();
        crankButton.targetGraphic = wheel;
        crankImg.gameObject.AddComponent<PressFeedback>();
        crankImg.gameObject.AddComponent<Era1CrankSpinView>();
        var crankLabel = Text(t, crank, "Label", "", 14, t.Text, TextAlignmentOptions.Center);
        crankLabel.rectTransform.anchorMin = new Vector2(0, 0);
        crankLabel.rectTransform.anchorMax = new Vector2(1, 0);
        crankLabel.rectTransform.pivot = new Vector2(0.5f, 0);
        crankLabel.rectTransform.sizeDelta = new Vector2(0, 20);

        var gates = ScreenKit.Button(t, column, "Gates", "Hold the gates open", 64, t.PanelRaised, 17);
        var hold = gates.gameObject.AddComponent<HoldButton>();

        var listScroll = ScreenKit.Scroll(column);
        var template = RowTemplate(t, listScroll, "ItemTemplate", withAction: false);
        var message = Text(t, column, "Message", "", 13, t.TextMuted, TextAlignmentOptions.Center, FontStyles.Italic);
        var primary = ScreenKit.Button(t, column, "Primary", "", 50, t.PanelRaised, 15);
        var secondary = ScreenKit.Button(t, column, "Secondary", "", 46, t.Panel, 14);
        var tertiary = ScreenKit.Button(t, column, "Tertiary", "", 42, t.Panel, 14);

        var view = holder.gameObject.AddComponent<BlackStartView>();
        Wire(view, ("_grid", grid), ("_panel", panel.gameObject), ("_timerLabel", timer), ("_phaseLabel", phase),
                   ("_situationLabel", situation), ("_messageLabel", message), ("_list", listScroll), ("_itemTemplate", template),
                   ("_crank", crankButton), ("_crankLabel", crankLabel), ("_gates", hold),
                   ("_gatesLabel", gates.GetComponentInChildren<TextMeshProUGUI>()), ("_bar", bar), ("_barLabel", barLabel),
                   ("_primary", primary), ("_primaryLabel", primary.GetComponentInChildren<TextMeshProUGUI>()),
                   ("_secondary", secondary), ("_secondaryLabel", secondary.GetComponentInChildren<TextMeshProUGUI>()),
                   ("_tertiary", tertiary), ("_tertiaryLabel", tertiary.GetComponentInChildren<TextMeshProUGUI>()), ("_sync", sync));
        SetColours(view, ("_ready", t.Healthy), ("_caution", t.Caution), ("_muted", t.TextMuted));
        panel.gameObject.SetActive(false);
    }
}
