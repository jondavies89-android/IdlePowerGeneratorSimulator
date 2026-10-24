using TMPro;
using UnityEngine;

// Era 1 colours and type (todo §7: candlelit workshop, sepia, Georgia-style serif,
// Bodoni numerals), in one place so the scene builder never hard-codes a shade.
public static class Era1Theme
{
    // Todo §7 values.
    public static readonly Color CameraBackground = Hex("#1A1208"); // deep warm black
    public static readonly Color Text             = Hex("#F5DEB3"); // wheat / candlelight
    public static readonly Color Accent           = Hex("#FFD700"); // candlelit gold, joule values
    public static readonly Color Panel            = Hex("#2E1F0E");

    // Derived from those.
    public static readonly Color ScreenBackground = CameraBackground;
    public static readonly Color TextMuted        = Hex("#BFA47A"); // wheat seen further from the candle
    public static readonly Color PanelRaised      = Hex("#3F2B12");
    public static readonly Color Glow             = new(1.00f, 0.62f, 0.22f, 0.22f); // candle light behind the crank
    public static readonly Color Overlay          = new(0.10f, 0.07f, 0.03f, 0.90f);

    // Colour grammar (CLAUDE.md): green healthy, amber caution, red alarm, gold achievement —
    // in Era 1's warm register.
    public static readonly Color Healthy          = Hex("#9BBF5A");
    public static readonly Color Caution          = Hex("#E0A040"); // lamp-oil amber
    public static readonly Color Achievement      = Accent;

    public static TMP_FontAsset BodyFont    => Era1FontBuilder.Body;
    public static TMP_FontAsset NumeralFont => Era1FontBuilder.Numeral;
    public static Sprite PanelSprite        => Era1SpriteFactory.PanelEdged;

    private static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;
}
