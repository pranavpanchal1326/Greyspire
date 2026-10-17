using System;
using System.Collections.Generic;
using UnityEngine;

public static partial class Save
{
    // Frozen migration table for the registry format. Display text/order can change
    // without changing these identities. Never reuse an ID for different content.
    static readonly string[] LegacyMissionIds =
    {
        "wolves-road", "slime-season", "first-steps", "lay-land", "knives-pines", "lake-monsters",
        "garrick", "tenth-floor", "sand-steel", "fen-monsters", "every-road", "hobb",
        "graves-walk", "twentieth-floor", "ember-hunt", "vane-signet", "sand-banner", "ashgrave-wager", "double-nothing",
    };

    internal static void MigrateMissions()
    {
        int active = JourneyStore.GetInt("mission", 0) - 1;
        if (active >= 0 && active < LegacyMissionIds.Length) JourneyStore.SetString("missionId", LegacyMissionIds[active]);
        int mask = JourneyStore.GetInt("missions");
        for (int i = 0; i < LegacyMissionIds.Length; i++)
            if ((mask & (1 << i)) != 0) JourneyStore.SetInt("mission:" + LegacyMissionIds[i] + ":paid", 1);
    }
    public static bool MissionPaid(string id) => JourneyStore.GetInt("mission:" + id + ":paid") == 1;

    // Mutates only journey data. Presentation happens after the complete transaction.
    public static int AwardXp(int amount)
    {
        if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
        return JourneyStore.Transaction(() =>
        {
            int before = Level;
            _ = StatPoints;
            long xp = Math.Max(0, Xp) + (long)amount;
            while (Level < GrowthPolicy.MaxLevel && xp >= Game.XpNeeded(Level))
            {
                xp -= Game.XpNeeded(Level); Level++;
                StatPoints = checked(StatPoints + 3);
            }
            if (Level >= GrowthPolicy.MaxLevel)
            {
                Sparks = checked(Sparks + (int)(xp / GrowthPolicy.XpPerSpark));
                xp %= GrowthPolicy.XpPerSpark;
            }
            Xp = checked((int)xp);
            return Level - before;
        });
    }

    public static bool PayMission(string id, int sparks, int xp)
    {
        if (sparks < 0 || xp < 0) throw new ArgumentOutOfRangeException();
        return JourneyStore.Transaction(() =>
        {
            if (string.IsNullOrEmpty(id) || JourneyStore.GetString("missionId") != id || MissionStage(id) != CommissionStage.Ready) return false;
            Sparks = checked(Sparks + sparks);
            AwardXp(xp);
            JourneyStore.SetInt("mission:" + id + ":paid", 1);
            JourneyStore.SetInt("consequence:commission:" + id + ":fulfilled", 1);
            JourneyStore.SetString("missionId", "");
            MissionCount = 0;
            return true;
        });
    }

    [Serializable]
    public class GuardianOffer
    {
        public string key;
        public int tier, floor, rerollCost = 10;
        public int[] choices;
    }
    internal static string GuardianKey(int tier, int floor) => "guardian:" + tier + ":" + floor;
    public static GuardianOffer PendingGuardian
    {
        get
        {
            for (int tier = 0; tier <= MaxGloom; tier++)
                for (int floor = 5; floor < Game.TowerFloors; floor += 5)
                {
                    string json = JourneyStore.GetString(GuardianKey(tier, floor) + ":offer");
                    if (json.Length > 0) return JsonUtility.FromJson<GuardianOffer>(json);
                }
            return null;
        }
    }
    public static bool GuardianPaid(int tier, int floor) => JourneyStore.GetInt(GuardianKey(tier, floor) + ":paid") == 1;
    static int[] ChoiceIds(List<Upgrade> choices) => choices.ConvertAll(u => u.id).ToArray();

    public static bool RecordFloorClear(int tier, int floor)
    {
        if (tier < 0 || tier > MaxGloom || floor < 1 || floor > Game.TowerFloors) throw new ArgumentOutOfRangeException();
        return JourneyStore.Transaction(() =>
        {
            bool first = floor > TowerFloor;
            if (first) TowerFloor = floor;
            BestFloor = Mathf.Max(BestFloor, floor);
            string key = GuardianKey(tier, floor);
            if (first && Game.GuardianFloor(floor) && !GuardianPaid(tier, floor) && !JourneyStore.HasKey(key + ":offer"))
            {
                var choices = Upgrades.Offer(floor, 3);
                if (choices.Count == 0)
                {
                    // An exhausted pool still has a disclosed, deterministic reward.
                    Sparks = checked(Sparks + 50);
                    JourneyStore.SetInt(key + ":paid", 1);
                }
                else JourneyStore.SetString(key + ":offer", JsonUtility.ToJson(new GuardianOffer
                    { key = key, tier = tier, floor = floor, choices = ChoiceIds(choices) }));
            }
            return first;
        });
    }

    public static List<Upgrade> GuardianCards(GuardianOffer offer)
    {
        var result = new List<Upgrade>();
        if (offer == null || offer.choices == null) return result;
        foreach (int id in offer.choices)
        {
            Upgrade u = Upgrades.All.Find(item => item.id == id);
            if (u == null || Boon(id) >= u.max)
                throw new InvalidOperationException("Pending Guardian choice is unavailable: " + id);
            result.Add(u);
        }
        return result;
    }

    public static bool TakeGuardian(string key, int id)
    {
        return JourneyStore.Transaction(() =>
        {
            var offer = PendingGuardian;
            Upgrade u = Upgrades.All.Find(item => item.id == id);
            if (offer == null || offer.key != key || u == null || Array.IndexOf(offer.choices, id) < 0 || Boon(id) >= u.max) return false;
            SetBoon(id, Boon(id) + 1);
            JourneyStore.SetInt(key + ":paid", 1);
            JourneyStore.SetString(key + ":offer", "");
            return true;
        });
    }

    public static bool RerollGuardian(string key, bool fromBank = false)
    {
        return JourneyStore.Transaction(() =>
        {
            var offer = PendingGuardian;
            if (offer == null || offer.key != key || (fromBank ? Sparks : CarriedSparks) < offer.rerollCost) return false;
            // A reroll changes the rolled cards; a Guardian's own boon stays on offer.
            var choices = Upgrades.Offer(offer.floor, 3);
            if (choices.Count == 0) return false;
            if (fromBank) Sparks -= offer.rerollCost;
            else CarriedSparks -= offer.rerollCost;
            offer.rerollCost += 10;
            offer.choices = ChoiceIds(choices);
            JourneyStore.SetString(key + ":offer", JsonUtility.ToJson(offer));
            return true;
        });
    }

    public static int RescueChoice(int who) => ObjectivesMigrated ? RescueChoice(RescueId(who)) : JourneyStore.GetInt("rescueAsk" + who);
    public static bool RescuePending(int who) => ObjectivesMigrated ? RescuePending(RescueId(who)) : JourneyStore.GetInt("rescuePending" + who) == 1;
    public static bool BeginRescue(int who) => BeginRescue(RescueId(who));
    public static bool ResolveRescue(int who, int choice) => ResolveRescue(RescueId(who), choice, RescueToken(RescueId(who)));
    public static bool FinishRescue(int who) => FinishRescue(RescueId(who), RescueToken(RescueId(who)));

    public static int CarriedSparks
    {
        get => JourneyStore.GetInt("carried");
        set => JourneyStore.SetInt("carried", Mathf.Max(0, value));
    }
    public static bool ExpeditionActive => JourneyStore.GetInt("expeditionActive") == 1;
    public static void BeginExpedition(bool wilds) => JourneyStore.Transaction(() =>
    {
        BankCarried();
        JourneyStore.SetString("expeditionId", Guid.NewGuid().ToString("N"));
        JourneyStore.SetInt("expeditionWilds", wilds ? 1 : 0);
        JourneyStore.SetInt("expeditionActive", 1);
    });
    public static void BankCarried() => JourneyStore.Transaction(() =>
    {
        Sparks = checked(Sparks + CarriedSparks);
        CarriedSparks = 0;
        JourneyStore.SetInt("expeditionActive", 0);
    });
    public static bool SettleExpedition(bool won, bool fell, int gloom)
    {
        return JourneyStore.Transaction(() =>
        {
            if (!ExpeditionActive) return false;
            bool wilds = JourneyStore.GetInt("expeditionWilds") == 1;
            // Preserve the game's rounding: Wilds kept floor(n/2); tower kept ceil(n/2).
            int banked = fell ? (wilds ? CarriedSparks / 2 : CarriedSparks - CarriedSparks / 2) : CarriedSparks;
            JourneyStore.SetInt("settledBanked", banked);
            JourneyStore.SetInt("settledLost", CarriedSparks - banked);
            Sparks = checked(Sparks + banked);
            CarriedSparks = 0;
            JourneyStore.SetInt("expeditionActive", 0);
            JourneyStore.SetString("settledId", JourneyStore.GetString("expeditionId"));
            JourneyStore.SetInt("settledGloomUnlocked", 0);
            if (!wilds)
            {
                Runs++;
                if (won) Wins++;
                if (won && gloom >= GloomUnlocked && gloom < MaxGloom)
                {
                    GloomUnlocked = gloom + 1;
                    JourneyStore.SetInt("settledGloomUnlocked", 1);
                }
            }
            return true;
        });
    }
    public static int SettledBanked => JourneyStore.GetInt("settledBanked");
    public static int SettledLost => JourneyStore.GetInt("settledLost");
    public static bool SettledGloomUnlocked => JourneyStore.GetInt("settledGloomUnlocked") == 1;
}
