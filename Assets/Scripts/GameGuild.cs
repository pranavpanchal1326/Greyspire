using System;
using UnityEngine;

// The Painters' Guild board. Townsfolk and the noble houses post commissions; a
// Painter takes one at a time, does the work out in the Wilds or up the tower,
// and comes back to be paid. Rank decides which commissions the Guild will hand
// over, and finished commissions are how a Painter makes a name.
public partial class Game
{
    enum Job { Bandits, Monsters, Chief, Explore, Climb, Recover }

    class Mission
    {
        public string id, title, giver, brief, thanks;
        public string targetId, target; // stable identity and separate display name
        public Job job;
        public string regionId = "";
        public int region => RegionIndex(regionId); // UI boundary only
        public int count = 1;       // how many (kills, regions), or which floor to clear
        public int rank;            // lowest rank that may take it: 0 = E ... 5 = S
        public int sparks, xp;
        public bool allRegions;
    }

    const string GuildDesk = "Nix of the Guild", HouseVane = "Lady Aurelia Vane", HouseAshgrave = "Lord Corvin Ashgrave";

    // Stable IDs own saved identity; ordering and display text are presentation.
    static readonly Mission[] Missions =
    {
        new Mission
        {
            id = "wolves-road", title = "Wolves on the Road", giver = GuildDesk, job = Job.Bandits, regionId = "greenreach", count = 5, rank = 0, sparks = 30, xp = 40,
            brief = "Bandits have camped along the meadow road, a morning's walk from the gate. Drive off five of them.",
            thanks = "Five fewer on the road. The carters will be buying you drinks for a week. Here: you earned it.",
        },
        new Mission
        {
            id = "slime-season", title = "Slime Season", giver = GuildDesk, job = Job.Monsters, count = 8, rank = 0, sparks = 25, xp = 35,
            brief = "The Wilds are crawling. Put down eight monsters, any kind, anywhere outside the walls.",
            thanks = "Eight. I counted the ears. Slimes have no ears, so I am taking your word on those.",
        },
        new Mission
        {
            id = "first-steps", title = "First Steps Up", giver = GuildDesk, job = Job.Climb, count = 3, rank = 0, sparks = 30, xp = 50,
            brief = "Anyone can walk into the tower. Prove you can climb it: clear the third floor and come back breathing.",
            thanks = "Third floor, and you walked out on your own legs. That is further than the Guild expected. Further than I did.",
        },
        new Mission
        {
            id = "lay-land", title = "The Lay of the Land", giver = GuildDesk, job = Job.Explore, count = 4, rank = 0, sparks = 30, xp = 60,
            brief = "The Guild's maps are ten years stale. Set foot in four regions of the Wilds and tell me what is still there.",
            thanks = "So the lake is still a lake. Good. I will redraw the map tonight. Take this for the boot leather.",
        },
        new Mission
        {
            id = "knives-pines", title = "Knives in the Pines", giver = GuildDesk, job = Job.Bandits, regionId = "whispering-woods", count = 8, rank = 1, sparks = 60, xp = 90,
            brief = "A band has dug in among the Whispering Woods and they throw knives from the trees. Eight of them, please.",
            thanks = "The woodcutters can go back in. You are making the Guild look competent. Do not let it go to your head.",
        },
        new Mission
        {
            id = "lake-monsters", title = "Something in the Lake", giver = GuildDesk, job = Job.Monsters, regionId = "mirror-lake", count = 8, rank = 1, sparks = 50, xp = 80,
            brief = "The fishers will not go near Mirror Lake. Clear eight monsters off the shore.",
            thanks = "Fish for supper again. You have no idea how tired this town is of turnips.",
        },
        new Mission
        {
            id = "garrick", title = "Wanted: Garrick Two-Axes", giver = GuildDesk, job = Job.Chief, regionId = "goldleaf", rank = 1, sparks = 90, xp = 140,
            targetId = "chief:garrick", target = "Garrick Two-Axes",
            brief = "A bandit chief holds a camp in Goldleaf Vale and taxes everyone who passes under the red trees. Bring him down.",
            thanks = "Garrick. Finished. I had his poster on that board for six years. I am keeping it as a trophy.",
        },
        new Mission
        {
            id = "tenth-floor", title = "The Tenth Floor", giver = GuildDesk, job = Job.Climb, count = 10, rank = 1, sparks = 100, xp = 160,
            brief = "The first Colour Heart is on the tenth floor. No Painter has opened it since the tower rose. Be the one.",
            thanks = "You opened a Heart. Did you see the grass out there? It is GREEN. Take the purse. Take two.",
        },
        new Mission
        {
            id = "sand-steel", title = "Sand and Steel", giver = GuildDesk, job = Job.Bandits, regionId = "sunscorch", count = 10, rank = 2, sparks = 110, xp = 170,
            brief = "The dune bandits have shields and know how to use them. Ten of them. Bring something heavy.",
            thanks = "Ten shields fewer in the sand. The caravans are moving again. C-Rank work, cleanly done.",
        },
        new Mission
        {
            id = "fen-monsters", title = "What the Fen Keeps", giver = GuildDesk, job = Job.Monsters, regionId = "sunken-fen", count = 10, rank = 2, sparks = 100, xp = 160,
            brief = "Nobody comes back from the Sunken Fen with dry boots or good news. Thin it out: ten monsters.",
            thanks = "You smell like a pond. Stand over there while I count out your pay.",
        },
        new Mission
        {
            id = "every-road", title = "Every Road", giver = GuildDesk, job = Job.Explore, count = 10, allRegions = true, rank = 2, sparks = 150, xp = 250,
            brief = "Walk all of it. Every region between here and the tower. The map should have no blank left on it.",
            thanks = "A whole map. The first complete one in ten years, and an E-Rank drew it. Well. Not E-Rank any more.",
        },
        new Mission
        {
            id = "hobb", title = "Wanted: Old Hobb Frostbite", giver = GuildDesk, job = Job.Chief, regionId = "frostfall", rank = 2, sparks = 160, xp = 260,
            targetId = "chief:hobb", target = "Old Hobb Frostbite",
            brief = "A chief up in Frostfall Heights has been raiding the pass since before you were born. End it.",
            thanks = "Old Hobb. My mother used to frighten me with his name. And you just... went and did it.",
        },
        new Mission
        {
            id = "graves-walk", title = "The Graves Walk", giver = GuildDesk, job = Job.Monsters, regionId = "hollow-graves", count = 12, rank = 3, sparks = 180, xp = 280,
            brief = "Things are moving among the Hollow Graves that should be lying still. Twelve of them, back in the ground.",
            thanks = "Quiet up there now. The gravediggers send their thanks, and a pie. I ate the pie.",
        },
        new Mission
        {
            id = "twentieth-floor", title = "The Twentieth Floor", giver = GuildDesk, job = Job.Climb, count = 20, rank = 3, sparks = 200, xp = 320,
            brief = "Two Hearts. Twenty floors. Nobody has been that high and come back to say what is there.",
            thanks = "Twenty. Sit down. Tell me everything. No: get paid first, then tell me everything.",
        },
        new Mission
        {
            id = "ember-hunt", title = "Ember Hunt", giver = GuildDesk, job = Job.Monsters, regionId = "emberscar", count = 12, rank = 3, sparks = 200, xp = 320,
            brief = "Emberscar. Lava, ash and the worst monsters outside the tower. Twelve. Do not fall in.",
            thanks = "Your eyebrows are singed. Your pay is not. That was B-Rank work and you made it look ordinary.",
        },
        new Mission
        {
            id = "vane-signet", title = "The Vane Signet", giver = HouseVane, job = Job.Recover, regionId = "whispering-woods", rank = 0, sparks = 80, xp = 70,
            targetId = "item:vane-signet", target = "The Vane strongbox",
            brief = "Bandits took a strongbox from my carriage on the forest road. The signet inside is four hundred years old. They may keep the coin.",
            thanks = "My grandmother's grandmother wore this. You have no idea what you carried home in your pocket. House Vane is in your debt.",
        },
        new Mission
        {
            id = "sand-banner", title = "A Banner in the Sand", giver = HouseVane, job = Job.Chief, regionId = "sunscorch", rank = 1, sparks = 140, xp = 160,
            targetId = "chief:sefa", target = "Red Sefa of the Dunes",
            brief = "A chief in the Sunscorch Dunes flies my house's banner over her tent, as a joke. I am not laughing. Take it down. Her too.",
            thanks = "Then the banner is ours again. I shall have it hung in the hall, and your name put under it. Yes, yours.",
        },
        new Mission
        {
            id = "ashgrave-wager", title = "Ashgrave's Wager", giver = HouseAshgrave, job = Job.Climb, count = 5, rank = 0, sparks = 100, xp = 80,
            brief = "I have ten Sparks that say an E-Rank never sees the fifth floor of that tower. Prove me a fool and I pay you tenfold.",
            thanks = "The fifth floor. Hm. A fluke, obviously. A hundred Sparks, as agreed. An Ashgrave pays his debts.",
        },
        new Mission
        {
            id = "double-nothing", title = "Double or Nothing", giver = HouseAshgrave, job = Job.Climb, count = 15, rank = 2, sparks = 220, xp = 260,
            brief = "Fifteen floors. My own cousin, A-Rank, never passed fourteen. If you do, I shall say so in public. And pay.",
            thanks = "Fifteen. Past my cousin. I said I would say it in public, so: I was wrong about you. There. Do not make me say it twice.",
        },
    };

    public static string MissionId(int index) => index >= 0 && index < Missions.Length ? Missions[index].id : "";
    public static int MissionIndex(string id) => Array.FindIndex(Missions, m => m.id == id);

    string boardGiver = GuildDesk;      // whose board is open: the Guild's, or one of the houses'
    int guildTab;                       // Guild hall: 0 commissions, 1 heroes
    bool missionAnnounced;

    Mission ActiveMission => Save.Mission >= 0 && Save.Mission < Missions.Length ? Missions[Save.Mission] : null;

    // How far along a commission is. Climbing and exploring are read off the save itself,
    // so floors cleared and regions found before taking the job still count.
    static int MissionProgress(Mission m)
    {
        if (m.job == Job.Climb) return Mathf.Min(m.count, Save.BestFloor);
        if (m.job == Job.Explore)
        {
            int found = 0;
            for (int i = 0; i < 16; i++) if ((Save.RegionMask & (1 << i)) != 0) found++;
            return Mathf.Min(MissionGoal(m), found);
        }
        return Mathf.Min(MissionGoal(m), Save.MissionCount);
    }

    static int MissionGoal(Mission m) => m.allRegions ? RegionDefinitionCount : m.job == Job.Chief || m.job == Job.Recover ? 1 : m.count;

    bool MissionReady => ActiveMission != null && Save.MissionStage(ActiveMission.id) == CommissionStage.Ready;
    public static bool CanAcceptObjective(string id, int rank) => Array.Exists(Missions, m => m.id == id && rank >= m.rank);
    public static bool ObjectiveReady(string id) => Array.Exists(Missions, m => m.id == id && MissionProgress(m) >= MissionGoal(m));
    public static bool ObjectiveEligible(QuestEvent e)
    {
        Mission m = Array.Find(Missions, item => item.id == e.quest);
        if (m == null || RegionIndex(e.region) < 0 || m.regionId.Length > 0 && m.regionId != e.region) return false;
        if (e.cause == ObjectiveCause.Recover) return m.job == Job.Recover && m.targetId == e.target;
        if (e.cause != ObjectiveCause.Kill) return false;
        return m.job == Job.Bandits ? e.bandit : m.job == Job.Monsters ? !e.bandit :
            m.job == Job.Chief && e.bandit && e.kind == Enemy.Kind.Brute && m.targetId == e.target;
    }

    static bool MissionFinished(int index) => Save.MissionPaid(MissionId(index));

    // Whether the Guild has a commission this hero may take now (for the town's next-step line).
    bool CommissionOnOffer()
    {
        int rank = RankIndex(Save.Level);
        for (int i = 0; i < Missions.Length; i++)
            if (!MissionFinished(i) && Missions[i].rank <= rank) return true;
        return false;
    }

    // One line saying what the work is, for the tracker and the board.
    string MissionTask(Mission m)
    {
        string where = m.region >= 0 ? " in " + RegionName(m.region) : " in the Wilds";
        switch (m.job)
        {
            case Job.Bandits: return "Defeat bandits" + (m.region >= 0 ? " from " + RegionName(m.region) : where);
            case Job.Monsters: return "Defeat monsters" + (m.region >= 0 ? " from " + RegionName(m.region) : where);
            case Job.Chief: return "Defeat " + m.target + where;
            case Job.Explore: return "Discover regions of the Wilds";
            case Job.Climb: return "Clear floor " + m.count + " of the tower";
            default: return "Recover " + m.target + where;
        }
    }

    // The very next thing to do for a commission, given where the hero is standing. Play-testers
    // took commissions and then did not know what they were, how to finish them or where to go.
    string MissionNext(Mission m)
    {
        if (MissionReady) return "Return to " + m.giver + " in Brushwick to be paid";
        if (m.job == Job.Climb)
            return InTown ? "Enter the Gloom Tower, north of the square" : "Clear floor " + m.count + " (highest cleared: " + Save.BestFloor + ")";
        if (InTown) return Save.BestFloor < 1 ? "First clear floor 1 of the tower: the south gate opens after that"
            : "Leave by the south gate into the Wilds";
        if (!InWilds) return "Go back to Brushwick, then out by the south gate";
        if (m.job == Job.Explore) return "Walk into regions you have not seen.  M: map";
        if (m.region >= 0 && regionNow != m.region) return "Travel to " + RegionName(m.region) + ".  M: map";
        if (m.job == Job.Chief) return "Find " + m.target + " in its camp";
        return m.job == Job.Recover ? "Search the camp here for " + m.target : "Fight here: each one counts";
    }

    void AcceptMission(int index)
    {
        if (index < 0 || index >= Missions.Length || !Save.AcceptCommission(MissionId(index))) return;
        missionAnnounced = false;
        ToonKit.Sfx("card");
        Mission m = Missions[index];
        SystemMessage("COMMISSION ACCEPTED", MissionTask(m) + "  (" + MissionGoal(m) + ").   Then report to " + m.giver + " for " + m.sparks + " Sparks."
            + (m.job == Job.Bandits || m.job == Job.Monsters ? "\nTargets count by their home region, wherever you chase them." : m.job == Job.Chief ? "\nOnly the named chief on this poster counts." : ""));
    }

    void AbandonMission()
    {
        if (!Save.AbandonCommission(Save.ActiveMissionId, Save.MissionAttempt)) return;
        ToonKit.Sfx("deny");
    }

    // Paid on the spot: Sparks in the purse, experience toward the next rank, and a word from the client.
    void TurnInMission()
    {
        Mission m = ActiveMission;
        if (m == null || !MissionReady || !InTown || DefeatPending || boardGiver != m.giver) return;
        int previousLevel = Save.Level;
        if (!Save.PayMission(m.id, m.sparks, m.xp)) return;
        PresentXp(previousLevel);
        OpenHubPanel(0);
        ToonKit.Sfx("epic");
        Flash(UI.Gold, 0.3f);
        string campaign = JourneyStore.Campaign;
        Say(Story.Thanks(m.giver, m.thanks), () =>
        {
            if (campaign != JourneyStore.Campaign) return;
            SetState(GameState.Hub);
            Banner("+" + m.sparks + " SPARKS", 2.4f, "Commission complete: " + m.title);
        });
    }

    // Called for every kill out in the Wilds.
    void MissionKill(Enemy e)
    {
        Mission m = ActiveMission;
        if (m == null || !InWilds || !Save.RecordObjective(e.ObjectiveEvent)) return;
    }

    // True when the purse covers the next rank of any forge upgrade.
    bool CanForge
    {
        get
        {
            for (int i = 0; i < Save.PerkCount; i++)
                if (Save.PerkCost(i) > 0 && Save.Sparks >= Save.PerkCost(i)) return true;
            return false;
        }
    }

    // Play-testers collected Sparks without learning what they buy: said once, the first time
    // there is something in the purse, and again the first time it is enough for the forge.
    void TickSparksTip()
    {
        if (TrainingActive) return;
        if (State != GameState.Playing && State != GameState.Hub) return;
        if (systemT > 0f) return;
        // Not in the middle of a fight: the message would sit on top of the action, unread.
        if (State == GameState.Playing && Enemy.All.Count > 0 && (InWilds ? Hunted() : waveActive)) return;
        if (Save.Sparks > 0 && JourneyStore.GetInt("tipSparks", 0) == 0)
        {
            JourneyStore.SetInt("tipSparks", 1);
            SystemMessage("SPARKS", "Colour you can carry. Bram the Smith in Brushwick hammers them into permanent upgrades.");
        }
        else if (CanForge && JourneyStore.GetInt("tipForge", 0) == 0)
        {
            JourneyStore.SetInt("tipForge", 1);
            SystemMessage("ENOUGH SPARKS", "You can afford an upgrade. Take them to Bram's forge in Brushwick before a fall halves them.");
        }
    }

    // Says so, once, the moment the work is done.
    void TickMission()
    {
        if (TrainingActive) return;
        if (missionAnnounced || ActiveMission == null || !MissionReady) return;
        if (State != GameState.Playing && State != GameState.Hub) return;
        missionAnnounced = true;
        Save.Flush();
        ToonKit.Sfx("epic");
        SystemMessage("COMMISSION COMPLETE", "Report to " + ActiveMission.giver + " in Brushwick for your pay.");
    }

    // The small tracker under the level bar: what you are doing and how far along you are.
    void DrawMission(float x, float y)
    {
        Mission m = ActiveMission;
        if (m == null) return;
        bool ready = MissionReady;
        var r = new Rect(x, y, 340, 84);
        UI.Panel(r, new Color(0.062f, 0.065f, 0.079f, 0.72f), true);
        UI.Fill(new Rect(r.x, r.y + 6, 4, r.height - 12), ready ? UI.Green : UI.Gold);
        UI.Text(new Rect(r.x + 14, r.y + 4, r.width - 20, 20), "COMMISSION   " + m.title, 14, ready ? UI.Green : UI.Gold, TextAnchor.MiddleLeft, false);
        string task = ready ? "Done" : MissionTask(m);
        UI.Text(new Rect(r.x + 14, r.y + 23, r.width - 20, 20), task, 13, Color.white, TextAnchor.MiddleLeft, false);
        UI.Text(new Rect(r.x + 14, r.y + 62, r.width - 20, 20), "NEXT   " + MissionNext(m), 13, ready ? UI.Green : UI.Gold, TextAnchor.MiddleLeft, false);
        if (ready) return;
        UI.Bar(new Rect(r.x + 14, r.y + 45, r.width - 90, 9), MissionProgress(m) / (float)MissionGoal(m), UI.Gold, UI.Ink);
        UI.Text(new Rect(r.xMax - 72, r.y + 39, 64, 20), MissionProgress(m) + " / " + MissionGoal(m), 14, Color.white, TextAnchor.MiddleRight, false);
    }

    // ------------------------------------------------------------ the board

    void OpenBoard(string giver)
    {
        boardGiver = giver;
        guildTab = 0;
        OpenHubPanel(giver == GuildDesk ? 2 : 3);
    }

    // The Guild hall: the commission board, and the roster of heroes.
    void DrawGuild(float w, float h, float cx)
    {
        Dim(w, h, 0.62f);
        UI.Text(new Rect(0, 26, w, 54), "PAINTERS' GUILD", 44, UI.Gold);
        int done = 0;
        for (int i = 0; i < Missions.Length; i++) if (MissionFinished(i)) done++;
        UI.Text(new Rect(0, 78, w, 26), Player.Hero.name + "     " + Rank(Save.Level) + "-RANK PAINTER     Lv. " + Save.Level + "     Commissions completed: " + done,
            17, Story.SystemC, TextAnchor.MiddleCenter, false);

        if (UI.Button(UI.Centered(cx - 130, 132, 240, 44), "COMMISSIONS", guildTab == 0 ? UI.Gold : new Color(0.4f, 0.4f, 0.5f), 20)) guildTab = 0;
        if (UI.Button(UI.Centered(cx + 130, 132, 240, 44), "HEROES", guildTab == 1 ? UI.Blue : new Color(0.4f, 0.4f, 0.5f), 20)) guildTab = 1;

        if (guildTab == 0) DrawBoard(w, h, cx, 170f);
        else DrawHeroPicker(h);
        if (UI.Button(UI.Centered(cx, h - 44, 220, 48), "LEAVE", UI.Red)) OpenHubPanel(0);
    }

    // A noble's own commissions: fewer, better paid.
    void DrawHouse(float w, float h, float cx)
    {
        Dim(w, h, 0.62f);
        bool vane = boardGiver == HouseVane;
        UI.Text(new Rect(0, 40, w, 54), vane ? "HOUSE VANE" : "HOUSE ASHGRAVE", 44, UI.Purple);
        UI.Text(new Rect(0, 94, w, 26), vane ? "\"Useful people are remembered.\"" : "\"An Ashgrave pays his debts.\"", 18, new Color(0.86f, 0.8f, 1f), TextAnchor.MiddleCenter, false);
        DrawBoard(w, h, cx, 140f);
        if (UI.Button(UI.Centered(cx, h - 44, 220, 48), "LEAVE", UI.Red)) OpenHubPanel(0);
    }

    // The commission in hand (if this client can settle it), then what else they have posted.
    void DrawBoard(float w, float h, float cx, float y)
    {
        float width = Mathf.Min(940f, w - 60f);
        Mission active = ActiveMission;
        int rank = RankIndex(Save.Level);

        if (active != null)
        {
            bool ready = MissionReady, here = active.giver == boardGiver;
            var card = UI.Centered(cx, y + 50, width, 106);
            UI.Framed(card, new Color(0.123f, 0.129f, 0.157f, 0.97f), ready ? UI.Green : UI.Gold, 3f);
            UI.Text(new Rect(card.x + 22, card.y + 8, 400, 22), "IN HAND" + (here ? "" : "   (for " + active.giver + ")"), 14, ready ? UI.Green : UI.Gold, TextAnchor.MiddleLeft, false);
            UI.Text(new Rect(card.x + 22, card.y + 28, width - 280, 30), active.title, 24, Color.white, TextAnchor.MiddleLeft);
            UI.Text(new Rect(card.x + 22, card.y + 60, width - 300, 24), MissionTask(active) + "     " + MissionProgress(active) + " / " + MissionGoal(active), 16,
                new Color(0.86f, 0.86f, 0.95f), TextAnchor.MiddleLeft, false);
            var act = new Rect(card.xMax - 236, card.y + 14, 214, 44);
            if (ready && here)
            {
                if (UI.Button(act, "COLLECT  " + active.sparks, UI.Green, 20)) TurnInMission();
            }
            else UI.Text(act, ready ? "Report to the client" : "In progress", 17, ready ? UI.Green : Color.white);
            UI.Text(new Rect(card.x + 22, card.y + 80, width - 260, 18), "NEXT   " + MissionNext(active), 13, ready ? UI.Green : UI.Gold, TextAnchor.MiddleLeft, false);
            if (UI.Button(new Rect(card.xMax - 176, card.y + 64, 154, 28), "Abandon", new Color(0.42f, 0.36f, 0.46f), 14)) AbandonMission();
            y += 112f;
        }
        else if (Save.ActiveMissionId.Length > 0)
        {
            var missing = UI.Centered(cx, y + 40, width, 80);
            UI.Panel(missing, UI.Ink);
            UI.Text(new Rect(missing.x + 20, missing.y + 8, width - 220, 64), "Your saved commission is unavailable in this content. Its records are kept; abandon it to accept another.", 15, UI.Gold, TextAnchor.MiddleLeft, false);
            if (UI.Button(new Rect(missing.xMax - 190, missing.y + 20, 170, 40), "ABANDON", UI.Red, 16)) AbandonMission();
            y += 88f;
        }

        int shown = 0, rows = active != null ? 3 : 4;
        for (int i = 0; i < Missions.Length && shown < rows; i++)
        {
            Mission m = Missions[i];
            if (m.giver != boardGiver || MissionFinished(i) || m == active) continue;
            bool allowed = rank >= m.rank;
            var row = UI.Centered(cx, y + 46 + shown * 100, width, 92);
            UI.Framed(row, new Color(0.107f, 0.112f, 0.136f, allowed ? 0.96f : 0.7f), UI.Ink, 3f);

            // Rank stamp.
            Color stamp = Rainbow[Mathf.Min(m.rank, Rainbow.Length - 1)];
            UI.Tex(new Rect(row.x + 16, row.y + 16, 60, 60), UI.Circle, UI.Ink);
            UI.Tex(new Rect(row.x + 20, row.y + 20, 52, 52), UI.Circle, allowed ? stamp : new Color(0.35f, 0.33f, 0.42f));
            UI.Text(new Rect(row.x + 16, row.y + 16, 60, 60), RankNames[m.rank], 30, Color.white);

            UI.Text(new Rect(row.x + 92, row.y + 8, width - 360, 28), m.title, 21, allowed ? Color.white : new Color(0.6f, 0.6f, 0.68f), TextAnchor.MiddleLeft);
            UI.Text(new Rect(row.x + 92, row.y + 36, width - 350, 46), m.brief, 14, new Color(0.84f, 0.84f, 0.93f), TextAnchor.UpperLeft, false);
            UI.Text(new Rect(row.xMax - 246, row.y + 8, 226, 24), m.sparks + " Sparks     " + m.xp + " XP", 16, UI.Gold, TextAnchor.MiddleRight, false);

            var take = new Rect(row.xMax - 196, row.y + 38, 176, 42);
            if (!allowed) UI.Text(take, RankNames[m.rank] + "-RANK ONLY", 16, new Color(0.7f, 0.6f, 0.75f));
            else if (UI.Button(take, "ACCEPT", UI.Gold, 20, Save.ActiveMissionId.Length == 0))
            {
                AcceptMission(i);
                return;
            }
            shown++;
        }
        if (shown == 0 && active == null)
            UI.Text(new Rect(0, y + 60, w, 40), "Nothing posted. Every commission here is done.", 22, Color.white);
        else if (active != null && shown > 0)
            UI.Text(new Rect(0, h - 96, w, 24), "One commission at a time: finish or abandon the one in hand to take another.", 15, new Color(0.8f, 0.8f, 0.9f), TextAnchor.MiddleCenter, false);
    }
}
