using System;
using UnityEngine;

// The save: the hero's level and stats, how far up the tower they are, what the
// world remembers (the day, who has been met, who has been rescued), and settings.
public static partial class Save
{
    public const int PerkCount = 4;
    public const int PerkMaxRank = 3;

    public static readonly string[] PerkNames = { "Vitality", "Might", "Swiftness", "Fortune" };
    public static readonly string[] PerkDescs =
    {
        "+1 starting heart per rank",
        "+10% sword damage per rank",
        "+5% move speed per rank",
        "+20% Sparks found per rank",
    };
    static readonly int[] PerkCosts = { 25, 60, 120 };

    public static int Sparks
    {
        get => JourneyStore.GetInt("sparks", 0);
        set => JourneyStore.SetInt("sparks", Mathf.Max(0, value));
    }

    public static int BestScore
    {
        get => JourneyStore.GetInt("best", 0);
        set => JourneyStore.SetInt("best", value);
    }

    public static int Runs
    {
        get => JourneyStore.GetInt("runs", 0);
        set => JourneyStore.SetInt("runs", value);
    }

    public static int Wins
    {
        get => JourneyStore.GetInt("wins", 0);
        set => JourneyStore.SetInt("wins", value);
    }

    // Gloom Tiers: harder, richer versions of the run, unlocked one win at a time.
    public const int MaxGloom = 5;

    public static int GloomUnlocked
    {
        get => JourneyStore.GetInt("gloomMax", 0);
        set => JourneyStore.SetInt("gloomMax", Mathf.Clamp(value, 0, MaxGloom));
    }

    public static int GloomTier
    {
        get => Mathf.Clamp(JourneyStore.GetInt("gloom", 0), 0, GloomUnlocked);
        set => JourneyStore.SetInt("gloom", Mathf.Clamp(value, 0, GloomUnlocked));
    }

    // Bit per recovered Echo (story memory) and per ending seen.
    public static int EchoMask
    {
        get => JourneyStore.GetInt("echoes", 0);
        set => JourneyStore.SetInt("echoes", value);
    }

    public static int EndingMask
    {
        get => JourneyStore.GetInt("endings", 0);
        set => JourneyStore.SetInt("endings", value);
    }

    // The hero's permanent level (the "System"), and the highest floor ever cleared.
    public static int Level
    {
        get => Mathf.Max(1, JourneyStore.GetInt("level", 1));
        set => JourneyStore.SetInt("level", value);
    }

    public static int Xp
    {
        get => JourneyStore.GetInt("xp", 0);
        set => JourneyStore.SetInt("xp", value);
    }

    public static int BestFloor
    {
        get => JourneyStore.GetInt("bestFloor", 0);
        set => JourneyStore.SetInt("bestFloor", value);
    }

    // The climb in progress: how many floors of the tower are cleared this time round. The
    // tower is climbed one floor at a time, and the next visit starts on the floor after this.
    public static int TowerFloor
    {
        get => JourneyStore.HasKey("towerFloor") ? JourneyStore.GetInt("towerFloor") : BestFloor;
        set => JourneyStore.SetInt("towerFloor", Mathf.Max(0, value));
    }

    // The highest floor whose arrival scene, and whose clearing scene, has been played.
    public static int StoryFloor
    {
        get => JourneyStore.GetInt("storyFloor", 0);
        set => JourneyStore.SetInt("storyFloor", value);
    }

    public static int ClearStory
    {
        get => JourneyStore.GetInt("clearStory", 0);
        set => JourneyStore.SetInt("clearStory", value);
    }

    // One bit per drained Painter brought back out of the tower.
    public static int RescuedMask
    {
        get => ObjectiveMask("rescue", "rescued", Story.Rescues.Length, i => Story.Rescues[i].id);
        set => SetObjectiveMask("rescue", "rescued", value, Story.Rescues.Length, i => Story.Rescues[i].id);
    }

    // The world's clock: which day it is, and the hour of it (0 to 24).
    public static int Day
    {
        get => Mathf.Max(1, JourneyStore.GetInt("day", 1));
        set => JourneyStore.SetInt("day", value);
    }

    public static float Hour
    {
        get => JourneyStore.GetFloat("hour", 8f);
        set => JourneyStore.SetFloat("hour", value);
    }

    // Stat points: three a level, spent in the Status window on the four stats.
    public const int StatCount = 4;

    public static int StatPoints
    {
        get
        {
            // A save from before stat points existed is owed the points for the levels it has.
            if (!JourneyStore.HasKey("statPoints")) JourneyStore.SetInt("statPoints", (int)Math.Min(int.MaxValue, 3L * (Level - 1)));
            return JourneyStore.GetInt("statPoints", 0);
        }
        set => JourneyStore.SetInt("statPoints", Mathf.Max(0, value));
    }

    public static int Stat(int index) => JourneyStore.GetInt("stat" + index, 0);

    public static void SetStat(int index, int value) => JourneyStore.SetInt("stat" + index, value);

    // Guardian's Boons: how many times each one has been taken. They are kept for good.
    public static int Boon(int id) => JourneyStore.GetInt("boon" + id, 0);

    public static void SetBoon(int id, int taken) => JourneyStore.SetInt("boon" + id, taken);

    // The System's daily quest: the day it was issued for, kills so far, and whether it is paid.
    public static int DailyDay
    {
        get => JourneyStore.GetInt("dailyDay", 0);
        set => JourneyStore.SetInt("dailyDay", value);
    }

    public static int DailyCount
    {
        get => JourneyStore.GetInt("dailyCount", 0);
        set => JourneyStore.SetInt("dailyCount", value);
    }

    public static bool DailyDone
    {
        get => JourneyStore.GetInt("dailyDone", 0) == 1;
        set => JourneyStore.SetInt("dailyDone", value ? 1 : 0);
    }
    public static int DailyGrowth => Mathf.Clamp(JourneyStore.GetInt("dailyGrowth"), 0, GrowthPolicy.DailyPointBudget);
    public static bool AwardDailyPoint() => JourneyStore.Transaction(() =>
    {
        if (DailyGrowth >= GrowthPolicy.DailyPointBudget) return false;
        JourneyStore.SetInt("dailyGrowth", DailyGrowth + 1);
        StatPoints = checked(StatPoints + 1);
        return true;
    });

    public readonly struct DailyReward
    {
        public readonly bool complete, point;
        public readonly int sparks;
        public DailyReward(bool point, int sparks) { complete = true; this.point = point; this.sparks = sparks; }
    }
    public static DailyReward RecordDailyKill(int goal, int sparks) => JourneyStore.Transaction(() =>
    {
        if (goal <= 0 || sparks < 0) throw new ArgumentOutOfRangeException();
        if (DailyDay != Day) { DailyDay = Day; DailyCount = 0; DailyDone = false; }
        if (DailyDone) return default(DailyReward);
        DailyCount = checked(DailyCount + 1);
        if (DailyCount < goal) return default(DailyReward);
        DailyDone = true;
        bool point = AwardDailyPoint();
        Sparks = checked(Sparks + sparks);
        return new DailyReward(point, sparks);
    });
    public static bool AllocateStat(int stat)
    {
        return JourneyStore.Transaction(() =>
        {
            if (stat < 0 || stat >= StatCount || StatPoints <= 0 || Stat(stat) >= GrowthPolicy.MaxStat) return false;
            SetStat(stat, checked(Mathf.Max(0, Stat(stat)) + 1)); StatPoints--;
            return true;
        });
    }
    public static bool RespecStats()
    {
        return JourneyStore.Transaction(() =>
        {
            long refund = StatPoints;
            for (int i = 0; i < StatCount; i++) refund += Math.Max(0, Stat(i));
            if (refund > int.MaxValue) throw new OverflowException("Point refund exceeds the saved point range");
            for (int i = 0; i < StatCount; i++) SetStat(i, 0);
            StatPoints = (int)refund;
            return true;
        });
    }
    public static bool ConvertPoint()
    {
        return JourneyStore.Transaction(() =>
        {
            if (StatPoints <= 0) return false;
            Sparks = checked(Sparks + GrowthPolicy.SparksPerPoint); StatPoints--;
            return true;
        });
    }

    // Is there a journey to continue?
    public static bool HasProgress => SeenIntro || Level > 1 || Xp > 0 || BestFloor > 0 || Sparks > 0;

    // People the hero has been introduced to. The names are kept in one list so a new game can forget them.
    public static bool Met(string who) => JourneyStore.GetInt("met" + who, 0) == 1;

    public static void SetMet(string who)
    {
        if (Met(who)) return;
        JourneyStore.SetInt("met" + who, 1);
        string all = JourneyStore.GetString("metAll", "");
        JourneyStore.SetString("metAll", all.Length == 0 ? who : all + "|" + who);
    }

    static readonly string[] Keys =
    {
        "sparks", "best", "runs", "wins", "gloomMax", "gloom", "echoes", "endings", "level", "xp", "bestFloor", "towerFloor",
        "storyFloor", "clearStory", "rescued", "day", "hour", "statPoints", "dailyDay", "dailyCount", "dailyDone", "pet",
        "regions", "chests", "mission", "missionCount", "missions", "intro", "heroPick", "metAll",
    };

    // Reset the complete journey namespace. Settings remain in PlayerPrefs.
    public static void Wipe() => JourneyStore.NewJourney();

    public static void Flush()
    {
        if (JourneyStore.InTransaction) return;
        JourneyStore.Flush();
        PlayerPrefs.Save();
    }

    // Import exactly once, without deleting the old registry data. The v1 snapshot
    // becomes authoritative, including after a new game or an empty journey.
    internal static void ImportLegacy()
    {
        foreach (string key in Keys)
        {
            if (!PlayerPrefs.HasKey(key)) continue;
            if (key == "hour") JourneyStore.SetFloat(key, PlayerPrefs.GetFloat(key));
            else if (key == "metAll") JourneyStore.SetString(key, PlayerPrefs.GetString(key));
            else JourneyStore.SetInt(key, PlayerPrefs.GetInt(key));
        }
        foreach (string key in new[] { "tipSparks", "tipForge" })
            if (PlayerPrefs.HasKey(key)) JourneyStore.SetInt(key, PlayerPrefs.GetInt(key));
        foreach (string who in PlayerPrefs.GetString("metAll", "").Split('|'))
            if (who.Length > 0) JourneyStore.SetInt("met" + who, PlayerPrefs.GetInt("met" + who));
        foreach (string who in new[] { "Bram the Smith", "Sol the Archivist", "Nix of the Guild", "Lady Aurelia Vane", "Lord Corvin Ashgrave" })
            if (PlayerPrefs.HasKey("met" + who)) JourneyStore.SetInt("met" + who, PlayerPrefs.GetInt("met" + who));
        for (int i = 0; i < 16; i++)
            foreach (string family in new[] { "perk", "hero", "stat", "boon" })
                if (PlayerPrefs.HasKey(family + i)) JourneyStore.SetInt(family + i, PlayerPrefs.GetInt(family + i));
        for (int floor = 0; floor <= 30; floor++)
            if (PlayerPrefs.HasKey("tc" + floor)) JourneyStore.SetInt("tc" + floor, PlayerPrefs.GetInt("tc" + floor));
        for (int i = 0; i < Story.Rescues.Length; i++)
        {
            if (PlayerPrefs.HasKey("rescueAsk" + i)) JourneyStore.SetInt("rescueAsk" + i, PlayerPrefs.GetInt("rescueAsk" + i));
            // Old saves do not identify the source of owned boons/commission XP.
            // Preserve their paid state; never invent an ambiguous second reward.
            if ((RescuedMask & (1 << i)) != 0 && RescueChoice(i) == 0)
                JourneyStore.SetInt("rescuePending" + i, 1);
        }
        for (int tier = 0; tier <= GloomTier; tier++)
            for (int floor = 5; floor < Game.TowerFloors; floor += 5)
                if (tier < GloomTier || floor <= TowerFloor)
                    JourneyStore.SetInt(GuardianKey(tier, floor) + ":paid", 1);
        MigrateMissions();
    }

    // Mop the tame slime has joined.
    public static bool Pet
    {
        get => JourneyStore.GetInt("pet", 0) == 1;
        set => JourneyStore.SetInt("pet", value ? 1 : 0);
    }

    // The Wilds: one bit per region discovered and per chest opened.
    public static int RegionMask
    {
        get => ObjectiveMask("region", "regions", Game.RegionDefinitionCount, Game.RegionId);
        set => SetObjectiveMask("region", "regions", value, Game.RegionDefinitionCount, Game.RegionId);
    }

    public static int ChestMask
    {
        get => ObjectiveMask("wildChest", "chests", Game.RegionDefinitionCount * 2, i => Game.RegionId(i / 2) + ":" + i % 2);
        set => SetObjectiveMask("wildChest", "chests", value, Game.RegionDefinitionCount * 2, i => Game.RegionId(i / 2) + ":" + i % 2);
    }

    // This v1 family is formally bounded. Expansion needs an explicit schema migration.
    static void ValidateTowerChest(int floor, int chest)
    {
        if (floor < 1 || floor > Game.TowerFloors || chest < 0 || chest >= 32) throw new ArgumentOutOfRangeException();
    }
    public static bool TowerChest(int floor, int chest)
    {
        ValidateTowerChest(floor, chest);
        return (JourneyStore.GetInt("tc" + floor, 0) & (1 << chest)) != 0;
    }
    public static void OpenTowerChest(int floor, int chest)
    {
        ValidateTowerChest(floor, chest);
        JourneyStore.SetInt("tc" + floor, JourneyStore.GetInt("tc" + floor, 0) | (1 << chest));
    }

    // Guild commissions: stable identity in storage, current array index at the UI boundary.
    public static int Mission
    {
        get => Game.MissionIndex(JourneyStore.GetString("missionId"));
        set
        {
            string id = value < 0 ? "" : Game.MissionId(value);
            JourneyStore.SetString("missionId", id);
            if (id.Length > 0 && MissionAttempt.Length == 0) JourneyStore.SetString("missionAttempt", Guid.NewGuid().ToString("N"));
        }
    }

    public static int MissionCount
    {
        get => Mathf.Max(0, ObjectivesMigrated ? JourneyStore.GetInt("mission:" + ActiveMissionId + ":count") : JourneyStore.GetInt("missionCount", 0));
        set
        {
            JourneyStore.SetInt("missionCount", value); // deprecated compatibility shadow
            if (ObjectivesMigrated && ActiveMissionId.Length > 0) JourneyStore.SetInt("mission:" + ActiveMissionId + ":count", Mathf.Max(0, value));
        }
    }

    public static bool SeenIntro
    {
        get => JourneyStore.GetInt("intro", 0) == 1;
        set => JourneyStore.SetInt("intro", value ? 1 : 0);
    }

    public static float Sensitivity
    {
        get => PlayerPrefs.GetFloat("sens", 0.5f);
        set => PlayerPrefs.SetFloat("sens", value);
    }

    public static float Volume
    {
        get => Mathf.Clamp01(PlayerPrefs.GetFloat("vol", 0.8f));
        set
        {
            PlayerPrefs.SetFloat("vol", Mathf.Clamp01(value));
            AudioListener.volume = Mathf.Clamp01(value);
        }
    }

    public static float CameraFov
    {
        get => Mathf.Clamp(PlayerPrefs.GetFloat("cameraFov", 64f), 55f, 85f);
        set => PlayerPrefs.SetFloat("cameraFov", Mathf.Clamp(value, 55f, 85f));
    }

    // One comfort control scales shake, impact shoves and changes in field of view.
    public static float CameraMotion
    {
        get => Mathf.Clamp01(PlayerPrefs.GetFloat("cameraMotion", 0.65f));
        set => PlayerPrefs.SetFloat("cameraMotion", Mathf.Clamp01(value));
    }

    public static bool InvertLook
    {
        get => PlayerPrefs.GetInt("invertLook", 0) == 1;
        set => PlayerPrefs.SetInt("invertLook", value ? 1 : 0);
    }

    // Graphics quality: 0 low, 1 medium, 2 high.
    public static int Quality
    {
        get => Mathf.Clamp(PlayerPrefs.GetInt("quality", 2), 0, 2);
        set => PlayerPrefs.SetInt("quality", Mathf.Clamp(value, 0, 2));
    }

    public static float MusicVolume
    {
        get => Mathf.Clamp01(PlayerPrefs.GetFloat("music", 0.5f));
        set => PlayerPrefs.SetFloat("music", Mathf.Clamp01(value));
    }

    public static int PerkRank(int perk) => JourneyStore.GetInt("perk" + perk, 0);

    // Cost of the next rank, or -1 when maxed.
    public static int PerkCost(int perk)
    {
        if (perk < 0 || perk >= PerkCount) return -1;
        int rank = Mathf.Clamp(PerkRank(perk), 0, PerkMaxRank);
        return rank >= PerkMaxRank ? -1 : PerkCosts[rank];
    }

    public static bool BuyPerk(int perk)
    {
        if (perk < 0 || perk >= PerkCount) return false;
        return JourneyStore.Transaction(() =>
        {
            int cost = PerkCost(perk);
            if (cost < 0 || Sparks < cost) return false;
            Sparks -= cost;
            JourneyStore.SetInt("perk" + perk, PerkRank(perk) + 1);
            return true;
        });
    }
}
