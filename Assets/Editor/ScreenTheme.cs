using TMPro;
using UnityEngine;

// One era's look for ScreenKit: colours by role, and the type. The colour grammar is the
// same in every era (CLAUDE.md: green healthy, amber caution, red alarm, gold achievement);
// only the shades change.
public class ScreenTheme
{
    public Color Background, Panel, PanelRaised, Edge, Track;
    public Color Text, TextMuted, Money, Research;
    public Color Healthy, Caution, Alarm, AlarmPanel, Achievement;
    public Color Overlay;
    public TMP_FontAsset Body, Numerals;
    // Monospaced readouts (Era 4's unit board) wrap their text in <mspace>.
    public bool Sans;
    public Sprite PanelSprite;

    public static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;
}

// Era 4 (bible Appendix A): a bakelite-and-brass control desk. Brown panels, cream type,
// sans-serif for the first time, and a monospaced unit board.
public static class Era4Theme
{
    public static TMP_FontAsset Sans => UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
        "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");

    public static readonly ScreenTheme Theme = new()
    {
        Background  = ScreenTheme.Hex("#3B2A1D"),
        Panel       = ScreenTheme.Hex("#1E1510"),
        PanelRaised = ScreenTheme.Hex("#2B1F16"),
        Edge        = ScreenTheme.Hex("#5C4632"),
        Track       = ScreenTheme.Hex("#120C08"),
        Text        = ScreenTheme.Hex("#EADBB5"),
        TextMuted   = ScreenTheme.Hex("#A8906E"),
        Money       = ScreenTheme.Hex("#F2DC94"),
        Research    = ScreenTheme.Hex("#C9B0E0"),
        Healthy     = ScreenTheme.Hex("#7CC47A"),
        Caution     = ScreenTheme.Hex("#F0B040"),
        Alarm       = ScreenTheme.Hex("#E05040"),
        AlarmPanel  = ScreenTheme.Hex("#3A1510"),
        Achievement = ScreenTheme.Hex("#F2DC94"),
        Overlay     = new Color(0.10f, 0.07f, 0.05f, 0.95f),
        Body        = Sans,
        Numerals    = Sans,
        Sans        = true,
        PanelSprite = Era1SpriteFactory.Panel,
    };

    public static readonly Color GaugeFace = ScreenTheme.Hex("#0A0806");
    public static readonly Color Needle    = ScreenTheme.Hex("#F2DC94");
}

// Era 5 (bible Appendix A): painted metal, mid-century. A light grey-green room, dark
// type, green lines on the mimic board, and the warning in terracotta.
public static class Era5Theme
{
    public static readonly ScreenTheme Theme = new()
    {
        Background  = ScreenTheme.Hex("#B6B8A6"),
        Panel       = ScreenTheme.Hex("#DCDCC6"),
        PanelRaised = ScreenTheme.Hex("#E8E8D4"),
        Edge        = ScreenTheme.Hex("#7E816E"),
        Track       = ScreenTheme.Hex("#A7AA96"),
        Text        = ScreenTheme.Hex("#22261E"),
        TextMuted   = ScreenTheme.Hex("#5E6354"),
        Money       = ScreenTheme.Hex("#2F5E3A"),
        Research    = ScreenTheme.Hex("#5A4A7A"),
        Healthy     = ScreenTheme.Hex("#3A6B42"),
        Caution     = ScreenTheme.Hex("#B5612A"),
        Alarm       = ScreenTheme.Hex("#A8281E"),
        AlarmPanel  = ScreenTheme.Hex("#F3D7C7"),
        Achievement = ScreenTheme.Hex("#8A6A10"),
        Overlay     = new Color(0.18f, 0.19f, 0.16f, 0.94f),
        Body        = Era4Theme.Sans,
        Numerals    = Era4Theme.Sans,
        Sans        = true,
        PanelSprite = Era1SpriteFactory.Panel,
    };

    public static readonly Color BoardGround = ScreenTheme.Hex("#F0F1E0");
    public static readonly Color Chip        = ScreenTheme.Hex("#8C907C");
}

// The black start's emergency lighting (bible §15.2): amber on deep black, one ominous red
// for the outage timer. The same in every era.
public static class BlackStartTheme
{
    public static readonly ScreenTheme Theme = new()
    {
        Background  = ScreenTheme.Hex("#080604"),
        Panel       = ScreenTheme.Hex("#18110A"),
        PanelRaised = ScreenTheme.Hex("#261A0D"),
        Edge        = ScreenTheme.Hex("#6A4A1A"),
        Track       = ScreenTheme.Hex("#0E0A06"),
        Text        = ScreenTheme.Hex("#F2C46A"),
        TextMuted   = ScreenTheme.Hex("#A07A3A"),
        Money       = ScreenTheme.Hex("#F2C46A"),
        Research    = ScreenTheme.Hex("#F2C46A"),
        Healthy     = ScreenTheme.Hex("#8FCB6A"),
        Caution     = ScreenTheme.Hex("#F0A030"),
        Alarm       = ScreenTheme.Hex("#E0261E"),
        AlarmPanel  = ScreenTheme.Hex("#2A0C08"),
        Achievement = ScreenTheme.Hex("#F2C46A"),
        Overlay     = new Color(0.02f, 0.015f, 0.01f, 0.98f),
        Body        = Era4Theme.Sans,
        Numerals    = Era4Theme.Sans,
        Sans        = true,
        PanelSprite = Era1SpriteFactory.Panel,
    };
}

// Era 3 (bible Appendix A): Pearl Street at night. Near-black with a warm cast, thin brass
// rules, lamp-gold for lit districts and money, coral for what the lines lose. Era 1–2's
// period serif carries on: it is still the 1890s.
public static class Era3Theme
{
    public static readonly ScreenTheme Theme = new()
    {
        Background  = ScreenTheme.Hex("#1C1814"),
        Panel       = ScreenTheme.Hex("#14100D"),
        PanelRaised = ScreenTheme.Hex("#221B14"),
        Edge        = ScreenTheme.Hex("#5A4A2E"),
        Track       = ScreenTheme.Hex("#0B0907"),
        Text        = ScreenTheme.Hex("#ECDDB8"),
        TextMuted   = ScreenTheme.Hex("#A08C68"),
        Money       = ScreenTheme.Hex("#F2C14E"),
        Research    = ScreenTheme.Hex("#C9A0DC"),
        Healthy     = ScreenTheme.Hex("#6FBF5A"),
        Caution     = ScreenTheme.Hex("#F0A030"),
        Alarm       = ScreenTheme.Hex("#E05A4A"),
        AlarmPanel  = ScreenTheme.Hex("#2A1712"),
        Achievement = ScreenTheme.Hex("#F2C14E"),
        Overlay     = new Color(0.07f, 0.06f, 0.05f, 0.94f),
        Body        = Era1FontBuilder.Body,
        Numerals    = Era1FontBuilder.Numeral,
        Sans        = false,
        PanelSprite = Era1SpriteFactory.PanelEdged,
    };

    // Lines and districts on the town map.
    public static readonly Color LitLine   = ScreenTheme.Hex("#B8893A");
    public static readonly Color DarkLine  = ScreenTheme.Hex("#3A3024");
    public static readonly Color Unlit     = ScreenTheme.Hex("#6A5C44");
    public static readonly Color MapGround = ScreenTheme.Hex("#0D0A08");
}
