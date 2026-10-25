using UnityEngine;

// Era 2 colours (todo Era 2 §7 and the bible's Appendix A mockup): a gas-lit office,
// warmer and a little lighter than the Era 1 workshop, with brass edges, a parchment
// text colour and an old-gold purse. The type stays Era 1's (todo §7: "retain").
public static class Era2Theme
{
    public static readonly Color Background  = Hex("#2A1A14"); // gas-lit brown, warmer than Era 1
    public static readonly Color Panel       = Hex("#1C120C");
    public static readonly Color PanelRaised = Hex("#24170F");
    public static readonly Color Brass       = Hex("#8A6A3A"); // panel rims and rules
    public static readonly Color Text        = Hex("#EDD9A3"); // warm parchment
    public static readonly Color TextMuted   = Hex("#B89A6A");
    public static readonly Color Purse       = Hex("#D4A017"); // old gold coin
    public static readonly Color Stars       = Hex("#F2C75C");
    public static readonly Color StarsEmpty  = Hex("#5A4630");
    public static readonly Color Running     = Hex("#E07050"); // coral: what the works costs to run
    public static readonly Color Track       = Hex("#120B07"); // empty part of a bar
    public static readonly Color OwnedPanel  = Hex("#3A2A10"); // a bought improvement: gilded, not lit
    public static readonly Color Overlay     = new(0.10f, 0.06f, 0.04f, 0.92f);

    // Colour grammar (CLAUDE.md), todo §7 shades.
    public static readonly Color Healthy     = Hex("#4CAF50");
    public static readonly Color Caution     = Hex("#FF9800");
    public static readonly Color Alarm       = Hex("#E53935");
    public static readonly Color AlarmPanel  = Hex("#311412");
    public static readonly Color Achievement = Purse;

    private static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;
}
