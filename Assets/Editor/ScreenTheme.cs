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
