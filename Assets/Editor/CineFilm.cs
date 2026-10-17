using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Shoots the trailer. Run from the command line:
//   Tools\cine.ps1 [-Scout] [-Only town,king]      (Unity -projectPath . -executeMethod CineFilm.Run)
// The game plays as it always does; this takes the camera away from the player and moves it
// like a film camera: dollies, cranes and orbits, long lenses with depth of field (FilmLens),
// and slow motion by filming more frames a second than the film plays back. Every frame is
// rendered straight from the game camera at 1920 x 1080 with no HUD, into Logs/cine/<shot>/.
// A scout run (-Scout) saves one frame in twelve, to check framing before a full shoot.
[InitializeOnLoad]
public static class CineFilm
{
    const string Key = "CineFilmStage";
    const int W = 1920, H = 1080, Fps = 24;
    const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;

    struct Pose
    {
        public Vector3 pos, look;
        public float fov, focus, blur, range, roll;
    }

    class Step
    {
        public int frames;
        public Action act;                          // preparation: runs after waiting `frames`
        public string shot;                         // a take: `frames` are filmed
        public Func<Func<float, Pose>> plan;        // called as the take starts, so it can read where things stand
        public Action<int> each;                    // called every filmed frame
        public int rate = Fps;                      // frames simulated per second of game time: 72 = three times slower
        public float shake;
    }

    static List<Step> steps;
    static int at, since, lastFrame = -1, filmed, moves;
    static float nextMove;
    static bool scout, brawl;
    static string[] only;
    static Vector3 drive;
    static Func<float, Pose> cam;
    static RenderTexture rt;
    static Texture2D tex;
    static FilmLens lens;
    static string outDir;

    static PlayerController P => Game.I.Player;
    static Camera C => Game.I.Cam.GetComponent<Camera>();
    static PostFx Post => C.GetComponent<PostFx>();

    static CineFilm()
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

    static void Begin()
    {
        string[] plan = File.Exists("Logs/cine_plan.txt") ? File.ReadAllLines("Logs/cine_plan.txt") : new string[0];
        scout = plan.Length > 0 && plan[0].Trim() == "scout";
        only = plan.Length > 1 && plan[1].Trim().Length > 0 && plan[1].Trim() != "*" ? plan[1].Trim().Split(',') : null;
        outDir = scout ? "Logs/cine_scout" : "Logs/cine";

        // Without this the game stops whenever the editor window is not the one in front,
        // and a shoot left running in the background hangs.
        Application.runInBackground = true;
        Time.captureFramerate = Fps;
        Save.Quality = 2;
        Game.I.ApplyQuality();
        QualitySettings.shadowDistance = 110f;
        QualitySettings.lodBias = 3f;
        rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
        tex = new Texture2D(W, H, TextureFormat.RGB24, false);
        lens = C.gameObject.AddComponent<FilmLens>();
        Post.grain = 0f;            // grain goes on in the edit, over the finished picture
        Post.vignette = 0.3f;

        steps = new List<Step>();
        Script();
        at = 0;
        since = 0;
        Debug.Log("CINE begin: " + steps.Count + " steps, " + (scout ? "scout" : "full") + (only != null ? ", only " + string.Join(",", only) : ""));
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying || Game.I == null) return;
        if (Time.frameCount == lastFrame) return;
        lastFrame = Time.frameCount;
        try
        {
            if (steps == null) Begin();

            P.hp = P.maxHp;
            typeof(PlayerController).GetField("invuln", Any).SetValue(P, 1f);
            if (drive != Vector3.zero)
            {
                typeof(PlayerController).GetField("planar", Any).SetValue(P, drive);
                P.transform.rotation = Quaternion.LookRotation(drive);
            }
            if (brawl && Game.I.State == Game.GameState.Playing && Time.time >= nextMove)
            {
                nextMove = Time.time + 0.7f;
                int which = moves++ % 5;
                Smoke("ResetMove");
                Enemy e = Nearest();
                if (e != null)
                {
                    Vector3 to = e.transform.position - P.transform.position;
                    to.y = 0f;
                    if (to.sqrMagnitude > 0.01f) P.transform.rotation = Quaternion.LookRotation(to);
                }
                if (which == 1) Move("StartHeavy", 0.8f);
                else if (which == 3) Move("StartStab", Vector3.zero);
                else if (which == 4) Move("StartKick");
                else Move("StartAttack", Vector3.zero);
            }

            if (at >= steps.Count)
            {
                Debug.Log("CINE DONE frames=" + filmed);
                SessionState.SetInt(Key, 0);
                EditorApplication.Exit(0);
                return;
            }
            Step s = steps[at];
            if (s.shot == null)
            {
                if (++since < s.frames) return;
                s.act?.Invoke();
                at++;
                since = 0;
                return;
            }

            if (since == 0)
            {
                Game.I.Cam.enabled = false;
                Time.captureFramerate = s.rate;
                cam = s.plan();
                string folder = outDir + "/" + s.shot;
                if (Directory.Exists(folder)) Directory.Delete(folder, true);
                Debug.Log("CINE roll " + s.shot + " " + s.frames + " frames at " + s.rate);
            }
            if (s.rate > Fps) Juice.Reset();        // a hit-stop made for real time is far too long in slow motion
            s.each?.Invoke(since);
            Apply(cam(s.frames > 1 ? since / (float)(s.frames - 1) : 0f), s.shake, since);
            if (!scout || since % 12 == 0 || since == s.frames - 1) Capture(s.shot, since);
            if (++since < s.frames) return;
            Time.captureFramerate = Fps;
            at++;
            since = 0;
        }
        catch (Exception e)
        {
            Debug.LogError("CINE FAILED at step " + at + ": " + e);
            SessionState.SetInt(Key, 0);
            EditorApplication.Exit(1);
        }
    }

    static void Apply(Pose p, float shake, int frame)
    {
        Transform t = C.transform;
        // A camera on a shoulder is never perfectly still: a slow, small drift.
        float n = frame * 0.035f;
        Vector3 sway = new Vector3(Mathf.PerlinNoise(n, 1.7f) - 0.5f, Mathf.PerlinNoise(4.2f, n) - 0.5f, Mathf.PerlinNoise(n, n + 9f) - 0.5f) * (0.03f + shake);
        Vector3 pos = p.pos + sway;
        pos.y = Mathf.Max(pos.y, 0.25f);
        t.position = pos;
        t.rotation = Quaternion.LookRotation(p.look - pos) * Quaternion.Euler(0f, 0f, p.roll);
        C.fieldOfView = p.fov > 0f ? p.fov : 40f;
        lens.focus = p.focus > 0f ? p.focus : (p.look - pos).magnitude;
        lens.range = p.range > 0f ? p.range : lens.focus * 0.6f;
        lens.blur = p.blur;
    }

    static void Capture(string shot, int frame)
    {
        Post.impact = 0f;
        Post.lines = 0f;
        Post.aberration = 0f;
        Camera c = C;
        c.targetTexture = rt;
        c.Render();
        c.targetTexture = null;
        RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
        RenderTexture.active = null;
        string dir = outDir + "/" + shot;
        Directory.CreateDirectory(dir);
        File.WriteAllBytes(dir + "/f_" + frame.ToString("00000") + ".jpg", tex.EncodeToJPG(scout ? 80 : 96));
        filmed++;
    }

    // ------------------------------------------------------------ building the shot list

    static bool groupOn = true;

    static void Group(string name, Action build)
    {
        groupOn = only == null || Array.IndexOf(only, name) >= 0;
        if (groupOn) build();
        groupOn = true;
    }

    static void Do(float seconds, Action act) =>
        steps.Add(new Step { frames = Mathf.Max(1, Mathf.RoundToInt(seconds * Fps)), act = act });

    static void Roll(string shot, float seconds, Func<Func<float, Pose>> plan, int rate = Fps, Action<int> each = null, float shake = 0f) =>
        steps.Add(new Step { shot = shot, frames = Mathf.RoundToInt(seconds * Fps), plan = plan, rate = rate, each = each, shake = shake });

    // ------------------------------------------------------------ camera moves

    static float Smooth(float t) => t * t * (3f - 2f * t);

    // A straight move from one place to another, looking from one point to another.
    static Func<float, Pose> Dolly(Vector3 from, Vector3 to, Vector3 lookFrom, Vector3 lookTo, float fov, float fovEnd = 0f, float blur = 0f, float range = 0f, bool ease = false)
    {
        return t =>
        {
            float k = ease ? Smooth(t) : t;
            return new Pose { pos = Vector3.Lerp(from, to, k), look = Vector3.Lerp(lookFrom, lookTo, k), fov = Mathf.Lerp(fov, fovEnd > 0f ? fovEnd : fov, k), blur = blur, range = range };
        };
    }

    // Circles a point: angles in degrees clockwise from north, as seen from above.
    static Func<float, Pose> Orbit(Func<Vector3> centre, float angle, float angleEnd, float radius, float radiusEnd, float height, float heightEnd, float fov, float blur = 0f, float range = 0f, float lookUp = 0f)
    {
        return t =>
        {
            Vector3 c = centre();
            float a = Mathf.Lerp(angle, angleEnd, t) * Mathf.Deg2Rad, r = Mathf.Lerp(radius, radiusEnd, t);
            Vector3 pos = new Vector3(c.x + Mathf.Sin(a) * r, Mathf.Lerp(height, heightEnd, t), c.z + Mathf.Cos(a) * r);
            return new Pose { pos = pos, look = c + Vector3.up * lookUp, fov = fov, blur = blur, range = range };
        };
    }

    // Travels with something: offsets are given in its own left/up/forward.
    static Func<float, Pose> Follow(Transform who, Vector3 from, Vector3 to, Vector3 lookAt, float fov, float blur = 0f, float range = 0f)
    {
        Quaternion facing = Quaternion.Euler(0f, who.eulerAngles.y, 0f);
        return t =>
        {
            Vector3 basePos = who.position;
            return new Pose { pos = basePos + facing * Vector3.Lerp(from, to, t), look = basePos + facing * lookAt, fov = fov, blur = blur, range = range };
        };
    }

    // The compass direction the light comes from, flat on the ground.
    static Vector3 SunSide()
    {
        Vector3 d = Post.sunDir;
        d.y = 0f;
        return d.sqrMagnitude > 0.01f ? d.normalized : Vector3.back;
    }

    // Travels beside someone, on the side the light falls from (turned by `bearing` degrees),
    // so the face that looks at the lens is the lit one.
    static Func<float, Pose> Track(Transform who, float bearing, float dist, float distEnd, float height, float heightEnd, float fov, float blur = 0f, float range = 0f, float lookUp = 1.2f)
    {
        Vector3 side = Quaternion.Euler(0f, bearing, 0f) * SunSide();
        return t =>
        {
            Vector3 b = who.position;
            return new Pose { pos = b + side * Mathf.Lerp(dist, distEnd, t) + Vector3.up * Mathf.Lerp(height, heightEnd, t), look = b + Vector3.up * lookUp, fov = fov, blur = blur, range = range };
        };
    }

    // ------------------------------------------------------------ the shot list

    static void Script()
    {
        Do(2.5f, () => P.SetHero(0));

        // ---- Brushwick late in the day: the valley as it should be.
        Group("town", () =>
        {
            Do(0.5f, () => Town(17.0f));
            Do(1f, EndIntro);
            Do(4f, () => { HeroAt(new Vector3(0f, 0.1f, -3f), 0f); });
            Do(1f, () => { });
            // High over the south road, sinking toward the square: the tower stands over all of it.
            Roll("t1_valley", 7f, () => Dolly(new Vector3(-14f, 21f, -52f), new Vector3(-8f, 10.5f, -32f), new Vector3(0f, 6f, 6f), new Vector3(0f, 9f, 24f), 42f));
            // The well and the people round it, turning.
            Roll("t3_well", 5f, () => Orbit(() => new Vector3(0f, 1.2f, 0f), 222f, 190f, 18f, 15.5f, 5.2f, 6f, 36f, 3f, 14f, 0.8f));
            Roll("t2_square", 5f, () => Dolly(new Vector3(11f, 2.4f, -8f), new Vector3(7f, 2.6f, -2f), new Vector3(-10f, 2.4f, 0f), new Vector3(-12f, 3.2f, 3f), 38f, 0f, 4f, 14f));
            // The hero walks up the north road; the dark tower fills the frame ahead.
            Do(0.3f, () => { Hour(17.35f); HeroAt(new Vector3(0f, 0.1f, 6f), 0f); drive = Vector3.forward * 1.7f; });
            Do(1.5f, () => { });
            Roll("t4_walk", 7f, () => Follow(P.transform, new Vector3(1.6f, 1.1f, -4.2f), new Vector3(0.9f, 0.7f, -3f), new Vector3(0f, 3.6f, 12f), 34f, 3f, 10f));
            Do(0.2f, () => drive = Vector3.zero);
        });

        // ---- The hero, close: the boy who carried their bags.
        Group("hero", () =>
        {
            Do(0.5f, () => Town(17.3f));
            Do(1f, EndIntro);
            Do(3f, () => HeroAt(new Vector3(1.5f, 0.1f, -6f), -90f));
            Do(2f, () => { });
            Roll("h1_face", 7f, () => Follow(P.transform, new Vector3(0.75f, 1.72f, 2.5f), new Vector3(0.45f, 1.74f, 1.5f), new Vector3(0f, 1.66f, 0f), 26f, 9f, 1.6f));
        });

        // ---- Night: the windows are lit and the tower is not.
        Group("night", () =>
        {
            Do(0.5f, () => Town(20.4f));
            Do(1f, EndIntro);
            Do(4f, () => HeroAt(new Vector3(-2f, 0.1f, -4f), 20f));
            Do(1f, () => { });
            Roll("n1_lamps", 5f, () => Dolly(new Vector3(-11f, 3.5f, -12.5f), new Vector3(-8f, 4.1f, -8.5f), new Vector3(2f, 3f, 6f), new Vector3(1f, 5.5f, 14f), 36f, 0f, 3f, 18f));
            Roll("n2_tower", 6f, () => Dolly(new Vector3(3f, 1.2f, 12f), new Vector3(2.2f, 2.4f, 15f), new Vector3(0f, 6f, 28f), new Vector3(0f, 13f, 29f), 34f));
        });

        // ---- A drained floor: grey grass, a path, and one small figure on it.
        Group("floor", () =>
        {
            Do(0.5f, () => Climb(1));
            Do(2.5f, () => Smoke("Settle"));
            Do(1f, () => { Smoke("Settle"); HeroAt(new Vector3(PathX(-33f), 0.1f, -33f), 0f); drive = PathDir(-33f) * 1.7f; });
            Do(1f, () => { });
            Roll("f1_grey", 6f, () => Dolly(new Vector3(PathX(-36f) + 2.5f, 0.7f, -39f), new Vector3(PathX(-36f) + 4f, 5.5f, -42f), new Vector3(PathX(-28f), 1.2f, -28f), new Vector3(PathX(-14f), 2f, -14f), 38f));
            // Coming back down the path, toward the light: the face is lit.
            Do(0.3f, () => { HeroAt(new Vector3(PathX(-10f), 0.1f, -10f), 180f); drive = -PathDir(-10f) * 1.7f; });
            Do(1f, () => { });
            Roll("f2_steps", 5f, () => Follow(P.transform, new Vector3(-1.4f, 0.45f, 3.6f), new Vector3(-1.1f, 0.5f, 2.6f), new Vector3(0f, 0.95f, 0f), 32f, 7f, 2.5f));
            Do(0.2f, () => drive = Vector3.zero);
        });

        // ---- Floor 4: a Painter in the stone, and the colour coming back.
        Group("statue", () =>
        {
            Do(0.5f, () => { Save.RescuedMask = 0; Climb(3); });
            Do(2.5f, () => Smoke("Settle"));
            Do(1f, () => { Smoke("Settle"); FaceStatue(7f); });
            Do(1.5f, () => { });
            Roll("s1_stone", 7f, () => Orbit(() => Statue().position + Vector3.up * 1.45f, StatueYaw() + 50f, StatueYaw() - 10f, 3.1f, 2.2f, 1.5f, 1.75f, 28f, 9f, 1.6f));
            Roll("s2_hand", 4f, () => Orbit(() => Statue().position + Vector3.up * 1.2f, StatueYaw() - 70f, StatueYaw() - 40f, 2.2f, 1.9f, 1.0f, 1.3f, 30f, 8f, 1.2f, 0.25f));
            // The floor falls quiet and the colour floods back across the meadow.
            Do(0.3f, () => FaceStatue(2.4f));
            Do(0.6f, () => { });
            Roll("s3_colour", 6f, () => Dolly(Statue().position + new Vector3(9f, 4.5f, -13f), Statue().position + new Vector3(7f, 3.2f, -9f), Statue().position + Vector3.up, Statue().position + Vector3.up * 1.4f, 40f),
                Fps, f => { if (f == 24) Smoke("KillAll"); });
            Do(2.5f, () => Smoke("Settle"));
            // Paint her back: the game's own rescue scene, seen through the film camera.
            Do(0.5f, () => UseSpot("A drained Painter"));
            Roll("s4_thaw", 7f, () => Orbit(() => Statue().position + Vector3.up * 1.5f, StatueYaw() + 28f, StatueYaw() + 4f, 2.6f, 1.9f, 1.55f, 1.65f, 28f, 8f, 1.5f), 48);
            Do(0.2f, () => Smoke("Settle"));
            Do(0.5f, () => Smoke("Settle"));
        });

        // ---- Steel: the fight, slowed down. Six imps are put in the hero's way on the path,
        // and stood back in front of him before every take so the fight stays in frame.
        Group("combat", () =>
        {
            Do(0.5f, () => Climb(2));
            Do(2.5f, () => Smoke("Settle"));
            Do(1f, () => { Smoke("Settle"); Ambush(6); });
            Do(0.9f, () => brawl = true);
            Roll("c1_side", 4f, () => Orbit(Fight, 255f, 285f, 5.4f, 4.6f, 1.2f, 1.5f, 34f, 3f, 4f), 72, null, 0.02f);
            Do(0.05f, () => Restage(2.6f));
            Roll("c2_front", 4f, () => Orbit(Fight, 300f, 335f, 5.8f, 4.8f, 1.3f, 1.7f, 34f, 3f, 4f), 60, null, 0.02f);
            Do(0.05f, () => Restage(2.4f));
            Roll("c3_close", 4f, () => Orbit(() => P.transform.position + Vector3.up * 1.3f, 235f, 265f, 3.2f, 2.7f, 1.4f, 1.6f, 30f, 6f, 2.2f), 72, null, 0.03f);
            Do(0.05f, () => Restage(3f));
            Roll("c4_wide", 3f, () => Orbit(Fight, 250f, 232f, 9f, 8f, 2.6f, 2.2f, 36f), 36, null, 0.04f);
            Do(0.2f, () => brawl = false);
        });

        // ---- The first Guardian.
        Group("guardian", () =>
        {
            Do(0.5f, () => Climb(4));
            Do(2.5f, () => Smoke("Settle"));
            Do(1.2f, () => { Smoke("Settle"); HeroAt(new Vector3(0f, 0.1f, 18f), 0f); });
            Do(0.6f, () => { });
            Roll("g1_reveal", 4f, () => Follow(P.transform, new Vector3(1.2f, 0.5f, -3.4f), new Vector3(0.7f, 0.9f, -2.2f), new Vector3(0f, 2.4f, 10f), 30f, 4f, 9f), 48);
            Do(0.1f, () => { HeroAt(new Vector3(0f, 0.1f, 24f), 0f); brawl = true; });
            Do(0.6f, () => { });
            Roll("g2_fight", 5f, () => Orbit(() => Vector3.Lerp(P.transform.position, GuardianPos(), 0.5f) + Vector3.up * 1.3f, 245f, 295f, 8f, 6.5f, 1.0f, 1.6f, 34f, 2f, 6f), 72, null, 0.03f);
            Do(0.2f, () => brawl = false);
        });

        // ---- Two more lands on the way up: dunes, then crystal.
        Group("climb", () =>
        {
            foreach (int floor in new[] { 11, 21 })
            {
                int f = floor;
                Do(0.5f, () => Climb(f));
                Do(2.5f, () => Smoke("Settle"));
                Do(1f, () => { Smoke("Settle"); HeroAt(new Vector3(PathX(-28f), 0.1f, -28f), 0f); drive = PathDir(-28f) * 5f; });
                Do(0.6f, () => { });
                Roll("k" + f + "_run", 3f, () => Track(P.transform, f == 11 ? 55f : -50f, 6.5f, 5.5f, 1.0f, 1.3f, 34f, 3f, 6f));
                Do(0.2f, () => drive = Vector3.zero);
            }
        });

        // ---- The Wilds: the colour that is left.
        Group("wilds", () =>
        {
            Do(0.5f, () => { Save.BestFloor = 25; Call("EnterWilds"); });
            Do(1f, EndIntro);
            foreach (int region in new[] { 2, 6, 5, 8, 9, 3 })
            {
                int r = region;
                Do(1f, () => { Hour(r == 6 ? 17.2f : r == 8 ? 8.5f : r == 3 ? 16.6f : 14f); Region(r, 0f); });
                Do(0.6f, Clear);
                Do(2.6f, () => { Clear(); Region(r, 5f); });
                Do(0.5f, () => { });
                Roll("w" + r, 3f, () => Track(P.transform, r == 3 ? 140f : r % 2 == 0 ? 40f : -40f, 9f, 7.5f, 1.6f, 2.0f, 38f, 2f, 9f));
                Do(0.2f, () => drive = Vector3.zero);
            }
        });

        // ---- The top of the tower.
        Group("king", () =>
        {
            Do(0.5f, () => Climb(29));
            Do(3f, () => HeroAt(new Vector3(0f, 0.1f, 4f), 0f));
            Do(0.6f, () => { });
            // Side on: one small Painter, and what he climbed thirty floors to face.
            Roll("b1_standoff", 5f, () => Dolly(new Vector3(11f, 0.9f, 6.2f), new Vector3(9.4f, 1.2f, 6.8f), new Vector3(0f, 2.1f, 7.4f), new Vector3(0f, 2.3f, 7.6f), 36f));
            Roll("b2_king", 7f, () => Orbit(() => Boss().position + Vector3.up * 2.7f, KingYaw() + 205f, KingYaw() + 335f, 6f, 4.2f, 0.9f, 1.5f, 32f, 4f, 4f, 0.5f));
            Roll("b3_over", 5f, () => Follow(P.transform, new Vector3(0.9f, 1.2f, -2.6f), new Vector3(0.6f, 1.5f, -1.8f), new Vector3(0f, 2.6f, 7f), 30f, 4f, 6f));
            Do(0.2f, () => Smoke("Settle"));
        });

        // ---- The last picture: one Painter, and the tower.
        Group("end", () =>
        {
            Do(0.5f, () => Town(17.15f));
            Do(1f, EndIntro);
            Do(4f, () => HeroAt(new Vector3(0f, 0.1f, 11f), 0f));
            Do(1.5f, () => { });
            Roll("e1_tower", 10f, () => Dolly(new Vector3(0.5f, 1.3f, 6.5f), new Vector3(0.3f, 1.9f, 1f), new Vector3(0f, 3.4f, 27f), new Vector3(0f, 4.5f, 27f), 34f, 30f, 2f, 18f, true));
        });

        Do(1f, () => { });
    }

    // ------------------------------------------------------------ helpers

    static void Smoke(string method) => typeof(SmokeTest).GetMethod(method, Any).Invoke(null, null);

    static void UseSpot(string name) => typeof(SmokeTest).GetMethod("UseSpot", Any).Invoke(null, new object[] { name });

    static object Call(string method, params object[] args) => typeof(Game).GetMethod(method, Any).Invoke(Game.I, args);

    static void Move(string method, params object[] args)
    {
        MethodInfo m = typeof(PlayerController).GetMethod(method, Any);
        if (m != null && m.GetParameters().Length == args.Length) m.Invoke(P, args);
    }

    static void Hour(float hour) => Call("SkipTo", hour);

    // Brushwick at its largest, everyone home, at the given hour.
    static void Town(float hour)
    {
        Save.BestFloor = 25;
        Save.RescuedMask = 31;
        Call("EnterHub");
        Hour(hour);
    }

    static void EndIntro()
    {
        typeof(Game).GetField("introT", Any).SetValue(Game.I, 0f);
        Game.I.Cam.SkipEntrance();
        Smoke("Settle");
    }

    static void Climb(int towerFloor)
    {
        drive = Vector3.zero;
        JourneyStore.SetInt("towerFloor", towerFloor);
        JourneyStore.SetInt("storyFloor", towerFloor);
        Call("StartRun");
    }

    static void HeroAt(Vector3 pos, float yaw)
    {
        drive = Vector3.zero;
        P.Teleport(pos);
        P.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
    }

    static float HeroYaw() => P.transform.eulerAngles.y;

    static float PathX(float z) => 9f * Mathf.Cos(z * Mathf.PI / 76f);

    static Vector3 PathDir(float z) => new Vector3(PathX(z + 2f) - PathX(z), 0f, 2f).normalized;

    static Transform Statue() => GameObject.Find("Drained Painter").transform;

    // The compass bearing the statue faces, for Orbit().
    static float StatueYaw() => Statue().eulerAngles.y;

    static void FaceStatue(float distance)
    {
        Transform s = Statue();
        Vector3 front = s.forward; front.y = 0f; front.Normalize();
        HeroAt(s.position + front * distance + Vector3.up * 0.05f, 0f);
        P.transform.rotation = Quaternion.LookRotation(-front);
    }

    static Enemy Nearest()
    {
        Enemy best = null;
        foreach (Enemy e in Enemy.All)
            if (best == null || (e.transform.position - P.transform.position).sqrMagnitude < (best.transform.position - P.transform.position).sqrMagnitude) best = e;
        return best;
    }

    static void Engage(float distance)
    {
        Enemy best = Nearest();
        if (best == null) return;
        Vector3 from = P.transform.position - best.transform.position;
        from.y = 0f;
        from = from.sqrMagnitude > 0.01f ? from.normalized : Vector3.back;
        HeroAt(best.transform.position + from * distance + Vector3.up * 0.05f, 0f);
        P.transform.rotation = Quaternion.LookRotation(-from);
    }

    // Stands the hero on the path facing the light, and puts imps in front of him.
    static void Ambush(int imps)
    {
        Vector3 at = new Vector3(PathX(-14f), 0.1f, -14f);
        HeroAt(at, 180f);
        for (int i = 0; i < imps; i++)
        {
            float a = (i - (imps - 1) * 0.5f) * 0.5f;
            Enemy e = Enemy.Create(Enemy.Kind.Minion, at + new Vector3(Mathf.Sin(a) * 4.2f, 0f, -Mathf.Cos(a) * (3.4f + (i % 2) * 1.1f)), 0);
            e.aggro = 40f;
            e.maxHp = e.hp = 260f;       // sturdy enough to last the take
        }
    }

    // Puts every imp back in an arc in front of the hero, who turns to face them.
    static void Restage(float radius)
    {
        P.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        Vector3 h = P.transform.position;
        var imps = new List<Enemy>();
        foreach (Enemy e in Enemy.All) if (e.kind == Enemy.Kind.Minion && !e.Dead) imps.Add(e);
        for (int i = 0; i < imps.Count; i++)
        {
            float a = (i - (imps.Count - 1) * 0.5f) * 0.52f;
            Vector3 to = h + new Vector3(Mathf.Sin(a), 0f, -Mathf.Cos(a)) * (radius + (i % 2) * 1f);
            var body = imps[i].GetComponent<CharacterController>();
            if (body != null) body.enabled = false;
            imps[i].transform.position = new Vector3(to.x, 0.1f, to.z);
            imps[i].transform.rotation = Quaternion.LookRotation(h - to);
            if (body != null) body.enabled = true;
        }
    }

    // Empties the land of whatever lives there, quietly: nothing is counted as a kill.
    static void Clear()
    {
        foreach (Enemy e in new List<Enemy>(Enemy.All)) e.Vanish();
    }

    // The middle of the fight: between the hero and whatever is nearest.
    static Vector3 Fight()
    {
        Enemy e = Nearest();
        Vector3 h = P.transform.position;
        return (e != null && (e.transform.position - h).magnitude < 7f ? Vector3.Lerp(h, e.transform.position, 0.45f) : h) + Vector3.up * 1.1f;
    }

    static float KingYaw() => Boss().eulerAngles.y;

    static Vector3 GuardianPos()
    {
        foreach (Enemy e in Enemy.All) if (!string.IsNullOrEmpty(e.title)) return e.transform.position;
        Enemy n = Nearest();
        return n != null ? n.transform.position : P.transform.position + Vector3.forward * 6f;
    }

    static Transform Boss()
    {
        foreach (Enemy e in Enemy.All) if (e.IsBoss) return e.transform;
        return P.transform;
    }

    // Stands the hero at the edge of a region of the Wilds and sets them running into it.
    static void Region(int index, float speed)
    {
        Vector3 spot = Game.I.RegionSpot(index, out float yaw);
        HeroAt(spot, yaw);
        drive = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward * speed;
    }
}
