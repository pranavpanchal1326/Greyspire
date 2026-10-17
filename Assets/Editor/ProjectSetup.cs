using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Creates Assets/Scenes/Main.unity the first time the project is opened and
// registers it in Build Settings, so both Play and Build work out of the box.
[InitializeOnLoad]
public static class ProjectSetup
{
    const string ScenePath = "Assets/Scenes/Main.unity";

    static ProjectSetup()
    {
        EditorApplication.delayCall += EnsureScene;
    }

    [MenuItem("Greyspire/Create Main Scene")]
    static void EnsureScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (!File.Exists(ScenePath))
        {
            Directory.CreateDirectory("Assets/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("Greyspire: created " + ScenePath + ". Press Play to start.");
        }
        if (EditorBuildSettings.scenes.Length == 0)
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
    }
}
