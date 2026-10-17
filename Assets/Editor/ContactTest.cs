using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class ContactTest
{
    const string Session = "GreyspireContactTest";
    const BindingFlags Any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static readonly Vector3 Origin = new Vector3(1000f, 0.1f, 1000f);
    static readonly List<GameObject> obstacles = new List<GameObject>();
    static int passed;
    static double readyAt, deadline;
    static Game G => Game.I;
    static PlayerController P => G.Player;
    static object Call(object host, string method, params object[] args) => host.GetType().GetMethod(method, Any).Invoke(host, args);
    static void Set(object host, string field, object value) => host.GetType().GetField(field, Any).SetValue(host, value);
    static object Get(object host, string field) => host.GetType().GetField(field, Any).GetValue(host);
    static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        passed++;
        Debug.Log("CONTACT PASS " + message);
    }
    static GameObject Box(Vector3 centre, Vector3 size)
    {
        var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = "Contact test obstacle";
        box.transform.position = centre;
        box.transform.localScale = size;
        obstacles.Add(box);
        Physics.SyncTransforms();
        return box;
    }
    static void Reset()
    {
        foreach (var enemy in Enemy.All.ToArray()) UnityEngine.Object.DestroyImmediate(enemy.gameObject);
        foreach (var shot in UnityEngine.Object.FindObjectsByType<Projectile>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            UnityEngine.Object.DestroyImmediate(shot.gameObject);
        foreach (var obstacle in obstacles) if (obstacle != null) UnityEngine.Object.DestroyImmediate(obstacle);
        obstacles.Clear();
        ((IList)Get(G, "timers")).Clear();
        G.enabled = P.enabled = false;
        Set(G, "<DefeatPending>k__BackingField", false);
        Set(G, "<InTown>k__BackingField", false);
        Set(G, "<InWilds>k__BackingField", false);
        Set(G, "level", 0); Set(G, "wave", 0); Set(G, "pending", 0);
        Set(G, "waveActive", false); Set(G, "combo", 0); Set(G, "fadeDir", 0f);
        Call(G, "SetState", Game.GameState.Playing);
        P.stats = new PlayerStats();
        P.weak = 1f;
        P.Revive(Origin, true);
        P.transform.rotation = Quaternion.identity;
        P.strikes = 0;
        Set(P, "invuln", 0f);
        P.stats.crit = 0f;
        Juice.Paused = false;
        Juice.Reset();
        Time.timeScale = 1f;
        Physics.SyncTransforms();
    }
    static Enemy Target(float z, float y = 0f, Enemy.Kind kind = Enemy.Kind.Minion)
    {
        var enemy = Enemy.Create(kind, Origin + new Vector3(0f, y, z), 0);
        enemy.hp = enemy.maxHp = 1000f;
        Set(enemy, "cooldown", 1000f);
        Set(enemy, "guarding", false);
        Physics.SyncTransforms();
        return enemy;
    }
    static Projectile Shot(bool friendly, float speed, Vector3? start = null, Vector3? direction = null)
    {
        Vector3 point = start ?? Origin + Vector3.up * 1.1f;
        if (friendly) Projectile.Throw(point, direction ?? Vector3.forward, speed, 10f);
        else Projectile.Fire(point, direction ?? Vector3.forward, speed);
        var shots = UnityEngine.Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None);
        Projectile result = shots[shots.Length - 1];
        // Only one live projectile is present in each scenario.
        Physics.SyncTransforms();
        return result;
    }
    static void Advance(Projectile shot, float dt) => Call(shot, "Advance", dt);
    static bool Spent(Projectile shot) => (bool)Get(shot, "spent");
    static void Block(float seconds)
    {
        Type move = typeof(PlayerController).GetNestedType("Move", BindingFlags.NonPublic);
        Set(P, "move", Enum.Parse(move, "Block"));
        Set(P, "blockT", seconds);
    }

    static ContactTest()
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

    static void DamageCases()
    {
        Reset();
        var enemy = Target(2f);
        var hit = enemy.ReceiveHit(new DamageRequest(10f, Origin, 0f, source: P));
        Check(hit.outcome == HitOutcome.Damaged && hit.damage == 10f && enemy.hp == 990f, "damage result reports actual applied health loss");
        Check(enemy.ReceiveHit(new DamageRequest(float.NaN, Origin, 0f)).outcome == HitOutcome.Miss
            && enemy.ReceiveHit(new DamageRequest(-1f, Origin, 0f)).outcome == HitOutcome.Miss && enemy.hp == 990f, "invalid or negative damage cannot heal or corrupt health");
        Set(enemy, "guarding", true);
        enemy.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        hit = enemy.ReceiveHit(new DamageRequest(10f, Origin, 0f, source: P));
        Check(hit.outcome == HitOutcome.Blocked && !hit.Damaged && enemy.hp == 990f, "enemy light guard returns blocked without health damage");
        hit = enemy.ReceiveHit(new DamageRequest(10f, Origin, 0f, true, source: P));
        Check(hit.Damaged && hit.guardBroken && hit.staggered && enemy.Open, "heavy request reports guard break and stagger");
        Reset();
        enemy = Target(2f);
        hit = enemy.ReceiveHit(new DamageRequest(10f, Origin, 0f, true, source: P, launch: 1.1f));
        Check(hit.Damaged && hit.launched && enemy.Airborne, "accepted launch is reported in the same contact result");
        Reset();
        enemy = Target(2f);
        enemy.hp = 3f;
        hit = enemy.ReceiveHit(new DamageRequest(100f, Origin, 0f, source: P));
        Check(hit.outcome == HitOutcome.Killed && hit.damage == 3f, "lethal result reports remaining health rather than overkill");
        Check(enemy.ReceiveHit(new DamageRequest(1f, Origin, 0f)).outcome == HitOutcome.Miss, "a dead actor cannot accept another contact");

        Reset();
        Block(0.5f);
        float hp = P.hp;
        hit = P.ReceiveHit(new DamageRequest(1f, Origin + Vector3.forward * 4f, 0f));
        Check(hit.outcome == HitOutcome.Blocked && hit.StopsProjectile && P.hp == hp, "player block consumes contact without health loss");
        Reset(); Block(0.1f);
        hit = P.ReceiveHit(new DamageRequest(1f, Origin + Vector3.forward * 4f, 0f));
        Check(hit.outcome == HitOutcome.Parried && hit.StopsProjectile && P.hp == P.maxHp, "player parry is distinct from damage and immunity");
        Reset(); Set(P, "invuln", 1f);
        hit = P.ReceiveHit(new DamageRequest(1f, Origin + Vector3.forward * 4f, 0f));
        Check(hit.outcome == HitOutcome.Immune && !hit.StopsProjectile, "immunity permits an incoming bolt to continue");
        Reset();
        Set(G, "level", 2); Set(G, "wave", 5); // floor 26: +2 health damage, same light category
        hit = P.ReceiveHit(new DamageRequest(1f, Origin + Vector3.forward * 4f, 0f));
        Check(hit.damage == 3f && P.hp == P.maxHp - 3f && !hit.guardBroken, "floor harm changes actual damage while retaining attack category");
        Reset();
        Set(G, "level", 2); Set(G, "wave", 5); Block(0.5f);
        hit = P.ReceiveHit(new DamageRequest(1f, Origin + Vector3.forward * 4f, 0f));
        Check(hit.outcome == HitOutcome.Blocked && P.hp == P.maxHp, "high-floor light attack remains a blockable light attack");
        Reset(); Block(0.5f);
        hit = P.ReceiveHit(new DamageRequest(2f, Origin + Vector3.forward * 4f, 0f, true));
        Check(hit.guardBroken && hit.damage == 1f && P.hp == P.maxHp - 1f, "heavy guard break result agrees with actual health loss");
        Reset(); Block(0.5f);
        hit = P.ReceiveHit(new DamageRequest(1f, Origin + Vector3.forward * 4f, 0f, true));
        Check(hit.guardBroken && !hit.Damaged && hit.StopsProjectile && P.hp == P.maxHp,
            "fully absorbed heavy contact still reports guard break and consumes its projectile");
        Reset();
        P.hp = 1f; P.stats.secondWinds = 1;
        hit = P.ReceiveHit(new DamageRequest(5f, Origin + Vector3.forward * 4f, 0f, true));
        Check(hit.outcome == HitOutcome.Revived && hit.damage == 1f && !P.Dead && !G.DefeatPending && P.stats.secondWinds == 0, "Second Wind reports revival and avoids expedition defeat");
    }

    static void ProjectileCases()
    {
        Reset();
        var enemy = Target(8f);
        var shot = Shot(true, 100f);
        Advance(shot, 0.2f);
        Check(enemy.hp == 990f && Spent(shot), "high-speed shot sweeps through a target between frame endpoints (hp=" + enemy.hp
            + ", spent=" + Spent(shot) + ", shot=" + shot.transform.position + ", target=" + enemy.transform.position
            + ", collider=" + enemy.GetComponent<CharacterController>().enabled + ")");
        Advance(shot, 0.2f);
        Check(enemy.hp == 990f, "spent projectile cannot deliver a repeated hit");
        Reset(); enemy = Target(8f);
        enemy.transform.position += Vector3.left * 2f;
        enemy.CaptureContactPose();
        enemy.transform.position += Vector3.right * 4f;
        shot = Shot(true, 100f); Advance(shot, 0.2f);
        Check(enemy.hp == 990f, "relative capsule sweep catches an actor crossing the shot between frame poses");
        Reset(); enemy = Target(0.8f);
        enemy.transform.position += Vector3.left * 2f; enemy.CaptureContactPose();
        enemy.transform.position += Vector3.right * 4f;
        shot = Shot(true, 100f); Set(shot, "life", 0.01f); Advance(shot, 0.2f);
        Check(enemy.hp == 1000f && Spent(shot), "expired projectile excludes actor motion after its remaining lifetime");
        Reset(); enemy = Target(0.8f);
        shot = Shot(true, 100f); Set(shot, "life", 0f); Advance(shot, 0.2f);
        Check(enemy.hp == 1000f && Spent(shot), "already expired projectile cannot resolve a new overlap");
        Reset();
        P.Teleport(Origin + Vector3.forward * 20f);
        shot = Shot(false, 10f); Advance(shot, 0.1f);
        Check(P.hp == P.maxHp && !Spent(shot), "teleport resets contact history instead of sweeping an actor across the world");
        foreach (float dt in new[] { 1f / 30f, 1f / 120f })
        {
            Reset(); enemy = Target(8f); shot = Shot(true, 100f);
            for (int i = 0; i < Mathf.CeilToInt(0.2f / dt); i++) Advance(shot, dt);
            Check(enemy.hp == 990f, "projectile contact agrees at simulated " + Mathf.RoundToInt(1f / dt) + " fps");
        }
        Reset();
        var near = Target(4f); var far = Target(8f);
        shot = Shot(true, 100f); Advance(shot, 0.2f);
        Check(near.hp == 990f && far.hp == 1000f, "nearest eligible actor wins rather than registry iteration order");
        Reset(); enemy = Target(8f);
        Box(Origin + new Vector3(0f, 1.5f, 4f), new Vector3(4f, 3f, 0.3f));
        shot = Shot(true, 100f); Advance(shot, 0.2f);
        Check(enemy.hp == 1000f && Spent(shot), "solid cover wins before a farther enemy");
        Reset();
        Target(2f); // a hostile shooter/friendly-to-the-bolt actor must be ignored
        P.Teleport(Origin + Vector3.forward * 8f);
        Box(Origin + new Vector3(0f, 1.5f, 4f), new Vector3(4f, 3f, 0.3f));
        shot = Shot(false, 100f); Advance(shot, 0.2f);
        Check(P.hp == P.maxHp && Spent(shot), "ignored actor cannot hide a wall behind it from a hostile bolt");
        Reset();
        P.Teleport(Origin + Vector3.forward * 4f); P.transform.rotation = Quaternion.Euler(0f, 180f, 0f); Block(0.5f);
        shot = Shot(false, 100f); Advance(shot, 0.2f);
        Check(P.hp == P.maxHp && Spent(shot), "blocked projectile is consumed without repeated guard contacts");
        Reset();
        P.Teleport(Origin + Vector3.forward * 4f); P.transform.rotation = Quaternion.Euler(0f, 180f, 0f); Block(0.1f);
        shot = Shot(false, 100f); Advance(shot, 0.2f);
        Check(P.hp == P.maxHp && Spent(shot), "parried projectile is consumed");
        Reset();
        P.Teleport(Origin + Vector3.forward * 4f); Set(P, "invuln", 1f);
        Box(Origin + new Vector3(0f, 1.5f, 8f), new Vector3(4f, 3f, 0.3f));
        shot = Shot(false, 100f); Advance(shot, 0.2f);
        Check(P.hp == P.maxHp && Spent(shot) && shot.transform.position.z > Origin.z + 7f, "immune player is skipped and the later wall still stops the bolt");
        Reset();
        shot = Shot(false, 10f, Origin + Vector3.up * 1.1f); Advance(shot, 0.01f);
        Check(P.hp == P.maxHp - 1f && Spent(shot), "projectile beginning inside the player resolves its initial overlap");
        Reset();
        enemy = Target(8f, 4f);
        shot = Shot(true, 100f); Advance(shot, 0.2f);
        Check(enemy.hp == 1000f, "horizontal shot cannot hit a target on an upper floor");
        Reset();
        enemy = Target(8f, 4f);
        Vector3 aim = (enemy.transform.position + Vector3.up - (Origin + Vector3.up * 1.1f)).normalized;
        shot = Shot(true, 100f, direction: aim); Advance(shot, 0.2f);
        Check(enemy.hp == 990f, "aimed elevated shot uses the complete three-dimensional segment");
        Reset();
        enemy = Target(80f);
        for (int i = 1; i <= 40; i++) Box(Origin + new Vector3(0f, 1.5f, i), new Vector3(3f, 3f, 0.1f));
        shot = Shot(true, 100f); Advance(shot, 1f);
        Check(enemy.hp == 1000f && Spent(shot) && shot.transform.position.z < Origin.z + 1f, "saturated collision buffer falls back and retains the nearest solid");
    }

    static void MeleeCases()
    {
        Reset(); var enemy = Target(1.5f);
        Call(P, "Strike");
        Check(enemy.hp < 1000f && P.strikes == 1 && (int)Get(G, "combo") == 1, "clear same-level melee hit earns one landed contact");
        Reset(); enemy = Target(1.5f);
        enemy.transform.rotation = Quaternion.Euler(0f, 180f, 0f); Set(enemy, "guarding", true);
        Call(P, "Strike");
        Check(enemy.hp == 1000f && P.strikes == 0 && (int)Get(G, "combo") == 0, "blocked melee does not count as landed damage");
        Reset(); enemy = Target(1.5f);
        Box(Origin + new Vector3(0f, 1.5f, 0.7f), new Vector3(4f, 3f, 0.2f));
        Call(P, "Strike");
        Check(enemy.hp == 1000f && P.strikes == 0, "melee contact cannot cross a solid wall");
        var retaliation = (HitResult)Call(enemy, "HitPlayer", P, 1f, enemy.transform.position, 0f);
        Check(!retaliation.Damaged && P.hp == P.maxHp, "enemy melee obeys the same cover rule");
        Call(P, "Slam", Origin + Vector3.forward * 0.7f, 4f, 2f, 1f);
        Check(enemy.hp == 1000f && !enemy.Airborne, "covered slam neither damages nor launches its target");
        Call(P, "BrushWave");
        Check(enemy.hp == 1000f, "Brush Wave pierces actors but stops at solid cover");
        Call(P, "ColourBurst");
        Check(enemy.hp == 940f, "Colour Burst uses its explicit cover-penetrating magic policy");
        Reset(); enemy = Target(1.5f, 4f);
        Call(P, "Strike"); Call(P, "Slam", Origin, 4f, 2f, 1f); Call(P, "BrushWave"); Call(P, "ColourBurst");
        Check(enemy.hp == 1000f && !enemy.Airborne, "melee slam wave and burst cannot flatten an upper-floor target into range");
        Reset(); enemy = Target(1.5f);
        Call(P, "StartAttack", Vector3.forward);
        P.ReceiveHit(new DamageRequest(1f, Origin + Vector3.back * 3f, 0f));
        Call(P, "TickAttack", 1f, Vector3.forward);
        Check(enemy.hp == 1000f && !P.Attacking && (float)Get(P, "attackT") < 0f, "hurt cancels wind-up and prevents an old pending strike");
        Reset(); enemy = Target(1.5f);
        Call(P, "StartAttack", Vector3.forward); Call(P, "BrushWave");
        float afterWave = enemy.hp;
        Call(P, "TickAttack", 1f, Vector3.forward);
        Check(afterWave == 975f && enemy.hp == afterWave && !P.Attacking, "skill override consumes the old attack instead of leaving a delayed hit");
        Reset(); enemy = Target(1.5f);
        Call(P, "StartAttack", Vector3.forward); Call(P, "ColourBurst");
        float afterBurst = enemy.hp;
        Call(P, "TickAttack", 1f, Vector3.forward);
        Check(afterBurst == 940f && enemy.hp == afterBurst && !P.Attacking, "burst override cancels old offence before dealing its own damage");
        Reset(); enemy = Target(1.5f);
        P.stats.dashStrike = true;
        Call(P, "DashStrike"); float once = enemy.hp;
        Call(P, "DashStrike");
        Check(once == 985f && enemy.hp == once, "dash keeps a per-action hit set and cannot damage the same actor twice");
        Reset(); enemy = Target(1.5f);
        Call(P, "StartAttack", Vector3.forward);
        Call(P, "TickAttack", 1f, Vector3.forward);
        Check(enemy.hp < 1000f && P.strikes == 1, "large frame step crossing contact and recovery still emits exactly one melee hit");
        foreach (bool expiredBeforeWindow in new[] { false, true })
        {
            Reset(); enemy = Target(1.5f);
            Call(P, "StartAttack", Vector3.forward); Set(P, "attackT", 0.13f);
            Type action = typeof(PlayerController).GetNestedType("BufferedAction", BindingFlags.NonPublic);
            Call(P, "Buffer", Enum.Parse(action, "Dodge"), Vector3.forward);
            Set(P, "bufferedUntil", Time.time - (expiredBeforeWindow ? 0.1f : 0.02f));
            int dashes = P.dashes;
            Call(P, "TickAttack", 0.1f, Vector3.forward);
            Check(P.strikes == 1 && P.dashes == dashes + (expiredBeforeWindow ? 0 : 1),
                expiredBeforeWindow ? "dodge expired before the cancel window remains rejected across a long frame"
                    : "long frame resolves the strike before a dodge valid at the cancel window");
        }
    }

    static void WaveCases()
    {
        foreach (float dt in new[] { 1f / 30f, 1f / 120f, 2f })
        {
            Reset(); P.Teleport(Origin + Vector3.forward * 8f);
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            obstacles.Add(go); go.transform.position = Origin;
            var wave = go.AddComponent<Shockwave>(); Call(wave, "Start");
            for (int i = 0; i < Mathf.CeilToInt(2f / dt); i++) Call(wave, "Advance", dt);
            Check(P.hp == P.maxHp - 1f, "shockwave resolves exactly one crossing at dt=" + dt);
        }
        Reset(); P.Teleport(Origin + Vector3.forward * 8f);
        Box(Origin + new Vector3(0f, 1.5f, 4f), new Vector3(4f, 3f, 0.3f));
        var covered = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        UnityEngine.Object.DestroyImmediate(covered.GetComponent<Collider>());
        obstacles.Add(covered); covered.transform.position = Origin;
        var ring = covered.AddComponent<Shockwave>(); Call(ring, "Start"); Call(ring, "Advance", 2f);
        Check(P.hp == P.maxHp, "swept shockwave retains its solid-cover policy");
        Reset(); P.Teleport(Origin + Vector3.forward * 5f);
        var moving = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        UnityEngine.Object.DestroyImmediate(moving.GetComponent<Collider>());
        obstacles.Add(moving); moving.transform.position = Origin;
        ring = moving.AddComponent<Shockwave>(); Call(ring, "Start");
        P.transform.position -= Vector3.forward * 5f;
        Call(ring, "Advance", 0.1f);
        Check(P.hp == P.maxHp - 1f, "moving player crossing a ring between poses receives one contact");
    }

    static void SettlementCases()
    {
        foreach (bool enemyFirst in new[] { true, false })
        {
            Reset(); Save.Wipe(); Save.Level = 12; Save.StatPoints = 33; Upgrades.Reset();
            Save.BeginExpedition(false); JourneyStore.Transaction(() => Save.CarriedSparks = 11);
            Set(G, "wave", 4); Set(G, "waveActive", true);
            var enemy = Target(1.5f); enemy.hp = 1f; P.hp = 1f;
            if (enemyFirst) enemy.ReceiveHit(new DamageRequest(10f, Origin, 0f));
            P.ReceiveHit(new DamageRequest(10f, Origin + Vector3.forward * 4f, 0f, true));
            if (!enemyFirst) enemy.ReceiveHit(new DamageRequest(10f, Origin, 0f));
            Check(Save.TowerFloor == 5 && Save.PendingGuardian != null && Save.PendingGuardian.floor == 5,
                "mutual Guardian kill retains its entitlement with " + (enemyFirst ? "enemy" : "player") + " death first");
            Check(Save.Sparks == 6 && Save.Runs == 1 && G.DefeatPending && ((IList)Get(G, "timers")).Count > 0,
                "mutual kill keeps one defeat settlement and a live results callback");
            Call(G, "EndRun", false, true);
            JourneyStore.ReloadForTest(); JourneyStore.Ensure(); Upgrades.Reset();
            Check(Save.Sparks == 6 && Save.Runs == 1 && Save.PendingGuardian != null && G.State == Game.GameState.Results,
                "mutual-kill reload neither loses the offer nor repeats the purse settlement");
        }
        Reset(); Save.Wipe(); Save.Level = 12; Save.StatPoints = 33; Upgrades.Reset(); Save.BeginExpedition(false);
        Set(G, "level", 2); Set(G, "wave", 9); Set(G, "waveActive", true);
        var boss = Target(3f, kind: Enemy.Kind.Boss); boss.hp = 1f; P.hp = 1f;
        P.ReceiveHit(new DamageRequest(10f, Origin + Vector3.forward * 4f, 0f, true));
        var result = boss.ReceiveHit(new DamageRequest(10f, Origin, 0f, true));
        Check(result.outcome == HitOutcome.Defeated && boss.Defeated && !boss.CombatEligible && !boss.Dead,
            "retained defeated boss has an explicit combat-ineligible state");
        Check(boss.ReceiveHit(new DamageRequest(10f, Origin, 0f)).outcome == HitOutcome.Miss,
            "defeated boss cannot accept more damage or reward events");
        Check(Call(P, "CombatTarget", 10f, Vector3.forward) == null && !(bool)Call(P, "TargetVisible", boss),
            "aim assistance and lock visibility exclude the defeated boss");
        Check(Save.TowerFloor == 30 && ((IList)Get(G, "timers")).Count > 0,
            "mutual final-boss kill preserves floor progress without canceling defeat results");
        Call(G, "EndRun", false, true);
        Check(G.State == Game.GameState.Results && Save.Runs == 1 && Save.Wins == 0,
            "final-boss mutual kill returns through defeat with an available rematch");
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        if (readyAt == 0) { readyAt = EditorApplication.timeSinceStartup + 2; deadline = readyAt + 180; }
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Contact test initialization timed out");
            if (G == null || P == null || EditorApplication.timeSinceStartup < readyAt) return;
            Application.runInBackground = true;
            Save.BeginExpedition(false);
            DamageCases(); ProjectileCases(); MeleeCases(); WaveCases(); SettlementCases();
            Check(JourneyStore.Error == null, "contact and settlement scenarios leave the journey store healthy");
            Debug.Log("CONTACT DONE " + passed + " checks passed");
            SessionState.SetBool(Session, false);
            EditorApplication.update -= Tick;
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            Debug.LogError("CONTACT FAILED " + e);
            SessionState.SetBool(Session, false);
            EditorApplication.update -= Tick;
            EditorApplication.Exit(1);
        }
    }
}
