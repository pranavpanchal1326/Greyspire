using UnityEngine;

public partial class Game
{
    int settingsPage;
    int controlBackHandledFrame = -1;
    bool controllerWasConnected, controllerLost;
    void SetMap(bool open)
    {
        showMap = open; Juice.Paused = open; resumeFrame = Time.frameCount;
        Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = open;
    }
    static int ScoreForRegion(string id)
    {
        switch (id)
        {
            case "sunscorch": case "shardfield": case "emberscar": return 2;
            case "hollow-graves": case "sunken-fen": case "frostfall": return 3;
            default: return 1;
        }
    }

    void TickControlAccess()
    {
        if (Player == null || JourneyStore.Error != null || startupError != null) return;
        bool connected = GameInput.ControllerConnected;
        if (controllerWasConnected && !connected && GameInput.UsingGamepad)
        {
            controllerLost = true;
            if (Roaming) OpenPause(0);
        }
        if (connected || !GameInput.UsingGamepad) controllerLost = false;
        controllerWasConnected = connected;
        bool back = GameInput.MenuBackPressed || GameInput.PausePressed;
        if (!back || fadeDir != 0f || Intro) return;
        if (State == GameState.Menu)
        {
            showSettings = showCredits = confirmNew = false;
            Save.Flush();
        }
        else if (State == GameState.Shrine || State == GameState.Codex) SetState(backTo);
        else if (State == GameState.Paused && GameInput.MenuBackPressed)
        {
            if (pauseTab != 0) { pauseTab = 0; showSettings = false; Save.Flush(); }
            else ClosePause();
        }
        else if (showMap)
        {
            SetMap(false);
            controlBackHandledFrame = Time.frameCount;
        }
    }

    void SettingSlider(Rect panel, int row, string label, float value, System.Action<float> save, Color color)
    {
        float y = panel.y + 116 + row * 49;
        UI.Text(new Rect(panel.x + 28, y, 204, 28), label, 18, Color.white, TextAnchor.MiddleLeft);
        float next = UI.Slider(new Rect(panel.x + 238, y + 6, 208, 18), value, color, label);
        UI.Text(new Rect(panel.x + 454, y, 40, 28), Mathf.RoundToInt(next * 100).ToString(), 15, UI.Gold);
        if (!Mathf.Approximately(value, next)) save(next);
    }

    void DrawFeedbackSettings(float cx, float y)
    {
        var frame = new Rect(cx - 320f, Mathf.Clamp(y - 15f, 30f, UI.H - 610f), 640f, 610f);
        UI.Framed(frame, new Color(0.107f, 0.112f, 0.136f, 0.96f), UI.Ink);
        UI.Text(new Rect(frame.x, frame.y + 10, frame.width, 40), "Settings", 30, UI.Gold);
        for (int i = 0; i < SettingsPages.Length; i++)
            if (UI.Button(new Rect(frame.x + 18 + i * 121, frame.y + 62, 115, 36), SettingsPages[i], i == settingsPage ? UI.Gold : UI.Blue, 12))
            {
                if (settingsPage != i) Bindings.Cancel();
                settingsPage = i;
            }
        UiNavigation.Prefer(SettingsPages[settingsPage]);
        // The first three pages keep their 520-wide layout, centred in the wider frame.
        var panel = new Rect(frame.x + 60f, frame.y, 520f, frame.height);
        if (settingsPage == 3) DrawControlSettings(frame);
        else if (settingsPage == 4) DrawAccessSettings(frame);
        else if (settingsPage == 0)
        {
            SettingSlider(panel, 0, "Master sound", Save.Volume, v => Save.Volume = v, UI.Green);
            SettingSlider(panel, 1, "Music", Save.MusicVolume, v => Save.MusicVolume = v, UI.Purple);
            UI.Text(new Rect(panel.x + 28, panel.y + 214, 200, 30), "Sound range", 18, Color.white, TextAnchor.MiddleLeft);
            if (UI.Button(new Rect(panel.x + 238, panel.y + 214, 244, 34), Save.QuietMix ? "Quieter peaks" : "Standard", UI.Green, 17)) Save.QuietMix = !Save.QuietMix;
            UI.Text(new Rect(panel.x + 28, panel.y + 264, 200, 30), "Graphics", 18, Color.white, TextAnchor.MiddleLeft);
            int q = Save.Quality;
            if (UI.Button(new Rect(panel.x + 238, panel.y + 264, 40, 34), "<", UI.Blue, 18, q > 0)) { Save.Quality = q - 1; ApplyQuality(); }
            UI.Text(new Rect(panel.x + 282, panel.y + 264, 156, 34), QualityNames[q], 19, UI.Gold);
            if (UI.Button(new Rect(panel.x + 442, panel.y + 264, 40, 34), ">", UI.Blue, 18, q < 2)) { Save.Quality = q + 1; ApplyQuality(); }
            UI.Text(new Rect(panel.x + 28, panel.y + 313, 200, 30), "Display", 18, Color.white, TextAnchor.MiddleLeft);
            if (UI.Button(new Rect(panel.x + 238, panel.y + 313, 244, 34), Screen.fullScreen ? "Fullscreen" : "Windowed", UI.Blue, 17))
            {
                if (Screen.fullScreen) Screen.SetResolution(1600, 900, FullScreenMode.Windowed);
                else Screen.SetResolution(Screen.currentResolution.width, Screen.currentResolution.height, FullScreenMode.FullScreenWindow);
            }
            UI.Text(new Rect(panel.x + 28, panel.y + 390, 460, 70), "Quieter peaks reduces loud effects. Threat cues keep their reserved voices; master sound at zero mutes all audio.", 16, Soft, TextAnchor.UpperLeft, false);
        }
        else if (settingsPage == 1)
        {
            SettingSlider(panel, 0, "Look speed", Save.Sensitivity, v => Save.Sensitivity = v, UI.Blue);
            SettingSlider(panel, 1, "Field of view", Mathf.InverseLerp(55, 85, Save.CameraFov), v => Save.CameraFov = Mathf.Lerp(55, 85, v), UI.Blue);
            UI.Text(new Rect(panel.x + 28, panel.y + 212, 450, 26), Mathf.RoundToInt(Save.CameraFov) + " degrees", 15, UI.Gold, TextAnchor.MiddleLeft);
            SettingSlider(panel, 3, "Camera motion", Save.CameraMotion, v => Save.CameraMotion = v, UI.Green);
            SettingSlider(panel, 4, "Auto camera", Save.CameraAuto, v => Save.CameraAuto = v, UI.Green);
            UI.Text(new Rect(panel.x + 28, panel.y + 365, 200, 30), "Vertical look", 18, Color.white, TextAnchor.MiddleLeft);
            if (UI.Button(new Rect(panel.x + 238, panel.y + 365, 244, 34), Save.InvertLook ? "Inverted" : "Normal", UI.Blue, 17)) Save.InvertLook = !Save.InvertLook;
            UI.Text(new Rect(panel.x + 28, panel.y + 416, 460, 62), "Motion zero removes shake, impact shoves and FOV pulses. Auto zero stops orbit/arrival motion and lock framing; manual look and wall protection remain.", 15, Soft, TextAnchor.UpperLeft, false);
        }
        else
        {
            SettingSlider(panel, 0, "Flashes", Save.Flashes, v => Save.Flashes = v, UI.Gold);
            SettingSlider(panel, 1, "Impact frames", Save.ImpactFrames, v => Save.ImpactFrames = v, UI.Gold);
            SettingSlider(panel, 2, "Motion lines", Save.MotionLines, v => Save.MotionLines = v, UI.Blue);
            SettingSlider(panel, 3, "Colour separation", Save.Aberration, v => Save.Aberration = v, UI.Purple);
            SettingSlider(panel, 4, "Vignette pulses", Save.VignettePulses, v => Save.VignettePulses = v, UI.Red);
            if (UI.Button(new Rect(panel.x + 28, panel.y + 381, 224, 40), "REDUCED EFFECTS", UI.Green, 17)) Save.ReducedEffects();
            if (UI.Button(new Rect(panel.x + 266, panel.y + 381, 224, 40), "STANDARD", UI.Blue, 17)) Save.StandardEffects();
            UI.Text(new Rect(panel.x + 28, panel.y + 432, 460, 54), "Threat outlines, expanding ground markers, hit words and health remain. Scene fades, static bloom and ordinary combat timing remain.", 15, Soft, TextAnchor.UpperLeft, false);
        }
        UI.Text(new Rect(frame.x + 18, frame.yMax - 80, frame.width - 36, 22), ControlGuide.Navigation, 12, Soft);
        if (UI.Button(UI.Centered(cx, frame.yMax - 36, 220, 44), "DONE", UI.Red))
        {
            Bindings.Cancel();
            showSettings = false;
            if (State == GameState.Paused) pauseTab = 0;
            Save.Flush();
        }
    }

    static readonly string[] SettingsPages = { "SOUND / DISPLAY", "CAMERA", "EFFECTS", "CONTROLS", "ACCESS" };

    // U02: every rebindable action with its key and controller button. Choosing a cell listens for
    // the next key or button; a binding already in use swaps over, so nothing is ever left unbound.
    void DrawControlSettings(Rect panel)
    {
        float top = panel.y + 104f;
        UI.Text(new Rect(panel.x + 28, top, 190, 24), "Action", 15, UI.Gold, TextAnchor.MiddleLeft);
        UI.Text(new Rect(panel.x + 228, top, 170, 24), "Keyboard", 15, UI.Gold);
        UI.Text(new Rect(panel.x + 410, top, 200, 24), "Controller", 15, UI.Gold);
        bool pad = GameInput.ControllerConnected;
        for (int i = 0; i < Bindings.Actions.Length; i++)
        {
            var action = Bindings.Actions[i];
            float rowY = top + 24 + i * 27;
            UI.Text(new Rect(panel.x + 28, rowY, 190, 25), Bindings.Names[i], 15, Color.white, TextAnchor.MiddleLeft);
            bool keyListening = Bindings.Listening == i && !Bindings.ListeningPad, padListening = Bindings.Listening == i && Bindings.ListeningPad;
            string key = keyListening ? "press a key..." : Bindings.KeyLabel(action);
            string button = padListening ? "press a button..." : Bindings.PadLabel(action);
            if (UI.Button(new Rect(panel.x + 228, rowY, 170, 25), key, keyListening ? UI.Gold : UI.Blue, 14)) Bindings.Listen(action, false);
            if (UI.Button(new Rect(panel.x + 410, rowY, 200, 25), button, padListening ? UI.Gold : UI.Green, 14, pad)) Bindings.Listen(action, true);
        }
        float below = top + 24 + Bindings.Actions.Length * 27 + 2;
        UI.Text(new Rect(panel.x + 28, below, panel.width - 56, 20), Bindings.LastMessage.Length > 0 ? Bindings.LastMessage
            : "Fixed: move, look, menus, pause, Z / V and D-pad sides (targets), mouse buttons, Ctrl (block) and I (status).", 12, Soft);
        if (UI.Button(new Rect(panel.x + 200, below + 22, 240, 28), "RESET CONTROLS", UI.Red, 14, !Bindings.IsDefault)) Bindings.ResetDefaults();
    }

    // U03: hold or toggle, timing room and the fight-focus HUD. All of it is disclosed, none of it
    // changes damage, rewards or enemies.
    void DrawAccessSettings(Rect panel)
    {
        float top = panel.y + 116f;
        AccessRow(panel, top, "Guard", Save.GuardToggle ? "Toggle (press once)" : "Hold", () => Save.GuardToggle = !Save.GuardToggle);
        AccessRow(panel, top + 50, "Sprint", Save.SprintToggle ? "Toggle (press once)" : "Hold", () => Save.SprintToggle = !Save.SprintToggle);
        AccessRow(panel, top + 100, "Timing assist", Save.TimingAssist ? "On" : "Off", () => Save.TimingAssist = !Save.TimingAssist);
        AccessRow(panel, top + 150, "Fight focus HUD", Save.CombatFocusHud ? "On" : "Off", () => Save.CombatFocusHud = !Save.CombatFocusHud);
        if (UI.Button(new Rect(panel.x + 270, top + 200, 300, 34), "REPLAY CONTROL TIPS", UI.Blue, 15)) { hintStep = 0; hintT = 0f; ToonKit.Sfx("click"); }
        UI.Text(new Rect(panel.x + 40, top + 250, panel.width - 80, 170),
            "Toggle guard: one press raises the guard and the next lowers it; a dodge or a spent deflect drops it.\n" +
            "Toggle sprint: every dash carries into a sprint until you stop; in town one press runs, another walks.\n" +
            "Timing assist widens the parry window " + Mathf.RoundToInt((Access.AssistParry - 1f) * 100f) + "% and holds a pressed move for " +
            Access.AssistBufferSeconds.ToString("0.00") + " s instead of " + Access.BufferSeconds.ToString("0.00") + " s. Damage, rewards and enemies are unchanged; the HUD shows ASSIST while it is on.\n" +
            "Fight focus fades the commission, daily and clock panels while enemies are engaged.", 13, Soft, TextAnchor.UpperLeft, false);
    }

    void AccessRow(Rect panel, float y, string label, string value, System.Action flip)
    {
        UI.Text(new Rect(panel.x + 40, y, 220, 34), label, 18, Color.white, TextAnchor.MiddleLeft);
        if (UI.Button(new Rect(panel.x + 270, y, 300, 34), value, UI.Green, 16)) { flip(); ToonKit.Sfx("click"); PlaytestLog.Emit("access", note: label); }
    }
}
