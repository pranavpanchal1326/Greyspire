using System;
using System.Collections.Generic;
using UnityEngine;

// The flow of the game: menu -> Brushwick -> the tower, one floor at a time
// (explore, clear the monsters, the stairs open; a Guardian every fifth floor)
// -> the Gloom King -> an ending. Progress up the tower is kept: a fall sends the
// hero home, and the next climb starts on the floor that beat him.
// Floors are built in GameFloors.cs, the boss room in GameLevel.cs, drawing in GameUI.cs.
public partial class Game : MonoBehaviour
{
    public enum GameState { Menu, Shrine, Story, Playing, Cards, Paused, Results, Codex, Hub }

    public const float ArenaRadius = 22f;
    public static readonly Vector3 StartPos = new Vector3(0f, 0.1f, -6f);
    const float FloorGrey = 0.22f;      // how much colour a floor has left before anyone fights for it

    public static Game I { get; private set; }

    public GameState State { get; private set; } = GameState.Menu;
    public PlayerController Player { get; private set; }
    public ThirdPersonCamera Cam { get; private set; }
    public Transform LevelRoot { get; private set; }
    public int Gloom { get; private set; }
    public bool InTown { get; private set; }        // Brushwick: weapons stay sheathed
    public bool DefeatPending { get; private set; }
    bool defeatWasTowerFight;

    class Timer
    {
        public float t;
        public Action act;
    }

    class Number
    {
        public Vector3 pos;
        public string text;
        public Color color;
        public float age;
        public int size;
    }

    readonly List<Timer> timers = new List<Timer>();
    readonly List<Number> numbers = new List<Number>();

    // The visit to the tower (or the Wilds) in progress
    int level, wave = -1, pending, floorTotal;
    int coins { get => Save.CarriedSparks; set => Save.CarriedSparks = value; }
    int kills, combo, maxCombo, sparksBanked, sparksLost, resultsFloor;
    float comboT, playTime, sat = 1f, satTarget = 1f;
    bool waveActive, runWon, gloomUnlocked, clearPending;

    // Dialogue
    Line[] dialog;
    Action dialogDone;
    int dialogIndex, lastTalk;
    float dialogChars, dialogT;

    // Guardian's Boon cards
    List<Upgrade> cards;
    Action cardsThen;
    float cardsT;
    int rerollCost;

    // Screen effects and HUD animation
    float fade = 1f, fadeDir = -1f, flashA, hurtA, bannerT, bannerDur, toastT, resultsT;
    float shownCoins, heartPunch, comboPunch, coinPunch, lastCoinT, hintT;
    int coinChain, hintStep = -1;
    public const float FocusFadeLevel = 0.2f;
    public float FocusFade { get; private set; } = 1f;
    // U01: in a fight the commission, daily and clock panels step back; health, foes and prompts stay.
    void TickFocus(bool fighting, float udt) =>
        FocusFade = Mathf.MoveTowards(FocusFade, fighting && Save.CombatFocusHud ? FocusFadeLevel : 1f, Mathf.Max(0f, udt) * 2.5f);
    Color flashC = Color.white, toastC = Color.white;
    string banner = "", bannerSub = "", toast = "", defeatLine = "";
    Action fadeAct;
    bool showSettings, confirmNew, showCredits;
    string startupError;
    int heroView = -1, endingSeen = -1, pauseTab, resumeFrame;
    GameState pausedFrom = GameState.Playing;
    bool endingChoice, awakening, prologueDue;
    float awakeningT;

    Light sun;
    Camera cam;
    Music music;
    Ambience ambienceBed;
    public Ambience WorldAmbience => ambienceBed;

    // ------------------------------------------------------------ setup

    void Awake()
    {
        I = this;
        try
        {
            ValidateJourneyContent();
            JourneyStore.Ensure();
            Save.BankCarried(); // Interrupted expeditions return home with their committed purse.
        }
        catch (Exception e) { startupError = e.Message; Debug.LogError("SAVE LOAD FAILED " + e); return; }
        // The HUD lays nothing out automatically, so the layout pass (a second run of all the
        // drawing code every frame) can be skipped outright.
        useGUILayout = false;
        QualitySettings.shadowDistance = 70f;
        QualitySettings.antiAliasing = 4;
        QualitySettings.shadowResolution = ShadowResolution.VeryHigh;
        QualitySettings.shadowCascades = 4;
        QualitySettings.shadows = ShadowQuality.All;
        QualitySettings.skinWeights = SkinWeights.FourBones;
        QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
        QualitySettings.vSyncCount = 1;
        Application.targetFrameRate = 120;
        AudioListener.volume = Save.Volume;
        Shader.SetGlobalFloat("_WorldSat", 1f);

        // Own generated sunlight; authored scene lighting survives startup.
        var sunGo = new GameObject("Sun");
        sunGo.transform.SetParent(transform, false);
        sun = sunGo.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 0.75f;
        sun.shadowBias = 0.03f;
        sun.shadowNormalBias = 0.25f;
        sunGo.transform.rotation = Quaternion.Euler(48f, -35f, 0f);

        cam = Camera.main;
        if (cam == null)
        {
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            cam = camGo.AddComponent<Camera>();
        }
        if (FindAnyObjectByType<AudioListener>() == null) cam.gameObject.AddComponent<AudioListener>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.fieldOfView = 60f;
        cam.farClipPlane = 800f;
        cam.allowHDR = true;
        post = cam.gameObject.AddComponent<PostFx>();
        ApplyQuality();
        Cam = cam.gameObject.AddComponent<ThirdPersonCamera>();

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = 40f;
        RenderSettings.fogEndDistance = 130f;

        music = new GameObject("Music").AddComponent<Music>();
        ambienceBed = music.gameObject.AddComponent<Ambience>();
        FloorCatalog.Validate();
        Upgrades.Reset();
        LoadClock();

        var playerGo = new GameObject("Player");
        Player = playerGo.AddComponent<PlayerController>();
        Cam.target = playerGo.transform;

        BuildLevel(0);
        Player.Revive(StartPos, true);
        Lumi.Create(playerGo.transform);
        Companion.Ensure();
        Cam.Snap();
        music.SetTrack(0);
        SetState(GameState.Menu);
        if (!Application.isEditor) CheckShotArgument();
    }

    // Closing the window mid-journey keeps the journey: the clock and the purse are written out.
    void OnApplicationQuit()
    {
        if (Player == null || JourneyStore.Error != null) return;
        SaveClock();
        Save.BankCarried();
        Save.Flush();
    }

    // Looking away from the game pauses it (in the built game; the editor's test runs unattended).
    void OnApplicationFocus(bool focused)
    {
        if (focused || Application.isEditor || fadeDir != 0f || Intro) return;
        if (State == GameState.Playing || State == GameState.Hub || State == GameState.Story) OpenPause(0);
    }

    // For checking a build without anyone at the keyboard: "-shot <file> <seconds>" saves a
    // picture of the screen after that long and quits.
    void CheckShotArgument()
    {
        string[] args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i + 2 < args.Length; i++)
        {
            if (args[i] != "-shot" || !float.TryParse(args[i + 2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float wait)) continue;
            StartCoroutine(ShotThenQuit(args[i + 1], wait));
            return;
        }
    }

    System.Collections.IEnumerator ShotThenQuit(string file, float wait)
    {
        yield return new WaitForSecondsRealtime(wait);
        ScreenCapture.CaptureScreenshot(file);
        yield return new WaitForSecondsRealtime(1.5f);
        Application.Quit();
    }

    // Graphics quality: 2 high (everything), 1 medium (no ambient occlusion, lighter anti-aliasing), 0 low.
    public void ApplyQuality()
    {
        int q = Save.Quality;
        QualitySettings.antiAliasing = q == 2 ? 4 : q == 1 ? 2 : 0;
        QualitySettings.shadowResolution = q == 0 ? ShadowResolution.Medium : ShadowResolution.VeryHigh;
        QualitySettings.shadowCascades = q == 0 ? 2 : 4;
        QualitySettings.shadowDistance = q == 0 ? 45f : 70f;
        post.occlusion = q == 2 ? 0.9f : 0f;
        post.grain = q == 0 ? 0f : 0.022f;
    }

    public bool StorageBlocked => JourneyStore.Error != null || startupError != null;

    void SetState(GameState s)
    {
        bool play = s == GameState.Playing || s == GameState.Hub;
        // Whatever button closed the last screen is not also a sword swing.
        if (play && State != s) resumeFrame = Time.frameCount;
        if (State != s) PlaytestLog.Emit("screen", note: s.ToString());
        State = s;
        Juice.Paused = s == GameState.Paused;
        Cursor.lockState = play ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !play;
    }

    // Fade to black, run the action while hidden, fade back in.
    void Transition(Action act)
    {
        if (fadeAct != null) return;
        fadeAct = act;
        fadeDir = 1f;
    }

    void After(float seconds, Action act)
    {
        timers.Add(new Timer { t = seconds, act = act });
    }

    // ------------------------------------------------------------ the climb

    void StartRun()
    {
        StartClimb();
    }

    // Into the tower, at the floor after the last one cleared. There is no other way in.
    void StartClimb()
    {
        if (InTown && ResumeRewards()) return;
        Transition(() =>
        {
            if (InWilds)
            {
                // Walking into the tower from the Wilds banks what you were carrying.
                Save.BankCarried();
                InWilds = false;
            }
            if (Player.Hero != Heroes.All[Heroes.Selected]) Player.SetHero(Heroes.Selected);
            Save.BeginExpedition(false);
            DefeatPending = false;
            Player.NewRun();
            kills = combo = maxCombo = 0;
            shownCoins = 0f;
            playTime = 0f;
            runWon = gloomUnlocked = false;
            sparksLost = 0;
            ResetStyle();
            Gloom = Save.GloomTier;
            hubPanel = 0;
            int floor = Mathf.Clamp(Save.TowerFloor + 1, 1, TowerFloors);
            level = FloorCatalog.At(floor).theme;
            // First ever climb: Pip has no System yet and can barely scratch a slime.
            awakening = floor == 1 && Save.Level == 1 && Save.Xp == 0;
            Player.weak = awakening ? 0.12f : 1f;
            hintStep = Save.BestFloor == 0 ? 0 : -1;
            EnterFloor(FloorCatalog.At(floor).layout);
        });
    }

    // Builds a floor and puts the hero at the foot of it. Call while the screen is dark.
    void EnterFloor(int index)
    {
        wave = index;
        bool boss = FloorWave(level, index).boss;
        // The top of the tower is not a land to cross. It is one room, and he is in it.
        if (boss) BuildLevel(level);
        else BuildFloor(level, index);
        music.SetTrack(level + 1);
        Player.gameObject.SetActive(true);
        Player.Revive(boss ? StartPos : FloorArrival(FloorCatalog.Legacy(level, index).number), false);
        Player.transform.rotation = Quaternion.identity;
        Cam.Face(0f);
        Pickup.AttractAll = false;
        waveActive = false;
        clearPending = false;
        pending = 0;
        floorTotal = 0;
        timers.Clear();
        numbers.Clear();
        Juice.Reset();
        Cam.Snap();
        sat = satTarget = boss ? 0.3f : FloorGrey;

        // The story of a floor is told once, the first time the hero sets foot on it.
        int floor = Floor;
        PlaytestLog.Emit("floor_start", value: floor, note: FloorCatalog.At(floor).id);
        var lines = new List<Line>();
        if (floor > Save.StoryFloor)
        {
            if (FloorCatalog.At(floor).intro) lines.AddRange(Story.LevelIntro[level]);
            Line[] beat = Story.FloorIntro(floor);
            if (beat != null) lines.AddRange(beat);
            Save.StoryFloor = floor;
        }
        if (boss)
        {
            // The Gloom King is already standing there when the confrontation starts.
            Enemy.Create(Enemy.Kind.Boss, new Vector3(0, 0.1f, 11f), level);
            Player.transform.rotation = Quaternion.LookRotation(Vector3.forward);
            UnlockEcho(4);
            lines.AddRange(Story.BossIntro);
        }

        if (lines.Count == 0)
        {
            SetState(GameState.Playing);
            StartFloor();
            return;
        }
        Say(lines.ToArray(), () =>
        {
            SetState(GameState.Playing);
            StartFloor();
        });
    }

    // The floor's monsters take their places and the fight for it begins.
    void StartFloor()
    {
        Pickup.AttractAll = false;
        waveActive = true;
        if (awakening)
        {
            // One slime. That is the whole fight, and he is losing it.
            awakeningT = 0f;
            Banner("FLOOR 1", 1.6f);
            pending++;
            After(1f, () =>
            {
                pending--;
                Vector3 near = Player.transform.position + new Vector3(3f, 0f, 5f);
                Enemy.Create(Enemy.Kind.Slime, new Vector3(near.x, 0.1f, near.z), 0);
            });
            return;
        }

        Wave w = FloorWave(level, wave);
        ToonKit.Sfx("wave");
        if (w.boss)
        {
            Banner("THE GLOOM KING", 2.5f, "Floor 30  -  the top of the tower");
            Cam.Shake(0.6f);
            ToonKit.Sfx("roar");
            floorTotal = 1;
            return;
        }

        // The floor's monsters are already out there, in packs, holding their ground.
        int floor = Floor;
        if (GuardianFloor(floor)) Banner("FLOOR " + floor, 2.6f, "Guardian floor  -  " + GuardianName(floor));
        else Banner("FLOOR " + floor, 2f, Levels[level].name);
        SpawnPacks(w);
        floorTotal = Enemy.All.Count;
    }

    void FloorCleared()
    {
        if (awakening)
        {
            TriggerAwakening();
            return;
        }
        waveActive = false;
        Pickup.AttractAll = true;
        int floor = Floor;
        if (IsGatekeeperFloor(floor)) PassArena();
        bool first = Save.RecordFloorClear(Gloom, floor);
        PlaytestLog.Emit("floor_cleared", value: floor, note: first ? "first clear" : "revisit");

        // The floor is the hero's now: every colour the Gloom took from it comes back.
        satTarget = 1f;
        bool heart = FloorCatalog.At(floor).heart;
        Banner(heart ? "COLOUR RESTORED!" : "FLOOR CLEARED", heart ? 3f : 2f, first ? "Floor " + floor + " of " + TowerFloors : "");
        Flash(Color.white, heart ? 0.7f : 0.3f);
        ToonKit.Sfx(heart ? "restore" : "wave", 1f, 1.3f);
        Vector3 at = Player.transform.position;
        Fx.Ring(at, UI.Gold, 1f, 16f, 0.9f);
        if (heart)
        {
            Fx.Ring(at, UI.Red, 1f, 30f, 1.2f);
            Fx.Ring(at, UI.Blue, 1f, 22f, 1.5f);
            Fx.Burst(at + Vector3.up * 3f, UI.Gold, 14, 10f, 1.5f);
        }
        UnsealStairs();

        // What the floor had to say, and what its Guardian left behind. The stairs wait for both.
        clearPending = true;
        After(2f, () => ClearScenes(floor, first));
    }

    void ClearScenes(int floor, bool first)
    {
        var lines = new List<Line>();
        // After the third floor, a small blue slime decides to tag along.
        if (floor >= 3 && !Save.Pet)
        {
            Save.Pet = true;
            Companion.Ensure();
            lines.AddRange(Story.PetJoin);
        }
        if (floor > Save.ClearStory)
        {
            Save.ClearStory = floor;
            Line[] beat = Story.FloorClear(floor);
            if (beat != null) lines.AddRange(beat);
            if (FloorCatalog.At(floor).heart && level < Story.LevelOutro.Length)
            {
                // A Colour Heart opened: it gives back what it remembers.
                UnlockEcho(level * 2);
                UnlockEcho(level * 2 + 1);
                lines.AddRange(Story.LevelOutro[level]);
            }
        }
        Save.Flush();

        Action finish = () =>
        {
            clearPending = false;
            SetState(GameState.Playing);
        };
        Action boon = () =>
        {
            if (Save.PendingGuardian != null) OpenCards(finish);
            else finish();
        };
        if (lines.Count > 0) Say(lines.ToArray(), boon);
        else boon();
    }

    public void OnPlayerDied()
    {
        defeatWasTowerFight = !InWilds && waveActive;
        DefeatPending = true;
        // Settle before any defeat animation or delayed callback can be interrupted.
        JourneyStore.Transaction(() =>
        {
            if (defeatWasTowerFight && pending == 0 && Enemy.All.Count == 0) Save.RecordFloorClear(Gloom, Floor);
            Save.SettleExpedition(false, true, Gloom);
        });
        PlaytestLog.Emit("player_defeat", target: Player, value: Save.SettledLost);
        timers.Clear();
        ResetStyle();
        if (InWilds)
        {
            // Out here Mop drags you home, minus half of what you were carrying.
            Juice.Slow(1f);
            ToonKit.Sfx("lose");
            After(1.6f, () =>
            {
                LeaveWilds();
            });
            return;
        }
        // The hero falls on screen before the results come up.
        waveActive = false;
        Juice.Slow(1.2f);
        ToonKit.Sfx("lose");
        After(1.5f, () =>
        {
            Player.gameObject.SetActive(false);
            EndRun(false, true);
        });
    }

    // The visit to the tower is over: beaten the King, or carried out. A fall costs half
    // the Sparks in hand; the floors already cleared stay cleared.
    void EndRun(bool won, bool fell = false)
    {
        runWon = won;
        resultsFloor = Floor;
        Save.SettleExpedition(won, fell, Gloom);
        sparksLost = Save.SettledLost;
        sparksBanked = Save.SettledBanked;
        gloomUnlocked = won && Save.SettledGloomUnlocked;

        defeatLine = Story.DefeatLines[ProductionRandom.Narrative.Range(0, Story.DefeatLines.Length)];
        resultsT = 0f;
        timers.Clear();
        Juice.Reset();
        music.SetTrack(0);
        if (won) ToonKit.Sfx("win");
        SetState(GameState.Results);
    }

    // Walking back out of the tower with everything in hand.
    void LeaveTower()
    {
        Save.BankCarried();
        EnterHub();
    }

    // Is something hunting the hero right now? Nobody walks away from a fight by wishing.
    public bool Threatened => State == GameState.Playing && !awakening && Hunted();

    bool Hunted()
    {
        Vector3 me = Player.transform.position;
        foreach (Enemy e in Enemy.All)
            if (e.Alerted && e.CombatEligible && (e.transform.position - me).sqrMagnitude < 40f * 40f) return true;
        return false;
    }

    void ToMenu()
    {
        Transition(() =>
        {
            SaveClock();
            level = 0;
            InWilds = false;
            hubPanel = 0;
            timers.Clear();
            numbers.Clear();
            Juice.Reset();
            BuildLevel(0);
            sat = satTarget = 1f;
            Player.gameObject.SetActive(true);
            Player.Revive(StartPos, true);
            Cam.Snap();
            music.SetTrack(0);
            showSettings = confirmNew = showCredits = false;
            SetState(GameState.Menu);
        });
    }

    // A fresh journey: the save goes, the settings stay.
    void NewGame()
    {
        Save.Wipe();
        ResetSystem();
        FloorCatalog.Validate();
        Upgrades.Reset();
        LoadClock();
        seenTown = seenWilds = false;
        heroView = -1;
        Player.SetHero(Heroes.Selected);
        Companion.Dismiss();
        EnterHub();
    }

    // ------------------------------------------------------------ dialogue & cards

    void Say(Line[] lines, Action done)
    {
        endingChoice = false;
        ApplyShot(string.IsNullOrEmpty(lines[0].shot) ? "hero" : lines[0].shot);
        dialog = lines;
        dialogDone = done;
        dialogIndex = 0;
        dialogChars = 0f;
        dialogT = 0f;
        lastTalk = 0;
        SetState(GameState.Story);
    }

    void TickDialog(float udt)
    {
        if (dialog == null) return;
        dialogT += udt;
        string text = dialog[dialogIndex].text;
        if (dialog[dialogIndex].who == Story.Title) dialogChars = text.Length;   // chapter cards appear whole
        if (dialogChars < text.Length)
        {
            dialogChars += udt * 52f;
            lastTalk = (int)dialogChars;
        }
        // Play-testers skipped most of the text one line at a time and found it irritating: X ends the
        // whole conversation at once. Whatever the conversation leads to still happens.
        if (dialogT >= 0.2f && fadeDir <= 0f && GameInput.SkipTalkPressed && dialog[dialogIndex].who != Story.Title)
        {
            Cam.EndCine();
            Action skipped = dialogDone;
            dialog = null;
            dialogDone = null;
            skipped?.Invoke();
            return;
        }
        if (dialogT < 0.2f || fadeDir > 0f || !GameInput.ConfirmPressed) return;

        if (dialogChars < text.Length)
        {
            dialogChars = text.Length;
            return;
        }
        dialogIndex++;
        dialogChars = 0f;
        dialogT = 0f;
        if (dialogIndex < dialog.Length)
        {
            ApplyShot(dialog[dialogIndex].shot);
            return;
        }

        Cam.EndCine();
        Action done = dialogDone;
        dialog = null;
        dialogDone = null;
        done?.Invoke();
    }

    // Points the cinematic camera for a line of dialogue.
    void ApplyShot(string shot)
    {
        if (string.IsNullOrEmpty(shot)) return;
        Transform p = Player.transform;
        float face = p.eulerAngles.y;
        Enemy boss = null;
        foreach (Enemy e in Enemy.All) if (e.IsBoss) boss = e;
        if (shot == "boss" && boss == null) shot = "heart";
        if (shot == "them" && shotSubject == null) shot = "hero";

        switch (shot)
        {
            case "them":
                // Whoever the hero is talking to, from a little to one side of where he stands.
                Cam.Cine(shotSubject.position + Vector3.up * 1.45f, shotSubject.eulerAngles.y + 160f, 4f, 3.1f, 0f);
                break;
            case "hero": Cam.Cine(p.position + Vector3.up * 1.3f, face + 205f, 6f, 3.8f, 0f); break;
            case "lumi": Cam.Cine(p.position + Vector3.up * 1.75f + p.right * 0.6f, face + 160f, 2f, 2.8f, 3f); break;
            case "heart": Cam.Cine(new Vector3(0, 3.2f, 0), face + 20f, 10f, 11f, 6f); break;
            case "wide": Cam.Cine(new Vector3(0, 2f, 0), face - 30f, 26f, 30f, 5f); break;
            case "sky": Cam.Cine(p.position + Vector3.up * 5f, face + 180f, -28f, 7f, 2f); break;
            case "low": Cam.Cine(p.position + Vector3.up * 0.9f, face + 150f, -14f, 3.2f, -2f); break;
            case "boss":
                Cam.Cine(boss.transform.position + Vector3.up * 2.6f, boss.transform.eulerAngles.y + 200f, 0f, 8f, 3f);
                break;
        }
    }

    void UnlockEcho(int index)
    {
        if (index < 0 || index >= Story.Echoes.Length || (Save.EchoMask & (1 << index)) != 0) return;
        Save.EchoMask |= 1 << index;
        Save.Flush();
        Toast("ECHO RECOVERED", UI.Gold);
    }

    // The final decision. 0 = shatter the crown, 1 = paint him back.
    void ChooseEnding(int which)
    {
        endingChoice = false;
        endingSeen = which;
        JourneyStore.Transaction(() =>
        {
            Save.EndingMask |= 1 << which;
            Save.EchoMask |= 1 << (5 + which);
            Save.SettleExpedition(true, false, Gloom);
        });
        Flash(which == 0 ? Color.white : UI.Gold, 0.9f);
        ToonKit.Sfx("restore");
        foreach (Enemy e in new List<Enemy>(Enemy.All)) if (which == 0 || !e.IsBoss) e.Vanish();
        Player.Cheer();
        Say(which == 0 ? Story.EndingShatter : Story.EndingPaint, () => EndRun(true));
    }

    // The System arrives: the turning point of the opening fight.
    void TriggerAwakening()
    {
        awakening = false;
        Juice.Slow(0.5f);
        Flash(Story.SystemC, 0.8f);
        Impact(1f);
        ToonKit.Sfx("levelup");
        Fx.Ring(Player.transform.position, Story.SystemC, 0.5f, 10f, 0.8f);
        foreach (Enemy e in Enemy.All) e.Shove(Player.transform.position, 14f);
        Say(Story.Awakening, () =>
        {
            Player.weak = 1f;
            Player.Heal(99);
            SetState(GameState.Playing);
            SystemMessage("SYSTEM ONLINE", "You can now gain experience and level up.");
            CutIn("AWAKENED", Story.SystemC);
            StartFloor();
        });
    }

    // A Guardian fell: the hero takes one boon from it, and keeps it.
    void OpenCards(Action then)
    {
        var offer = Save.PendingGuardian;
        cards = Save.GuardianCards(offer);
        if (cards.Count == 0)
        {
            then();
            return;
        }
        cardsThen = then;
        cardsT = 0f;
        rerollCost = offer.rerollCost;
        SetState(GameState.Cards);
        ToonKit.Sfx("card");
    }

    void PickCard(int index)
    {
        if (cards == null || index < 0 || index >= cards.Count || cardsT < 0.5f) return;
        Upgrade u = cards[index];
        var offer = Save.PendingGuardian;
        if (offer == null || !Save.TakeGuardian(offer.key, u.id)) return;
        FloorCatalog.Validate();
        Upgrades.Reset();
        Player.RefreshStats();
        ToonKit.Sfx(u.rarity == Rarity.Epic ? "epic" : "buy");
        Flash(u.Color, 0.25f);
        Action then = cardsThen;
        cards = null;
        cardsThen = null;
        then?.Invoke();
    }

    void Reroll()
    {
        var offer = Save.PendingGuardian;
        if (offer == null || !Save.RerollGuardian(offer.key, InTown)) return;
        offer = Save.PendingGuardian;
        rerollCost = offer.rerollCost;
        cards = Save.GuardianCards(offer);
        cardsT = 0.2f;
        ToonKit.Sfx("card");
    }

    // ------------------------------------------------------------ gameplay hooks

    public void AddCombo(int hits)
    {
        combo += hits;
        comboT = 3.5f;
        comboPunch = 1f;
        maxCombo = Mathf.Max(maxCombo, combo);
    }

    public void OnEnemyKilled(Enemy e)
    {
        if (e == null || e.Training || !e.gameObject.activeInHierarchy || e.KillRewardsCommitted || e.BirthCampaign != JourneyStore.Campaign) return;
        bool big = e.elite || e.kind == Enemy.Kind.Brute;
        int previousLevel = Save.Level;
        Save.DailyReward daily = default;
        // One authoritative kill produces one disk commit, even when it advances
        // XP, a commission, and a daily reward together.
        JourneyStore.Transaction(() =>
        {
            Save.AwardXp(KillXp(e));
            MissionKill(e);
            daily = DailyKill();
            if (!DefeatPending) coins = checked(coins + GrowthPolicy.StyleSparks(e.coinValue, StyleAfterGain(big ? 45f : 18f)));
            if (DefeatPending && defeatWasTowerFight && !InWilds && pending == 0 && Enemy.All.Count == 0)
                Save.RecordFloorClear(Gloom, Floor);
        });
        e.CommitKillRewards();
        kills++; Player.OnKill(); AddStyle(big ? 45f : 18f);
        PlaytestLog.Emit("kill_reward", source: Player, target: e, value: KillXp(e));
        PresentXp(previousLevel);
        if (daily.complete && !DefeatPending)
        {
            ToonKit.Sfx("epic");
            SystemMessage("DAILY QUEST COMPLETE", (daily.point ? "+1 stat point    " : "") + "+" + daily.sparks + " banked Sparks.\nDaily points: " + Save.DailyGrowth + " / " + GrowthPolicy.DailyPointBudget + " lifetime. Sparks continue each day.");
        }
        if (InWilds || !waveActive) return;

        // Each monster that falls gives a little of the floor's colour back.
        floorTotal = Mathf.Max(floorTotal, Enemy.All.Count + 1);
        satTarget = Mathf.Lerp(FloorGrey, 0.9f, 1f - Enemy.All.Count / (float)floorTotal);
        if (pending == 0 && Enemy.All.Count == 0)
        {
            // The last enemy of a floor goes down in slow motion.
            Juice.Slow(0.8f);
            Cam.Kick(9f);
            Impact(1f);
        }
    }

    public void OnBossDefeated(Enemy boss)
    {
        if (boss == null || !boss.gameObject.activeInHierarchy || boss.KillRewardsCommitted || boss.BirthCampaign != JourneyStore.Campaign) return;
        int previousLevel = Save.Level;
        JourneyStore.Transaction(() =>
        {
            Save.AwardXp(600);
            Save.TowerFloor = TowerFloors;
            Save.BestFloor = Mathf.Max(Save.BestFloor, TowerFloors);
        });
        boss.CommitKillRewards();
        kills++;
        PlaytestLog.Emit("kill_reward", source: Player, target: boss, value: 600, note: "boss");
        PresentXp(previousLevel);
        satTarget = 1f;
        waveActive = false;
        // A mutual final-boss kill keeps the fall/results flow. Cleared-floor and
        // XP progress persist; the ending still requires surviving a rematch.
        if (DefeatPending) return;
        timers.Clear();
        Juice.Slow(1.5f);
        Flash(Color.white, 0.8f);
        Impact(1f);
        Cam.Shake(0.8f);
        ToonKit.Sfx("kill", 1f, 0.5f);
        Pickup.AttractAll = true;
        foreach (Enemy other in new List<Enemy>(Enemy.All))
            if (other != boss) other.Vanish();
        After(2.2f, () => Say(Story.BossDefeat, () => endingChoice = true));
    }

    public void OnPlayerHurt()
    {
        combo = 0;
        hurtA = 1f;
        heartPunch = 1f;
        StyleHurt();
    }

    public void Collect(Pickup.Kind kind, int value)
    {
        if (DefeatPending || Player.Dead) return;
        if (kind == Pickup.Kind.Coin)
        {
            float night = InWilds ? 1f + 0.25f * Night : 1f;
            int v = Mathf.Max(1, Mathf.RoundToInt(value * Player.stats.coinMult * (1f + 0.25f * Gloom) * night));
            if (Player.Dead) return;
            JourneyStore.Transaction(() => coins = checked(coins + v));
            coinPunch = 1f;
            // Each coin in a quick chain rings a little higher.
            coinChain = Time.unscaledTime - lastCoinT < 0.6f ? Mathf.Min(coinChain + 1, 14) : 0;
            lastCoinT = Time.unscaledTime;
            ToonKit.Sfx("coin", 0.5f, 1f + coinChain * 0.02f);
        }
        else
        {
            Player.Heal(1);
            heartPunch = 1f;
            ToonKit.Sfx("heart");
        }
    }

    public void HitNumber(Vector3 pos, float amount, bool crit)
    {
        if (numbers.Count > 40) numbers.RemoveAt(0);
        numbers.Add(new Number
        {
            pos = pos + ProductionRandom.Cosmetic.insideUnitSphere * 0.3f,
            text = Mathf.Max(1, Mathf.RoundToInt(amount)) + (crit ? "!" : ""),
            color = crit ? UI.Gold : Color.white,
            size = crit ? 38 : 24,
        });
    }

    public void Toast(string text, Color color)
    {
        toast = text;
        toastC = color;
        toastT = 1.4f;
    }

    public void Flash(Color color, float strength)
    {
        flashC = color;
        flashA = Mathf.Max(flashA, strength);
    }

    void Banner(string text, float seconds, string sub = "")
    {
        banner = text;
        bannerSub = sub;
        bannerT = bannerDur = seconds;
    }

    // ------------------------------------------------------------ pause

    // tab: 0 = the pause menu, 1 = the Status window.
    void OpenPause(int tab)
    {
        pausedFrom = State;
        pauseTab = tab;
        showSettings = false;
        SetState(GameState.Paused);
        ToonKit.Sfx("click");
    }

    void ClosePause()
    {
        showSettings = false;
        Save.Flush();
        SetState(pausedFrom);
    }

    // ------------------------------------------------------------ update

    void Update()
    {
        Bindings.Capture();
        GameInput.PollDevice();
        if (Player == null || JourneyStore.Error != null) { Juice.Paused = true; Time.timeScale = 0f; return; }
        TickControlAccess();
        Juice.Tick();
        float udt = Time.unscaledDeltaTime;

        if (fadeDir != 0f)
        {
            fade = Mathf.Clamp01(fade + fadeDir * udt * 4f);
            if (fadeDir > 0f && fade >= 1f)
            {
                Action act = fadeAct;
                fadeAct = null;
                fadeDir = -1f;
                act?.Invoke();
            }
            else if (fadeDir < 0f && fade <= 0f) fadeDir = 0f;
        }

        TickIntro(udt);
        sat = Mathf.MoveTowards(sat, satTarget, udt * 0.45f);
        Shader.SetGlobalFloat("_WorldSat", sat);
        GameClocks.Tick();
        TickClock(GameClocks.WorldDelta);
        TickScenery(GameClocks.PresentationDelta);
        TickStyle(udt);

        flashA = Mathf.MoveTowards(flashA, 0f, udt * 1.6f);
        hurtA = Mathf.MoveTowards(hurtA, 0f, udt * 1.8f);
        heartPunch = Mathf.MoveTowards(heartPunch, 0f, udt * 4f);
        comboPunch = Mathf.MoveTowards(comboPunch, 0f, udt * 5f);
        coinPunch = Mathf.MoveTowards(coinPunch, 0f, udt * 6f);
        bannerT -= udt;
        TickSystem(udt);
        toastT -= udt;
        resultsT += udt;
        shownCoins = Mathf.MoveTowards(shownCoins, coins, Mathf.Max(20f, Mathf.Abs(coins - shownCoins) * 8f) * udt);
        for (int i = numbers.Count - 1; i >= 0; i--)
        {
            numbers[i].age += udt;
            if (numbers[i].age > 0.8f) numbers.RemoveAt(i);
        }

        bool fighting = State == GameState.Playing && Enemy.All.Count > 0 && (InWilds ? Hunted() : waveActive);
        music.intensity = fighting ? 1f : State == GameState.Cards || State == GameState.Paused ? 0.25f : 0f;
        TickFocus(fighting, udt);
        TickGuidanceLog(udt);
        if (ambienceBed != null) ambienceBed.Tick(InTown, !InTown && LevelRoot != null && State != GameState.Menu, fighting, udt);

        if (State == GameState.Story)
        {
            TickDialog(udt);
            TickStory();
        }
        else if (State == GameState.Cards)
        {
            cardsT += udt;
            int n = GameInput.NumberPressed;
            if (n >= 0) PickCard(n);
        }

        // Esc: close whatever is open, else pause. Tab: the Status window.
        if (fadeDir == 0f && !Intro)
        {
            bool esc = controlBackHandledFrame != Time.frameCount && (GameInput.PausePressed || (GameInput.MenuBackPressed && hubPanel != 0)), tab = GameInput.StatusPressed;
            if (State == GameState.Playing || State == GameState.Hub)
            {
                if (esc && hubPanel != 0) OpenHubPanel(0);
                else if (esc) OpenPause(0);
                else if (tab && hubPanel == 0) OpenPause(1);
            }
            else if (State == GameState.Story && esc) OpenPause(0);
            else if (State == GameState.Paused && (esc || tab)) ClosePause();
        }
        TickMission();
        TickSparksTip();
        // An order for Mop, wherever the hero is walking about.
        if (Save.Pet && Roaming && fadeDir == 0f && !Intro && GameInput.CompanionPressed)
        {
            CompanionOrder order = Companion.CycleOrder();
            Toast("Mop - " + Companion.Describe(order), new Color(0.55f, 0.82f, 1f));
            ToonKit.Sfx("click", 0.8f, order == CompanionOrder.Solo ? 0.8f : 1.1f);
        }
        if (State == GameState.Hub) TickHub();
        if (InWilds && State == GameState.Playing) TickWilds();
        else if (State == GameState.Playing && !Intro) TickFloor();
        if (State != GameState.Playing) return;

        float dt = GameClocks.SimulationDelta;
        if (dt <= 0f) return;
        playTime += dt;
        comboT -= dt;
        if (comboT <= 0f) combo = 0;

        for (int i = timers.Count - 1; i >= 0; i--)
        {
            if (i >= timers.Count) continue;
            Timer t = timers[i];
            t.t -= dt;
            if (t.t > 0f) continue;
            timers.RemoveAt(i);
            t.act();
            if (State != GameState.Playing) return;
        }

        TickHints(dt);

        if (awakening && Enemy.All.Count > 0)
        {
            awakeningT += dt;
            Enemy first = Enemy.All[0];
            if (Player.hp <= 2f || awakeningT > 16f || first.hp < first.maxHp * 0.45f) TriggerAwakening();
        }

        TickArena();
        if (waveActive && pending == 0 && Enemy.All.Count == 0) FloorCleared();
    }

    // First-climb tutorial: each hint stays until the player does the thing.
    void TickHints(float dt)
    {
        if (hintStep < 0) return;
        hintT += dt;
        bool done =
            hintStep == 0 ? Player.movedTime > 1.2f :
            hintStep == 2 ? Player.strikes >= 3 :
            hintStep == 3 ? Player.dashes >= 1 :
            hintT > 8f;
        if (!done) return;
        hintT = 0f;
        hintStep = hintStep >= Hints.Length - 1 ? -1 : hintStep + 1;
        if (hintStep >= 0) PlaytestLog.Emit("tip", value: hintStep);
    }
}
