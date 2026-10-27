using IdlePowerGen.Data;
using IdlePowerGen.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static Era1SceneBuilder;
using static ScreenKit;
using static UnitGridScreenKit;

// Builds the Era 6 screen, after the bible's Appendix A mockup: the National Grid's 1978
// control room. A charcoal header bar; the FREQ CRT with RoCoF; DEMAND and GENERATION on cream
// tiles; the DISPATCH board in green on black, each unit on BASE, AGC or standby; the ALARMS
// panel in red; the preparedness scores. Machine output on the CRTs, human prose on cream.
public static class Era6ScreenBuilder
{
    private const string EngagementCardPrefab = PrefabFolder + "/Era6EngagementCard.prefab";
    private const string OfferCardPrefab      = PrefabFolder + "/Era6OfferCard.prefab";
    private const string UpgradeNodePrefab    = PrefabFolder + "/Era6UpgradeNode.prefab";
    private const string ResearchNodePrefab   = PrefabFolder + "/Era6ResearchNode.prefab";

    private static ScreenTheme T => Era6Theme.Theme;
    private static ScreenTheme C => Era6Theme.Crt;

    public static void BuildPrefabs()
    {
        SaveAsPrefab(EngagementCard(T, "Era6EngagementCard"), EngagementCardPrefab);
        SaveAsPrefab(OfferCard(T, "Era6OfferCard"), OfferCardPrefab);
        SaveAsPrefab(UpgradeNode(T, "Era6UpgradeNode", T.Money), UpgradeNodePrefab);
        SaveAsPrefab(UpgradeNode(T, "Era6ResearchNode", T.Research), ResearchNodePrefab);
    }

    internal static RectTransform Create(Transform canvas, Controllers c)
    {
        var grid = c.Era6;
        var root = Screen(canvas, "Era6Screen", T, out var column);
        var sync = SyncOverlay(T, root, grid, "Era6Sync");

        var date = HeaderBar(column);
        // Above the tabs, so the balances show on every page.
        CashRow(T, column, grid, date, "", GridHeaderView.DateStyle.National);
        Boost(T, column, ScreenTheme.Hex("#C8DCC0"));
        // Above the tabs, so the state of the grid and the cascade warning show on every page.
        var status = Status(T, column, grid, "region", "Regions");
        Cascade(T, column, grid, sync);

        var pagesRt = Pages(column);
        var control = Page(pagesRt, "ControlPage");
        var controlContent = Scroll(control);
        Frequency(controlContent, grid);
        Balance(controlContent, grid);
        var card = Board(C, controlContent, grid, sync, grouped: false, "Dispatch", modes: true);
        Crt(card.transform.parent);
        Alarms(controlContent, grid);
        Dashboard(T, controlContent, root, grid);
        Era3ScreenBuilder.CreateGoal(controlContent, grid, "Carried at once", T);

        var regions = Page(pagesRt, "RegionsPage");
        var regionContent = Scroll(regions);
        Mimic(regionContent, grid);
        Areas(T, regionContent, grid, "Regions");
        Contracts(T, regionContent, grid, EngagementCardPrefab, OfferCardPrefab);

        var works = Page(pagesRt, "WorksPage");
        var worksContent = Scroll(works);
        Fuel(T, worksContent, grid);
        Shop(T, worksContent, grid);

        var research = Page(pagesRt, "ResearchPage");
        Era3ScreenBuilder.CreateResearchSummary(research, Era6Data.EraNumber, T, "build a research institute to make research");
        var researchTree = Tree(T, research, Era6Data.EraNumber, true, AssetDatabase.LoadAssetAtPath<UpgradeNodeView>(ResearchNodePrefab));

        var upgrades = Page(pagesRt, "UpgradesPage");
        var upgradeTree = Tree(T, upgrades, Era6Data.EraNumber, false, AssetDatabase.LoadAssetAtPath<UpgradeNodeView>(UpgradeNodePrefab));

        Capstone(T, column, root, grid, "Era 6 · The Nuclear Age", "A terawatt",
                 "The whole country runs as one machine: reactors steady through the night, gas turbines on the peaks, " +
                 "and the frequency held to a hair. The market is coming.",
                 "Open the market");

        var tabs = TabBar(T, column, new[] { "Control", "Regions", "Works", "Research", "Upgrades" },
                          new[] { control, regions, works, research, upgrades });
        Era4ScreenBuilder.Badges(tabs, grid, researchTree, upgradeTree, T.Money);
        Wire(status, ("_tabs", tabs));

        BlackStart(root, grid, sync, "Era6BlackStart");
        sync.transform.SetAsLastSibling();
        return root;
    }

    // The mockup's charcoal bar: NATIONAL GRID on the left, the clock and date on the right.
    private static TextMeshProUGUI HeaderBar(Transform column)
    {
        var bar = Row(column, "Header", 8);
        Fixed(bar, 34);
        bar.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(12, 12, 4, 4);
        Panel(T, bar, Era6Theme.HeaderBar);
        var title = Text(T, bar, "Title", "NATIONAL GRID", 13, Era6Theme.HeaderText);
        title.characterSpacing = 12;
        title.textWrappingMode = TextWrappingModes.NoWrap;
        title.gameObject.AddComponent<LayoutElement>().preferredWidth = 150;
        var date = Text(T, bar, "Date", "", 13, Era6Theme.HeaderText, TextAlignmentOptions.Right, numerals: true);
        date.characterSpacing = 4;
        OneLine(date, 9);
        Flexible(date);
        return date;
    }

    // A thin phosphor-green rim, as on the mockup's CRT panels.
    private static void Crt(Transform box, Color? rim = null)
    {
        var ring = box.gameObject.AddComponent<Outline>();
        ring.effectColor = rim ?? C.Edge;
        ring.effectDistance = new Vector2(1.5f, -1.5f);
    }

    private static void Frequency(Transform content, IdlePowerGen.Controllers.UnitGridController grid)
    {
        var box = Box(C, content, "Frequency", 12, 2);
        Crt(box);
        var top = Row(box, "Top");
        var caption = Text(C, top, "Caption", "FREQ", 14, C.Text);
        caption.gameObject.AddComponent<LayoutElement>().preferredWidth = 60;
        var value = Text(C, top, "Value", "50.00 Hz", 30, C.Text, TextAlignmentOptions.Center, numerals: true);
        OneLine(value, 16);
        Flexible(value);
        var offset = Text(C, top, "Offset", "", 15, C.Text, TextAlignmentOptions.Right, numerals: true);
        offset.gameObject.AddComponent<LayoutElement>().preferredWidth = 80;
        var bottom = Row(box, "Bottom");
        var nominal = Text(C, bottom, "Nominal", "NOMINAL 50.00 ± 0.20", 12, C.TextMuted);
        nominal.textWrappingMode = TextWrappingModes.NoWrap;
        Flexible(nominal);
        var rocof = Text(C, bottom, "Rocof", "", 12, C.TextMuted, TextAlignmentOptions.Right, numerals: true);
        rocof.textWrappingMode = TextWrappingModes.NoWrap;
        var view = box.gameObject.AddComponent<Era6FrequencyView>();
        Wire(view, ("_grid", grid), ("_valueLabel", value), ("_offsetLabel", offset), ("_rocofLabel", rocof));
        SetColours(view, ("_healthy", C.Healthy), ("_caution", C.Caution), ("_alarm", C.Alarm));
    }

    private static void Balance(Transform content, IdlePowerGen.Controllers.Era6NationalGridController grid)
    {
        var row = Rect("Balance", content);
        var hlg = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 8;
        hlg.childControlWidth = hlg.childControlHeight = true;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = false;
        (TextMeshProUGUI value, TextMeshProUGUI note) Tile(string caption)
        {
            var tile = Box(T, row, caption, 12, 1);
            var le = tile.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = 1;
            le.flexibleWidth = 1;
            Caption(T, tile, "Caption", caption, 12);
            var v = Text(T, tile, "Value", "", 26, T.Text, numerals: true);
            OneLine(v, 14);
            var n = Text(T, tile, "Note", "", 12, T.TextMuted);
            OneLine(n, 8);
            return (v, n);
        }
        var demand = Tile("Demand");
        var generation = Tile("Generation");
        var view = row.gameObject.AddComponent<Era6BalanceView>();
        Wire(view, ("_grid", grid), ("_demandLabel", demand.value), ("_demandNote", demand.note),
                   ("_generationLabel", generation.value), ("_generationNote", generation.note));
        SetColours(view, ("_muted", T.TextMuted), ("_caution", T.Caution), ("_alarm", T.Alarm));
    }

    private static void Alarms(Transform content, IdlePowerGen.Controllers.Era6NationalGridController grid)
    {
        var box = Box(C, content, "Alarms", 12, 2, C.AlarmPanel);
        Crt(box, C.Alarm);
        // The font has no warning sign; the mockup's "⚠" becomes a plain "!".
        var title = Text(C, box, "Title", "!  ALARMS", 13, C.Alarm);
        title.characterSpacing = 12;
        const int rows = 4;
        var left = new Object[rows];
        var right = new Object[rows];
        for (int i = 0; i < rows; i++)
        {
            var r = Row(box, $"Alarm{i}");
            var l = Text(C, r, "Left", "", 14, C.Alarm);
            l.textWrappingMode = TextWrappingModes.NoWrap;
            l.overflowMode = TextOverflowModes.Ellipsis;
            Flexible(l);
            var rr = Text(C, r, "Right", "", 14, C.Alarm, TextAlignmentOptions.Right);
            rr.textWrappingMode = TextWrappingModes.NoWrap;
            left[i] = l;
            right[i] = rr;
        }
        var view = box.gameObject.AddComponent<Era6AlarmsView>();
        Wire(view, ("_grid", grid), ("_background", box.GetComponent<Image>()), ("_title", title));
        var so = new SerializedObject(view);
        SetArray(so.FindProperty("_left"), left);
        SetArray(so.FindProperty("_right"), right);
        so.ApplyModifiedPropertiesWithoutUndo();
        SetColours(view, ("_clearPanel", C.Panel), ("_alarmPanel", C.AlarmPanel), ("_healthy", C.Healthy), ("_alarm", C.Alarm));
    }

    // The national mimic board, a dark painted panel with the regions on it, and the region card.
    private static void Mimic(Transform content, IdlePowerGen.Controllers.Era6NationalGridController grid)
    {
        var box = Box(C, content, "MimicBoard", 12, 6);
        Crt(box);
        Caption(C, box, "Caption", "National mimic board", 13);
        var area = Rect("Board", box);
        Fixed(area, 300);
        AddImage(area, Era1SpriteFactory.White, C.Track).raycastTarget = false;

        var line = Rect("LineTemplate", area);
        AddImage(line, Era1SpriteFactory.White, C.Healthy).raycastTarget = false;

        var node = Rect("RegionTemplate", area);
        var nodeImg = AddImage(node, GridSpriteFactory.Ring, C.Healthy);
        var face = Rect("Face", node);
        Stretch(face);
        face.offsetMin = new Vector2(4, 4); face.offsetMax = new Vector2(-4, -4);
        AddImage(face, Era1SpriteFactory.Disc, C.Track).raycastTarget = false;
        var b = node.gameObject.AddComponent<Button>();
        b.targetGraphic = nodeImg;
        // 44dp to tap, whatever the ring's size.
        var hit = Rect("Hit", node);
        hit.anchorMin = hit.anchorMax = new Vector2(0.5f, 0.5f);
        hit.sizeDelta = new Vector2(46, 46);
        AddImage(hit, null, new Color(0, 0, 0, 0));
        var code = Text(C, node, "Code", "", 10, C.Text, TextAlignmentOptions.Center);
        code.textWrappingMode = TextWrappingModes.NoWrap;
        Stretch(code.rectTransform);

        var cardRt = Card("RegionCard", out var cardButton, out var cardGroup);
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
        var card = cardRt.gameObject.AddComponent<Era6RegionCardView>();
        Wire(card, ("_grid", grid), ("_nameLabel", cName), ("_detailLabel", cDetail), ("_actionLabel", cAction),
                   ("_button", cardButton), ("_group", cardGroup));
        SetColours(card, ("_muted", T.TextMuted), ("_caution", T.Caution), ("_alarm", T.Alarm));

        var view = area.gameObject.AddComponent<Era6MimicBoardView>();
        Wire(view, ("_grid", grid), ("_area", area), ("_nodeTemplate", nodeImg),
                   ("_lineTemplate", line.GetComponent<Image>()), ("_card", card));
        SetColours(view, ("_healthy", C.Healthy), ("_caution", C.Caution), ("_alarm", C.Alarm),
                         ("_unlit", C.TextMuted * new Color(0.6f, 0.6f, 0.6f, 1)), ("_selected", Color.white));
    }

    public static void CreateTitleCard(Transform canvas) =>
        TitleCard(T, canvas, Era6Data.EraNumber, "End of Era 5  ·  The supergrid is energised",
                  "The Nuclear Age", "14th November 1978",
                  "One grid for the whole country, run from the National Control Centre. The new machines are " +
                  "enormous and slow: reactors run flat out on BASE and can't follow the load, coal takes hours to " +
                  "start, and only the gas turbines are quick. Keep enough on the bars for the evening peak, and not " +
                  "so much that the night can't take it.",
                  "Into the control room");

    public static void CreateEndCard(Transform canvas)
    {
        TitleCard(T, canvas, 7, "End of Era 6  ·  A terawatt on one grid",
                  "The Liberalised Market", "Coming soon",
                  "A terawatt, and the frequency held to a hair. Era 7, The Liberalised Market, is still being built. " +
                  "Your grid runs on: keep connecting, keep it steady.",
                  "Keep running the grid");
        var card = GameObject.Find("Era7TitleCard").GetComponent<EraTitleCardView>();
        SetBool(card, "_contentEnd", true);
    }
}
