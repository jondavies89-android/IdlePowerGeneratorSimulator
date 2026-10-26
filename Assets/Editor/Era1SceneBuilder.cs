using System.Linq;
using IdlePowerGen.Controllers;
using IdlePowerGen.UI;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Builds Assets/Scenes/Era1_Workshop.unity from scratch. The scene is generated rather than
// hand-edited so it can be rebuilt in batch mode and reviewed as code; re-running it
// replaces the scene file, so make lasting changes here, not in the Editor.
public static class Era1SceneBuilder
{
    public const string ScenePath = "Assets/Scenes/Era1_Workshop.unity";

    internal const string PrefabFolder        = "Assets/Prefabs/UI";
    internal const string UpgradeCardPrefab   = PrefabFolder + "/UpgradeCard.prefab";

    // Portrait phone reference (CLAUDE.md: test at 412×915). 1 canvas unit ≈ 1 dp.
    private static readonly Vector2 ReferenceResolution = new(412, 915);

    internal const float Gutter = 16f;

    [MenuItem("IdlePowerGen/Build/Era 1 Scene")]
    public static void Build()
    {
        Era1SpriteFactory.Build();
        GridSpriteFactory.Build();
        Era1FontBuilder.Build();
        Era1AudioFactory.Build();
        BuildPrefabs();
        Era2ScreenBuilder.BuildPrefabs();
        Era3ScreenBuilder.BuildPrefabs();
        Era4ScreenBuilder.BuildPrefabs();
        Era5ScreenBuilder.BuildPrefabs();

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        CreateCamera();
        CreateManagers();
        var controllers = CreateControllers();
        CreateEventSystem();
        CreateUi(controllers);

        EditorSceneManager.SaveScene(scene, ScenePath);
        RegisterInBuildSettings();
        Debug.Log($"[Era1SceneBuilder] Wrote {ScenePath}");
    }

    // =====================================================================
    // Managers & controllers
    // =====================================================================

    private static void CreateCamera()
    {
        var go = new GameObject("Main Camera") { tag = "MainCamera" };
        var cam = go.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Era1Theme.CameraBackground;
        cam.orthographic = true;
        go.AddComponent<AudioListener>();
    }

    // One root object for every manager: DontDestroyOnLoad on any of them keeps them all.
    private static void CreateManagers()
    {
        var go = new GameObject("_Managers");
        WireCatalogues(go.AddComponent<IdlePowerGen.Managers.GameManager>());
        go.AddComponent<IdlePowerGen.Simulation.SimulationTick>();

        var clock = go.AddComponent<IdlePowerGen.Utils.GameClock>();
        var so = new SerializedObject(clock);
        // 1 real second = 1 game minute (todo §3.1).
        so.FindProperty("_gameTimeScale").floatValue = 60f;
        so.ApplyModifiedPropertiesWithoutUndo();

        go.AddComponent<IdlePowerGen.Save.SaveManager>();
        go.AddComponent<IdlePowerGen.Managers.EraManager>();
        go.AddComponent<IdlePowerGen.Managers.UpgradeEffectApplier>();
        go.AddComponent<IdlePowerGen.Managers.Era1MilestoneTracker>();
        go.AddComponent<IdlePowerGen.Managers.EraShortcutLink>();
    }

    // Todo §4.4 — the Inspector drag-and-drop, done from the asset folders.
    private static void WireCatalogues(IdlePowerGen.Managers.GameManager gm)
    {
        var so = new SerializedObject(gm);
        SetArray(so.FindProperty("_eraConfigs"),           Era1AssetBuilder.EraConfigs());
        SetArray(so.FindProperty("_upgradeDefinitions"),
                 Era1AssetBuilder.Upgrades().Concat(Era2AssetBuilder.Upgrades())
                     .Concat(Era3AssetBuilder.Upgrades()).Concat(Era4AssetBuilder.Upgrades()).Concat(Era5AssetBuilder.Upgrades()).ToArray());
        SetArray(so.FindProperty("_generatorDefinitions"),
                 Era1AssetBuilder.Generators().Concat(Era2AssetBuilder.Generators())
                     .Concat(Era3AssetBuilder.Generators()).Concat(Era4AssetBuilder.Generators()).Concat(Era5AssetBuilder.Generators()).ToArray());
        SetArray(so.FindProperty("_contractDefinitions"),
                 Era1AssetBuilder.Contracts().Concat(Era2AssetBuilder.Contracts())
                     .Concat(Era3AssetBuilder.Contracts()).Concat(Era4AssetBuilder.Contracts()).Concat(Era5AssetBuilder.Contracts()).ToArray());
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    internal struct Controllers
    {
        public Era1TapController Tap;
        public Era2GeneratorController Generators;
        public Era1FirstCustomerController FirstCustomer;
        public Era2WorksController Works;
        public Era3TownController Era3;
        public Era4StationController Era4;
        public Era5DispatchController Era5;
    }

    // Scene-scoped (not DontDestroyOnLoad): they belong to the era screens.
    private static Controllers CreateControllers()
    {
        var go = new GameObject("_Controllers");
        var controllers = new Controllers
        {
            Tap           = go.AddComponent<Era1TapController>(),
            Generators    = go.AddComponent<Era2GeneratorController>(),
            FirstCustomer = go.AddComponent<Era1FirstCustomerController>(),
            Works         = go.AddComponent<Era2WorksController>(),
            Era3          = go.AddComponent<Era3TownController>(),
            Era4          = go.AddComponent<Era4StationController>(),
            Era5          = go.AddComponent<Era5DispatchController>(),
        };
        Wire(controllers.Works, ("_generators", controllers.Generators));
        return controllers;
    }

    private static void CreateEventSystem()
    {
        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<StandaloneInputModule>();
    }

    private static void RegisterInBuildSettings()
    {
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
    }

    // =====================================================================
    // UI (todo §5.1 hierarchy)
    // =====================================================================

    private static void CreateUi(Controllers controllers)
    {
        var canvasGo = new GameObject("Canvas", typeof(RectTransform));
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceResolution;
        // Match width: phones differ mostly in height, and the scroll list absorbs it.
        scaler.matchWidthOrHeight = 0f;
        canvasGo.AddComponent<GraphicRaycaster>();

        var bg = Rect("Background", canvasGo.transform);
        Stretch(bg);
        AddImage(bg, null, Era1Theme.ScreenBackground).raycastTarget = false;

        // Candle light pooled around the reading and the crank — the room is otherwise dark.
        var glow = Rect("CandleGlow", bg);
        glow.anchorMin = glow.anchorMax = new Vector2(0.5f, 1f);
        glow.sizeDelta = new Vector2(640, 640);
        glow.anchoredPosition = new Vector2(0, -300);
        AddImage(glow, Era1SpriteFactory.Glow, Era1Theme.Glow).raycastTarget = false;

        var safe = Rect("SafeArea", canvasGo.transform);
        Stretch(safe);
        safe.gameObject.AddComponent<SafeAreaFitter>();

        var column = Rect("Era1Screen", safe);
        Stretch(column);
        var vlg = column.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset((int)Gutter, (int)Gutter, 12, 0);
        vlg.spacing = 4;
        vlg.childControlWidth = vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        CreateHeader(column);
        CreateStoredCharge(column, controllers);
        CreateCrank(column, controllers);
        CreateLists(column, controllers);

        // Each later era's screen draws its own background over the workshop's.
        var toast = CreateToast(canvasGo.transform);
        var era2 = Era2ScreenBuilder.Create(canvasGo.transform, controllers, toast);
        var era3 = Era3ScreenBuilder.Create(canvasGo.transform, controllers);
        var era4 = Era4ScreenBuilder.Create(canvasGo.transform, controllers);
        var era5 = Era5ScreenBuilder.Create(canvasGo.transform, controllers);
        // Toasts and era cards sit above every screen.
        toast.transform.SetAsLastSibling();
        CreateEndOfEra(canvasGo.transform);
        Era3ScreenBuilder.CreateTitleCard(canvasGo.transform);
        Era4ScreenBuilder.CreateTitleCard(canvasGo.transform);
        Era5ScreenBuilder.CreateTitleCard(canvasGo.transform);
        Era5ScreenBuilder.CreateEndCard(canvasGo.transform);

        var switcher = canvasGo.AddComponent<EraScreenSwitcher>();
        var screens = new SerializedObject(switcher);
        SetArray(screens.FindProperty("_screens"), new Object[] { column.gameObject, era2.gameObject, era3.gameObject, era4.gameObject, era5.gameObject });
        screens.ApplyModifiedPropertiesWithoutUndo();

        // Over everything, title cards included, so a tester can always reach it.
        DevToolsBuilder.Create(canvasGo.transform);
    }

    private static void CreateHeader(Transform parent)
    {
        var header = Rect("Header", parent);
        Fixed(header, 58);
        var vlg = header.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.MiddleCenter;
        vlg.childControlWidth = vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        Text(header, "Place", "London  ·  14th March 1821", 14, Era1Theme.TextMuted, TextAlignmentOptions.Center, FontStyles.Italic);
        Text(header, "Title", "The Workshop", 26, Era1Theme.Text, TextAlignmentOptions.Center);
    }

    private static void CreateStoredCharge(Transform parent, Controllers controllers)
    {
        var root = Rect("StoredChargeDisplay", parent);
        Fixed(root, 150);

        var caption = Rect("Caption", root);
        Anchor(caption, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -2), 22);
        var captionRow = caption.gameObject.AddComponent<HorizontalLayoutGroup>();
        captionRow.childAlignment = TextAnchor.MiddleCenter;
        captionRow.spacing = 4;
        captionRow.childControlWidth = captionRow.childControlHeight = true;
        captionRow.childForceExpandWidth = captionRow.childForceExpandHeight = false;
        var bolt = Rect("Bolt", caption);
        AddImage(bolt, Era1SpriteFactory.Bolt, Era1Theme.Accent).raycastTarget = false;
        var boltLe = bolt.gameObject.AddComponent<LayoutElement>();
        boltLe.preferredWidth = boltLe.preferredHeight = 18;
        Text(caption, "Label", "Stored charge", 15, Era1Theme.TextMuted, TextAlignmentOptions.Center);

        var charge = Text(root, "Charge", "0 J", 60, Era1Theme.Accent, TextAlignmentOptions.Center, numerals: true);
        Anchor(charge.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -24), 74);

        var rate = Text(root, "Rate", "", 15, Era1Theme.TextMuted, TextAlignmentOptions.Center);
        Anchor(rate.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -100), 46);

        var view = root.gameObject.AddComponent<Era1StoredChargeView>();
        Wire(view, ("_chargeLabel", charge), ("_rateLabel", rate), ("_tapController", controllers.Tap));
    }

    private static void CreateCrank(Transform parent, Controllers controllers)
    {
        var area = Rect("CrankArea", parent);
        Fixed(area, 236);

        var button = Rect("CrankButton", area);
        // Pivot at the centre: the wheel turns (Era1CrankSpinView) and presses about it.
        button.anchorMin = button.anchorMax = new Vector2(0.5f, 1f);
        button.pivot = new Vector2(0.5f, 0.5f);
        button.anchoredPosition = new Vector2(0, -6 - 98);
        button.sizeDelta = new Vector2(196, 196);
        var image = AddImage(button, Era1SpriteFactory.Crank, Color.white);
        var btn = button.gameObject.AddComponent<Button>();
        btn.targetGraphic = image;
        btn.transition = Selectable.Transition.ColorTint;
        var colors = btn.colors;
        colors.pressedColor = new Color(0.85f, 0.8f, 0.7f);
        btn.colors = colors;
        button.gameObject.AddComponent<PressFeedback>();
        // The wheel is round, so turning the button itself keeps the hit area in place.
        button.gameObject.AddComponent<Era1CrankSpinView>();
        UnityEventTools.AddPersistentListener(btn.onClick, controllers.Tap.OnCrankTapped);
        // Todo §8: the crank's own AudioSource.
        var crankSource = button.gameObject.AddComponent<AudioSource>();
        crankSource.playOnAwake = false;
        crankSource.volume = 0.7f;
        CreateAudio(crankSource);

        var hint = Text(area, "Hint", "Turn the crank", 15, Era1Theme.TextMuted, TextAlignmentOptions.Center, FontStyles.Italic);
        Anchor(hint.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 4), 26, bottom: true);

        // Template label the tap pops are cloned from, centred over the crank.
        var pops = Rect("TapPops", area);
        Stretch(pops);
        var template = Text(pops, "PopTemplate", "+1 J", 22, Era1Theme.Accent, TextAlignmentOptions.Center, numerals: true);
        template.rectTransform.anchorMin = template.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        template.rectTransform.sizeDelta = new Vector2(160, 30);
        template.rectTransform.anchoredPosition = new Vector2(0, -60);
        var popView = pops.gameObject.AddComponent<Era1TapPopView>();
        Wire(popView, ("_template", template));
    }

    // Todo §8 — chimes and stingers on one source, the room's ambience on a quiet loop.
    private static void CreateAudio(AudioSource crankSource)
    {
        var go = new GameObject("_Audio");
        var sfx = go.AddComponent<AudioSource>();
        sfx.playOnAwake = false;
        sfx.volume = 0.6f;

        var ambience = go.AddComponent<AudioSource>();
        ambience.clip = Era1AudioFactory.Ambience;
        ambience.loop = true;
        ambience.playOnAwake = true;
        // Background texture, felt more than heard.
        ambience.volume = 0.15f;

        var presenter = go.AddComponent<Era1AudioPresenter>();
        Wire(presenter, ("_crankSource", crankSource), ("_sfxSource", sfx), ("_ambienceSource", ambience),
                        ("_crankClip", Era1AudioFactory.Crank), ("_upgradeClip", Era1AudioFactory.Chime),
                        ("_milestoneClip", Era1AudioFactory.Stinger));
    }

    private static void CreateLists(Transform parent, Controllers controllers)
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
        // Raycast target so a drag that starts between cards still scrolls.
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

        CreateFirstCustomer(content, controllers);

        Text(content, "ImprovementsHeader", "Improvements", 18, Era1Theme.Text, TextAlignmentOptions.Left);
        var upgradePanel = Stack("UpgradePanel", content);
        var empty = Text(content, "NothingToImprove", "Nothing more to improve for now.", 14, Era1Theme.TextMuted,
                         TextAlignmentOptions.Left, FontStyles.Italic);

        var upgrades = upgradePanel.gameObject.AddComponent<UpgradeListView>();
        Wire(upgrades,
             ("_cardPrefab", AssetDatabase.LoadAssetAtPath<UpgradeCardView>(UpgradeCardPrefab)),
             ("_content", upgradePanel),
             ("_emptyHint", empty.gameObject));
    }

    private static ToastView CreateToast(Transform canvas)
    {
        var toast = Rect("MilestoneToast", canvas);
        toast.anchorMin = new Vector2(0, 1);
        toast.anchorMax = new Vector2(1, 1);
        toast.pivot = new Vector2(0.5f, 1);
        toast.offsetMin = new Vector2(Gutter, -100);
        toast.offsetMax = new Vector2(-Gutter, -24);
        AddImage(toast, Era1Theme.PanelSprite, Era1Theme.Panel).type = Image.Type.Sliced;
        var group = toast.gameObject.AddComponent<CanvasGroup>();

        var label = Text(toast, "Message", "", 18, Era1Theme.Text, TextAlignmentOptions.Center, FontStyles.Italic);
        Stretch(label.rectTransform);
        label.rectTransform.offsetMin = new Vector2(14, 8);
        label.rectTransform.offsetMax = new Vector2(-14, -8);

        var view = toast.gameObject.AddComponent<ToastView>();
        Wire(view, ("_group", group), ("_label", label));

        var presenter = toast.gameObject.AddComponent<Era1MilestoneToastPresenter>();
        Wire(presenter, ("_toast", view));
        var era2Presenter = toast.gameObject.AddComponent<Era2ToastPresenter>();
        Wire(era2Presenter, ("_toast", view));
        return view;
    }

    // Todo §6 — the neighbour's contract card, first in the list once he has knocked.
    // The holder stays active (it runs the view); the card inside is shown and hidden.
    private static void CreateFirstCustomer(Transform content, Controllers controllers)
    {
        var holder = Stack("FirstCustomer", content, spacing: 0);

        var card = Card("FirstCustomerCard", out var button, out var group);
        card.SetParent(holder, false);
        // A column, not a row: title, story, progress, then the call to action.
        Object.DestroyImmediate(card.GetComponent<HorizontalLayoutGroup>());
        var vlg = card.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(14, 14, 12, 12);
        vlg.spacing = 4;
        vlg.childControlWidth = vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        card.GetComponent<Image>().color = Era1Theme.PanelRaised;

        var title = Text(card, "Title", "", 19, Era1Theme.Accent, TextAlignmentOptions.Left);
        var body = Text(card, "Body", "", 14, Era1Theme.Text, TextAlignmentOptions.Left, FontStyles.Italic);

        var bar = Rect("Progress", card);
        Fixed(bar, 8);
        AddImage(bar, Era1SpriteFactory.White, Era1Theme.ScreenBackground).raycastTarget = false;
        var fillRt = Rect("Fill", bar);
        Stretch(fillRt);
        var fill = AddImage(fillRt, Era1SpriteFactory.White, Era1Theme.Healthy);
        fill.raycastTarget = false;
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillAmount = 0f;

        var status = Text(card, "Status", "", 16, Era1Theme.Text, TextAlignmentOptions.Left);

        var view = holder.gameObject.AddComponent<Era1FirstCustomerView>();
        Wire(view, ("_controller", controllers.FirstCustomer), ("_card", card.gameObject),
                   ("_titleLabel", title), ("_bodyLabel", body), ("_statusLabel", status),
                   ("_button", button), ("_group", group), ("_progressFill", fill));
        var colours = new SerializedObject(view);
        colours.FindProperty("_buildingColour").colorValue  = Era1Theme.Caution;
        colours.FindProperty("_supplyingColour").colorValue = Era1Theme.Healthy;
        colours.ApplyModifiedPropertiesWithoutUndo();
        card.gameObject.SetActive(false);
    }

    // Shown once the first shilling arrives: the title card into Era 2 (todo Era 2 §8).
    private static void CreateEndOfEra(Transform canvas)
    {
        var holder = Rect("EndOfEra", canvas);
        Stretch(holder);

        var panel = Rect("Panel", holder);
        Stretch(panel);
        // Blocks taps on the workshop underneath while open.
        AddImage(panel, Era1SpriteFactory.White, Era1Theme.Overlay);
        var group = panel.gameObject.AddComponent<CanvasGroup>();

        var box = Stack("Box", panel, spacing: 12);
        box.anchorMin = new Vector2(0, 0.5f);
        box.anchorMax = new Vector2(1, 0.5f);
        box.pivot = new Vector2(0.5f, 0.5f);
        box.offsetMin = new Vector2(Gutter, 0);
        box.offsetMax = new Vector2(-Gutter, 0);
        var boxVlg = box.GetComponent<VerticalLayoutGroup>();
        boxVlg.padding = new RectOffset(20, 20, 24, 20);
        box.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        AddImage(box, Era1Theme.PanelSprite, Era1Theme.PanelRaised).type = Image.Type.Sliced;

        Text(box, "Kicker", "End of Era 1  ·  Mr Hale paid you a shilling", 14, Era1Theme.TextMuted,
             TextAlignmentOptions.Center, FontStyles.Italic);
        Text(box, "Title", "The Demonstration", 28, Era1Theme.Achievement, TextAlignmentOptions.Center);
        Text(box, "Date", "9th October 1851", 18, Era1Theme.Text, TextAlignmentOptions.Center, FontStyles.Italic);
        Text(box, "Body",
             "Your first customer has arrived. Thirty years on, the workshop is a demonstration " +
             "office, and all London wants electric light. Take on as many clients as the works can supply.",
             16, Era1Theme.Text, TextAlignmentOptions.Center);

        var open = MakeButton(box, "OpenTheDoors", "Open the doors");

        var view = holder.gameObject.AddComponent<Era1EndOfEraView>();
        Wire(view, ("_panel", panel.gameObject), ("_group", group), ("_continueButton", open));
        panel.gameObject.SetActive(false);
    }

    internal static Button MakeButton(Transform parent, string name, string label)
    {
        var rt = Rect(name, parent);
        Fixed(rt, 52);
        var image = AddImage(rt, Era1Theme.PanelSprite, Era1Theme.Panel);
        image.type = Image.Type.Sliced;
        var button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        rt.gameObject.AddComponent<PressFeedback>();
        var text = Text(rt, "Label", label, 18, Era1Theme.Text, TextAlignmentOptions.Center);
        Stretch(text.rectTransform);
        return button;
    }

    // =====================================================================
    // Prefabs (todo §5.4, §5.5)
    // =====================================================================

    private static void BuildPrefabs()
    {
        Era1AssetBuilder.EnsureFolder(PrefabFolder);
        SaveAsPrefab(BuildUpgradeCard(), UpgradeCardPrefab);
    }

    private static GameObject BuildUpgradeCard()
    {
        var root = Card("UpgradeCard", out var button, out var group);

        var texts = Stack("Texts", root, spacing: 2);
        texts.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
        var name = Text(texts, "Name", "Upgrade", 18, Era1Theme.Text, TextAlignmentOptions.Left);
        var desc = Text(texts, "Description", "", 13, Era1Theme.TextMuted, TextAlignmentOptions.Left, FontStyles.Italic);
        var effect = Text(texts, "Effect", "", 14, Era1Theme.Accent, TextAlignmentOptions.Left);

        var cost = Text(root, "Cost", "0 J", 17, Era1Theme.Accent, TextAlignmentOptions.Right, numerals: true);
        cost.gameObject.AddComponent<LayoutElement>().preferredWidth = 84;

        var view = root.gameObject.AddComponent<UpgradeCardView>();
        Wire(view, ("_nameLabel", name), ("_descriptionLabel", desc), ("_effectLabel", effect),
                   ("_costLabel", cost), ("_button", button), ("_group", group));
        return root.gameObject;
    }

    // A tappable panel laid out as a row. Min height 64 keeps it well over the 44dp target.
    internal static RectTransform Card(string name, out Button button, out CanvasGroup group)
    {
        var root = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        var image = AddImage(root, Era1Theme.PanelSprite, Era1Theme.Panel);
        image.type = Image.Type.Sliced;

        button = root.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        var colors = button.colors;
        // The CanvasGroup does the dimming; a tinted disabled colour would double it.
        colors.disabledColor = Color.white;
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
        button.colors = colors;
        root.gameObject.AddComponent<PressFeedback>();
        group = root.gameObject.AddComponent<CanvasGroup>();

        var hlg = root.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(14, 14, 10, 10);
        hlg.spacing = 10;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = hlg.childControlHeight = true;
        hlg.childForceExpandWidth = hlg.childForceExpandHeight = false;
        root.gameObject.AddComponent<LayoutElement>().minHeight = 64;
        return root;
    }

    internal static void SaveAsPrefab(GameObject go, string path)
    {
        PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
    }

    // =====================================================================
    // Small helpers
    // =====================================================================

    internal static RectTransform Rect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    internal static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    // Full-width strip of fixed height, measured from the top (or bottom) edge.
    internal static void Anchor(RectTransform rt, Vector2 min, Vector2 max, Vector2 offset, float height, bool bottom = false)
    {
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.pivot = new Vector2(0.5f, bottom ? 0f : 1f);
        rt.sizeDelta = new Vector2(0, height);
        rt.anchoredPosition = offset;
    }

    internal static void Fixed(RectTransform rt, float height)
    {
        var le = rt.gameObject.AddComponent<LayoutElement>();
        le.minHeight = le.preferredHeight = height;
        // A layout group that force-expands its children reports itself as flexible;
        // without this a fixed row with such a group soaks up the column's spare height.
        le.flexibleHeight = 0;
    }

    internal static RectTransform Stack(string name, Transform parent, float spacing = 8)
    {
        var rt = Rect(name, parent);
        var vlg = rt.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = spacing;
        vlg.childControlWidth = vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        return rt;
    }

    internal static Image AddImage(RectTransform rt, Sprite sprite, Color color)
    {
        var image = rt.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        return image;
    }

    internal static TextMeshProUGUI Text(Transform parent, string name, string text, float size, Color color,
                                        TextAlignmentOptions align, FontStyles style = FontStyles.Normal,
                                        bool numerals = false)
    {
        var rt = Rect(name, parent);
        var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        var font = numerals ? Era1Theme.NumeralFont : Era1Theme.BodyFont;
        if (font != null) tmp.font = font;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = align;
        tmp.fontStyle = style;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        // Text never eats taps: cards and the crank own their whole area.
        tmp.raycastTarget = false;
        return tmp;
    }

    internal static void Wire(Object target, params (string field, Object value)[] refs)
    {
        var so = new SerializedObject(target);
        foreach (var (field, value) in refs)
        {
            var prop = so.FindProperty(field);
            if (prop == null) throw new System.ArgumentException($"{target.GetType().Name} has no field {field}");
            prop.objectReferenceValue = value;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    internal static void SetArray(SerializedProperty prop, Object[] items)
    {
        prop.arraySize = items.Length;
        for (int i = 0; i < items.Length; i++)
            prop.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
    }
}
