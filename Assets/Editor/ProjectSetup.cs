using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Creates Assets/Scenes/Main.unity and validates/exports KBTU assets. The game builds rooms at runtime (see Game.cs).</summary>
[InitializeOnLoad]
public static class ProjectSetup
{
    const string ScenePath = "Assets/Scenes/Main.unity";

    static ProjectSetup()
    {
        EditorApplication.delayCall += () =>
        {
            if (!Application.isBatchMode && !File.Exists(ScenePath)) CreateScene();
        };
    }

    [MenuItem("KBTU/Create Main Scene")]
    public static void CreateScene()
    {
        Directory.CreateDirectory("Assets/Scenes");
        var s = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        var cam = Camera.main;
        if (cam != null)
        {
            cam.orthographic = true;
            cam.orthographicSize = 6.5f;
            cam.backgroundColor = new Color(0.06f, 0.06f, 0.07f);
            cam.clearFlags = CameraClearFlags.SolidColor;
        }
        EditorSceneManager.SaveScene(s, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        Debug.Log("KBTU: SCENE_OK");
    }

    [MenuItem("KBTU/Export Clean Sliced Sprites")]
    public static void ExportSprites()
    {
        int count = Sheets.ExportCleanPngs("Assets/Resources/Sprites");
        AssetDatabase.Refresh();
        Debug.Log("KBTU: EXPORT_OK=" + count);
    }

    [MenuItem("KBTU/Validate Maps")]
    public static void ValidateMaps() { Debug.Log("Maps: " + Areas.Validate()); }

    [MenuItem("KBTU/Build Windows EXE")]
    public static void BuildExe()
    {
        if (!File.Exists(ScenePath)) CreateScene();
        BuildPipeline.BuildPlayer(new[] { ScenePath }, "Builds/KBTU-Outbreak.exe", BuildTarget.StandaloneWindows64, BuildOptions.None);
    }

    public static void VerifyAndSetup()
    {
        CreateScene();
        int count = Sheets.ExportCleanPngs("Assets/Resources/Sprites");
        Debug.Log("KBTU: EXPORT_OK=" + count);
        AssetDatabase.Refresh();
        string val = Areas.Validate();
        Debug.Log("Maps: " + val);
        if (val != "OK")
        {
            Debug.LogError("Map validation failed:\n" + val);
            EditorApplication.Exit(1);
            return;
        }
        Debug.Log("KBTU: VERIFY_OK");
    }
}
