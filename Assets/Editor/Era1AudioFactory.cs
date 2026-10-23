using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// Placeholder Era 1 sounds (todo §8), synthesised so the build has audio before real
// recordings exist. Each is a short mono WAV; drop a real clip over the same file name and
// the scene keeps working. Fixed seeds keep the output identical from run to run.
public static class Era1AudioFactory
{
    public const string SfxFolder   = "Assets/Audio/SFX/Era1";
    public const string MusicFolder = "Assets/Audio/Music/Era1";

    public const string CrankPath     = SfxFolder + "/CrankRatchet.wav";
    public const string ChimePath     = SfxFolder + "/UpgradeChime.wav";
    public const string StingerPath   = SfxFolder + "/MilestoneStinger.wav";
    public const string AmbiencePath  = MusicFolder + "/WorkshopAmbience.wav";

    private const int Rate = 44100;

    public static AudioClip Crank    => AssetDatabase.LoadAssetAtPath<AudioClip>(CrankPath);
    public static AudioClip Chime    => AssetDatabase.LoadAssetAtPath<AudioClip>(ChimePath);
    public static AudioClip Stinger  => AssetDatabase.LoadAssetAtPath<AudioClip>(StingerPath);
    public static AudioClip Ambience => AssetDatabase.LoadAssetAtPath<AudioClip>(AmbiencePath);

    public static void Build()
    {
        Era1AssetBuilder.EnsureFolder(SfxFolder);
        Era1AssetBuilder.EnsureFolder(MusicFolder);
        Write(CrankPath, CrankRatchet());
        Write(ChimePath, BrassChime());
        Write(StingerPath, OrchestralHit());
        Write(AmbiencePath, FireAndWorkshop(), loop: true);
    }

    // Three ratchet pawl clicks over a low wooden thunk, ~0.12 s.
    private static float[] CrankRatchet()
    {
        var rng = new System.Random(11);
        var s = new float[(int)(Rate * 0.12f)];
        foreach (float at in new[] { 0f, 0.028f, 0.056f })
        {
            int start = (int)(at * Rate);
            for (int i = 0; i < Rate * 0.012f && start + i < s.Length; i++)
            {
                float env = Mathf.Exp(-i / (Rate * 0.0018f));
                s[start + i] += (float)(rng.NextDouble() * 2 - 1) * env * 0.55f;
            }
        }
        for (int i = 0; i < s.Length; i++)
        {
            float t = i / (float)Rate;
            s[i] += Mathf.Sin(2 * Mathf.PI * 140f * t) * Mathf.Exp(-t / 0.03f) * 0.35f;
        }
        return s;
    }

    // Small brass bell: inharmonic partials typical of a bell, long exponential ring.
    private static float[] BrassChime()
    {
        var s = new float[(int)(Rate * 1.6f)];
        float[] ratios = { 1f, 2.0f, 2.76f, 5.40f, 8.93f };
        float[] amps   = { 0.5f, 0.2f, 0.25f, 0.1f, 0.05f };
        float[] decays = { 1.2f, 0.9f, 0.6f, 0.35f, 0.2f };
        for (int i = 0; i < s.Length; i++)
        {
            float t = i / (float)Rate;
            float attack = Mathf.Clamp01(t / 0.004f);
            for (int p = 0; p < ratios.Length; p++)
                s[i] += Mathf.Sin(2 * Mathf.PI * 880f * ratios[p] * t) * amps[p] * Mathf.Exp(-t / decays[p]) * attack;
        }
        return s;
    }

    // Warm tutti chord (C major, low strings and brass) over a timpani thump.
    private static float[] OrchestralHit()
    {
        var s = new float[(int)(Rate * 2.0f)];
        float[] notes = { 65.4f, 130.8f, 196.0f, 261.6f, 329.6f, 392.0f };
        for (int i = 0; i < s.Length; i++)
        {
            float t = i / (float)Rate;
            float env = Mathf.Clamp01(t / 0.025f) * Mathf.Exp(-t / 0.7f);
            float v = 0f;
            foreach (float f in notes)
                for (int h = 1; h <= 6; h++)
                    v += Mathf.Sin(2 * Mathf.PI * f * h * t + h) / (h * h) * 0.09f;
            float drop = 70f * Mathf.Exp(-t / 0.4f) + 45f;
            v += Mathf.Sin(2 * Mathf.PI * drop * t) * Mathf.Exp(-t / 0.25f) * 0.5f;
            s[i] = v * env;
        }
        return s;
    }

    // 8 s seamless loop: brown-noise fire roar with random crackles. The tail is crossfaded
    // into the head so the loop point is inaudible.
    private static float[] FireAndWorkshop()
    {
        var rng = new System.Random(23);
        int n = Rate * 8, fade = Rate / 2;
        var s = new float[n + fade];
        float brown = 0f;
        for (int i = 0; i < s.Length; i++)
        {
            brown = Mathf.Clamp(brown + (float)(rng.NextDouble() * 2 - 1) * 0.02f, -1f, 1f) * 0.998f;
            s[i] = brown * 0.5f;
        }
        int crackles = 8 * 7;
        for (int c = 0; c < crackles; c++)
        {
            int start = rng.Next(0, s.Length - Rate / 50);
            float amp = (float)(0.15 + rng.NextDouble() * 0.45);
            float len = Rate * (0.002f + (float)rng.NextDouble() * 0.006f);
            for (int i = 0; i < len * 4 && start + i < s.Length; i++)
                s[start + i] += (float)(rng.NextDouble() * 2 - 1) * amp * Mathf.Exp(-i / len);
        }
        var loop = new float[n];
        for (int i = 0; i < n; i++)
        {
            loop[i] = s[i];
            if (i < fade)
            {
                float k = i / (float)fade;
                loop[i] = s[i] * k + s[n + i] * (1f - k);
            }
        }
        return loop;
    }

    private static void Write(string path, float[] samples, bool loop = false)
    {
        // Normalise to -1 dBFS so placeholders sit at a consistent level; volume is set per source.
        float peak = 1e-6f;
        foreach (float v in samples) peak = Mathf.Max(peak, Mathf.Abs(v));
        float gain = 0.89f / peak;

        using (var fs = new FileStream(path, FileMode.Create))
        using (var w = new BinaryWriter(fs))
        {
            int dataBytes = samples.Length * 2;
            w.Write(new[] { 'R', 'I', 'F', 'F' }); w.Write(36 + dataBytes);
            w.Write(new[] { 'W', 'A', 'V', 'E' });
            w.Write(new[] { 'f', 'm', 't', ' ' }); w.Write(16); w.Write((short)1); w.Write((short)1);
            w.Write(Rate); w.Write(Rate * 2); w.Write((short)2); w.Write((short)16);
            w.Write(new[] { 'd', 'a', 't', 'a' }); w.Write(dataBytes);
            foreach (float v in samples)
                w.Write((short)Mathf.Clamp(Mathf.RoundToInt(v * gain * short.MaxValue), short.MinValue, short.MaxValue));
        }

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (AudioImporter)AssetImporter.GetAtPath(path);
        importer.forceToMono = true;
        var settings = importer.defaultSampleSettings;
        // Short effects decompress on load for zero-latency taps; the loop streams.
        settings.loadType = loop ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
        settings.compressionFormat = AudioCompressionFormat.Vorbis;
        settings.quality = 0.6f;
        importer.defaultSampleSettings = settings;
        importer.SaveAndReimport();
    }
}
