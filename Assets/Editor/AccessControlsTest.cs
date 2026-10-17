using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
#endif

// Phase nine, packages U01-U03. U02: the binding table, its persistence and recovery, capture
// through the real settings page with real keyboard and controller events, swaps, reserved keys,
// a controller pulled mid-capture. U03: hold and toggle guard and sprint through the hero's own
// combat input, timing assist on a real parry, the onboarding tip. U01: the fight-focus HUD and
// the journal, read from saved records. Captures land in Logs/access/.
[InitializeOnLoad]
public static class AccessControlsTest
{
    const string Session = "GreyspireAccessControlsTest";
    const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static readonly Vector3 Origin = new Vector3(1000f, 0.1f, 1000f);
    class Step { public float wait; public Action action; }
    static List<Step> steps;
    static int at, passed;
    static double next, deadline;
    static readonly List<string> errors = new List<string>();
    static Game G => Game.I;
    static PlayerController P => G.Player;
    static object Get(object host, string name) => host.GetType().GetField(name, Any).GetValue(host);
    static void Set(object host, string name, object value) => (host.GetType().GetField(name, Any) ?? host.GetType().GetField("<" + name + ">k__BackingField", Any)).SetValue(host, value);
    static object Call(object host, string name, params object[] args) => host.GetType().GetMethod(name, Any).Invoke(host, args);
    static void UsePad(bool on) => typeof(GameInput).GetField("<UsingGamepad>k__BackingField", Any).SetValue(null, on);
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); passed++; Debug.Log("ACCESS PASS " + message); }
    static void Do(float delay, Action action) => steps.Add(new Step { wait = delay, action = action });
    static void OnLog(string message, string stack, LogType type) { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message); }

    // What the overlay saw through GameInput, frame by frame.
    public static int interact, throws, jumps, attacks, confirms;
    public static bool driveHero, steer;
    public static int sprintMode;
    static GameObject ground;

    // The pre-U02 prompt tables: with default bindings every prompt must read exactly as before.
    static readonly string[] OldKeyboard = { "WASD", "Mouse", "Space", "Left click / J", "Right click / Shift", "C / Ctrl", "R / middle click", "G", "Q", "F", "E", "T", "Z / V", "Tab / I", "M", "Esc / P", "Click / Space / Enter", "X", "H" };
    static readonly string[] OldPad = { "Left stick", "Right stick", "A / South", "X / West", "B / East", "LT", "RT", "D-pad up", "RB", "LB", "Y / North", "Right stick click", "D-pad left / right", "D-pad down", "View / Select", "Menu / Start", "A / South", "Y / North", "Left stick click" };

#if ENABLE_INPUT_SYSTEM
    static Gamepad pad;
    static Keyboard keyboard;
    static void Pad(params GamepadButton[] buttons)
    {
        var state = new GamepadState(); foreach (var button in buttons) state = state.WithButton(button);
        InputSystem.QueueStateEvent(pad, state);
    }
    static void Keys(params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
    static void Direction(Vector2 value) => Pad(value.x < 0 ? GamepadButton.DpadLeft : value.x > 0 ? GamepadButton.DpadRight : value.y > 0 ? GamepadButton.DpadUp : GamepadButton.DpadDown);
#endif

    static void Shot(string name) { Directory.CreateDirectory("Logs/access"); ScreenCapture.CaptureScreenshot("Logs/access/" + name + ".png"); }

    static void Stage()
    {
        foreach (var e in Enemy.All.ToArray()) UnityEngine.Object.DestroyImmediate(e.gameObject);
        ((IList)Get(G, "timers")).Clear();
        Call(G, "SetState", Game.GameState.Playing);
        Set(G, "InTown", false); Set(G, "InWilds", false); Set(G, "DefeatPending", false); Set(G, "fade", 0f); Set(G, "fadeDir", 0f);
        Set(G, "hubPanel", 0); Set(G, "showMap", false); Set(G, "awakening", false); Set(G, "hintStep", -1); Set(G, "showSettings", false);
        P.stats = new PlayerStats();
        P.maxHp = P.hp = 1000f;
        P.Revive(Origin, true);
        P.transform.rotation = Quaternion.identity;
        Set(P, "invuln", 0f); Set(P, "guardSpent", false); Set(P, "LockedTarget", null);
        if (ground == null)
        {
            ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Access test ground";
            ground.transform.position = Origin - Vector3.up * 0.6f;
            ground.transform.localScale = new Vector3(60f, 1f, 60f);
        }
        Physics.SyncTransforms();
    }

    static string Move() => Get(P, "move").ToString();

    // ------------------------------------------------------------ U02: the table itself

    static void Table()
    {
#if ENABLE_INPUT_SYSTEM
        Bindings.ResetDefaults(); Bindings.Reload();
        Check(Bindings.IsDefault && Bindings.Actions.Length == 13 && Bindings.Names.Length == 13, "thirteen actions start on their default keys and buttons");
        UsePad(false);
        bool sameKeys = true, samePad = true;
        foreach (ControlAction a in Enum.GetValues(typeof(ControlAction))) sameKeys &= ControlGuide.Key(a) == OldKeyboard[(int)a];
        UsePad(true);
        foreach (ControlAction a in Enum.GetValues(typeof(ControlAction))) samePad &= ControlGuide.Key(a) == OldPad[(int)a];
        Check(sameKeys && samePad, "with default bindings every keyboard and controller prompt reads exactly as before");
        Check(ControlGuide.ShortKey(ControlAction.Dash) == "B" && ControlGuide.ShortKey(ControlAction.Wave) == "LB" && ControlGuide.ShortKey(ControlAction.Companion) == "L3" && ControlGuide.ShortKey(ControlAction.Status) == "D-down",
            "short controller prompts are unchanged");
        UsePad(false);

        foreach (var reserved in new[] { Key.W, Key.Escape, Key.Enter, Key.I, Key.LeftCtrl, Key.Z, Key.Digit1 })
            Check(!Bindings.SetKey(ControlAction.Attack, reserved) && Bindings.KeyFor(ControlAction.Attack) == Key.J, "reserved key " + reserved + " is refused and changes nothing");
        Check(!Bindings.SetPad(ControlAction.Attack, GamepadButton.Start) && !Bindings.SetPad(ControlAction.Attack, GamepadButton.DpadLeft) && Bindings.PadFor(ControlAction.Attack) == GamepadButton.West,
            "Start and the target-switch d-pad sides cannot be taken");

        Check(Bindings.SetKey(ControlAction.Attack, Key.K) && ControlGuide.Key(ControlAction.Attack) == "Left click / K" && ControlGuide.Tutorial(2).StartsWith("Left click / K"),
            "a rebound attack is named by the prompts and the tutorial");
        Check(Bindings.SetKey(ControlAction.Jump, Key.K) && Bindings.KeyFor(ControlAction.Jump) == Key.K && Bindings.KeyFor(ControlAction.Attack) == Key.Space && Bindings.LastMessage.Contains("Attack"),
            "taking a key that is in use swaps it, and the message says what moved");
        Check(Bindings.SetPad(ControlAction.Interact, GamepadButton.West) && Bindings.PadFor(ControlAction.Attack) == GamepadButton.North, "controller buttons swap the same way");
        Bindings.Reload();
        Check(Bindings.KeyFor(ControlAction.Jump) == Key.K && Bindings.KeyFor(ControlAction.Attack) == Key.Space && Bindings.PadFor(ControlAction.Interact) == GamepadButton.West,
            "bindings survive a reload from PlayerPrefs");
        PlayerPrefs.SetInt("bind.kb.Map", (int)Key.K); Bindings.Reload();
        Check(Bindings.KeyFor(ControlAction.Jump) == Key.Space && Bindings.KeyFor(ControlAction.Map) == Key.M && Bindings.PadFor(ControlAction.Interact) == GamepadButton.West,
            "a stored keyboard table with a doubled key falls back to defaults; the valid controller table stays");
        PlayerPrefs.SetInt("bind.pad.Map", 999); PlayerPrefs.SetInt("bind.kb.Lock", (int)Key.W); Bindings.Reload();
        Check(Bindings.IsDefault, "unknown buttons and reserved keys in storage restore the defaults");
        Bindings.SetKey(ControlAction.Interact, Key.K);
        Bindings.ResetDefaults(); Bindings.Reload();
        Check(Bindings.IsDefault && PlayerPrefs.GetInt("bind.kb.Interact") == (int)Key.E, "reset restores and stores every default");
#endif
    }

    static void Script()
    {
#if ENABLE_INPUT_SYSTEM
        pad = InputSystem.AddDevice<Gamepad>(); keyboard = InputSystem.AddDevice<Keyboard>();
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#else
        throw new Exception("Access acceptance requires the installed Input System.");
#endif
        Application.runInBackground = true; QualitySettings.vSyncCount = 0; Application.targetFrameRate = 60;
        G.enabled = P.enabled = false;
        G.gameObject.AddComponent<AccessTestOverlay>();
        Table();
#if ENABLE_INPUT_SYSTEM
        // ---- U02: real events through GameInput
        Do(0.1f, () => { Stage(); Bindings.SetKey(ControlAction.Interact, Key.K); interact = 0; Keys(Key.K); });
        Do(0.15f, () => { Check(interact == 1, "the rebound key fires interact through GameInput"); Keys(); });
        Do(0.1f, () => { interact = 0; Keys(Key.E); });
        Do(0.15f, () => { Check(interact == 0, "the old key no longer fires it"); Keys(); Bindings.ResetDefaults(); });

        // ---- U02: the Controls page, reached and driven with the controller
        Do(0.15f, () => { Call(G, "OpenPause", 2); Set(G, "showSettings", true); Set(G, "settingsPage", 0); });
        Do(0.2f, () => Pad());
        for (int i = 0; i < 3; i++) { Do(0.12f, () => Direction(Vector2.right)); Do(0.12f, () => Pad()); }
        Do(0.15f, () => { Check(UiNavigation.FocusedLabel == "CONTROLS", "controller focus reaches the Controls tab"); Pad(GamepadButton.South); });
        Do(0.15f, () => { Pad(); });
        Do(0.2f, () => { Check((int)Get(G, "settingsPage") == 3, "the Controls page opens through controller focus"); Shot("controls"); });
        Do(0.3f, () => { Bindings.Listen(ControlAction.Throw, false); confirms = 0; throws = 0; Keys(Key.Y); });
        Do(0.15f, () => { Check(Bindings.Listening < 0 && Bindings.KeyFor(ControlAction.Throw) == Key.Y, "a listening cell takes the next key pressed"); Keys(); });
        Do(0.1f, () => { Bindings.Listen(ControlAction.Throw, false); Keys(Key.Escape); });
        Do(0.15f, () => { Check(Bindings.Listening < 0 && Bindings.KeyFor(ControlAction.Throw) == Key.Y && G.State == Game.GameState.Paused && (int)Get(G, "pauseTab") == 2 && (bool)Get(G, "showSettings"),
            "Esc cancels listening without leaving the settings page"); Keys(); });
        Do(0.1f, () => { Bindings.Listen(ControlAction.Throw, false); Keys(Key.W); });
        Do(0.15f, () => { Check(Bindings.KeyFor(ControlAction.Throw) == Key.Y && Bindings.LastMessage.Contains("kept"), "a reserved key pressed while listening is refused with a reason"); Keys(); });
        Do(0.1f, () => { Bindings.Listen(ControlAction.Map, true); confirms = 0; Pad(GamepadButton.South); });
        Do(0.15f, () =>
        {
            Check(Bindings.PadFor(ControlAction.Map) == GamepadButton.South && Bindings.PadFor(ControlAction.Jump) == GamepadButton.Select, "a controller button taken while listening swaps with Jump");
            Check(confirms == 0 && (int)Get(G, "settingsPage") == 3 && (bool)Get(G, "showSettings"), "the captured press does not also confirm a menu button or go back");
            Pad();
        });
        Do(0.1f, () => { Bindings.Listen(ControlAction.Map, true); Pad(GamepadButton.East); });
        Do(0.15f, () => { Check(Bindings.PadFor(ControlAction.Map) == GamepadButton.East && G.State == Game.GameState.Paused && (bool)Get(G, "showSettings"), "binding the back button does not close the page"); Pad(); Shot("controls-rebound"); });
        Do(0.3f, () => { Bindings.Listen(ControlAction.Lock, true); InputSystem.RemoveDevice(pad); });
        Do(0.15f, () => { Check(Bindings.Listening < 0 && Bindings.LastMessage.Contains("No controller") && Bindings.PadFor(ControlAction.Lock) == GamepadButton.RightStick, "a controller pulled mid-capture cancels it cleanly"); InputSystem.AddDevice(pad); Pad(); });
        Do(0.2f, () => { Check(GameInput.ControllerConnected, "the controller reconnects"); Bindings.ResetDefaults(); Check(Bindings.IsDefault, "reset controls restores the defaults after a session of changes"); });

        // ---- U03: the Access page and the hero's guard and sprint
        Do(0.1f, () => { Set(G, "settingsPage", 4); });
        Do(0.25f, () => Shot("access"));
        Do(0.2f, () => { Set(G, "showSettings", false); Call(G, "ClosePause"); Stage(); Save.GuardToggle = false; driveHero = true; Keys(Key.C); });
        Do(0.2f, () => { Check(Move() == "Block", "hold guard: holding the block key raises the guard"); Keys(); });
        Do(0.2f, () => { Check(Move() != "Block", "hold guard: letting go lowers it"); Save.GuardToggle = true; Keys(Key.C); });
        Do(0.12f, () => Keys());
        Do(0.3f, () => { Check(Move() == "Block" && Access.Guard, "toggle guard: one press raises the guard and it stays up with the key released"); Keys(Key.C); });
        Do(0.12f, () => Keys());
        Do(0.2f, () => { Check(Move() != "Block" && !Access.Guard, "toggle guard: a second press lowers it"); Keys(Key.C); });
        Do(0.12f, () => Keys());
        Do(0.15f, () => { Check(Access.Guard, "toggle guard raised again"); Keys(Key.LeftShift); });
        Do(0.12f, () => { Check(!Access.Guard, "a dodge drops a toggled guard"); Keys(); });
        Do(0.4f, () => { driveHero = false; P.SetHero(Array.FindIndex(Heroes.All, h => h.id == "nix")); P.enabled = false; Stage(); Check(P.Weapon.guard == GuardStyle.Deflect, "Nix carries the deflecting blade"); driveHero = true; Keys(Key.C); });
        Do(0.06f, () => Keys());
        Do(0.06f, () => Check(Move() == "Block", "toggle guard raises Nix's deflect"));
        Do(0.6f, () => { Check(Move() != "Block" && !Access.Guard, "a spent deflect drops the toggle, so the guard is not stuck up"); driveHero = false; P.SetHero(0); P.enabled = false; Save.GuardToggle = false; });

        Do(0.1f, () => { Save.SprintToggle = true; Access.Reset(); sprintMode = 1; steer = true; Keys(Key.LeftShift); });
        Do(0.1f, () => Keys());
        Do(0.1f, () => { Check(Access.Sprint, "toggle sprint: one press in town starts the run, and it holds with the key released"); Keys(Key.LeftShift); });
        Do(0.1f, () => Keys());
        Do(0.1f, () => { Check(!Access.Sprint, "toggle sprint: a second press in town walks again"); sprintMode = 2; Keys(Key.LeftShift); });
        Do(0.1f, () => Keys());
        Do(0.1f, () => { Check(Access.Sprint, "toggle sprint: a dash in a fight carries into a sprint"); steer = false; });
        Do(0.5f, () => { Check(!Access.Sprint, "toggle sprint ends when the hero stops moving"); Save.SprintToggle = false; sprintMode = 0; Access.Reset(); });
#endif
        // ---- U03: timing assist on a real parry
        Do(0.1f, () =>
        {
            Stage();
            Vector3 front = P.transform.position + P.transform.forward * 2f;
            Save.TimingAssist = false;
            Call(P, "StartBlock"); Set(P, "blockT", 0.3f); Set(P, "invuln", 0f);
            var plain = P.ReceiveHit(new DamageRequest(1f, front, 0f)).outcome;
            Stage();
            Save.TimingAssist = true;
            Call(P, "StartBlock"); Set(P, "blockT", 0.3f); Set(P, "invuln", 0f);
            var assisted = P.ReceiveHit(new DamageRequest(1f, front, 0f)).outcome;
            Check(plain == HitOutcome.Blocked && assisted == HitOutcome.Parried, "a block raised 0.3 s early is a plain block, and a parry with timing assist (" + plain + " / " + assisted + ")");
            Stage();
            Call(P, "StartBlock"); Set(P, "blockT", 0.45f); Set(P, "invuln", 0f);
            Check(P.ReceiveHit(new DamageRequest(1f, front, 0f)).outcome == HitOutcome.Blocked, "assist widens the window; it does not make every block a parry");
            Check(Access.Buffer == 0.35f && Access.ParryScale == 1.6f, "assist holds a pressed move 0.35 s and widens the parry 60%");
            Call(P, "Buffer", Enum.Parse(P.GetType().GetNestedType("BufferedAction", Any), "Light"), Vector3.forward);
            Check(Mathf.Abs((float)Get(P, "bufferedUntil") - Time.time - 0.35f) < 0.01f, "the hero's input buffer follows the assist");
            Save.TimingAssist = false;
            Call(P, "Buffer", Enum.Parse(P.GetType().GetNestedType("BufferedAction", Any), "Light"), Vector3.forward);
            Check(Mathf.Abs((float)Get(P, "bufferedUntil") - Time.time - 0.2f) < 0.01f, "without assist the buffer is the standard 0.2 s");
            Call(P, "ClearIntent");
        });

        // ---- U03: onboarding tip
        Do(0.1f, () =>
        {
            var hints = (string[])typeof(Game).GetField("Hints", Any).GetValue(null);
            Check(hints.Length == 9 && ControlGuide.Tutorial(8).Contains("Controls") && ControlGuide.Tutorial(8).Contains("Access"), "the first climb's last tip points at Controls and Access");
            Save.GuardToggle = true; Save.SprintToggle = true;
            Check(ControlGuide.Tutorial(4).Contains("raises or lowers") && ControlGuide.Tutorial(3).Contains("carries into a sprint"), "tips describe the toggles when they are on");
            Save.GuardToggle = false; Save.SprintToggle = false;
            Check(ControlGuide.Tutorial(4).Contains(" block;") && ControlGuide.Tutorial(3).Contains("hold to sprint"), "and the holds when they are off");
            Set(G, "hintStep", 8); Set(G, "hintT", 0f); Save.TimingAssist = true;
        });
        Do(0.3f, () => { Shot("tip-and-assist"); });

        // ---- U01: fight focus
        Do(0.3f, () =>
        {
            Set(G, "hintStep", -1); Save.TimingAssist = false;
            Save.CombatFocusHud = true;
            for (int i = 0; i < 40; i++) Call(G, "TickFocus", true, 0.02f);
            Check(Mathf.Approximately(G.FocusFade, Game.FocusFadeLevel), "in a fight the side panels fade to " + Game.FocusFadeLevel);
            for (int i = 0; i < 10; i++) Call(G, "TickFocus", false, 0.02f);
            Check(G.FocusFade > Game.FocusFadeLevel && G.FocusFade < 1f, "they return gradually, not with a pop");
            for (int i = 0; i < 40; i++) Call(G, "TickFocus", false, 0.02f);
            Check(G.FocusFade == 1f, "out of the fight they are fully back");
            Save.CombatFocusHud = false;
            for (int i = 0; i < 40; i++) Call(G, "TickFocus", true, 0.02f);
            Check(G.FocusFade == 1f, "with fight focus off nothing fades");
            Save.CombatFocusHud = true;
            for (int i = 0; i < 40; i++) Call(G, "TickFocus", true, 0.02f);
        });
        Do(0.3f, () => Shot("fight-focus"));

        // ---- U01: the journal
        Do(0.1f, () =>
        {
            for (int i = 0; i < 40; i++) Call(G, "TickFocus", false, 0.02f);
            Save.Wipe(); Save.SeenIntro = true; Save.Level = 6; Save.BestFloor = 1; Save.TowerFloor = 1; Save.Flush();
            var fresh = G.Journal();
            Check(fresh.Count >= 2 && fresh[0].head == "NOW" && fresh.Any(e => e.head == "COMMISSION") && !fresh.Any(e => e.head == "RESCUE" || e.head == "GUARDIAN"),
                "a young journey's journal names the objective and commission and invents no later chapter");
            Save.BestFloor = 4; Save.TowerFloor = 4; Save.Pet = true;
            JourneyStore.SetString("pilot:event:selected", "courier-medicine");
            JourneyStore.SetInt("pilot:event:choice", 1); JourneyStore.SetInt("consequence:pilot:courier:helped", 1); JourneyStore.SetInt("consequence:pilot:courier:reported", 1);
            JourneyStore.SetInt("rescue:rhea:choice", 2); JourneyStore.SetInt("consequence:rescue:rhea:guardian-intel", 1);
            JourneyStore.SetInt("chapter:tower:05:gatekeeper-checkpoint-v1", 1);
            Companion.SetOrder(CompanionOrder.Hold);
            Save.TimingAssist = true;
            var mid = G.Journal();
            string all = string.Join("\n", mid.Select(e => e.head + ": " + e.line));
            Debug.Log("ACCESS JOURNAL\n" + all);
            Check(mid.Any(e => e.head == "THE COURIER" && e.line.Contains("medicine") && e.line.Contains("shortcut") && e.line.Contains("reported")), "the courier's outcome is recorded with the supplies and the report");
            Check(mid.Any(e => e.head == "RESCUE" && e.line.Contains("Rhea") && e.line.Contains("ambush") && e.line.Contains("pillars")), "Rhea's choice and both its effects are recorded");
            Check(mid.Any(e => e.head == "CHECKPOINT" && e.line.Contains("Gatekeeper") && e.line.Contains("marked pillar")), "the floor-5 checkpoint and the intel are recorded");
            Check(mid.Any(e => e.head == "MOP" && e.line.StartsWith("Hold")), "Mop's standing order is recorded");
            Check(mid.Any(e => e.head == "ASSIST"), "timing assist is disclosed in the journal");
            Call(G, "OpenPause", 3);
        });
        Do(0.3f, () => Shot("journal"));
        Do(0.1f, () =>
        {
            JourneyStore.SetInt("rescue:rhea:choice", 1); JourneyStore.SetInt("consequence:rescue:rhea:guardian-intel", 0);
            JourneyStore.SetInt("consequence:rescue:rhea:safe-return", 1); JourneyStore.SetInt("consequence:rescue:rhea:home", 1);
            JourneyStore.SetInt("chapter:tower:05:gatekeeper-checkpoint-v1", 0); Save.BestFloor = 5; Save.TimingAssist = false;
            var late = G.Journal();
            Check(late.Any(e => e.head == "RESCUE" && e.line.Contains("home") && e.line.Contains("thank")), "sending Rhea home records the draught and Lady Vane's waiting thanks");
            Check(late.Any(e => e.head == "GUARDIAN" && e.line.Contains("fallen")) && !late.Any(e => e.head == "CHECKPOINT" || e.head == "ASSIST"), "a fallen Gatekeeper replaces the checkpoint line");
            JourneyStore.SetInt("consequence:rescue:rhea:vane-thanks-paid", 1);
            Check(G.Journal().Any(e => e.head == "RESCUE" && e.line.Contains("paid")), "paid thanks are recorded once paid");
        });
        Do(0.2f, () =>
        {
            Call(G, "ClosePause");
            Check(errors.Count == 0, "access, controls and journal scenarios logged no unexpected errors" + (errors.Count > 0 ? ": " + errors[0] : ""));
            foreach (var shot in new[] { "controls", "controls-rebound", "access", "tip-and-assist", "fight-focus", "journal" })
                Check(File.Exists("Logs/access/" + shot + ".png"), "capture " + shot + " was written");
            Save.Wipe(); Bindings.ResetDefaults(); Save.GuardToggle = Save.SprintToggle = Save.TimingAssist = false;
            Check(JourneyStore.Error == null, "the journey store is healthy afterwards");
        });
    }

    // ------------------------------------------------------------ the overlay and runner

    public static void DrawUi() { if (G != null && !G.enabled) Call(G, "OnGUI"); }
    public static void DriveInput()
    {
        if (G == null || G.enabled) return;
        Bindings.Capture();
        GameInput.PollDevice();
        Call(G, "TickControlAccess");
        if (GameInput.InteractPressed) interact++;
        if (GameInput.ThrowPressed) throws++;
        if (GameInput.JumpPressed) jumps++;
        if (GameInput.MenuConfirmPressed) confirms++;
        if (sprintMode > 0) Access.Tick(steer, sprintMode == 1, Time.deltaTime);
        if (!driveHero || P == null) return;
        // The hero's own combat input, as his Update runs it, without moving him about the floor.
        float dt = Time.deltaTime;
        ((CharacterController)Get(P, "cc")).Move(Vector3.down * 0.05f);
        Access.Tick(false, false, dt);
        Call(P, "ReadCombatInput", Vector3.zero, dt);
    }

    static AccessControlsTest() { if (SessionState.GetBool(Session, false)) { EditorApplication.update += Tick; Application.logMessageReceived += OnLog; } }

    public static void Run()
    {
        Save.Wipe(); Save.SeenIntro = true; Save.Flush();
        PlayerPrefs.DeleteKey("access.guardToggle"); PlayerPrefs.DeleteKey("access.sprintToggle"); PlayerPrefs.DeleteKey("access.timingAssist");
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
            if (steps == null) { steps = new List<Step>(); Script(); deadline = EditorApplication.timeSinceStartup + 180; next = EditorApplication.timeSinceStartup + (steps.Count > 0 ? steps[0].wait : 0); return; }
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("access scenario deadline exceeded");
            if (at < steps.Count) { steps[at++].action(); if (at < steps.Count) { next = EditorApplication.timeSinceStartup + steps[at].wait; return; } }
            Debug.Log("ACCESS DONE " + passed + " checks passed");
            Finish(0);
        }
        catch (Exception e) { Debug.LogError("ACCESS FAILED step=" + at + " " + e); Finish(1); }
    }

    static void Finish(int code)
    {
        SessionState.SetBool(Session, false);
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= OnLog;
        EditorApplication.Exit(code);
    }
}

public class AccessTestOverlay : MonoBehaviour
{
    void OnGUI() => AccessControlsTest.DrawUi();
    void Update() => AccessControlsTest.DriveInput();
}
