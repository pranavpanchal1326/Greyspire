using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

[InitializeOnLoad]
public static class TerrainTest
{
    const string Session = "GreyspireTerrainTest";
    const BindingFlags Any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static double readyAt, deadline, nextAt;
    static int passed, stage;
    static Transform course;
    static NavField field;
    static Enemy chase;
    static Vector3 actorStart;
    static Game G => Game.I;
    static PlayerController P => G.Player;
    static Vector3 At(float x, float y, float z) => TerrainCourse.At(x, y, z);
    static object Call(object host, string method, params object[] args) => host.GetType().GetMethod(method, Any).Invoke(host, args);
    static void Set(object host, string name, object value) => host.GetType().GetField(name, Any).SetValue(host, value);
    static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        passed++; Debug.Log("TERRAIN PASS " + message);
    }
    static TerrainTest() { if (SessionState.GetBool(Session, false)) EditorApplication.update += Tick; }
    public static void Run()
    {
        Save.Wipe(); Save.SeenIntro = true; Save.Level = 12; Save.StatPoints = 33; Save.Flush();
        EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        SessionState.SetBool(Session, true); EditorApplication.update -= Tick; EditorApplication.update += Tick;
        EditorApplication.EnterPlaymode();
    }
    static NavFollower Mover(string name, Vector3 at, float radius, float height)
    {
        var go = new GameObject(name); go.transform.SetParent(course, false); go.transform.position = at;
        var cc = go.AddComponent<CharacterController>(); cc.radius = radius; cc.height = height;
        cc.center = Vector3.up * (height * 0.5f); cc.stepOffset = 0.3f;
        return NavFollower.AddTo(go);
    }
    static void Travel(NavFollower mover, Vector3 goal, float speed = 4f)
    {
        for (int i = 0; i < 900 && Vector3.Distance(mover.transform.position, goal) > 0.35f; i++)
            mover.Advance(goal, 0.2f, speed, 1f / 30f);
    }
    static Enemy Target(Vector3 at, Enemy.Kind kind = Enemy.Kind.Minion)
    {
        var e = Enemy.Create(kind, at, 0); e.hp = e.maxHp = 1000f;
        Set(e, "stun", 0f); Set(e, "cooldown", 1000f); return e;
    }
    static void GroundCases()
    {
        Check(WorldSupport.TryGround(At(0f, 0.05f, 4f), out var lower) && Mathf.Abs(lower.point.y + 20f) < 0.01f
            && lower.collider.name == "Lower route", "support under bridge retains the lower surface identity");
        Check(WorldSupport.TryGround(At(0f, 8.05f, 4f), out var upper) && Mathf.Abs(upper.point.y + 12f) < 0.01f,
            "bridge support retains its elevated world position");
        Check(WorldSupport.TryGround(At(-9f, 4.1f, -10f), out var hill) && hill.normal.y > 0.9f && hill.normal.y < 0.99f,
            "hill query returns the local slope normal");
        Check(!WorldSupport.TryGround(At(45f, 0.05f, 0f), out _), "missing ledge reports failure without inventing a floor");
        Check(WorldSupport.TryStand(At(-22f, 0.05f, -5f), 0.35f, 1f, out _)
            && !WorldSupport.TryStand(At(-22f, 0.05f, -5f), 0.45f, 1.8f, out _), "low beam clearance distinguishes small and human bodies");
        Check(WorldSupport.CanInteract(At(1f, 8.05f, 4f), At(3f, 8f, 4f)), "supported same-level rescue approach is usable");
        Check(!WorldSupport.CanInteract(At(1f, 0.05f, 4f), At(1f, 8f, 4f)), "lower route cannot interact with a rescue overhead");
        var wall = TerrainCourse.Box(course, "Interaction wall", new Vector3(2f, 9f, 4f), new Vector3(0.2f, 2f, 4f));
        Physics.SyncTransforms();
        Check(!WorldSupport.CanInteract(At(1f, 8.05f, 4f), At(3f, 8f, 4f)), "interaction cannot cross closed solid cover");
        UnityEngine.Object.DestroyImmediate(wall);
        var chest = TerrainCourse.Box(course, "Interactive chest", new Vector3(3f, 8.7f, 4f), new Vector3(1f, 1.4f, 1f));
        Physics.SyncTransforms();
        Check(WorldSupport.CanInteract(At(1f, 8.05f, 4f), At(3f, 8f, 4f), targetOwner: chest.transform),
            "interaction target's own collider does not hide its usable chest prompt");
        wall = TerrainCourse.Box(course, "Chest cover", new Vector3(2f, 9f, 4f), new Vector3(0.2f, 2f, 4f));
        Physics.SyncTransforms();
        Check(!WorldSupport.CanInteract(At(1f, 8.05f, 4f), At(3f, 8f, 4f), targetOwner: chest.transform),
            "ignoring the chest still retains the separate wall before it");
        UnityEngine.Object.DestroyImmediate(chest); UnityEngine.Object.DestroyImmediate(wall);

        var fallback = new GameObject("No-nav follower"); fallback.transform.position = At(20f, 0.05f, -10f);
        TerrainCourse.Barrier.SetActive(true); Physics.SyncTransforms();
        WorldSupport.TryMove(fallback.transform, Vector3.forward * 6f, 0.35f, 1f);
        Check(fallback.transform.position.z < At(0f, 0f, -7.4f).z, "fallback motion sweeps a long step against the closing barrier");
        TerrainCourse.Barrier.SetActive(false); fallback.transform.position = At(39f, 0.05f, 30f);
        WorldSupport.TryMove(fallback.transform, Vector3.right * 5f, 0.35f, 1f);
        Check(fallback.transform.position.x < At(40.1f, 0f, 0f).x, "fallback motion stops at an unsupported ledge");
        fallback.transform.position = At(-9f, 0.05f, -22f);
        WorldSupport.TryMove(fallback.transform, Vector3.forward * 13f, 0.35f, 1f);
        WorldSupport.TryMove(fallback.transform, Vector3.forward * 13f, 0.35f, 1f);
        Check(fallback.transform.position.y > At(0f, 7.8f, 0f).y, "collision fallback climbs the hill onto the bridge");
        WorldSupport.TryMove(fallback.transform, Vector3.back * 13f, 0.35f, 1f);
        WorldSupport.TryMove(fallback.transform, Vector3.back * 13f, 0.35f, 1f);
        Check(Mathf.Abs(fallback.transform.position.y + 19.95f) < 0.2f, "collision fallback walks downhill to the lower route");
        UnityEngine.Object.DestroyImmediate(fallback);
    }
    static void NavigationCases()
    {
        var ignored = P.GetComponent<CharacterController>();
        bool before = ignored.enabled;
        NavField.BeforeBuildForTest = () => throw new Exception("injected navigation failure");
        var failedRoot = new GameObject("Failed bake fixture").transform;
        TerrainCourse.Box(failedRoot, "Failure ground", Vector3.down, new Vector3(5f, 1f, 5f));
        var failed = NavField.Bake(failedRoot, 3f, ignored);
        Check(!failed.Ready && failed.Error != null && ignored.enabled == before, "failed bake restores the exact collider state and reports unavailable");
        NavField.BeforeBuildForTest = null;
        UnityEngine.Object.DestroyImmediate(failedRoot.gameObject);
        Physics.SyncTransforms();
        field = NavField.Bake(course, 40f, ignored);
        Check(field.Ready && field.CollectionBounds.size.y > 10f && field.CollectionBounds.center.y > 2f,
            "navigation bounds include stacked geometry rather than a six-metre slice");
        Check(ignored.enabled == before && field.BakeMs > 0f && field.Triangles > 0, "successful bake restores colliders and reports time and walkable geometry");
        var human = Mover("Human route", At(-9f, 0.05f, -22f), 0.45f, 1.8f);
        bool attached = human.Attach();
        Check(attached, "human profile attaches to its local lower support (ground="
            + WorldSupport.TryGround(human.transform.position, out _) + ", sample="
            + NavMesh.SamplePosition(human.transform.position, out var probe, 1.5f, human.Filter) + ", probe=" + probe.position
            + ", destination=" + human.TryDestination(human.transform.position, out _) + ", profile=" + human.Filter.agentTypeID + ")");
        Travel(human, At(4f, 8.05f, 4f));
        Check(human.RouteComplete && Vector3.Distance(human.transform.position, At(4f, 8.05f, 4f)) < 0.6f,
            "complete route climbs the hill and crosses the bridge (at=" + human.transform.position + ", complete=" + human.RouteComplete
            + ", corners=" + string.Join("; ", human.RouteCorners) + ")");
        Travel(human, At(0f, 0.05f, 4f));
        Check(human.RouteComplete && human.transform.position.y < At(0f, 0.3f, 0f).y,
            "upper actor routes around to the lower path instead of warping through the bridge");
        Check(!human.TryDestination(At(45f, 0.05f, 0f), out _), "unsupported navigation goal is rejected");
        Check(!human.Teleport(At(0f, 4f, 4f)) && human.transform.position.y < -19f, "warp cannot steal a different support level");
        UnityEngine.Object.DestroyImmediate(human.gameObject);
        human = Mover("Door human", At(20f, 0.05f, -10f), 0.45f, 1.8f);
        Travel(human, At(20f, 0.05f, 0f));
        Check(human.RouteComplete && Vector3.Distance(human.transform.position, At(20f, 0.05f, 0f)) < 0.6f, "human route fits the narrow door");
        UnityEngine.Object.DestroyImmediate(human.gameObject);
        var large = Mover("Large route", At(20f, 0.05f, -10f), 1.2f, 4f);
        Check(large.Attach(), "large profile attaches on open terrain");
        large.DirectionTo(At(20f, 0.05f, 0f), 1f);
        Check(!large.RouteComplete, "large body cannot claim a complete route through the narrow door");
        UnityEngine.Object.DestroyImmediate(large.gameObject);
        var small = Mover("Small route", At(-22f, 0.05f, -8f), 0.35f, 1f);
        Travel(small, At(-22f, 0.05f, -3f));
        Check(small.RouteComplete && small.transform.position.z > At(0f, 0f, -3.6f).z,
            "small profile traverses beneath the low beam (at=" + small.transform.position + ", complete=" + small.RouteComplete + ")");
        UnityEngine.Object.DestroyImmediate(small.gameObject);
    }
    static void ParticipantCases()
    {
        P.enabled = false; P.Revive(At(-9f, 0.05f, -22f), true);
        var cc = P.GetComponent<CharacterController>();
        for (int i = 0; i < 272; i++) cc.Move(new Vector3(0f, -0.04f, 0.096f));
        Check(P.transform.position.y > At(0f, 7.7f, 0f).y && P.transform.position.z > At(0f, 0f, 2f).z,
            "player controller climbs uphill onto the elevated bridge");
        for (int i = 0; i < 272; i++) cc.Move(new Vector3(0f, -0.08f, -0.096f));
        Check(P.transform.position.y < At(0f, 0.4f, 0f).y, "player controller descends the hill");
        P.Teleport(At(20f, 0.05f, 11f));
        for (int i = 0; i < 85; i++) cc.Move(new Vector3(0f, -0.04f, 0.096f));
        Check(P.transform.position.y > At(0f, 1.8f, 0f).y, "player controller climbs stairs to their landing");
        P.Teleport(At(3f, 0.05f, 4f)); Call(P, "Update");
        Check(P.transform.position.y < -19f && (P.transform.position - At(3f, 0.05f, 4f)).magnitude < 0.4f,
            "player below world y=-15 remains on valid lower support");
        P.Teleport(At(3f, 8.05f, 4f));
        var enemy = Target(At(-2f, 8.05f, 4f));
        for (int i = 0; i < 30; i++) Call(enemy, "Update");
        Check(enemy.transform.position.y > -12.3f, "enemy movement never pulls elevated feet toward y=0.1");
        UnityEngine.Object.DestroyImmediate(enemy.gameObject);
        var crowdA = Target(At(-2f, 8.05f, 4f)); var crowdB = Target(At(-2f, 8.05f, 4f));
        for (int i = 0; i < 40; i++) { Call(crowdA, "Update"); Call(crowdB, "Update"); }
        Check(Vector3.Distance(crowdA.transform.position, crowdB.transform.position) > 0.5f
            && crowdA.transform.position.y > -12.3f && crowdB.transform.position.y > -12.3f,
            "crowded enemies separate on the elevated surface without an absolute-height reset");
        UnityEngine.Object.DestroyImmediate(crowdA.gameObject); UnityEngine.Object.DestroyImmediate(crowdB.gameObject);
        crowdA = Target(At(-2f, 8.05f, 4f), Enemy.Kind.BigSlime);
        crowdB = Target(At(-2f, 10.02f, 4f), Enemy.Kind.BigSlime);
        // Manual updates share one frame's delta time, so a fixed count covers less time on a fast frame:
        // step until the upper slime has settled (with a generous cap) rather than a fixed 100 times.
        for (int i = 0; i < 600 && crowdB.transform.position.y >= -11.5f; i++) { Call(crowdA, "Update"); Call(crowdB, "Update"); }
        Check(crowdB.transform.position.y < -11.5f && crowdB.transform.position.y > -12.3f,
            "slime standing on another capsule separates and returns to its local ground (at=" + crowdB.transform.position + ")");
        UnityEngine.Object.DestroyImmediate(crowdA.gameObject); UnityEngine.Object.DestroyImmediate(crowdB.gameObject);
        enemy = Target(At(3f, 8.05f, 4f));
        P.Teleport(At(3f, 0.05f, 4f));
        Check(!CombatContact.CanContact(P.transform.position + Vector3.up, enemy), "melee cannot flatten bridge and lower path into one level");
        Check(!(bool)Call(P, "TargetVisible", enemy), "lock visibility respects the bridge's solid underside");
        Projectile.Throw(P.transform.position + Vector3.up, Vector3.up, 100f, 10f);
        var shot = UnityEngine.Object.FindFirstObjectByType<Projectile>(); Call(shot, "Advance", 0.2f);
        Check(enemy.hp == 1000f && !shot.gameObject.activeSelf, "upward projectile stops on bridge before its upper actor");
        UnityEngine.Object.DestroyImmediate(shot.gameObject); UnityEngine.Object.DestroyImmediate(enemy.gameObject);

        P.Teleport(At(0f, 0.05f, 30f)); Pickup.AttractAll = false;
        Pickup.Spawn(Pickup.Kind.Coin, At(3f, 9f, 4f), 2);
        var pickup = UnityEngine.Object.FindFirstObjectByType<Pickup>(); Set(pickup, "velocity", Vector3.zero);
        for (int i = 0; i < 120; i++) Call(pickup, "Advance", 1f / 30f);
        Check(pickup.transform.position.y > -12f && pickup.transform.position.y < -11.2f, "loot settles on the bridge rather than a global bounce plane");
        Set(pickup, "age", 31f); Call(pickup, "Advance", 0.01f);
        Check(pickup != null && Vector3.Distance(pickup.transform.position, P.transform.position) < 1f,
            "timed-out inaccessible loot is recovered beside the player instead of discarded");
        UnityEngine.Object.DestroyImmediate(pickup.gameObject);
        var resident = Mover("Resident route", At(20f, 0.05f, -10f), 0.4f, 1.8f);
        var person = resident.gameObject.AddComponent<Resident>(); person.enabled = false;
        person.home = At(20f, 0.05f, 0f);
        TerrainCourse.Barrier.SetActive(true); Physics.SyncTransforms();
        for (int i = 0; i < 600; i++) resident.Advance(person.home, 0.25f, 1.45f, 1f / 30f);
        Check(resident.transform.position.z < At(0f, 0f, -7.4f).z, "resident route recognizes a closing barrier and stays outside");
        TerrainCourse.Barrier.SetActive(false); Physics.SyncTransforms(); Travel(resident, person.home, 1.45f);
        Check(Vector3.Distance(resident.transform.position, person.home) < 0.6f, "resident resumes its owned home route after the barrier opens");
        UnityEngine.Object.DestroyImmediate(resident.gameObject);

        P.Teleport(At(3f, 8.05f, 4f)); Save.Pet = true; Companion.Ensure();
        var pet = UnityEngine.Object.FindFirstObjectByType<Companion>(); pet.enabled = false;
        enemy = Target(At(3f, 8.05f, 6f));
        pet.transform.position = At(3f, 8.05f, 5.2f); Call(pet, "Update");
        Check(enemy.hp == 1000f && pet.transform.position.y > -12.3f, "pet keeps elevated support and does not initiate an idle camp fight");
        Set(enemy, "provoked", true); Set(pet, "attackCd", 0f); pet.transform.position = At(3f, 8.05f, 5.2f); Call(pet, "Update");
        Check(enemy.hp < 1000f, "pet assists a visible enemy already engaged with the player");
        float hp = enemy.hp;
        var cover = TerrainCourse.Box(course, "Pet cover", new Vector3(3f, 9f, 5.6f), new Vector3(3f, 2f, 0.2f)); Physics.SyncTransforms();
        Set(pet, "attackCd", 0f); Call(pet, "Update");
        Check(enemy.hp == hp, "pet assistance does not strike through cover");
        UnityEngine.Object.DestroyImmediate(cover); UnityEngine.Object.DestroyImmediate(enemy.gameObject);
        Travel(pet.GetComponent<NavFollower>(), At(-9f, 0.05f, -22f));
        Check(pet.transform.position.y < -19f, "pet follows the complete downhill route to the lower level");
        Companion.Dismiss();
        Fx.Telegraph(At(3f, 8f, 4f), 2f, 5f, Color.red);
        var telegraph = UnityEngine.Object.FindFirstObjectByType<TelegraphFx>();
        Check(Mathf.Abs(telegraph.transform.position.y + 11.94f) < 0.01f, "attack telegraph uses the elevated support origin");
        foreach (var marker in telegraph.GetComponentsInChildren<SurfaceMarker>()) marker.Project();
        Check(telegraph.GetComponentInChildren<MeshFilter>().sharedMesh.triangles.Length > 0, "ground marker projects onto the intended bridge surface");
        UnityEngine.Object.DestroyImmediate(telegraph.gameObject);
    }
    static void BeginRescue()
    {
        UnityEngine.Object.DestroyImmediate(chase.gameObject);
        P.Teleport(At(0f, 8.05f, 4f));
        Call(G, "BuildStatue", 0, At(-2f, 8f, 4f));
        typeof(SmokeTest).GetMethod("UseSpot", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { "A drained Painter" });
        Check(G.Scene != null && Mathf.Abs(G.Scene.transform.position.y + 12f) < 0.1f,
            "actual rescue Timeline is staged on the bridge's local surface");
        G.Scene.Skip();
        typeof(SmokeTest).GetMethod("SkipDialog", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
        Check(G.Asking, "elevated rescue reaches its persisted story choice");
        G.Answer(0);
        typeof(SmokeTest).GetMethod("SkipDialog", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
        actorStart = GameObject.Find("Drained Painter").transform.position;
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        if (readyAt == 0) { readyAt = EditorApplication.timeSinceStartup + 2; deadline = readyAt + 180; }
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Terrain check timed out");
            if (G == null || P == null || EditorApplication.timeSinceStartup < readyAt || EditorApplication.timeSinceStartup < nextAt) return;
            Application.runInBackground = true;
            if (stage == 0)
            {
                G.enabled = P.enabled = false; Call(G, "SetState", Game.GameState.Playing);
                // Fixed-iteration controller fixtures need a fixed engine clock and seed.
                Time.captureFramerate = 60; ProductionRandom.OverrideSeedForTest = 734912; ProductionRandom.BeginScene("terrain-course");
                Set(G, "resumeFrame", Time.frameCount - 1); Set(P, "invuln", 1000f);
                Check(G.Roaming, "terrain participants run in active gameplay rather than a resume-frame pause");
                P.stats = new PlayerStats(); course = TerrainCourse.Build();
                GroundCases(); NavigationCases(); ParticipantCases();
                TerrainCourse.Capture();
                UnityEngine.Object.DestroyImmediate(field);
                var cancelRoot = new GameObject("Cancelled staged bake").transform;
                TerrainCourse.Box(cancelRoot, "Cancellation ground", Vector3.down, new Vector3(10f, 1f, 10f));
                var cancelled = NavField.BakeStaged(cancelRoot, 5f, P.GetComponent<CharacterController>());
                cancelRoot.gameObject.SetActive(false);
                Check(!cancelled.Ready && NavField.Current == null && P.GetComponent<CharacterController>().enabled,
                    "cancelled staged bake leaves no ready field and restores the player collider");
                UnityEngine.Object.DestroyImmediate(cancelRoot.gameObject);
                P.Teleport(At(3f, 8.05f, 4f)); Save.Pet = true; Companion.Ensure();
                var fallbackPet = UnityEngine.Object.FindFirstObjectByType<Companion>(); fallbackPet.enabled = false;
                fallbackPet.transform.position = At(3f, 8.05f, 5.2f);
                var petWall = TerrainCourse.Box(course, "No-nav pet wall", new Vector3(3f, 9f, 4.5f), new Vector3(4f, 2f, 0.3f));
                Physics.SyncTransforms();
                for (int i = 0; i < 120; i++) Call(fallbackPet, "Update");
                Check(fallbackPet.transform.position.z > At(0f, 0f, 4.6f).z && fallbackPet.transform.position.y > -12.3f,
                    "actual pet fallback preserves elevated support and cannot walk through a wall");
                fallbackPet.transform.position = At(3f, 0.05f, 4f); Set(fallbackPet, "lostTime", 2.1f); Call(fallbackPet, "Update");
                Check(fallbackPet.transform.position.y > -12.3f && Vector3.Distance(fallbackPet.transform.position, P.transform.position) < 3f,
                    "stranded no-nav pet recovers to a validated socket behind the player");
                UnityEngine.Object.DestroyImmediate(petWall); Companion.Dismiss();
                field = NavField.BakeStaged(course, 40f, P.GetComponent<CharacterController>());
                Check(!field.Ready && P.GetComponent<CharacterController>().enabled, "staged bake exposes pending readiness with colliders already restored");
                stage = 5;
            }
            else if (stage == 5)
            {
                if (field.Error != null) throw new Exception(field.Error);
                if (!field.Ready) return;
                Check(field.Triangles > 0 && field.BakeMs > 0f, "asynchronous navigation completes all body profiles and reports elapsed cost");
                P.Teleport(At(4f, 8.05f, 4f)); chase = Target(At(-9f, 0.05f, -22f));
                stage = 1; nextAt = EditorApplication.timeSinceStartup + 14;
                Debug.Log("TERRAIN running real enemy chase and elevated rescue scenario");
            }
            else if (stage == 1)
            {
                Check(chase.transform.position.y > -12.4f && Vector3.Distance(chase.transform.position, P.transform.position) < 5f,
                    "live enemy chase follows the ramp and bridge to a player on another level (at=" + chase.transform.position + ")");
                BeginRescue(); stage = 2; nextAt = EditorApplication.timeSinceStartup + 3;
            }
            else if (stage == 2)
            {
                var actor = GameObject.Find("Drained Painter");
                Check(actor != null && actor.transform.position.y > -12.2f && (actor.transform.position - actorStart).magnitude > 0.2f,
                    "rescue departure walks along elevated support without flattening its feet");
                stage = 3; nextAt = EditorApplication.timeSinceStartup + 4;
            }
            else
            {
                Check(Save.RescueChoice(0) == 1 && !Save.RescuePending(0) && (Save.RescuedMask & 1) != 0 && G.State == Game.GameState.Playing,
                    "elevated rescue completes its saved branch and restores gameplay");
                Check(JourneyStore.Error == null, "terrain scenarios leave the journey store healthy");
                Debug.Log("TERRAIN DONE " + passed + " checks passed");
                SessionState.SetBool(Session, false); EditorApplication.update -= Tick; EditorApplication.Exit(0);
            }
        }
        catch (Exception e)
        {
            NavField.BeforeBuildForTest = null;
            Debug.LogError("TERRAIN FAILED " + e); SessionState.SetBool(Session, false);
            EditorApplication.update -= Tick; EditorApplication.Exit(1);
        }
    }
}
