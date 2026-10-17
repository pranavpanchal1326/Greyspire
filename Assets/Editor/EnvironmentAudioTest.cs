using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Phase nine, packages V01 (first-zone environment pass) and S01 (movement, contact and
// ambience audio). V01: the proven route surfaces are untouched while what is drawn changes.
// S01: every wind-up is heard, warnings survive a mixed fight, and the bed stays underneath.
[InitializeOnLoad]
public static class EnvironmentAudioTest
{
    const string Session = "GreyspireEnvironmentAudioTest";
    const BindingFlags Any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static;
    static readonly Vector3 Origin = new Vector3(1000f, 0.1f, 1000f);
    static int passed;
    static double readyAt, stageEnds;
    static int stage;
    static readonly List<string> errors = new List<string>();
    static Game G => Game.I;
    static PlayerController P => G.Player;
    static object Get(object host, string name) => host.GetType().GetField(name, Any).GetValue(host);
    static void Set(object host, string name, object value) => (host.GetType().GetField(name, Any) ?? host.GetType().GetField("<" + name + ">k__BackingField", Any)).SetValue(host, value);
    static object Call(object host, string name, params object[] args) => host.GetType().GetMethod(name, Any).Invoke(host, args);
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); passed++; Debug.Log("ENVAUDIO PASS " + message); }
    static void OnLog(string message, string stack, LogType type) { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message); }

    static void Fresh()
    {
        Save.Wipe(); Save.SeenIntro = true; Save.Level = 8; Save.BestFloor = 5; Save.TowerFloor = 4; Save.StoryFloor = 5;
        JourneyStore.SetInt("productionSeed", 734912); JourneyStore.SetInt("seedPolicy", 1); Save.Flush(); Upgrades.Reset(); ProductionRandom.ClearRuntimeStreams();
    }

    static void BuildFloor(int floor)
    {
        foreach (var e in Enemy.All.ToArray()) UnityEngine.Object.DestroyImmediate(e.gameObject);
        G.enabled = false; P.enabled = false;
        Call(G, "SetState", Game.GameState.Playing);
        Set(G, "InWilds", false); Set(G, "InTown", false);
        var def = FloorCatalog.At(floor);
        Set(G, "level", def.theme); Set(G, "wave", def.layout);
        Call(G, "BuildFloor", def.theme, def.layout);
        Physics.SyncTransforms();
    }

    static Transform Find(string name) => G.LevelRoot.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
    static IEnumerable<Transform> All(string name) => G.LevelRoot.GetComponentsInChildren<Transform>(true).Where(t => t.name == name);
    static bool Kit(Renderer r) => r != null && r.sharedMaterials.All(m => m != null && m.mainTexture != null && m.mainTexture.name.StartsWith("T_"));

    // ------------------------------------------------------------ V01

    static void Millrace()
    {
        Fresh();
        BuildFloor(2);
        var deck = Find("Millrace bridge");
        var box = deck.GetComponent<BoxCollider>();
        Check(box != null && box.enabled && deck.position == new Vector3(0f, 5.15f, 0f) && deck.localScale == new Vector3(10f, 0.5f, 14f) && box.size == Vector3.one,
            "the bridge deck keeps its proven 10 x 14 m collision at 5.4 m");
        Check(Kit(deck.GetComponent<Renderer>()) && deck.GetComponent<MeshFilter>().sharedMesh.name == "Dressed box", "the deck is drawn in the kit's paving, not a flat slab");
        foreach (string ramp in new[] { "Millrace south ramp", "Millrace north ramp" })
        {
            var r = Find(ramp);
            var collider = r.GetComponent<MeshCollider>().sharedMesh;
            var drawn = r.GetComponent<MeshFilter>().sharedMesh;
            Check(collider.vertexCount == 8 && collider != drawn && Mathf.Abs(collider.bounds.max.y - 5.4f) < 0.001f && Mathf.Abs(collider.bounds.size.z - 20f) < 0.001f,
                ramp + " keeps its proven 20 m collision ramp; only its drawing changed");
            Check(r.GetComponent<Renderer>().sharedMaterials.Length == 2 && Kit(r.GetComponent<Renderer>()), ramp + " has a paved walking surface and stone sides");
        }
        var stoneParts = All("Millrace bridge parapet").Concat(All("Millrace sluice pier")).ToList();
        Check(stoneParts.Count == 4 && stoneParts.All(t => t.GetComponent<BoxCollider>() != null && Kit(t.GetComponent<Renderer>())), "parapets and sluice piers keep their colliders and are drawn in kit stone");
        var gate = Find("Millrace service gate");
        Check(gate != null && gate.GetComponent<BoxCollider>() != null && Kit(gate.GetComponent<Renderer>()), "the service gate keeps its collider and is drawn in planks");
        var piers = All("Pillar").Where(t => t.parent == G.LevelRoot && Mathf.Abs(Mathf.Abs(t.position.x) - 4.4f) < 0.05f && Mathf.Abs(Mathf.Abs(t.position.z) - 6.3f) < 0.05f).ToList();
        Check(piers.Count == 4 && piers.All(t => Mathf.Abs(t.position.z) > 3f + 0.45f), "four stone piers carry the deck corners, clear of the 6 m crossing lane");
        var lights = G.LevelRoot.GetComponentsInChildren<Light>().Where(l => l.name == "Lantern light").ToList();
        var under = lights.Where(l => Mathf.Abs(l.transform.position.x) < 1.5f && Mathf.Abs(l.transform.position.z) < 6f).ToList();
        Check(under.Count == 4 && under.All(l => l.shadows == LightShadows.None && l.range >= 8f && Vector3.Distance(l.transform.position, new Vector3(0f, 1f, 0f)) < l.range * 0.7f),
            "four shadowless lanterns light the underpass crossing (the guide asks for fill before shadow-casting lights)");
        Check(under.All(l => l.transform.parent.position.y > 2.6f), "the lanterns hang above head height");
    }

    static void SurveyRuin()
    {
        Fresh();
        BuildFloor(3);
        foreach (string wing in new[] { "South ruin wing", "North ruin wing" })
        {
            var w = Find(wing);
            Check(w.GetComponent<BoxCollider>() != null && w.localScale == new Vector3(26f, 9f, 12f) && Kit(w.GetComponent<Renderer>()), wing + " keeps its proven bulk and is drawn in kit stone");
        }
        var merlons = All("Merlon").ToList();
        Check(merlons.Count > 40 && merlons.All(m => m.GetComponent<Collider>() == null), "battlements crown both wings (" + merlons.Count + " merlons), none of them solid");
        var rest = Find("Courier rest");
        Check(rest != null && rest.GetComponentsInChildren<Collider>().Length == 0, "the courier's rest point stands on the overlook and blocks nothing");
        foreach (var spot in new[] { new Vector3(-23f, 3.2f, 9f), Game.PilotCourierAt })
        {
            var blocking = Physics.OverlapSphere(spot + Vector3.up * 0.9f, 0.6f).Where(c => !c.transform.IsChildOf(P.transform) && !c.name.StartsWith("Chest") && c.GetComponentInParent<Pickup>() == null
                && !(c is MeshCollider) && c.transform.parent?.name != "Iven the courier" && c.name != "Survey overlook").ToList();
            Check(blocking.Count == 0, "nothing new stands where the hero meets the " + (spot.z > 5f ? "overlook chest" : "courier") + (blocking.Count > 0 ? ": " + blocking[0].name : ""));
        }
        Check(Kit(Find("Survey overlook").GetComponent<Renderer>()), "the overlook is paved");
    }

    static void Statue()
    {
        Fresh();
        BuildFloor(4);
        var statue = Find("Drained Painter");
        Check(statue != null, "Rhea stands drained on floor 4");
        Vector3 s = statue.position; s.y = 0f;
        float trailX = 9f * Mathf.Cos(s.z * Mathf.PI / 76f);
        Vector3 toTrail = new Vector3(trailX, 0f, s.z) - s;
        var rocks = G.LevelRoot.GetComponentsInChildren<Transform>().Where(t => t.parent == G.LevelRoot && t.name.StartsWith("Rock_Medium"))
            .Where(t => { Vector3 d = t.position - s; d.y = 0f; return d.magnitude > 3.5f && d.magnitude < 5f; }).ToList();
        Check(rocks.Count >= 6, "a ring of standing rocks frames the statue (" + rocks.Count + ")");
        Check(rocks.All(r => Vector3.Angle(Vector3.ProjectOnPlane(r.position - s, Vector3.up), toTrail) > 25f), "the ring opens toward the trail, so the approach and the story camera stay clear");
    }

    // Ownership is measured across frames: a rebuilt floor's old objects (and the meshes they
    // own) are only destroyed at the end of the frame, as in the game.
    static readonly List<int> owned = new List<int>();
    static void OwnershipStep(int pass)
    {
        if (pass > 0) owned.Add(GeneratedResources.LiveAssets);
        // With both shortcuts open: an opened gate is inactive when it is dressed, and its mesh
        // must still be freed with the floor (the leak the route test caught).
        if (pass < 4) { Fresh(); ChapterProgress.OpenMillrace(ChapterProgress.MillraceToken); PilotProgress.OpenShortcut(); BuildFloor(pass % 2 == 0 ? 2 : 3); }
    }
    static void JudgeOwnership()
    {
        // owned[0]: floor 2 (first build), [1]: floor 3, [2]: floor 2 again, [3]: floor 3 again.
        Check(ChapterProgress.MillraceOpen && owned[2] == owned[0] && owned[3] == owned[1], "rebuilding the dressed floors, shortcuts open, keeps generated ownership exactly bounded (floor 2: " + owned[0] + " / " + owned[2] + ", floor 3: " + owned[1] + " / " + owned[3] + ")");
    }

    // ------------------------------------------------------------ S01

    static void Catalog()
    {
        var tell = GameAudio.Definition("tell");
        Check(tell.priority == CuePriority.Threat && tell.requirement == CueRequirement.Critical && GameAudio.Takes("tell").Length >= 3, "the wind-up tell is a critical Threat cue with three recorded takes");
        var bed = Resources.Load<AudioClip>("Audio/ambience_meadow_1");
        Check(bed != null && bed.length >= 20f, "the ambience bed is a " + (bed != null ? bed.length.ToString("0") : "?") + " s loop");
        Check(Ambience.Wanted(true, false, false, 0, false) < Ambience.Wanted(false, true, false, 0, false) && Ambience.Wanted(false, true, true, 0, false) < Ambience.Wanted(false, true, false, 0, false)
            && Ambience.Wanted(false, true, true, 1, false) < Ambience.Wanted(false, true, true, 0, false) && Ambience.Wanted(false, true, false, 0, true) < Ambience.Wanted(false, true, false, 0, false),
            "the bed is quieter in town, ducks in a fight, ducks further under a threat, and follows the quiet mix");
    }

    // A sustained mixed fight over real frames: every frame four contact sounds and three
    // footsteps; every quarter second a wind-up from one of eight enemies round the hero.
    static int tellsRequested, tellsPlayed;
    static double nextTell, survivalEnds;
    static void BeginSurvival()
    {
        GameAudio.StopWorld();
        tellsRequested = tellsPlayed = 0;
        nextTell = EditorApplication.timeSinceStartup;
        survivalEnds = EditorApplication.timeSinceStartup + 4.0;
    }
    static bool SurvivalFrame()
    {
        Vector3 at = P.transform.position;
        for (int i = 0; i < 4; i++) GameAudio.Play(i % 2 == 0 ? "hit" : "swing", position: at + new Vector3(i - 2, 1, 3));
        for (int i = 0; i < 3; i++) GameAudio.Play("step", 0.4f, position: at + new Vector3(i, 0.2f, -2));
        if (EditorApplication.timeSinceStartup >= nextTell)
        {
            nextTell += 0.25;
            float a = (tellsRequested % 8) * 45f * Mathf.Deg2Rad;
            tellsRequested++;
            if (GameAudio.Play("tell", 0.85f, 1f, at + new Vector3(Mathf.Sin(a) * 6f, 1f, Mathf.Cos(a) * 6f))) tellsPlayed++;
        }
        return EditorApplication.timeSinceStartup >= survivalEnds;
    }
    static void JudgeSurvival()
    {
        Check(tellsRequested >= 14 && tellsPlayed == tellsRequested, "under 4 s of a sustained mixed-fight barrage every wind-up tell got a voice (" + tellsPlayed + " of " + tellsRequested + ")");
        Vector3 at = P.transform.position;
        // The duck: a contact sound while a threat sounds plays quieter than the same sound alone.
        GameAudio.StopWorld();
        GameAudio.Play("hit", 1f, position: at + Vector3.forward * 3f);
        float alone = GameAudio.LastSource.volume;
        GameAudio.Play("tell", 0.85f, 1f, at + Vector3.right * 5f);
        Check(GameAudio.ActiveThreats > 0, "a tell sounds as a threat");
        GameAudio.Play("hit", 1f, position: at + Vector3.forward * 3.5f + Vector3.left);
        float ducked = GameAudio.LastSource.volume;
        Check(Mathf.Abs(ducked - alone * GameAudio.ThreatDuck) < 0.001f, "while a threat sounds, contact steps back (" + alone.ToString("0.###") + " -> " + ducked.ToString("0.###") + ")");
        GameAudio.StopWorld();
    }

    static readonly List<Enemy> fighters = new List<Enemy>();
    static int commitsAt;

    static void BeginFight()
    {
        Fresh();
        foreach (var e in Enemy.All.ToArray()) UnityEngine.Object.DestroyImmediate(e.gameObject);
        var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.transform.position = Origin - Vector3.up * 0.6f; ground.transform.localScale = new Vector3(80f, 1f, 80f);
        G.enabled = false; P.enabled = false;
        Call(G, "SetState", Game.GameState.Playing);
        Set(G, "InTown", false); Set(G, "InWilds", false); Set(G, "DefeatPending", false);
        P.maxHp = P.hp = 100000f;
        P.Revive(Origin, true);
        EncounterDirector.Reset();
        fighters.Clear();
        foreach (var (kind, at) in new[] { (Enemy.Kind.Minion, new Vector3(-3f, 0f, 6f)), (Enemy.Kind.Slime, new Vector3(4f, 0f, 4f)), (Enemy.Kind.Spitter, new Vector3(0f, 0f, 12f)), (Enemy.Kind.Brute, new Vector3(6f, 0f, 14f)) })
        {
            var e = Enemy.Create(kind, Origin + at, 0);
            e.hp = e.maxHp = 100000f; e.aggro = 60f;
            fighters.Add(e);
        }
        commitsAt = EncounterDirector.Log.Count;
        Set(G, "waveActive", true);     // a fight on the floor: the score and the bed respond
        Physics.SyncTransforms();
    }

    static void JudgeFight()
    {
        foreach (var e in fighters)
        {
            int commits = EncounterDirector.Log.Skip(commitsAt).Count(c => c.spawnId == e.SpawnId);
            // Every blow that landed was heard winding up first (a thrower's bolt lands as a projectile).
            Check(commits > 0 && e.Tells > 0 && e.Tells >= e.HitsLanded, e.Role + ": " + e.HitsLanded + " blows landed, each heard winding up first (" + e.Tells + " tells, " + commits + " commitments)");
        }
        var brute = fighters.First(e => e.kind == Enemy.Kind.Brute);
        Check(brute.Footfalls > 0, "the brute was heard walking in (" + brute.Footfalls + " footfalls)");
        var bed = G.WorldAmbience;
        Check(bed != null && bed.Target > 0f && bed.Target < Ambience.FloorLevel, "during the fight the wind bed sits ducked below its resting level (" + bed.Target.ToString("0.###") + ")");
    }

    // ------------------------------------------------------------ runner

    static EnvironmentAudioTest()
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

    static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        if (readyAt == 0) readyAt = EditorApplication.timeSinceStartup + 2;
        try
        {
            if (G == null || P == null || EditorApplication.timeSinceStartup < readyAt) return;
            Application.runInBackground = true;
            switch (stage)
            {
                case 0:
                    Millrace(); SurveyRuin(); Statue(); Catalog();
                    BeginSurvival();
                    stage = 5; return;
                case 5:
                    if (!SurvivalFrame()) return;
                    JudgeSurvival();
                    OwnershipStep(0);
                    stage = 10; stageEnds = EditorApplication.timeSinceStartup + 0.5; return;
                case 10: case 11: case 12: case 13:
                    if (EditorApplication.timeSinceStartup < stageEnds) return;
                    OwnershipStep(stage - 9);
                    stage++; stageEnds = EditorApplication.timeSinceStartup + 0.5;
                    if (stage < 14) return;
                    JudgeOwnership();
                    BeginFight();
                    G.enabled = true;   // the game's own loop drives the ambience during the fight
                    stage = 1; stageEnds = EditorApplication.timeSinceStartup + 12; return;
                case 1:
                    foreach (var e in Enemy.All) Set(e, "provoked", true);
                    Set(P, "invuln", 0f); P.hp = 100000f;
                    if (EditorApplication.timeSinceStartup < stageEnds) return;
                    JudgeFight();
                    break;
            }
            G.enabled = false;
            Check(errors.Count == 0, "environment and audio scenarios logged no unexpected errors" + (errors.Count > 0 ? ": " + errors[0] : ""));
            Fresh();
            Check(JourneyStore.Error == null, "environment and audio scenarios leave the journey store healthy");
            Debug.Log("ENVAUDIO DONE " + passed + " checks passed");
            Finish(0);
        }
        catch (Exception e)
        {
            Debug.LogError("ENVAUDIO FAILED stage=" + stage + " " + e);
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
