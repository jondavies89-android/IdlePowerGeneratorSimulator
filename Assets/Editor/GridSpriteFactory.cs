using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// Procedural sprites for the Era 3–5 screens: rings for map nodes and dials, a half ring
// for the frequency gauge, a rounded square for stations. White, tinted per use, drawn from
// signed distances like Era1SpriteFactory's.
public static class GridSpriteFactory
{
    public const string Folder = "Assets/Sprites/UI/Grid";

    public static Sprite Ring     => Load("Ring");
    public static Sprite ThinRing => Load("ThinRing");
    public static Sprite HalfRing => Load("HalfRing");
    public static Sprite Square   => Load("Square");

    public static void Build()
    {
        Era1AssetBuilder.EnsureFolder(Folder);
        Write("Ring", 128, (x, y, s) => RingPixel(x, y, s, 9f));
        Write("ThinRing", 256, (x, y, s) => RingPixel(x, y, s, 4f));
        Write("HalfRing", 256, HalfRingPixel);
        Write("Square", 64, SquarePixel, border: 12);
    }

    private static Sprite Load(string name) => AssetDatabase.LoadAssetAtPath<Sprite>($"{Folder}/{name}.png");

    private static Color RingPixel(float x, float y, int size, float thickness)
    {
        float d = Vector2.Distance(new Vector2(x, y), Vector2.one * size / 2f);
        float outer = size / 2f - 2f;
        float sd = Mathf.Abs(d - (outer - thickness / 2f)) - thickness / 2f;
        return new Color(1, 1, 1, Coverage(sd));
    }

    // The top half of a ring, for a gauge's scale; the sprite's centre is the needle's pivot.
    private static Color HalfRingPixel(float x, float y, int size)
    {
        var c = RingPixel(x, y, size, 6f);
        if (y < size / 2f) c.a = 0f;
        return c;
    }

    private static Color SquarePixel(float x, float y, int size)
    {
        const float r = 8f;
        float qx = Mathf.Max(Mathf.Abs(x - size / 2f) - (size / 2f - r - 1f), 0f);
        float qy = Mathf.Max(Mathf.Abs(y - size / 2f) - (size / 2f - r - 1f), 0f);
        float sd = Mathf.Sqrt(qx * qx + qy * qy) - r;
        return new Color(1, 1, 1, Coverage(sd));
    }

    private static float Coverage(float signedDistance) => Mathf.Clamp01(0.5f - signedDistance);

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
