using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// Phase nine, the first-player learning path: a fresh journey from Brushwick to the floor-5 Guardian,
// stage by stage, with the real game loop running. At each stage it reads what a newcomer is told
// (the objective, the town's next step, the tutorial tip, the floor line, the journal) and checks it
// names the next useful action. Writes a readable transcript (Logs/learning/path.md) and captures.
[InitializeOnLoad]
public static class LearningPathTest
{
    const string Session = "GreyspireLearningPathTest";
    const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    class Step { public float wait; public Action action; }
    static List<Step> steps;
    static int at, passed;
    static double next, deadline;
    static readonly List<string> errors = new List<string>();
    static readonly StringBuilder path = new StringBuilder("# First-player learning path\n\nWhat a newcomer is told at each stage of the first chapter, read from the running game.\n\n");
    static Game G => Game.I;
    static PlayerController P => G.Player;
    static object Get(object host, string name) => host.GetType().GetField(name, Any).GetValue(host);
    static void Set(object host, string name, object value) => (host.GetType().GetField(name, Any) ?? host.GetType().GetField("<" + name + ">k__BackingField", Any)).SetValue(host, value);
    static object Call(object host, string name, params object[] args) => host.GetType().GetMethod(name, Any).Invoke(host, args);
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); passed++; Debug.Log("LEARN PASS " + message); }
    static void Do(float delay, Action action) => steps.Add(new Step { wait = delay, action = action });
    static void OnLog(string message, string stack, LogType type) { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message); }

    static Keyboard keyboard;
    static void Keys(params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
    static string Objective() => (string)Call(G, "Objective");
    static string Next() => G.NextStep();

    static void Record(string stage, string shot)
    {
        path.AppendLine("## " + stage + "\n");
        path.AppendLine("- **Objective:** " + Objective());
        path.AppendLine("- **Next step:** " + (Next() ?? "(none)"));
        int hint = (int)Get(G, "hintStep");
        if (hint >= 0) path.AppendLine("- **Tip on screen:** " + (ControlGuide.Tutorial(hint) ?? ((string[])typeof(Game).GetField("Hints", Any).GetValue(null))[hint]));
        foreach (var e in G.Journal()) path.AppendLine("- Journal " + e.head + ": " + e.line);
        if (shot != null) { path.AppendLine("\n![" + stage + "](" + shot + ".png)\n"); ScreenCapture.CaptureScreenshot("Logs/learning/" + shot + ".png"); }
        path.AppendLine();
    }

    static void Town()
    {
        foreach (var e in Enemy.All.ToArray()) UnityEngine.Object.DestroyImmediate(e.gameObject);
        ((System.Collections.IList)Get(G, "timers")).Clear();
        Call(G, "BuildHub"); Call(G, "SnapResidents");
        Call(G, "SetState", Game.GameState.Hub);
        Set(G, "hubPanel", 0); Set(G, "fade", 0f); Set(G, "fadeDir", 0f); Set(G, "hintStep", -1);
        P.Revive(new Vector3(0f, 0.1f, -4f), true);
        P.transform.rotation = Quaternion.identity;
        G.Cam.Face(0f); G.Cam.Snap();
    }

    static void Script()
    {
        Directory.CreateDirectory("Logs/learning");
        Application.runInBackground = true;
        keyboard = InputSystem.AddDevice<Keyboard>();
        // A synthetic session in the playtest format, so the Q01 report is proven end to end.
        PlaytestLog.Begin("synthetic-learning-path", Path.GetFullPath("Logs/learning/sessions"));
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        Save.Wipe(); Save.SeenIntro = true; Save.Flush();

        // 1. A new journey, first time in Brushwick.
        Do(0.5f, () => Town());
        Do(1.0f, () =>
        {
            Check(Objective().Contains("Gloom Tower") && Objective().Contains("north"), "a new journey is told where the tower is (" + Objective() + ")");
            Check(Next() == null, "the first visit asks only for the tower; nothing else competes");
            Record("1. A new journey in Brushwick", "1-town-fresh");
        });

        // 2. The first climb: the tutorial walks the controls, the floor says what is left.
        Do(0.5f, () => Call(G, "StartClimb"));
        // The first climb opens with its story; a player skips it (X), or confirms a title card.
        for (int i = 0; i < 8; i++)
        {
            Do(0.6f, () => { if (G.State == Game.GameState.Story) Keys(Key.X); });
            Do(0.15f, () => Keys());
            Do(0.3f, () => { if (G.State == Game.GameState.Story) Keys(Key.Enter); });
            Do(0.15f, () => Keys());
        }
        Do(1.0f, () =>
        {
            Check(G.State == Game.GameState.Playing && G.Floor == 1, "the tower gate starts floor 1 (" + G.State + ", floor " + G.Floor + ")");
            int hint = (int)Get(G, "hintStep");
            Check(hint == 0 && ControlGuide.Tutorial(0).Contains(ControlGuide.Key(ControlAction.Move)), "the first climb opens with the movement tip (" + ControlGuide.Tutorial(0) + ")");
            Check(Objective().StartsWith("Floor 1") && Objective().Contains("stairs"), "inside the tower the objective is the floor's own task (" + Objective() + ")");
            Record("2. The first climb", "2-floor1-tip");
        });

        // 3. Back in town after floor 1, a level and three stat points to spend.
        Do(0.3f, () => { Save.BestFloor = 1; Save.TowerFloor = 1; Save.Level = 2; Save.StatPoints = 3; Save.Flush(); Town(); });
        Do(1.0f, () =>
        {
            Check(Next() != null && Next().Contains("stat point") && Next().Contains(ControlGuide.Key(ControlAction.Status)), "back from floor 1 the town says to spend stat points, with the key (" + Next() + ")");
            Check(Game.Compass(new Vector2(0f, 10f)) == "north" && Game.Compass(new Vector2(7f, -5f)) == "south-east" && Game.Compass(new Vector2(-4.4f, 7.5f)) == "north-west", "directions are read from where things stand");
            Check(G.Journal().Any(e => e.head == "NEXT" && e.line == Next()), "the journal carries the same next step");
            Record("3. Back from floor 1 with a level", "3-town-points");
        });

        // 4. Points spent: the Field desk can teach a technique.
        Do(0.3f, () => { Save.StatPoints = 0; Save.Flush(); });
        Do(0.5f, () =>
        {
            Check(Next() != null && Next().Contains("Field desk") && Next().Contains("technique"), "with points spent, the Field desk's technique is next (" + Next() + ")");
            Record("4. Stat points spent", "4-town-mastery");
        });

        // 5. A technique learned: the Guild has work.
        Do(0.3f, () => { JourneyStore.SetString("pilot:mastery:branch", "duelist"); JourneyStore.SetInt("pilot:mastery:rank", 1); });
        Do(0.5f, () =>
        {
            Check(Next() != null && Next().Contains("Guild") && Next().Contains("commission"), "with a technique learned, the Guild's commissions are next (" + Next() + ")");
            Record("5. A technique learned", "5-town-guild");
        });

        // 6. A commission taken: nothing else in town; the objective climbs on.
        Do(0.3f, () => { Save.Mission = Array.FindIndex(Enumerable.Range(0, 20).Select(Game.MissionId).ToArray(), id => id == "first-steps"); Save.Flush(); });
        Do(0.5f, () =>
        {
            Check(Next() == null && Objective().Contains("Floor 2"), "with work in hand the town has nothing to add and the objective climbs on (" + Objective() + ")");
            Check(G.Journal().Any(e => e.head == "COMMISSION" && e.line.Contains("First Steps")), "the journal tracks the commission");
            Record("6. A commission taken", "6-town-commission");
        });

        // 7. Floor 3 cleared with the courier helped: the second technique, then the report.
        Do(0.3f, () =>
        {
            Save.BestFloor = 3; Save.TowerFloor = 3;
            JourneyStore.SetString("pilot:event:selected", "courier-tools"); JourneyStore.SetInt("pilot:event:choice", 1);
            JourneyStore.SetInt("consequence:pilot:courier:helped", 1); Save.Flush();
        });
        Do(0.5f, () =>
        {
            Check(Next() != null && Next().Contains("technique"), "after floor 3 the second technique comes first (" + Next() + ")");
            JourneyStore.SetInt("pilot:mastery:rank", 2);
        });
        Do(0.5f, () =>
        {
            Check(Next() != null && Next().Contains("courier"), "then the courier report (" + Next() + ")");
            Record("7. Floor 3 cleared, the courier helped", "7-town-courier");
            JourneyStore.SetInt("pilot:event:callback", 1); JourneyStore.SetInt("consequence:pilot:courier:reported", 1);
        });

        // 8. Rhea sent home from floor 4: Lady Vane's thanks.
        Do(0.5f, () =>
        {
            Save.BestFloor = 4; Save.TowerFloor = 4;
            JourneyStore.SetInt("rescue:rhea:choice", 1);
            JourneyStore.SetInt("consequence:rescue:rhea:safe-return", 1); JourneyStore.SetInt("consequence:rescue:rhea:home", 1); Save.Flush();
        });
        Do(0.5f, () =>
        {
            Check(Next() != null && Next().Contains("Lady Vane") && Next().Contains("of the square"), "with Rhea home, Lady Vane's thanks are next, and where (" + Next() + ")");
            Check(Objective().StartsWith("Report to Nix"), "the finished commission is reported first (" + Objective() + ")");
            Record("8. Rhea sent home from floor 4", "8-town-vane");
        });

        // 9. The floor-5 checkpoint reached and the Gatekeeper still standing.
        Do(0.3f, () =>
        {
            JourneyStore.SetInt("consequence:rescue:rhea:vane-thanks-paid", 1); JourneyStore.SetInt("chapter:tower:05:gatekeeper-checkpoint-v1", 1);
            Save.Mission = -1; Save.Flush();
        });
        Do(0.5f, () =>
        {
            Check(G.Journal().Any(e => e.head == "CHECKPOINT" && e.line.Contains("retry")), "a failed Guardian attempt leaves a checkpoint the journal explains");
            Check(Objective().Contains("Floor 5") && Objective().Contains("guards"), "with the pay collected the objective names the Guardian ahead (" + Objective() + ")");
            Record("9. The Gatekeeper's checkpoint", "9-town-checkpoint");
        });

        Do(0.5f, () =>
        {
            File.WriteAllText("Logs/learning/path.md", path.ToString());
            var log = PlaytestLog.Active;
            Check(log != null && log.Failure == null && File.Exists(log.FilePath), "the synthetic session was recorded in the playtest format");
            log.End();
            string recorded = File.ReadAllText(log.FilePath);
            Check(recorded.Contains("\"objective\"") && recorded.Contains("\"next_step\"") && recorded.Contains("\"floor_start\""), "it holds objective, next-step and floor events");
            foreach (var shot in new[] { "1-town-fresh", "2-floor1-tip", "3-town-points", "5-town-guild", "8-town-vane" })
                Check(File.Exists("Logs/learning/" + shot + ".png"), "capture " + shot + " written");
            Check(errors.Count == 0, "the learning path logged no unexpected errors" + (errors.Count > 0 ? ": " + errors[0] : ""));
            Save.Wipe();
            Check(JourneyStore.Error == null, "the journey store is healthy afterwards");
        });
    }

    static LearningPathTest() { if (SessionState.GetBool(Session, false)) { EditorApplication.update += Tick; Application.logMessageReceived += OnLog; } }

    public static void Run()
    {
        Save.Wipe(); Save.SeenIntro = true; Save.Flush();
        EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        SessionState.SetBool(Session, true);
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
        Application.logMessageReceived -= OnLog; Application.logMessageReceived += OnLog;
        EditorApplication.EnterPlaymode();
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying || G == null || G.Player == null) return;
        try
        {
            if (next == 0) { next = EditorApplication.timeSinceStartup + 3; return; }
            if (EditorApplication.timeSinceStartup < next) return;
            if (steps == null) { steps = new List<Step>(); Script(); deadline = EditorApplication.timeSinceStartup + 180; next = EditorApplication.timeSinceStartup + steps[0].wait; return; }
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("learning path deadline exceeded");
            if (at < steps.Count) { steps[at++].action(); if (at < steps.Count) { next = EditorApplication.timeSinceStartup + steps[at].wait; return; } }
            Debug.Log("LEARN DONE " + passed + " checks passed");
            Finish(0);
        }
        catch (Exception e)
        {
            Directory.CreateDirectory("Logs/learning");
            File.WriteAllText("Logs/learning/path.md", path.ToString());
            Debug.LogError("LEARN FAILED step=" + at + " " + (e.InnerException ?? e));
            Finish(1);
        }
    }

    static void Finish(int code)
    {
        SessionState.SetBool(Session, false);
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= OnLog;
        EditorApplication.Exit(code);
    }
}
