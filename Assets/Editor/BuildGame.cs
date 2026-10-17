using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

// Builds the game people actually run: Builds/Greyspire/Greyspire.exe.
//   Unity -batchmode -quit -projectPath . -executeMethod BuildGame.Windows
// or the menu item. Registers the one scene, sets the icon and the window
// defaults, and reports what the build weighs.
public static class BuildGame
{
    const string Scene = "Assets/Scenes/Main.unity";
    const string Output = "Builds/Greyspire/Greyspire.exe";

    [MenuItem("Greyspire/Build Windows Game")]
    public static void Windows()
    {
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(Scene, true) };
        var icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Icon.png");
        if (icon != null) PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
        PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
        PlayerSettings.resizableWindow = true;
        PlayerSettings.runInBackground = false;

        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { Scene },
            locationPathName = Output,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None,
        });
        var s = report.summary;
        Debug.Log("BUILD " + s.result + "  size=" + (s.totalSize / 1048576) + " MB  errors=" + s.totalErrors + "  warnings=" + s.totalWarnings + "  -> " + Output);
        if (Application.isBatchMode && s.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) EditorApplication.Exit(1);
    }
}
