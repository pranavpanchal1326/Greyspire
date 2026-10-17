using System;
using UnityEngine;

// One source of truth for effective power. Legacy records are retained; caps
// affect calculation and new allocations, never erase earned save entries.
public static class GrowthPolicy
{
    public const int MaxLevel = 60, MaxStat = 60, DailyPointBudget = 20, XpPerSpark = 20, SparksPerPoint = 25;
    public const float MaxClipPace = 2f, MaxAttackSpeed = 1.8f, MaxMoveSpeed = 1.08f, MaxDamage = 100f, MaxCrit = 0.75f;
    public static int Rank(int value) => Mathf.Clamp(value, 0, MaxStat);
    public static int Level(int value) => Mathf.Clamp(value, 1, MaxLevel);
    public static int CoreXp(int score, int tier, float night) => Mathf.Max(1, Mathf.RoundToInt(Mathf.Max(1f, score * 0.5f) * (1f + 0.1f * (Mathf.Max(1, tier) - 1)) * (1f + 0.25f * Mathf.Clamp01(night))));
    public static int StyleSparks(int coinValue, float multiplier) => Mathf.Max(0, Mathf.RoundToInt(Mathf.Max(0, coinValue) * (Mathf.Clamp(multiplier, 1f, 2f) - 1f)));
    static float Bonus(float value, float limit) => value <= 1f ? value : 1f + (value - 1f) / (1f + (value - 1f) / (limit - 1f));
    public static PlayerStats Build(int level, int[] allocation, int[] perks, int[] boons, HeroDef hero)
    {
        var stats = new PlayerStats();
        stats.maxHearts += Mathf.Clamp(perks[0], 0, Save.PerkMaxRank);
        stats.damage *= 1f + 0.1f * Mathf.Clamp(perks[1], 0, Save.PerkMaxRank);
        stats.moveSpeed += 0.05f * Mathf.Clamp(perks[2], 0, Save.PerkMaxRank);
        stats.coinMult += 0.2f * Mathf.Clamp(perks[3], 0, Save.PerkMaxRank);
        hero?.tune?.Invoke(stats);
        int str = Rank(allocation[0]), agi = Rank(allocation[1]), vit = Rank(allocation[2]), sen = Rank(allocation[3]);
        stats.damage *= (1f + 0.02f * (Level(level) - 1)) * (1f + PlayerController.StrDamage * str);
        stats.attackSpeed *= 1f + PlayerController.AgiAttack * agi;
        stats.moveSpeed += PlayerController.AgiMove * agi;
        stats.dashCooldown *= Mathf.Max(0.6f, 1f - 0.01f * agi);
        stats.maxHearts += vit / PlayerController.VitPerHeart;
        stats.crit += PlayerController.SenCrit * sen;
        stats.burstGain *= 1f + PlayerController.SenBurst * sen;
        stats.coinMult += 0.03f * sen;
        stats.dashStrike = level >= Game.SkillLevels[Game.SkillDashStrike];
        foreach (Upgrade u in Upgrades.All)
            // A record from before a boon existed has no entry for it: untaken.
            for (int i = 0; i < Mathf.Clamp(u.id < boons.Length ? boons[u.id] : 0, 0, u.max); i++) u.apply(stats);
        Finalize(stats);
        return stats;
    }
    public static void Finalize(PlayerStats stats)
    {
        // Keep early damage intact; late stacks approach the cap smoothly.
        float knee = MaxDamage * 0.5f;
        if (stats.damage > knee) stats.damage = knee + (stats.damage - knee) / (1f + (stats.damage - knee) / knee);
        stats.damage = Mathf.Clamp(stats.damage, 1f, MaxDamage);
        stats.attackSpeed = Mathf.Clamp(Bonus(stats.attackSpeed, MaxAttackSpeed), 0.6f, MaxAttackSpeed);
        stats.moveSpeed = Mathf.Clamp(Bonus(stats.moveSpeed, MaxMoveSpeed), 0.6f, MaxMoveSpeed);
        stats.critMultiplier = 2f + Mathf.Max(0f, stats.crit - MaxCrit);
        stats.crit = Mathf.Clamp(stats.crit, 0f, MaxCrit);
        stats.maxHearts = Mathf.Clamp(stats.maxHearts, 1, 30);
        stats.reach = Mathf.Clamp(stats.reach, 0.5f, 2f);
        stats.dashCooldown = Mathf.Clamp(stats.dashCooldown, 0.2f, 2f);
        stats.burstGain = Mathf.Clamp(stats.burstGain, 0.1f, 20f);
        stats.coinMult = Mathf.Clamp(stats.coinMult, 0.1f, 5f);
        stats.magnet = Mathf.Clamp(stats.magnet, 0.5f, 15f);
        stats.secondWinds = Mathf.Clamp(stats.secondWinds, 0, 2);
    }
    public readonly struct ContactPlan
    {
        public readonly float duration, clipSpeed, clipContact, fraction, power;
        public ContactPlan(float requested, float authoredContact, float fraction)
        {
            this.fraction = Mathf.Clamp(fraction, 0.1f, 0.9f);
            duration = Mathf.Max(0.12f, requested, authoredContact / (MaxClipPace * this.fraction));
            clipContact = authoredContact;
            clipSpeed = Mathf.Clamp(authoredContact / (duration * this.fraction), 0.1f, MaxClipPace);
            // Tempo that cannot be shown becomes bounded impact, rather than invisible hits.
            power = Mathf.Clamp(duration / Mathf.Max(0.12f, requested), 1f, 1.5f);
        }
    }
}
