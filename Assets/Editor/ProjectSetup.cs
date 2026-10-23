using System.IO;
using UnityEditor;
using UnityEngine;

// One-off project configuration that the todo lists as manual Editor steps.
// Kept as code so a fresh clone (or a batch-mode run) reaches the same state.
public static class ProjectSetup
{
    private const string TmpEssentialsPackage =
        "Packages/com.unity.ugui/Package Resources/TMP Essential Resources.unitypackage";
    private const string TmpSettingsAsset = "Assets/TextMesh Pro/Resources/TMP Settings.asset";

    [MenuItem("IdlePowerGen/Setup/Apply Project Settings")]
    public static void Apply()
    {
        PlayerSettings.productName = "Idle Power Generator Simulator";

        // Android 7.0 — the floor the todo sets for the £150-device target.
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;

        ImportTmpEssentials();
        AssetDatabase.SaveAssets();
    }

    // TMP's default shaders and settings live in a .unitypackage inside com.unity.ugui;
    // without them every TextMeshPro component logs a missing-settings error.
    private static void ImportTmpEssentials()
    {
        if (File.Exists(TmpSettingsAsset)) return;
        AssetDatabase.ImportPackage(TmpEssentialsPackage, interactive: false);
        AssetDatabase.Refresh();
        Debug.Log("[ProjectSetup] Imported TMP Essential Resources");
    }
}
