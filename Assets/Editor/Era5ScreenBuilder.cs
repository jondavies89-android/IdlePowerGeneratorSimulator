using IdlePowerGen.Data;
using IdlePowerGen.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static Era1SceneBuilder;
using static ScreenKit;
using static UnitGridScreenKit;

// Builds the Era 5 screen, after the bible's Appendix A mockup: Central Dispatch's painted-
// metal room — Demand, Freq, PF and V across the top, the regional mimic board, the power-
// factor warning with its capacitor-bank fix, and the fleet grouped by kind.
public static class Era5ScreenBuilder
{
    private const string EngagementCardPrefab = PrefabFolder + "/Era5EngagementCard.prefab";
    private const string OfferCardPrefab      = PrefabFolder + "/Era5OfferCard.prefab";
    private const string UpgradeNodePrefab    = PrefabFolder + "/Era5UpgradeNode.prefab";
    private const string ResearchNodePrefab   = PrefabFolder + "/Era5ResearchNode.prefab";

    private static ScreenTheme T => Era5Theme.Theme;

    public static void BuildPrefabs()
    {
        SaveAsPrefab(EngagementCard(T, "Era5EngagementCard"), EngagementCardPrefab);
        SaveAsPrefab(OfferCard(T, "Era5OfferCard"), OfferCardPrefab);
        SaveAsPrefab(UpgradeNode(T, "Era5UpgradeNode", T.Money), UpgradeNodePrefab);
        SaveAsPrefab(UpgradeNode(T, "Era5ResearchNode", T.Research), ResearchNodePrefab);
    }

    internal static RectTransform Create(Transform canvas, Controllers c)
    {
        var grid = c.Era5;
        var root = Screen(canvas, "Era5Screen", T, out var column);
        var sync = SyncOverlay(T, root, grid, "Era5Sync");

        var header = Row(column, "Header", 8);
        Fixed(header, 50);
        var names = Stack("Names", header, spacing: 0);
        Flexible(names);
        var title = Text(T, names, "Title", "CENTRAL DISPATCH", 17, T.Text);
        title.characterSpacing = 16;
        title.textWrappingMode = TextWrappingModes.NoWrap;
        var date = Text(T, names, "Date", "", 13, T.TextMuted);

        Stats(column, grid);
        Boost(T, column, ScreenTheme.Hex("#C8DCC0"));

        var pagesRt = Pages(column);
        var board = Page(pagesRt, "BoardPage");
        var boardContent = Scroll(board);
        Cascade(T, boardContent, grid, sync);
        Mimic(boardContent, grid);
        Alert(boardContent, grid);
        SyncPanel(T, boardContent, grid, sync);
        Board(T, boardContent, grid, sync, grouped: true, "Fleet");
        Dashboard(T, boardContent, root, grid);
        CashRow(T, boardContent, grid, date, "midlands region", GridHeaderView.DateStyle.Dispatch);
        Era3ScreenBuilder.CreateGoal(boardContent, grid, "Carried at once", "Energise the supergrid", T);

        var cities = Page(pagesRt, "CitiesPage");
        var cityContent = Scroll(cities);
        Areas(T, cityContent, grid, "Cities");
        Contracts(T, cityContent, grid, EngagementCardPrefab, OfferCardPrefab);

        var works = Page(pagesRt, "WorksPage");
        var worksContent = Scroll(works);
        Fuel(T, worksContent, grid);
        Shop(T, worksContent, grid);

        var research = Page(pagesRt, "ResearchPage");
        Era3ScreenBuilder.CreateResearchSummary(research, Era5Data.EraNumber, T);
        var researchTree = Tree(T, research, Era5Data.EraNumber, true, AssetDatabase.LoadAssetAtPath<UpgradeNodeView>(ResearchNodePrefab));

        var upgrades = Page(pagesRt, "UpgradesPage");
        var upgradeTree = Tree(T, upgrades, Era5Data.EraNumber, false, AssetDatabase.LoadAssetAtPath<UpgradeNodeView>(UpgradeNodePrefab));

        var tabs = TabBar(T, column, new[] { "Board", "Cities", "Works", "Research", "Upgrades" },
                          new[] { board, cities, works, research, upgrades });
        Era4ScreenBuilder.Badges(tabs, grid, researchTree, upgradeTree, T.Money);

        BlackStart(root, grid, sync, "Era5BlackStart");
        sync.transform.SetAsLastSibling();
        return root;
    }

    private static void Stats(Transform parent, IdlePowerGen.Controllers.Era5DispatchController grid)
    {
        var row = Rect("Stats", parent);
        Fixed(row, 66);
        var hlg = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 6;
        hlg.childControlWidth = hlg.childControlHeight = true;
        hlg.childForceExpandWidth = hlg.childForceExpandHeight = true;
        TextMeshProUGUI Cell(string caption)
        {
            var cell = Stack(caption, row, spacing: 0);
            cell.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
            Panel(T, cell);
            Caption(T, cell, "Caption", caption, 11, TextAlignmentOptions.Center);
            var v = Text(T, cell, "Value", "", 22, T.Text, TextAlignmentOptions.Center, numerals: true);
            v.textWrappingMode = TextWrappingModes.NoWrap;
            return v;
        }
        var demand = Cell("Demand");
        var freq = Cell("Freq");
        var pf = Cell("PF");
        var v5 = Cell("V");
        var view = row.gameObject.AddComponent<Era5StatsView>();
        Wire(view, ("_grid", grid), ("_demandLabel", demand), ("_frequencyLabel", freq), ("_pfLabel", pf), ("_voltageLabel", v5));
        SetColours(view, ("_text", T.Text), ("_caution", T.Caution), ("_alarm", T.Alarm));
    }

    private static void Mimic(Transform content, IdlePowerGen.Controllers.Era5DispatchController grid)
    {
        var box = Box(T, content, "MimicBoard", 12, 6);
        Caption(T, box, "Caption", "Regional mimic board", 13);
        var area = Rect("Board", box);
        Fixed(area, 236);
        AddImage(area, Era1SpriteFactory.White, Era5Theme.BoardGround).raycastTarget = false;

        var central = Rect("Central", area);
        central.sizeDelta = new Vector2(40, 40);
        AddImage(central, GridSpriteFactory.Square, T.Healthy).raycastTarget = false;
        var cl = Text(T, central, "Label", "CENTRAL", 11, T.Text, TextAlignmentOptions.Center);
        cl.rectTransform.anchorMin = cl.rectTransform.anchorMax = new Vector2(0.5f, 0);
        cl.rectTransform.pivot = new Vector2(0.5f, 1);
        cl.rectTransform.sizeDelta = new Vector2(80, 16);
        cl.rectTransform.anchoredPosition = new Vector2(0, -2);

        var line = Rect("LineTemplate", area);
        AddImage(line, Era1SpriteFactory.White, T.Healthy).raycastTarget = false;

        var node = Rect("CityTemplate", area);
        var nodeImg = AddImage(node, GridSpriteFactory.Ring, T.Healthy);
        var face = Rect("Face", node);
        Stretch(face);
        face.offsetMin = new Vector2(5, 5); face.offsetMax = new Vector2(-5, -5);
        AddImage(face, Era1SpriteFactory.Disc, Era5Theme.BoardGround).raycastTarget = false;
        var b = node.gameObject.AddComponent<Button>();
        b.targetGraphic = nodeImg;
        var hit = Rect("Hit", node);
        hit.anchorMin = hit.anchorMax = new Vector2(0.5f, 0.5f);
        hit.sizeDelta = new Vector2(46, 46);
        AddImage(hit, null, new Color(0, 0, 0, 0));
        var initial = Text(T, node, "Initial", "", 13, T.Text, TextAlignmentOptions.Center);
        Stretch(initial.rectTransform);
        var name = Text(T, node, "Name", "", 9, T.TextMuted, TextAlignmentOptions.Center);
        name.textWrappingMode = TextWrappingModes.NoWrap;
        name.rectTransform.anchorMin = name.rectTransform.anchorMax = new Vector2(0.5f, 1);
        name.rectTransform.pivot = new Vector2(0.5f, 0);
        name.rectTransform.sizeDelta = new Vector2(96, 12);
        name.rectTransform.anchoredPosition = new Vector2(0, 1);

        // The city card under the board.
        var cardRt = Card("CityCard", out var cardButton, out var cardGroup);
        cardRt.SetParent(box, false);
        ((Image)cardButton.targetGraphic).sprite = T.PanelSprite;
        ((Image)cardButton.targetGraphic).color = T.PanelRaised;
        Object.DestroyImmediate(cardRt.GetComponent<HorizontalLayoutGroup>());
        var cvlg = cardRt.gameObject.AddComponent<VerticalLayoutGroup>();
        cvlg.padding = new RectOffset(12, 12, 8, 8);
        cvlg.spacing = 2;
        cvlg.childControlWidth = cvlg.childControlHeight = true;
        cvlg.childForceExpandWidth = true;
        cvlg.childForceExpandHeight = false;
        var cName = Text(T, cardRt, "Name", "", 16, T.Text);
        var cDetail = Text(T, cardRt, "Detail", "", 12, T.TextMuted);
        var cAction = Text(T, cardRt, "Action", "", 14, T.Money);
        var card = cardRt.gameObject.AddComponent<CityCardView>();
        Wire(card, ("_grid", grid), ("_nameLabel", cName), ("_detailLabel", cDetail), ("_actionLabel", cAction),
                   ("_button", cardButton), ("_group", cardGroup));
        SetColours(card, ("_text", T.Text), ("_muted", T.TextMuted), ("_caution", T.Caution), ("_alarm", T.Alarm));

        var view = area.gameObject.AddComponent<Era5MimicBoardView>();
        Wire(view, ("_grid", grid), ("_area", area), ("_central", central), ("_nodeTemplate", nodeImg),
                   ("_lineTemplate", line.GetComponent<Image>()), ("_card", card));
        SetColours(view, ("_healthy", T.Healthy), ("_caution", T.Caution), ("_alarm", T.Alarm),
                         ("_unlit", Era5Theme.Chip), ("_selected", T.Text));
    }

    private static void Alert(Transform content, IdlePowerGen.Controllers.Era5DispatchController grid)
    {
        var holder = Stack("Alert", content, spacing: 0);
        var alert = Card("AlertCard", out var button, out _);
        alert.SetParent(holder, false);
        ((Image)button.targetGraphic).sprite = T.PanelSprite;
        ((Image)button.targetGraphic).color = T.AlarmPanel;
        var texts = Stack("Texts", alert, spacing: 2);
        Flexible(texts);
        var title = Text(T, texts, "Title", "", 16, T.Caution);
        var detail = Text(T, texts, "Detail", "", 13, T.Caution);
        var view = holder.gameObject.AddComponent<Era5AlertView>();
        Wire(view, ("_grid", grid), ("_panel", alert.gameObject), ("_titleLabel", title), ("_detailLabel", detail), ("_button", button));
        alert.gameObject.SetActive(false);
    }

    public static void CreateTitleCard(Transform canvas) =>
        TitleCard(T, canvas, Era5Data.EraNumber, "End of Era 4  ·  The National Grid is switched on",
                  "The Interconnected Grid", "12th November 1952",
                  "The grid is national now, and you run the Midlands from Central Dispatch. The cities' motors " +
                  "and mills draw more than real power: watch each city's power factor and voltage, and put " +
                  "capacitor banks where they sag. The relays will shed load to save the grid if they must.",
                  "Into Central Dispatch");

    public static void CreateEndCard(Transform canvas)
    {
        TitleCard(T, canvas, 6, "End of Era 5  ·  The supergrid is energised",
                  "The Nuclear Age", "Coming soon",
                  "Fifty gigawatts on one grid, and the Midlands never dark. Era 6, The Nuclear Age, is still being " +
                  "built. Your grid runs on: keep connecting, keep it steady.",
                  "Keep running the grid");
        var card = GameObject.Find("Era6TitleCard").GetComponent<EraTitleCardView>();
        SetBool(card, "_contentEnd", true);
    }
}
