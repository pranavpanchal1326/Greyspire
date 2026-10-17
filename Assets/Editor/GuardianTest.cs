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

// Phase nine, package C03: Grol the Gatekeeper, floor 5's Guardian. Receipts, the boon offer,
// the real floor-5 build and arena, every move of his language stepped deterministically,
// phases, defeat and clear, the retry path, Gatebreaker, the aftermath, and review captures.
[InitializeOnLoad]
public static class GuardianTest
{
    const string Session = "GreyspireGuardianTest";
    const BindingFlags Any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static;
    static int passed;
    static double readyAt, deadline;
    static readonly List<string> errors = new List<string>();
    static Game G => Game.I;
    static PlayerController P => G.Player;
    static object Get(object host, string name) => host.GetType().GetField(name, Any).GetValue(host);
    static void Set(object host, string name, object value) => (host.GetType().GetField(name, Any) ?? host.GetType().GetField("<" + name + ">k__BackingField", Any)).SetValue(host, value);
    static object Call(object host, string name, params object[] args) => host.GetType().GetMethod(name, Any).Invoke(host, args);
    static string Num(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); passed++; Debug.Log("GUARDIAN PASS " + message); }
    static void OnLog(string message, string stack, LogType type) { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message); }

    static void State()
    {
        Call(G, "SetState", Game.GameState.Playing); Set(G, "fade", 0f); Set(G, "fadeDir", 0f); Set(G, "introT", 0f);
        Set(G, "showMap", false); Set(G, "awakening", false); Set(G, "hintStep", -1);
        Set(G, "hubPanel", 0); Set(G, "InTown", false); Set(G, "InWilds", false); Set(G, "DefeatPending", false);
        ((IList)Get(G, "timers")).Clear();
    }
    static void Fresh(int towerFloor = 4)
    {
        Save.Wipe(); Save.SeenIntro = true; Save.Level = 8; Save.BestFloor = towerFloor; Save.TowerFloor = towerFloor; Save.StoryFloor = 5; Save.Sparks = 100;
        JourneyStore.SetInt("productionSeed", 734912); JourneyStore.SetInt("seedPolicy", 1); Save.Flush(); Upgrades.Reset(); ProductionRandom.ClearRuntimeStreams();
    }
    static void Reload() { JourneyStore.BeforeReplaceForTest = null; JourneyStore.ReloadForTest(); JourneyStore.Ensure(); }

    // Builds floor 5 for real and puts its monsters out.
    static void BuildFloor5()
    {
        foreach (var e in Enemy.All.ToArray()) UnityEngine.Object.DestroyImmediate(e.gameObject);
        G.enabled = P.enabled = false;
        State();
        Set(G, "level", 0); Set(G, "wave", 4);
        Call(G, "BuildFloor", 0, 4);
        Set(G, "waveActive", true); Set(G, "pending", 0);
        Call(G, "SpawnPacks", Call(G, "FloorWave", 0, 4));
        P.Revive((Vector3)Call(G, "FloorArrival", 5), true);
        Physics.SyncTransforms();
    }

    static Enemy K => G.Keeper;

    // ------------------------------------------------------------ records and boon

    static void Records()
    {
        Fresh();
        byte[] original = File.ReadAllBytes(JourneyStore.FilePath);
        ChapterProgress.Validate();
        Check(!ChapterProgress.GatekeeperCheckpoint && File.ReadAllBytes(JourneyStore.FilePath).SequenceEqual(original), "an existing journey has no checkpoint and needs no migration");
        bool interrupted = false;
        JourneyStore.BeforeReplaceForTest = path => { if (path == JourneyStore.FilePath) throw new IOException("injected checkpoint interruption"); };
        try { ChapterProgress.ReachGatekeeper(ChapterProgress.GatekeeperToken); } catch (IOException) { interrupted = true; } finally { JourneyStore.BeforeReplaceForTest = null; }
        Reload();
        Check(interrupted && !ChapterProgress.GatekeeperCheckpoint, "an interrupted checkpoint write keeps the previous state");
        string token = ChapterProgress.GatekeeperToken;
        Check(ChapterProgress.ReachGatekeeper(token), "the checkpoint commits");
        Reload();
        Check(ChapterProgress.GatekeeperCheckpoint && !ChapterProgress.ReachGatekeeper(token) && Save.TowerFloor == 4 && Save.Sparks == 100 && Save.Level == 8,
            "the checkpoint survives reload, cannot be taken twice, and grants no floor, Sparks or level");
        Check(ChapterProgress.PassGatekeeper(token) && !ChapterProgress.GatekeeperCheckpoint, "passing the Gatekeeper clears the checkpoint");
        ChapterProgress.ReachGatekeeper(token);
        Fresh();
        Check(!ChapterProgress.ReachGatekeeper(token) && !ChapterProgress.GatekeeperCheckpoint, "a callback from an old campaign cannot set a new journey's checkpoint");
        foreach (int bad in new[] { -1, 2 })
        {
            byte[] supported = File.ReadAllBytes(JourneyStore.FilePath);
            JourneyStore.Transaction(() => JourneyStore.SetInt("chapter:tower:05:gatekeeper-checkpoint-v1", bad));
            byte[] future = File.ReadAllBytes(JourneyStore.FilePath);
            JourneyStore.ReloadForTest();
            bool rejected = false;
            try { JourneyStore.Ensure(); ChapterProgress.Validate(); } catch (NotSupportedException) { rejected = true; }
            Check(rejected && File.ReadAllBytes(JourneyStore.FilePath).SequenceEqual(future), "an unsupported checkpoint value (" + bad + ") is rejected with its bytes retained");
            File.WriteAllBytes(JourneyStore.FilePath, supported); File.WriteAllBytes(JourneyStore.BackupPath, supported); Reload();
        }
    }

    static void Boon()
    {
        Fresh();
        Upgrades.Reset();
        var gate = Upgrades.All.First(u => u.id == Upgrades.GatebreakerId);
        Check(gate.signature == 5 && gate.max == 1 && Upgrades.All.Select(u => u.id).Distinct().Count() == Upgrades.All.Count && Upgrades.All.Max(u => u.id) == Upgrades.GatebreakerId,
            "Gatebreaker is appended as boon 14, Grol's own, without disturbing the saved ids 0-13");
        bool rolled = false;
        for (int i = 0; i < 40; i++) rolled |= Upgrades.Roll(3).Any(u => u.id == Upgrades.GatebreakerId);
        Check(!rolled, "Gatebreaker never appears in an ordinary roll (40 rolls)");
        var offer5 = Upgrades.Offer(5, 3);
        Check(offer5.Count == 3 && offer5[0].id == Upgrades.GatebreakerId && offer5.Select(u => u.id).Distinct().Count() == 3, "Grol's offer leads with Gatebreaker and two other distinct boons");
        Check(!Upgrades.Offer(10, 3).Any(u => u.id == Upgrades.GatebreakerId), "another Guardian's offer does not include it");
        Check(Save.RecordFloorClear(0, 5) && Save.PendingGuardian != null && Save.PendingGuardian.choices.Contains(Upgrades.GatebreakerId), "clearing floor 5 for the first time records an offer containing Gatebreaker");
        Save.Sparks = 500;
        string key = Save.PendingGuardian.key;
        Check(Save.RerollGuardian(key, true) && Save.PendingGuardian.choices.Contains(Upgrades.GatebreakerId), "a paid reroll changes the other cards but keeps Grol's own on offer");
        Check(Save.TakeGuardian(key, Upgrades.GatebreakerId) && Save.Boon(Upgrades.GatebreakerId) == 1, "taking Gatebreaker keeps it for good");
        Upgrades.Reset();
        Check(!Upgrades.Offer(5, 3).Any(u => u.id == Upgrades.GatebreakerId), "once owned, it is not offered again");
        var stats = GrowthPolicy.Build(Save.Level, new int[4], new int[4], Upgrades.All.OrderBy(u => u.id).Select(u => Save.Boon(u.id)).ToArray(), Heroes.All[0]);
        Check(stats.gatebreaker, "the owned boon reaches the hero's stats");
    }

    // ------------------------------------------------------------ the arena

    static void Arena()
    {
        Fresh();
        BuildFloor5();
        Check(G.Arena == Game.ArenaState.Shut && K != null && K.Gatekeeper && K.Dormant && K.Role == EnemyRole.Guardian && K.title == "Grol the Gatekeeper",
            "floor 5 builds a shut arena with Grol bound as its dormant Gatekeeper");
        Check(G.PillarsStanding == 4 && G.SouthGateShut && G.NorthGateShut, "four pillars stand; both gates are shut");
        Check(Enemy.All.Count > 1, "the floor's other packs are out (" + (Enemy.All.Count - 1) + " monsters)");
        Vector3 c = Game.ArenaCentre;
        Check(Enemy.All.Where(e => e != K).All(e => new Vector2(e.transform.position.x - c.x, e.transform.position.z - c.z).magnitude > Game.KeeperArenaRadius + 1f),
            "no other monster stands inside the arena");
        float hp = K.hp;
        var hit = K.ReceiveHit(new DamageRequest(50f, K.transform.position + Vector3.back * 2f, 0f, true, source: P));
        Check(hit.outcome == HitOutcome.Immune && K.hp == hp, "while he keeps his shut gate, nothing hurts him");
        Call(G, "TickArena");
        Check(G.Arena == Game.ArenaState.Shut && !ChapterProgress.GatekeeperCheckpoint, "the gate stays shut while any other monster lives");
        foreach (var e in Enemy.All.Where(e => e != K).ToArray()) UnityEngine.Object.DestroyImmediate(e.gameObject);
        Call(G, "TickArena");
        Check(G.Arena == Game.ArenaState.Open && !G.SouthGateShut && G.NorthGateShut && ChapterProgress.GatekeeperCheckpoint,
            "with the floor quiet the south gate opens and the checkpoint is kept; the stairs gate stays shut");
        P.Teleport(c + new Vector3(0f, 0.1f, -4f));
        Call(G, "TickArena");
        Check(G.Arena == Game.ArenaState.Sealed && G.SouthGateShut && !K.Dormant, "walking in seals the gate behind the hero and wakes him");
        Check(!G.KeeperIntel && !G.PillarMarked(0), "without Rhea's intel the pillars are not pointed out at the start");

        // With Rhea's intel from floor 4, the fight opens differently.
        Fresh();
        JourneyStore.Transaction(() => JourneyStore.SetInt("consequence:rescue:rhea:guardian-intel", 1));
        BuildFloor5();
        foreach (var e in Enemy.All.Where(e => e != K).ToArray()) UnityEngine.Object.DestroyImmediate(e.gameObject);
        Call(G, "TickArena");
        P.Teleport(c + new Vector3(0f, 0.1f, -4f));
        Call(G, "TickArena");
        Check(G.KeeperIntel && Enumerable.Range(0, 4).All(G.PillarMarked), "with Rhea's intel her warning comes at the seal and every pillar is marked from the start");
    }

    // ------------------------------------------------------------ his moves

    static float Step(float seconds, Vector3 heroAt, float dt = 0.02f)
    {
        float hpBefore = P.hp;
        for (float t = 0f; t < seconds; t += dt)
        {
            Set(P, "invuln", 0f);
            P.transform.position = heroAt;
            Vector3 to = P.transform.position - K.transform.position; to.y = 0f;
            float dist = to.magnitude;
            Vector3 dir = dist > 0.001f ? to / dist : K.transform.forward;
            bool turn = true;
            object[] args = { P, dir, dist, dt, turn };
            float stun = (float)Get(K, "stun");
            if (stun > 0f) { Set(K, "stun", Mathf.Max(0f, stun - dt)); Set(K, "openT", Mathf.Max(0f, (float)Get(K, "openT") - dt)); continue; }
            Set(K, "cooldown", (float)Get(K, "cooldown") - dt);
            var vel = (Vector3)typeof(Enemy).GetMethod("TickGatekeeper", Any).Invoke(K, args);
            if ((bool)args[4]) K.transform.rotation = Quaternion.LookRotation(dir);
            K.transform.position += new Vector3(vel.x, 0f, vel.z) * dt;
            Physics.SyncTransforms();
        }
        return hpBefore - P.hp;
    }

    // Steps a charge from its wind-up until it resolves (stopped, or turned into another), so
    // what is measured afterwards (stun, damage) is the charge's own result.
    static float StepCharge(Vector3 heroAt)
    {
        float taken = 0f;
        for (int i = 0; i < 400 && (K.RushPending || K.Rushing); i++) taken += Step(0.02f, heroAt);
        return taken;
    }

    // Clears whatever move he had started, ready to choose afresh.
    static void Idle()
    {
        Set(K, "grol", Enum.Parse(typeof(Enemy).GetNestedType("GrolMove", BindingFlags.NonPublic), "None"));
        Set(K, "grolT", 0f);
        Set(K, "cooldown", 0f);
        Set(K, "stun", 0f);
        Set(K, "lungeT", 0f);
    }

    static void Awake(Vector3 keeperAt)
    {
        Fresh();
        BuildFloor5();
        foreach (var e in Enemy.All.Where(e => e != K).ToArray()) UnityEngine.Object.DestroyImmediate(e.gameObject);
        Call(G, "TickArena");
        P.Teleport(Game.ArenaCentre + new Vector3(0f, 0.1f, -4f));
        Call(G, "TickArena");
        K.transform.position = keeperAt;
        K.hp = K.maxHp;
        Set(K, "stun", 0f);     // monsters rise out of the ground when made; skip that here
        P.maxHp = P.hp = 1000f;
        Set(K, "cooldown", 0f);
        Physics.SyncTransforms();
    }

    static void Moves()
    {
        Vector3 c = Game.ArenaCentre;
        Check(Enemy.HammerWindup >= EncounterDirector.MinWindup && Enemy.SweepWindup >= EncounterDirector.MinWindup && Enemy.RushWindup >= EncounterDirector.MinWindup,
            "every move announces itself for at least " + EncounterDirector.MinWindup + " s (hammer " + Enemy.HammerWindup + ", sweep " + Enemy.SweepWindup + ", rush " + Enemy.RushWindup + ")");

        // Hammerfall and Gate Sweep, forced in turn.
        Type move = typeof(Enemy).GetNestedType("GrolMove", BindingFlags.NonPublic);
        foreach (string name in new[] { "Hammer", "Sweep" })
        {
            foreach (bool inside in new[] { true, false })
            {
                Awake(c);
                K.transform.rotation = Quaternion.LookRotation(Vector3.back);
                Vector3 hero = name == "Hammer" ? (inside ? c + Vector3.back * 2f : c + Vector3.right * 4.5f) : (inside ? c + Vector3.forward * 3.5f : c + Vector3.back * 6.2f);
                hero.y = P.transform.position.y;
                Set(K, "grol", Enum.Parse(move, name));
                Set(K, "grolT", name == "Hammer" ? Enemy.HammerWindup : Enemy.SweepWindup);
                float before = Step((name == "Hammer" ? Enemy.HammerWindup : Enemy.SweepWindup) - 0.05f, hero);
                float taken = Step(0.2f, hero);
                Check(before == 0f && (inside ? taken == 2f : taken == 0f),
                    name + (inside ? (name == "Hammer" ? " lands on a hero standing in its red disc" : " catches a hero behind him inside its ring") : (name == "Hammer" ? " misses a hero who stepped aside" : " misses a hero outside its 4.8 m ring")) + " (nothing lands during the wind-up; before=" + Num(before) + " taken=" + Num(taken) + " harm=" + G.Harm + " hero=" + P.transform.position + " keeper=" + K.transform.position + " facing=" + K.transform.forward + ")");
            }
        }

        // Phase one never charges, however far away the hero stands.
        Awake(c);
        Step(1.5f, c + Vector3.back * 9f);
        Check(!K.RushPending && !K.Rushing && K.GuardianPhase == 1, "in the first phase he walks to the hero rather than charging");

        // 60%: a roar, immunity while it lasts, then the charge enters his language.
        Awake(c);
        K.hp = K.maxHp * 0.59f;
        Step(0.05f, c + Vector3.back * 3f);
        float hp = K.hp;
        var hit = K.ReceiveHit(new DamageRequest(30f, P.transform.position, 0f, source: P));
        Check(K.GuardianPhase == 2 && K.Roaring && hit.outcome == HitOutcome.Immune && K.hp == hp, "at 60% he roars into phase two, and nothing gets through while he roars");
        Check(Enumerable.Range(0, 4).All(G.PillarMarked), "phase two points out the pillars (no intel this time)");
        Step(Enemy.PhaseRoar + 0.05f, c + Vector3.back * 3f);
        Check(!K.Roaring && K.ReceiveHit(new DamageRequest(1f, P.transform.position, 0f, source: P)).Damaged, "the roar ends and he can be hurt again");

        // A charge brought up against a pillar: the pillar shatters and he is stunned and open.
        Awake(c);
        K.hp = K.maxHp * 0.59f;
        Step(Enemy.PhaseRoar + 0.1f, c + Vector3.back * 3f);
        Vector3 pillar = c + new Vector3(Mathf.Sin(45f * Mathf.Deg2Rad), 0f, Mathf.Cos(45f * Mathf.Deg2Rad)) * Game.PillarRing;
        Vector3 behindPillar = c + (pillar - c).normalized * (Game.PillarRing + 1.6f);
        behindPillar.y = P.transform.position.y;
        Idle();
        Step(0.05f, behindPillar);
        Check(K.RushPending, "with the hero far off in phase two he winds up a charge down a telegraphed lane");
        float taken0 = StepCharge(behindPillar);
        Check(G.PillarsStanding == 3 && K.Open && (float)Get(K, "stun") > Enemy.PillarStun - 0.6f && taken0 == 0f,
            "a charge brought up against a pillar shatters it and leaves him stunned and open; the hero behind it is untouched");
        float riposte = K.hp;
        var rip = (HitResult)Call(P, "Hit", K, 10f, false, 0f, false, DamageKind.Physical, false, 0f, 0f);
        Check(rip.Damaged && riposte - K.hp >= 10f * 2.2f * 0.9f, "the stunned Gatekeeper takes a riposte (" + Num(riposte - K.hp) + " damage from a 10-point blow)");

        // Into the bars: only a short stun.
        Awake(c);
        K.hp = K.maxHp * 0.59f;
        Step(Enemy.PhaseRoar + 0.1f, c + Vector3.back * 3f);
        Vector3 east = c + Vector3.right * 9.5f; east.y = P.transform.position.y;
        Idle();
        Step(0.05f, east);
        Check(K.RushPending, "the hero off to the side draws a charge");
        // He settles his aim; then the hero steps out of the lane and the charge carries on to the bars.
        float taken1 = Step(Enemy.RushWindup - 0.15f, c + Vector3.right * 9.5f + Vector3.forward * 3f);
        taken1 += StepCharge(c + Vector3.left * 5f + Vector3.back * 2f);
        float wallStun = (float)Get(K, "stun");
        Check(G.PillarsStanding == 4 && wallStun > 0f && wallStun <= Enemy.WallStun && taken1 == 0f, "a charge into the bars stuns him only briefly (" + Num(wallStun) + " s) and breaks nothing");

        // Straight through the hero: one heavy blow, once.
        Awake(c);
        K.hp = K.maxHp * 0.59f;
        Step(Enemy.PhaseRoar + 0.1f, c + Vector3.back * 3f);
        Vector3 lane = c + Vector3.right * 7f; lane.y = P.transform.position.y;
        Idle();
        Step(0.05f, lane);
        float caught = StepCharge(lane);
        Check(caught == 2f, "a hero who stays in the lane takes one heavy blow (" + Num(caught) + ")");

        // Phase three: the bars turn him straight into a second charge; only stone stops him.
        Awake(c);
        K.hp = K.maxHp * 0.29f;
        Step(0.05f, c + Vector3.back * 3f);
        Check(K.GuardianPhase == 3, "below 30% he is enraged");
        Step(Enemy.PhaseRoar + 0.1f, c + Vector3.back * 3f);
        Set(K, "cooldown", 0f);
        Vector3 far = c + Vector3.right * 9.5f + Vector3.forward * 3f; far.y = P.transform.position.y;
        Idle();
        Step(0.05f, far);
        Check(K.RushPending, "the enraged Gatekeeper charges");
        // Step the first charge to the bars: it must come straight back as a fresh wind-up.
        bool second = false;
        for (int i = 0; i < 400; i++)
        {
            bool wasRushing = K.Rushing;
            Step(0.02f, far);
            if (wasRushing && K.RushPending) { second = true; break; }
            if (!K.RushPending && !K.Rushing) break;
        }
        Check(second && (float)Get(K, "stun") == 0f, "enraged, a charge that meets the bars turns straight into a second telegraphed charge");
    }

    // ------------------------------------------------------------ defeat, retry, boon, aftermath

    static void Defeat()
    {
        Awake(Game.ArenaCentre);
        Set(G, "waveActive", true);
        K.hp = 1f;
        K.ReceiveHit(new DamageRequest(10f, P.transform.position, 0f, source: P));
        Check(K.Dead || !K.CombatEligible, "Grol falls");
        if (Enemy.All.Contains(K)) Enemy.All.Remove(K);
        Call(G, "FloorCleared");
        Check(G.Arena == Game.ArenaState.Won && !G.SouthGateShut && !G.NorthGateShut && !ChapterProgress.GatekeeperCheckpoint && Save.TowerFloor == 5,
            "his fall opens both gates, clears floor 5 and spends the checkpoint");
        Check(Save.PendingGuardian != null && Save.PendingGuardian.choices.Contains(Upgrades.GatebreakerId), "the Guardian's Boon offer waits, with Gatebreaker among it");
        var lines = Story.FloorClear(5);
        Check(lines != null && lines.Any(l => l.text.Contains("Grol")) && lines.Any(l => l.text.Contains("bars")), "floor 5's clear tells what became of the Gatekeeper and his gate");
    }

    static void Retry()
    {
        Fresh();
        ChapterProgress.ReachGatekeeper(ChapterProgress.GatekeeperToken);
        BuildFloor5();
        Vector3 arrival = (Vector3)Call(G, "FloorArrival", 5);
        Check(Enemy.All.Count == 1 && Enemy.All[0] == K && K.Dormant, "a retry rebuilds floor 5 with only the Gatekeeper: its packs stay beaten and pay nothing again");
        Check(Vector3.Distance(arrival, Game.ArenaCentre) < Game.KeeperArenaRadius + 4f && arrival.z < Game.ArenaCentre.z, "the hero arrives at his south gate (" + arrival + ")");
        Call(G, "TickArena");
        Check(G.Arena == Game.ArenaState.Open && !G.SouthGateShut, "the gate stands open for the retry");
        Check(Save.TowerFloor == 4 && Save.Sparks == 100, "the retry grants no floor and no Sparks");
        Fresh();
        Check((Vector3)Call(G, "FloorArrival", 5) == (Vector3)typeof(Game).GetField("FloorStart", Any).GetValue(null), "without a checkpoint floor 5 starts at its foot as before");
    }

    static void Gatebreaker()
    {
        foreach (bool owned in new[] { false, true })
        {
            Fresh();
            BuildFloor5();
            foreach (var e in Enemy.All.ToArray()) UnityEngine.Object.DestroyImmediate(e.gameObject);
            var brute = Enemy.Create(Enemy.Kind.Brute, P.transform.position + Vector3.forward * 1.6f, 0);
            brute.hp = brute.maxHp = 5000f;
            Set(brute, "stun", 0f);
            P.transform.rotation = Quaternion.identity;
            P.stats.gatebreaker = owned;
            Physics.SyncTransforms();
            Call(P, "Hit", brute, 10f, false, 0f, true, DamageKind.Physical, false, 0f, 0f);
            Check(brute.Open == owned, owned ? "with Gatebreaker a heavy blow staggers a brute open" : "without it the same heavy blow does not");
        }
    }

    static void Aftermath()
    {
        Fresh(5);
        var plain = Story.GatekeeperAftermath();
        JourneyStore.Transaction(() => { JourneyStore.SetInt("consequence:rescue:rhea:guardian-intel", 1); Save.SetBoon(Upgrades.GatebreakerId, 1); });
        var rich = Story.GatekeeperAftermath();
        Check(plain.Length >= 2 && rich.Length == plain.Length + 2 && rich.Any(l => l.text.Contains("Vane")), "the gate sergeant remembers what the hero carries and who told him how");
        var rhea = Story.RescueTown(Story.RescueIndex("rhea"));
        Check(rhea.Any(l => l.text.Contains("into the stone")), "Rhea knows her warning helped, once Grol has fallen");
        Fresh(4);
        JourneyStore.Transaction(() => JourneyStore.SetInt("consequence:rescue:rhea:guardian-intel", 1));
        Check(!Story.RescueTown(Story.RescueIndex("rhea")).Any(l => l.text.Contains("into the stone")), "before he falls she only repeats her warning");
        Check(Story.Rescues[0].sightings[1].Contains("cannot turn"), "the intel Rhea gives on floor 4 is the charge's weakness");
    }

    // ------------------------------------------------------------ captures

    static void Shot(string path, Vector3 at, Vector3 look)
    {
        var go = new GameObject("Guardian evidence camera");
        var cam = go.AddComponent<Camera>();
        cam.transform.position = at;
        cam.transform.LookAt(look);
        cam.fieldOfView = 55f;
        cam.farClipPlane = 300f;
        var rt = new RenderTexture(1200, 675, 24);
        var tex = new Texture2D(1200, 675, TextureFormat.RGB24, false);
        var previous = RenderTexture.active;
        try
        {
            cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, 1200, 675), 0, 0); tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous; cam.targetTexture = null; rt.Release();
            UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(tex); UnityEngine.Object.DestroyImmediate(go);
        }
    }

    static int stage;
    static double nextAt;

    static void CaptureStage()
    {
        Vector3 c = Game.ArenaCentre;
        switch (stage)
        {
            case 10:
                Fresh();
                BuildFloor5();
                foreach (var e in Enemy.All.Where(e => e != K).ToArray()) UnityEngine.Object.DestroyImmediate(e.gameObject);
                Call(G, "TickArena");
                P.Teleport(c + new Vector3(-3f, 0.1f, -6f));
                Call(G, "TickArena");
                K.hp = K.maxHp * 0.59f;
                Set(K, "cooldown", 0f);
                G.enabled = false; P.enabled = false;
                stage = 11; nextAt = EditorApplication.timeSinceStartup + 1.6; return;
            case 11:
                Shot("Logs/guardian-arena-over.png", c + new Vector3(0f, 26f, -16f), c);
                Shot("Logs/guardian-arena-gate.png", c + new Vector3(4f, 3.2f, -19f), c + Vector3.up * 1.5f);
                P.transform.position = c + (new Vector3(Mathf.Sin(225f * Mathf.Deg2Rad), 0f, Mathf.Cos(225f * Mathf.Deg2Rad)) * (Game.PillarRing + 1.6f)) + Vector3.up * 0.1f;
                Set(K, "cooldown", 0f);
                stage = 12; nextAt = EditorApplication.timeSinceStartup + 2.2; return;
            case 12:
                Shot("Logs/guardian-rush.png", P.transform.position + new Vector3(-3f, 3f, -5f), K.transform.position + Vector3.up * 1.5f);
                stage = 13; nextAt = EditorApplication.timeSinceStartup + 2.0; return;
            case 13:
                Shot("Logs/guardian-pillar.png", c + new Vector3(-12f, 7f, -14f), c + new Vector3(-3f, 1f, -3f));
                // The aftermath in Brushwick: Grol's bars by the tower door, and the gate sergeant.
                Fresh(5);
                Call(G, "BuildHub");
                Call(G, "SetState", Game.GameState.Hub);
                stage = 14; nextAt = EditorApplication.timeSinceStartup + 1.5; return;
            case 14:
            {
                Vector3 trophy = new Vector3(Mathf.Sin(18f * Mathf.Deg2Rad), 0f, Mathf.Cos(18f * Mathf.Deg2Rad)) * 23f;
                Shot("Logs/guardian-town-trophy.png", trophy * 0.45f + new Vector3(-3f, 2.4f, 0f), trophy + Vector3.up * 1f);
                Check(GameObject.Find("Gatekeeper's bars") != null, "Brushwick hangs the Gatekeeper's bars by the tower door once floor 5 is cleared");
                stage = 20; return;
            }
        }
    }

    // ------------------------------------------------------------ runner

    static GuardianTest()
    {
        if (SessionState.GetBool(Session, false)) { EditorApplication.update += Tick; Application.logMessageReceived += OnLog; }
    }

    public static void Run()
    {
        Save.Wipe();
        Save.SeenIntro = true;
        Save.Flush();
        EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        SessionState.SetBool(Session, true);
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        Application.logMessageReceived -= OnLog;
        Application.logMessageReceived += OnLog;
        EditorApplication.EnterPlaymode();
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        if (readyAt == 0) { readyAt = EditorApplication.timeSinceStartup + 2; deadline = readyAt + 300; }
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Guardian test timed out at stage " + stage);
            if (G == null || P == null || EditorApplication.timeSinceStartup < readyAt || EditorApplication.timeSinceStartup < nextAt) return;
            Application.runInBackground = true;
            if (stage == 0)
            {
                Records(); Boon(); Arena(); Moves(); Defeat(); Retry(); Gatebreaker(); Aftermath();
                stage = 10;
                return;
            }
            if (stage < 20) { CaptureStage(); return; }
            Check(errors.Count == 0, "the Guardian runtime logged no unexpected errors" + (errors.Count > 0 ? ": " + errors[0] : ""));
            Fresh();
            Check(JourneyStore.Error == null, "the Guardian scenarios leave the journey store healthy");
            Debug.Log("GUARDIAN DONE " + passed + " checks passed");
            Finish(0);
        }
        catch (Exception e)
        {
            Debug.LogError("GUARDIAN FAILED stage=" + stage + " " + e);
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
