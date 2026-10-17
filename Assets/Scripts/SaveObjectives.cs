using System;
using UnityEngine;

public enum CommissionStage { Available, Active, Ready, Paid }
public enum ObjectiveCause { Kill, Recover }

// Immutable event metadata: birthplace and named identity survive movement,
// display-name changes and split children. Attempt/expedition fence stale work.
public readonly struct QuestEvent
{
    public readonly string quest, attempt, expedition, eventId, region, target;
    public readonly ObjectiveCause cause;
    public readonly bool bandit;
    public readonly Enemy.Kind kind;
    public QuestEvent(string quest, string attempt, string expedition, string eventId, string region, string target,
        ObjectiveCause cause, bool bandit = false, Enemy.Kind kind = Enemy.Kind.Slime)
    {
        this.quest = quest; this.attempt = attempt; this.expedition = expedition; this.eventId = eventId;
        this.region = region; this.target = target; this.cause = cause; this.bandit = bandit; this.kind = kind;
    }
}

public static partial class Save
{
    // Frozen v1 interpretation. Never use the current definition order here.
    internal static readonly string[] LegacyRegions = { "greenreach", "whispering-woods", "mirror-lake", "sunscorch", "hollow-graves", "shardfield", "goldleaf", "sunken-fen", "frostfall", "emberscar" };
    internal static readonly string[] LegacyRescues = { "rhea", "dorn", "kael", "imel", "oswin" };
    internal static readonly string[] LegacyHeroes = { "pip", "bram", "nix", "sol", "cedric" };
    public static bool ObjectivesMigrated => JourneyStore.GetInt("objectiveVersion") == 1;
    internal static void InitializeObjectives() => JourneyStore.SetInt("objectiveVersion", 1);
    internal static string OwnedKey(string family, string id) => family + ":" + id + ":owned";
    internal static int ObjectiveMask(string family, string oldKey, int count, Func<int, string> id)
    {
        if (count > 32) throw new InvalidOperationException("Mask facade capacity exceeded");
        if (!ObjectivesMigrated) return JourneyStore.GetInt(oldKey);
        int mask = 0;
        for (int i = 0; i < count; i++) if (JourneyStore.GetInt(OwnedKey(family, id(i))) == 1) mask |= 1 << i;
        return mask;
    }
    internal static void SetObjectiveMask(string family, string oldKey, int value, int count, Func<int, string> id)
    {
        if (!ObjectivesMigrated) { JourneyStore.SetInt(oldKey, value); return; }
        if (count > 32) throw new InvalidOperationException("Mask facade capacity exceeded");
        for (int i = 0; i < count; i++) JourneyStore.SetInt(OwnedKey(family, id(i)), (value & (1 << i)) != 0 ? 1 : 0);
    }
    internal static void MigrateObjectives()
    {
        if (ObjectivesMigrated) return;
        if (JourneyStore.GetInt("objectiveVersion") != 0) throw new NotSupportedException("Unsupported objective data revision; existing records are retained.");
        int regions = JourneyStore.GetInt("regions"), chests = JourneyStore.GetInt("chests"), rescued = JourneyStore.GetInt("rescued");
        for (int i = 0; i < LegacyRegions.Length; i++)
        {
            if ((regions & (1 << i)) != 0) JourneyStore.SetInt(OwnedKey("region", LegacyRegions[i]), 1);
            for (int chest = 0; chest < 2; chest++)
                if ((chests & (1 << (2 * i + chest))) != 0) JourneyStore.SetInt(OwnedKey("wildChest", LegacyRegions[i] + ":" + chest), 1);
        }
        for (int i = 0; i < LegacyRescues.Length; i++)
        {
            string id = LegacyRescues[i], key = "rescue:" + id;
            int choice = Mathf.Clamp(JourneyStore.GetInt("rescueAsk" + i), 0, 2);
            bool owned = (rescued & (1 << i)) != 0 || JourneyStore.GetInt("rescue:" + i + ":paid") == 1 || choice != 0 || JourneyStore.GetInt("rescuePending" + i) == 1;
            bool pending = JourneyStore.GetInt("rescuePending" + i) == 1 || owned && choice == 0;
            if (owned) { JourneyStore.SetInt(OwnedKey("rescue", id), 1); JourneyStore.SetInt(key + ":paid", 1); }
            if (!owned && !pending && choice == 0) continue;
            JourneyStore.SetInt(key + ":choice", choice); JourneyStore.SetInt(key + ":pending", pending ? 1 : 0);
            JourneyStore.SetString(key + ":token", CampaignToken());
            RecordRescueConsequence(id, choice);
            if (owned && choice != 0 && !pending) JourneyStore.SetInt("consequence:rescue:" + id + ":home", 1);
        }
        for (int i = 0; i < LegacyHeroes.Length; i++)
            if (JourneyStore.GetInt("hero" + i) == 1) JourneyStore.SetInt(OwnedKey("hero", LegacyHeroes[i]), 1);
        int selected = Mathf.Clamp(JourneyStore.GetInt("heroPick"), 0, LegacyHeroes.Length - 1);
        JourneyStore.SetString("heroSelected", LegacyHeroes[selected]);
        string active = ActiveMissionId;
        if (active.Length > 0)
        {
            JourneyStore.SetInt("mission:" + active + ":count", Mathf.Max(0, JourneyStore.GetInt("missionCount")));
            JourneyStore.SetString("missionAttempt", CampaignToken());
        }
        foreach (string id in LegacyMissionIds)
            if (MissionPaid(id)) JourneyStore.SetInt("consequence:commission:" + id + ":fulfilled", 1);
        // Raw v1 fields, including unknown reserved bits, stay in the snapshot.
        InitializeObjectives();
    }
    static string CampaignToken() => JourneyStore.Campaign + ":" + Guid.NewGuid().ToString("N");
    public static string ActiveMissionId => JourneyStore.GetString("missionId");
    public static string MissionAttempt => JourneyStore.GetString("missionAttempt");
    public static string ExpeditionId => JourneyStore.GetString("expeditionId");
    public static bool DiscoverRegion(string id, string expedition, bool pay) => JourneyStore.Transaction(() =>
    {
        if (Game.RegionIndex(id) < 0 || !ExpeditionActive || string.IsNullOrEmpty(expedition) || expedition != ExpeditionId || JourneyStore.GetInt(OwnedKey("region", id)) == 1) return false;
        JourneyStore.SetInt(OwnedKey("region", id), 1);
        if (pay) { CarriedSparks = checked(CarriedSparks + 8 + Game.RegionTier(id)); AwardXp(5); }
        return true;
    });
    public static bool WildChestOpened(string region, int slot) => JourneyStore.GetInt(OwnedKey("wildChest", region + ":" + slot)) == 1;
    public static bool OpenWildChest(string region, int slot, string expedition) => JourneyStore.Transaction(() =>
    {
        if (Game.RegionIndex(region) < 0 || slot < 0 || slot > 1 || !ExpeditionActive || string.IsNullOrEmpty(expedition) || expedition != ExpeditionId || WildChestOpened(region, slot)) return false;
        JourneyStore.SetInt(OwnedKey("wildChest", region + ":" + slot), 1);
        CarriedSparks = checked(CarriedSparks + 10 + Game.RegionTier(region) * 2); return true;
    });
    public static CommissionStage MissionStage(string id) => MissionPaid(id) ? CommissionStage.Paid :
        ActiveMissionId != id || string.IsNullOrEmpty(id) ? CommissionStage.Available :
        Game.ObjectiveReady(id) ? CommissionStage.Ready : CommissionStage.Active;
    public static bool AcceptCommission(string id) => JourneyStore.Transaction(() =>
    {
        if (ActiveMissionId.Length > 0 || MissionPaid(id) || !Game.CanAcceptObjective(id, Game.RankIndex(Level))) return false;
        JourneyStore.SetString("missionId", id); JourneyStore.SetString("missionAttempt", CampaignToken()); MissionCount = 0;
        return true;
    });
    public static bool AbandonCommission(string id, string attempt) => JourneyStore.Transaction(() =>
    {
        if (string.IsNullOrEmpty(id) || id != ActiveMissionId || attempt != MissionAttempt) return false;
        MissionCount = 0; JourneyStore.SetString("missionId", ""); JourneyStore.SetString("missionAttempt", "");
        return true;
    });
    public static bool RecordObjective(QuestEvent e) => JourneyStore.Transaction(() =>
    {
        if (string.IsNullOrEmpty(e.quest) || e.quest != ActiveMissionId || e.attempt != MissionAttempt || string.IsNullOrEmpty(e.attempt)
            || string.IsNullOrEmpty(e.expedition) || e.expedition != ExpeditionId || string.IsNullOrEmpty(e.eventId)
            || MissionStage(e.quest) != CommissionStage.Active || !Game.ObjectiveEligible(e)) return false;
        string key = "mission:" + e.quest;
        for (int i = 1; i <= MissionCount; i++) if (JourneyStore.GetString(key + ":event:" + i) == e.eventId) return false;
        int next = checked(MissionCount + 1);
        JourneyStore.SetString(key + ":event:" + next, e.eventId);
        JourneyStore.SetString(key + ":source:" + next, e.region + "/" + e.target);
        MissionCount = next;
        return true;
    });
    public static bool Consequence(string id) => JourneyStore.GetInt("consequence:" + id) == 1;
    static void RecordRescueConsequence(string id, int choice)
    {
        if (choice == 1 || choice == 2) JourneyStore.SetInt("consequence:rescue:" + id + (choice == 1 ? ":safe-return" : ":guardian-intel"), 1);
    }
    static string RescueId(int who) => who >= 0 && who < Story.Rescues.Length ? Story.Rescues[who].id : "";
    public static string RescueToken(string id) => JourneyStore.GetString("rescue:" + id + ":token");
    public static int RescueChoice(string id) => JourneyStore.GetInt("rescue:" + id + ":choice");
    public static bool RescuePending(string id) => JourneyStore.GetInt("rescue:" + id + ":pending") == 1;
    public static bool BeginRescue(string id) => JourneyStore.Transaction(() =>
    {
        if (Story.RescueIndex(id) < 0 || JourneyStore.GetInt(OwnedKey("rescue", id)) == 1) return false;
        JourneyStore.SetInt(OwnedKey("rescue", id), 1); StatPoints = checked(StatPoints + 2);
        JourneyStore.SetInt("rescue:" + id + ":pending", 1); JourneyStore.SetInt("rescue:" + id + ":paid", 1);
        JourneyStore.SetString("rescue:" + id + ":token", CampaignToken()); return true;
    });
    public static bool ResolveRescue(string id, int choice, string token) => JourneyStore.Transaction(() =>
    {
        if (!RescuePending(id) || choice < 0 || choice > 1 || RescueChoice(id) != 0 || string.IsNullOrEmpty(token) || token != RescueToken(id)) return false;
        JourneyStore.SetInt("rescue:" + id + ":choice", choice + 1); RecordRescueConsequence(id, choice + 1);
        PlaytestLog.Emit("rescue_choice", value: choice + 1, note: id); return true;
    });
    public static bool FinishRescue(string id, string token) => JourneyStore.Transaction(() =>
    {
        if (!RescuePending(id) || RescueChoice(id) == 0 || string.IsNullOrEmpty(token) || token != RescueToken(id)) return false;
        JourneyStore.SetInt("rescue:" + id + ":pending", 0); JourneyStore.SetInt("consequence:rescue:" + id + ":home", 1); return true;
    });
}
