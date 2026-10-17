using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
#endif

// The player's own controls (package U02). Thirteen actions can be moved to any free key and any
// controller button; GameInput reads them from here and ControlGuide prints them, so prompts,
// the help page and the tutorial always name the real binding. Movement, the menus, pause,
// target cycling and the mouse buttons stay fixed so no binding can lock the player out.
// Stored in PlayerPrefs as "bind.kb.<Action>" and "bind.pad.<Action>"; a stored table that is
// invalid or doubles a binding is ignored and the defaults return.
public static class Bindings
{
    public static readonly ControlAction[] Actions =
    {
        ControlAction.Jump, ControlAction.Attack, ControlAction.Dash, ControlAction.Block, ControlAction.Heavy,
        ControlAction.Throw, ControlAction.Burst, ControlAction.Wave, ControlAction.Interact, ControlAction.Lock,
        ControlAction.Status, ControlAction.Map, ControlAction.Companion,
    };
    public static readonly string[] Names =
        { "Jump", "Attack", "Dash / sprint", "Block / parry", "Heavy", "Throw", "Burst", "Wave", "Interact", "Lock on", "Status", "Map", "Companion order" };
    public const float ListenSeconds = 6f;

    public static int IndexOf(ControlAction action) => System.Array.IndexOf(Actions, action);
    public static bool Rebindable(ControlAction action) => IndexOf(action) >= 0;

    // The action now waiting for a key or button, and on which device. -1 when none.
    public static int Listening { get; private set; } = -1;
    public static bool ListeningPad { get; private set; }
    static int listenFrame, quietFrame = -1;
    static float listenUntil;
    // True while listening and on the frame a capture ends, so the same press cannot also
    // confirm a menu button, go back or pause.
    public static bool Busy => Listening >= 0 || quietFrame == Time.frameCount;
    public static string LastMessage { get; private set; } = "";

#if ENABLE_INPUT_SYSTEM
    public static readonly Key[] KeyDefaults =
        { Key.Space, Key.J, Key.LeftShift, Key.C, Key.R, Key.G, Key.Q, Key.F, Key.E, Key.T, Key.Tab, Key.M, Key.H };
    public static readonly GamepadButton[] PadDefaults =
    {
        GamepadButton.South, GamepadButton.West, GamepadButton.East, GamepadButton.LeftTrigger, GamepadButton.RightTrigger,
        GamepadButton.DpadUp, GamepadButton.RightShoulder, GamepadButton.LeftShoulder, GamepadButton.North,
        GamepadButton.RightStick, GamepadButton.DpadDown, GamepadButton.Select, GamepadButton.LeftStick,
    };
    // Every pad button a player may choose; Start opens the menu and the d-pad's sides cycle targets.
    public static readonly GamepadButton[] PadChoices =
    {
        GamepadButton.South, GamepadButton.East, GamepadButton.West, GamepadButton.North, GamepadButton.LeftShoulder,
        GamepadButton.RightShoulder, GamepadButton.LeftTrigger, GamepadButton.RightTrigger, GamepadButton.DpadUp,
        GamepadButton.DpadDown, GamepadButton.LeftStick, GamepadButton.RightStick, GamepadButton.Select,
    };
    static Key[] keys;
    static GamepadButton[] pads;

    static void Load()
    {
        if (keys != null) return;
        keys = (Key[])KeyDefaults.Clone();
        pads = (GamepadButton[])PadDefaults.Clone();
        var k = new Key[keys.Length]; var p = new GamepadButton[pads.Length];
        bool keysOk = true, padsOk = true;
        for (int i = 0; i < Actions.Length; i++)
        {
            k[i] = (Key)PlayerPrefs.GetInt("bind.kb." + Actions[i], (int)KeyDefaults[i]);
            p[i] = (GamepadButton)PlayerPrefs.GetInt("bind.pad." + Actions[i], (int)PadDefaults[i]);
            if (!KeyAllowed(k[i])) keysOk = false;
            if (System.Array.IndexOf(PadChoices, p[i]) < 0) padsOk = false;
            for (int j = 0; j < i; j++) { if (k[j] == k[i]) keysOk = false; if (p[j] == p[i]) padsOk = false; }
        }
        if (keysOk) keys = k;
        if (padsOk) pads = p;
    }

    // Drops the cached table so the next read comes from PlayerPrefs again (tests and restores).
    public static void Reload() { keys = null; pads = null; Listening = -1; }

    public static Key KeyFor(ControlAction action) { Load(); int i = IndexOf(action); return i < 0 ? Key.None : keys[i]; }
    public static GamepadButton PadFor(ControlAction action) { Load(); int i = IndexOf(action); return i < 0 ? GamepadButton.Start : pads[i]; }

    // Kept by movement, menus, pause, target cycling, dialogue and the fixed second bindings.
    public static bool Reserved(Key key)
    {
        switch (key)
        {
            case Key.W: case Key.A: case Key.S: case Key.D: case Key.UpArrow: case Key.DownArrow: case Key.LeftArrow: case Key.RightArrow:
            case Key.Escape: case Key.P: case Key.Enter: case Key.NumpadEnter: case Key.Z: case Key.V: case Key.X:
            case Key.Digit1: case Key.Digit2: case Key.Digit3: case Key.F3: case Key.LeftCtrl: case Key.I:
            case Key.LeftMeta: case Key.RightMeta: case Key.ContextMenu: case Key.PrintScreen: case Key.None:
                return true;
        }
        return false;
    }
    static bool KeyAllowed(Key key) => key > Key.None && key <= Key.OEM5 && System.Enum.IsDefined(typeof(Key), key) && !Reserved(key);

    public static bool Pressed(ControlAction action)
    {
        Load(); int i = IndexOf(action); if (i < 0 || Listening >= 0) return false;
        var k = Keyboard.current; var g = Gamepad.current;
        return (k != null && k[keys[i]].wasPressedThisFrame) || (g != null && g[pads[i]].wasPressedThisFrame);
    }

    public static bool Held(ControlAction action)
    {
        Load(); int i = IndexOf(action); if (i < 0 || Listening >= 0) return false;
        var k = Keyboard.current; var g = Gamepad.current;
        return (k != null && k[keys[i]].isPressed) || (g != null && g[pads[i]].isPressed);
    }

    // Binds a key. A key already used by another action is swapped onto it, so nothing is lost.
    // Returns false for a reserved key.
    public static bool SetKey(ControlAction action, Key key)
    {
        Load(); int i = IndexOf(action);
        if (i < 0 || !KeyAllowed(key)) { LastMessage = KeyName(key) + " is kept for movement, menus or a fixed control"; return false; }
        int other = System.Array.IndexOf(keys, key);
        if (other >= 0 && other != i) { keys[other] = keys[i]; LastMessage = Names[other] + " moved to " + KeyName(keys[other]); }
        else LastMessage = Names[i] + " set to " + KeyName(key);
        keys[i] = key;
        Persist();
        return true;
    }

    public static bool SetPad(ControlAction action, GamepadButton button)
    {
        Load(); int i = IndexOf(action);
        if (i < 0 || System.Array.IndexOf(PadChoices, button) < 0) { LastMessage = PadName(button) + " is kept for the menu or target switching"; return false; }
        int other = System.Array.IndexOf(pads, button);
        if (other >= 0 && other != i) { pads[other] = pads[i]; LastMessage = Names[other] + " moved to " + PadName(pads[other]); }
        else LastMessage = Names[i] + " set to " + PadName(button);
        pads[i] = button;
        Persist();
        return true;
    }

    public static void ResetDefaults()
    {
        Load();
        keys = (Key[])KeyDefaults.Clone();
        pads = (GamepadButton[])PadDefaults.Clone();
        Listening = -1;
        LastMessage = "Controls reset to defaults";
        Persist();
    }

    public static bool IsDefault
    {
        get
        {
            Load();
            for (int i = 0; i < Actions.Length; i++) if (keys[i] != KeyDefaults[i] || pads[i] != PadDefaults[i]) return false;
            return true;
        }
    }

    static void Persist()
    {
        PlaytestLog.Emit("rebind", note: LastMessage);
        for (int i = 0; i < Actions.Length; i++)
        {
            PlayerPrefs.SetInt("bind.kb." + Actions[i], (int)keys[i]);
            PlayerPrefs.SetInt("bind.pad." + Actions[i], (int)pads[i]);
        }
        PlayerPrefs.Save();
    }

    public static void Listen(ControlAction action, bool pad)
    {
        Load();
        Listening = IndexOf(action); ListeningPad = pad;
        listenFrame = Time.frameCount; listenUntil = Time.unscaledTime + ListenSeconds;
        LastMessage = pad ? "Press a controller button for " + Names[Listening] + " (Start cancels)" : "Press a key for " + Names[Listening] + " (Esc cancels)";
    }

    public static void Cancel(string why = "Unchanged")
    {
        if (Listening < 0) return;
        Listening = -1; quietFrame = Time.frameCount; LastMessage = why;
    }

    // Called once a frame before menus read input. The press that opened listening is ignored.
    public static void Capture()
    {
        if (Listening < 0 || Time.frameCount == listenFrame) return;
        if (Time.unscaledTime > listenUntil) { Cancel("No input; unchanged"); return; }
        var action = Actions[Listening];
        if (!ListeningPad)
        {
            var k = Keyboard.current; if (k == null) return;
            if (k.escapeKey.wasPressedThisFrame) { Cancel(); return; }
            foreach (var control in k.allKeys)
            {
                if (control == null || !control.wasPressedThisFrame) continue;
                Listening = -1; quietFrame = Time.frameCount;
                SetKey(action, control.keyCode);
                return;
            }
        }
        else
        {
            var g = Gamepad.current; if (g == null) { Cancel("No controller; unchanged"); return; }
            if (g.startButton.wasPressedThisFrame) { Cancel(); return; }
            foreach (var b in new[] { GamepadButton.DpadLeft, GamepadButton.DpadRight })
                if (g[b].wasPressedThisFrame) { Listening = -1; quietFrame = Time.frameCount; SetPad(action, b); return; }
            foreach (var b in PadChoices)
            {
                if (!g[b].wasPressedThisFrame) continue;
                Listening = -1; quietFrame = Time.frameCount;
                SetPad(action, b);
                return;
            }
        }
    }

    public static string KeyName(Key key)
    {
        switch (key)
        {
            case Key.LeftShift: return "Shift";
            case Key.RightShift: return "R-Shift";
            case Key.LeftAlt: return "Alt";
            case Key.RightAlt: return "R-Alt";
            case Key.RightCtrl: return "R-Ctrl";
            case Key.LeftCtrl: return "Ctrl";
            case Key.Backquote: return "`";
            case Key.Minus: return "-";
            case Key.Equals: return "=";
            case Key.LeftBracket: return "[";
            case Key.RightBracket: return "]";
            case Key.Semicolon: return ";";
            case Key.Quote: return "'";
            case Key.Comma: return ",";
            case Key.Period: return ".";
            case Key.Slash: return "/";
            case Key.Backslash: return "\\";
            case Key.Backspace: return "Backspace";
            case Key.CapsLock: return "Caps";
        }
        string s = key.ToString();
        if (s.StartsWith("Digit")) return s.Substring(5);
        if (s.StartsWith("Numpad")) return "Num " + s.Substring(6);
        return s;
    }

    public static string PadName(GamepadButton b)
    {
        switch (b)
        {
            case GamepadButton.South: return "A / South";
            case GamepadButton.West: return "X / West";
            case GamepadButton.East: return "B / East";
            case GamepadButton.North: return "Y / North";
            case GamepadButton.LeftTrigger: return "LT";
            case GamepadButton.RightTrigger: return "RT";
            case GamepadButton.LeftShoulder: return "LB";
            case GamepadButton.RightShoulder: return "RB";
            case GamepadButton.DpadUp: return "D-pad up";
            case GamepadButton.DpadDown: return "D-pad down";
            case GamepadButton.DpadLeft: return "D-pad left";
            case GamepadButton.DpadRight: return "D-pad right";
            case GamepadButton.LeftStick: return "Left stick click";
            case GamepadButton.RightStick: return "Right stick click";
            case GamepadButton.Select: return "View / Select";
            default: return "Menu / Start";
        }
    }

    public static string PadShort(GamepadButton b)
    {
        switch (b)
        {
            case GamepadButton.South: return "A";
            case GamepadButton.West: return "X";
            case GamepadButton.East: return "B";
            case GamepadButton.North: return "Y";
            case GamepadButton.DpadUp: return "D-up";
            case GamepadButton.DpadDown: return "D-down";
            case GamepadButton.LeftStick: return "L3";
            case GamepadButton.RightStick: return "R3";
            case GamepadButton.Select: return "View";
            default: return PadName(b);
        }
    }

    public static string KeyLabel(ControlAction action) => KeyName(KeyFor(action));
    public static string PadLabel(ControlAction action) => PadName(PadFor(action));
    public static string PadShortLabel(ControlAction action) => PadShort(PadFor(action));
#else
    public static void Reload() { }
    public static void ResetDefaults() { }
    public static bool IsDefault => true;
    public static void Listen(ControlAction action, bool pad) { }
    public static void Cancel(string why = "") { }
    public static void Capture() { }
    static readonly string[] KeyText = { "Space", "J", "Shift", "C", "R", "G", "Q", "F", "E", "T", "Tab", "M", "H" };
    static readonly string[] PadText = { "A / South", "X / West", "B / East", "LT", "RT", "D-pad up", "RB", "LB", "Y / North", "Right stick click", "D-pad down", "View / Select", "Left stick click" };
    public static string KeyLabel(ControlAction action) => KeyText[IndexOf(action)];
    public static string PadLabel(ControlAction action) => PadText[IndexOf(action)];
    public static string PadShortLabel(ControlAction action) => PadText[IndexOf(action)];
#endif
}
