using TMPro;
using UnityEngine;

// Era 1 colours and type, in one place so the scene builder never hard-codes a shade.
// Interim neutral palette; the candlelit sepia theme replaces it (todo §7).
public static class Era1Theme
{
    public static readonly Color CameraBackground = new(0.10f, 0.10f, 0.10f);
    public static readonly Color ScreenBackground = new(0.12f, 0.12f, 0.12f);
    public static readonly Color Panel            = new(0.20f, 0.20f, 0.20f);
    public static readonly Color Text             = new(0.92f, 0.92f, 0.92f);
    public static readonly Color TextMuted        = new(0.65f, 0.65f, 0.65f);
    public static readonly Color Accent           = new(1.00f, 0.84f, 0.00f);
    public static readonly Color PanelRaised      = new(0.26f, 0.26f, 0.26f);
    public static readonly Color Overlay          = new(0.00f, 0.00f, 0.00f, 0.80f);

    // Colour grammar (CLAUDE.md): green healthy, amber caution, red alarm, gold achievement.
    public static readonly Color Healthy          = new(0.45f, 0.75f, 0.35f);
    public static readonly Color Achievement      = new(1.00f, 0.84f, 0.00f);

    public static TMP_FontAsset BodyFont     => null; // null = TMP default font
    public static TMP_FontAsset NumeralFont  => null;
    public static Sprite PanelSprite         => Era1SpriteFactory.Panel;
}
