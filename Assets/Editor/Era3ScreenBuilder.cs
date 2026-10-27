using IdlePowerGen.Data;
using IdlePowerGen.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static Era1SceneBuilder;
using static ScreenKit;

// Builds the Era 3 screen into the scene. Layout follows the bible's Appendix A mockup:
// the works' name and the cash, the town map with its lit and faint districts, the
// distribution panel with the loss share, research in progress, and an alert with its fix.
public static class Era3ScreenBuilder
{
    private const string EngagementCardPrefab = PrefabFolder + "/Era3EngagementCard.prefab";
    private const string OfferCardPrefab      = PrefabFolder + "/Era3OfferCard.prefab";
    private const string GeneratorRowPrefab   = PrefabFolder + "/Era3GeneratorRow.prefab";
    private const string UpgradeNodePrefab    = PrefabFolder + "/Era3UpgradeNode.prefab";
    private const string ResearchNodePrefab   = PrefabFolder + "/Era3ResearchNode.prefab";

    private static ScreenTheme T => Era3Theme.Theme;

    public static void BuildPrefabs()
    {
        Era1AssetBuilder.EnsureFolder(PrefabFolder);
        SaveAsPrefab(EngagementCard(T, "Era3EngagementCard"), EngagementCardPrefab);
        SaveAsPrefab(OfferCard(T, "Era3OfferCard"), OfferCardPrefab);
        SaveAsPrefab(GeneratorRow(T, "Era3GeneratorRow"), GeneratorRowPrefab);
        SaveAsPrefab(UpgradeNode(T, "Era3UpgradeNode", T.Money), UpgradeNodePrefab);
        SaveAsPrefab(UpgradeNode(T, "Era3ResearchNode", T.Research), ResearchNodePrefab);
    }

    internal static RectTransform Create(Transform canvas, Controllers c)
    {
        var root = Screen(canvas, "Era3Screen", T, out var column);
        var town = c.Era3;

        CreateHeader(column, town);
        DayBar(T, column, Era3Theme.LitLine * new Color(1, 1, 1, 0.35f));
        Boost(T, column, ScreenTheme.Hex("#1B2414"));

        var pagesRt = Pages(column);
        var townPage = Page(pagesRt, "TownPage");
        CreateTown(Scroll(townPage), town);

        var clients = Page(pagesRt, "ClientsPage");
        Contracts(T, Scroll(clients), town, EngagementCardPrefab, OfferCardPrefab);

        var works = Page(pagesRt, "WorksPage");
        CreateWorks(Scroll(works), c);

        var research = Page(pagesRt, "ResearchPage");
        CreateResearchSummary(research, Era3Data.EraNumber);
        CreateGalvanometer(research, town);
        var researchTree = Tree(T, research, Era3Data.EraNumber, true,
                                AssetDatabase.LoadAssetAtPath<UpgradeNodeView>(ResearchNodePrefab));

        var upgrades = Page(pagesRt, "UpgradesPage");
        var upgradeTree = Tree(T, upgrades, Era3Data.EraNumber, false,
                               AssetDatabase.LoadAssetAtPath<UpgradeNodeView>(UpgradeNodePrefab));

        Capstone(T, column, root, town, "Era 3 · The War of Currents", "Ten megawatts at once",
                 "Westgate is lit from end to end, and alternating current has won the war. The town will " +
                 "celebrate at its own Electrical Exhibition, and its stations will become one Central Electric Station.",
                 "Begin Era 4 · The Grid Awakens");

        var tabs = TabBar(T, column, new[] { "Town", "Clients", "Works", "Research", "Upgrades" },
                          new[] { townPage, clients, works, research, upgrades });
        var badges = tabs.gameObject.AddComponent<GridTabBadgesView>();
        Wire(badges, ("_tabs", tabs), ("_works", town), ("_town", town));
        var so = new SerializedObject(badges);
        SetArray(so.FindProperty("_trees"), new Object[] { researchTree, upgradeTree });
        var tabsProp = so.FindProperty("_treeTabs");
        tabsProp.arraySize = 2;
        tabsProp.GetArrayElementAtIndex(0).intValue = 3;
        tabsProp.GetArrayElementAtIndex(1).intValue = 4;
        so.FindProperty("_opportunity").colorValue = T.Money;
        so.ApplyModifiedPropertiesWithoutUndo();
        return root;
    }

    private static void CreateHeader(Transform parent, IdlePowerGen.Controllers.Era3TownController town)
    {
        var header = Stack("Header", parent, spacing: 1);
        Fixed(header, 36);
        header.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.MiddleLeft;
        var title = Text(T, header, "Title", "EDISON ELECTRIC WORKS", 14, T.Money);
        title.characterSpacing = 2;
        OneLine(title, 10);
        var date = Text(T, header, "Date", "", 12, T.TextMuted, style: FontStyles.Italic);
        OneLine(date, 9);

        // The cash, the research points and the repute, above the tabs so they show on every page.
        UnitGridScreenKit.CashRow(T, parent, town, date, "Pearl Street", GridHeaderView.DateStyle.Period);
    }

    private static void CreateTown(Transform content, IdlePowerGen.Controllers.Era3TownController town)
    {
        // The map.
        var mapBox = Box(T, content, "TownMap", 12, 6);
        var top = Row(mapBox, "Top");
        Flexible(Caption(T, top, "Caption", "Town map · Westgate"));
        var count = Text(T, top, "Count", "", 14, T.Money, TextAlignmentOptions.Right);

        var area = Rect("Map", mapBox);
        Fixed(area, 190);
        AddImage(area, Era1SpriteFactory.White, Era3Theme.MapGround).raycastTarget = false;

        // A brass-rimmed square: the rim is the square, the face a smaller one inside it.
        var station = Rect("Station", area);
        station.sizeDelta = new Vector2(34, 34);
        AddImage(station, GridSpriteFactory.Square, T.Money * new Color(1, 1, 1, 0.8f)).raycastTarget = false;
        var stationFace = Rect("Face", station);
        Stretch(stationFace);
        stationFace.offsetMin = new Vector2(2, 2);
        stationFace.offsetMax = new Vector2(-2, -2);
        AddImage(stationFace, GridSpriteFactory.Square, T.PanelRaised).raycastTarget = false;
        var stationLabel = Text(T, station, "Label", "station", 11, T.TextMuted, TextAlignmentOptions.Center);
        stationLabel.rectTransform.anchorMin = new Vector2(0.5f, 0);
        stationLabel.rectTransform.anchorMax = new Vector2(0.5f, 0);
        stationLabel.rectTransform.pivot = new Vector2(0.5f, 1);
        stationLabel.rectTransform.sizeDelta = new Vector2(60, 16);
        stationLabel.rectTransform.anchoredPosition = new Vector2(0, -2);

        var lineTemplate = Rect("StreetTemplate", area);
        AddImage(lineTemplate, Era1SpriteFactory.White, Era3Theme.DarkLine).raycastTarget = false;

        // A 44dp tap target around a smaller disc.
        var nodeTemplate = Rect("DistrictTemplate", area);
        var nodeImage = AddImage(nodeTemplate, Era1SpriteFactory.Disc, Era3Theme.Unlit);
        var nodeButton = nodeTemplate.gameObject.AddComponent<Button>();
        nodeButton.targetGraphic = nodeImage;
        var hit = Rect("Hit", nodeTemplate);
        hit.anchorMin = hit.anchorMax = new Vector2(0.5f, 0.5f);
        hit.sizeDelta = new Vector2(44, 44);
        AddImage(hit, null, new Color(0, 0, 0, 0));

        var hint = Text(T, mapBox, "Hint", "faint districts are unserved customers · tap one", 12,
                        T.TextMuted * new Color(1, 1, 1, 0.8f), TextAlignmentOptions.Center, FontStyles.Italic);

        // The district card under the map.
        var cardRt = Card("DistrictCard", out var cardButton, out var cardGroup);
        cardRt.SetParent(mapBox, false);
        ((Image)cardButton.targetGraphic).sprite = T.PanelSprite;
        ((Image)cardButton.targetGraphic).color = T.PanelRaised;
        Object.DestroyImmediate(cardRt.GetComponent<HorizontalLayoutGroup>());
        var cvlg = cardRt.gameObject.AddComponent<VerticalLayoutGroup>();
        cvlg.padding = new RectOffset(12, 12, 8, 8);
        cvlg.spacing = 2;
        cvlg.childControlWidth = cvlg.childControlHeight = true;
        cvlg.childForceExpandWidth = true;
        cvlg.childForceExpandHeight = false;
        var dName = Text(T, cardRt, "Name", "", 18, T.Text);
        var dDetail = Text(T, cardRt, "Detail", "", 13, T.TextMuted, style: FontStyles.Italic);
        var dAction = Text(T, cardRt, "Action", "", 15, T.Money);
        var card = cardRt.gameObject.AddComponent<Era3DistrictCardView>();
        Wire(card, ("_town", town), ("_nameLabel", dName), ("_detailLabel", dDetail), ("_actionLabel", dAction),
                   ("_button", cardButton), ("_group", cardGroup));
        SetColours(card, ("_text", T.Text), ("_money", T.Money), ("_muted", T.TextMuted),
                         ("_caution", T.Caution), ("_alarm", T.Alarm));

        var map = area.gameObject.AddComponent<Era3TownMapView>();
        Wire(map, ("_town", town), ("_area", area), ("_nodeTemplate", nodeImage), ("_lineTemplate", lineTemplate.GetComponent<Image>()),
                  ("_station", station), ("_countLabel", count), ("_card", card),
                  ("_litSprite", Era1SpriteFactory.Disc), ("_unlitSprite", GridSpriteFactory.Ring));
        SetColours(map, ("_lit", T.Money), ("_unlit", Era3Theme.Unlit), ("_available", T.Text),
                        ("_caution", T.Caution), ("_alarm", T.Alarm), ("_litLine", Era3Theme.LitLine),
                        ("_darkLine", Era3Theme.DarkLine), ("_selected", T.Text));

        // The alert, with its fix.
        var alertHolder = Stack("Alert", content, spacing: 0);
        var alert = Card("AlertCard", out var alertButton, out _);
        alert.SetParent(alertHolder, false);
        ((Image)alertButton.targetGraphic).sprite = T.PanelSprite;
        ((Image)alertButton.targetGraphic).color = T.AlarmPanel;
        var alertTexts = Stack("Texts", alert, spacing: 2);
        Flexible(alertTexts);
        var aTitle = Text(T, alertTexts, "Title", "", 17, T.Money);
        var aDetail = Text(T, alertTexts, "Detail", "", 13, T.TextMuted, style: FontStyles.Italic);
        var alertView = alertHolder.gameObject.AddComponent<Era3AlertView>();
        Wire(alertView, ("_town", town), ("_panel", alert.gameObject), ("_titleLabel", aTitle),
                        ("_detailLabel", aDetail), ("_button", alertButton));
        alert.gameObject.SetActive(false);

        CreateBoiler(content, town);

        // Distribution.
        var dist = Box(T, content, "Distribution", 14, 4);
        Caption(T, dist, "Caption", "Distribution");
        // Captions over the numbers, so a phone's width fits "Generated 3.6 MW → 14 % → 3.1 MW".
        var flow = Row(dist, "Flow", 6);
        var genCol = Stack("GeneratedColumn", flow, spacing: 0);
        Text(T, genCol, "GeneratedCaption", "Generated", 14, T.TextMuted);
        var generated = Text(T, genCol, "Generated", "0 W", 20, T.Money, numerals: true);
        var loss = Text(T, flow, "Loss", "→ losses 0% →", 15, T.Alarm, TextAlignmentOptions.Center);
        Flexible(loss);
        var delCol = Stack("DeliveredColumn", flow, spacing: 0);
        Text(T, delCol, "DeliveredCaption", "delivered", 14, T.TextMuted, TextAlignmentOptions.Right);
        var delivered = Text(T, delCol, "Delivered", "0 W", 20, T.Money, TextAlignmentOptions.Right, numerals: true);
        foreach (var label in flow.GetComponentsInChildren<TextMeshProUGUI>()) label.textWrappingMode = TextWrappingModes.NoWrap;
        var distHint = Text(T, dist, "Hint", "", 13, T.TextMuted, style: FontStyles.Italic);
        var distView = dist.gameObject.AddComponent<Era3DistributionView>();
        Wire(distView, ("_town", town), ("_generatedLabel", generated), ("_lossLabel", loss),
                       ("_deliveredLabel", delivered), ("_hintLabel", distHint));
        SetColours(distView, ("_healthy", T.Healthy), ("_caution", T.Caution), ("_alarm", T.Alarm), ("_muted", T.TextMuted));

        CreateGoal(content, town, "Lit at once");
        CreateResearchSummary(content, Era3Data.EraNumber);
        var worksSummary = content.GetComponentInChildren<ResearchSummaryView>(true);
        SetString(worksSummary, "_noRateHint", "take readings on the Research tab, or build an R&D workshop");
        SetString(worksSummary, "_chooseHint", "choose what to save for on the Research tab");
    }

    // The boiler house: a pressure gauge (green band, red where the valve lifts) and the Stoke button.
    private static void CreateBoiler(Transform content, IdlePowerGen.Controllers.Era3TownController town)
    {
        var box = Box(T, content, "Boiler", 14, 6);
        Caption(T, box, "Caption", "Boiler house");
        var gauge = Rect("Gauge", box);
        Fixed(gauge, 22);
        AddImage(gauge, Era1SpriteFactory.White, T.Track).raycastTarget = false;
        Zone(gauge, "Green", Era3Data.GreenLow, Era3Data.GreenHigh, T.Healthy * new Color(1, 1, 1, 0.3f));
        Zone(gauge, "Red", 0.94f, 1f, T.Alarm * new Color(1, 1, 1, 0.35f));
        var fillRt = Rect("Fill", gauge);
        Stretch(fillRt);
        fillRt.offsetMin = new Vector2(0, 6);
        fillRt.offsetMax = new Vector2(0, -6);
        var fill = AddImage(fillRt, Era1SpriteFactory.White, T.Money);
        fill.raycastTarget = false;
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillAmount = 0;
        var status = Text(T, box, "Status", "", 13, T.TextMuted, style: FontStyles.Italic);
        var stoke = Button(T, box, "Stoke", "Stoke the boilers", 56, T.PanelRaised, 18);
        var stokeLabel = stoke.GetComponentInChildren<TextMeshProUGUI>();
        stokeLabel.color = T.Money;
        var earned = Text(T, box, "Earned", "", 12, T.TextMuted);

        var view = box.gameObject.AddComponent<Era3BoilerView>();
        Wire(view, ("_town", town), ("_fill", fill), ("_statusLabel", status), ("_earnedLabel", earned),
                   ("_stoke", stoke), ("_stokeLabel", stokeLabel));
        SetColours(view, ("_healthy", T.Healthy), ("_caution", T.Caution), ("_alarm", T.Alarm),
                         ("_muted", T.TextMuted), ("_cold", T.TextMuted));
    }

    // The bench galvanometer: a scale with the band to catch the needle in, and the button.
    private static void CreateGalvanometer(Transform page, IdlePowerGen.Controllers.Era3TownController town)
    {
        var box = Box(T, page, "Galvanometer", 14, 6);
        Caption(T, box, "Caption", "Bench galvanometer · take readings");
        var scale = Rect("Scale", box);
        Fixed(scale, 40);
        AddImage(scale, Era1SpriteFactory.White, Era3Theme.MapGround).raycastTarget = false;
        var band = Zone(scale, "Band", 0.4f, 0.6f, T.Healthy);
        var mid = Zone(scale, "Zero", 0.499f, 0.501f, T.Edge);
        mid.GetComponent<Image>().raycastTarget = false;
        var needle = Rect("Needle", scale);
        needle.anchorMin = new Vector2(0.5f, 0);
        needle.anchorMax = new Vector2(0.5f, 1);
        needle.pivot = new Vector2(0.5f, 0.5f);
        needle.sizeDelta = new Vector2(4, -6);
        AddImage(needle, Era1SpriteFactory.White, T.Money).raycastTarget = false;
        var status = Text(T, box, "Status", "", 13, T.TextMuted, style: FontStyles.Italic);
        var read = Button(T, box, "TakeReading", "Take a reading", 50, T.PanelRaised, 17);
        read.GetComponentInChildren<TextMeshProUGUI>().color = T.Research;
        var earned = Text(T, box, "Earned", "", 12, T.TextMuted);

        var summary = page.GetComponentInChildren<ResearchSummaryView>(true);
        if (summary != null) SetString(summary, "_noRateHint", "take readings below, or build an R&D workshop");

        var view = box.gameObject.AddComponent<Era3GalvanometerView>();
        Wire(view, ("_town", town), ("_needle", needle), ("_band", band), ("_bandImage", band.GetComponent<Image>()),
                   ("_statusLabel", status), ("_earnedLabel", earned), ("_read", read));
        SetColours(view, ("_healthy", T.Healthy * new Color(1, 1, 1, 0.45f)), ("_caution", T.Caution),
                         ("_muted", T.TextMuted), ("_money", T.Money * new Color(1, 1, 1, 0.75f)));
    }

    // A band across a bar, from one fraction of its width to another.
    private static RectTransform Zone(RectTransform bar, string name, float from, float to, Color colour)
    {
        var zone = Rect(name, bar);
        zone.anchorMin = new Vector2(from, 0);
        zone.anchorMax = new Vector2(to, 1);
        zone.offsetMin = zone.offsetMax = Vector2.zero;
        AddImage(zone, Era1SpriteFactory.White, colour).raycastTarget = false;
        return zone;
    }

    internal static void CreateGoal(Transform content, IdlePowerGen.Controllers.GridWorksController works,
                                    string goalText, ScreenTheme theme = null)
    {
        var T = theme ?? Era3Theme.Theme;
        var goal = Box(T, content, "Goal", 14, 6);
        var label = Text(T, goal, "Label", "", 14, T.Text);
        var fill = Bar(T, goal, "Bar", 8, T.Achievement);
        var view = goal.gameObject.AddComponent<GoalView>();
        Wire(view, ("_works", works), ("_label", label), ("_fill", fill));
        SetString(view, "_goalText", goalText);
    }

    // noRateHint: what to build for research in this era, said while there is none.
    internal static void CreateResearchSummary(Transform content, int era, ScreenTheme theme = null, string noRateHint = null)
    {
        var T = theme ?? Era3Theme.Theme;
        var box = Box(T, content, "ResearchSummary", 14, 4);
        Caption(T, box, "Caption", "Research · saving for");
        var top = Row(box, "Top");
        var name = Text(T, top, "Name", "", 17, T.Text);
        Flexible(name);
        var points = Text(T, top, "Points", "", 15, T.TextMuted, TextAlignmentOptions.Right, numerals: true);
        var fill = Bar(T, box, "Bar", 6, T.Research);
        var eta = Text(T, box, "Eta", "", 13, T.TextMuted, style: FontStyles.Italic);
        var view = box.gameObject.AddComponent<ResearchSummaryView>();
        Wire(view, ("_nameLabel", name), ("_pointsLabel", points), ("_fill", fill), ("_etaLabel", eta));
        SetInt(view, "_era", era);
        if (noRateHint != null) SetString(view, "_noRateHint", noRateHint);
    }

    private static void CreateWorks(Transform content, Controllers c)
    {
        SectionHeader(T, content, "MachinesHeader", "Machines");
        var total = Text(T, content, "Total", "", 14, T.TextMuted, style: FontStyles.Italic);
        var inherited = InheritedRow(T, content, "The old works",
                                     "the Demonstration's engines, steady as ever");
        var rows = Stack("MachinePanel", content);
        var locked = Text(T, content, "Locked", "More machines come with research.", 13, T.TextMuted,
                          style: FontStyles.Italic);
        var list = rows.gameObject.AddComponent<MachineListView>();
        Wire(list, ("_generators", c.Generators),
             ("_rowPrefab", AssetDatabase.LoadAssetAtPath<Era2GeneratorRowView>(GeneratorRowPrefab)),
             ("_content", rows), ("_inheritedLabel", inherited), ("_totalLabel", total), ("_lockedHint", locked.gameObject));
        SetInt(list, "_era", Era3Data.EraNumber);

        Text(T, content, "Footer", "~ arc lamps buzz along Pearl Street ~", 14, T.TextMuted * new Color(1, 1, 1, 0.7f),
             TextAlignmentOptions.Center, FontStyles.Italic);
    }

    public static void CreateTitleCard(Transform canvas) =>
        TitleCard(T, canvas, Era3Data.EraNumber, "End of Era 2  ·  The Crystal Palace was lit",
                  "The War of Currents", "7th April 1893",
                  "Forty years on, the demonstration office is the Edison Electric Works on Pearl Street, " +
                  "and the whole town of Westgate wants lighting. Mr Edison swears by direct current. " +
                  "Mr Westinghouse says it will never reach past the next street.",
                  "Light the town");
}
