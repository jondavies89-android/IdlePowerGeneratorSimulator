using IdlePowerGen.Data;
using IdlePowerGen.UI;
using UnityEditor;
using UnityEngine;
using static Era1SceneBuilder;
using static ScreenKit;
using static UnitGridScreenKit;

// Builds the Era 4 screen into the scene, after the bible's Appendix A mockup: the Central
// Electric Station's control desk — the frequency needle and the load, the monospaced board
// of generators on line, the SYNC panel, and cash and research side by side.
public static class Era4ScreenBuilder
{
    private const string EngagementCardPrefab = PrefabFolder + "/Era4EngagementCard.prefab";
    private const string OfferCardPrefab      = PrefabFolder + "/Era4OfferCard.prefab";
    private const string UpgradeNodePrefab    = PrefabFolder + "/Era4UpgradeNode.prefab";
    private const string ResearchNodePrefab   = PrefabFolder + "/Era4ResearchNode.prefab";

    private static ScreenTheme T => Era4Theme.Theme;

    public static void BuildPrefabs()
    {
        SaveAsPrefab(EngagementCard(T, "Era4EngagementCard"), EngagementCardPrefab);
        SaveAsPrefab(OfferCard(T, "Era4OfferCard"), OfferCardPrefab);
        SaveAsPrefab(UpgradeNode(T, "Era4UpgradeNode", T.Money), UpgradeNodePrefab);
        SaveAsPrefab(UpgradeNode(T, "Era4ResearchNode", T.Research), ResearchNodePrefab);
    }

    internal static RectTransform Create(Transform canvas, Controllers c)
    {
        var grid = c.Era4;
        var root = Screen(canvas, "Era4Screen", T, out var column);
        var sync = SyncOverlay(T, root, grid, "Era4Sync");

        var date = Header(T, column, "CENTRAL ELECTRIC STATION");
        // Above the tabs, so the balances show on every page.
        CashRow(T, column, grid, date, "CONTROL DESK", GridHeaderView.DateStyle.ControlDesk);
        Boost(T, column, ScreenTheme.Hex("#1E2A16"));
        // Above the tabs, so the state of the grid and the cascade warning show on every page.
        var status = Status(T, column, grid, "feeder", "Feeders");
        Cascade(T, column, grid, sync);

        var pagesRt = Pages(column);
        var desk = Page(pagesRt, "DeskPage");
        var deskContent = Scroll(desk);
        Gauge(T, deskContent, grid, Era4Theme.GaugeFace, Era4Theme.Needle);
        SyncPanel(T, deskContent, grid, sync);
        Board(T, deskContent, grid, sync, grouped: false, "Generators on line");
        Dashboard(T, deskContent, root, grid);
        Era3ScreenBuilder.CreateGoal(deskContent, grid, "Carried at once", T);

        var feeders = Page(pagesRt, "FeedersPage");
        var feederContent = Scroll(feeders);
        Areas(T, feederContent, grid, "Feeders");
        Contracts(T, feederContent, grid, EngagementCardPrefab, OfferCardPrefab);

        var works = Page(pagesRt, "WorksPage");
        var worksContent = Scroll(works);
        Fuel(T, worksContent, grid);
        Shop(T, worksContent, grid);

        var research = Page(pagesRt, "ResearchPage");
        Era3ScreenBuilder.CreateResearchSummary(research, Era4Data.EraNumber, T, "build an engineering office to make research");
        var researchTree = Tree(T, research, Era4Data.EraNumber, true, AssetDatabase.LoadAssetAtPath<UpgradeNodeView>(ResearchNodePrefab));

        var upgrades = Page(pagesRt, "UpgradesPage");
        var upgradeTree = Tree(T, upgrades, Era4Data.EraNumber, false, AssetDatabase.LoadAssetAtPath<UpgradeNodeView>(UpgradeNodePrefab));

        Capstone(T, column, root, grid, "Era 4 · The Grid Awakens", "A gigawatt on one desk",
                 "Every unit on the board runs in step with the rest. It is time to switch on the National Grid " +
                 "and run a whole region from Central Dispatch.",
                 "Begin Era 5 · The Interconnected Grid");

        var tabs = TabBar(T, column, new[] { "Desk", "Feeders", "Works", "Research", "Upgrades" },
                          new[] { desk, feeders, works, research, upgrades });
        Badges(tabs, grid, researchTree, upgradeTree, T.Money);
        Wire(status, ("_tabs", tabs));

        sync.transform.SetAsLastSibling();
        BlackStart(root, grid, sync, "Era4BlackStart");
        sync.transform.SetAsLastSibling();
        return root;
    }

    internal static void Badges(TabBarView tabs, IdlePowerGen.Controllers.GridWorksController works,
                                UpgradeTreeView research, UpgradeTreeView upgrades, Color colour)
    {
        var badges = tabs.gameObject.AddComponent<GridTabBadgesView>();
        Wire(badges, ("_tabs", tabs), ("_works", works));
        var so = new SerializedObject(badges);
        SetArray(so.FindProperty("_trees"), new Object[] { research, upgrades });
        var tabsProp = so.FindProperty("_treeTabs");
        tabsProp.arraySize = 2;
        tabsProp.GetArrayElementAtIndex(0).intValue = 3;
        tabsProp.GetArrayElementAtIndex(1).intValue = 4;
        so.FindProperty("_clientsTab").intValue = 1;
        so.FindProperty("_opportunity").colorValue = colour;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    public static void CreateTitleCard(Transform canvas) =>
        TitleCard(T, canvas, Era4Data.EraNumber, "End of Era 3  ·  Westgate's Electrical Exhibition",
                  "The Grid Awakens", "14th June 1928",
                  "The town's stations are now one Central Electric Station, and the whole city runs from its " +
                  "control desk. Every unit you add must be brought into step with the rest by hand. Watch the " +
                  "frequency: it is the grid's heartbeat, and it tells you when you are short.",
                  "Take the desk");
}
