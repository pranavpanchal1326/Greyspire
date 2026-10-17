using UnityEngine;

// The tower: thirty floors, climbed one at a time, each stronger than the last.
// And the "System" that lets the hero grow: experience from what he defeats,
// levels, the Painter ranks, skills learned at set levels, and a quest a day.
// Nothing here is given for free: there is no starting higher up, and no power
// for walking through a door.
public partial class Game
{
    public const int FloorsPerChapter = 10;
    public const int TowerFloors = 30;

    static readonly string[] RankNames = { "E", "D", "C", "B", "A", "S" };
    static readonly int[] RankLevels = { 1, 4, 8, 13, 19, 26 };

    float systemT, systemDuration;
    string systemTitle = "", systemBody = "";
    readonly System.Collections.Generic.Queue<(string title, string body)> systemMessages = new System.Collections.Generic.Queue<(string, string)>();

    // 1-based floor number across the whole tower. Out in the Wilds it is how dangerous the land underfoot is.
    public int Floor => InWilds ? wildFloor : FloorCatalog.Legacy(level, wave).number;

    // Extra hearts every blow costs the hero where he is standing: one from floor 16, two from floor 26.
    // The Wilds follow the same rule by the strength of the region, so a deadly land is deadly.
    // (The King's own floor is the exception: his blows are already his.)
    public int Harm => (Floor >= 16 ? 1 : 0) + (Floor >= 26 && (InWilds || Floor < TowerFloors) ? 1 : 0);

    // Experience to go from this level to the next. It climbs steeply: early levels come in a
    // floor or two, late ones take real work.
    public static int XpNeeded(int lvl)
    {
        lvl = GrowthPolicy.Level(lvl);
        return 10 + lvl * 12 + lvl * lvl * 3;
    }

    // Skills the System hands out as the hero levels. Level 1 has only a basic swing.
    public const int SkillFinisher = 0, SkillBurst = 1, SkillKick = 2, SkillWave = 3, SkillDashStrike = 4;
    public static readonly int[] SkillLevels = { 2, 4, 6, 9, 12 };
    public static readonly string[] SkillNames = { "Spin Finisher", "Colour Burst", "Launcher Kick", "Brush Wave", "Dash Strike" };
    public static readonly string[] SkillDescs =
    {
        "A third combo hit that strikes all round you",
        "Press Q when the Colour meter is full",
        "Press R straight after a sword hit to kick enemies into the air",
        "Press F to cut a line through everything ahead",
        "Dashing through enemies hurts them",
    };

    public static bool HasSkill(int skill) => Save.Level >= SkillLevels[skill];

    public static string Rank(int lvl) => RankNames[RankIndex(lvl)];

    // 0 for E-Rank up to 5 for S-Rank.
    public static int RankIndex(int lvl)
    {
        int r = 0;
        for (int i = 0; i < RankLevels.Length; i++) if (lvl >= RankLevels[i]) r = i;
        return r;
    }

    // ------------------------------------------------------------ guardians

    // Every fifth floor the tower grows a Guardian. The stairs do not open until it falls.
    static readonly string[] GuardianNames =
    {
        "Grol the Gatekeeper", "The Heartwarden", "Ash-Maw", "The Second Heartwarden", "The King's Shadow",
    };

    public static bool GuardianFloor(int floor) => floor >= 1 && floor <= TowerFloors && FloorCatalog.At(floor).guardian >= 0;

    static string GuardianName(int floor) => GuardianNames[Mathf.Max(0, FloorCatalog.At(floor).guardian)];

    // What lives on a floor. A budget grows with the floor number and is spent on
    // enemies; tougher kinds only become available higher up.
    Wave FloorWave(int chapter, int index)
    {
        int floor = FloorCatalog.Legacy(chapter, index).number;
        if (floor >= TowerFloors) return new Wave(0, 0, 0, 0, 0, 0, true);

        var rng = new System.Random(7919 * floor + Gloom);
        var w = new Wave();
        float budget = 3.5f + floor * 1.2f;
        int count = 0;

        // A guardian floor: the Guardian itself, and higher up a lieutenant or two.
        if (GuardianFloor(floor))
        {
            w.brutes = 1 + floor / 15;
            budget -= 4f;
            count += w.brutes;
        }

        int guard = 0;
        while (budget > 0.9f && count < 16 && guard++ < 80)
        {
            int roll = rng.Next(100);
            if (floor >= 12 && roll < 6 && budget >= 6f) { w.brutes++; budget -= 7f; }
            else if (floor >= 7 && roll < 20 && budget >= 2f) { w.bombers++; budget -= 2f; }
            else if (floor >= 5 && roll < 32 && budget >= 3f) { w.bigSlimes++; budget -= 3f; }
            else if (floor >= 3 && roll < 50 && budget >= 2.5f) { w.spitters++; budget -= 2.5f; }
            else if (floor >= 2 && roll < 78 && budget >= 1.5f) { w.minions++; budget -= 1.5f; }
            else { w.slimes++; budget -= 1f; }
            count++;
        }
        return w;
    }

    // ------------------------------------------------------------ levelling

    // Permanent XP follows encounter risk. Style pays carried Sparks, so a
    // player's combat skill does not compound into twice the permanent levels.
    int KillXp(Enemy e) => GrowthPolicy.CoreXp(e.scoreValue, e.tier, InWilds ? Night : 0f);

    public void GainXp(int amount)
    {
        int previousLevel = Save.Level;
        JourneyStore.Transaction(() => Save.AwardXp(amount));
        PresentXp(previousLevel);
    }

    void PresentXp(int level0)
    {
        if (DefeatPending) return; // Earned growth persists; defeat cannot be canceled by its healing presentation.
        if (Save.Level == level0) return;
        Player.OnLevelUp();
        string rank0 = Rank(level0);
        string rank = Rank(Save.Level);
        SystemMessage("GROWTH", GrowthSummary(level0, Save.Level));
        if (rank != rank0)
        {
            Banner(rank + "-RANK", 2.4f);
            CutIn(rank + "-RANK PAINTER", Story.SystemC);
        }
        Flash(Story.SystemC, 0.35f);
        Fx.Ring(Player.transform.position, Story.SystemC, 0.5f, 6f, 0.5f);
        Fx.Burst(Player.transform.position + Vector3.up, Story.SystemC, 12, 7f);
        ToonKit.Sfx("levelup");
    }

    public static string GrowthSummary(int before, int after)
    {
        string body = "Lv. " + before + " -> " + after + "    +" + (3 * (GrowthPolicy.Level(after) - GrowthPolicy.Level(before))) + " stat points. " + ControlGuide.Key(ControlAction.Status) + " to allocate.";
        string ranks = "", skills = "";
        for (int i = 0; i < RankLevels.Length; i++)
            if (before < RankLevels[i] && after >= RankLevels[i]) ranks += (ranks.Length > 0 ? " / " : "") + RankNames[i];
        for (int i = 0; i < SkillLevels.Length; i++)
            if (before < SkillLevels[i] && after >= SkillLevels[i]) skills += (skills.Length > 0 ? " / " : "") + SkillNames[i];
        if (ranks.Length > 0) body += "\nRanks gained: " + ranks + ".";
        if (skills.Length > 0) body += "\nSkills: " + skills + ".";
        if (after >= GrowthPolicy.MaxLevel) body += "\nLevel limit reached. Future XP: 20 = 1 banked Spark; remainder retained.";
        return body;
    }

    void ResetSystem()
    {
        systemMessages.Clear(); systemT = systemDuration = 0f;
        systemTitle = systemBody = "";
    }
    void SystemMessage(string title, string body)
    {
        systemMessages.Enqueue((title, body));
        if (systemT <= 0f) TickSystem(0f);
    }
    void TickSystem(float dt)
    {
        // Reading time only runs while the window is visible. Pause, dialogue,
        // card choices and town panels must not silently consume queued rewards.
        bool visible = !Intro && !awakening && !showMap && fade < 0.1f &&
            (State == GameState.Playing || State == GameState.Hub && hubPanel == 0);
        if (dt > 0f && !visible) return;
        systemT -= dt;
        if (systemT > 0f || systemMessages.Count == 0) return;
        var message = systemMessages.Dequeue();
        systemTitle = message.title; systemBody = message.body;
        systemT = systemDuration = Mathf.Clamp(3f + systemBody.Length / 35f, 4f, 12f);
    }

    // The translucent blue "System" window, top centre.
    void DrawSystem(float w, float cx)
    {
        if (systemT <= 0f) return;
        float a = Mathf.Clamp01(systemT * 2f) * Mathf.Clamp01((systemDuration - systemT) * 6f);
        float width = Mathf.Min(720f, w - 48f);
        var r = UI.Centered(cx, 190, width, systemBody.Length > 140 ? 190 : 140);
        r.y -= (1f - Mathf.Clamp01((systemDuration - systemT) * 5f)) * 14f;
        SystemPanel(r, a);
        UI.Text(new Rect(r.x, r.y + 6, r.width, 22), "[ SYSTEM ]", 14, new Color(Story.SystemC.r, Story.SystemC.g, Story.SystemC.b, a));
        UI.Text(new Rect(r.x, r.y + 26, r.width, 36), systemTitle, 28, new Color(1f, 1f, 1f, a));
        UI.Text(new Rect(r.x + 20, r.y + 64, r.width - 40, r.height - 72), systemBody, 15, new Color(0.8f, 0.92f, 1f, a), TextAnchor.MiddleCenter, false);
    }

    // The System's window: dark glass with a thin bright edge and corner ticks.
    static void SystemPanel(Rect r, float a)
    {
        Color edge = new Color(Story.SystemC.r, Story.SystemC.g, Story.SystemC.b, 0.9f * a);
        UI.Fill(UI.Grow(r, 2), new Color(edge.r, edge.g, edge.b, 0.55f * a));
        UI.Fill(r, new Color(0.045f, 0.047f, 0.058f, 0.93f * a));
        UI.Fill(new Rect(r.x, r.y, r.width, 2), edge);
        UI.Fill(new Rect(r.x, r.yMax - 2, r.width, 2), edge);
        const float t = 14f;
        UI.Fill(new Rect(r.x - 2, r.y - 2, t, 4), edge); UI.Fill(new Rect(r.x - 2, r.y - 2, 4, t), edge);
        UI.Fill(new Rect(r.xMax - t + 2, r.y - 2, t, 4), edge); UI.Fill(new Rect(r.xMax - 2, r.y - 2, 4, t), edge);
        UI.Fill(new Rect(r.x - 2, r.yMax - 2, t, 4), edge); UI.Fill(new Rect(r.x - 2, r.yMax - t + 2, 4, t), edge);
        UI.Fill(new Rect(r.xMax - t + 2, r.yMax - 2, t, 4), edge); UI.Fill(new Rect(r.xMax - 2, r.yMax - t + 2, 4, t), edge);
    }

    // Level, rank and experience bar (HUD and town).
    void DrawLevel(float x, float y)
    {
        int lvl = GrowthPolicy.Level(Save.Level);
        UI.Text(new Rect(x, y, 240, 24), "Lv. " + lvl + "    " + Rank(lvl) + "-RANK", 17, Story.SystemC, TextAnchor.MiddleLeft);
        UI.Bar(new Rect(x, y + 24, 200, 10), Save.Xp / (float)(lvl >= GrowthPolicy.MaxLevel ? GrowthPolicy.XpPerSpark : XpNeeded(lvl)), Story.SystemC, UI.Ink);
        if (lvl >= GrowthPolicy.MaxLevel) UI.Text(new Rect(x, y + 36, 200, 18), "XP -> Sparks  (20 : 1)", 12, Story.SystemC, TextAnchor.MiddleLeft, false);
        if (Save.StatPoints > 0)
        {
            float pulse = 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 5f);
            UI.Text(new Rect(x + 208, y + 17, 220, 22), "+" + Save.StatPoints + "  " + ControlGuide.ShortKey(ControlAction.Status), 14, new Color(1f, 0.9f, 0.4f, pulse), TextAnchor.MiddleLeft);
        }
    }

    // ------------------------------------------------------------ the daily quest

    // One quest a day from the System, the same shape every day and a little bigger as the
    // hero grows: defeat so many enemies before the day is out. It pays a stat point.
    int DailyGoal => Mathf.Min(40, 8 + GrowthPolicy.Level(Save.Level) * 2);

    Save.DailyReward DailyKill() => Save.RecordDailyKill(DailyGoal, 10 + GrowthPolicy.Level(Save.Level) * 3);

    // The quest line under the commission tracker.
    void DrawDaily(float x, float y)
    {
        if (Save.BestFloor < 1) return;     // the System has to wake first
        bool today = Save.DailyDay == Save.Day;
        bool done = today && Save.DailyDone;
        int count = today ? Mathf.Min(Save.DailyCount, DailyGoal) : 0;
        var r = new Rect(x, y, 300, 30);
        UI.Panel(r, new Color(0.045f, 0.047f, 0.058f, 0.72f), true);
        UI.Fill(new Rect(r.x, r.y + 5, 4, r.height - 10), done ? UI.Green : Story.SystemC);
        UI.Text(new Rect(r.x + 14, r.y, 200, r.height), done ? "DAILY QUEST   done" : "DAILY QUEST   defeat enemies", 13,
            done ? UI.Green : Story.SystemC, TextAnchor.MiddleLeft, false);
        if (!done) UI.Text(new Rect(r.xMax - 82, r.y, 74, r.height), count + " / " + DailyGoal, 14, Color.white, TextAnchor.MiddleRight, false);
    }
}
