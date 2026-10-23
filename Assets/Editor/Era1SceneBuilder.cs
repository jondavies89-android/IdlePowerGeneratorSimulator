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

    private const string PrefabFolder         = "Assets/Prefabs/UI";
    private const string UpgradeCardPrefab    = PrefabFolder + "/UpgradeCard.prefab";
    private const string GeneratorRowPrefab   = PrefabFolder + "/GeneratorRow.prefab";

    // Portrait phone reference (CLAUDE.md: test at 412×915). 1 canvas unit ≈ 1 dp.
    private static readonly Vector2 ReferenceResolution = new(412, 915);

    private const float Gutter = 16f;

    [MenuItem("IdlePowerGen/Build/Era 1 Scene")]
    public static void Build()
    {
        Era1SpriteFactory.Build();
        BuildPrefabs();

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
    }

    // Todo §4.4 — the Inspector drag-and-drop, done from the asset folders.
    private static void WireCatalogues(IdlePowerGen.Managers.GameManager gm)
    {
        var so = new SerializedObject(gm);
        SetArray(so.FindProperty("_eraConfigs"),           Era1AssetBuilder.EraConfigs());
        SetArray(so.FindProperty("_upgradeDefinitions"),   Era1AssetBuilder.Upgrades());
        SetArray(so.FindProperty("_generatorDefinitions"), Era1AssetBuilder.Generators());
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private struct Controllers
    {
        public Era1TapController Tap;
        public Era2GeneratorController Generators;
    }

    // Scene-scoped (not DontDestroyOnLoad): they belong to the Era 1 screen.
    private static Controllers CreateControllers()
    {
        var go = new GameObject("_Controllers");
        return new Controllers
        {
            Tap        = go.AddComponent<Era1TapController>(),
            Generators = go.AddComponent<Era2GeneratorController>(),
        };
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

        var safe = Rect("SafeArea", canvasGo.transform);
        Stretch(safe);
        safe.gameObject.AddComponent<SafeAreaFitter>();

        var column = Rect("Column", safe);
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

        CreateToast(canvasGo.transform);
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
        button.anchorMin = button.anchorMax = new Vector2(0.5f, 1f);
        button.pivot = new Vector2(0.5f, 1f);
        button.anchoredPosition = new Vector2(0, -6);
        button.sizeDelta = new Vector2(196, 196);
        var image = AddImage(button, Era1SpriteFactory.Crank, Color.white);
        var btn = button.gameObject.AddComponent<Button>();
        btn.targetGraphic = image;
        btn.transition = Selectable.Transition.ColorTint;
        var colors = btn.colors;
        colors.pressedColor = new Color(0.85f, 0.8f, 0.7f);
        btn.colors = colors;
        button.gameObject.AddComponent<PressFeedback>();
        UnityEventTools.AddPersistentListener(btn.onClick, controllers.Tap.OnCrankTapped);

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

        Text(content, "ImprovementsHeader", "Improvements", 18, Era1Theme.Text, TextAlignmentOptions.Left);
        var upgradePanel = Stack("UpgradePanel", content);
        var empty = Text(content, "NothingToImprove", "Nothing more to improve for now.", 14, Era1Theme.TextMuted,
                         TextAlignmentOptions.Left, FontStyles.Italic);

        var machinesHeader = Text(content, "MachinesHeader", "Machines", 18, Era1Theme.Text, TextAlignmentOptions.Left);
        var generatorPanel = Stack("GeneratorPanel", content);

        var upgrades = upgradePanel.gameObject.AddComponent<UpgradeListView>();
        Wire(upgrades,
             ("_cardPrefab", AssetDatabase.LoadAssetAtPath<UpgradeCardView>(UpgradeCardPrefab)),
             ("_content", upgradePanel),
             ("_emptyHint", empty.gameObject));

        var generators = generatorPanel.gameObject.AddComponent<GeneratorListView>();
        Wire(generators,
             ("_rowPrefab", AssetDatabase.LoadAssetAtPath<GeneratorRowView>(GeneratorRowPrefab)),
             ("_content", generatorPanel),
             ("_sectionHeader", machinesHeader.gameObject),
             ("_controller", controllers.Generators));
    }

    private static void CreateToast(Transform canvas)
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
    }

    // =====================================================================
    // Prefabs (todo §5.4, §5.5)
    // =====================================================================

    private static void BuildPrefabs()
    {
        Era1AssetBuilder.EnsureFolder(PrefabFolder);
        SaveAsPrefab(BuildUpgradeCard(), UpgradeCardPrefab);
        SaveAsPrefab(BuildGeneratorRow(), GeneratorRowPrefab);
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

    private static GameObject BuildGeneratorRow()
    {
        var root = Card("GeneratorRow", out var button, out var group);

        var icon = Rect("Icon", root);
        var iconImage = AddImage(icon, Era1SpriteFactory.Disc, Era1Theme.Accent);
        iconImage.raycastTarget = false;
        iconImage.preserveAspect = true;
        var iconLe = icon.gameObject.AddComponent<LayoutElement>();
        iconLe.preferredWidth = iconLe.preferredHeight = 36;

        var texts = Stack("Texts", root, spacing: 2);
        texts.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
        var name = Text(texts, "Name", "Machine", 18, Era1Theme.Text, TextAlignmentOptions.Left);
        var owned = Text(texts, "Owned", "Owned: 0", 14, Era1Theme.TextMuted, TextAlignmentOptions.Left);

        var cost = Text(root, "Cost", "Build\n0 J", 16, Era1Theme.Accent, TextAlignmentOptions.Right, numerals: true);
        cost.gameObject.AddComponent<LayoutElement>().preferredWidth = 84;

        var view = root.gameObject.AddComponent<GeneratorRowView>();
        Wire(view, ("_nameLabel", name), ("_ownedLabel", owned), ("_costLabel", cost),
                   ("_icon", iconImage), ("_button", button), ("_group", group));
        return root.gameObject;
    }

    // A tappable panel laid out as a row. Min height 64 keeps it well over the 44dp target.
    private static RectTransform Card(string name, out Button button, out CanvasGroup group)
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

    private static void SaveAsPrefab(GameObject go, string path)
    {
        PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
    }

    // =====================================================================
    // Small helpers
    // =====================================================================

    private static RectTransform Rect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    // Full-width strip of fixed height, measured from the top (or bottom) edge.
    private static void Anchor(RectTransform rt, Vector2 min, Vector2 max, Vector2 offset, float height, bool bottom = false)
    {
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.pivot = new Vector2(0.5f, bottom ? 0f : 1f);
        rt.sizeDelta = new Vector2(0, height);
        rt.anchoredPosition = offset;
    }

    private static void Fixed(RectTransform rt, float height)
    {
        var le = rt.gameObject.AddComponent<LayoutElement>();
        le.minHeight = le.preferredHeight = height;
    }

    private static RectTransform Stack(string name, Transform parent, float spacing = 8)
    {
        var rt = Rect(name, parent);
        var vlg = rt.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = spacing;
        vlg.childControlWidth = vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        return rt;
    }

    private static Image AddImage(RectTransform rt, Sprite sprite, Color color)
    {
        var image = rt.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        return image;
    }

    private static TextMeshProUGUI Text(Transform parent, string name, string text, float size, Color color,
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

    private static void Wire(Object target, params (string field, Object value)[] refs)
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

    private static void SetArray(SerializedProperty prop, Object[] items)
    {
        prop.arraySize = items.Length;
        for (int i = 0; i < items.Length; i++)
            prop.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
    }
}
