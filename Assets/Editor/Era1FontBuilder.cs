using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

// TMP font assets for the Era 1 type (todo §7): IM Fell English for text, Bodoni Moda for
// numerals. Dynamic atlases pre-filled with the characters the UI uses, so the first frame
// doesn't stall rasterising glyphs; anything unexpected still renders on demand, and glyphs
// neither font has fall back to TMP's LiberationSans.
public static class Era1FontBuilder
{
    private const string Folder = "Assets/Fonts";

    public const string BodyPath    = Folder + "/IMFellEnglish SDF.asset";
    public const string ItalicPath  = Folder + "/IMFellEnglish Italic SDF.asset";
    public const string NumeralPath = Folder + "/BodoniModa SDF.asset";

    private const string FallbackPath =
        "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

    private const string Prefill =
        " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~" +
        "£·×—–…‘’“”";

    public static TMP_FontAsset Body    => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BodyPath);
    public static TMP_FontAsset Numeral => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(NumeralPath);

    public static void Build()
    {
        var fallback = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FallbackPath);

        var italic  = Create(Folder + "/IMFellEnglish/IMFellEnglish-Italic.ttf", ItalicPath, fallback);
        var body    = Create(Folder + "/IMFellEnglish/IMFellEnglish-Regular.ttf", BodyPath, fallback);
        var numeral = Create(Folder + "/BodoniModa/BodoniModa.ttf", NumeralPath, fallback);

        // Real italic face for FontStyles.Italic instead of TMP's synthetic slant
        // (index 4 = weight 400, the regular weight).
        body.fontWeightTable[4].italicTypeface = italic;
        // Numerals in Bodoni, words around them ("Build", "Owned") in Fell.
        numeral.fallbackFontAssetTable = new List<TMP_FontAsset> { body, fallback };
        EditorUtility.SetDirty(body);
        EditorUtility.SetDirty(numeral);

        AssetDatabase.SaveAssets();
        Debug.Log("[Era1FontBuilder] Font assets written");
    }

    private static TMP_FontAsset Create(string ttfPath, string assetPath, TMP_FontAsset fallback)
    {
        // Kept if present: recreating would change the GUID every scene reference points at.
        var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
        if (existing != null) return existing;

        var font = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
        var asset = TMP_FontAsset.CreateFontAsset(font, 72, 8, GlyphRenderMode.SDFAA, 1024, 1024,
                                                  AtlasPopulationMode.Dynamic, enableMultiAtlasSupport: true);
        asset.name = System.IO.Path.GetFileNameWithoutExtension(assetPath);
        AssetDatabase.CreateAsset(asset, assetPath);

        // The atlas and material are sub-objects; without this they vanish on reload.
        asset.atlasTextures[0].name = asset.name + " Atlas";
        AssetDatabase.AddObjectToAsset(asset.atlasTextures[0], asset);
        asset.material.name = asset.name + " Material";
        AssetDatabase.AddObjectToAsset(asset.material, asset);

        asset.TryAddCharacters(Prefill, out _);
        asset.fallbackFontAssetTable = new List<TMP_FontAsset> { fallback };
        EditorUtility.SetDirty(asset);
        return asset;
    }
}
