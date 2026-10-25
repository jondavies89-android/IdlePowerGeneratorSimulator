using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Entry points for menu builds and CLI/CI builds (-executeMethod BuildScript.BuildWebGL).
// Player settings are re-applied on every build so CI output never depends on
// whatever the Editor last saved to ProjectSettings.asset.
public static class BuildScript
{
    // Pixel 7-class logical resolution; narrow enough to surface layout problems
    // before the 380dp Android minimum does.
    private const int PortraitWidth  = 412;
    private const int PortraitHeight = 915;

    private const string DefaultWebGLPath   = "Builds/WebGL";
    private const string DefaultAndroidPath = "Builds/Android/IdlePowerGen.apk";

    [MenuItem("IdlePowerGen/Build/Apply Portrait Player Settings")]
    public static void ApplyPortraitSettings()
    {
        PlayerSettings.defaultWebScreenWidth  = PortraitWidth;
        PlayerSettings.defaultWebScreenHeight = PortraitHeight;

        // GitHub Pages can't send Content-Encoding headers for pre-compressed files,
        // so the loader must be able to decompress gzip itself.
        PlayerSettings.WebGL.compressionFormat     = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true;

        PlayerSettings.defaultInterfaceOrientation      = UIOrientation.Portrait;
        PlayerSettings.allowedAutorotateToPortrait           = true;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft      = false;
        PlayerSettings.allowedAutorotateToLandscapeRight     = false;

        AssetDatabase.SaveAssets();
    }

    // Every era's ScriptableObjects, then the scene, in order (the scene wires the assets).
    [MenuItem("IdlePowerGen/Build/All Content")]
    public static void BuildContent()
    {
        Era2AssetBuilder.Build();
        Era3AssetBuilder.Build();
        Era4AssetBuilder.Build();
        Era5AssetBuilder.Build();
        Era1SceneBuilder.Build();
    }

    [MenuItem("IdlePowerGen/Build/Web (WebGL)")]
    public static void BuildWebGL() =>
        Build(BuildTarget.WebGL, GetArg("-customBuildPath") ?? DefaultWebGLPath);

    [MenuItem("IdlePowerGen/Build/Android (APK)")]
    public static void BuildAndroid() =>
        Build(BuildTarget.Android, GetArg("-customBuildPath") ?? DefaultAndroidPath);

    private static void Build(BuildTarget target, string outputPath)
    {
        ApplyPortraitSettings();

        string[] scenes = EditorBuildSettings.scenes
            .Where(s => s.enabled)
            .Select(s => s.path)
            .ToArray();

        if (scenes.Length == 0)
            Fail("No enabled scenes in Build Profiles. Add Assets/Scenes/Era1_Workshop.unity to the scene list.");

        var options = new BuildPlayerOptions
        {
            scenes           = scenes,
            locationPathName = outputPath,
            target           = target,
            options          = BuildOptions.None,
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;

        if (summary.result != BuildResult.Succeeded)
            Fail($"{target} build {summary.result} with {summary.totalErrors} error(s).");

        Debug.Log($"{target} build succeeded: {outputPath} ({summary.totalSize / (1024 * 1024)} MB)");
    }

    private static string GetArg(string name)
    {
        string[] args = Environment.GetCommandLineArgs();
        int i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    // In batch mode a thrown exception doesn't set a non-zero exit code; EditorApplication.Exit does.
    private static void Fail(string message)
    {
        Debug.LogError(message);
        if (Application.isBatchMode) EditorApplication.Exit(1);
        throw new BuildFailedException(message);
    }
}
