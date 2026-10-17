using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Phase nine, packages N01 and N02. N01: both answers to a freed Painter have an immediate and a
// later effect (the floor-4 branch, Rhea). N02: Mop's four orders, run through his real Update.
[InitializeOnLoad]
public static class BranchCompanionTest
{
    const string Session = "GreyspireBranchCompanionTest";
    const BindingFlags Any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static;
    static readonly Vector3 Origin = new Vector3(1000f, 0.1f, 1000f);
    static int passed;
    static double readyAt, deadline, stageEnds;
    static readonly List<string> errors = new List<string>();
    static Game G => Game.I;
    static PlayerController P => G.Player;
    static object Get(object host, string name) => host.GetType().GetField(name, Any).GetValue(host);
    static void Set(object host, string name, object value) => (host.GetType().GetField(name, Any) ?? host.GetType().GetField("<" + name + ">k__BackingField", Any)).SetValue(host, value);
    static object Call(object host, string name, params object[] args) => host.GetType().GetMethod(name, Any).Invoke(host, args);
    static string Num(float v) => v.ToString("0.##", CultureInfo.InvariantCulture);
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); passed++; Debug.Log("BRANCH PASS " + message); }
    static void OnLog(string message, string stack, LogType type) { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message); }
    static void Reload() { JourneyStore.BeforeReplaceForTest = null; JourneyStore.ReloadForTest(); JourneyStore.Ensure(); }

    static void Fresh()
    {
        Save.Wipe(); Save.SeenIntro = true; Save.Level = 6; Save.BestFloor = 4; Save.TowerFloor = 4; Save.Sparks = 100; Save.Pet = true;
        JourneyStore.SetInt("productionSeed", 734912); JourneyStore.SetInt("seedPolicy", 1); Save.Flush(); Upgrades.Reset(); ProductionRandom.ClearRuntimeStreams();
    }

    static GameObject ground;
    static void Stage()
    {
        foreach (var e in Enemy.All.ToArray()) UnityEngine.Object.DestroyImmediate(e.gameObject);
        ((IList)Get(G, "timers")).Clear();
        G.enabled = false; P.enabled = false;
        Call(G, "SetState", Game.GameState.Playing);
        Set(G, "InTown", false); Set(G, "InWilds", false); Set(G, "DefeatPending", false); Set(G, "fade", 0f); Set(G, "fadeDir", 0f);
        Set(G, "hubPanel", 0); Set(G, "showMap", false); Set(G, "introT", 0f); Set(G, "resumeFrame", Time.frameCount - 2);
        P.stats = new PlayerStats();
        P.maxHp = P.hp = 1000f;
        P.Revive(Origin, true);
        P.transform.rotation = Quaternion.identity;
        Set(P, "LockedTarget", null);
        if (ground == null)
        {
            ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Branch test ground";
            ground.transform.position = Origin - Vector3.up * 0.6f;
            ground.transform.localScale = new Vector3(90f, 1f, 90f);
        }
        Companion.Ensure();
        Physics.SyncTransforms();
    }

    static Enemy Foe(Vector3 offset, Enemy.Kind kind = Enemy.Kind.Minion)
    {
        var e = Enemy.Create(kind, Origin + offset, 0);
        e.hp = e.maxHp = 100000f;
        e.aggro = 60f;
        Set(e, "stun", 0f);
        Set(e, "cooldown", 100000f);
        Set(e, "speed", 0f);        // they stand where they are put: the test is about Mop
        Physics.SyncTransforms();
        return e;
    }

    static Companion Mop => Companion.Instance;

    // ------------------------------------------------------------ N01

    static void Branch()
    {
        // Go home, now: her last draught.
        Fresh(); Stage();
        P.maxHp = 6f; P.hp = 2f;
        Call(G, "RescueAftermath", 0, 0, Origin);
        Check(P.hp == 2f + Game.DraughtHearts && Enemy.All.Count == 0, "GO HOME, at once: Rhea presses her last draught on the hero (+" + Game.DraughtHearts + " hearts) and nothing comes for them");
        P.hp = 5.5f;
        Call(G, "RescueAftermath", 0, 0, Origin);
        Check(P.hp == 6f, "the draught never lifts the hero past full health");

        // What did you see, now: the Gloom heard them talking.
        Fresh(); Stage();
        Call(G, "RescueAftermath", 0, 1, Origin);
        var ambush = Enemy.All.ToList();
        Check(ambush.Count == Game.AmbushPack.Length && ambush.Count(e => e.kind == Enemy.Kind.Minion) == 2 && ambush.Count(e => e.kind == Enemy.Kind.Spitter) == 1,
            "WHAT DID YOU SEE, at once: the talking draws an ambush of two imps and a thrower");
        Check(ambush.All(e => Vector3.Distance(e.transform.position, P.transform.position) >= 6f && Vector3.Distance(e.transform.position, P.transform.position) <= 11f && e.aggro >= 30f),
            "the ambush arrives at a fair distance (6-11 m) and is already hunting");
        Check(Math.Abs(P.hp - P.maxHp) < 0.001f, "asking her questions gives no draught");

        // Later, go home: Lady Vane's thanks, once.
        Fresh();
        JourneyStore.Transaction(() => { JourneyStore.SetInt("consequence:rescue:rhea:safe-return", 1); JourneyStore.SetInt("consequence:rescue:rhea:home", 1); });
        Check(Game.VaneThanksDue, "with Rhea home safe, House Vane's thanks is due");
        bool interrupted = false;
        JourneyStore.BeforeReplaceForTest = path => { if (path == JourneyStore.FilePath) throw new IOException("injected thanks interruption"); };
        try { G.PayVaneThanks(); } catch (IOException) { interrupted = true; } finally { JourneyStore.BeforeReplaceForTest = null; }
        Reload();
        Check(interrupted && Save.Sparks == 100 && Game.VaneThanksDue, "an interrupted payment pays nothing and leaves the thanks due");
        Check(G.PayVaneThanks() && Save.Sparks == 100 + Game.VaneThanks && !Game.VaneThanksDue, "Lady Vane pays her thanks (+" + Game.VaneThanks + " banked Sparks)");
        Reload();
        Check(!G.PayVaneThanks() && Save.Sparks == 100 + Game.VaneThanks && Game.VaneThanksPaid, "the thanks survives reload and is never paid twice");
        Check(Save.Level == 6 && Save.TowerFloor == 4, "the thanks grants no level or floor");

        // The real Lady Vane conversation.
        Fresh();
        JourneyStore.Transaction(() => { JourneyStore.SetInt("consequence:rescue:rhea:safe-return", 1); JourneyStore.SetInt("consequence:rescue:rhea:home", 1); });
        Stage();
        bool opened = false;
        Call(G, "VaneGreeting", (Action)(() => opened = true));
        var lines = (Line[])Get(G, "dialog");
        Check(G.State == Game.GameState.Story && lines.Any(l => l.text.Contains("pays what it owes")) && !opened, "talking to Lady Vane, she thanks the hero first");
        ((Action)Get(G, "dialogDone"))();
        Check(opened && Save.Sparks == 100 + Game.VaneThanks, "when she finishes, the thanks is paid and her board opens");

        // Later, what did you see: no thanks, and Lady Vane is cool, once.
        Fresh();
        JourneyStore.Transaction(() => { JourneyStore.SetInt("consequence:rescue:rhea:guardian-intel", 1); JourneyStore.SetInt("consequence:rescue:rhea:home", 1); });
        Stage();
        Check(!Game.VaneThanksDue, "asking her questions earns no thanks from House Vane");
        opened = false;
        Call(G, "VaneGreeting", (Action)(() => opened = true));
        lines = (Line[])Get(G, "dialog");
        Check(G.State == Game.GameState.Story && lines.Any(l => l.text.Contains("not a map")), "Lady Vane is cool with the hero who kept her niece talking");
        ((Action)Get(G, "dialogDone"))();
        Call(G, "SetState", Game.GameState.Playing);
        opened = false;
        Call(G, "VaneGreeting", (Action)(() => opened = true));
        Check(opened && G.State == Game.GameState.Playing && Save.Sparks == 100, "she says it once; after that her board opens directly, and nothing is paid");

        // Rhea's own later lines follow the branch.
        Fresh();
        JourneyStore.Transaction(() => JourneyStore.SetInt("consequence:rescue:rhea:safe-return", 1));
        bool home = Story.RescueTown(0).Any(l => l.text.Contains("reached Brushwick safely"));
        Fresh();
        JourneyStore.Transaction(() => JourneyStore.SetInt("consequence:rescue:rhea:guardian-intel", 1));
        bool asked = Story.RescueTown(0).Any(l => l.text.Contains("ground goes red"));
        Check(home && asked, "Rhea remembers in town which way the hero chose");
    }

    // ------------------------------------------------------------ N02

    static void Orders()
    {
        Fresh();
        Check(Companion.Order == CompanionOrder.Follow, "a journey starts with Mop at Follow, as before");
        var seen = new List<CompanionOrder>();
        for (int i = 0; i < 4; i++) seen.Add(Companion.CycleOrder());
        Check(seen.SequenceEqual(new[] { CompanionOrder.Assist, CompanionOrder.Hold, CompanionOrder.Solo, CompanionOrder.Follow }), "the order key cycles Follow, Assist, Hold, Solo and back");
        Companion.SetOrder(CompanionOrder.Hold);
        Reload();
        Check(Companion.Order == CompanionOrder.Hold, "the order is kept in the journey across a reload");
        JourneyStore.Transaction(() => JourneyStore.SetInt("companion:order", 9));
        Check(Companion.Order == CompanionOrder.Follow, "an unknown stored order falls back to Follow without rewriting the journey");
        Check(ControlGuide.Key(ControlAction.Companion).Length > 0 && Enum.GetValues(typeof(ControlAction)).Length == 19, "the order has its own control, appended after every existing one");
        Check(Enum.GetValues(typeof(CompanionOrder)).Cast<CompanionOrder>().All(o => Companion.Describe(o).Length > 10), "every order is described when it is given");
    }

    // A timed scene: arranged, then left to Mop's own Update for a while, then judged.
    class Scene { public string name; public Action arrange; public Action<float> each; public float seconds; public Action judge; }
    static readonly List<Scene> scenes = new List<Scene>();
    static int sceneIndex = -1;
    static Enemy a, b;
    static Vector3 post;
    static float nearestToPost, farthestFromHero, maxFromPost;
    static int strikesAtStart;

    static void BuildScenes()
    {
        // Follow: the nearest fight to the hero.
        scenes.Add(new Scene
        {
            name = "follow", seconds = 4f,
            arrange = () =>
            {
                Fresh(); Stage(); Companion.SetOrder(CompanionOrder.Follow);
                a = Foe(new Vector3(2.5f, 0f, 3f)); b = Foe(new Vector3(-9f, 0f, 8f));
                strikesAtStart = Mop.Strikes;
            },
            judge = () => Check(Mop.Strikes > strikesAtStart && a.hp < a.maxHp && b.hp == b.maxHp, "FOLLOW: Mop fights beside the hero, going for the nearest threat (" + (Mop.Strikes - strikesAtStart) + " strikes; roaming=" + G.Roaming + " mop=" + Mop.transform.position + " a=" + a.transform.position + " engaged=" + a.Engaged + " target=" + (Mop.Target != null ? Mop.Target.name : "none") + " state=" + Mop.GetComponent<NavFollower>().state + " enabled=" + Mop.enabled + " active=" + Mop.gameObject.activeInHierarchy + " intro=" + G.Intro + ")"),
        });
        // Assist: only the hero's target, which flinches.
        scenes.Add(new Scene
        {
            name = "assist", seconds = 4.5f,
            arrange = () =>
            {
                Fresh(); Stage(); Companion.SetOrder(CompanionOrder.Assist);
                a = Foe(new Vector3(-5f, 0f, 6f)); b = Foe(new Vector3(2f, 0f, 2f));
                Set(P, "LockedTarget", a);
                strikesAtStart = Mop.Strikes;
                maxFromPost = 0f;
            },
            each = dt => { if (a.Open) maxFromPost = 1f; },
            judge = () =>
            {
                Check(Mop.Strikes > strikesAtStart && a.hp < a.maxHp && b.hp == b.maxHp, "ASSIST: Mop goes for the hero's locked target and ignores the nearer enemy");
                Check(maxFromPost > 0f, "ASSIST: his bump makes the hero's target flinch, open for a riposte");
            },
        });
        scenes.Add(new Scene
        {
            name = "assist-idle", seconds = 2.5f,
            arrange = () =>
            {
                Fresh(); Stage(); Companion.SetOrder(CompanionOrder.Assist);
                a = Foe(new Vector3(2f, 0f, 2f));
                strikesAtStart = Mop.Strikes;
            },
            judge = () => Check(Mop.Strikes == strikesAtStart && Mop.Target == null, "ASSIST: with no target of the hero's, Mop waits at heel instead of picking his own fight"),
        });
        scenes.Add(new Scene
        {
            name = "assist-struck", seconds = 4f,
            arrange = () =>
            {
                Fresh(); Stage(); Companion.SetOrder(CompanionOrder.Assist);
                a = Foe(new Vector3(-5f, 0f, 5f)); b = Foe(new Vector3(2f, 0f, 2f));
                a.transform.position = P.transform.position + Vector3.forward * 1.4f; Physics.SyncTransforms();
                Call(P, "Hit", a, 1f, false, 0f, false, DamageKind.Physical, false, 0f, 0f);
                a.transform.position = Origin + new Vector3(-5f, 0f, 5f); Physics.SyncTransforms();
                strikesAtStart = Mop.Strikes;
            },
            judge = () => Check(Mop.Strikes > strikesAtStart && b.hp == b.maxHp, "ASSIST: without a lock, Mop goes for whatever the hero has just been hitting"),
        });
        // Hold: his post, and only what comes near it.
        scenes.Add(new Scene
        {
            name = "hold", seconds = 5f,
            arrange = () =>
            {
                Fresh(); Stage(); Companion.SetOrder(CompanionOrder.Hold);
                post = Mop.transform.position;
                a = Foe(new Vector3(0f, 0f, 16f));
                P.Teleport(Origin + new Vector3(0f, 0f, 14f));
                strikesAtStart = Mop.Strikes; maxFromPost = 0f;
            },
            each = dt => { P.transform.position = Origin + new Vector3(0f, 0.1f, 14f); maxFromPost = Mathf.Max(maxFromPost, Vector3.Distance(Mop.transform.position, post)); },
            judge = () => Check(maxFromPost <= Companion.HoldLeash + 0.5f && Mop.Strikes == strikesAtStart && a.hp == a.maxHp,
                "HOLD: Mop keeps his post (" + Num(maxFromPost) + " m at most) while the hero fights 14 m away"),
        });
        scenes.Add(new Scene
        {
            name = "hold-guard", seconds = 4f,
            arrange = () =>
            {
                Fresh(); Stage(); Companion.SetOrder(CompanionOrder.Hold);
                post = Mop.transform.position;
                a = Foe(post - Origin + new Vector3(2.2f, 0f, 0.5f));
                P.Teleport(Origin + new Vector3(0f, 0f, 14f));
                strikesAtStart = Mop.Strikes;
            },
            each = dt => P.transform.position = Origin + new Vector3(0f, 0.1f, 14f),
            judge = () => Check(Mop.Strikes > strikesAtStart && a.hp < a.maxHp, "HOLD: anything that comes within " + Companion.HoldGuard + " m of his post gets bitten"),
        });
        // Solo: the hero fights alone.
        scenes.Add(new Scene
        {
            name = "solo", seconds = 5f,
            arrange = () =>
            {
                Fresh(); Stage(); Companion.SetOrder(CompanionOrder.Solo);
                a = Foe(new Vector3(1.5f, 0f, 2f)); b = Foe(new Vector3(-2f, 0f, 1.5f));
                strikesAtStart = Mop.Strikes; nearestToPost = 999f;
            },
            each = dt => nearestToPost = Mathf.Min(nearestToPost, Mathf.Min(Vector3.Distance(Mop.transform.position, a.transform.position), Vector3.Distance(Mop.transform.position, b.transform.position))),
            judge = () =>
            {
                float back = Vector3.Distance(Mop.transform.position, P.transform.position);
                Check(Mop.Strikes == strikesAtStart && a.hp == a.maxHp && b.hp == b.maxHp && Mop.Target == null, "SOLO: Mop does not fight at all");
                Check(back > 2.5f && back < 7f, "SOLO: he keeps well back, " + Num(back) + " m behind the hero");
            },
        });
        // A hold point belongs to the floor it was given on.
        scenes.Add(new Scene
        {
            name = "hold-rebuild", seconds = 0.5f,
            arrange = () =>
            {
                Fresh(); Stage(); Companion.SetOrder(CompanionOrder.Hold);
                post = (Vector3)Get(Mop, "holdAt");
                var root = new GameObject("New floor").transform;
                Set(G, "LevelRoot", root);
                Mop.transform.position = Origin + new Vector3(6f, 0f, 6f);
            },
            judge = () => Check(Vector3.Distance((Vector3)Get(Mop, "holdAt"), post) > 3f, "on a new floor Mop holds where he stands, not at the old floor's post"),
        });
    }

    static void Capture(string name)
    {
        var go = new GameObject("Branch evidence camera");
        var cam = go.AddComponent<Camera>();
        cam.transform.position = P.transform.position + new Vector3(0f, 14f, -10f);
        cam.transform.LookAt(P.transform.position + Vector3.forward * 2f);
        cam.fieldOfView = 50f;
        var rt = new RenderTexture(800, 450, 24);
        var tex = new Texture2D(800, 450, TextureFormat.RGB24, false);
        var previous = RenderTexture.active;
        try
        {
            cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, 800, 450), 0, 0); tex.Apply();
            File.WriteAllBytes("Logs/companion-" + name + ".png", tex.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous; cam.targetTexture = null; rt.Release();
            UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(tex); UnityEngine.Object.DestroyImmediate(go);
        }
    }

    // ------------------------------------------------------------ runner

    static BranchCompanionTest()
    {
        if (SessionState.GetBool(Session, false)) { EditorApplication.update += Tick; Application.logMessageReceived += OnLog; }
    }

    public static void Run()
    {
        Save.Wipe(); Save.SeenIntro = true; Save.Flush();
        EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        SessionState.SetBool(Session, true);
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
        Application.logMessageReceived -= OnLog; Application.logMessageReceived += OnLog;
        EditorApplication.EnterPlaymode();
    }

    static int lastFrame = -1;

    static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        if (readyAt == 0) { readyAt = EditorApplication.timeSinceStartup + 2; deadline = readyAt + 300; }
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Branch/companion test timed out at scene " + sceneIndex);
            if (G == null || P == null || EditorApplication.timeSinceStartup < readyAt) return;
            if (Time.frameCount == lastFrame) return;
            lastFrame = Time.frameCount;
            Application.runInBackground = true;
            if (sceneIndex < 0)
            {
                Branch(); Orders(); BuildScenes();
                sceneIndex = 0;
                scenes[0].arrange();
                stageEnds = EditorApplication.timeSinceStartup + scenes[0].seconds;
                return;
            }
            if (sceneIndex < scenes.Count)
            {
                var s = scenes[sceneIndex];
                // Enemies are kept from attacking the hero; Mop's own Update runs untouched.
                foreach (var e in Enemy.All) Set(e, "cooldown", 100000f);
                s.each?.Invoke(Time.deltaTime);
                if (EditorApplication.timeSinceStartup < stageEnds) return;
                s.judge();
                if (s.name == "assist" || s.name == "hold" || s.name == "solo") Capture(s.name);
                if (++sceneIndex < scenes.Count)
                {
                    scenes[sceneIndex].arrange();
                    stageEnds = EditorApplication.timeSinceStartup + scenes[sceneIndex].seconds;
                }
                return;
            }
            Check(errors.Count == 0, "branch and companion scenarios logged no unexpected errors" + (errors.Count > 0 ? ": " + errors[0] : ""));
            Fresh();
            Check(JourneyStore.Error == null, "branch and companion scenarios leave the journey store healthy");
            Debug.Log("BRANCH DONE " + passed + " checks passed");
            Finish(0);
        }
        catch (Exception e)
        {
            Debug.LogError("BRANCH FAILED scene=" + sceneIndex + " " + e);
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
