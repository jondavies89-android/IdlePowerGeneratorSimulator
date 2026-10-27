using IdlePowerGen.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Era1SceneBuilder;

// The DEV strip (DevToolsView): a small tab at the top right of the screen over every era's
// screen, opening a panel with era jumps and a time-acceleration slider. Neutral greys, so
// it reads as a tool and not as part of any era's look.
public static class DevToolsBuilder
{
    private static readonly Color Panel   = new(0.08f, 0.08f, 0.09f, 0.96f);
    private static readonly Color Button  = new(0.22f, 0.22f, 0.25f);
    private static readonly Color Text    = new(0.92f, 0.92f, 0.92f);
    private static readonly Color Muted   = new(0.65f, 0.65f, 0.68f);
    private static readonly Color Accent  = new(0.36f, 0.70f, 0.95f);

    public static void Create(Transform canvas)
    {
        // Full-screen but with no graphic of its own: only the tab and the open panel take taps.
        var root = Rect("DevTools", canvas);
        Stretch(root);

        var safe = Rect("SafeArea", root);
        Stretch(safe);
        safe.gameObject.AddComponent<SafeAreaFitter>();

        var toggleRt = Rect("DevToggle", safe);
        toggleRt.anchorMin = toggleRt.anchorMax = new Vector2(1, 1);
        toggleRt.pivot = new Vector2(1, 1);
        toggleRt.sizeDelta = new Vector2(44, 26);
        toggleRt.anchoredPosition = new Vector2(-4, -2);
        var toggleImage = AddImage(toggleRt, Era1SpriteFactory.Panel, new Color(0.1f, 0.1f, 0.12f, 0.75f));
        toggleImage.type = Image.Type.Sliced;
        var toggle = toggleRt.gameObject.AddComponent<UnityEngine.UI.Button>();
        toggle.targetGraphic = toggleImage;
        var toggleLabel = Label(toggleRt, "Label", "DEV", 12, Accent, TextAlignmentOptions.Center);
        Stretch(toggleLabel.rectTransform);

        var panel = Stack("DevPanel", safe, spacing: 8);
        panel.anchorMin = new Vector2(0, 1);
        panel.anchorMax = new Vector2(1, 1);
        panel.pivot = new Vector2(0.5f, 1);
        panel.offsetMin = new Vector2(Gutter, 0);
        panel.offsetMax = new Vector2(-Gutter, -30);
        panel.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(12, 12, 10, 12);
        panel.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var panelImage = AddImage(panel, Era1SpriteFactory.Panel, Panel);
        panelImage.type = Image.Type.Sliced;

        Label(panel, "Title", "DEVELOPER · jump to the start of an era (replaces the save)", 13, Muted,
              TextAlignmentOptions.Left);
        var eraLabel = Label(panel, "EraNow", "", 12, Muted, TextAlignmentOptions.Left);

        var eras = Rect("Eras", panel);
        Fixed(eras, 44);
        var hlg = eras.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 6;
        hlg.childControlWidth = hlg.childControlHeight = true;
        hlg.childForceExpandWidth = hlg.childForceExpandHeight = true;
        var eraButtons = new UnityEngine.UI.Button[6];
        for (int i = 0; i < eraButtons.Length; i++)
            eraButtons[i] = MakeDevButton(eras, $"Era{i + 1}", $"Era {i + 1}");

        var speedRow = Rect("Speed", panel);
        Fixed(speedRow, 44);
        var srow = speedRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        srow.spacing = 10;
        srow.childAlignment = TextAnchor.MiddleLeft;
        srow.childControlWidth = srow.childControlHeight = true;
        srow.childForceExpandWidth = false;
        srow.childForceExpandHeight = true;
        var speedLabel = Label(speedRow, "SpeedLabel", "Time ×1", 15, Text, TextAlignmentOptions.Left);
        speedLabel.gameObject.AddComponent<LayoutElement>().preferredWidth = 90;
        var slider = MakeSlider(speedRow);
        slider.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
        var realTime = MakeDevButton(speedRow, "RealTime", "1×");
        realTime.gameObject.AddComponent<LayoutElement>().preferredWidth = 52;

        var blackout = MakeDevButton(panel, "Blackout", "Black out the grid (Eras 4–6)");
        Fixed((RectTransform)blackout.transform, 40);
        var meetGoal = MakeDevButton(panel, "MeetGoal", "Meet this era's goal (Eras 2–6)");
        Fixed((RectTransform)meetGoal.transform, 40);

        var view = root.gameObject.AddComponent<DevToolsView>();
        Wire(view, ("_toggle", toggle), ("_panel", panel.gameObject), ("_speed", slider),
                   ("_speedLabel", speedLabel), ("_realTime", realTime), ("_eraLabel", eraLabel), ("_blackout", blackout),
                   ("_meetGoal", meetGoal));
        var so = new UnityEditor.SerializedObject(view);
        SetArray(so.FindProperty("_eraButtons"), eraButtons);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static UnityEngine.UI.Button MakeDevButton(Transform parent, string name, string label)
    {
        var rt = Rect(name, parent);
        var image = AddImage(rt, Era1SpriteFactory.Panel, Button);
        image.type = Image.Type.Sliced;
        var button = rt.gameObject.AddComponent<UnityEngine.UI.Button>();
        button.targetGraphic = image;
        var colors = button.colors;
        colors.disabledColor = new Color(1, 1, 1, 0.35f);
        button.colors = colors;
        var text = Label(rt, "Label", label, 15, Text, TextAlignmentOptions.Center);
        Stretch(text.rectTransform);
        return button;
    }

    private static Slider MakeSlider(Transform parent)
    {
        var rt = Rect("SpeedSlider", parent);
        var slider = rt.gameObject.AddComponent<Slider>();

        var track = Rect("Background", rt);
        track.anchorMin = new Vector2(0, 0.5f);
        track.anchorMax = new Vector2(1, 0.5f);
        track.sizeDelta = new Vector2(0, 6);
        AddImage(track, Era1SpriteFactory.White, Button).raycastTarget = false;

        var fillArea = Rect("Fill Area", rt);
        fillArea.anchorMin = new Vector2(0, 0.5f);
        fillArea.anchorMax = new Vector2(1, 0.5f);
        fillArea.sizeDelta = new Vector2(-20, 6);
        var fill = Rect("Fill", fillArea);
        fill.sizeDelta = new Vector2(10, 0);
        AddImage(fill, Era1SpriteFactory.White, Accent).raycastTarget = false;

        var handleArea = Rect("Handle Slide Area", rt);
        Stretch(handleArea);
        handleArea.offsetMin = new Vector2(10, 0);
        handleArea.offsetMax = new Vector2(-10, 0);
        var handle = Rect("Handle", handleArea);
        handle.sizeDelta = new Vector2(24, 0);
        var handleImage = AddImage(handle, Era1SpriteFactory.Disc, Text);

        slider.fillRect = fill;
        slider.handleRect = handle;
        slider.targetGraphic = handleImage;
        slider.direction = Slider.Direction.LeftToRight;
        return slider;
    }

    // Plain sans (TMP's default) rather than an era's period type.
    private static TextMeshProUGUI Label(Transform parent, string name, string text, float size, Color colour,
                                         TextAlignmentOptions align)
    {
        var rt = Rect(name, parent);
        var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = colour;
        tmp.alignment = align;
        tmp.raycastTarget = false;
        return tmp;
    }
}
