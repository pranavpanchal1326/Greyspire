using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Phase nine, package C02: coordinated enemy roles. The first half drives roles, the director,
// the caller and the shield carrier directly; the second half runs two real 30-second mixed
// fights through the enemies' own Update, one coordinated and one not, and compares them.
[InitializeOnLoad]
public static class EncounterRoleTest
{
    const string Session = "GreyspireEncounterRoleTest";
    const BindingFlags Any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static readonly Vector3 Origin = new Vector3(1000f, 0.1f, 1000f);
    static int passed;
    static double readyAt, deadline;
    static Game G => Game.I;
    static PlayerController P => G.Player;
    static GameObject ground;
    static object Call(object host, string method, params object[] args) => host.GetType().GetMethod(method, Any).Invoke(host, args);
    static void Set(object host, string field, object value) => host.GetType().GetField(field, Any).SetValue(host, value);
    static object Get(object host, string field) => host.GetType().GetField(field, Any).GetValue(host);
    static string Num(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);
    static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        passed++;
        Debug.Log("ROLES PASS " + message);
    }

    static void Clear()
    {
        foreach (var e in Enemy.All.ToArray()) UnityEngine.Object.DestroyImmediate(e.gameObject);
        foreach (var shot in UnityEngine.Object.FindObjectsByType<Projectile>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            UnityEngine.Object.DestroyImmediate(shot.gameObject);
        EncounterDirector.Reset();
        EncounterDirector.Enabled = true;
        EncounterDirector.ClockOverride = 0f;
    }

    static void Stage()
    {
        Clear();
        ((IList)Get(G, "timers")).Clear();
        G.enabled = P.enabled = false;
        Set(G, "<DefeatPending>k__BackingField", false);
        Set(G, "<InTown>k__BackingField", false);
        Set(G, "<InWilds>k__BackingField", false);
        Set(G, "level", 0); Set(G, "wave", 0); Set(G, "fadeDir", 0f);
        Call(G, "SetState", Game.GameState.Playing);
        P.stats = new PlayerStats();
        P.Revive(Origin, true);
        P.transform.rotation = Quaternion.identity;
        Set(P, "invuln", 0f);
        if (ground == null)
        {
            ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Encounter test ground";
            ground.transform.position = Origin - Vector3.up * 0.6f;
            ground.transform.localScale = new Vector3(80f, 1f, 80f);
        }
        // The camera stands behind the hero looking the way the hero faces, and stays there.
        G.Cam.enabled = false;
        G.Cam.transform.position = Origin + new Vector3(0f, 3.2f, -6.5f);
        G.Cam.transform.rotation = Quaternion.LookRotation(new Vector3(0f, -0.25f, 1f));
        Juice.Paused = false;
        Juice.Reset();
        Time.timeScale = 1f;
        Physics.SyncTransforms();
    }

    static Enemy Spawn(Enemy.Kind kind, Vector3 offset, bool bandit = false, bool elite = false)
    {
        var e = Enemy.Create(kind, Origin + offset, 0, elite, bandit);
        Set(e, "stun", 0f);
        Physics.SyncTransforms();
        return e;
    }

    // ------------------------------------------------------------ synchronous scenarios

    static void Roles()
    {
        Stage();
        var cases = new (Enemy.Kind kind, bool bandit, bool elite, EnemyRole role)[]
        {
            (Enemy.Kind.Minion, false, false, EnemyRole.Duelist),
            (Enemy.Kind.Minion, true, true, EnemyRole.ShieldCarrier),
            (Enemy.Kind.Slime, false, false, EnemyRole.Skirmisher),
            (Enemy.Kind.Spitter, false, false, EnemyRole.Artillery),
            (Enemy.Kind.Bomber, false, false, EnemyRole.Sapper),
            (Enemy.Kind.Brute, false, false, EnemyRole.Heavy),
            (Enemy.Kind.Boss, false, false, EnemyRole.Guardian),
        };
        float x = -12f;
        foreach (var c in cases)
        {
            var e = Spawn(c.kind, new Vector3(x += 3.5f, 0f, 30f), c.bandit, c.elite);
            Check(e.Role == c.role, c.kind + (c.bandit ? " (bandit" + (c.elite ? ", shield" : "") + ")" : "") + " takes the " + c.role + " role");
        }
        Check(EncounterDirector.Weight(EnemyRole.Heavy) == 2 && EncounterDirector.Weight(EnemyRole.Sapper) == 2 && EncounterDirector.Weight(EnemyRole.Duelist) == 1,
            "a heavy slam and a sapper's blast weigh twice an ordinary attack");
    }

    static void Director()
    {
        Stage();
        int budget = EncounterDirector.Budget;
        Check(budget == 2, "the first floors allow two attack weights at once (budget " + budget + ")");
        var a = Spawn(Enemy.Kind.Minion, new Vector3(-2f, 0f, 3f));
        var b = Spawn(Enemy.Kind.Minion, new Vector3(2f, 0f, 3f));
        var c = Spawn(Enemy.Kind.Slime, new Vector3(0f, 0f, 4f));
        var brute = Spawn(Enemy.Kind.Brute, new Vector3(0f, 0f, 6f));
        EncounterDirector.ClockOverride = 10f;
        Check(EncounterDirector.TryCommit(a, a.Role), "the first attacker commits");
        EncounterDirector.ClockOverride = 10.1f;
        Check(!EncounterDirector.TryCommit(b, b.Role), "a second attack cannot start within " + EncounterDirector.MinGap + " s of the first");
        EncounterDirector.ClockOverride = 10.4f;
        Check(EncounterDirector.TryCommit(b, b.Role) && EncounterDirector.Committed == 2, "after the gap a second attacker commits");
        EncounterDirector.ClockOverride = 10.8f;
        Check(!EncounterDirector.TryCommit(c, c.Role), "a third waits while the budget is full");
        Check(!EncounterDirector.Allows(c, c.Role), "the director reports the wait without granting anything");
        EncounterDirector.Release(a);
        Check(!EncounterDirector.TryCommit(brute, brute.Role), "a heavy slam (weight 2) waits while one attack is still committed");
        Check(EncounterDirector.TryCommit(c, c.Role), "a released slot goes to the next attacker");
        var sapper = Spawn(Enemy.Kind.Bomber, new Vector3(3f, 0f, 2f));
        EncounterDirector.ClockOverride = 10.85f;
        Check(EncounterDirector.TryCommit(sapper, sapper.Role, true) && EncounterDirector.Committed == 4,
            "a lit fuse always burns, but it occupies the budget so others hold back");
        EncounterDirector.Release(b); EncounterDirector.Release(c);
        EncounterDirector.ClockOverride = 12f;
        Check(!EncounterDirector.TryCommit(a, a.Role), "nothing else commits while the sapper's weight fills the budget");
        EncounterDirector.Release(sapper);
        UnityEngine.Object.DestroyImmediate(sapper.gameObject);

        // Unseen attackers: behind the camera.
        Stage();
        var behind1 = Spawn(Enemy.Kind.Minion, new Vector3(-1.5f, 0f, -9f));
        var behind2 = Spawn(Enemy.Kind.Minion, new Vector3(1.5f, 0f, -9f));
        var front = Spawn(Enemy.Kind.Minion, new Vector3(0f, 0f, 4f));
        Check(EncounterDirector.Unseen(behind1) && EncounterDirector.Unseen(behind2) && !EncounterDirector.Unseen(front), "enemies behind the camera count as unseen; one in view does not");
        EncounterDirector.ClockOverride = 20f;
        Check(EncounterDirector.TryCommit(behind1, behind1.Role) && EncounterDirector.CommittedUnseen(behind1), "one unseen attacker may commit");
        EncounterDirector.ClockOverride = 20.5f;
        Check(!EncounterDirector.TryCommit(behind2, behind2.Role), "a second unseen attacker waits while one is coming");
        Check(EncounterDirector.TryCommit(front, front.Role), "an attacker in view may still commit alongside it");

        // An unseen commitment waits longer before it lands.
        Stage();
        var slime = Spawn(Enemy.Kind.Slime, new Vector3(0f, 0f, -9f));
        EncounterDirector.ClockOverride = 30f;
        Set(slime, "cooldown", 0f);
        Call(slime, "TickSlime", P, Vector3.back, 1.5f, 0.016f, Vector3.one, true);
        float unseenWindup = (float)Get(slime, "windup");
        Stage();
        slime = Spawn(Enemy.Kind.Slime, new Vector3(0f, 0f, 3f));
        EncounterDirector.ClockOverride = 40f;
        Set(slime, "cooldown", 0f);
        Call(slime, "TickSlime", P, Vector3.back, 1.5f, 0.016f, Vector3.one, true);
        float seenWindup = (float)Get(slime, "windup");
        Check(Mathf.Abs(unseenWindup - seenWindup - EncounterDirector.UnseenExtraWindup) < 0.001f && seenWindup >= EncounterDirector.MinWindup,
            "a pounce from out of sight winds up " + Num(unseenWindup) + " s against " + Num(seenWindup) + " s in view");

        // Control: with coordination off, everyone commits at once.
        Stage();
        EncounterDirector.Enabled = false;
        var many = Enumerable.Range(0, 5).Select(i => Spawn(Enemy.Kind.Minion, new Vector3(i - 2f, 0f, 3f))).ToList();
        EncounterDirector.ClockOverride = 50f;
        Check(many.All(e => EncounterDirector.TryCommit(e, e.Role)) && EncounterDirector.Committed == 5, "the uncoordinated control lets all five commit together");
        EncounterDirector.Enabled = true;
    }

    static void Caller()
    {
        Stage();
        var t1 = Spawn(Enemy.Kind.Spitter, new Vector3(-2f, 0f, 9f));
        var t2 = Spawn(Enemy.Kind.Spitter, new Vector3(2f, 0f, 9f));
        var imp = Spawn(Enemy.Kind.Minion, new Vector3(0f, 0f, 6f));
        var far = Spawn(Enemy.Kind.Minion, new Vector3(0f, 0f, 9f + Enemy.CallRadius + 4f));
        foreach (var e in new[] { t1, t2, imp }) Set(e, "alerted", true);
        Call(t1, "OrganisePack");
        Call(t2, "OrganisePack");
        var callers = new[] { t1, t2 }.Where(e => e.Role == EnemyRole.Caller).ToList();
        string first = string.CompareOrdinal(t1.SpawnId, t2.SpawnId) < 0 ? t1.SpawnId : t2.SpawnId;
        Check(callers.Count == 1 && callers[0].SpawnId == first, "a waking pack of three gets exactly one caller, chosen by stable identity");
        Enemy caller = callers[0], other = caller == t1 ? t2 : t1;

        Stage();
        var lone1 = Spawn(Enemy.Kind.Spitter, new Vector3(-2f, 0f, 9f));
        var lone2 = Spawn(Enemy.Kind.Minion, new Vector3(2f, 0f, 9f));
        Set(lone1, "alerted", true); Set(lone2, "alerted", true);
        Call(lone1, "OrganisePack");
        Check(lone1.Role == EnemyRole.Artillery, "a pair is too small to need a caller");

        // A completed call rallies the pack inside its radius, and only them.
        Stage();
        caller = Spawn(Enemy.Kind.Spitter, new Vector3(0f, 0f, 9f));
        imp = Spawn(Enemy.Kind.Minion, new Vector3(3f, 0f, 8f));
        var imp2 = Spawn(Enemy.Kind.Minion, new Vector3(-3f, 0f, 8f));
        far = Spawn(Enemy.Kind.Minion, new Vector3(0f, 0f, 9f + Enemy.CallRadius + 4f));
        foreach (var e in new[] { caller, imp, imp2 }) Set(e, "alerted", true);
        Call(caller, "OrganisePack");
        Check(caller.Role == EnemyRole.Caller, "the pack's thrower becomes its caller");
        Set(caller, "callCd", 0f);
        Check((bool)Call(caller, "TickCaller", 0.016f) && caller.Calling, "the caller begins a signal with a visible, audible wind-up");
        float waited = 0f;
        while (caller.Calling && waited < 3f) { Call(caller, "TickCaller", 0.05f); waited += 0.05f; }
        Check(Mathf.Abs(waited - Enemy.CallWindup) < 0.06f, "the signal takes " + Num(waited) + " s to complete");
        Check(imp.Rallied && imp2.Rallied && !far.Rallied && !caller.Rallied, "the completed call rallies allies within " + Enemy.CallRadius + " m and nobody further");
        Set(imp, "rallyT", 0.01f);
        Call(imp, "TickRoles", 0.02f);
        Check(!imp.Rallied, "a rally wears off");

        // Hitting the caller during the signal breaks it.
        Stage();
        caller = Spawn(Enemy.Kind.Spitter, new Vector3(0f, 0f, 4f));
        imp = Spawn(Enemy.Kind.Minion, new Vector3(3f, 0f, 4f));
        imp2 = Spawn(Enemy.Kind.Minion, new Vector3(-3f, 0f, 4f));
        foreach (var e in new[] { caller, imp, imp2 }) Set(e, "alerted", true);
        Call(caller, "OrganisePack");
        Set(caller, "callCd", 0f);
        Call(caller, "TickCaller", 0.016f);
        Call(caller, "TickCaller", 0.5f);
        caller.ReceiveHit(new DamageRequest(1f, P.transform.position, 0f, source: P));
        Check(!caller.Calling, "a hit landing during the signal breaks it");
        for (int i = 0; i < 40; i++) Call(caller, "TickCaller", 0.05f);
        Check(!imp.Rallied && !imp2.Rallied, "a broken signal rallies no one");
        Check((float)Get(caller, "callCd") > 0f, "a broken caller has to wait before trying again");
    }

    static void ShieldCarrier()
    {
        Stage();
        var carrier = Spawn(Enemy.Kind.Minion, new Vector3(0f, 0f, 1.6f), bandit: true, elite: true);
        carrier.hp = carrier.maxHp = 10000f;
        carrier.transform.rotation = Quaternion.LookRotation(Vector3.back);
        Set(carrier, "cooldown", 1000f);
        Check(carrier.Role == EnemyRole.ShieldCarrier, "the shielded bandit is a shield carrier");
        EncounterDirector.ClockOverride = 60f;
        for (int i = 0; i < Enemy.BlocksBeforeCounter - 1; i++)
        {
            Set(carrier, "guarding", true);
            Check(carrier.ReceiveHit(new DamageRequest(1f, P.transform.position, 0f, source: P)).outcome == HitOutcome.Blocked, "frontal light blow " + (i + 1) + " glances off the shield");
        }
        Check(!carrier.Countering, "two blocked blows are not yet a pattern");
        Set(carrier, "guarding", true);
        carrier.ReceiveHit(new DamageRequest(1f, P.transform.position, 0f, source: P));
        float bash = (float)Get(carrier, "bashWind");
        Check(carrier.Countering && bash >= Enemy.CounterWindup - 0.001f, "the third frontal light blow brings a telegraphed counter-bash (" + Num(bash) + " s warning)");
        float hp = P.hp;
        Set(P, "invuln", 0f);
        for (int i = 0; i < 20 && carrier.Countering; i++) Call(carrier, "TickCounter", P, Vector3.back, 1.6f, 0.05f);
        Check(!carrier.Countering && P.hp == hp - 1f, "the counter lands once on a hero who kept attacking from the front");
        // The adaptation is bounded: it cannot answer again while its counter is recovering.
        EncounterDirector.ClockOverride = 61f;
        for (int i = 0; i < Enemy.BlocksBeforeCounter; i++)
        {
            Set(carrier, "guarding", true);
            carrier.ReceiveHit(new DamageRequest(1f, P.transform.position, 0f, source: P));
        }
        Check(!carrier.Countering, "the counter cannot be repeated inside its " + Enemy.CounterCooldown + " s recovery");
        // The window forgets: blows spread out over time are not a pattern.
        Set(carrier, "counterCd", 0f);
        Set(carrier, "blockedRun", 0);
        for (int i = 0; i < Enemy.BlocksBeforeCounter; i++)
        {
            Set(carrier, "guarding", true);
            carrier.ReceiveHit(new DamageRequest(1f, P.transform.position, 0f, source: P));
            Call(carrier, "TickRoles", Enemy.BlockWindow + 0.1f);
        }
        Check(!carrier.Countering, "blocked blows spread beyond the " + Enemy.BlockWindow + " s window bring no counter");
        // Heavy blows break the guard rather than feeding the counter.
        Set(carrier, "blockedRun", 0);
        Set(carrier, "guarding", true);
        var heavy = carrier.ReceiveHit(new DamageRequest(1f, P.transform.position, 0f, true, source: P));
        Check(heavy.guardBroken && carrier.BlockedRun == 0, "a heavy blow breaks the guard and does not count towards a counter");

        // Screening: the carrier's post lies on the line between the hero and its thrower.
        Stage();
        carrier = Spawn(Enemy.Kind.Minion, new Vector3(1f, 0f, 5f), bandit: true, elite: true);
        var thrower = Spawn(Enemy.Kind.Spitter, new Vector3(0f, 0f, 12f));
        var ward = (Enemy)Call(carrier, "ScreenedAlly");
        Vector3 post = (Vector3)Call(carrier, "ScreenPoint", P, thrower);
        Vector3 lane = (thrower.transform.position - P.transform.position); lane.y = 0f;
        Vector3 rel = post - P.transform.position; rel.y = 0f;
        Check(ward == thrower && Vector3.Cross(lane.normalized, rel).magnitude < 0.01f && Vector3.Dot(lane, rel) > 0f && rel.magnitude < lane.magnitude,
            "a shield carrier's post holds the lane between the hero and the thrower (" + Num(rel.magnitude) + " m out)");
    }

    // ------------------------------------------------------------ real fights

    struct FightStats { public int maxCommitted, maxUnseen, hits, simultaneous, starved, attacks; public float worstSecond, waitingSpeed; public HashSet<string> attackers; }

    static readonly (Enemy.Kind kind, Vector3 at, bool bandit, bool elite)[] Pack =
    {
        (Enemy.Kind.Minion, new Vector3(-3f, 0f, 7f), false, false),
        (Enemy.Kind.Minion, new Vector3(3f, 0f, 7f), false, false),
        (Enemy.Kind.Minion, new Vector3(0f, 0f, 9f), true, true),
        (Enemy.Kind.Slime, new Vector3(-6f, 0f, 2f), false, false),
        (Enemy.Kind.Slime, new Vector3(6f, 0f, -3f), false, false),
        (Enemy.Kind.Spitter, new Vector3(0f, 0f, 13f), false, false),
        (Enemy.Kind.Spitter, new Vector3(-5f, 0f, 12f), false, false),
        (Enemy.Kind.Brute, new Vector3(4f, 0f, 10f), false, false),
    };

    const float FightSeconds = 30f;
    static int stage;
    static float fightClock;
    static List<Enemy> fighters;
    static FightStats current, coordinated, uncoordinated;
    static readonly List<float> hitTimes = new List<float>();
    static float lastHp;
    static int lastLog;
    static readonly Dictionary<Enemy, Vector3> lastSeenAt = new Dictionary<Enemy, Vector3>();
    static float waitingTime;
    static readonly StringBuilder csv = new StringBuilder("run,time,committed,unseen,hit\n");

    static void BeginFight(bool coordinate)
    {
        Stage();
        EncounterDirector.ClockOverride = -1f;
        EncounterDirector.Enabled = coordinate;
        P.maxHp = P.hp = 100000f;
        lastHp = P.hp;
        fighters = Pack.Select(p => Spawn(p.kind, p.at, p.bandit, p.elite)).ToList();
        foreach (var e in fighters) { e.hp = e.maxHp = 100000f; e.aggro = 60f; }
        current = new FightStats { attackers = new HashSet<string>() };
        hitTimes.Clear();
        lastSeenAt.Clear();
        waitingTime = 0f;
        lastLog = EncounterDirector.Log.Count;
        fightClock = 0f;
        G.enabled = false;
    }

    // One rendered frame of a fight: the enemies run on their own Update; this only watches.
    static bool FightFrame(string run)
    {
        float dt = Time.deltaTime;
        fightClock += dt;
        Set(P, "invuln", 0f);
        P.transform.position = Origin;
        int committed = EncounterDirector.Committed, unseen = EncounterDirector.UnseenCommitted;
        current.maxCommitted = Mathf.Max(current.maxCommitted, committed);
        current.maxUnseen = Mathf.Max(current.maxUnseen, unseen);
        bool hit = P.hp < lastHp;
        if (hit) { current.hits++; hitTimes.Add(fightClock); }
        P.hp = lastHp = 100000f;
        for (int i = lastLog; i < EncounterDirector.Log.Count; i++) { current.attacks++; current.attackers.Add(EncounterDirector.Log[i].spawnId); }
        lastLog = EncounterDirector.Log.Count;
        // How much the ones not attacking keep moving: waiting should be repositioning, not standing in a heap.
        foreach (var e in fighters)
        {
            if (e == null || !e.CombatEligible) continue;
            Vector3 now = e.transform.position;
            if (!EncounterDirector.Holds(e) && lastSeenAt.TryGetValue(e, out Vector3 was))
                current.waitingSpeed += new Vector2(now.x - was.x, now.z - was.z).magnitude;
            lastSeenAt[e] = now;
        }
        waitingTime += dt * fighters.Count(e => e != null && e.CombatEligible && !EncounterDirector.Holds(e));
        csv.AppendLine(string.Join(",", run, Num(fightClock), committed, unseen, hit ? 1 : 0));
        // Review frames from the coordinated fight: the gameplay view and an overview.
        if (run == "coordinated")
            foreach (float at in new[] { 6f, 12f, 18f, 24f })
                if (fightClock - dt < at && fightClock >= at)
                {
                    Shot("Logs/encounter-" + at + "s-view.png", G.Cam.transform.position, G.Cam.transform.rotation);
                    Shot("Logs/encounter-" + at + "s-over.png", Origin + new Vector3(0f, 22f, -8f), Quaternion.LookRotation(new Vector3(0f, -1f, 0.45f)));
                }
        if (fightClock < FightSeconds) return false;
        for (int i = 0; i < hitTimes.Count; i++)
        {
            int inSecond = hitTimes.Count(t => t >= hitTimes[i] && t < hitTimes[i] + 1f);
            current.worstSecond = Mathf.Max(current.worstSecond, inSecond);
            if (i > 0 && hitTimes[i] - hitTimes[i - 1] < 0.1f) current.simultaneous++;
        }
        current.waitingSpeed = waitingTime > 0f ? current.waitingSpeed / waitingTime : 0f;
        var starved = fighters.Where(e => e != null && !current.attackers.Contains(e.SpawnId)).ToList();
        current.starved = starved.Count;
        if (starved.Count > 0) Debug.Log("ROLES STARVED " + run + ": " + string.Join(", ", starved.Select(e => e.Role.ToString())));
        Debug.Log("ROLES FIGHT " + run + " maxCommitted=" + current.maxCommitted + " maxUnseen=" + current.maxUnseen + " attacks=" + current.attacks + " hits=" + current.hits
            + " simultaneous=" + current.simultaneous + " worstSecond=" + Num(current.worstSecond) + " starved=" + current.starved + " waitingSpeed=" + Num(current.waitingSpeed));
        return true;
    }

    static void Shot(string path, Vector3 at, Quaternion rot)
    {
        var go = new GameObject("Encounter evidence camera");
        var cam = go.AddComponent<Camera>();
        cam.transform.SetPositionAndRotation(at, rot);
        cam.fieldOfView = 55f;
        var rt = new RenderTexture(960, 540, 24);
        var tex = new Texture2D(960, 540, TextureFormat.RGB24, false);
        var previous = RenderTexture.active;
        try
        {
            cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous; cam.targetTexture = null; rt.Release();
            UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(tex); UnityEngine.Object.DestroyImmediate(go);
        }
    }

    static void JudgeFights()
    {
        int budget = EncounterDirector.Budget;
        Check(coordinated.maxCommitted <= budget + EncounterDirector.Weight(EnemyRole.Sapper) - 1 && coordinated.maxCommitted <= budget,
            "in a 30 s mixed fight of eight, commitments never exceeded the budget (peak " + coordinated.maxCommitted + " of " + budget + ")");
        Check(uncoordinated.maxCommitted > budget, "without coordination the same fight peaked at " + uncoordinated.maxCommitted + " commitments");
        Check(coordinated.maxUnseen <= EncounterDirector.MaxUnseen, "never more than one attack at a time came from out of sight");
        Check(coordinated.simultaneous < uncoordinated.simultaneous || uncoordinated.simultaneous == 0 && coordinated.simultaneous == 0,
            "hits landing together (within 0.1 s): " + coordinated.simultaneous + " coordinated against " + uncoordinated.simultaneous + " uncoordinated");
        // A commitment cycle (wind-up, strike, release) takes at least about half a second, so each
        // budget slot can land at most two blows a second.
        Check(coordinated.worstSecond <= budget * 2 && coordinated.worstSecond < uncoordinated.worstSecond,
            "the worst single second dealt " + Num(coordinated.worstSecond) + " hits coordinated (bound " + budget * 2 + ") against " + Num(uncoordinated.worstSecond));
        Check(coordinated.hits >= 8, "the coordinated fight is still a threat: " + coordinated.hits + " hits on a hero who never moved in 30 s");
        Check(coordinated.starved == 0, "every role got its turn: all eight fighters committed at least once");
        Check(coordinated.waitingSpeed > 0.3f, "enemies waiting their turn kept repositioning (" + Num(coordinated.waitingSpeed) + " m/s on average)");
        Directory.CreateDirectory("Reports");
        File.WriteAllText("Reports/encounter-fights.csv", csv.ToString());
        File.WriteAllText("Reports/encounter-summary.csv", "run,max_committed,max_unseen,attacks,hits,simultaneous,worst_second,starved,waiting_speed\n"
            + Row("coordinated", coordinated) + Row("uncoordinated", uncoordinated));
    }

    static string Row(string run, FightStats s) => string.Join(",", run, s.maxCommitted, s.maxUnseen, s.attacks, s.hits, s.simultaneous, Num(s.worstSecond), s.starved, Num(s.waitingSpeed)) + "\n";

    // ------------------------------------------------------------ runner

    static EncounterRoleTest()
    {
        if (SessionState.GetBool(Session, false)) EditorApplication.update += Tick;
    }

    public static void Run()
    {
        Save.Wipe();
        Save.Level = 12;
        Save.StatPoints = 33;
        Save.SeenIntro = true;
        Save.Flush();
        EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        SessionState.SetBool(Session, true);
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        EditorApplication.EnterPlaymode();
    }

    static int lastFrame = -1;

    static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        if (readyAt == 0) { readyAt = EditorApplication.timeSinceStartup + 2; deadline = readyAt + 300; }
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Encounter role test timed out at stage " + stage);
            if (G == null || P == null || EditorApplication.timeSinceStartup < readyAt) return;
            if (Time.frameCount == lastFrame) return;
            lastFrame = Time.frameCount;
            Application.runInBackground = true;
            switch (stage)
            {
                case 0:
                    Save.BeginExpedition(false);
                    Roles(); Director(); Caller(); ShieldCarrier();
                    BeginFight(true); stage = 1; return;
                case 1:
                    if (!FightFrame("coordinated")) return;
                    coordinated = current;
                    BeginFight(false); stage = 2; return;
                case 2:
                    if (!FightFrame("uncoordinated")) return;
                    uncoordinated = current;
                    EncounterDirector.Enabled = true;
                    JudgeFights();
                    break;
            }
            Clear();
            EncounterDirector.ClockOverride = -1f;
            Check(JourneyStore.Error == null, "encounter scenarios leave the journey store healthy");
            Debug.Log("ROLES DONE " + passed + " checks passed");
            Finish(0);
        }
        catch (Exception e)
        {
            Debug.LogError("ROLES FAILED stage=" + stage + " " + e);
            Finish(1);
        }
    }

    static void Finish(int code)
    {
        EncounterDirector.Enabled = true;
        EncounterDirector.ClockOverride = -1f;
        SessionState.SetBool(Session, false);
        EditorApplication.update -= Tick;
        EditorApplication.Exit(code);
    }
}
