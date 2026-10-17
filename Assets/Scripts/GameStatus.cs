using UnityEngine;

// The Status window: the System's own page on the hero. Level, rank and title,
// the four stats and the points to spend on them, the skills learned and the ones
// still locked, and the boons taken from Guardians. Opened with Tab. Also the
// skill bar at the foot of the HUD and the line that says what to do next.
public partial class Game
{
    static readonly string[] StatNames = { "STRENGTH", "AGILITY", "VITALITY", "SENSE" };
    static readonly Color[] StatColors = { UI.Red, UI.Green, new Color(1f, 0.55f, 0.6f), UI.Purple };

    static string StatEffect(int stat, int points)
    {
        points = GrowthPolicy.Rank(points);
        switch (stat)
        {
            case 0: return "Base damage +" + Mathf.RoundToInt(points * PlayerController.StrDamage * 100f) + "%. Soft cap after 50 damage.";
            case 1:
                return "Base tempo +" + (points * PlayerController.AgiAttack * 100f).ToString("0.#") + "% / move +"
                    + (points * PlayerController.AgiMove * 100f).ToString("0.#") + "%. Diminishing returns.";
            case 2:
                int left = PlayerController.VitPerHeart - points % PlayerController.VitPerHeart;
                return "Hearts  +" + points / PlayerController.VitPerHeart + "     (next heart in " + left + (left == 1 ? " point)" : " points)");
            default:
                return "Critical  +" + Mathf.RoundToInt(points * PlayerController.SenCrit * 100f) + "%     Colour charge  +"
                    + Mathf.RoundToInt(points * PlayerController.SenBurst * 100f) + "%";
        }
    }

    // What the hero is known as. It changes with what he has done, not with what he was given.
    string HeroTitle()
    {
        if (Save.EndingMask != 0) return "The One Who Painted It Back";
        int f = Save.BestFloor;
        if (f >= 25) return "Shadowbreaker";
        if (f >= 20) return "Twice Heart-Opener";
        if (f >= 15) return "Ash-Walker";
        if (f >= 10) return "Heart-Opener";
        if (f >= 5) return "Gatebreaker";
        if (f >= 1) return "The Awakened";
        return "The Weakest Painter";
    }

    void SpendStat(int stat)
    {
        if (!Save.AllocateStat(stat)) return;
        Player.RefreshStats();
        ToonKit.Sfx("buy", 0.8f, 1f + stat * 0.08f);
    }

    void DrawStatus(float w, float h, float cx)
    {
        float width = Mathf.Min(980f, w - 60f);
        var r = UI.Centered(cx, 365, width, 550);
        SystemPanel(r, 1f);
        Color sys = Story.SystemC, soft = new Color(0.8f, 0.92f, 1f);
        UI.Text(new Rect(r.x, r.y + 8, r.width, 24), "[ STATUS ]", 16, sys);

        // Who he is.
        float lx = r.x + 34, lw = width * 0.4f;
        int lvl = Save.Level;
        UI.Text(new Rect(lx, r.y + 44, lw, 34), Player.Hero.name, 27, Color.white, TextAnchor.MiddleLeft);
        UI.Text(new Rect(lx, r.y + 78, lw, 24), "\"" + HeroTitle() + "\"", 17, UI.Gold, TextAnchor.MiddleLeft, false);
        if (lvl > GrowthPolicy.MaxLevel) UI.Text(new Rect(lx, r.y + 100, lw, 16), "Legacy level " + lvl + " retained; effective 60.", 11, UI.Gold, TextAnchor.MiddleLeft, false);
        UI.Text(new Rect(lx, r.y + 114, lw, 30), "LEVEL  " + GrowthPolicy.Level(lvl) + " / 60    " + Rank(lvl) + "-RANK", 22, sys, TextAnchor.MiddleLeft);
        int need = lvl >= GrowthPolicy.MaxLevel ? GrowthPolicy.XpPerSpark : XpNeeded(lvl);
        UI.Bar(new Rect(lx, r.y + 150, lw - 20, 14), Save.Xp / (float)need, sys, UI.Ink);
        UI.Text(new Rect(lx, r.y + 166, lw - 20, 24), lvl >= GrowthPolicy.MaxLevel ? "XP -> banked Sparks: 20 : 1. Remainder " + Save.Xp : "Experience  " + Save.Xp + " / " + need, 13, soft, TextAnchor.MiddleLeft, false);

        PlayerStats s = Player.stats;
        string[,] facts =
        {
            { "Hearts", s.maxHearts.ToString() },
            { "Sword damage", Mathf.RoundToInt(s.damage).ToString() },
            { "Critical chance", Mathf.RoundToInt(s.crit * 100f) + "%" },
            { "Critical damage", s.critMultiplier.ToString("0.##") + "x" },
            { "Attack speed", Mathf.RoundToInt(s.attackSpeed * 100f) + "%" },
            { "Move speed", Mathf.RoundToInt(s.moveSpeed * 100f) + "%" },
            { "Daily points", Save.DailyGrowth + " / 20 lifetime" },
            { "Tower", "floor " + Save.TowerFloor + " of " + TowerFloors + " cleared" },
            { "Painters rescued", Rescued() + " of " + Story.Rescues.Length },
        };
        for (int i = 0; i < facts.GetLength(0); i++)
        {
            float y = r.y + 194 + i * 20;
            UI.Text(new Rect(lx, y, 190, 24), facts[i, 0], 16, soft, TextAnchor.MiddleLeft, false);
            UI.Text(new Rect(lx + 170, y, lw - 180, 24), facts[i, 1], 16, Color.white, TextAnchor.MiddleLeft, false);
        }

        // The four stats, and the points to put into them.
        float sx = r.x + 54 + lw, sw = r.xMax - sx - 30;
        int points = Save.StatPoints;
        UI.Text(new Rect(sx, r.y + 44, sw, 28), points > 0 ? "POINTS TO SPEND:  " + points : "Three points per level, through level 60.", points > 0 ? 20 : 15,
            points > 0 ? UI.Gold : soft, TextAnchor.MiddleLeft, points > 0);
        for (int i = 0; i < Save.StatCount; i++)
        {
            var row = new Rect(sx, r.y + 82 + i * 62, sw, 54);
            UI.Fill(row, new Color(1f, 1f, 1f, 0.05f));
            UI.Fill(new Rect(row.x, row.y, 4, row.height), StatColors[i]);
            int have = Save.Stat(i);
            UI.Text(new Rect(row.x + 16, row.y + 4, 180, 26), StatNames[i], 19, StatColors[i], TextAnchor.MiddleLeft);
            UI.Text(new Rect(row.x + 174, row.y + 2, sw - 244, 30), GrowthPolicy.Rank(have) + " / 60", 22, Color.white, TextAnchor.MiddleLeft);
            UI.Text(new Rect(row.x + 16, row.y + 29, sw - 90, 23), have > GrowthPolicy.MaxStat ? "Legacy " + have + " retained; effective 60. Free town respec." : StatEffect(i, have), 12, soft, TextAnchor.MiddleLeft, false);
            if (UI.Button(new Rect(row.xMax - 62, row.y + 7, 50, 40), "+", StatColors[i], 26, points > 0 && have < GrowthPolicy.MaxStat)) SpendStat(i);
        }
        UI.Text(new Rect(sx, r.y + 330, sw, 44), "Damage <100 (soft after 50); hearts 30 / reach 2x / dash 0.2s.\nTempo <180%, move <108%; clips <=2x.\nCrit <=75%; excess adds critical damage.", 11, soft, TextAnchor.UpperLeft, false);

        // Skills: learned, or the level they come at.
        float ky = r.y + 382;
        UI.Text(new Rect(r.x + 34, ky, 200, 22), "SKILLS", 15, sys, TextAnchor.MiddleLeft);
        float kw = (r.width - 68f) / SkillNames.Length;
        for (int i = 0; i < SkillNames.Length; i++)
        {
            bool have = HasSkill(i);
            var box = new Rect(r.x + 34 + i * kw, ky + 22, kw - 8, 48);
            UI.Fill(box, have ? new Color(sys.r, sys.g, sys.b, 0.16f) : new Color(1f, 1f, 1f, 0.04f));
            UI.Fill(new Rect(box.x, box.y, box.width, 2), have ? sys : new Color(0.4f, 0.45f, 0.55f));
            UI.Text(new Rect(box.x + 4, box.y + 2, box.width - 8, 20), SkillNames[i], 14, have ? Color.white : new Color(0.6f, 0.62f, 0.7f));
            UI.Text(new Rect(box.x + 4, box.y + 23, box.width - 8, 24), have ? SkillDescs[i] : "Unlocks at Lv. " + SkillLevels[i], 10,
                have ? soft : new Color(0.6f, 0.62f, 0.7f), TextAnchor.UpperCenter, false);
        }

        // Boons kept from Guardians.
        string boons = "";
        foreach (Upgrade u in Upgrades.All)
            if (u.taken > 0) boons += (boons.Length > 0 ? "     " : "") + u.name + (u.taken > 1 ? " x" + u.taken : "");
        UI.Text(new Rect(r.x + 34, r.yMax - 95, 220, 18), "GUARDIAN'S BOONS", 13, sys, TextAnchor.MiddleLeft);
        UI.Text(new Rect(r.x + 34, r.yMax - 77, r.width - 68, 30), boons.Length > 0 ? boons : "None yet. A Guardian waits on every fifth floor.", 12,
            boons.Length > 0 ? UI.Gold : soft, TextAnchor.MiddleLeft, false);
        bool inTown = InTown && !DefeatPending;
        if (UI.Button(new Rect(r.x + 34, r.yMax - 40, 280, 30), "FREE RESPEC (refund all points)", UI.Blue, 13, inTown))
        {
            Save.RespecStats(); Player.RefreshStats();
            SystemMessage("RESPEC", "All allocations refunded. " + Save.StatPoints + " points available; each stat has a limit of 60.");
        }
        if (UI.Button(new Rect(r.x + 326, r.yMax - 40, 290, 30), "CONVERT 1 POINT -> 25 SPARKS", UI.Gold, 13, inTown && points > 0))
        {
            if (Save.ConvertPoint()) SystemMessage("POINT CONVERTED", "1 point spent permanently. +25 banked Sparks.");
        }
        UI.Text(new Rect(r.x + 630, r.yMax - 40, r.width - 664, 30), inTown ? "Conversion is permanent." : "Respec / conversion in town.", 11, soft, TextAnchor.MiddleLeft, false);
    }

    static int Rescued()
    {
        int n = 0;
        for (int i = 0; i < Story.Rescues.Length; i++) if ((Save.RescuedMask & (1 << i)) != 0) n++;
        return n;
    }

    // ------------------------------------------------------------ HUD pieces

    // Dash, throw and the two System skills, with the key for each and how ready it is.
    void DrawSkillBar(float h, float cx)
    {
        const float size = 54f, gap = 10f;
        float x = cx - (size * 4f + gap * 3f) * 0.5f, y = h - size - 22f;
        Slot(new Rect(x, y, size, size), "DASH", ControlGuide.ShortKey(ControlAction.Dash), Player.DashReady, UI.Blue, 0);
        Slot(new Rect(x + (size + gap), y, size, size), "THROW", ControlGuide.ShortKey(ControlAction.Throw), Player.ThrowReady, new Color(0.6f, 0.9f, 1f), 0);
        Slot(new Rect(x + (size + gap) * 2f, y, size, size), "WAVE", ControlGuide.ShortKey(ControlAction.Wave), Player.WaveReady, UI.Gold, HasSkill(SkillWave) ? 0 : SkillLevels[SkillWave]);
        bool ready = Player.BurstReady;
        Color burst = ready && Save.Flashes > 0f ? Color.HSVToRGB(Time.unscaledTime * 0.7f % 1f, 0.7f, 1f) : UI.Purple;
        var b = new Rect(x + (size + gap) * 3f, y, size, size);
        if (ready && HasSkill(SkillBurst)) UI.Tex(UI.Grow(b, 16), UI.Glow, new Color(burst.r, burst.g, burst.b, 0.6f));
        Slot(b, "BURST", ControlGuide.ShortKey(ControlAction.Burst), Player.burst / 100f, burst, HasSkill(SkillBurst) ? 0 : SkillLevels[SkillBurst]);
    }

    // lockedUntil > 0: the skill is not learned yet, and that is the level it comes at.
    static void Slot(Rect r, string name, string key, float ready, Color color, int lockedUntil)
    {
        UI.Panel(UI.Grow(r, 3), UI.Ink, true);
        UI.Panel(r, new Color(0.135f, 0.142f, 0.173f), true);
        if (lockedUntil > 0)
        {
            UI.Text(new Rect(r.x, r.y + 6, r.width, 20), name, 11, new Color(0.55f, 0.55f, 0.65f), TextAnchor.MiddleCenter, false);
            UI.Text(new Rect(r.x, r.y + 24, r.width, 24), "Lv " + lockedUntil, 14, new Color(0.6f, 0.6f, 0.7f), TextAnchor.MiddleCenter, false);
            return;
        }
        ready = Mathf.Clamp01(ready);
        bool full = ready >= 0.999f;
        float fill = r.height * ready;
        Color c = full ? color : new Color(color.r * 0.55f, color.g * 0.55f, color.b * 0.6f);
        if (fill > 8f) UI.Panel(new Rect(r.x, r.yMax - fill, r.width, fill), c, true);
        UI.Text(new Rect(r.x, r.y + 5, r.width, 18), name, 11, Color.white, TextAnchor.MiddleCenter);
        UI.Text(new Rect(r.x, r.y + 22, r.width, 28), key, key.Length > 1 ? 15 : 22, full ? Color.white : new Color(1f, 1f, 1f, 0.6f));
    }

    // What has the hero's attention: the nearest enemy that has noticed him, or failing that
    // the person or thing he is close enough to use. False when there is nothing in particular.
    public bool Attention(out Vector3 point)
    {
        point = Vector3.zero;
        if (!Roaming) return false;
        Vector3 me = Player.transform.position;
        float best = 11f * 11f;
        Enemy near = null;
        if (State == GameState.Playing)
            foreach (Enemy e in Enemy.All)
            {
                float d = (e.transform.position - me).sqrMagnitude;
                if (d < best && !e.Dead) { best = d; near = e; }
            }
        if (near != null)
        {
            point = near.transform.position + Vector3.up * Mathf.Min(1.5f, near.height * 0.8f);
            return true;
        }
        if (nearSpot == null) return false;
        point = nearSpot.pos + Vector3.up * 1.5f;
        return true;
    }

    // What the hero's hands do when he uses something: lift a lid, drink, greet.
    void SpotGesture(Spot s)
    {
        string clip = s.name == "Treasure" || s.prompt == "Take it back" ? "Chest_Open"
            : s.name == "Colour Spring" ? "Consume"
            : s.who != null || s.name == "A drained Painter" ? "Interact" : null;
        if (clip != null) Player.Gesture(clip, clip == "Interact" ? 1.2f : 1f, s.pos);
    }

    // One line saying where the story wants the hero next.
    string Objective()
    {
        // Inside the tower the objective is the floor's own task.
        if (!InTown && !InWilds && !TrainingActive && Floor >= 1 && stairSpot != null)
        {
            if (stairsOpen) return "Floor " + Floor + " is clear: the stairs are open, to the north";
            if (GuardianFloor(Floor)) return "Floor " + Floor + ": defeat " + GuardianName(Floor) + " to open the stairs";
            return "Floor " + Floor + ": clear its monsters to open the stairs";
        }
        if (ActiveMission != null && MissionReady) return "Report to " + ActiveMission.giver + " for your pay";
        if (Save.BestFloor == 0) return "Enter the Gloom Tower, north of the square";
        if (Save.TowerFloor >= TowerFloors) return "The tower is quiet. Brushwick is yours to walk.";
        int next = Save.TowerFloor + 1;
        if (next == TowerFloors) return "Floor 30: the Gloom King is waiting";
        return GuardianFloor(next) ? "Floor " + next + ": " + GuardianName(next) + " guards the stairs" : "Climb the tower: Floor " + next + " is next";
    }
}
