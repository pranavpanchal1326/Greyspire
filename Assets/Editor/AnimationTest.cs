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
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
#endif

// Phase nine, packages A01 and A02, measured in play on the real hero with real input.
//  A01: the gait ledger and directional set; planted-foot slip in world space while running,
//       side-stepping and backing off locked on, with the directional set off (the old behaviour)
//       and on; the chest still facing the target when the hips turn; turning on the spot; the stop.
//  A02: soles against a slope and a step with foot placement off and on; nothing changed on flat
//       ground; the great axe's off-hand grip in locomotion.
// Results: Logs/anim/results.csv, captures in Logs/anim/.
[InitializeOnLoad]
public static class AnimationTest
{
    const string Session = "GreyspireAnimationTest";
    const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static readonly Vector3 Origin = new Vector3(1200f, 0f, 1200f);
    class Step { public float wait; public Action action; }
    static List<Step> steps;
    static int at, passed;
    static double next, deadline;
    static readonly List<string> errors = new List<string>();
    static readonly StringBuilder csv = new StringBuilder("scenario,mode,speed,slip,ratio,extra\n");
    static Game G => Game.I;
    static PlayerController P => G.Player;
    static object Get(object host, string name) => host.GetType().GetField(name, Any).GetValue(host);
    static void Set(object host, string name, object value) => (host.GetType().GetField(name, Any) ?? host.GetType().GetField("<" + name + ">k__BackingField", Any)).SetValue(host, value);
    static object Call(object host, string name, params object[] args) => host.GetType().GetMethod(name, Any).Invoke(host, args);
    static string F(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); passed++; Debug.Log("ANIM PASS " + message); }
    static void Do(float delay, Action action) => steps.Add(new Step { wait = delay, action = action });
    static void OnLog(string message, string stack, LogType type) { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message); }

#if ENABLE_INPUT_SYSTEM
    static Keyboard keyboard;
    static void Keys(params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
#endif

    // ------------------------------------------------------------ sampling (AnimationTestProbe calls in LateUpdate)

    public static bool sampling;
    static readonly List<float> twistErr = new List<float>(), hipRel = new List<float>(), chestRel = new List<float>(), toeLow = new List<float>(), thetas = new List<float>(), slips = new List<float>(), speeds = new List<float>(), chest = new List<float>(), grips = new List<float>(), yaws = new List<float>();
    static readonly List<string> leads = new List<string>();
    static Vector3 lastL, lastR; static bool lastLDown, lastRDown;
    static Enemy dummy;
    static readonly List<GameObject> props = new List<GameObject>();

    static Animator Rig => P.GetComponentInChildren<Animator>();
    static LimbPlacement Limbs => P.GetComponent<LimbPlacement>();
    static CharAnim Clips => P.GetComponent<CharAnim>();

    public static void Sample()
    {
        // The game camera swings to a locked target each LateUpdate; with the game loop off here, the
        // probe does the same, so "right" means round the target as it does in play.
        if (P != null && P.LockedTarget != null && G.Cam != null)
        {
            Vector3 to = P.LockedTarget.transform.position - P.transform.position;
            G.Cam.transform.rotation = Quaternion.Euler(20f, Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg, 0f);
        }
        if (!sampling || P == null || Rig == null) return;
        float dt = Time.deltaTime; if (dt <= 0f) return;
        Transform lt = Rig.GetBoneTransform(HumanBodyBones.LeftToes), rt = Rig.GetBoneTransform(HumanBodyBones.RightToes);
        float ground = P.transform.position.y;
        bool lDown = lt.position.y - ground < 0.035f, rDown = rt.position.y - ground < 0.035f;
        toeLow.Add(Mathf.Min(lt.position.y, rt.position.y) - ground);
        {
            Vector3 hipsAcross = Rig.GetBoneTransform(HumanBodyBones.RightUpperLeg).position - Rig.GetBoneTransform(HumanBodyBones.LeftUpperLeg).position;
            Vector3 chestAcross = Rig.GetBoneTransform(HumanBodyBones.RightUpperArm).position - Rig.GetBoneTransform(HumanBodyBones.LeftUpperArm).position;
            Vector3 right = P.transform.right;
            hipRel.Add(Vector3.SignedAngle(right, Flat(hipsAcross), Vector3.up));
            chestRel.Add(Vector3.SignedAngle(right, Flat(chestAcross), Vector3.up));
        }
        Vector3 l = Flat(lt.position), r = Flat(rt.position);
        if (lDown && lastLDown) slips.Add((l - lastL).magnitude / dt);
        if (rDown && lastRDown) slips.Add((r - lastR).magnitude / dt);
        lastL = l; lastR = r; lastLDown = lDown; lastRDown = rDown;
        speeds.Add(P.GroundVelocity.magnitude);
        if (P.GroundVelocity.sqrMagnitude > 0.04f) thetas.Add(Vector3.SignedAngle(P.transform.forward, P.GroundVelocity, Vector3.up));
        yaws.Add(P.HipYaw);
        leads.Add(Clips != null ? Clips.GaitLead : "");
        if (dummy != null)
        {
            // The chest's facing: square to the shoulders.
            Vector3 across = Rig.GetBoneTransform(HumanBodyBones.RightUpperArm).position - Rig.GetBoneTransform(HumanBodyBones.LeftUpperArm).position;
            Vector3 facing = Vector3.Cross(Flat(across), Vector3.up);
            // The model faces the other way from its bones' handedness on this rig; take the nearer.
            Vector3 toTarget = Flat(dummy.transform.position - P.transform.position);
            float a = Vector3.Angle(facing, toTarget), b = Vector3.Angle(-facing, toTarget);
            chest.Add(Mathf.Min(a, b));
        }
        if (Limbs != null && Limbs.GripError >= 0f) grips.Add(Limbs.GripError);
        if (Limbs != null && Mathf.Abs(P.HipYaw) > 5f) twistErr.Add(Mathf.Abs(Limbs.ChestYawError));
    }

    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
    static float Median(List<float> v) { if (v.Count == 0) return 0f; var s = v.OrderBy(x => x).ToList(); return s[s.Count / 2]; }
    static void Begin() { twistErr.Clear(); hipRel.Clear(); chestRel.Clear(); toeLow.Clear(); thetas.Clear(); slips.Clear(); speeds.Clear(); chest.Clear(); grips.Clear(); yaws.Clear(); leads.Clear(); lastLDown = lastRDown = false; sampling = true; }

    struct Result { public float speed, slip, ratio, chest, yaw, hips, torso, lowest, twist; public string lead; }
    static Result End(string scenario, string mode)
    {
        sampling = false;
        var r = new Result { speed = Median(speeds), slip = Median(slips), chest = Median(chest), yaw = Median(yaws.Select(Mathf.Abs).ToList()) };
        r.ratio = r.speed > 0.2f ? r.slip / r.speed : 0f;
        r.twist = twistErr.Count > 0 ? twistErr.Max() : 0f;
        r.hips = Median(hipRel); r.torso = Median(chestRel); r.lowest = toeLow.Count > 0 ? toeLow.Min() : 1f;
        r.lead = leads.GroupBy(x => x).OrderByDescending(g => g.Count()).Select(g => g.Key).FirstOrDefault() ?? "";
        csv.AppendLine(scenario + "," + mode + "," + F(r.speed) + "," + F(r.slip) + "," + F(r.ratio) + ",lead " + r.lead + " chest " + F(r.chest) + " hipYaw " + F(r.yaw) + " samples " + slips.Count);
        Debug.Log("ANIM MEASURE " + scenario + " " + mode + " speed " + F(r.speed) + " slip " + F(r.slip) + " ratio " + F(r.ratio) + " lead " + r.lead + " chest " + F(r.chest) + " hipYaw " + F(r.yaw) + " samples " + slips.Count + " travel " + F(Median(thetas)) + " deg, camera yaw " + F(G.Cam.transform.eulerAngles.y) + ", facing " + F(P.transform.eulerAngles.y)
            + " | hips vs body " + F(Median(hipRel)) + " chest vs body " + F(Median(chestRel)) + " lowest toe " + F(toeLow.Count > 0 ? toeLow.Min() : -1f) + " median " + F(Median(toeLow)) + " twist error max " + F(twistErr.Count > 0 ? twistErr.Max() : 0f));
        return r;
    }

    // ------------------------------------------------------------ staging

    static GameObject ground;
    static void Stage()
    {
        foreach (var e in Enemy.All.ToArray()) UnityEngine.Object.DestroyImmediate(e.gameObject);
        foreach (var p in props) if (p != null) UnityEngine.Object.DestroyImmediate(p);
        props.Clear();
        ((IList)Get(G, "timers")).Clear();
        Call(G, "SetState", Game.GameState.Playing);
        Set(G, "InTown", false); Set(G, "InWilds", false); Set(G, "DefeatPending", false); Set(G, "fade", 0f); Set(G, "fadeDir", 0f);
        Set(G, "hubPanel", 0); Set(G, "showMap", false); Set(G, "awakening", false); Set(G, "hintStep", -1);
        P.stats = new PlayerStats();
        P.maxHp = P.hp = 1000f;
        P.Revive(Origin, true);
        P.transform.rotation = Quaternion.identity;
        Set(P, "invuln", 0f); Set(P, "LockedTarget", null);
        if (ground == null)
        {
            ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Animation test ground";
            ground.transform.position = Origin - Vector3.up * 0.5f;
            ground.transform.localScale = new Vector3(80f, 1f, 80f);
            Paint(ground, new Color(0.3f, 0.36f, 0.28f));
        }
        G.Cam.Face(0f); G.Cam.Snap();
        Physics.SyncTransforms();
        dummy = null;
    }

    static Enemy Dummy(Vector3 offset)
    {
        var e = Enemy.Create(Enemy.Kind.Minion, Origin + offset, 0);
        e.hp = e.maxHp = 100000f;
        Set(e, "stun", 0f); Set(e, "cooldown", 100000f); Set(e, "speed", 0f);
        Physics.SyncTransforms();
        dummy = e;
        return e;
    }

    // Test geometry is painted, so the captures show where the ground is.
    static void Paint(GameObject go, Color c)
    {
        var shader = Shader.Find("Standard");
        if (shader != null) go.GetComponent<Renderer>().sharedMaterial = new Material(shader) { color = c };
    }

    static GameObject Block(string name, Vector3 centre, Vector3 size, Quaternion rotation)
    {
        var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
        b.name = name; b.transform.position = centre; b.transform.localScale = size; b.transform.rotation = rotation;
        Paint(b, new Color(0.42f, 0.36f, 0.3f));
        props.Add(b); Physics.SyncTransforms();
        return b;
    }

    static void Shot(string name, Vector3 offset, Vector3 look, float fov = 40f)
    {
        Directory.CreateDirectory("Logs/anim");
        var go = new GameObject("Animation evidence camera");
        var cam = go.AddComponent<Camera>();
        cam.transform.position = P.transform.position + offset;
        cam.transform.LookAt(P.transform.position + look);
        cam.fieldOfView = fov;
        var rt = new RenderTexture(640, 640, 24);
        var tex = new Texture2D(640, 640, TextureFormat.RGB24, false);
        var previous = RenderTexture.active;
        try
        {
            cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, 640, 640), 0, 0); tex.Apply();
            File.WriteAllBytes("Logs/anim/" + name + ".png", tex.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous; cam.targetTexture = null; rt.Release();
            UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(tex); UnityEngine.Object.DestroyImmediate(go);
        }
    }

    // ------------------------------------------------------------ the script

    static void Ledger()
    {
        var text = Resources.Load<TextAsset>("Humans/gait_ledger");
        Check(text != null, "the gait ledger ships in Resources");
        var json = text.text;
        foreach (var clip in new[] { CharAnim.WalkBack, CharAnim.JogBack, CharAnim.SideRight, CharAnim.SideLeft, CharAnim.TurnStep, CharAnim.JogStop })
            Check(json.Contains("\"" + clip + "\"") && Clips.Has(clip), "ledger records and the hero can play " + clip);
        Check(json.Contains("generative") || json.Contains("no generative model"), "the ledger states how the set was made");
    }

    static void Locomotion(string scenario, Key[] keys, bool lockOn, float seconds, Action<Result, Result> judge)
    {
        Result before = default, after = default;
        foreach (bool on in new[] { false, true })
        {
            bool mode = on;
            Do(0.2f, () =>
            {
                CharAnim.Directional = mode;
                Stage(); P.enabled = true;
                if (lockOn) Dummy(new Vector3(0f, 0f, 6f));
                Keys();
            });
            if (lockOn) { Do(0.15f, () => Keys(Key.T)); Do(0.1f, () => Keys()); Do(0.1f, () => Check(P.LockedTarget != null, scenario + ": locked on")); }
            Do(0.1f, () => Keys(keys));
            Do(0.7f, () => Begin());
            Do(seconds * 0.5f, () => { if (mode) Shot(scenario + "-after", new Vector3(-3.2f, 1.6f, 3.2f), Vector3.up * 0.8f); else Shot(scenario + "-before", new Vector3(-3.2f, 1.6f, 3.2f), Vector3.up * 0.8f); });
            Do(seconds * 0.5f, () => { var r = End(scenario, mode ? "directional" : "forward-only"); if (mode) after = r; else before = r; Keys(); });
        }
        Do(0.05f, () => judge(before, after));
    }

    static void Script()
    {
#if ENABLE_INPUT_SYSTEM
        keyboard = InputSystem.AddDevice<Keyboard>();
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
        Application.runInBackground = true; QualitySettings.vSyncCount = 0; Application.targetFrameRate = 60;
        G.enabled = false;
        G.gameObject.AddComponent<AnimationTestProbe>();
        Stage();
        Ledger();
        Bindings.ResetDefaults();

        // ---- A01: real movement, old and new
        Locomotion("run", new[] { Key.W }, false, 1.0f, (b, a) =>
            Check(a.ratio < 0.2f && a.lead == "Jog_Fwd_Loop", "a forward run keeps its feet planted (slip " + F(a.slip) + " m/s at " + F(a.speed) + " m/s)"));
        Locomotion("strafe-right", new[] { Key.D }, true, 1.4f, (b, a) =>
        {
            Check(a.ratio < b.ratio * 0.6f, "side-stepping round a locked target slips less than the forward-only gait (" + F(a.ratio) + " vs " + F(b.ratio) + " of speed)");
            Check(Mathf.Abs(a.hips) > 40f && a.twist < 3f, "at speed the hips turn into the travel (" + F(a.hips) + " deg) while the chest turns back to the target (worst shoulder-line error " + F(a.twist) + " deg)");
            Check(a.lowest < 0.03f, "the side-stepping feet reach the ground (lowest toe " + F(a.lowest) + " m)");
        });
        Locomotion("strafe-left", new[] { Key.A }, true, 1.4f, (b, a) =>
            Check(a.ratio < b.ratio * 0.6f && a.twist < 3f && a.lowest < 0.03f, "side-stepping left: slip " + F(a.ratio) + " vs " + F(b.ratio) + " of speed, shoulder-line error " + F(a.twist) + " deg, lowest toe " + F(a.lowest) + " m"));
        Locomotion("backpedal", new[] { Key.S }, true, 1.4f, (b, a) =>
            Check(a.lead == CharAnim.WalkBack || a.lead == CharAnim.JogBack, "backing off plays the authored backstep (" + a.lead + ", slip " + F(a.ratio) + " vs " + F(b.ratio) + " of speed)"));
        Locomotion("guard-shuffle", new[] { Key.D, Key.C }, true, 1.4f, (b, a) =>
            Check(a.speed < 0.3f || a.ratio <= b.ratio + 0.05f, "shuffling sideways behind the guard is no worse than before (" + F(a.ratio) + " vs " + F(b.ratio) + ")"));

        // ---- A01: turning on the spot, and the stop
        Do(0.2f, () =>
        {
            CharAnim.Directional = true; Stage(); P.enabled = false;
            Clips.Release();
            for (int i = 0; i < 10; i++) Clips.Move(0f, Vector2.up, 0.05f, 240f);
        });
        Do(0.3f, () =>
        {
            Clips.Move(0f, Vector2.up, 0.05f, 240f);
            var slots = (IDictionary)typeof(CharAnim).GetField("slots", Any).GetValue(Clips);
            float target = slots.Contains(CharAnim.TurnStep) ? (float)slots[CharAnim.TurnStep].GetType().GetField("target").GetValue(slots[CharAnim.TurnStep]) : 0f;
            Check(target > 0.6f, "turning on the spot at 240 deg/s steps round (turn step weight " + F(target) + ")");
            Clips.Move(0f, Vector2.up, 0.05f, 0f);
            target = (float)slots[CharAnim.TurnStep].GetType().GetField("target").GetValue(slots[CharAnim.TurnStep]);
            Check(target < 0.01f, "standing still the turn step lets go");
        });
        string stopSeen = "";
        Do(0.2f, () => { Stage(); P.enabled = true; Keys(Key.W); });
        Do(1.0f, () => { Keys(); Begin(); });
        for (int i = 0; i < 10; i++) Do(0.03f, () => { var current = (string)typeof(CharAnim).GetField("current", Any).GetValue(Clips); if (current == CharAnim.JogStop) stopSeen = current; });
        Do(0.35f, () =>
        {
            var r = End("stop", "directional");
            Check(stopSeen == CharAnim.JogStop, "letting go of a run plays the authored stop");
            Check(r.slip < 0.6f, "through the stop the planted foot holds (median slip " + F(r.slip) + " m/s)");
        });
        Do(0.2f, () => { Keys(Key.W); });
        Do(0.25f, () => { Check(Clips.GaitLead == "Jog_Fwd_Loop" || Clips.GaitLead == "Walk_Loop", "moving again cuts the stop short (" + Clips.GaitLead + ")"); Keys(); });

        // ---- A02: feet on a slope, on a step, and nothing changed on flat ground
        Do(0.3f, () => { Stage(); P.enabled = true; Keys(); });
        Do(0.8f, () =>
        {
            Check(Mathf.Abs(Limbs.Pelvis) < 0.02f && Limbs.SoleErrorLeft < 0.015f && Limbs.SoleErrorRight < 0.015f, "on flat ground the standing soles sit on the ground (" + F(Limbs.SoleErrorLeft) + ", " + F(Limbs.SoleErrorRight) + ") with the pelvis all but still (" + F(Limbs.Pelvis) + ")");
        });
        foreach (var (name, build, place) in new (string, Action, Vector3)[]
        {
            ("slope", () => Block("Test slope", Origin + new Vector3(0f, 0.2f, 8f), new Vector3(6f, 1f, 6f), Quaternion.Euler(0f, 0f, 18f)), new Vector3(0f, 1.3f, 8f)),
            ("step", () => Block("Test step", Origin + new Vector3(1.02f - 0.04f, 0.09f, 8f), new Vector3(2f, 0.18f, 3f), Quaternion.identity), new Vector3(0f, 0.3f, 8f)),
        })
        {
            float offL = 0f, offR = 0f, onL = 0f, onR = 0f;
            Do(0.2f, () => { Stage(); build(); P.enabled = true; P.Revive(Origin + place, true); P.transform.rotation = Quaternion.identity; Limbs.FeetOn = false; });
            Do(0.8f, () => { offL = Limbs.SoleErrorLeft; offR = Limbs.SoleErrorRight; Shot(name + "-before", new Vector3(0f, 0.5f, 2.2f), Vector3.up * 0.3f, 50f); Limbs.FeetOn = true; });
            Do(0.8f, () =>
            {
                onL = Limbs.SoleErrorLeft; onR = Limbs.SoleErrorRight;
                Shot(name + "-after", new Vector3(0f, 0.5f, 2.2f), Vector3.up * 0.3f, 50f);
                csv.AppendLine(name + ",feet off,0," + F(Mathf.Max(offL, offR)) + ",0,left " + F(offL) + " right " + F(offR));
                csv.AppendLine(name + ",feet on,0," + F(Mathf.Max(onL, onR)) + ",0,left " + F(onL) + " right " + F(onR) + " pelvis " + F(Limbs.Pelvis));
                Debug.Log("ANIM MEASURE " + name + " sole error off " + F(offL) + "/" + F(offR) + " on " + F(onL) + "/" + F(onR) + " pelvis " + F(Limbs.Pelvis)
                    + " signed " + F(Limbs.SoleAboveLeft) + "/" + F(Limbs.SoleAboveRight) + " raise " + F(Limbs.RaiseLeft) + "/" + F(Limbs.RaiseRight) + " planted " + F(Limbs.PlantedLeft) + "/" + F(Limbs.PlantedRight) + " root " + F(P.transform.position.y - Origin.y));
                Check(Mathf.Max(onL, onR) <= 0.03f, name + ": with foot placement both soles meet the ground within 3 cm (" + F(onL) + ", " + F(onR) + ")");
                Check(Mathf.Max(offL, offR) > Mathf.Max(onL, onR) + 0.02f, name + ": without it they did not (" + F(offL) + ", " + F(offR) + ")");
            });
        }

        // ---- A02: the great axe in two hands, when it is in use
        float gripOff = 0f;
        Do(0.2f, () =>
        {
            Stage(); P.SetHero(Array.FindIndex(Heroes.All, h => h.id == "bram")); P.enabled = true; Stage();
            Check(P.Weapon.guard == GuardStyle.Haft, "Bram carries the great axe");
            Limbs.GripAllowed = false;
            Keys(Key.C);
        });
        Do(0.6f, () =>
        {
            Check(Get(P, "move").ToString() == "Block" && Limbs.GripGap >= 0f, "raising the haft guard takes the axe in two hands");
            gripOff = Limbs.GripGap;
            Shot("grip-before", new Vector3(1.4f, 1.3f, 1.6f), Vector3.up * 1.0f, 40f);
            Limbs.GripAllowed = true;
        });
        Do(0.3f, () => Begin());
        Do(1.0f, () =>
        {
            var r = End("axe-guard", "hold");
            float within = grips.Count == 0 ? 0f : grips.Count(x => x <= 0.03f) / (float)grips.Count;
            csv.AppendLine("axe-guard,hold,0," + F(Median(grips)) + ",0,within 3 cm " + F(within) + " samples " + grips.Count + " off hand from its grip without placement " + F(gripOff));
            Shot("grip-after", new Vector3(1.4f, 1.3f, 1.6f), Vector3.up * 1.0f, 40f);
            Check(gripOff > 0.05f, "without placement the off hand is off the haft in the guard (" + F(gripOff) + " m)");
            Check(grips.Count > 20 && within >= 0.95f, "in the haft guard the left hand stays on the haft (" + F(within * 100f) + "% of frames within 3 cm)");
            Check(Limbs.GuardWeight > 0.99f && Limbs.GuardHaftError < 8f, "the haft is held up across the body (" + F(Limbs.GuardHaftError) + " deg from the guard line)");
            Keys();
        });
        // Squared up to a threat the axe stays across the body, at a stand and on the move.
        Do(0.2f, () => { var e = Dummy(new Vector3(0f, 0f, 7f)); Set(e, "alerted", true); });
        Do(0.5f, () => { Check(G.Threatened, "an alerted foe nearby squares Bram up"); Keys(Key.W); Begin(); });
        Do(1.0f, () =>
        {
            var r = End("axe-ready", "advance");
            float within = grips.Count == 0 ? 0f : grips.Count(x => x <= 0.03f) / (float)grips.Count;
            csv.AppendLine("axe-ready,advance," + F(r.speed) + "," + F(Median(grips)) + ",0,within 3 cm " + F(within) + " samples " + grips.Count);
            Check(grips.Count > 20 && within >= 0.95f, "squared up and advancing, the axe stays in both hands (" + F(within * 100f) + "% within 3 cm)");
            Keys();
            Shot("axe-ready", new Vector3(1.6f, 1.4f, 2.0f), Vector3.up * 1.0f, 40f);
        });
        // Swinging, the clips are one-handed: the grip lets go instead of dragging the arm.
        Do(0.3f, () => { Begin(); Call(P, "StartAttack", Vector3.forward); });
        Do(0.25f, () =>
        {
            Check(Limbs.GripGap < 0f, "mid-swing the off hand is the clip's own, not dragged after the haft");
            End("axe-swing", "combo");
        });
        Do(0.4f, () => { P.SetHero(0); P.enabled = false; });

        // ---- the set, as the hero plays it
        foreach (var clip in new[] { "Walk_Loop", CharAnim.WalkBack, CharAnim.SideRight, CharAnim.SideLeft, CharAnim.TurnStep, "Jog_Fwd_Loop", CharAnim.JogBack })
        {
            string c = clip;
            Do(0.15f, () => { Stage(); P.enabled = false; Clips.Release(); Clips.Loop(c, 1f, 0f); });
            Do(0.35f, () => Shot("sheet-" + c + "-a", new Vector3(2.6f, 1.2f, 2.6f), Vector3.up * 0.8f, 40f));
            Do(0.3f, () => Shot("sheet-" + c + "-b", new Vector3(2.6f, 1.2f, 2.6f), Vector3.up * 0.8f, 40f));
        }

        Do(0.2f, () =>
        {
            CharAnim.Directional = true;
            File.WriteAllText("Logs/anim/results.csv", csv.ToString());
            Check(errors.Count == 0, "animation scenarios logged no unexpected errors" + (errors.Count > 0 ? ": " + errors[0] : ""));
            Check(JourneyStore.Error == null, "the journey store is healthy afterwards");
        });
    }

    // ------------------------------------------------------------ runner

    static AnimationTest() { if (SessionState.GetBool(Session, false)) { EditorApplication.update += Tick; Application.logMessageReceived += OnLog; } }

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
            if (next == 0) { next = EditorApplication.timeSinceStartup + 2; return; }
            if (EditorApplication.timeSinceStartup < next) return;
            if (steps == null) { steps = new List<Step>(); Script(); deadline = EditorApplication.timeSinceStartup + 240; next = EditorApplication.timeSinceStartup + steps[0].wait; return; }
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("animation scenario deadline exceeded");
            if (at < steps.Count) { steps[at++].action(); if (at < steps.Count) { next = EditorApplication.timeSinceStartup + steps[at].wait; return; } }
            Debug.Log("ANIM DONE " + passed + " checks passed");
            Finish(0);
        }
        catch (Exception e)
        {
            Directory.CreateDirectory("Logs/anim");
            File.WriteAllText("Logs/anim/results.csv", csv.ToString());
            Debug.LogError("ANIM FAILED step=" + at + " " + e);
            Finish(1);
        }
    }

    static void Finish(int code)
    {
        CharAnim.Directional = true;
        SessionState.SetBool(Session, false);
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= OnLog;
        EditorApplication.Exit(code);
    }
}

[DefaultExecutionOrder(300)]
public class AnimationTestProbe : MonoBehaviour
{
    void LateUpdate() => AnimationTest.Sample();
}
