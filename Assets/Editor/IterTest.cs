using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
#endif

// Checks the changes made after the first play-test, and takes the "after" pictures:
//   Unity -projectPath . -executeMethod IterTest.Run       (pictures in Logs/iter)
// 1 the Sparks tip and the forge note   2 a commission's "NEXT" line in town, on the board and
// in the Wilds   3 X ends a whole conversation   4 monsters on floor 16 carry the new health.
[InitializeOnLoad]
public static class IterTest
{
    const string Key = "IterTestStage";
    const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;

    class Step { public float wait; public Action act; }
    static List<Step> steps;
    static int at;
    static float since;

    static IterTest()
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

    static void Tick()
    {
        if (!EditorApplication.isPlaying || Game.I == null) return;
        float now = Time.realtimeSinceStartup;
        if (steps == null)
        {
            Application.runInBackground = true;     // keep playing when the editor window is not in front
            steps = new List<Step>();
            Script();
            at = 0;
            since = now;
        }
        try
        {
            Game.I.Player.hp = Game.I.Player.maxHp;
            Step s = steps[at];
            if (now - since < s.wait) return;
            s.act();
            at++;
            since = now;
            if (at < steps.Count) return;
            Debug.Log("ITER DONE");
            SessionState.SetInt(Key, 0);
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            Debug.LogError("ITER FAILED at step " + at + ": " + e);
            SessionState.SetInt(Key, 0);
            EditorApplication.Exit(1);
        }
    }

    static void Do(float wait, Action act) => steps.Add(new Step { wait = wait, act = act });
    static void Smoke(string method) => typeof(SmokeTest).GetMethod(method, Any).Invoke(null, null);
    static object Call(string method, params object[] args) => typeof(Game).GetMethod(method, Any).Invoke(Game.I, args);
    static object Field(string name) => typeof(Game).GetField(name, Any).GetValue(Game.I);

    static void Shot(string name)
    {
        Directory.CreateDirectory("Logs/iter");
        ScreenCapture.CaptureScreenshot("Logs/iter/" + name + ".png", 2);
        Debug.Log("ITER captured " + name);
    }

    static void Script()
    {
        // ---- Sparks: the tip, then the forge note in town
        Do(2.5f, () => { Save.BestFloor = 1; Call("EnterHub"); });
        Do(6f, () => Smoke("Settle"));
        Do(1f, () => { Smoke("Settle"); Save.Sparks = 12; });
        Do(1.2f, () => { Shot("a1_sparks_tip"); Debug.Log("ITER sparks tip: title=" + Field("systemTitle") + " seen=" + JourneyStore.GetInt("tipSparks")); });
        Do(4.5f, () => Save.Sparks = 30);
        Do(1.2f, () => { Shot("a2_forge_note"); Debug.Log("ITER forge tip: title=" + Field("systemTitle") + " canForge=" + typeof(Game).GetProperty("CanForge", Any).GetValue(Game.I)); });

        // ---- A commission: accepted, on the board, on the tracker in town and in the Wilds
        Do(4.5f, () => { Call("OpenBoard", "Nix of the Guild"); });
        Do(1f, () => Call("AcceptMission", 0));
        Do(1f, () => { Shot("b1_board_in_hand"); Debug.Log("ITER accepted: title=" + Field("systemTitle") + " body=" + Field("systemBody")); });
        Do(0.5f, () => Call("OpenHubPanel", 0));
        Do(1.2f, () => { Shot("b2_tracker_town"); Next("town"); });
        Do(3.5f, () => Call("EnterWilds"));
        Do(6f, () => Smoke("Settle"));
        Do(1.5f, () => { Shot("b3_tracker_wilds"); Next("wilds"); });

        // ---- A conversation, and X
        Do(1f, () => typeof(SmokeTest).GetMethod("Say", Any).Invoke(null, new object[] { Story.Rescues[0].town }));
        Do(2.6f, () => { Shot("c1_talk_hint"); Debug.Log("ITER talking: lines=" + ((Line[])Field("dialog")).Length + " on line " + Field("dialogIndex")); });
        Do(0.8f, PressX);
        Do(0.6f, () => Debug.Log("ITER after X: conversation " + (Field("dialog") == null ? "ended" : "STILL OPEN")));

        // ---- Balance: what a floor-16 monster carries now
        Do(0.5f, () =>
        {
            JourneyStore.SetInt("towerFloor", 15);
            JourneyStore.SetInt("storyFloor", 15);
            Call("StartRun");
        });
        Do(2.5f, () => Smoke("Settle"));
        Do(1f, () =>
        {
            Smoke("Settle");
            var seen = new HashSet<string>();
            foreach (Enemy e in Enemy.All)
                if (seen.Add(e.kind + (e.elite ? " elite" : "")))
                    Debug.Log("ITER floor " + Game.I.Floor + " " + e.kind + (e.elite ? " elite" : "") + " maxHp=" + e.maxHp.ToString("0.0"));
            Shot("d1_floor16");
        });
        Do(2f, () => { });
    }

    static void Next(string where)
    {
        object mission = typeof(Game).GetProperty("ActiveMission", Any).GetValue(Game.I);
        Debug.Log("ITER next (" + where + "): " + Call("MissionNext", mission));
    }

    // A real key event, queued the way the keyboard would send it.
    static void PressX()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard k = Keyboard.current ?? InputSystem.AddDevice<Keyboard>();
        InputSystem.QueueStateEvent(k, new KeyboardState(UnityEngine.InputSystem.Key.X));
        InputSystem.QueueStateEvent(k, new KeyboardState(), InputState.currentTime + 0.05);
#endif
    }
}
