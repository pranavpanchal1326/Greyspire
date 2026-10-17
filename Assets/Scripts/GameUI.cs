using UnityEngine;

// Every screen and the in-game HUD, drawn with the UI kit.
public partial class Game
{
    static readonly Color[] Rainbow = { UI.Red, UI.Gold, UI.Green, UI.Blue, UI.Purple };
    static readonly Color Paper = new Color(0.9f, 0.87f, 0.8f), InkText = new Color(0.1f, 0.095f, 0.1f);
    static readonly Color Soft = new Color(0.85f, 0.85f, 0.95f), PanelFill = new Color(0.09f, 0.095f, 0.115f, 0.95f);

    static readonly string[] Hints =
    {
        "Move with WASD. Look around with the mouse.",
        "Monsters hold their ground until you come near. Clear the floor to unseal the stairs, to the north.",
        "Left click to swing. Hold it to charge a heavy slam. Attack in mid-air to plunge.",
        "Right click to dash: keep it held to sprint. Space twice for a second jump in the air.",
        "Hold C to block: block at the last instant to PARRY. Attack while blocking for a SHIELD BASH.",
        "T locks on. Z / V switch targets. G throws a blade. Heavy blows (R) break a raised guard.",
        "Mix your moves: style adds carried Sparks. XP follows the enemy's strength and night risk.",
        "Tab opens Status: 3 points per level through 60, up to 60 in each stat. Free respec in town.",
        "Settings > Controls rebinds any key or button. Settings > Access: hold or toggle guard and sprint, timing assist.",
    };

    void OnGUI()
    {
        UI.Begin(State + ":" + pauseTab + ":" + hubPanel + ":" + guildTab + ":" + boardGiver + ":" + showSettings + ":" + showCredits + ":" + confirmNew + ":" + settingsPage + ":" + Asking + ":" + askTitle + ":" + endingChoice + ":" + showMap);
        float w = UI.W, h = UI.H, cx = w * 0.5f;
        if (JourneyStore.Error != null || startupError != null)
        {
            Dim(w, h, 1f);
            UI.Text(new Rect(40, h * 0.35f, w - 80, 60), "JOURNEY SAVE NEEDS ATTENTION", 30, UI.Gold);
            UI.Text(new Rect(80, h * 0.45f, w - 160, 150), (JourneyStore.Error ?? startupError) + "\nClose the game and reopen it after resolving the issue.", 18, Color.white);
            if (UI.Button(UI.Centered(cx, h * 0.75f, 260, 52), "CLOSE GAME", UI.Red, 20)) Application.Quit();
            UI.End();
            return;
        }

        switch (State)
        {
            case GameState.Menu: DrawMenu(w, h, cx); break;
            case GameState.Shrine: DrawShrine(w, h, cx); break;
            case GameState.Codex: DrawCodex(w, h, cx); break;
            case GameState.Hub: if (Intro) DrawIntro(w, h); else DrawHub(w, h, cx); break;
            case GameState.Story: DrawStory(w, h, cx); break;
            case GameState.Playing: if (Intro) DrawIntro(w, h); else DrawHud(w, h, cx); break;
            case GameState.Cards: DrawHud(w, h, cx); DrawCards(w, h, cx); break;
            case GameState.Paused:
                if (pausedFrom == GameState.Hub) DrawHub(w, h, cx); else DrawHud(w, h, cx);
                DrawPause(w, h, cx);
                break;
            case GameState.Results: DrawResults(w, h, cx); break;
        }
        if (!showMap && (State == GameState.Playing || State == GameState.Hub || State == GameState.Story)) DrawCutIn(w, h);

        // Screen-wide overlays, back to front.
        var full = new Rect(0, 0, w, h);
        UI.Tex(full, UI.Vignette, new Color(0.025f, 0.026f, 0.031f, 0.5f));
        if ((State == GameState.Playing || State == GameState.Cards) && Player.hp <= 1f && !Player.Dead)
            UI.Tex(full, UI.Vignette, new Color(1f, 0.1f, 0.15f, FeedbackPolicy.Pulse(0.35f + 0.25f * Mathf.Sin(Time.unscaledTime * 6f))));
        if (hurtA > 0f) UI.Tex(full, UI.Vignette, new Color(1f, 0.1f, 0.15f, FeedbackPolicy.Pulse(hurtA * 0.8f)));
        if (flashA > 0f) UI.Fill(full, new Color(flashC.r, flashC.g, flashC.b, FeedbackPolicy.Flash(flashA)));
        if (fade > 0f) UI.Fill(full, new Color(UI.Ink.r * 0.5f, UI.Ink.g * 0.5f, UI.Ink.b * 0.5f, fade));
        if (controllerLost) UI.Text(new Rect(20, h - 30, w - 40, 26), "Controller disconnected. Reconnect, or use arrows / Enter / Esc.", 17, UI.Gold);
        UI.End();
    }

    bool ToGui(Vector3 world, out Vector2 gui)
    {
        Vector3 sp = cam.WorldToScreenPoint(world);
        gui = new Vector2(sp.x / UI.Scale, (Screen.height - sp.y) / UI.Scale);
        return sp.z > 0f;
    }

    static void Dim(float w, float h, float a)
    {
        UI.Fill(new Rect(0, 0, w, h), new Color(0.029f, 0.03f, 0.037f, a));
    }

    // The big line across the middle of the screen, and the smaller one under it.
    void DrawBanner(float w, float y, int size)
    {
        if (bannerT <= 0f || cutT > 0f) return;     // a cut-in has the middle of the screen to itself
        float age = bannerDur - bannerT;
        float pop = 1f + Mathf.Max(0f, 0.2f - age) * 2.5f;
        float a = Mathf.Clamp01(bannerT * 2.5f);
        UI.Text(new Rect(0, y, w, size * 1.5f), banner, Mathf.RoundToInt(size * pop), new Color(1f, 0.95f, 0.6f, a));
        if (string.IsNullOrEmpty(bannerSub)) return;
        // A rule draws out from the middle, and the second line sits under it.
        float rule = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(age / 0.4f)) * 170f;
        UI.Fill(new Rect(w * 0.5f - rule, y + size * 1.42f, rule * 2f, 2f), new Color(1f, 0.86f, 0.45f, a));
        UI.Text(new Rect(0, y + size * 1.48f, w, 30), bannerSub, 19, new Color(1f, 1f, 1f, a));
    }

    // ------------------------------------------------------------ menu

    // The title screen: the name, where the journey stands, and four plain choices down the
    // left, with the hero standing in the world on the right.
    void DrawMenu(float w, float h, float cx)
    {
        if (!string.IsNullOrEmpty(JourneyStore.Recovery))
            UI.Text(new Rect(30, h - 40, w - 60, 28), JourneyStore.Recovery, 16, UI.Gold);
        float t = Time.unscaledTime;
        const float x = 76f;
        // A soft dark wash behind the column so the words read over any scenery.
        UI.Tex(new Rect(-260, -120, 900, h + 240), UI.Glow, new Color(0.021f, 0.022f, 0.026f, 0.82f));

        UI.Text(new Rect(x - 4, 58, 900, 104), GameName, 88, UI.Paper, TextAnchor.MiddleLeft);
        float stroke = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t * 0.9f)) * 468f;
        UI.Fill(new Rect(x, 166, stroke, 2), UI.Gold);
        UI.Fill(new Rect(x, 171, stroke * 0.4f, 1), new Color(UI.Gold.r, UI.Gold.g, UI.Gold.b, 0.6f));
        UI.Prose(new Rect(x, 180, 700, 36), "The Weakest Painter", 27, UI.Gold, TextAnchor.MiddleLeft, true);
        UI.Text(new Rect(x, 218, 700, 26), "Thirty floors. One spark. Paint the world back.", 16, Soft, TextAnchor.MiddleLeft, false);
        UI.Text(new Rect(w - 260, h - 34, 240, 22), GameName + "   v" + Application.version, 13, new Color(1f, 1f, 1f, 0.45f), TextAnchor.MiddleRight, false);

        if (showCredits)
        {
            DrawCredits(w, h);
            return;
        }

        if (showSettings)
        {
            DrawSettings(x + 250f, 270);
            return;
        }

        bool saved = Save.HasProgress;
        if (confirmNew)
        {
            // Starting over erases a journey: say exactly what goes.
            var box = new Rect(x - 10, 286, 520, 230);
            UI.Framed(box, PanelFill, UI.Red);
            UI.Text(new Rect(box.x + 24, box.y + 16, box.width - 48, 34), "Start a new journey?", 26, Color.white, TextAnchor.MiddleLeft);
            UI.Text(new Rect(box.x + 24, box.y + 56, box.width - 48, 70),
                "This erases the one you have: Lv. " + Save.Level + ", floor " + Save.TowerFloor + " of the tower, " + Save.Sparks + " Sparks. Settings are kept. It cannot be undone.",
                16, Soft, TextAnchor.UpperLeft, false);
            if (UI.Button(new Rect(box.x + 24, box.y + 150, 220, 54), "ERASE AND BEGIN", UI.Red, 19))
            {
                confirmNew = false;
                NewGame();
            }
            if (UI.Button(new Rect(box.x + 264, box.y + 150, 230, 54), "KEEP MY JOURNEY", UI.Blue, 19)) confirmNew = false;
            UiNavigation.Prefer("KEEP MY JOURNEY");
            return;
        }

        float y = 286f;
        if (saved)
        {
            // Where the journey stands.
            var card = new Rect(x, y, 420, 62);
            UI.Panel(card, new Color(0.062f, 0.065f, 0.079f, 0.8f), true);
            UI.Fill(new Rect(card.x, card.y + 8, 4, card.height - 16), Story.SystemC);
            UI.Text(new Rect(card.x + 18, card.y + 6, 400, 26), Heroes.All[Heroes.Selected].name + "     Lv. " + Save.Level + "     " + Rank(Save.Level) + "-Rank",
                17, Color.white, TextAnchor.MiddleLeft);
            UI.Text(new Rect(card.x + 18, card.y + 32, 400, 24),
                "Tower: floor " + Save.TowerFloor + " of " + TowerFloors + "       Day " + Save.Day + ", " + PhaseNames[PhaseOf(Save.Hour)].ToLower(),
                14, Soft, TextAnchor.MiddleLeft, false);
            y += 78f;
            if (UI.Button(new Rect(x, y, 340, 64), "CONTINUE", UI.Red, 30)) EnterHub();
            y += 76f;
            if (UI.Button(new Rect(x, y, 340, 48), "NEW GAME", new Color(0.36f, 0.34f, 0.5f), 21)) confirmNew = true;
            y += 58f;
        }
        else
        {
            if (UI.Button(new Rect(x, y, 340, 64), "BEGIN", UI.Red, 30)) NewGame();
            y += 76f;
        }
        if (UI.Button(new Rect(x, y, 340, 48), "SETTINGS", UI.Blue, 21)) showSettings = true;
        y += 58f;
        if (UI.Button(new Rect(x, y, 340, 48), "QUIT", new Color(0.4f, 0.4f, 0.5f), 21)) Application.Quit();
        if (UI.Button(new Rect(w - 180, h - 86, 160, 40), "CREDITS", new Color(0.4f, 0.4f, 0.5f), 16)) showCredits = true;

        UI.Text(new Rect(x, h - 64, w - x * 2f, 22), ControlGuide.Key(ControlAction.Move) + " move    " + ControlGuide.Key(ControlAction.Look) + " look    " + ControlGuide.Key(ControlAction.Attack) + " attack (hold: charge)    " + ControlGuide.Key(ControlAction.Dash) + " dash",
            14, Soft, TextAnchor.MiddleLeft, false);
        UI.Text(new Rect(x, h - 42, w - x * 2f, 22), ControlGuide.Key(ControlAction.Block) + " block    " + ControlGuide.Key(ControlAction.Interact) + " talk    " + ControlGuide.Key(ControlAction.Lock) + " lock    " + ControlGuide.Key(ControlAction.Cycle) + " target    " + ControlGuide.Key(ControlAction.Pause) + " pause",
            14, Soft, TextAnchor.MiddleLeft, false);
    }

    // Browse the heroes at the Guild; the one on screen is the one standing in the square.
    void DrawHeroPicker(float h)
    {
        if (heroView < 0) heroView = Heroes.Selected;
        HeroDef hero = Heroes.All[heroView];
        bool owned = Heroes.Owned(heroView);

        var p = new Rect(28, h - 380, 280, 216);
        UI.Framed(p, new Color(0.107f, 0.112f, 0.136f, 0.92f), owned ? UI.Ink : UI.Purple);
        UI.Text(new Rect(p.x, p.y + 8, p.width, 24), "HERO", 18, UI.Gold);
        UI.Text(new Rect(p.x + 50, p.y + 34, p.width - 100, 52), hero.name, 22, Color.white);
        UI.Text(new Rect(p.x + 16, p.y + 90, p.width - 32, 74), hero.blurb, 16, Soft, TextAnchor.UpperCenter, false);

        int step = 0;
        if (UI.Button(new Rect(p.x + 10, p.y + 40, 36, 40), "<", UI.Blue, 22)) step = -1;
        if (UI.Button(new Rect(p.xMax - 46, p.y + 40, 36, 40), ">", UI.Blue, 22)) step = 1;
        if (step != 0)
        {
            heroView = (heroView + step + Heroes.All.Length) % Heroes.All.Length;
            Player.SetHero(heroView);
            if (Heroes.Owned(heroView)) Heroes.Selected = heroView;
            Player.RefreshStats();
            return;
        }

        var action = new Rect(p.x + 30, p.yMax - 52, p.width - 60, 40);
        if (owned) UI.Text(action, "READY", 20, UI.Green);
        else if (UI.Button(action, "UNLOCK  " + hero.cost + " Sparks", UI.Purple, 18, Save.Sparks >= hero.cost))
        {
            if (Heroes.Buy(heroView))
            {
                Heroes.Selected = heroView;
                ToonKit.Sfx("epic");
                Flash(UI.Gold, 0.3f);
            }
        }
    }

    public const string GameName = "GREYSPIRE";

    static readonly string[,] CreditLines =
    {
        { "Characters, outfits, monsters", "Quaternius" },
        { "Animation libraries", "Quaternius" },
        { "Village, props and nature kits", "Quaternius" },
        { "Sound effects and jingles", "Kenney" },
        { "Town and battle themes", "cynicmusic" },
        { "Engine", "Unity" },
    };

    // Who made what the game is built from. All of it is public-domain (CC0) or free-licence work.
    void DrawCredits(float w, float h)
    {
        var panel = new Rect(76, 262, 560, 360);
        UI.Framed(panel, PanelFill, UI.Gold);
        UI.Text(new Rect(panel.x, panel.y + 14, panel.width, 40), "Credits", 30, UI.Gold);
        for (int i = 0; i < CreditLines.GetLength(0); i++)
        {
            float y = panel.y + 70 + i * 34;
            UI.Text(new Rect(panel.x + 36, y, 300, 30), CreditLines[i, 0], 16, Soft, TextAnchor.MiddleLeft, false);
            UI.Prose(new Rect(panel.x + 320, y, 210, 30), CreditLines[i, 1], 20, Color.white, TextAnchor.MiddleLeft);
        }
        UI.Text(new Rect(panel.x + 36, panel.yMax - 96, panel.width - 72, 24), "Full list and licences: CREDITS.md in the game folder.", 14, Soft, TextAnchor.MiddleLeft, false);
        if (UI.Button(UI.Centered(panel.center.x, panel.yMax - 40, 200, 46), "BACK", UI.Red, 20)) showCredits = false;
    }

    static readonly string[] QualityNames = { "Low", "Medium", "High" };

    // cx is the middle of the panel.
    void DrawSettings(float cx, float y) => DrawFeedbackSettings(cx, y);

    // Bram's forge: Sparks hammered into strength that lasts.
    void DrawShrine(float w, float h, float cx)
    {
        Dim(w, h, 0.55f);
        UI.Text(new Rect(0, 40, w, 70), "BRAM'S FORGE", 56, UI.Purple);
        UI.Text(new Rect(0, 108, w, 34), "Sparks you bring home, hammered into strength that lasts.", 22, Color.white);
        UI.Text(new Rect(0, 144, w, 40), "SPARKS  " + Save.Sparks, 30, UI.Gold);

        for (int i = 0; i < Save.PerkCount; i++)
        {
            var row = UI.Centered(cx, 240 + i * 96, 760, 84);
            UI.Framed(row, new Color(0.107f, 0.112f, 0.136f, 0.95f), UI.Ink);
            UI.Text(new Rect(row.x + 24, row.y + 10, 300, 34), Save.PerkNames[i], 28, Rainbow[i], TextAnchor.MiddleLeft);
            UI.Text(new Rect(row.x + 24, row.y + 44, 360, 28), Save.PerkDescs[i], 19, Color.white, TextAnchor.MiddleLeft, false);

            int rank = Save.PerkRank(i);
            for (int p = 0; p < Save.PerkMaxRank; p++)
            {
                var pip = new Rect(row.x + 410 + p * 34, row.y + 28, 28, 28);
                UI.Tex(pip, UI.Circle, UI.Ink);
                UI.Tex(UI.Grow(pip, -4), UI.Circle, p < rank ? Rainbow[i] : new Color(0.3f, 0.28f, 0.4f));
            }

            int cost = Save.PerkCost(i);
            var buy = new Rect(row.xMax - 190, row.y + 16, 170, 52);
            if (cost < 0) UI.Text(buy, "MAXED", 24, UI.Gold);
            else if (UI.Button(buy, "BUY  " + cost, UI.Green, 24, Save.Sparks >= cost))
            {
                if (Save.BuyPerk(i))
                {
                    Player.RefreshStats();
                    ToonKit.Sfx("buy");
                    Flash(Rainbow[i], 0.25f);
                }
            }
        }

        if (UI.Button(UI.Centered(cx, h - 60, 260, 56), "BACK", UI.Red)) SetState(backTo);
    }

    // ------------------------------------------------------------ story

    // Dialogue, set the way a manga page sets it: narration in a paper caption box up in the
    // corner, speech in a white bubble with the speaker's name on a tab, the System in its
    // own blue glass, and the King in black.
    static string TalkHint => ControlGuide.Dialogue;

    void DrawStory(float w, float h, float cx)
    {
        UI.Fill(new Rect(0, 0, w, 70), new Color(0.021f, 0.022f, 0.026f, 0.96f));
        UI.Fill(new Rect(0, h - 70, w, 70), new Color(0.021f, 0.022f, 0.026f, 0.96f));
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f);

        // The last decision of the run.
        if (endingChoice)
        {
            Dim(w, h, 0.35f);
            UI.Text(new Rect(0, 190, w, 60), "What will you do?", 44, Color.white);
            if (UI.Button(UI.Centered(cx - 230, 380, 400, 120), Story.ChoiceA, UI.Red, 30)) ChooseEnding(0);
            if (UI.Button(UI.Centered(cx + 230, 380, 400, 120), Story.ChoiceB, UI.Gold, 30)) ChooseEnding(1);
            UI.Text(new Rect(cx - 430, 450, 400, 60), "Everything returns at once.\nThe king is lost.", 18, new Color(0.9f, 0.9f, 1f), TextAnchor.UpperCenter, false);
            UI.Text(new Rect(cx + 30, 450, 400, 60), "Spend Lumi to wake him.\nShe will not come back.", 18, new Color(0.9f, 0.9f, 1f), TextAnchor.UpperCenter, false);
            return;
        }
        // A question in the middle of a scene: two answers, and what each one means.
        if (askPick != null)
        {
            Dim(w, h, 0.35f);
            UI.Text(new Rect(0, 190, w, 60), askTitle, 44, Color.white);
            if (UI.Button(UI.Centered(cx - 230, 380, 400, 120), askA, UI.Gold, 30)) Answer(0);
            if (UI.Button(UI.Centered(cx + 230, 380, 400, 120), askB, UI.Blue, 30)) Answer(1);
            UI.Text(new Rect(cx - 430, 450, 400, 60), askHintA, 18, new Color(0.9f, 0.9f, 1f), TextAnchor.UpperCenter, false);
            UI.Text(new Rect(cx + 30, 450, 400, 60), askHintB, 18, new Color(0.9f, 0.9f, 1f), TextAnchor.UpperCenter, false);
            return;
        }
        // A Timeline is playing: the bars are up and the picture does the talking.
        if (cutscene)
        {
            UI.Text(new Rect(w - 400, h - 50, 370, 30), ControlGuide.Key(ControlAction.Skip) + " skip scene", 16, new Color(1f, 1f, 1f, 0.35f), TextAnchor.MiddleRight, false);
            return;
        }
        if (dialog == null || dialogIndex >= dialog.Length) return;

        Line line = dialog[dialogIndex];
        int shown = Mathf.Min(line.text.Length, (int)dialogChars);
        bool finished = shown >= line.text.Length;
        string said = line.text.Substring(0, shown);
        var hint = new Rect(w - 500, h - 50, 470, 30);
        Color hintC = new Color(1f, 1f, 1f, 0.4f + pulse * 0.6f);
        float settle = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(dialogT / 0.18f));

        // Chapter card: number over name, nothing else on screen.
        if (line.who == Story.Title)
        {
            string[] parts = line.text.Split('|');
            float a = Mathf.Clamp01(dialogT * 1.5f);
            Dim(w, h, 0.45f * a);
            UI.Text(new Rect(0, h * 0.5f - 90, w, 40), parts[0], 26, new Color(1f, 0.85f, 0.4f, a));
            UI.Fill(new Rect(cx - 190 * a, h * 0.5f - 44, 380 * a, 3), new Color(1f, 0.85f, 0.4f, a));
            UI.Text(new Rect(0, h * 0.5f - 36, w, 90), parts.Length > 1 ? parts[1] : "", 68, new Color(1f, 1f, 1f, a));
            UI.Text(hint, TalkHint, 18, hintC, TextAnchor.MiddleRight, false);
            return;
        }

        // Narration: a caption box in the corner of the panel.
        if (string.IsNullOrEmpty(line.who))
        {
            float width = Mathf.Min(780f, w - 120f);
            var cap = new Rect(60f - (1f - settle) * 30f, 96f, width, 40f + Mathf.Ceil(line.text.Length / (width / 13.2f)) * 31f);
            UI.Fill(new Rect(cap.x + 7, cap.y + 7, cap.width, cap.height), new Color(0f, 0f, 0f, 0.45f));
            UI.Fill(UI.Grow(cap, 3), InkText);
            UI.Fill(cap, Paper);
            UI.Prose(UI.Grow(cap, -18), said, 24, InkText, TextAnchor.UpperLeft, true);
            if (finished) UI.Text(hint, TalkHint, 18, hintC, TextAnchor.MiddleRight, false);
            return;
        }

        var panel = UI.Centered(cx, h - 172 + (1f - settle) * 16f, Mathf.Min(920f, w - 60f), 156);

        // The System does not speak. It reports.
        if (line.who == Story.SystemName)
        {
            var sys = UI.Centered(cx, h - 180, Mathf.Min(820f, w - 80f), 124);
            SystemPanel(sys, settle);
            UI.Text(new Rect(sys.x, sys.y + 8, sys.width, 22), "[ SYSTEM ]", 15, new Color(Story.SystemC.r, Story.SystemC.g, Story.SystemC.b, settle));
            UI.Text(new Rect(sys.x + 30, sys.y + 34, sys.width - 60, sys.height - 44), said, 24, new Color(0.85f, 0.95f, 1f), TextAnchor.MiddleCenter, false);
            if (finished) UI.Text(hint, TalkHint, 18, hintC, TextAnchor.MiddleRight, false);
            return;
        }

        // Everyone else speaks in a bubble: white for the living, black for the King.
        bool dark = line.who.Contains("King");
        Color fill = dark ? new Color(0.049f, 0.052f, 0.063f) : Paper;
        Color edge = dark ? line.color : InkText;
        Color words = dark ? new Color(0.94f, 0.88f, 1f) : InkText;
        UI.Panel(new Rect(panel.x + 6, panel.y + 8, panel.width, panel.height), new Color(0f, 0f, 0f, 0.4f));
        UI.Panel(UI.Grow(panel, 4), edge);
        UI.Panel(panel, fill);
        var tag = new Rect(panel.x + 26, panel.y - 24, 64 + line.who.Length * 14.5f, 44);
        UI.Panel(UI.Grow(tag, 3), dark ? line.color : InkText, true);
        UI.Panel(tag, dark ? new Color(0.049f, 0.052f, 0.063f) : line.color, true);
        UI.Prose(tag, line.who, 22, Color.white, TextAnchor.MiddleCenter);
        UI.Prose(new Rect(panel.x + 36, panel.y + 30, panel.width - 72, panel.height - 50), said, 26, words);
        if (finished)
            UI.Text(new Rect(panel.xMax - 440, panel.yMax - 36, 420, 28), TalkHint, 17,
                dark ? hintC : new Color(0.078f, 0.082f, 0.1f, 0.35f + pulse * 0.5f), TextAnchor.MiddleRight, false);
    }

    // Recovered memories, re-readable at the archive. Locked ones only show that they exist.
    void DrawCodex(float w, float h, float cx)
    {
        Dim(w, h, 0.62f);
        UI.Text(new Rect(0, 26, w, 60), "ECHOES", 52, UI.Gold);
        UI.Text(new Rect(0, 84, w, 30), "Memories the Colour Hearts gave back.", 20, Color.white);

        float y = 124f;
        for (int i = 0; i < Story.Echoes.Length; i++)
        {
            bool have = (Save.EchoMask & (1 << i)) != 0;
            var row = UI.Centered(cx, y + 31, Mathf.Min(1040f, w - 60f), 62);
            UI.Framed(row, new Color(0.107f, 0.112f, 0.136f, have ? 0.95f : 0.6f), have ? UI.Gold : UI.Ink, 3f);
            UI.Text(new Rect(row.x + 18, row.y + 4, 260, row.height - 8), have ? Story.Echoes[i].title : "? ? ?", 19,
                have ? UI.Gold : new Color(0.5f, 0.5f, 0.6f), TextAnchor.MiddleLeft);
            UI.Text(new Rect(row.x + 280, row.y + 6, row.width - 296, row.height - 12),
                have ? Story.Echoes[i].text : (i >= 5 ? "Reach an ending to recover this." : "Not yet recovered."), 15,
                have ? Color.white : new Color(0.6f, 0.6f, 0.7f), TextAnchor.MiddleLeft, false);
            y += 70f;
        }
        if (UI.Button(UI.Centered(cx, h - 48, 240, 50), "BACK", UI.Red)) SetState(backTo);
    }

    // ------------------------------------------------------------ HUD

    void DrawHud(float w, float h, float cx)
    {
        if (InWilds && showMap) { DrawMap(w, h, cx); return; }
        float t = Time.unscaledTime;

        // World-anchored: enemy bars, off-screen markers, damage numbers. A Guardian (or the
        // King) gets the long bar at the top of the screen instead.
        Enemy lead = null;
        foreach (Enemy e in Enemy.All)
        {
            if (!e.Training && (e.IsBoss || (e.title != null && !e.IsBandit && e.Alerted))) { lead = e; if (e.IsBoss) continue; }
            if (e.IsBoss) continue;
            if (InWilds && (e.transform.position - Player.transform.position).sqrMagnitude > 900f) continue;
            bool front = ToGui(e.transform.position + Vector3.up * (e.height + 0.35f), out Vector2 p);
            bool onScreen = front && p.x > 0 && p.x < w && p.y > 0 && p.y < h;
            // A Guardian or a wanted chief has a name, and it shows.
            if (onScreen && e.title != null && e != lead) UI.Text(UI.Centered(p.x, p.y - 22, 320, 26), e.title, 18, UI.Gold);
            // A pack's caller, mid-signal, is the one to go for.
            if (onScreen && e.Calling) UI.Text(UI.Centered(p.x, p.y - (e.title != null ? 44 : 22), 220, 24), "CALLING", 17, UI.Gold);
            // Monsters that have not noticed the hero yet are for the hero to find, until only a few are left.
            if (!onScreen && !InWilds && !e.Alerted && Enemy.All.Count > 3) continue;
            if (onScreen)
            {
                if (e.hp >= e.maxHp || e == lead) continue;
                var bar = UI.Centered(p.x, p.y, e.elite ? 64 : 48, 9);
                UI.Fill(UI.Grow(bar, 2), UI.Ink);
                UI.Fill(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(e.shownHp / e.maxHp), bar.height), Color.white);
                UI.Fill(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(e.hp / e.maxHp), bar.height), e.elite ? UI.Gold : UI.Red);
            }
            else
            {
                // Point at enemies you can't see so the last one is never a hunt.
                Vector2 d = p - new Vector2(cx, h * 0.5f);
                if (!front) d = -d;
                if (d.sqrMagnitude < 1f) d = Vector2.up;
                d.Normalize();
                float k = Mathf.Min((cx - 36f) / Mathf.Max(0.001f, Mathf.Abs(d.x)), (h * 0.5f - 36f) / Mathf.Max(0.001f, Mathf.Abs(d.y)));
                Vector2 m = new Vector2(cx, h * 0.5f) + d * k;
                // An attack already coming from out of sight: the marker swells and pulses through its wind-up.
                if (EncounterDirector.CommittedUnseen(e))
                {
                    float pulse = 1f + 0.18f * Mathf.Sin(t * 18f);
                    UI.Tex(UI.Centered(m.x, m.y, 46 * pulse, 46 * pulse), UI.Circle, UI.Ink);
                    UI.Tex(UI.Centered(m.x, m.y, 36 * pulse, 36 * pulse), UI.Circle, UI.Red);
                    UI.Text(UI.Centered(m.x, m.y, 40, 40), "!", 26, Color.white);
                    continue;
                }
                UI.Tex(UI.Centered(m.x, m.y, 26, 26), UI.Circle, UI.Ink);
                UI.Tex(UI.Centered(m.x, m.y, 18, 18), UI.Circle, e.elite ? UI.Gold : UI.Red);
            }
        }

        foreach (Number n in numbers)
        {
            if (!ToGui(n.pos, out Vector2 p)) continue;
            float k = n.age / 0.8f;
            float pop = 1f + Mathf.Max(0f, 0.25f - n.age) * 3f;
            Color c = n.color;
            c.a = Mathf.Clamp01((1f - k) * 3f);
            UI.Text(UI.Centered(p.x, p.y - k * 60f, 200, 60), n.text, Mathf.RoundToInt(n.size * pop), c);
        }
        DrawLockTarget(w, h);
        DrawWords();

        // Health: a bar in segments, one for each blow the hero can take.
        int hearts = Mathf.RoundToInt(Player.maxHp);
        float segW = Mathf.Min(34f, 300f / Mathf.Max(1, hearts));
        var health = new Rect(26, 24, segW * hearts + 4f, 20f + heartPunch * 3f);
        UI.Fill(UI.Grow(health, 1.5f), new Color(0.8f, 0.76f, 0.66f, 0.55f));
        UI.Fill(health, new Color(UI.Ink.r, UI.Ink.g, UI.Ink.b, 0.9f));
        bool low = Player.hp <= 1f && !Player.Dead;
        Color blood = low ? Color.Lerp(UI.Red, new Color(1f, 0.5f, 0.45f), 0.5f + 0.5f * Mathf.Sin(t * 8f)) : UI.Red;
        for (int i = 0; i < hearts; i++)
            if (i < Mathf.CeilToInt(Player.hp))
                UI.Fill(new Rect(health.x + 3f + i * segW, health.y + 3f, segW - 2f, health.height - 6f), blood);

        DrawLevel(26, 58);

        // Sparks carried.
        float cs = 13f + coinPunch * 5f;
        UI.Tex(UI.Centered(34, 114, cs + 4, cs + 4), UI.Circle, UI.Ink);
        UI.Tex(UI.Centered(34, 114, cs, cs), UI.Circle, UI.Gold);
        UI.Text(new Rect(48, 99, 200, 30), Mathf.RoundToInt(shownCoins) + "  Sparks", 17, UI.Gold, TextAnchor.MiddleLeft);
        // Mop's standing order, beside the purse, with the key that changes it.
        if (Save.Pet && Companion.Instance != null)
            UI.Text(new Rect(176, 99, 260, 30), "Mop: " + Companion.Order.ToString().ToUpper() + "   [" + ControlGuide.ShortKey(ControlAction.Companion) + "]", 15,
                Companion.Order == CompanionOrder.Solo ? new Color(0.7f, 0.72f, 0.78f) : new Color(0.55f, 0.82f, 1f), TextAnchor.MiddleLeft);

        // Timing assist is never hidden: a small tag beside health says it is on.
        if (Save.TimingAssist) UI.Text(new Rect(health.xMax + 10, 22, 90, 24), "ASSIST", 13, UI.Green, TextAnchor.MiddleLeft);
        UI.Fade = FocusFade;
        if (!TrainingActive) { DrawMission(26, 138); DrawDaily(26, ActiveMission != null ? 228 : 138); }
        DrawClock(w, 12);
        UI.Fade = 1f;
        DrawSystem(w, cx);
        DrawStyle(w, 66);

        // The Wilds show the region and a minimap; the tower shows the floor and what is left on it.
        if (InWilds)
        {
            DrawWildsHud(w, h, cx);
        }
        else
        {
            LevelDef def = Levels[level];
            UI.Text(new Rect(0, 12, w, 28), TrainingActive ? "GUILD PRACTICE YARD" : "FLOOR " + Floor + "   -   " + (Floor == ChapterProgress.MillraceFloor ? "THE MILLRACE" : Floor == PilotProgress.Floor ? "SURVEYOR'S CROSSING" : def.name.ToUpper()) + (Gloom > 0 ? "   -   GLOOM " + Gloom : ""), 20, Color.white);
            if (stairSpot != null || TrainingActive) DrawFloorHud(w, h, cx);
        }

        if (lead != null)
        {
            var bar = UI.Centered(cx, 102, Mathf.Min(620f, w - 560f), 22);
            UI.Text(new Rect(bar.x - 100, bar.y - 33, bar.width + 200, 26), lead.IsBoss ? "THE GLOOM KING" : lead.title.ToUpper(), 18, UI.Purple);
            UI.Panel(UI.Grow(bar, 4), UI.Ink, true);
            UI.Bar(bar, lead.shownHp / lead.maxHp, Color.white, new Color(0.2f, 0.15f, 0.3f));
            UI.Bar(bar, lead.hp / lead.maxHp, UI.Purple, new Color(0, 0, 0, 0));
        }

        DrawBanner(w, 222f, 60);
        if (toastT > 0f)
        {
            float pop = 1f + Mathf.Max(0f, toastT - 1.2f) * 2.5f;
            Color c = toastC;
            c.a = Mathf.Clamp01(toastT * 3f);
            UI.Text(new Rect(0, 350, w, 50), toast, Mathf.RoundToInt(30 * pop), c);
        }

        if (hintStep >= 0 && State == GameState.Playing)
        {
            // Bottom left, clear of the fight and of the skill bar.
            var panel = new Rect(24, h - 92, Mathf.Clamp(cx - 170f, 300f, 520f), 68);
            UI.Framed(panel, new Color(0.09f, 0.095f, 0.115f, 0.92f), UI.Gold, 3f);
            UI.Text(UI.Grow(panel, -12), ControlGuide.Tutorial(hintStep) ?? Hints[hintStep], 15, Color.white, TextAnchor.MiddleLeft, false);
        }
        DrawSkillBar(h, cx);
    }

    void DrawLockTarget(float w, float h)
    {
        Enemy locked = Player.LockedTarget;
        if (locked == null || locked.Dead) return;
        if (!ToGui(locked.transform.position + Vector3.up * Mathf.Clamp(locked.height * 0.65f, 0.5f, 1.6f), out Vector2 p)) return;
        if (p.x < 16f || p.x > w - 16f || p.y < 16f || p.y > h - 16f) return;
        Color color = UI.Gold;
        float r = 11f + Mathf.Sin(Time.unscaledTime * 3f) * 1.5f;
        UI.Fill(new Rect(p.x - r, p.y - r, 7f, 2f), color);
        UI.Fill(new Rect(p.x - r, p.y - r, 2f, 7f), color);
        UI.Fill(new Rect(p.x + r - 7f, p.y - r, 7f, 2f), color);
        UI.Fill(new Rect(p.x + r - 2f, p.y - r, 2f, 7f), color);
        UI.Fill(new Rect(p.x - r, p.y + r - 2f, 7f, 2f), color);
        UI.Fill(new Rect(p.x - r, p.y + r - 7f, 2f, 7f), color);
        UI.Fill(new Rect(p.x + r - 7f, p.y + r - 2f, 7f, 2f), color);
        UI.Fill(new Rect(p.x + r - 2f, p.y + r - 7f, 2f, 7f), color);
        UI.Text(new Rect(w * 0.5f - 170f, h - 120f, 340f, 24f), "LOCKED   T release   Z / V switch", 14, color);
    }

    // ------------------------------------------------------------ cards

    // A Guardian fell: one boon, kept for good.
    void DrawCards(float w, float h, float cx)
    {
        if (cards == null) return;
        Dim(w, h, 0.62f);
        UI.Text(new Rect(0, 46, w, 66), "THE GUARDIAN'S BOON", 48, UI.Gold);
        UI.Text(new Rect(0, 110, w, 28), "Choose one. It is yours for good.", 19, Color.white, TextAnchor.MiddleCenter, false);

        for (int i = 0; i < cards.Count; i++)
        {
            Upgrade u = cards[i];
            float k = Mathf.Clamp01((cardsT - i * 0.09f) / 0.28f);
            float ease = 1f - Mathf.Pow(1f - k, 3f);
            float x = cx + (i - (cards.Count - 1) * 0.5f) * 290f;
            var slot = UI.Centered(x, 372 + (1f - ease) * 260f, 260, 350);
            bool over = slot.Contains(UI.Mouse) && cardsT > 0.5f;
            if (over) slot.y -= 12f;

            if (over || u.rarity == Rarity.Epic)
                UI.Tex(UI.Grow(slot, 40), UI.Glow, new Color(u.Color.r, u.Color.g, u.Color.b, over ? 0.55f : 0.3f));
            UI.Framed(slot, new Color(0.115f, 0.121f, 0.147f, 1f), u.Color, 5f);

            var strip = new Rect(slot.x + 16, slot.y + 16, slot.width - 32, 34);
            UI.Panel(strip, u.Color, true);
            UI.Text(strip, u.rarity.ToString().ToUpper(), 18, Color.white);

            UI.Text(new Rect(slot.x + 16, slot.y + 64, slot.width - 32, 76), u.name, 27, Color.white);
            UI.Text(new Rect(slot.x + 22, slot.y + 148, slot.width - 44, 120), u.desc, 19, new Color(0.88f, 0.88f, 0.96f),
                TextAnchor.UpperCenter, false);
            if (u.max > 1)
                UI.Text(new Rect(slot.x, slot.yMax - 78, slot.width, 26), "Owned " + u.taken + " / " + u.max, 17,
                    new Color(0.7f, 0.7f, 0.82f), TextAnchor.MiddleCenter, false);
            UI.Text(new Rect(slot.x, slot.yMax - 48, slot.width, 34), "[ " + (i + 1) + " ]", 22, u.Color);

            if (UI.SelectArea(slot, u.name, cardsT > 0.5f)) PickCard(i);
            if (cards == null) return;
        }

        if (UI.Button(UI.Centered(cx, h - 78, 360, 50), "OFFER AGAIN  (" + rerollCost + " Sparks)", UI.Blue, 20, cardsT > 0.5f && (InTown ? Save.Sparks : coins) >= rerollCost))
            Reroll();
    }

    // ------------------------------------------------------------ pause & results

    // Esc: the pause menu, with the Status window and the settings one tab away.
    void DrawPause(float w, float h, float cx)
    {
        Dim(w, h, 0.66f);
        // Shown in reading order; pauseTab keeps its old numbers (2 is settings) and the journal is 3.
        string[] tabs = { "PAUSED", "STATUS", "JOURNAL", "SETTINGS" };
        int[] ids = { 0, 1, 3, 2 };
        for (int i = 0; i < tabs.Length; i++)
        {
            int id = ids[i];
            Color c = pauseTab == id ? (id == 1 ? Story.SystemC : UI.Gold) : new Color(0.36f, 0.34f, 0.48f);
            if (UI.Button(UI.Centered(cx + (i - 1.5f) * 214f, 66, 200, 46), tabs[i], c, 20))
            {
                if (pauseTab == 2 && id != 2) Bindings.Cancel();
                if (id == 3 && pauseTab != 3) PlaytestLog.Emit("journal_open");
                pauseTab = id;
            }
        }
        if (pauseTab == 3)
        {
            DrawJournal(w, h, cx);
            if (UI.Button(UI.Centered(cx, h - 46, 240, 48), "RESUME", UI.Red, 22)) ClosePause();
            return;
        }

        if (pauseTab == 1)
        {
            DrawStatus(w, h, cx);
            if (UI.Button(UI.Centered(cx, h - 46, 240, 48), "RESUME", UI.Red, 22)) ClosePause();
            return;
        }
        if (pauseTab == 2)
        {
            DrawSettings(cx, 150);
            return;
        }

        bool town = pausedFrom == GameState.Hub;
        bool hunted = !town && (Hunted() || clearPending || pausedFrom == GameState.Story);
        if (UI.Button(UI.Centered(cx, 190, 320, 60), "RESUME", UI.Red, 28)) ClosePause();
        if (!town)
        {
            string leave = InWilds ? "RETURN TO BRUSHWICK" : "LEAVE THE TOWER";
            if (UI.Button(UI.Centered(cx, 262, 320, 52), leave, UI.Blue, 20, !hunted))
            {
                Juice.Paused = false;
                if (InWilds) LeaveWilds();
                else LeaveTower();
            }
        }
        if (UI.Button(UI.Centered(cx, town ? 262 : 326, 320, 52), "MAIN MENU", new Color(0.4f, 0.4f, 0.5f), 20, !hunted))
        {
            Juice.Paused = false;
            Save.BankCarried();
            ToMenu();
        }
        float ny = town ? 304f : 368f;
        if (hunted) UI.Text(new Rect(0, ny, w, 24), "You are being hunted: there is no walking away from a fight.", 15, UI.Red, TextAnchor.MiddleCenter, false);
        else if (!town) UI.Text(new Rect(0, ny, w, 24), "Leaving keeps the Sparks you carry and every floor you have cleared.", 15, Soft, TextAnchor.MiddleCenter, false);
        UI.Text(new Rect(0, ny + 30, w, 26), Objective(), 17, new Color(1f, 0.95f, 0.75f));

        // Everything the hero can do: input on the left, move on the right.
        string[,] moves =
        {
            { ControlGuide.Key(ControlAction.Attack), "Sword combo" + (HasSkill(SkillFinisher) ? ", ending in a spin" : "") },
            { "Hold " + ControlGuide.Key(ControlAction.Attack), "Charged slam" },
            { ControlGuide.Key(ControlAction.Heavy), HasSkill(SkillKick) ? "Heavy slam, or launcher kick after a hit" : "Heavy slam" },
            { "Attack in the air", "Plunge" },
            { "Attack in a dash", "Piercing stab" },
            { (Save.GuardToggle ? "Press " : "Hold ") + ControlGuide.Key(ControlAction.Block), "Block. Time it for a PARRY" + (Save.GuardToggle ? " (toggle)" : "") },
            { "Attack while blocking", "Shield bash: breaks guards, opens a RIPOSTE" },
            { ControlGuide.Key(ControlAction.Dash), Save.SprintToggle ? "Dash, carrying into a sprint (toggle)" : "Dash. Keep it held to sprint" },
            { ControlGuide.Key(ControlAction.Block) + " in sprint", "Slide under attacks, tripping what you hit" },
            { ControlGuide.Key(ControlAction.Jump) + " in air", "Second jump" },
            { ControlGuide.Key(ControlAction.Throw), "Throw a blade at what you look at" },
            { ControlGuide.Key(ControlAction.Burst), HasSkill(SkillBurst) ? "Colour Burst" : "(Colour Burst: Lv " + SkillLevels[SkillBurst] + ")" },
            { ControlGuide.Key(ControlAction.Wave), HasSkill(SkillWave) ? "Brush Wave" : "(Brush Wave: Lv " + SkillLevels[SkillWave] + ")" },
            { ControlGuide.Key(ControlAction.Lock), "Lock / release; " + ControlGuide.Key(ControlAction.Cycle) + " switches" },
            { ControlGuide.Key(ControlAction.Status), "Status: spend stat points" },
            { ControlGuide.Key(ControlAction.Companion), Save.Pet ? "Mop's order: " + Companion.Order : "(Companion orders: after floor 3)" },
        };
        float mx = cx - 430f, my = h - 286f;
        UI.Text(new Rect(mx, my - 30, 420, 26), "MOVES", 18, UI.Gold, TextAnchor.MiddleLeft);
        int half = (moves.GetLength(0) + 1) / 2;
        for (int i = 0; i < moves.GetLength(0); i++)
        {
            float x = mx + (i >= half ? 450f : 0f), y = my + (i % half) * 25f;
            UI.Text(new Rect(x, y, 150, 24), moves[i, 0], 14, UI.Blue, TextAnchor.MiddleLeft, false);
            UI.Text(new Rect(x + 150, y, 300, 24), moves[i, 1], 14, Color.white, TextAnchor.MiddleLeft, false);
        }
    }

    void DrawResults(float w, float h, float cx)
    {
        Dim(w, h, 0.66f);
        float pop = 1f + Mathf.Max(0f, 0.3f - resultsT) * 2f;
        if (runWon) UI.Text(new Rect(0, 50, w, 100), "THE VALE IS FREE", Mathf.RoundToInt(76 * pop), UI.Gold);
        else UI.Text(new Rect(0, 50, w, 100), "YOU FELL ON FLOOR " + resultsFloor, Mathf.RoundToInt(60 * pop), UI.Red);
        if (!runWon && resultsT > 0.5f)
            UI.Text(new Rect(0, 142, w, 28), "Mop dragged you home. The floors you cleared stay cleared.", 19, Color.white, TextAnchor.MiddleCenter, false);

        var panel = UI.Centered(cx, 340, 600, 290);
        UI.Framed(panel, new Color(0.107f, 0.112f, 0.136f, 0.96f), UI.Ink);

        // The rows land one after another.
        Row(panel, 0, "Tower", "floor " + Save.TowerFloor + " of " + TowerFloors + " cleared", Color.white, 0.3f);
        Row(panel, 1, "Enemies defeated", kills.ToString(), Color.white, 0.6f);
        Row(panel, 2, "Time", (int)playTime / 60 + ":" + ((int)playTime % 60).ToString("00"), Color.white, 0.8f);
        Row(panel, 3, "Sparks carried home", "+" + sparksBanked + "   (total " + Save.Sparks + ")", UI.Gold, 1f);
        if (sparksLost > 0) Row(panel, 4, "Sparks dropped in the fall", "-" + sparksLost, UI.Red, 1.2f);
        else Row(panel, 4, "Painter", "Lv. " + Save.Level + "   " + Rank(Save.Level) + "-Rank", Story.SystemC, 1.2f);

        if (gloomUnlocked && resultsT > 1.9f)
            UI.Text(new Rect(0, 150, w, 34), "GLOOM TIER " + Save.GloomUnlocked + " UNLOCKED: climb again from the tower door", 21, UI.Purple);

        if (resultsT > 1.6f)
        {
            if (runWon && endingSeen >= 0)
                UI.Text(new Rect(cx - 420, 500, 840, 50), Story.EndingNames[endingSeen] + "   (" +
                    ((Save.EndingMask & 1) + ((Save.EndingMask >> 1) & 1)) + " of 2 endings seen)", 22, UI.Gold);
            else
                UI.Text(new Rect(cx - 420, 500, 840, 50), "Lumi:  " + defeatLine, 19, new Color(1f, 0.92f, 0.45f));
        }

        if (resultsT < 1.2f) return;
        if (runWon)
        {
            if (UI.Button(UI.Centered(cx, h - 80, 320, 58), "BACK TO BRUSHWICK", UI.Blue, 24)) EnterHub();
            return;
        }
        if (UI.Button(UI.Centered(cx - 170, h - 80, 320, 58), "BACK TO BRUSHWICK", UI.Blue, 22)) EnterHub();
        // Fallen to the Gatekeeper with his floor's packs already beaten: back to his gate.
        string again = resultsFloor == ChapterProgress.GatekeeperFloor && ChapterProgress.GatekeeperCheckpoint && Save.TowerFloor + 1 == resultsFloor
            ? "RETRY THE GATEKEEPER" : "CLIMB AGAIN: FLOOR " + Mathf.Min(Save.TowerFloor + 1, TowerFloors);
        if (UI.Button(UI.Centered(cx + 170, h - 80, 320, 58), again, UI.Red, 22)) StartRun();
        if (again == "RETRY THE GATEKEEPER" && resultsT > 1.4f)
            UI.Text(new Rect(0, h - 140, w, 24), "You will start at his gate: the rest of floor 5 stays beaten. A fall still costs half the Sparks you carry.", 15, new Color(0.85f, 0.85f, 0.9f));
    }

    void Row(Rect panel, int index, string label, string value, Color color, float appearAt)
    {
        if (resultsT < appearAt) return;
        float y = panel.y + 22 + index * 50;
        UI.Text(new Rect(panel.x + 36, y, 300, 44), label, 22, new Color(0.8f, 0.8f, 0.92f), TextAnchor.MiddleLeft);
        UI.Text(new Rect(panel.xMax - 356, y, 320, 44), value, 22, color, TextAnchor.MiddleRight);
    }
}
