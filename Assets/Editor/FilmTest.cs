using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Films a short play-through for the gameplay video, from the command line:
//   Tools\film.ps1        (Unity -projectPath . -executeMethod FilmTest.Run, then encodes the frames)
// The game runs at a fixed 30 frames a second of game time and every frame is saved to
// Logs/film, so the film is smooth however long a frame takes to write. The hero is steered
// by the script: the title, Brushwick, a tower floor with Mop at heel, the F3 NavMesh view,
// a fight, the rescue of Rhea Vane with its choice, and the first Guardian.
[InitializeOnLoad]
public static class FilmTest
{
    const string Key = "FilmTestStage";
    const int Fps = 30;
    const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;

    class Step { public int frames; public Action act; public string label; }

    static List<Step> steps;
    static int at, since, frame, lastFrame = -1;
    static bool brawl;
    static Vector3 drive;

    static PlayerController P => Game.I.Player;

    static FilmTest()
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
        if (Time.frameCount == lastFrame) return;       // once per game frame
        lastFrame = Time.frameCount;
        if (steps == null)
        {
            Application.runInBackground = true;     // keep playing when the editor window is not in front
            Time.captureFramerate = Fps;
            Directory.CreateDirectory("Logs/film");
            steps = new List<Step>();
            Script();
            at = 0;
            since = 0;
        }
        try
        {
            ScreenCapture.CaptureScreenshot("Logs/film/f_" + frame.ToString("00000") + ".png", 2);
            frame++;

            P.hp = P.maxHp;
            typeof(PlayerController).GetField("invuln", Any).SetValue(P, 1f);
            if (drive != Vector3.zero)
            {
                typeof(PlayerController).GetField("planar", Any).SetValue(P, drive);
                P.transform.rotation = Quaternion.LookRotation(drive);
            }
            if (brawl && Game.I.State == Game.GameState.Playing && Time.frameCount % 20 == 0)
            {
                int which = Time.frameCount / 20 % 6;
                Smoke("ResetMove");
                if (which == 1) Move("StartHeavy", 0.8f);
                else if (which == 2) Move("StartKick");
                else if (which == 3) Move("StartStab", Vector3.zero);
                else if (which == 4) Move("StartBash");
                else if (which == 5) Move("StartThrow");
                else Move("StartAttack", Vector3.zero);
            }

            Step s = steps[at];
            if (++since < s.frames) return;
            Debug.Log("FILM " + (frame / (float)Fps).ToString("0.0") + "s  " + s.label + "  state=" + Game.I.State);
            s.act();
            at++;
            since = 0;
            if (at < steps.Count) return;

            Debug.Log("FILM DONE frames=" + frame);
            SessionState.SetInt(Key, 0);
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            Debug.LogError("FILM FAILED at step " + at + ": " + e);
            SessionState.SetInt(Key, 0);
            EditorApplication.Exit(1);
        }
    }

    static void Do(float seconds, string label, Action act) =>
        steps.Add(new Step { frames = Mathf.Max(1, Mathf.RoundToInt(seconds * Fps)), act = act, label = label });

    static void Script()
    {
        // ---- the title, then Brushwick
        Do(3f, "title", () => { P.SetHero(0); Call("EnterHub"); });
        Do(6.5f, "town arrival", () => { Smoke("Settle"); drive = P.transform.forward * 3.4f; });
        Do(3.5f, "town walk", () =>
        {
            drive = Vector3.zero;
            Save.Pet = true;
            Companion.Ensure();
            Climb(1);
        });

        // ---- floor 2: the path, Mop at heel, the NavMesh view, a fight
        Do(2.2f, "floor 2", () => Smoke("Settle"));
        Do(0.6f, "floor 2 settle", () => { Smoke("Settle"); drive = Vector3.forward * 5f; View(0f, 16f); });
        Do(3.6f, "run", () => NavField.Show = true);
        Do(3.2f, "run with navmesh", () => { NavField.Show = false; drive = Vector3.zero; Engage(3.2f); });
        Do(0.4f, "engage", () => brawl = true);
        Do(8f, "fight", () => { brawl = false; Smoke("KillAll"); });

        // ---- floor 4: the rescue of Rhea Vane, branch B
        Do(1.6f, "floor 2 done", () => Climb(3));
        Do(2.2f, "floor 4", () => Smoke("Settle"));
        Do(0.6f, "floor 4 settle", () => { Smoke("Settle"); Approach(); });
        Do(2.2f, "the statue", () => Smoke("KillAll"));
        Do(3.2f, "floor cleared", () => Smoke("Settle"));
        Do(0.6f, "paint her back", () => typeof(SmokeTest).GetMethod("UseSpot", Any).Invoke(null, new object[] { "A drained Painter" }));
        Do(9.4f, "rescue + first line", () => Line(1));
        Do(2.4f, "second line", () => Smoke("SkipDialog"));
        Do(2.4f, "choice", () => Game.I.Answer(1));
        Do(6.4f, "tell + question", () => Line(1));
        Do(3.4f, "what she saw", () => Line(2));
        Do(4f, "the warning", () => Smoke("SkipDialog"));
        Do(5.6f, "depart", () => { });
        Do(2.6f, "rescued", () => Climb(4));

        // ---- floor 5: the first Guardian
        Do(2.4f, "guardian floor", () => Smoke("Settle"));
        Do(1.4f, "guardian banner", () =>
        {
            Smoke("Settle");
            P.Teleport(new Vector3(0f, 0.1f, 17f));
            View(0f, 14f);
        });
        Do(1.6f, "guardian", () => brawl = true);
        Do(7f, "guardian fight", () => brawl = false);
        Do(0.6f, "end", () => { });
    }

    // ------------------------------------------------------------ helpers

    static void Smoke(string method) => typeof(SmokeTest).GetMethod(method, Any).Invoke(null, null);

    static void Call(string method) => typeof(Game).GetMethod(method, Any).Invoke(Game.I, null);

    static void Move(string method, params object[] args)
    {
        MethodInfo m = typeof(PlayerController).GetMethod(method, Any);
        if (m != null) m.Invoke(P, m.GetParameters().Length == args.Length ? args : new object[m.GetParameters().Length]);
    }

    static void Climb(int towerFloor)
    {
        JourneyStore.SetInt("towerFloor", towerFloor);
        JourneyStore.SetInt("storyFloor", towerFloor);
        Call("StartRun");
    }

    static void View(float yaw, float pitch)
    {
        ThirdPersonCamera cam = Game.I.Cam;
        typeof(ThirdPersonCamera).GetField("yaw", Any).SetValue(cam, yaw);
        typeof(ThirdPersonCamera).GetField("pitch", Any).SetValue(cam, pitch);
        cam.Snap();
    }

    // Puts the hero in front of the nearest monster.
    static void Engage(float distance)
    {
        Enemy best = null;
        foreach (Enemy e in Enemy.All)
            if (best == null || (e.transform.position - P.transform.position).magnitude < (best.transform.position - P.transform.position).magnitude) best = e;
        if (best == null) return;
        Vector3 from = P.transform.position - best.transform.position;
        from.y = 0f;
        from = from.sqrMagnitude > 0.01f ? from.normalized : Vector3.back;
        P.Teleport(best.transform.position + from * distance + Vector3.up * 0.05f);
        P.transform.rotation = Quaternion.LookRotation(-from);
        View(Mathf.Atan2(-from.x, -from.z) * Mathf.Rad2Deg + 20f, 16f);
    }

    static void Approach()
    {
        Transform s = GameObject.Find("Drained Painter").transform;
        Vector3 front = s.forward; front.y = 0f; front.Normalize();
        P.Teleport(s.position + front * 2.3f + Vector3.up * 0.05f);
        P.transform.rotation = Quaternion.LookRotation(-front);
        View(Mathf.Atan2(-front.x, -front.z) * Mathf.Rad2Deg + 28f, 10f);
    }

    // Moves the conversation on to line `index`, typed out and framed as the game would.
    static void Line(int index)
    {
        Game g = Game.I;
        var lines = (Line[])typeof(Game).GetField("dialog", Any).GetValue(g);
        if (lines == null || index >= lines.Length) return;
        typeof(Game).GetField("dialogIndex", Any).SetValue(g, index);
        typeof(Game).GetField("dialogChars", Any).SetValue(g, 0f);
        typeof(Game).GetField("dialogT", Any).SetValue(g, 0f);
        if (!string.IsNullOrEmpty(lines[index].shot))
            typeof(Game).GetMethod("ApplyShot", Any).Invoke(g, new object[] { lines[index].shot });
    }
}
