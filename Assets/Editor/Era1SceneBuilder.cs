using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;

// Builds Assets/Scenes/Era1_Workshop.unity from scratch. The scene is generated rather than
// hand-edited so it can be rebuilt in batch mode and reviewed as code; re-running it
// replaces the scene file, so make lasting changes here, not in the Editor.
public static class Era1SceneBuilder
{
    public const string ScenePath = "Assets/Scenes/Era1_Workshop.unity";

    [MenuItem("IdlePowerGen/Build/Era 1 Scene")]
    public static void Build()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        CreateCamera();
        CreateManagers();
        CreateEventSystem();

        EditorSceneManager.SaveScene(scene, ScenePath);
        RegisterInBuildSettings();
        Debug.Log($"[Era1SceneBuilder] Wrote {ScenePath}");
    }

    private static void CreateCamera()
    {
        var go = new GameObject("Main Camera") { tag = "MainCamera" };
        var cam = go.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.orthographic = true;
    }

    // One root object for every manager: DontDestroyOnLoad on any of them keeps them all.
    private static void CreateManagers()
    {
        var go = new GameObject("_Managers");
        go.AddComponent<IdlePowerGen.Managers.GameManager>();
        go.AddComponent<IdlePowerGen.Simulation.SimulationTick>();

        var clock = go.AddComponent<IdlePowerGen.Utils.GameClock>();
        var so = new SerializedObject(clock);
        // 1 real second = 1 game minute (todo §3.1).
        so.FindProperty("_gameTimeScale").floatValue = 60f;
        so.ApplyModifiedPropertiesWithoutUndo();

        go.AddComponent<IdlePowerGen.Save.SaveManager>();
        go.AddComponent<IdlePowerGen.Managers.EraManager>();
        go.AddComponent<IdlePowerGen.Managers.UpgradeEffectApplier>();
    }

    private static void CreateEventSystem()
    {
        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<StandaloneInputModule>();
    }

    private static void RegisterInBuildSettings()
    {
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
    }
}
