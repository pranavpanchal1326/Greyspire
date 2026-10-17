using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
#endif

// Exercises real input, controller collisions, jumps, recovery buffering and lock-on.
// Run through Tools/feeltest.ps1 so the editor's player save is restored afterwards.
[InitializeOnLoad]
public static class FeelTest
{
    const string SessionKey = "GreyspireFeelTest";
    const BindingFlags Any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static readonly Vector3 Origin = new Vector3(1000f, 0.05f, 1000f);
    class Step { public float wait; public Action act; }
    static List<Step> steps;
    static int at, passed, dashCount;
    static float since, fullJumpHeight;
    static double deadline;
    static bool watchLanding, bufferedDescent, bufferedRise;
    static int landingObservedFrame = -1;
    static GameObject ground, obstacle;
    static Enemy enemyA, enemyB;
#if ENABLE_INPUT_SYSTEM
    static Keyboard keyboard;
#endif
    static PlayerController P => Game.I.Player;
    static object Call(object host, string method, params object[] args) => host.GetType().GetMethod(method, Any).Invoke(host, args);
    static void Set(object host, string field, object value) => host.GetType().GetField(field, Any).SetValue(host, value);
    static float Number(string field) => (float)typeof(PlayerController).GetField(field, Any).GetValue(P);

    static FeelTest()
    {
        if (SessionState.GetBool(SessionKey, false)) EditorApplication.update += Tick;
    }

    public static void Run()
    {
        EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        SessionState.SetBool(SessionKey, true);
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        EditorApplication.EnterPlaymode();
    }

    static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        passed++;
        Debug.Log("FEEL PASS " + message);
    }

    static void Do(float wait, Action act) => steps.Add(new Step { wait = wait, act = act });
    static void Press(params UnityEngine.InputSystem.Key[] keys)
    {
#if ENABLE_INPUT_SYSTEM
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
#endif
    }

    static GameObject Box(string name, Vector3 position, Vector3 size)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.position = position;
        go.transform.localScale = size;
        Physics.SyncTransforms();
        return go;
    }

    static void Neutral()
    {
        Press();
        P.Revive(Origin, true);
        P.transform.rotation = Quaternion.identity;
        Game.I.Cam.Face(0f);
        Game.I.Cam.Snap();
        Physics.SyncTransforms();
    }

    static void Buffer(string action)
    {
        Type actionType = typeof(PlayerController).GetNestedType("BufferedAction", Any);
        Call(P, "Buffer", Enum.Parse(actionType, action), Vector3.forward);
    }

    static void Script()
    {
#if ENABLE_INPUT_SYSTEM
        keyboard = InputSystem.AddDevice<Keyboard>();
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#else
        throw new Exception("This test needs the project's Input System backend.");
#endif
        Application.runInBackground = true;
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 60;
        Game.I.enabled = false;
        Call(Game.I, "SetState", Game.GameState.Playing);
        ground = Box("Feel test ground", Origin + new Vector3(0f, -0.55f, 0f), new Vector3(80f, 1f, 80f));
        P.stats = new PlayerStats();
        Neutral();

        Do(1f, () => { Check(P.Grounded, "controller settles onto ground (y=" + P.transform.position.y + ", vy=" + Number("vy") + ", roaming=" + Game.I.Roaming + ", frame=" + Time.frameCount + ")"); Press(UnityEngine.InputSystem.Key.W); });
        Do(0.45f, () => { Check(P.GroundVelocity.magnitude > 5.7f, "movement accelerates to combat pace (speed=" + P.GroundVelocity.magnitude + ", input=" + GameInput.Move + ", planar=" + typeof(PlayerController).GetField("planar", Any).GetValue(P) + ")"); Press(); });
        Do(0.25f, () => { Check(P.GroundVelocity.magnitude < 0.15f, "braking stops promptly without drift"); Neutral();
            obstacle = Box("Movement wall", Origin + new Vector3(0f, 1.5f, 2f), new Vector3(8f, 4f, 0.4f)); Press(UnityEngine.InputSystem.Key.W); });
        Do(0.75f, () => { Check(P.GroundVelocity.magnitude < 0.2f && P.Speed01 < 0.05f, "wall collision reports stopped motion to animation");
            Check(P.transform.position.z < Origin.z + 1.7f, "controller cannot walk through a wall");
            UnityEngine.Object.DestroyImmediate(obstacle); Neutral(); });

        Do(0.2f, () => Press(UnityEngine.InputSystem.Key.Space));
        Do(0.27f, () => { fullJumpHeight = P.transform.position.y - Origin.y; Check(fullJumpHeight > 1f, "held jump rises"); Press(); });
        Do(1.2f, () => { Neutral(); });
        Do(0.2f, () => Press(UnityEngine.InputSystem.Key.Space));
        Do(0.04f, () => Press());
        Do(0.23f, () => { Check(P.transform.position.y - Origin.y < fullJumpHeight - 0.25f, "released jump is lower than held jump"); Neutral(); });

        Do(0.2f, () => {
            obstacle = Box("Low ceiling", Origin + new Vector3(0f, 2.1f, 0f), new Vector3(8f, 0.25f, 8f));
            Press(UnityEngine.InputSystem.Key.Space); });
        Do(0.15f, () => { Check(Number("vy") <= 0f && P.transform.position.y < Origin.y + 0.35f, "ceiling contact cancels ascent");
            UnityEngine.Object.DestroyImmediate(obstacle); Neutral();
        });
        // Process the ceiling jump release before queuing the next press.
        Do(0.1f, () => {
            Set(Game.I, "<InTown>k__BackingField", true); P.Revive(Origin + Vector3.up * 0.5f, true); Set(P, "vy", -8f);
            // A teleport preserves CharacterController collision flags until Move.
            P.GetComponent<CharacterController>().Move(Vector3.zero);
            Check(!P.Grounded, "buffered-landing fixture begins airborne with fresh collision flags");
            bufferedDescent = bufferedRise = false; watchLanding = true;
            Press(UnityEngine.InputSystem.Key.Space); });
        Do(0.03f, () => Press());
        Do(0.16f, () => { Check(bufferedDescent && bufferedRise, "jump pressed just before landing executes on contact (buffered descent="+bufferedDescent+", observed rise="+bufferedRise+", y="+P.transform.position.y+", vy="+Number("vy")+")"); watchLanding = false;
            Set(Game.I, "<InTown>k__BackingField", false); Neutral(); });

        Do(0.2f, () => { Call(P, "StartAttack", Vector3.forward); dashCount = P.dashes; Press(UnityEngine.InputSystem.Key.LeftShift); });
        Do(0.26f, () => { Check(P.dashes == dashCount + 1, "early dodge input survives light recovery and executes once"); Press(); });
        Do(0.4f, () => { Neutral(); Call(P, "StartHeavy", 0f); Buffer("Throw"); });
        Do(0.75f, () => { Check(Number("throwCd") <= 0f, "expired throw input cannot fire after heavy recovery"); Neutral(); });

        Do(0.2f, () => { obstacle = Box("Camera wall", Origin + new Vector3(0f, 1.7f, -1.2f), new Vector3(8f, 5f, 0.4f)); Game.I.Cam.Snap(); });
        Do(0.3f, () => {
            Camera cam = Game.I.Cam.GetComponent<Camera>();
            Check(Vector3.Distance(cam.transform.position, P.transform.position + Vector3.up * 1.5f) < 1.4f, "camera pulls inside a nearby obstruction");
            Check(!obstacle.GetComponent<Collider>().bounds.Contains(cam.transform.position), "camera stays outside wall geometry");
            UnityEngine.Object.DestroyImmediate(obstacle); Neutral(); });
        Do(0.6f, () => {
            Check(Vector3.Distance(Game.I.Cam.transform.position, P.transform.position + Vector3.up * 1.5f) > 3f, "camera eases back out after obstruction clears");
            enemyA = Enemy.Create(Enemy.Kind.Slime, Origin + new Vector3(0f, 0f, 7f), 0);
            enemyB = Enemy.Create(Enemy.Kind.Slime, Origin + new Vector3(3f, 0f, 7f), 0);
            // Targeting candidates must survive the throw. Their death is tested
            // explicitly below, independently of the current damage curve.
            enemyA.hp = enemyA.maxHp = enemyB.hp = enemyB.maxHp = 1000f;
            enemyA.enabled = enemyB.enabled = false;
            Enemy.All.Add(enemyA); Enemy.All.Add(enemyB);
            Physics.SyncTransforms(); });
        Do(0.2f, () => Press(UnityEngine.InputSystem.Key.T));
        Do(0.15f, () => { Check(P.LockedTarget == enemyA, "lock selects the visible central threat"); Press(UnityEngine.InputSystem.Key.S); });
        Do(0.4f, () => { Check(P.GroundVelocity.magnitude > 1f && P.GroundVelocity.magnitude <= 2.05f, "locked backstep matches the restrained reverse gait");
            Check(Vector3.Dot(P.transform.forward, (enemyA.transform.position - P.transform.position).normalized) > 0.9f, "backstep preserves threat-facing orientation");
            Press(); Call(P, "StartThrow");
            Check(typeof(PlayerController).GetField("aimed", Any).GetValue(P) == enemyA, "throw respects the locked target"); });
        Do(0.5f, () => { Call(P, "End"); Set(P, "throwCd", 0f); });
        Do(0.1f, () => Press(UnityEngine.InputSystem.Key.V));
        Do(0.15f, () => { Check(P.LockedTarget == enemyB, "target switch selects the threat on the right (lock=" + (P.LockedTarget != null ? P.LockedTarget.name : "none") + ", A=" + enemyA.hp + ", B=" + enemyB.hp + ")"); Press(); enemyB.hp = 0f; });
        Do(0.2f, () => { Check(P.LockedTarget == enemyA, "lock transfers when a target dies");
            obstacle = Box("Sight wall", Origin + new Vector3(0f, 1.5f, 3.5f), new Vector3(10f, 4f, 0.5f)); });
        Do(0.8f, () => { Check(P.LockedTarget == null, "lock releases after sustained occlusion");
            Enemy aim = (Enemy)Call(P, "CombatTarget", 10f, Vector3.forward);
            Check(aim == null, "aim assist cannot select through a wall");
            UnityEngine.Object.DestroyImmediate(obstacle); Neutral(); });
        Do(0.2f, () => Press(UnityEngine.InputSystem.Key.T));
        Do(0.15f, () => { Check(P.LockedTarget != null, "lock reacquires after sight is restored"); Press();
            Call(P, "StartAttack", Vector3.forward); Buffer("Light"); Call(Game.I, "SetState", Game.GameState.Paused); });
        Do(0.15f, () => { Check(typeof(PlayerController).GetField("bufferedAction", Any).GetValue(P).ToString() == "None", "pause clears pending combat input");
            P.Revive(Origin, true); Check(P.LockedTarget == null && P.GroundVelocity == Vector3.zero, "revive clears target and movement state");
            Call(Game.I, "SetState", Game.GameState.Playing);
            CharAnim clips = P.GetComponent<CharAnim>();
            Check(clips != null && clips.Has("Walk_Loop") && clips.Has("Sword_Regular_A"), "hero has locomotion and combat clips");
            P.enabled = false;
            clips.Release(); clips.Move(1.3f, 0f, true); });
        Do(0.2f, () => {
            CharAnim clips = P.GetComponent<CharAnim>();
            var slots = (System.Collections.IDictionary)typeof(CharAnim).GetField("slots", Any).GetValue(clips);
            // A01: backing away plays the authored backward walk forward, not the walk in reverse.
            object back = slots[CharAnim.WalkBack];
            var playable = (AnimationClipPlayable)back.GetType().GetField("playable").GetValue(back);
            float length = (float)back.GetType().GetField("length").GetValue(back);
            float walkTarget = slots.Contains("Walk_Loop") ? (float)slots["Walk_Loop"].GetType().GetField("target").GetValue(slots["Walk_Loop"]) : 0f;
            Check(playable.GetSpeed() > 0f && playable.GetTime() >= 0 && playable.GetTime() <= length && walkTarget < 0.01f, "backward gait plays the authored backstep at a valid phase");
            P.enabled = true;
            Enemy.All.Remove(enemyA); Enemy.All.Remove(enemyB);
            UnityEngine.Object.DestroyImmediate(enemyA.gameObject); UnityEngine.Object.DestroyImmediate(enemyB.gameObject);
            UnityEngine.Object.DestroyImmediate(ground);
            Game.I.enabled = true;
            Call(Game.I, "EnterHub"); });
        Do(4f, () => typeof(SmokeTest).GetMethod("Settle", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null));
        Do(2f, () => {
            Check(Game.I.InTown && P.LockedTarget == null, "town transition retains peaceful movement");
            Capture("Logs/feel/town-camera.png");
            Call(Game.I, "SetState", Game.GameState.Paused);
            Set(Game.I, "pauseTab", 2);
            ScreenCapture.CaptureScreenshot("Logs/feel/settings.png"); });
        Do(0.5f, () => {});
    }

    static void Capture(string file)
    {
        Camera camera = Game.I.Cam.GetComponent<Camera>();
        RenderTexture oldTarget = camera.targetTexture, oldActive = RenderTexture.active;
        var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        var render = new RenderTexture(1280, 720, 24);
        try
        {
            camera.targetTexture = render; camera.Render(); RenderTexture.active = render;
            texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); texture.Apply();
            File.WriteAllBytes(file, texture.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = oldTarget; RenderTexture.active = oldActive;
            render.Release(); UnityEngine.Object.DestroyImmediate(render); UnityEngine.Object.DestroyImmediate(texture);
        }
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying || Game.I == null) return;
        try
        {
            if (steps == null)
            {
                Directory.CreateDirectory("Logs/feel");
                steps = new List<Step>(); Script(); since = Time.realtimeSinceStartup;
                deadline = EditorApplication.timeSinceStartup + 110;
            }
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("test exceeded its runtime deadline");
            if (watchLanding && landingObservedFrame != Time.frameCount)
            {
                landingObservedFrame = Time.frameCount;
                bufferedDescent |= Number("jumpBuffer") > 0f && Number("vy") < 0f;
                bufferedRise |= bufferedDescent && Number("vy") > 0f && P.transform.position.y > Origin.y + 0.1f;
            }
            if (Time.realtimeSinceStartup - since < steps[at].wait) return;
            steps[at++].act(); since = Time.realtimeSinceStartup;
            if (at < steps.Count) return;
            Debug.Log("FEEL DONE checks=" + passed);
            SessionState.SetBool(SessionKey, false); EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            Debug.LogError("FEEL FAILED step=" + at + " " + error);
            SessionState.SetBool(SessionKey, false); EditorApplication.Exit(1);
        }
    }
}
