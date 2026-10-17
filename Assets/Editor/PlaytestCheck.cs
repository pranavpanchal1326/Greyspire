using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Checks that the play-testing logger writes what it should, with nobody at the keyboard:
//   Unity -projectPath . -executeMethod PlaytestCheck.Run
// A script (not a person) starts a session called "tool-check", clears floor 2 and stops.
// The log is copied to Logs/playtest_toolcheck.csv.
[InitializeOnLoad]
public static class PlaytestCheck
{
    const string Key = "PlaytestCheckStage";
    const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
    static float since = -1f;
    static int step;

    static PlaytestCheck()
    {
        if (SessionState.GetInt(Key, 0) > 0) EditorApplication.update += Tick;
    }

    public static void Run()
    {
        Save.Wipe();
        EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        SessionState.SetInt(Key, 1);
        EditorApplication.update += Tick;
        EditorApplication.EnterPlaymode();
    }

    static void Smoke(string method) => typeof(SmokeTest).GetMethod(method, Any).Invoke(null, null);

    static void Tick()
    {
        if (!EditorApplication.isPlaying || Game.I == null) return;
        float now = Time.realtimeSinceStartup;
        if (since < 0f)
        {
            Application.runInBackground = true;     // keep playing when the editor window is not in front
            since = now;
        }
        float[] waits = { 2.5f, 2.5f, 1f, 4f, 3f, 1f };
        if (now - since < waits[Mathf.Min(step, waits.Length - 1)]) return;
        since = now;
        try
        {
            if (step == 0)
            {
                PlaytestLog.Begin("tool-check");
                JourneyStore.SetInt("towerFloor", 1);
                JourneyStore.SetInt("storyFloor", 1);
                typeof(Game).GetMethod("StartRun", Any).Invoke(Game.I, null);
            }
            else if (step == 1 || step == 2) Smoke("Settle");
            else if (step == 3) Game.I.Player.ReceiveHit(new DamageRequest(1f, Game.I.Player.transform.position, 0f));             // one hit, to see it counted
            else if (step == 4) Smoke("KillAll");
            else if (step == 5)
            {
                PlaytestLog log = PlaytestLog.Active;
                log.SendMessage("OnApplicationQuit");
                File.Copy(log.FilePath, "Logs/playtest_toolcheck.csv", true);
                Debug.Log("PLAYTEST log written: " + log.FilePath);
                Debug.Log("PLAYTEST DONE");
                SessionState.SetInt(Key, 0);
                EditorApplication.Exit(0);
            }
            step++;
        }
        catch (Exception e)
        {
            Debug.LogError("PLAYTEST FAILED at step " + step + ": " + e);
            SessionState.SetInt(Key, 0);
            EditorApplication.Exit(1);
        }
    }
}
