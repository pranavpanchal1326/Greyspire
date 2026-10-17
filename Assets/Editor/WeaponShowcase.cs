using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

// Review captures for the weapon families: each hero's three blows, played with the real
// animation graph and attack timing, photographed at wind-up, contact and follow-through
// from a side view and from behind the shoulder (roughly where the gameplay camera sits).
// Writes Logs/weapons/<hero>_<blow>_<phase>_<view>.png. Run through Tools/weaponshowcase.ps1.
[InitializeOnLoad]
public static class WeaponShowcase
{
    const string Session = "GreyspireWeaponShowcase";
    const BindingFlags Any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static readonly Vector3 Origin = new Vector3(1000f, 0f, 1000f);
    static readonly string[] HeroIds = { "pip", "bram", "nix" };
    static Game G => Game.I;
    static PlayerController P => G.Player;
    static object Call(object host, string method, params object[] args) => host.GetType().GetMethod(method, Any).Invoke(host, args);
    static void Set(object host, string field, object value) => host.GetType().GetField(field, Any).SetValue(host, value);
    static object Get(object host, string field) => host.GetType().GetField(field, Any).GetValue(host);

    static double readyAt, deadline;
    static int hero, blow, shot;
    static float attackClock;
    static bool swinging, staged;
    static double settleUntil;
    static bool probed;
    static int stowed, stowStage;
    static bool measuring = true;
    static GameObject floor;
    static readonly List<Enemy> dummies = new List<Enemy>();

    // Measures, for each candidate attack clip, when the weapon's striking point moves fastest
    // (the frame the cut lands) and which way it travels then, in the weapon's own axes.
    // Each clip is played through the hero's real animation system and sampled once per
    // rendered frame. Combo timings and grips are set from these numbers, not by eye.
    // Writes Reports/weapon-clip-probe.csv before the photographs are taken.
    static readonly string[] ProbeHeroes = { "bram", "nix" };
    static readonly string[] ProbeClips = { "Sword_Regular_A", "Sword_Regular_B", "Sword_Regular_C", "Sword_Attack",
        "TreeChopping_Loop", "Melee_Hook", "Sword_Dash_RM", "Punch_Jab", "Punch_Cross" };
    static readonly System.Text.StringBuilder probeCsv = new System.Text.StringBuilder(
        "hero,prop,clip,length,peak_time,peak_fraction,peak_speed,lead_x,lead_y,lead_z,horizontal,edge_leads,edge_turn\n");
    static int probeHero, probeClip = -1, probeFrame;
    static float probeStart, probeBest, probeBestTime, probeLength;
    static Vector3 probeLast, probeLead, probeVel;
    static bool probeHasLast;

    // Returns true once every clip has been measured.
    static bool ProbeStep()
    {
        if (probeHero >= ProbeHeroes.Length) return true;
        var clips = (CharAnim)Get(P, "clips");
        var trail = (TrailRenderer)Get(P, "trail");
        if (probeClip < 0)
        {
            hero = Array.IndexOf(HeroIds, ProbeHeroes[probeHero]);
            blow = 0;
            Stage();
            probeClip = 0;
            probeStart = -1f;
            return false;
        }
        string name = ProbeClips[probeClip];
        if (probeStart < 0f)
        {
            probeLength = clips.Length(name);
            clips.Release();
            clips.OnceAt(name, 1f, probeLength + 0.3f, 0.09f);
            probeStart = Time.time;
            probeBest = -1f;
            probeHasLast = false;
            probeFrame = Time.frameCount;
            return false;
        }
        if (Time.frameCount == probeFrame) return false;
        float dt = Time.deltaTime;
        probeFrame = Time.frameCount;
        float t = Time.time - probeStart;
        Vector3 tip = P.transform.InverseTransformPoint(trail.transform.position);
        if (probeHasLast && dt > 0f && t > probeLength * 0.1f && t < probeLength * 0.8f)
        {
            Vector3 vel = (tip - probeLast) / dt;
            if (vel.magnitude > probeBest)
            {
                probeBest = vel.magnitude; probeBestTime = t; probeVel = vel;
                probeLead = trail.transform.parent.InverseTransformDirection(P.transform.TransformDirection(vel)).normalized;
            }
        }
        probeLast = tip;
        probeHasLast = true;
        if (t < probeLength) return false;
        bool horizontal = Mathf.Abs(probeVel.y) < new Vector2(probeVel.x, probeVel.z).magnitude;
        // How squarely the edge (the side the mesh reaches furthest to) faces the motion: 1 is a clean cut.
        var mesh = trail.transform.parent.GetComponentInChildren<MeshFilter>();
        Bounds mb = mesh.sharedMesh.bounds; Vector3 ms = mb.size;
        int along = ms.x >= ms.y && ms.x >= ms.z ? 0 : ms.y >= ms.z ? 1 : 2, thin = ms.x <= ms.y && ms.x <= ms.z ? 0 : ms.y <= ms.z ? 1 : 2;
        // The props built by Tools/build_weapons.py keep their edge (axe head, seax edge) on mesh +X.
        Vector3 edgeDir = Vector3.right;
        Vector3 leadMesh = mesh.transform.InverseTransformDirection(P.transform.TransformDirection(probeVel)).normalized;
        float edgeLeads = Vector3.Dot(leadMesh, edgeDir);
        // The extra turn about the weapon's length that would put the edge square into this cut.
        Vector3 lengthAxis = Vector3.zero; lengthAxis[along] = 1f;
        float edgeTurn = Vector3.SignedAngle(edgeDir, Vector3.ProjectOnPlane(leadMesh, lengthAxis), lengthAxis);
        string line = string.Join(",", ProbeHeroes[probeHero], P.Hero.weapon, name, probeLength.ToString("0.###"), probeBestTime.ToString("0.###"),
            (probeBestTime / probeLength).ToString("0.###"), probeBest.ToString("0.##"), probeLead.x.ToString("0.##"), probeLead.y.ToString("0.##"),
            probeLead.z.ToString("0.##"), horizontal, edgeLeads.ToString("0.##"), edgeTurn.ToString("0"));
        probeCsv.AppendLine(line);
        Debug.Log("PROBE " + line);
        probeStart = -1f;
        if (++probeClip >= ProbeClips.Length) { probeClip = -1; probeHero++; }
        if (probeHero < ProbeHeroes.Length) return false;
        Directory.CreateDirectory("Reports");
        File.WriteAllText("Reports/weapon-clip-probe.csv", probeCsv.ToString());
        Debug.Log("PROBE DONE");
        hero = 0; blow = 0; staged = false;
        return true;
    }

    static WeaponShowcase()
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

    static void Stage()
    {
        foreach (var e in Enemy.All.ToArray()) UnityEngine.Object.DestroyImmediate(e.gameObject);
        dummies.Clear();
        G.enabled = P.enabled = false;
        Set(G, "<InTown>k__BackingField", false);
        Set(G, "<InWilds>k__BackingField", false);
        Call(G, "SetState", Game.GameState.Playing);
        int index = Array.FindIndex(Heroes.All, h => h.id == HeroIds[hero]);
        if (P.Hero.id != HeroIds[hero]) P.SetHero(index);
        P.enabled = false;
        P.stats = new PlayerStats();
        P.Revive(Origin + Vector3.up * 0.05f, true);
        P.transform.rotation = Quaternion.identity;
        Call(P, "CancelOffense");
        Set(P, "comboStep", blow);
        Set(P, "dashCd", 0f);
        if (floor == null)
        {
            floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Showcase floor";
            floor.transform.position = Origin;
            floor.transform.localScale = Vector3.one * 3f;
            floor.GetComponent<Renderer>().material.color = new Color(0.36f, 0.34f, 0.31f);
        }
        // Two dummies: one dead ahead at mid reach, one off to the side, so the arc reads.
        foreach (var (bearing, gap) in new[] { (0f, 1.4f), (60f, 1.6f) })
        {
            var e = Enemy.Create(Enemy.Kind.Minion, Origin, 0);
            Vector3 dir = Quaternion.Euler(0f, bearing, 0f) * Vector3.forward;
            e.transform.position = Origin + dir * (gap + e.radius);
            e.transform.rotation = Quaternion.LookRotation(-dir);
            e.hp = e.maxHp = 100000f;
            Set(e, "cooldown", 1000f);
            dummies.Add(e);
        }
        Physics.SyncTransforms();
    }

    static int swingFrame;
    static Vector3 tipLast, tipVel;
    static Vector3 Tip() => ((TrailRenderer)Get(P, "trail")).transform.position;

    // Around contact (80 ms before to 40 ms after): how squarely the weapon's edge (mesh +X for
    // the props built by Tools/build_weapons.py) faces the way the striking point is moving,
    // averaged with each frame weighted by its speed. 1 is a clean cut. correction is the extra
    // wrist twist (HeroDef.comboTwist units) that would square it.
    static readonly System.Text.StringBuilder edgeCsv = new System.Text.StringBuilder("hero,blow,clip,edge_dot,consistency,twist_degrees,thrust\n");
    static Vector3 edgeSum;
    static float edgeWeight;

    static void EdgeSample(float contactAt)
    {
        if (attackClock < contactAt - 0.08f || attackClock > contactAt + 0.04f) return;
        var mesh = ((TrailRenderer)Get(P, "trail")).transform.parent.GetComponentInChildren<MeshFilter>();
        Vector3 v = mesh.transform.InverseTransformDirection(tipVel);
        Vector3 sz = mesh.sharedMesh.bounds.size, length = Vector3.zero;
        length[sz.x >= sz.y && sz.x >= sz.z ? 0 : sz.y >= sz.z ? 1 : 2] = 1f;
        edgeSum += Vector3.ProjectOnPlane(v, length);
        edgeWeight += v.magnitude;
    }

    static void EdgeReport()
    {
        var mesh = ((TrailRenderer)Get(P, "trail")).transform.parent.GetComponentInChildren<MeshFilter>();
        Vector3 sz = mesh.sharedMesh.bounds.size, length = Vector3.zero;
        length[sz.x >= sz.y && sz.x >= sz.z ? 0 : sz.y >= sz.z ? 1 : 2] = 1f;
        Vector3 lead = edgeSum.normalized;
        // Consistency: 1 when every frame moved the same way, near 0 when the direction swung about.
        float consistency = edgeWeight > 0f ? edgeSum.magnitude / edgeWeight : 0f;
        edgeCsv.AppendLine(string.Join(",", HeroIds[hero], blow + 1, P.Hero.combo[blow], Vector3.Dot(lead, Vector3.right).ToString("0.##"),
            consistency.ToString("0.##"), P.Hero.comboTwist != null ? P.Hero.comboTwist[blow].ToString("0") : "0", P.Weapon.steps[blow].thrust));
        Debug.Log("EDGE " + HeroIds[hero] + " blow " + (blow + 1) + " " + P.Hero.combo[blow] + " edge_dot=" + Vector3.Dot(lead, Vector3.right).ToString("0.##")
            + " consistency=" + consistency.ToString("0.##") + " twist=" + ((float)Get(P, "twist")).ToString("0")
            + " correction=" + Vector3.SignedAngle(Vector3.right, lead, length).ToString("0"));
        edgeSum = Vector3.zero; edgeWeight = 0f;
    }

    static void Photo(string phase)
    {
        Directory.CreateDirectory("Logs/weapons");
        string stem = "Logs/weapons/" + HeroIds[hero] + "_" + (blow + 1) + "_" + phase;
        Snap(stem + "_side.png", 75f, 4.2f, 1.1f, 0.9f);
        Snap(stem + "_back.png", 200f, 4.6f, 2.2f, 1.0f);
    }

    static void Snap(string path, float yaw, float distance, float camHeight, float focusHeight)
    {
        var go = new GameObject("ShowcaseCam");
        var cam = go.AddComponent<Camera>();
        cam.fieldOfView = 45f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.5f, 0.55f, 0.62f);
        Vector3 focus = Origin + Vector3.up * focusHeight + Vector3.forward * 0.8f;
        go.transform.position = Origin + Quaternion.Euler(0f, yaw, 0f) * Vector3.forward * distance + Vector3.up * camHeight;
        go.transform.LookAt(focus);
        var rt = new RenderTexture(720, 540, 24);
        var tex = new Texture2D(720, 540, TextureFormat.RGB24, false);
        var previous = RenderTexture.active;
        try
        {
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, 720, 540), 0, 0);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            cam.targetTexture = null;
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(tex);
            UnityEngine.Object.DestroyImmediate(go);
        }
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        if (readyAt == 0) { readyAt = EditorApplication.timeSinceStartup + 2; deadline = readyAt + 400; }
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Weapon showcase timed out");
            if (G == null || P == null || EditorApplication.timeSinceStartup < readyAt) return;
            Application.runInBackground = true;
            if (!probed) { probed = ProbeStep(); return; }
            if (hero >= HeroIds.Length && stowed < HeroIds.Length)
            {
                // Weapons put away, as they are worn in town: front and back three-quarter views.
                int index = Array.FindIndex(Heroes.All, h => h.id == HeroIds[stowed]);
                if (stowStage == 0)
                {
                    foreach (var e in Enemy.All.ToArray()) UnityEngine.Object.DestroyImmediate(e.gameObject);
                    P.SetHero(index); P.enabled = false;
                    P.Revive(Origin + Vector3.up * 0.05f, true);
                    P.transform.rotation = Quaternion.identity;
                    settleUntil = EditorApplication.timeSinceStartup + 0.6;
                    stowStage = 1;
                    return;
                }
                if (EditorApplication.timeSinceStartup < settleUntil) return;
                Call(P, "SetArmed", false);
                Snap("Logs/weapons/" + HeroIds[stowed] + "_stowed_back.png", 160f, 2.8f, 1.5f, 1.1f);
                Snap("Logs/weapons/" + HeroIds[stowed] + "_stowed_front.png", 25f, 2.8f, 1.5f, 1.1f);
                Call(P, "SetArmed", true);
                stowed++; stowStage = 0;
                return;
            }
            if (hero >= HeroIds.Length)
            {
                File.WriteAllText("Reports/weapon-edge-alignment.csv", edgeCsv.ToString());
                Debug.Log("SHOWCASE DONE " + Directory.GetFiles("Logs/weapons", "*.png").Length + " images");
                Finish(0);
                return;
            }
            if (!staged)
            {
                Stage();
                staged = true;
                settleUntil = EditorApplication.timeSinceStartup + 0.6;   // let the idle pose blend in
                return;
            }
            if (EditorApplication.timeSinceStartup < settleUntil) return;
            float dt = Time.deltaTime;   // the same clock CharAnim advances the pose with
            if (!swinging)
            {
                Call(P, "StartAttack", Vector3.forward);
                swinging = true;
                attackClock = 0f;
                shot = 0;
                swingFrame = Time.frameCount;
                tipLast = Tip();
                return;
            }
            // One step per rendered frame, so the attack clock and the animation stay in step.
            if (Time.frameCount == swingFrame) return;
            swingFrame = Time.frameCount;
            Call(P, "TickAttack", dt, Vector3.forward);
            Call(P, "TickTwist", dt);
            attackClock += dt;
            Vector3 tipNow = Tip();
            tipVel = (tipNow - tipLast) / Mathf.Max(1e-4f, dt);
            tipLast = tipNow;
            var stepNow = P.Weapon.steps[blow];
            if (measuring) EdgeSample((float)Get(P, "attackDur") * stepNow.contact);
            float dur = (float)Get(P, "attackDur");
            var step = P.Weapon.steps[blow];
            float[] at = { dur * step.contact * 0.45f, dur * step.contact + 0.02f, dur * step.contact + 0.16f };
            string[] names = { "a_windup", "b_contact", "c_follow" };
            // Each blow is swung twice: once only to measure the edge (photographs stall a frame,
            // which would spoil the measurement), then again to be photographed.
            if (shot < at.Length && attackClock >= at[shot])
            {
                if (measuring) { if (shot == 2) EdgeReport(); }
                else Photo(names[shot]);
                shot++;
            }
            if (shot < at.Length) return;
            swinging = false;
            staged = false;
            measuring = !measuring;
            if (measuring && ++blow > 2) { blow = 0; hero++; }
        }
        catch (Exception e)
        {
            Debug.LogError("SHOWCASE FAILED " + e);
            Finish(1);
        }
    }

    static void Finish(int code)
    {
        SessionState.SetBool(Session, false);
        EditorApplication.update -= Tick;
        EditorApplication.Exit(code);
    }
}
