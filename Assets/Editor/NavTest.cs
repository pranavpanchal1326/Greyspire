using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

// Automated check of the NavMesh work, run from the command line:
//   Tools\navtest.ps1        (Unity -projectPath . -executeMethod NavTest.Run)
// Enters play mode, climbs to a tower floor with Mop in tow, and saves pictures to Logs/nav:
// the baked map from above, Mop's route round an obstacle at three moments, a hunt, and the
// same things as the player sees them with the F3 view on.
[InitializeOnLoad]
public static class NavTest
{
    const string Key = "NavTestStage";
    const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;

    class Step { public float wait; public Action act; }

    static List<Step> steps;
    static int at;
    static float since;
    static Vector3 drive;
    static Vector3 rock, side;

    static PlayerController P => Game.I.Player;

    static NavTest()
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
            P.hp = P.maxHp;
            typeof(PlayerController).GetField("invuln", Any).SetValue(P, 1f);
            if (drive != Vector3.zero)
            {
                typeof(PlayerController).GetField("planar", Any).SetValue(P, drive);
                P.transform.rotation = Quaternion.LookRotation(drive);
            }

            Step s = steps[at];
            if (now - since < s.wait) return;
            s.act();
            at++;
            since = now;
            if (at < steps.Count) return;

            Debug.Log("NAV DONE");
            SessionState.SetInt(Key, 0);
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            Debug.LogError("NAV FAILED at step " + at + ": " + e);
            SessionState.SetInt(Key, 0);
            EditorApplication.Exit(1);
        }
    }

    static void Do(float wait, Action act) => steps.Add(new Step { wait = wait, act = act });

    static void Script()
    {
        // Second floor of the tower, Mop already met.
        Do(2.5f, () =>
        {
            Save.Pet = true;
            Companion.Ensure();
            JourneyStore.SetInt("towerFloor", 1);
            JourneyStore.SetInt("storyFloor", 1);
            typeof(Game).GetMethod("StartRun", Any).Invoke(Game.I, null);
        });
        Do(2.5f, Settle);
        Do(1f, Settle);
        Do(0.5f, () =>
        {
            NavField f = NavField.Current;
            Debug.Log("NAV floor=" + Game.I.Floor + " state=" + Game.I.State + " field=" + (f != null)
                + " enemies=" + Enemy.All.Count + " agents=" + NavField.Agents.Count);
            Shot("g0_plain");
        });
        Do(1.6f, () =>
        {
            NavField.Show = true;
            Top("t0_floor_plain", Vector3.zero, 47f, false);
        });
        Do(0.5f, () => Top("t1_floor_navmesh", Vector3.zero, 47f, true));

        // A rock or a tree well away from any pack, the hero on one side of it and Mop on the other.
        Do(0.3f, () =>
        {
            PickObstacle();
            Face(side);
            P.Teleport(rock + side * 5.5f);
            Mop().Teleport(rock - side * 6.5f);
            Look(rock, side, 30f);
        });
        Do(0.3f, () => { Top("p1_route_start", rock, 9.5f, true); Report("start"); });
        Do(0.45f, () => { Top("p2_route_mid", rock, 9.5f, true); Report("mid"); });
        Do(1.6f, () => { Top("p3_route_arrived", rock, 9.5f, true); Report("arrived"); });

        // The same again for the game view.
        Do(0.3f, () =>
        {
            Mop().Teleport(rock - side * 6.5f);
            Look(rock, side, 34f);
        });
        Do(0.25f, () => Shot("g1_route"));
        Do(2.2f, () => Shot("g2_arrived"));

        // The hero walks off through the props; Mop keeps to heel behind.
        Do(1.8f, () => drive = Quaternion.Euler(0f, 40f, 0f) * -side * 4.2f);
        Do(1.6f, () => { Top("f1_follow", P.transform.position, 9.5f, true); Report("follow"); Shot("g3_follow"); });
        Do(1.8f, () => drive = Vector3.zero);

        // A pack within Mop's reach: he routes to the nearest monster.
        Do(0.5f, () =>
        {
            Enemy e = Nearest();
            if (e == null) { Debug.Log("NAV no enemy for the hunt"); return; }
            Vector3 away = (rock - e.transform.position); away.y = 0f; away.Normalize();
            P.Teleport(e.transform.position + away * 8.5f);
            Mop().Teleport(P.transform.position + away * 4f);
            Look(e.transform.position, away, 26f);
        });
        Do(0.45f, () => { Top("h1_hunt", P.transform.position, 11f, true); Report("hunt"); Shot("g4_hunt"); });
        Do(2.5f, () => Report("end"));
    }

    // ------------------------------------------------------------ helpers

    static NavFollower Mop() => UnityEngine.Object.FindAnyObjectByType<Companion>().GetComponent<NavFollower>();

    static void Settle() => typeof(SmokeTest).GetMethod("Settle", Any).Invoke(null, null);

    static Enemy Nearest()
    {
        Enemy best = null;
        foreach (Enemy e in Enemy.All)
            if (best == null || (e.transform.position - rock).magnitude < (best.transform.position - rock).magnitude) best = e;
        return best;
    }

    static void PickObstacle()
    {
        float bestScore = -1f;
        foreach (Collider c in Game.I.LevelRoot.GetComponentsInChildren<Collider>())
        {
            if (!(c is CapsuleCollider) || c.isTrigger) continue;
            Vector3 p = c.transform.position; p.y = 0f;
            if (p.magnitude > 28f || p.magnitude < 9f) continue;
            float clear = 99f;
            foreach (Enemy e in Enemy.All) clear = Mathf.Min(clear, (e.transform.position - p).magnitude);
            if (clear < 17f) continue;
            // The widest thing in the way makes the clearest picture.
            float score = c.bounds.size.x + c.bounds.size.z;
            if (score > bestScore) { bestScore = score; rock = p; }
        }
        // Walk the line that has open ground at both ends.
        side = Vector3.right;
        for (int i = 0; i < 12; i++)
        {
            Vector3 d = Quaternion.Euler(0f, i * 30f, 0f) * Vector3.right;
            if (Open(rock + d * 5.5f) && Open(rock - d * 6.5f)) { side = d; break; }
        }
        Debug.Log("NAV obstacle at " + rock + " side " + side + " width " + bestScore.ToString("0.0"));
    }

    static bool Open(Vector3 p) =>
        NavMesh.SamplePosition(p, out NavMeshHit hit, 0.4f, NavMesh.AllAreas) && !Physics.CheckSphere(p + Vector3.up, 1.4f);

    static void Face(Vector3 dir) => P.transform.rotation = Quaternion.LookRotation(dir);

    // Game camera: behind `from`, looking along -from at `target`.
    static void Look(Vector3 target, Vector3 from, float pitch)
    {
        ThirdPersonCamera cam = Game.I.Cam;
        typeof(ThirdPersonCamera).GetField("yaw", Any).SetValue(cam, Mathf.Atan2(-from.x, -from.z) * Mathf.Rad2Deg);
        typeof(ThirdPersonCamera).GetField("pitch", Any).SetValue(cam, pitch);
        cam.Snap();
    }

    static void Report(string when)
    {
        NavMeshAgent a = Mop().GetComponent<NavMeshAgent>();
        Debug.Log("NAV " + when + ": " + Mop().Report().Replace("\n", " | ") + " straight="
            + (Mop().transform.position - P.transform.position).magnitude.ToString("0.0") + " onMesh=" + a.isOnNavMesh);
    }

    static void Shot(string name)
    {
        Directory.CreateDirectory("Logs/nav");
        ScreenCapture.CaptureScreenshot("Logs/nav/" + name + ".png", 2);
        Debug.Log("NAV captured " + name);
    }

    // A straight-down picture of `half` metres either side of `centre`, without the HUD.
    static void Top(string name, Vector3 centre, float half, bool overlay)
    {
        bool shown = NavField.Show;
        Transform layer = Game.I.LevelRoot.Find("NavMesh Overlay");
        if (layer != null) layer.gameObject.SetActive(overlay);

        const int W = 1800, H = 1350;
        var go = new GameObject("Top camera");
        var cam = go.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = half * H / W;
        cam.transform.position = new Vector3(centre.x, 70f, centre.z);
        cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        cam.nearClipPlane = 1f;
        cam.farClipPlane = 120f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.1f, 0.11f, 0.12f);
        var rt = new RenderTexture(W, H, 24);
        cam.targetTexture = rt;
        bool fog = RenderSettings.fog;
        RenderSettings.fog = false;
        cam.Render();
        RenderSettings.fog = fog;
        RenderTexture.active = rt;
        var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        Directory.CreateDirectory("Logs/nav");
        File.WriteAllBytes("Logs/nav/" + name + ".png", tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
        cam.targetTexture = null;
        UnityEngine.Object.DestroyImmediate(go);
        rt.Release();
        if (layer != null) layer.gameObject.SetActive(shown);
        Debug.Log("NAV captured " + name);
    }
}
