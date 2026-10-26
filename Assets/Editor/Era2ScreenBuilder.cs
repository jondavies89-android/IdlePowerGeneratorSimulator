using System.Linq;
using IdlePowerGen.UI;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;
using static Era1SceneBuilder;

// Builds the Era 2 screen into the Era 1 scene (todo Era 2 §2.2, option B: one scene, one
// screen per era). Layout follows the bible's Appendix A mockup: the office header, the
// Capacity · Purse · Repute panel, the engagements with their progress bars, the machines
// with their running costs in coral, and the coal hopper warning with its order button.
public static class Era2ScreenBuilder
{
    private const string EngagementCardPrefab = PrefabFolder + "/Era2EngagementCard.prefab";
    private const string OfferCardPrefab      = PrefabFolder + "/Era2OfferCard.prefab";
    private const string GeneratorRowPrefab   = PrefabFolder + "/Era2GeneratorRow.prefab";
    private const string UpgradeNodePrefab    = PrefabFolder + "/Era2UpgradeNode.prefab";
    // The upgrade list's card before the tree replaced it.
    private const string StaleUpgradeCardPrefab = PrefabFolder + "/Era2UpgradeCard.prefab";

    public static void BuildPrefabs()
    {
        Era1AssetBuilder.EnsureFolder(PrefabFolder);
        SaveAsPrefab(BuildEngagementCard(), EngagementCardPrefab);
        SaveAsPrefab(BuildOfferCard(), OfferCardPrefab);
        SaveAsPrefab(BuildGeneratorRow(), GeneratorRowPrefab);
        SaveAsPrefab(BuildUpgradeNode(), UpgradeNodePrefab);
        AssetDatabase.DeleteAsset(StaleUpgradeCardPrefab);
    }

    // Returns the screen root, inactive until EraScreenSwitcher shows it.
    internal static RectTransform Create(Transform canvas, Controllers controllers, ToastView toast)
    {
        var root = Rect("Era2Screen", canvas);
        Stretch(root);
        var bg = Rect("Background", root);
        Stretch(bg);
        AddImage(bg, null, Era2Theme.Background).raycastTarget = false;

        var safe = Rect("SafeArea", root);
        Stretch(safe);
        safe.gameObject.AddComponent<SafeAreaFitter>();

        var column = Rect("Column", safe);
        Stretch(column);
        var vlg = column.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset((int)Gutter, (int)Gutter, 12, 0);
        vlg.spacing = 6;
        vlg.childControlWidth = vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        CreateHeader(column, controllers);
        CreateStats(column, controllers);
        CreateDayBar(column);
        CreateContractBoost(column);
        CreatePages(column, root, controllers, toast);

        root.gameObject.SetActive(false);
        return root;
    }

    // ---- Header, stats, day bar ----

    private static void CreateHeader(Transform parent, Controllers controllers)
    {
        var header = Rect("Header", parent);
        Fixed(header, 50);
        var vlg = header.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.MiddleCenter;
        vlg.childControlWidth = vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        var title = Text(header, "Title", "— THE DEMONSTRATION OFFICE —", 15, Era2Theme.TextMuted, TextAlignmentOptions.Center);
        title.characterSpacing = 12;
        var date = Text(header, "Date", "", 17, Era2Theme.Text, TextAlignmentOptions.Center, FontStyles.Italic);

        var view = header.gameObject.AddComponent<Era2HeaderView>();
        Wire(view, ("_works", controllers.Works), ("_dateLabel", date));
    }

    private static void CreateStats(Transform parent, Controllers controllers)
    {
        var panel = Rect("Stats", parent);
        Fixed(panel, 86);
        Panel(panel);
        var hlg = panel.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(8, 8, 10, 10);
        hlg.childControlWidth = hlg.childControlHeight = true;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = true;

        var capacity = StatCell(panel, "Capacity", "CAPACITY", "0 W", Era2Theme.Text);
        var purse    = StatCell(panel, "Purse", "PURSE", "0 d", Era2Theme.Purse);
        // The steady income under the purse: what an idle game owes the player at a glance.
        var income = Text(purse.transform.parent, "Income", "", 12, Era2Theme.TextMuted, TextAlignmentOptions.Center,
                          numerals: true);

        var repute = Stack("Repute", panel, spacing: 2);
        repute.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
        Caption(repute, "REPUTE");
        var starsRow = Rect("Stars", repute);
        Fixed(starsRow, 26);
        var srow = starsRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        srow.childAlignment = TextAnchor.MiddleCenter;
        srow.spacing = 2;
        srow.childControlWidth = srow.childControlHeight = true;
        srow.childForceExpandWidth = srow.childForceExpandHeight = false;
        var stars = new Image[5];
        for (int i = 0; i < stars.Length; i++)
        {
            // An empty star behind a filled one: part-earned stars fill from the left.
            var slot = Rect($"Star{i}", starsRow);
            var le = slot.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = le.preferredHeight = 22;
            AddImage(slot, Era1SpriteFactory.Star, Era2Theme.StarsEmpty).raycastTarget = false;
            var fillRt = Rect("Fill", slot);
            Stretch(fillRt);
            var fill = AddImage(fillRt, Era1SpriteFactory.Star, Era2Theme.Stars);
            fill.raycastTarget = false;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillAmount = 0f;
            stars[i] = fill;
        }
        var reputeLabel = Text(repute, "Value", "0 repute", 12, Era2Theme.TextMuted, TextAlignmentOptions.Center);

        var supply = Text(parent, "Supply", "", 14, Era2Theme.TextMuted, TextAlignmentOptions.Center, FontStyles.Italic);
        Fixed(supply.rectTransform, 20);

        var view = panel.gameObject.AddComponent<Era2StatsView>();
        Wire(view, ("_works", controllers.Works), ("_capacityLabel", capacity), ("_purseLabel", purse),
                   ("_incomeLabel", income), ("_reputeLabel", reputeLabel), ("_supplyLabel", supply));
        var so = new SerializedObject(view);
        var starsProp = so.FindProperty("_stars");
        starsProp.arraySize = stars.Length;
        for (int i = 0; i < stars.Length; i++) starsProp.GetArrayElementAtIndex(i).objectReferenceValue = stars[i];
        so.FindProperty("_healthy").colorValue = Era2Theme.Healthy;
        so.FindProperty("_caution").colorValue = Era2Theme.Caution;
        so.FindProperty("_alarm").colorValue   = Era2Theme.Alarm;
        so.FindProperty("_muted").colorValue   = Era2Theme.TextMuted;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static TextMeshProUGUI StatCell(Transform parent, string name, string caption, string value, Color colour)
    {
        var cell = Stack(name, parent, spacing: 2);
        cell.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
        Caption(cell, caption);
        return Text(cell, "Value", value, 28, colour, TextAlignmentOptions.Center, numerals: true);
    }

    private static void Caption(Transform parent, string caption)
    {
        var t = Text(parent, "Caption", caption, 12, Era2Theme.TextMuted, TextAlignmentOptions.Center);
        t.characterSpacing = 8;
    }

    private static void CreateDayBar(Transform parent)
    {
        var row = Rect("DayBar", parent);
        Fixed(row, 22);
        var hlg = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 10;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = true;

        var clock = Text(row, "Clock", "18:00", 16, Era2Theme.Text, TextAlignmentOptions.Left, numerals: true);
        clock.gameObject.AddComponent<LayoutElement>().preferredWidth = 48;

        var bar = Rect("Day", row);
        bar.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
        var track = Rect("Track", bar);
        track.anchorMin = new Vector2(0, 0.5f);
        track.anchorMax = new Vector2(1, 0.5f);
        track.sizeDelta = new Vector2(0, 8);
        AddImage(track, Era1SpriteFactory.White, Era2Theme.Track).raycastTarget = false;

        // Daylight, 06:00 to 20:00, a shade lighter than the night either side.
        var day = Rect("Daylight", track);
        day.anchorMin = new Vector2(6f / 24f, 0);
        day.anchorMax = new Vector2(20f / 24f, 1);
        day.offsetMin = day.offsetMax = Vector2.zero;
        AddImage(day, Era1SpriteFactory.White, Era2Theme.Brass * new Color(1, 1, 1, 0.5f)).raycastTarget = false;

        var marker = Rect("Now", bar);
        marker.anchorMin = new Vector2(0.75f, 0);
        marker.anchorMax = new Vector2(0.75f, 1);
        marker.sizeDelta = new Vector2(3, 0);
        AddImage(marker, Era1SpriteFactory.White, Era2Theme.Purse).raycastTarget = false;

        var view = row.gameObject.AddComponent<Era2DayBarView>();
        Wire(view, ("_marker", marker), ("_clockLabel", clock));
    }

    // Above the tabs so it shows on every page; 44 tall, the minimum tap target.
    private static void CreateContractBoost(Transform parent)
    {
        var rt = Rect("ContractBoost", parent);
        Fixed(rt, 44);
        var image = AddImage(rt, Era1Theme.PanelSprite, Era2Theme.PanelRaised);
        image.type = Image.Type.Sliced;
        var button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        rt.gameObject.AddComponent<PressFeedback>();
        var label = Text(rt, "Label", "Contracts complete 2× faster", 16, Era2Theme.Purse, TextAlignmentOptions.Center);
        Stretch(label.rectTransform);

        var view = rt.gameObject.AddComponent<Era2ContractBoostView>();
        Wire(view, ("_button", button), ("_label", label), ("_background", image));
        SetColours(view, ("_idle", Era2Theme.PanelRaised), ("_active", Era2Theme.BoostPanel),
                         ("_idleText", Era2Theme.Purse), ("_activeText", Era2Theme.Healthy));
    }

    // ---- Tabs and their pages ----

    private static readonly string[] TabNames = { "Clients", "Works", "Improvements" };

    // Three pages under the fixed header, switched by a tab bar at the foot of the screen:
    // one long scroll held the clients, the machines and the improvements in a list the
    // player had to hunt through.
    private static void CreatePages(Transform parent, Transform root, Controllers controllers, ToastView toast)
    {
        var pagesRt = Rect("Pages", parent);
        var le = pagesRt.gameObject.AddComponent<LayoutElement>();
        le.flexibleHeight = 1;
        le.minHeight = 200;

        var clients = Page(pagesRt, "ClientsPage");
        CreateClients(Scroll(clients), controllers);

        var works = Page(pagesRt, "WorksPage");
        CreateWorks(Scroll(works), controllers, toast);

        var improvements = Page(pagesRt, "ImprovementsPage");
        var tree = CreateImprovements(improvements);

        ScreenKit.Capstone(Era2Theme.Screen, parent, root, controllers.Works, "Era 2 · The Demonstration",
                           "The Crystal Palace is lit",
                           "All London came to see the Great Exhibition by your light. The demonstration is over: " +
                           "forty years on, a whole town will want lighting.",
                           "Begin Era 3 · The War of Currents");
        CreateTabBar(parent, controllers, new[] { clients, works, improvements }, tree);
    }

    private static RectTransform Page(Transform parent, string name)
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

    private static void CreateTabBar(Transform parent, Controllers controllers, RectTransform[] pages,
                                     UpgradeTreeView tree)
    {
        var bar = Rect("TabBar", parent);
        Fixed(bar, 56);
        var hlg = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(0, 0, 4, 8);
        hlg.spacing = 6;
        hlg.childControlWidth = hlg.childControlHeight = true;
        hlg.childForceExpandWidth = hlg.childForceExpandHeight = true;

        var buttons = new Button[TabNames.Length];
        var backgrounds = new Image[TabNames.Length];
        var labels = new TextMeshProUGUI[TabNames.Length];
        var badges = new Image[TabNames.Length];
        for (int i = 0; i < TabNames.Length; i++)
        {
            var tab = Rect("Tab_" + TabNames[i], bar);
            var image = AddImage(tab, Era1Theme.PanelSprite, Era2Theme.Panel);
            image.type = Image.Type.Sliced;
            var button = tab.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            tab.gameObject.AddComponent<PressFeedback>();
            var label = Text(tab, "Label", TabNames[i], 16, Era2Theme.TextMuted, TextAlignmentOptions.Center);
            Stretch(label.rectTransform);

            var badge = Rect("Badge", tab);
            badge.anchorMin = badge.anchorMax = new Vector2(1, 1);
            badge.pivot = new Vector2(1, 1);
            badge.sizeDelta = new Vector2(10, 10);
            badge.anchoredPosition = new Vector2(-7, -7);
            var dot = AddImage(badge, Era1SpriteFactory.Disc, Era2Theme.Purse);
            dot.raycastTarget = false;
            badge.gameObject.SetActive(false);

            buttons[i] = button; backgrounds[i] = image; labels[i] = label; badges[i] = dot;
        }

        var view = bar.gameObject.AddComponent<TabBarView>();
        var so = new SerializedObject(view);
        SetObjects(so.FindProperty("_pages"), pages.Select(p => (Object)p.GetComponent<CanvasGroup>()).ToArray());
        SetObjects(so.FindProperty("_tabs"), buttons);
        SetObjects(so.FindProperty("_tabBackgrounds"), backgrounds);
        SetObjects(so.FindProperty("_tabLabels"), labels);
        SetObjects(so.FindProperty("_badges"), badges);
        so.FindProperty("_selected").colorValue = Era2Theme.PanelRaised;
        so.FindProperty("_unselected").colorValue = Era2Theme.Panel;
        so.FindProperty("_selectedText").colorValue = Era2Theme.Text;
        so.FindProperty("_unselectedText").colorValue = Era2Theme.TextMuted;
        so.ApplyModifiedPropertiesWithoutUndo();

        var badgesView = bar.gameObject.AddComponent<Era2TabBadgesView>();
        Wire(badgesView, ("_tabs", view), ("_works", controllers.Works), ("_tree", tree));
        SetColours(badgesView, ("_opportunity", Era2Theme.Purse), ("_alarm", Era2Theme.Alarm));
    }

    private static void SetObjects(SerializedProperty prop, Object[] items) => SetArray(prop, items);

    // The contract managers, the engagements running now, then the board of clients.
    private static void CreateClients(Transform content, Controllers controllers)
    {
        ScreenKit.Managers(Era2Theme.Screen, content, controllers.Works);

        // Engagements running now.
        var engagementsHeader = SectionHeader(content, "EngagementsHeader", "Engagements");
        var engagements = Stack("EngagementPanel", content);
        var noEngagements = Text(content, "NoEngagements", "None running. Book a client from the board below.",
                                 14, Era2Theme.TextMuted, TextAlignmentOptions.Left, FontStyles.Italic);
        var engagementList = engagements.gameObject.AddComponent<ContractEngagementListView>();
        Wire(engagementList, ("_works", controllers.Works),
             ("_cardPrefab", AssetDatabase.LoadAssetAtPath<ContractEngagementCardView>(EngagementCardPrefab)),
             ("_content", engagements), ("_headerLabel", engagementsHeader), ("_emptyHint", noEngagements.gameObject));

        // The contract board.
        SectionHeader(content, "BoardHeader", "On the board");
        var offers = Stack("OfferPanel", content);
        var offerList = offers.gameObject.AddComponent<ContractOfferListView>();
        Wire(offerList, ("_works", controllers.Works),
             ("_cardPrefab", AssetDatabase.LoadAssetAtPath<ContractOfferCardView>(OfferCardPrefab)), ("_content", offers));
    }

    // The coal hopper, the machines, and the office's furniture.
    private static void CreateWorks(Transform content, Controllers controllers, ToastView toast)
    {
        CreateCoalHopper(content, controllers);

        // Machines.
        SectionHeader(content, "GeneratorsHeader", "Generators");
        var costs = Text(content, "RunningCosts", "", 14, Era2Theme.Running, TextAlignmentOptions.Left, FontStyles.Italic);
        var workshopOutput = CreateWorkshopRow(content, controllers);
        var generators = Stack("GeneratorPanel", content);
        var generatorList = generators.gameObject.AddComponent<Era2GeneratorListView>();
        Wire(generatorList, ("_generators", controllers.Generators), ("_works", controllers.Works),
             ("_rowPrefab", AssetDatabase.LoadAssetAtPath<Era2GeneratorRowView>(GeneratorRowPrefab)),
             ("_content", generators), ("_workshopOutputLabel", workshopOutput), ("_runningCostsLabel", costs));

        // The weather glass on the wall (a hint at Era 3's forecasting), and the gas-lamps.
        var glass = MakeButton(content, "WeatherGlass", "The weather glass reads Change");
        Recolour(glass, Era2Theme.Panel);
        var glassLabel = glass.GetComponentInChildren<TextMeshProUGUI>();
        glassLabel.color = Era2Theme.TextMuted;
        glassLabel.fontStyle = FontStyles.Italic;
        glassLabel.fontSize = 15;
        var glassView = glass.gameObject.AddComponent<Era2WeatherGlassView>();
        Wire(glassView, ("_button", glass), ("_toast", toast));

        Text(content, "Footer", "~ gas-lamps hiss along the wall ~", 14, Era2Theme.TextMuted * new Color(1, 1, 1, 0.7f),
             TextAlignmentOptions.Center, FontStyles.Italic);
    }

    // The tree scrolls; the detail card for the selected node stays put beneath it, so the
    // buy button is always in the same place.
    private static UpgradeTreeView CreateImprovements(RectTransform page)
    {
        var content = Scroll(page);
        var area = Rect("Tree", content);
        area.gameObject.AddComponent<LayoutElement>();

        var heading = Text(area, "BranchHeading", "", 12, Era2Theme.TextMuted, TextAlignmentOptions.Center);
        heading.characterSpacing = 6;
        heading.fontStyle = FontStyles.UpperCase;
        heading.textWrappingMode = TextWrappingModes.NoWrap;
        heading.overflowMode = TextOverflowModes.Ellipsis;

        var detail = BuildUpgradeCard();
        detail.name = "UpgradeDetail";
        detail.transform.SetParent(page, false);
        var detailImage = detail.GetComponent<Image>();
        detailImage.color = Era2Theme.PanelRaised;
        // The detail card's minimum covers a two-line description without the page jumping.
        detail.GetComponent<LayoutElement>().minHeight = 92;

        var tree = area.gameObject.AddComponent<UpgradeTreeView>();
        Wire(tree, ("_nodePrefab", AssetDatabase.LoadAssetAtPath<UpgradeNodeView>(UpgradeNodePrefab)),
                   ("_area", area), ("_headingTemplate", heading), ("_detail", detail.GetComponent<UpgradeCardView>()));
        SetColours(tree, ("_line", Era2Theme.StarsEmpty), ("_ownedLine", Era2Theme.Brass));
        var so = new SerializedObject(tree);
        so.FindProperty("_era").intValue = IdlePowerGen.Data.Era2Data.EraNumber;
        so.ApplyModifiedPropertiesWithoutUndo();
        return tree;
    }

    private static RectTransform Scroll(Transform parent)
    {
        var scrollRt = Rect("Lists", parent);
        var le = scrollRt.gameObject.AddComponent<LayoutElement>();
        le.flexibleHeight = 1;
        le.minHeight = 200;

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

    private static TextMeshProUGUI SectionHeader(Transform parent, string name, string text)
    {
        var t = Text(parent, name, text, 15, Era2Theme.TextMuted, TextAlignmentOptions.Left);
        t.characterSpacing = 8;
        t.margin = new Vector4(0, 8, 0, 0);
        return t;
    }

    private static void CreateCoalHopper(Transform parent, Controllers controllers)
    {
        var holder = Stack("CoalHopper", parent, spacing: 0);

        var panel = Rect("HopperPanel", holder);
        var image = Panel(panel);
        var hlg = panel.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(14, 14, 10, 10);
        hlg.spacing = 10;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = hlg.childControlHeight = true;
        hlg.childForceExpandWidth = hlg.childForceExpandHeight = false;
        panel.gameObject.AddComponent<LayoutElement>().minHeight = 72;

        var texts = Stack("Texts", panel, spacing: 3);
        texts.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
        var title = Text(texts, "Title", "Coal hopper", 18, Era2Theme.Text, TextAlignmentOptions.Left);
        var detail = Text(texts, "Detail", "", 13, Era2Theme.TextMuted, TextAlignmentOptions.Left, FontStyles.Italic);
        var bar = Rect("Level", texts);
        Fixed(bar, 6);
        AddImage(bar, Era1SpriteFactory.White, Era2Theme.Track).raycastTarget = false;
        var fillRt = Rect("Fill", bar);
        Stretch(fillRt);
        var fill = AddImage(fillRt, Era1SpriteFactory.White, Era2Theme.Healthy);
        fill.raycastTarget = false;
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;

        // Its own button, 44 tall: the one-tap remedy.
        var orderRt = Rect("Order", panel);
        var orderLe = orderRt.gameObject.AddComponent<LayoutElement>();
        orderLe.preferredWidth = 108;
        orderLe.preferredHeight = orderLe.minHeight = 44;
        var orderImage = AddImage(orderRt, Era1Theme.PanelSprite, Era2Theme.AlarmPanel);
        orderImage.type = Image.Type.Sliced;
        var order = orderRt.gameObject.AddComponent<Button>();
        order.targetGraphic = orderImage;
        orderRt.gameObject.AddComponent<PressFeedback>();
        var orderGroup = orderRt.gameObject.AddComponent<CanvasGroup>();
        var orderLabel = Text(orderRt, "Label", "order 0 d", 16, Era2Theme.Text, TextAlignmentOptions.Center, numerals: true);
        Stretch(orderLabel.rectTransform);

        var view = holder.gameObject.AddComponent<Era2CoalHopperView>();
        Wire(view, ("_works", controllers.Works), ("_panel", panel.gameObject), ("_background", image),
                   ("_titleLabel", title), ("_detailLabel", detail), ("_fill", fill),
                   ("_orderButton", order), ("_orderLabel", orderLabel), ("_orderGroup", orderGroup));
        var so = new SerializedObject(view);
        so.FindProperty("_normalPanel").colorValue = Era2Theme.Panel;
        so.FindProperty("_alarmPanel").colorValue  = Era2Theme.AlarmPanel;
        so.FindProperty("_healthy").colorValue = Era2Theme.Healthy;
        so.FindProperty("_caution").colorValue = Era2Theme.Caution;
        so.FindProperty("_alarm").colorValue   = Era2Theme.Alarm;
        so.ApplyModifiedPropertiesWithoutUndo();
        panel.gameObject.SetActive(false);
    }

    // The Era 1 workshop, carried over as one steady kilowatt: a row to read, not to tap.
    private static TextMeshProUGUI CreateWorkshopRow(Transform parent, Controllers controllers)
    {
        var row = Card("Workshop", out var button, out _);
        row.SetParent(parent, false);
        Recolour(button, Era2Theme.Panel);
        button.interactable = false;

        var texts = Stack("Texts", row, spacing: 2);
        texts.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
        Text(texts, "Name", "The old workshop", 18, Era2Theme.Text, TextAlignmentOptions.Left);
        Text(texts, "Running", "crank, flywheel and water wheel, kept turning by the old hands", 13, Era2Theme.TextMuted,
             TextAlignmentOptions.Left, FontStyles.Italic);
        var output = Text(row, "Output", "0 W", 18, Era2Theme.Text, TextAlignmentOptions.Right, numerals: true);
        output.gameObject.AddComponent<LayoutElement>().preferredWidth = 84;
        return output;
    }

    // ---- Prefabs ----

    private static GameObject BuildEngagementCard()
    {
        var root = new GameObject("Era2EngagementCard", typeof(RectTransform)).GetComponent<RectTransform>();
        Panel(root);
        var vlg = root.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(14, 14, 12, 12);
        vlg.spacing = 4;
        vlg.childControlWidth = vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        var title = Text(root, "Title", "", 20, Era2Theme.Text, TextAlignmentOptions.Left);
        var body = Text(root, "Body", "", 14, Era2Theme.TextMuted, TextAlignmentOptions.Left, FontStyles.Italic);

        // The inset box of the mockup: times and need, the bar, then time left and state.
        var box = Stack("Box", root, spacing: 4);
        var boxVlg = box.GetComponent<VerticalLayoutGroup>();
        boxVlg.padding = new RectOffset(12, 12, 8, 8);
        var boxImage = AddImage(box, Era1Theme.PanelSprite, Era2Theme.PanelRaised);
        boxImage.type = Image.Type.Sliced;
        boxImage.raycastTarget = false;

        var top = Row(box, "Top");
        var window = Text(top, "Window", "", 15, Era2Theme.TextMuted, TextAlignmentOptions.Left, numerals: true);
        window.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
        var need = Text(top, "Need", "", 15, Era2Theme.Text, TextAlignmentOptions.Right, numerals: true);

        var bar = Rect("Progress", box);
        Fixed(bar, 8);
        AddImage(bar, Era1SpriteFactory.White, Era2Theme.Track).raycastTarget = false;
        var fillRt = Rect("Fill", bar);
        Stretch(fillRt);
        var fill = AddImage(fillRt, Era1SpriteFactory.White, Era2Theme.Healthy);
        fill.raycastTarget = false;
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;

        var bottom = Row(box, "Bottom");
        var time = Text(bottom, "Time", "", 13, Era2Theme.TextMuted, TextAlignmentOptions.Left, FontStyles.Italic);
        time.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
        var state = Text(bottom, "State", "", 14, Era2Theme.Text, TextAlignmentOptions.Right);

        var pay = Text(root, "Pay", "", 15, Era2Theme.Purse, TextAlignmentOptions.Left, FontStyles.Italic);

        var view = root.gameObject.AddComponent<ContractEngagementCardView>();
        Wire(view, ("_titleLabel", title), ("_bodyLabel", body), ("_windowLabel", window), ("_needLabel", need),
                   ("_progressFill", fill), ("_timeLabel", time), ("_stateLabel", state), ("_payLabel", pay));
        SetColours(view, ("_healthy", Era2Theme.Healthy), ("_caution", Era2Theme.Caution), ("_alarm", Era2Theme.Alarm));
        return root.gameObject;
    }

    private static GameObject BuildOfferCard()
    {
        var root = Card("Era2OfferCard", out var button, out var group);
        Recolour(button, Era2Theme.Panel);
        Object.DestroyImmediate(root.GetComponent<HorizontalLayoutGroup>());
        var vlg = root.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(14, 14, 10, 10);
        vlg.spacing = 3;
        vlg.childControlWidth = vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        var top = Row(root, "Top");
        var title = Text(top, "Title", "", 18, Era2Theme.Text, TextAlignmentOptions.Left);
        title.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
        // Reputation-tier clients carry a gold star (todo Era 2 §5.1).
        var badge = Rect("Badge", top);
        var badgeLe = badge.gameObject.AddComponent<LayoutElement>();
        badgeLe.preferredWidth = badgeLe.preferredHeight = 18;
        AddImage(badge, Era1SpriteFactory.Star, Era2Theme.Stars).raycastTarget = false;

        var body = Text(root, "Body", "", 13, Era2Theme.TextMuted, TextAlignmentOptions.Left, FontStyles.Italic);
        var detail = Text(root, "Detail", "", 14, Era2Theme.Purse, TextAlignmentOptions.Left);
        var status = Text(root, "Status", "", 15, Era2Theme.Text, TextAlignmentOptions.Left);

        var view = root.gameObject.AddComponent<ContractOfferCardView>();
        Wire(view, ("_titleLabel", title), ("_bodyLabel", body), ("_detailLabel", detail), ("_statusLabel", status),
                   ("_badge", badge.gameObject), ("_button", button), ("_group", group));
        SetColours(view, ("_statusColour", Era2Theme.Text), ("_cautionColour", Era2Theme.Caution));
        return root.gameObject;
    }

    private static GameObject BuildGeneratorRow()
    {
        var root = Card("Era2GeneratorRow", out var button, out var group);
        Recolour(button, Era2Theme.Panel);

        var texts = Stack("Texts", root, spacing: 2);
        texts.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
        var name = Text(texts, "Name", "Machine", 18, Era2Theme.Text, TextAlignmentOptions.Left);
        var running = Text(texts, "Running", "", 13, Era2Theme.Running, TextAlignmentOptions.Left, FontStyles.Italic);
        var output = Text(texts, "Output", "0 W", 15, Era2Theme.TextMuted, TextAlignmentOptions.Left, numerals: true);

        var cost = Text(root, "Cost", "Build\n0 d", 16, Era2Theme.Purse, TextAlignmentOptions.Right, numerals: true);
        cost.gameObject.AddComponent<LayoutElement>().preferredWidth = 84;

        var view = root.gameObject.AddComponent<Era2GeneratorRowView>();
        Wire(view, ("_nameLabel", name), ("_runningLabel", running), ("_outputLabel", output), ("_costLabel", cost),
                   ("_button", button), ("_group", group));
        SetColours(view, ("_runningColour", Era2Theme.Running), ("_alarmColour", Era2Theme.Alarm));
        return root.gameObject;
    }

    // A plaque in the upgrade tree: name over price, the whole plaque the button.
    private static GameObject BuildUpgradeNode()
    {
        var root = new GameObject("Era2UpgradeNode", typeof(RectTransform)).GetComponent<RectTransform>();
        var image = AddImage(root, Era1Theme.PanelSprite, Era2Theme.Panel);
        image.type = Image.Type.Sliced;
        var button = root.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        var colors = button.colors;
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
        button.colors = colors;
        root.gameObject.AddComponent<PressFeedback>();
        var group = root.gameObject.AddComponent<CanvasGroup>();
        var ring = root.gameObject.AddComponent<Outline>();
        ring.effectColor = Era2Theme.Purse;
        ring.effectDistance = new Vector2(2, -2);
        ring.enabled = false;

        var vlg = root.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(6, 6, 6, 6);
        vlg.spacing = 2;
        vlg.childAlignment = TextAnchor.MiddleCenter;
        vlg.childControlWidth = vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        var name = Text(root, "Name", "Upgrade", 14, Era2Theme.Text, TextAlignmentOptions.Center);
        var cost = Text(root, "Cost", "0 d", 13, Era2Theme.Purse, TextAlignmentOptions.Center, numerals: true);

        var view = root.gameObject.AddComponent<UpgradeNodeView>();
        Wire(view, ("_nameLabel", name), ("_costLabel", cost), ("_button", button), ("_group", group),
                   ("_background", image), ("_selectedRing", ring));
        SetColours(view, ("_panel", Era2Theme.Panel), ("_ready", Era2Theme.PanelRaised), ("_owned", Era2Theme.OwnedPanel),
                         ("_text", Era2Theme.Text), ("_muted", Era2Theme.TextMuted), ("_price", Era2Theme.Purse),
                         ("_achievement", Era2Theme.Achievement));
        return root.gameObject;
    }

    // The Era 1 upgrade card in Era 2's colours: the tree's detail card.
    private static GameObject BuildUpgradeCard()
    {
        var root = Card("Era2UpgradeCard", out var button, out var group);
        Recolour(button, Era2Theme.Panel);

        var texts = Stack("Texts", root, spacing: 2);
        texts.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
        var name = Text(texts, "Name", "Upgrade", 18, Era2Theme.Text, TextAlignmentOptions.Left);
        var desc = Text(texts, "Description", "", 13, Era2Theme.TextMuted, TextAlignmentOptions.Left, FontStyles.Italic);
        var effect = Text(texts, "Effect", "", 14, Era2Theme.Purse, TextAlignmentOptions.Left);

        var cost = Text(root, "Cost", "0 d", 17, Era2Theme.Purse, TextAlignmentOptions.Right, numerals: true);
        cost.gameObject.AddComponent<LayoutElement>().preferredWidth = 84;

        var view = root.gameObject.AddComponent<UpgradeCardView>();
        Wire(view, ("_nameLabel", name), ("_descriptionLabel", desc), ("_effectLabel", effect),
                   ("_costLabel", cost), ("_button", button), ("_group", group));
        return root.gameObject;
    }

    // ---- Helpers ----

    private static Image Panel(RectTransform rt)
    {
        var image = AddImage(rt, Era1Theme.PanelSprite, Era2Theme.Panel);
        image.type = Image.Type.Sliced;
        return image;
    }

    private static void Recolour(Button button, Color colour)
    {
        var image = (Image)button.targetGraphic;
        image.color = colour;
        var label = button.GetComponentInChildren<TextMeshProUGUI>();
        if (label != null && label.color == Era1Theme.Text) label.color = Era2Theme.Text;
    }

    private static RectTransform Row(Transform parent, string name)
    {
        var rt = Rect(name, parent);
        var hlg = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 8;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = hlg.childControlHeight = true;
        hlg.childForceExpandWidth = hlg.childForceExpandHeight = false;
        return rt;
    }

    private static void SetColours(Object target, params (string field, Color colour)[] colours)
    {
        var so = new SerializedObject(target);
        foreach (var (field, colour) in colours) so.FindProperty(field).colorValue = colour;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
