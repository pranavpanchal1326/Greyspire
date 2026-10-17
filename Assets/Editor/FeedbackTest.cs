using System;
using System.Collections.Generic;
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

// Real gamepad state events drive the production IMGUI and story callbacks.
// Layout fixtures control the destination, never invoke the selected action.
[InitializeOnLoad]
public static partial class FeedbackTest
{
    const string Session = "GreyspireFeedbackTest";
    const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    class Step { public float wait; public Action action; }
    static List<Step> steps;
    static int at, passed;
    static double next, deadline;
    static readonly List<string> errors = new List<string>();
    static readonly StringBuilder routes = new StringBuilder("context,control,enabled,slider,x,y,width,height\n");
    static Game G => Game.I;
    static PlayerController P => G.Player;
    static object Get(object host, string name) => host.GetType().GetField(name, Any).GetValue(host);
    static void Set(object host, string name, object value) => host.GetType().GetField(name, Any).SetValue(host, value);
    static object Call(object host, string name, params object[] args) => host.GetType().GetMethod(name, Any).Invoke(host, args);
#if ENABLE_INPUT_SYSTEM
    static Gamepad pad;
    static Keyboard keyboard;
    static void Pad(params GamepadButton[] buttons)
    {
        var state = new GamepadState(); foreach (var button in buttons) state = state.WithButton(button);
        InputSystem.QueueStateEvent(pad, state);
    }
    static void Direction(Vector2 value) => Pad(value.x < 0 ? GamepadButton.DpadLeft : value.x > 0 ? GamepadButton.DpadRight : value.y > 0 ? GamepadButton.DpadUp : GamepadButton.DpadDown);
#endif
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); passed++; Debug.Log("FEEDBACK PASS " + message); }
    static void Do(float delay, Action action) => steps.Add(new Step { wait = delay, action = action });
    static void OnLog(string message, string stack, LogType type) { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message); }
    static FeedbackTest() { if (SessionState.GetBool(Session, false)) { EditorApplication.update += Tick; Application.logMessageReceived += OnLog; } }
    public static void Run()
    {
        try
        {
            AudioAssetAudit.Validate();
            Check(AudioAssetAudit.MissingSlots(new[] { 1, 3, 5 }).SequenceEqual(new[] { 2, 4 }), "numbered gaps are disclosed independently of later valid takes");
            Check(GameAudio.Catalog.Select(c => c.id).Distinct().Count() == GameAudio.Catalog.Length, "cue catalog identities are unique");
            Save.Wipe(); Save.SeenIntro = true; Save.TowerFloor = 2; Save.Level = 20;
            Save.ReducedEffects();
            Check(Save.CameraMotion == 0 && Save.CameraAuto == 0 && FeedbackPolicy.Flash(1) == 0 && FeedbackPolicy.Impact(1) == 0 && FeedbackPolicy.Lines(1) == 0 && FeedbackPolicy.Aberr(1) == 0 && FeedbackPolicy.Pulse(1) == 0, "reduced preset gates every screen moment and camera motion");
            Save.Flashes = 4f; Save.MotionLines = -1f;
            Check(Save.Flashes == 1f && Save.MotionLines == 0f, "individual comfort preferences remain bounded");
            Save.ReducedEffects(); Save.Wipe(); JourneyStore.ReloadForTest(); JourneyStore.Ensure();
            Check(Save.Flashes == 0 && Save.CameraAuto == 0 && Save.QuietMix, "new journey and reload retain comfort preferences");
            Save.SeenIntro = true; Save.TowerFloor = 2; Save.Level = 20; Save.StatPoints = 5; Save.Sparks = 500; Save.Flush();
            SessionState.SetInt(Session + "Passed", passed); SessionState.SetBool(Session, true);
            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            EditorApplication.update -= Tick; EditorApplication.update += Tick; EditorApplication.EnterPlaymode();
        }
        catch (Exception e) { Debug.LogError("FEEDBACK FAILED " + e); EditorApplication.Exit(1); }
    }
    static void State(Game.GameState state)
    {
        Call(G, "SetState", state); Set(G, "fade", 0f); Set(G, "fadeDir", 0f); Set(G, "introT", 0f);
        Set(G, "showSettings", false); Set(G, "showCredits", false); Set(G, "confirmNew", false); Set(G, "showMap", false);
        Set(G, "hubPanel", 0); Set(G, "pauseTab", 0); Set(G, "askPick", null); Set(G, "endingChoice", false);
        UiNavigation.Reset();
    }
    static void Route(string name)
    {
        var controls = UiNavigation.Controls;
        Check(controls.Count > 0 && controls.Any(c => c.enabled) && controls.All(c => c.rect.width > 0 && c.rect.height > 0), name + " registers actual usable controls");
        Check(controls.Where(c => c.enabled).All(c => c.rect.x >= 0 && c.rect.xMax <= UI.W + 1 && c.rect.y >= 0 && c.rect.yMax <= UI.H + 1), name + " controls fit the current screen");
        foreach (var c in controls) routes.AppendLine(name + ",\"" + c.label.Replace("\"", "\"\"") + "\"," + c.enabled + "," + c.slider + "," + c.rect.x + "," + c.rect.y + "," + c.rect.width + "," + c.rect.height);
    }
    static void AudioProof()
    {
        var firstTake = Resources.Load<AudioClip>("Audio/hit_1"); var laterTake = Resources.Load<AudioClip>("Audio/hit_3");
        var gapTakes = GameAudio.DiscoverTakes("gap-proof", path => path.EndsWith("_1") ? firstTake : path.EndsWith("_3") ? laterTake : null);
        Check(gapTakes.Length == 2 && gapTakes[1] == laterTake, "production discovery still loads take three when take two is missing");
        foreach (var cue in GameAudio.Catalog.Where(c => c.requirement != CueRequirement.Optional)) Check(GameAudio.Takes(cue.id).Length > 0, "recorded " + cue.id + " available at runtime");
        GameAudio.StopWorld();
        for (int i = 0; i < 8; i++) GameAudio.Play("hit", position: P.transform.position + new Vector3(i + 1, 1, 4));
        for (int i = 0; i < 4; i++) GameAudio.Play("roar", position: P.transform.position + new Vector3(i + 1, 1, 6));
        Check(GameAudio.WorldVoiceCount == 12 && GameAudio.ActiveThreats == 4, "crowded contact leaves four reserved spatial threat voices");
        int stolen = GameAudio.Stolen;
        GameAudio.Play("fuse", position: P.transform.position + new Vector3(8, 1, 6));
        Check(GameAudio.ActiveThreats == 5 && GameAudio.Stolen == stolen + 1, "a further threat evicts contact rather than disappearing");
        var threat = GameAudio.LastSource; int threats = GameAudio.ActiveThreats;
        for (int i = 0; i < 20; i++) GameAudio.Play("step", position: P.transform.position + new Vector3(i * 2, 1, 8));
        Check(GameAudio.ActiveThreats == threats && GameAudio.Dropped > 0, "footstep spam cannot evict stronger world cues");
        Check(threat.spatialBlend == 1 && threat.dopplerLevel == 0 && threat.maxDistance == 32 && threat.priority == 0, "threat source carries world origin, attenuation and semantic priority");
        GameAudio.Play("click");
        Check(GameAudio.LastSource.spatialBlend == 0 && GameAudio.UiVoiceCount == 4 && GameAudio.ActiveThreats == threats, "UI acknowledgement uses its independent flat pool");
        GameAudio.StopWorld();
        Vector3 from = G.Cam.transform.position, to = from + G.Cam.transform.forward * 6;
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.transform.position = Vector3.Lerp(from, to, 0.5f); wall.transform.localScale = Vector3.one * 2f; Physics.SyncTransforms();
        Check(GameAudio.Play("roar", position: to) && GameAudio.LastSource.GetComponent<AudioLowPassFilter>().cutoffFrequency == 3000, "geometry filters a covered world threat without muting it");
        UnityEngine.Object.DestroyImmediate(wall); GameAudio.StopWorld();
        GameAudio.Play("roar", 1f, position: from + Vector3.up);
        Check(GameAudio.LastSource.volume <= 0.36f, "quieter mix limits peak cue gain");
        GameAudio.StopWorld(); Check(GameAudio.ActiveThreats == 0, "scene cleanup stops voices from the previous world");
        var score = (Music)Get(G, "music"); score.enabled = false;
        var hashes = new HashSet<int>();
        for (int i = 1; i <= 3; i++)
        {
            var bed = score.BedClip(i); var drive = score.DriveClip(i);
            Check(bed.samples == drive.samples && bed.frequency == drive.frequency && Math.Abs(bed.length - 8f) < 0.001f, Music.ScoreId(i) + " paired stems share the exact loop grid");
            float[] a = new float[bed.samples], b = new float[drive.samples]; bed.GetData(a, 0); drive.GetData(b, 0);
            Check(a.Any(x => Math.Abs(x) > 0.02f) && b.Any(x => Math.Abs(x) > 0.02f) && !a.SequenceEqual(b), Music.ScoreId(i) + " combat adds different musical material");
            Check(a.All(x => Math.Abs(x) <= 1) && b.All(x => Math.Abs(x) <= 1), Music.ScoreId(i) + " source stems avoid clipped samples");
            hashes.Add(a.Where((x, n) => n % 79 == 0).Aggregate(17, (h, x) => unchecked(h * 31 + x.GetHashCode())));
            WriteWav("Reports/" + Music.ScoreId(i) + "-preview.wav", a.Zip(b, (x, z) => x * 0.35f + z * 0.3f).ToArray(), bed.frequency);
        }
        Check(hashes.Count == 3, "battle scene scores carry three distinct authored identities");
        Check(Music.TransitionGain(9, 10) == 0 && Music.TransitionGain(10.5, 10) == 0.5f && Music.TransitionGain(12, 10) == 1, "score transition gains are bounded before, during and after the DSP bar");
        score.SetTrack(1); score.TickMix(AudioSettings.dspTime, 0.2f);
        Check(score.Transitioning && score.RequestedTrack == 1, "runtime score schedules a scene change through the second bank");
        score.intensity = 1f; score.enabled = true;
    }
    static void WriteWav(string file, float[] data, int rate)
    {
        using (var stream = File.Create(file)) using (var writer = new BinaryWriter(stream))
        {
            writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + data.Length * 2); writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
            writer.Write(16); writer.Write((short)1); writer.Write((short)1); writer.Write(rate); writer.Write(rate * 2); writer.Write((short)2); writer.Write((short)16);
            writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(data.Length * 2);
            foreach (float sample in data) writer.Write((short)Mathf.RoundToInt(Mathf.Clamp(sample, -1, 1) * 32767));
        }
    }
    static void Script()
    {
#if ENABLE_INPUT_SYSTEM
        pad = InputSystem.AddDevice<Gamepad>(); keyboard = InputSystem.AddDevice<Keyboard>();
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#else
        throw new Exception("Feedback acceptance requires the installed Input System.");
#endif
        Application.runInBackground = true; QualitySettings.vSyncCount = 0; Application.targetFrameRate = 60;
        passed = SessionState.GetInt(Session + "Passed", 0);
        G.enabled = P.enabled = false; Call(G, "BuildHub"); Call(G, "SnapResidents"); P.NewRun(); P.Revive(new Vector3(0, 0.1f, -5), true);
        G.gameObject.AddComponent<FeedbackTestOverlay>(); AudioProof(); State(Game.GameState.Menu);
#if ENABLE_INPUT_SYSTEM
        Pad(GamepadButton.North);
        Do(0.25f, () => { Route("title"); Check(GameInput.UsingGamepad && ControlGuide.Key(ControlAction.Lock).Contains("stick"), "gamepad activity selects shared controller help"); Direction(Vector2.down); });
        Do(0.2f, () => { Check(UiNavigation.FocusedLabel == "NEW GAME", "D-pad moves title focus to new journey"); Pad(); });
        Do(0.1f, () => Pad(GamepadButton.South));
        Do(0.2f, () => { Check((bool)Get(G, "confirmNew") && Save.TowerFloor == 2, "title confirmation opens without erasing progress"); Check(UiNavigation.FocusedLabel == "KEEP MY JOURNEY", "destructive confirmation defaults to keeping the journey"); Route("new journey confirmation"); });
        Do(0.15f, () => { Check(Save.TowerFloor == 2 && (bool)Get(G, "confirmNew"), "held confirm cannot bleed into the next screen"); Pad(); });
        Do(0.1f, () => Pad(GamepadButton.South));
        Do(0.2f, () => { Check(!(bool)Get(G, "confirmNew") && Save.TowerFloor == 2, "controller cancels destructive confirmation through the safe default"); Pad(); State(Game.GameState.Menu); Set(G, "showSettings", true); });
        Do(0.25f, () => { Route("sound settings"); Direction(Vector2.right); });
        Do(0.2f, () => { Check(UiNavigation.FocusedLabel == "CAMERA", "horizontal focus reaches the camera page"); Pad(); });
        Do(0.1f, () => Pad(GamepadButton.South));
        Do(0.2f, () => { Check((int)Get(G, "settingsPage") == 1, "controller selects the camera settings page"); Pad(); });
        Do(0.15f, () => { Route("camera settings"); Pad(); });
        Do(0.2f, () => { Check(UiNavigation.FocusedLabel == "CAMERA", "fresh page focus returns to a stable tab"); Pad(); });
        Do(0.1f, () => Direction(Vector2.right));
        Do(0.2f, () => { Check(UiNavigation.FocusedLabel == "EFFECTS", "controller reaches effect controls"); Pad(); });
        Do(0.1f, () => Pad(GamepadButton.South));
        Do(0.2f, () => { Check((int)Get(G, "settingsPage") == 2, "effect page opens through controller focus"); Pad(); });
        Do(0.15f, () => { Route("effect settings"); Set(G, "settingsPage", 0); });
        Do(0.1f, () =>
        {
            var score = (Music)Get(G, "music");
            Check(score.ActiveTrack == 1 && !score.Transitioning && score.GetComponents<AudioSource>().Count(s => s.isPlaying && s.volume > 0) == 2, "real DSP playback completes the crossfade with both battle stems audible");
        });
        Do(0.25f, () => Direction(Vector2.down));
        Do(0.2f, () => { Check(UiNavigation.FocusedLabel == "Master sound", "D-pad reaches a setting slider"); Pad(); });
        Do(0.1f, () => { Save.Volume = 0.5f; Direction(Vector2.right); });
        Do(0.2f, () => { Check(Math.Abs(Save.Volume - 0.55f) < 0.001f, "one controller step adjusts a slider once across GUI events"); Pad(GamepadButton.East); });
        Do(0.2f, () => { Check(!(bool)Get(G, "showSettings"), "controller back closes title settings"); Pad(); State(Game.GameState.Menu); Set(G, "showCredits", true); });
        Do(0.25f, () => { Route("credits"); Pad(GamepadButton.East); });
        Do(0.2f, () => { Check(!(bool)Get(G, "showCredits"), "controller back closes credits"); Pad(); State(Game.GameState.Shrine); Set(G, "backTo", Game.GameState.Hub); });
        Do(0.25f, () => { Route("forge"); Pad(GamepadButton.East); });
        Do(0.2f, () => { Check(G.State == Game.GameState.Hub, "controller back leaves forge"); Pad(); State(Game.GameState.Codex); Set(G, "backTo", Game.GameState.Hub); });
        Do(0.25f, () => { Route("echo journal"); Pad(GamepadButton.East); });
        Do(0.2f, () => { Check(G.State == Game.GameState.Hub, "controller back leaves journal"); Pad(); State(Game.GameState.Hub); Call(G, "OpenHubPanel", 1); });
        Do(0.25f, () => { Route("tower gate and recovery"); Call(G, "OpenBoard", "Nix of the Guild"); });
        Do(0.25f, () => { Route("guild commissions"); Set(G, "guildTab", 1); });
        Do(0.25f, () => { Route("hero roster"); Call(G, "OpenBoard", "Lady Aurelia Vane"); });
        Do(0.25f, () => { Route("noble commissions"); Call(G, "OpenHubPanel", 4); });
        Do(0.25f, () => { Route("rest"); State(Game.GameState.Paused); Set(G, "pausedFrom", Game.GameState.Hub); Set(G, "pauseTab", 1); });
        Do(0.25f, () => { Route("status"); Direction(Vector2.down); });
        Do(0.2f, () => { Check(UiNavigation.FocusedLabel == "+", "controller reaches stat allocation"); Pad(); });
        Do(0.1f, () => Pad(GamepadButton.South));
        Do(0.2f, () => { Check(Save.StatPoints == 4 && Enumerable.Range(0, 4).Sum(i => Save.Stat(i)) == 1, "controller stat confirmation spends exactly one durable point"); Pad(GamepadButton.East); });
        Do(0.2f, () => { Check((int)Get(G, "pauseTab") == 0, "back returns from status to pause"); Pad(); });
        Do(0.1f, () => Pad(GamepadButton.East));
        Do(0.2f, () => { Check(G.State == Game.GameState.Hub, "a second back resumes play"); Pad(); Save.RecordFloorClear(0, 5); Call(G, "OpenCards", (Action)(() => Call(G, "SetState", Game.GameState.Hub))); Set(G, "cardsT", 1f); });
        Do(0.25f, () => { Route("Guardian boon"); Direction(Vector2.right); });
        Do(0.2f, () => { var cards = (List<Upgrade>)Get(G, "cards"); Check(UiNavigation.FocusedLabel == cards[1].name, "controller moves horizontally to the second Guardian card"); Pad(); });
        Do(0.1f, () => Pad(GamepadButton.South));
        Do(0.2f, () => { Check(Save.PendingGuardian == null && Save.GuardianPaid(0, 5) && G.State == Game.GameState.Hub, "controller claims one Guardian boon through its durable production callback"); Pad(); });
        Rescue("rhea", 1);
        Rescue("dorn", 0);
        Do(0.2f, () => { State(Game.GameState.Story); Set(G, "endingChoice", true); });
        Do(0.25f, () => { Route("ending choice"); Direction(Vector2.right); });
        Do(0.2f, () => { Check(UiNavigation.FocusedLabel == Story.ChoiceB, "controller can focus the second ending"); Pad(); });
        Do(0.1f, () => Pad(GamepadButton.South));
        Do(0.2f, () => { Check((Save.EndingMask & 2) != 0 && !(bool)Get(G, "endingChoice"), "controller ending choice commits the chosen ending"); Pad(); State(Game.GameState.Results); Set(G, "resultsT", 3f); });
        Do(0.25f, () => { Route("results"); State(Game.GameState.Hub); P.gameObject.SetActive(true); Pad(GamepadButton.North); });
        Do(0.2f, () => { Check(GameInput.UsingGamepad, "controller prompts remain active before disconnect"); InputSystem.RemoveDevice(pad); });
        Do(0.2f, () => { Check(G.State == Game.GameState.Paused && (bool)Get(G, "controllerLost"), "disconnect pauses roaming and provides a recovery message"); pad = InputSystem.AddDevice<Gamepad>(); Pad(); });
        Do(0.2f, () => { Check(!(bool)Get(G, "controllerLost") && G.State == Game.GameState.Paused, "reconnect clears warning without silently resuming"); InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Enter)); });
        Do(0.2f, () => { Check(!GameInput.UsingGamepad && ControlGuide.Key(ControlAction.Lock) == "T", "keyboard activity restores matching prompts"); InputSystem.QueueStateEvent(keyboard, new KeyboardState()); Pad(GamepadButton.North); });
        Do(0.2f, () => { State(Game.GameState.Menu); Set(G, "showSettings", true); Set(G, "settingsPage", 2); P.Revive(new Vector3(4, 0.1f, 5), true); G.Cam.Face(0); G.Cam.Snap(); });
        Do(0.25f, () => { ScreenCapture.CaptureScreenshot("Logs/feedback-effects.png"); File.WriteAllText("Reports/controller-routes.csv", routes.ToString()); });
        Do(0.5f, () => RenderProof());
        Do(0.5f, () =>
        {
            var material = (Material)Get(Get(G, "post"), "mat");
            Check(material != null && material.GetFloat("_Impact") == 0 && material.GetFloat("_Lines") == 0 && material.GetFloat("_Aberr") == 0, "real rendered post material suppresses the strongest burst at zero settings");
            Check(Math.Abs(G.Cam.GetComponent<Camera>().fieldOfView - Save.CameraFov) < 0.1f, "strongest burst leaves the configured FOV steady at camera motion zero");
            Check(File.Exists("Logs/feedback-effects.png") && File.Exists("Logs/feedback-reduced.png"), "controller settings and reduced-effect scene captures were produced");
            Check(errors.Count == 0, "feedback scenarios contain no unexpected runtime errors" + (errors.Count == 0 ? "" : ": " + errors[0]));
            Check(JourneyStore.Error == null, "feedback checks leave journey storage healthy");
        });
        Chapter();
#endif
    }
#if ENABLE_INPUT_SYSTEM
    static void Rescue(string id, int branch)
    {
        Do(0.2f, () => { State(Game.GameState.Hub); Save.BeginRescue(id); Call(G, "ResumeRescue", Story.RescueIndex(id), (Action)(() => Call(G, "SetState", Game.GameState.Hub))); });
        Do(0.35f, () => Pad(GamepadButton.North));
        Do(0.2f, () => { Check(G.Asking, id + " controller whole-dialogue skip reaches the mandatory choice"); Route(id + " rescue choice"); Pad(); });
        if (branch == 1)
        {
            Do(0.1f, () => Direction(Vector2.right));
            Do(0.2f, () => { Check(UiNavigation.FocusedLabel == "WHAT DID YOU SEE?", "D-pad focuses the information branch"); Pad(); });
        }
        Do(0.1f, () => Pad(GamepadButton.South));
        Do(0.2f, () => { Check(Save.RescueChoice(id) == branch + 1 && !G.Asking, id + " controller choice commits the intended branch once"); Pad(); });
        Do(0.35f, () => Pad(GamepadButton.North));
        Do(0.2f, () => { Check(!Save.RescuePending(id) && G.State == Game.GameState.Hub, id + " controller skip completes the return callback"); Pad(); });
    }
#endif
    static void RenderProof()
    {
        Save.ReducedEffects(); State(Game.GameState.Playing); Set(G, "level", 0); Set(G, "wave", 0); Call(G, "BuildFloor", 0, 0);
        P.NewRun(); P.Revive(new Vector3(0, 0.1f, -37), true); G.Cam.EndCine(); G.Cam.Face(0); G.Cam.Snap();
        G.Flash(Color.white, 1); G.Impact(1); G.Lines(1); G.Cam.Shake(1); G.Cam.Kick(12); G.Cam.Punch(Vector3.forward, 2);
        Call(P, "ColourBurst");
        var post = (PostFx)Get(G, "post");
        Check(post.impact > 0 && FeedbackPolicy.Impact(post.impact) == 0 && FeedbackPolicy.Lines(post.lines) == 0 && FeedbackPolicy.Aberr(post.aberration) == 0, "actual strongest burst retains intent while render policy suppresses all selected moments");
        ScreenCapture.CaptureScreenshot("Logs/feedback-reduced.png");
    }
    public static void DrawUi() { if (G != null && !G.enabled) Call(G, "OnGUI"); }
    public static void DriveInput()
    {
        ChapterInput();
        if (G == null || G.enabled) return;
        GameInput.PollDevice(); Call(G, "TickControlAccess");
        if (G.State == Game.GameState.Story) { Call(G, "TickDialog", Time.unscaledDeltaTime); Call(G, "TickStory"); }
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || G == null || G.Player == null) return;
        try
        {
            if (next == 0) { next = EditorApplication.timeSinceStartup + 2; return; }
            if (EditorApplication.timeSinceStartup < next) return;
            if (steps == null) { steps = new List<Step>(); Script(); deadline = EditorApplication.timeSinceStartup + 150; next = EditorApplication.timeSinceStartup + steps[0].wait; return; }
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("feedback scenario deadline exceeded");
            steps[at++].action();
            if (at < steps.Count) { next = EditorApplication.timeSinceStartup + steps[at].wait; return; }
            Debug.Log("FEEDBACK DONE " + passed + " checks passed"); SessionState.SetBool(Session, false); EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogError("FEEDBACK FAILED step=" + at + " " + e); SessionState.SetBool(Session, false); EditorApplication.Exit(1); }
    }
}
public class FeedbackTestOverlay : MonoBehaviour
{
    void OnGUI() => FeedbackTest.DrawUi();
    void Update() => FeedbackTest.DriveInput();
}
