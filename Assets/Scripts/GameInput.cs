using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Single place that reads input, so the game works whether the project's
// Active Input Handling is the Input System package or the legacy manager.
public static class GameInput
{
    public static bool UsingGamepad { get; private set; }
    public static bool ControllerConnected
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            return Gamepad.current != null && Gamepad.current.added;
#else
            return false;
#endif
        }
    }
    static int deviceFrame = -1;
    static Vector2 heldDirection;
    static float repeatAt;
    public static Vector2 MenuDirection { get; private set; }
    public static void PollDevice()
    {
        if (deviceFrame == Time.frameCount) return;
        deviceFrame = Time.frameCount;
        Vector2 next = Vector2.zero;
#if ENABLE_INPUT_SYSTEM
        var pad = Gamepad.current; var keyboard = Keyboard.current; var mouse = Mouse.current;
        if (pad != null && (PadPressed(pad)
            || pad.leftStick.ReadValue().sqrMagnitude > 0.16f || pad.rightStick.ReadValue().sqrMagnitude > 0.16f)) UsingGamepad = true;
        // Cursor locking/unlocking can generate pointer deltas. Explicit keys
        // and mouse buttons switch prompts; synthetic motion cannot steal focus.
        if ((keyboard != null && keyboard.anyKey.wasPressedThisFrame) || (mouse != null &&
            (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame))) UsingGamepad = false;
        if (pad != null) next = pad.dpad.ReadValue() + pad.leftStick.ReadValue();
        if (keyboard != null)
        {
            if (keyboard.leftArrowKey.isPressed) next.x = -1f;
            if (keyboard.rightArrowKey.isPressed) next.x = 1f;
            if (keyboard.upArrowKey.isPressed) next.y = 1f;
            if (keyboard.downArrowKey.isPressed) next.y = -1f;
        }
#else
        if (Input.GetKey(KeyCode.LeftArrow)) next.x = -1f;
        if (Input.GetKey(KeyCode.RightArrow)) next.x = 1f;
        if (Input.GetKey(KeyCode.UpArrow)) next.y = 1f;
        if (Input.GetKey(KeyCode.DownArrow)) next.y = -1f;
#endif
        next = Mathf.Max(Mathf.Abs(next.x), Mathf.Abs(next.y)) < 0.55f ? Vector2.zero :
            Mathf.Abs(next.x) > Mathf.Abs(next.y) ? new Vector2(Mathf.Sign(next.x), 0f) : new Vector2(0f, Mathf.Sign(next.y));
        MenuDirection = Vector2.zero;
        if (Bindings.Busy) next = Vector2.zero;
        if (next == Vector2.zero) { heldDirection = next; repeatAt = 0f; }
        else if (next != heldDirection) { MenuDirection = heldDirection = next; repeatAt = Time.unscaledTime + 0.35f; }
        else if (Time.unscaledTime >= repeatAt) { MenuDirection = next; repeatAt = Time.unscaledTime + 0.16f; }
    }
#if ENABLE_INPUT_SYSTEM
    static bool PadPressed(Gamepad pad)
    {
        foreach (var control in pad.allControls)
            if (control is UnityEngine.InputSystem.Controls.ButtonControl button && button.wasPressedThisFrame) return true;
        return false;
    }
#endif
    public static bool MenuConfirmPressed
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            return !Bindings.Busy && ((Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame)
                || (Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame));
#else
            return Input.GetKeyDown(KeyCode.Return);
#endif
        }
    }
    public static bool MenuBackPressed
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            return !Bindings.Busy && Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame;
#else
            return false;
#endif
        }
    }
#if ENABLE_INPUT_SYSTEM
    public static Vector2 Move
    {
        get
        {
            Vector2 v = Vector2.zero;
            var k = Keyboard.current;
            if (k != null)
            {
                if (k.dKey.isPressed || k.rightArrowKey.isPressed) v.x += 1;
                if (k.aKey.isPressed || k.leftArrowKey.isPressed) v.x -= 1;
                if (k.wKey.isPressed || k.upArrowKey.isPressed) v.y += 1;
                if (k.sKey.isPressed || k.downArrowKey.isPressed) v.y -= 1;
            }
            var g = Gamepad.current;
            if (g != null) v += g.leftStick.ReadValue();
            return Vector2.ClampMagnitude(v, 1f);
        }
    }

    public static Vector2 Look
    {
        get
        {
            Vector2 v = Vector2.zero;
            var m = Mouse.current;
            if (m != null) v += m.delta.ReadValue() * 0.1f;
            var g = Gamepad.current;
            if (g != null) v += g.rightStick.ReadValue() * (Time.unscaledDeltaTime * 90f);
            return v;
        }
    }

    public static bool JumpPressed
    {
        get
        {
            return Bindings.Pressed(ControlAction.Jump);
        }
    }

    public static bool JumpHeld
    {
        get
        {
            return Bindings.Held(ControlAction.Jump);
        }
    }

    public static bool LockPressed => Bindings.Pressed(ControlAction.Lock);

    public static int TargetCycle
    {
        get
        {
            var k = Keyboard.current; var g = Gamepad.current;
            if ((k != null && k.zKey.wasPressedThisFrame) || (g != null && g.dpad.left.wasPressedThisFrame)) return -1;
            if ((k != null && k.vKey.wasPressedThisFrame) || (g != null && g.dpad.right.wasPressedThisFrame)) return 1;
            return 0;
        }
    }

    public static bool AttackPressed
    {
        get
        {
            var m = Mouse.current;
            return (m != null && m.leftButton.wasPressedThisFrame) || Bindings.Pressed(ControlAction.Attack);
        }
    }

    public static bool DashPressed
    {
        get
        {
            var m = Mouse.current;
            return (m != null && m.rightButton.wasPressedThisFrame) || Bindings.Pressed(ControlAction.Dash);
        }
    }

    // Held after a dash to sprint; in town it is simply "run".
    public static bool DashHeld
    {
        get
        {
            var m = Mouse.current;
            return (m != null && m.rightButton.isPressed) || Bindings.Held(ControlAction.Dash);
        }
    }

    public static bool ThrowPressed
    {
        get
        {
            return Bindings.Pressed(ControlAction.Throw);
        }
    }

    public static bool BurstPressed
    {
        get
        {
            return Bindings.Pressed(ControlAction.Burst);
        }
    }

    public static bool AttackHeld
    {
        get
        {
            var m = Mouse.current;
            return (m != null && m.leftButton.isPressed) || Bindings.Held(ControlAction.Attack);
        }
    }

    public static bool HeavyPressed
    {
        get
        {
            var m = Mouse.current;
            return (m != null && m.middleButton.wasPressedThisFrame) || Bindings.Pressed(ControlAction.Heavy);
        }
    }

    public static bool BlockHeld
    {
        get
        {
            var k = Keyboard.current;
            return (k != null && k.leftCtrlKey.isPressed && !Bindings.Busy) || Bindings.Held(ControlAction.Block);
        }
    }

    public static bool BlockPressed
    {
        get
        {
            var k = Keyboard.current;
            return (k != null && k.leftCtrlKey.wasPressedThisFrame && !Bindings.Busy) || Bindings.Pressed(ControlAction.Block);
        }
    }

    public static bool SkillPressed
    {
        get
        {
            return Bindings.Pressed(ControlAction.Wave);
        }
    }

    public static bool InteractPressed
    {
        get
        {
            return Bindings.Pressed(ControlAction.Interact);
        }
    }

    // Ends a whole conversation.
    public static bool SkipTalkPressed => (Keyboard.current != null && Keyboard.current.xKey.wasPressedThisFrame)
        || (Gamepad.current != null && Gamepad.current.buttonNorth.wasPressedThisFrame);

    // The NavMesh debug view.
    public static bool NavDebugPressed => Keyboard.current != null && Keyboard.current.f3Key.wasPressedThisFrame;

    public static bool MapPressed
    {
        get
        {
            return Bindings.Pressed(ControlAction.Map);
        }
    }

    // Give the companion the next order (follow, assist, hold, solo).
    public static bool CompanionPressed
    {
        get
        {
            return Bindings.Pressed(ControlAction.Companion);
        }
    }

    // The Status window.
    public static bool StatusPressed
    {
        get
        {
            var k = Keyboard.current;
            return (k != null && k.iKey.wasPressedThisFrame && !Bindings.Busy) || Bindings.Pressed(ControlAction.Status);
        }
    }

    public static bool PausePressed
    {
        get
        {
            var k = Keyboard.current; var g = Gamepad.current;
            return !Bindings.Busy && ((k != null && (k.escapeKey.wasPressedThisFrame || k.pKey.wasPressedThisFrame))
                || (g != null && g.startButton.wasPressedThisFrame));
        }
    }

    public static bool ConfirmPressed
    {
        get
        {
            var m = Mouse.current; var k = Keyboard.current; var g = Gamepad.current;
            if (Bindings.Busy) return false;
            return (m != null && m.leftButton.wasPressedThisFrame)
                || (k != null && (k.spaceKey.wasPressedThisFrame || k.enterKey.wasPressedThisFrame))
                || (g != null && g.buttonSouth.wasPressedThisFrame);
        }
    }

    // 0, 1 or 2 when the 1 / 2 / 3 key was pressed this frame, otherwise -1.
    public static int NumberPressed
    {
        get
        {
            var k = Keyboard.current;
            if (k == null) return -1;
            if (k.digit1Key.wasPressedThisFrame) return 0;
            if (k.digit2Key.wasPressedThisFrame) return 1;
            if (k.digit3Key.wasPressedThisFrame) return 2;
            return -1;
        }
    }
#else
    public static Vector2 Move =>
        Vector2.ClampMagnitude(new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")), 1f);

    public static Vector2 Look => new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));

    public static bool JumpPressed => Input.GetKeyDown(KeyCode.Space);
    public static bool JumpHeld => Input.GetKey(KeyCode.Space);
    public static bool LockPressed => Input.GetKeyDown(KeyCode.T);
    public static int TargetCycle => Input.GetKeyDown(KeyCode.Z) ? -1 : Input.GetKeyDown(KeyCode.V) ? 1 : 0;
    public static bool AttackPressed => Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.J);
    public static bool DashPressed => Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.LeftShift);
    public static bool DashHeld => Input.GetMouseButton(1) || Input.GetKey(KeyCode.LeftShift);
    public static bool ThrowPressed => Input.GetKeyDown(KeyCode.G);
    public static bool BurstPressed => Input.GetKeyDown(KeyCode.Q);
    public static bool InteractPressed => Input.GetKeyDown(KeyCode.E);
    public static bool MapPressed => Input.GetKeyDown(KeyCode.M);
    public static bool NavDebugPressed => Input.GetKeyDown(KeyCode.F3);
    public static bool SkipTalkPressed => Input.GetKeyDown(KeyCode.X);
    public static bool StatusPressed => Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.I);
    public static bool CompanionPressed => Input.GetKeyDown(KeyCode.H);
    public static bool SkillPressed => Input.GetKeyDown(KeyCode.F);
    public static bool AttackHeld => Input.GetMouseButton(0) || Input.GetKey(KeyCode.J);
    public static bool HeavyPressed => Input.GetMouseButtonDown(2) || Input.GetKeyDown(KeyCode.R);
    public static bool BlockHeld => Input.GetKey(KeyCode.C) || Input.GetKey(KeyCode.LeftControl);
    public static bool BlockPressed => Input.GetKeyDown(KeyCode.C) || Input.GetKeyDown(KeyCode.LeftControl);
    public static bool PausePressed => Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P);
    public static bool ConfirmPressed =>
        Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return);

    public static int NumberPressed
    {
        get
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) return 0;
            if (Input.GetKeyDown(KeyCode.Alpha2)) return 1;
            if (Input.GetKeyDown(KeyCode.Alpha3)) return 2;
            return -1;
        }
    }
#endif
}
