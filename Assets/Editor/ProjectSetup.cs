using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Creates Assets/Scenes/Main.unity on first open. The game builds itself at runtime (see Game.cs).</summary>
[InitializeOnLoad]
public static class ProjectSetup
{
    const string ScenePath = "Assets/Scenes/Main.unity";

    static ProjectSetup()
    {
        EditorApplication.delayCall += () =>
        {
            if (!System.IO.File.Exists(ScenePath)) CreateScene();
        };
    }

    [MenuItem("KBTU/Create Main Scene")]
    public static void CreateScene()
    {
        System.IO.Directory.CreateDirectory("Assets/Scenes");
        var s = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        EditorSceneManager.SaveScene(s, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        Debug.Log("KBTU: Main scene created. Press Play.");
    }

    [MenuItem("KBTU/Validate Maps")]
    public static void ValidateMaps() { Debug.Log("Maps: " + Areas.Validate()); }

    [MenuItem("KBTU/Build Windows EXE")]
    public static void BuildExe()
    {
        if (!System.IO.File.Exists(ScenePath)) CreateScene();
        BuildPipeline.BuildPlayer(new[] { ScenePath }, "Builds/KBTU-Outbreak.exe", BuildTarget.StandaloneWindows64, BuildOptions.None);
    }
}
