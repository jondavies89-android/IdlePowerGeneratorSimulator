using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// Placeholder art for Era 1, drawn procedurally so the build has no missing sprites until
// real art arrives. Shapes are anti-aliased from signed distances; replace any PNG in
// Assets/Sprites/UI/Era1 with real art of the same name and the scene picks it up.
public static class Era1SpriteFactory
{
    public const string Folder = "Assets/Sprites/UI/Era1";

    public static Sprite Panel  => Load("Panel");
    public static Sprite Disc   => Load("Disc");
    public static Sprite Crank  => Load("Crank");
    public static Sprite Bolt   => Load("Bolt");
    public static Sprite White  => Load("White");

    public static void Build()
    {
        Era1AssetBuilder.EnsureFolder(Folder);
        // Filled images (progress bars) need a sprite; a sliced panel would distort.
        Write("White", 8, (x, y, s) => Color.white);
        Write("Panel", 64, PanelPixel, border: 20);
        Write("Disc", 128, DiscPixel);
        Write("Crank", 512, CrankPixel);
        Write("Bolt", 128, BoltPixel);
    }

    private static Sprite Load(string name) => AssetDatabase.LoadAssetAtPath<Sprite>($"{Folder}/{name}.png");

    // ---- Shapes (coordinates in pixels, origin bottom-left) ----

    // White rounded rectangle; 9-sliced and tinted per use.
    private static Color PanelPixel(float x, float y, int size)
    {
        const float r = 16f;
        float qx = Mathf.Max(Mathf.Abs(x - size / 2f) - (size / 2f - r), 0f);
        float qy = Mathf.Max(Mathf.Abs(y - size / 2f) - (size / 2f - r), 0f);
        float sd = Mathf.Sqrt(qx * qx + qy * qy) - r;
        return new Color(1, 1, 1, Coverage(sd));
    }

    private static Color DiscPixel(float x, float y, int size)
    {
        float sd = Vector2.Distance(new Vector2(x, y), Vector2.one * size / 2f) - (size / 2f - 2f);
        return new Color(1, 1, 1, Coverage(sd));
    }

    // Brass crank wheel: bronze rim, brass face with a lit upper-left, six spokes, hub,
    // and a wooden-knobbed arm.
    private static Color CrankPixel(float x, float y, int size)
    {
        var c = new Vector2(size / 2f, size / 2f);
        var p = new Vector2(x, y);
        float d = Vector2.Distance(p, c);

        var bronze = new Color(0.36f, 0.24f, 0.10f);
        var brassHi = new Color(0.96f, 0.82f, 0.50f);
        var brassLo = new Color(0.60f, 0.43f, 0.17f);
        var wood = new Color(0.45f, 0.26f, 0.12f);

        Color col = new Color(0, 0, 0, 0);

        // Rim and face
        col = Over(col, bronze, Coverage(d - 240f));
        Vector2 light = c + new Vector2(-60f, 70f);
        float shade = Mathf.Clamp01(Vector2.Distance(p, light) / 300f);
        col = Over(col, Color.Lerp(brassHi, brassLo, shade), Coverage(d - 224f));

        // Spokes
        for (int i = 0; i < 6; i++)
        {
            float a = i * Mathf.PI / 3f;
            float along = Vector2.Dot(p - c, new Vector2(Mathf.Cos(a), Mathf.Sin(a)));
            float across = Mathf.Abs(Vector2.Dot(p - c, new Vector2(-Mathf.Sin(a), Mathf.Cos(a))));
            if (along > 40f && along < 205f)
                col = Over(col, new Color(bronze.r, bronze.g, bronze.b, 0.75f), Coverage(across - 6f));
        }
        col = Over(col, bronze, Coverage(Mathf.Abs(d - 205f) - 5f));

        // Hub
        col = Over(col, bronze, Coverage(d - 48f));
        col = Over(col, brassHi, Coverage(d - 26f));

        // Arm towards the upper right, ending in a wooden knob
        Vector2 dir = new Vector2(1f, 1f).normalized;
        Vector2 knob = c + dir * 165f;
        float t = Mathf.Clamp(Vector2.Dot(p - c, dir), 0f, 165f);
        float armDist = Vector2.Distance(p, c + dir * t);
        col = Over(col, Color.Lerp(brassLo, bronze, 0.4f), Coverage(armDist - 14f));
        float kd = Vector2.Distance(p, knob);
        col = Over(col, wood, Coverage(kd - 36f));
        col = Over(col, Color.Lerp(wood, brassHi, 0.35f), Coverage(Vector2.Distance(p, knob + new Vector2(-10f, 10f)) - 12f));

        return col;
    }

    // Lightning bolt, the ⚡ of the stored-charge reading (no font here has the glyph).
    private static readonly Vector2[] BoltShape =
    {
        new(0.62f, 1.00f), new(0.18f, 0.44f), new(0.47f, 0.44f), new(0.36f, 0.00f),
        new(0.84f, 0.58f), new(0.55f, 0.58f), new(0.72f, 1.00f),
    };

    private static Color BoltPixel(float x, float y, int size)
    {
        // 4×4 supersampling; polygons have no cheap signed distance.
        int inside = 0;
        for (int sx = 0; sx < 4; sx++)
        for (int sy = 0; sy < 4; sy++)
        {
            var q = new Vector2((x - 0.5f + (sx + 0.5f) / 4f) / size, (y - 0.5f + (sy + 0.5f) / 4f) / size);
            if (InPolygon(q, BoltShape)) inside++;
        }
        return new Color(1, 1, 1, inside / 16f);
    }

    // ---- Helpers ----

    private static float Coverage(float signedDistance) => Mathf.Clamp01(0.5f - signedDistance);

    private static Color Over(Color under, Color over, float alpha)
    {
        float a = over.a * alpha;
        float outA = a + under.a * (1f - a);
        if (outA <= 0f) return new Color(0, 0, 0, 0);
        Color rgb = (over * a + under * under.a * (1f - a)) / outA;
        rgb.a = outA;
        return rgb;
    }

    private static bool InPolygon(Vector2 p, Vector2[] poly)
    {
        bool inside = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
        {
            if ((poly[i].y > p.y) != (poly[j].y > p.y) &&
                p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                inside = !inside;
        }
        return inside;
    }

    private static void Write(string name, int size, Func<float, float, int, Color> pixel, int border = 0)
    {
        string path = $"{Folder}/{name}.png";
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
            pixels[y * size + x] = pixel(x + 0.5f, y + 0.5f, size);
        tex.SetPixels(pixels);
        File.WriteAllBytes(path, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType         = TextureImporterType.Sprite;
        importer.spriteImportMode    = SpriteImportMode.Single;
        importer.mipmapEnabled       = false;
        importer.alphaIsTransparency = true;
        importer.spriteBorder        = new Vector4(border, border, border, border);
        importer.SaveAndReimport();
    }
}
