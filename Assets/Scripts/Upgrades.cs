using System;
using System.Collections.Generic;
using UnityEngine;

// Everything about the hero that level, stats, perks and boons can change.
[Serializable]
public class PlayerStats
{
    public int maxHearts = 5;
    public float damage = 10f;
    public float attackSpeed = 1f;
    public float moveSpeed = 1f;
    public float crit = 0.05f;
    public float critMultiplier = 2f;
    public float reach = 1f;
    public float dashCooldown = 0.9f;
    public float magnet = 3.5f;
    public float burstGain = 1f;
    public float coinMult = 1f;
    public float thorns;
    public int lifestealEvery;
    public int secondWinds;
    public bool dashStrike;
    public bool shockFinisher;
    public bool gatebreaker;    // heavy blows stagger even the largest foes; ripostes hit harder
}

public enum Rarity { Common, Rare, Epic }

public class Upgrade
{
    public int id;              // the save format: a boon is remembered by this number
    public string name, desc;
    public Rarity rarity;
    public int max;
    public int taken;
    public int signature;       // a Guardian's own boon: offered only when that floor's Guardian falls (0 = any)
    public Action<PlayerStats> apply;

    public Color Color => rarity == Rarity.Epic ? UI.Gold : rarity == Rarity.Rare ? UI.Purple : UI.Blue;
}

// Guardian's Boons. Nothing here is handed out for walking up a staircase: one boon
// is offered each time a Floor Guardian falls for the first time, and it is kept for
// good. Each apply() runs once per time the boon has been taken, on fresh stats.
public static class Upgrades
{
    public static readonly List<Upgrade> All = new List<Upgrade>();

    static void Add(int id, string name, string desc, Rarity rarity, int max, Action<PlayerStats> apply, int signature = 0)
    {
        All.Add(new Upgrade { id = id, name = name, desc = desc, rarity = rarity, max = max, apply = apply, signature = signature, taken = Mathf.Min(max, Save.Boon(id)) });
    }

    public const int GatebreakerId = 14;
    public const float GatebreakerStagger = 0.8f, GatebreakerRiposte = 1.3f;

    // Rebuilds the pool from the save. The ids are the save format: never reuse one.
    public static void Reset()
    {
        All.Clear();
        Add(0, "Sharper Bristles", "+18% sword damage", Rarity.Common, 5, s => s.damage *= 1.18f);
        Add(1, "Quick Strokes", "+12% base tempo\nDiminishing returns; clips at most 2x", Rarity.Common, 4, s => s.attackSpeed *= 1.12f);
        Add(2, "Light Feet", "+8% base move speed\nDiminishing returns; final limit 108%", Rarity.Common, 3, s => s.moveSpeed += 0.08f);
        Add(3, "Big Heart", "+1 heart", Rarity.Common, 4, s => s.maxHearts++);
        Add(4, "Spark Magnet", "+60% pickup range\n+25% Sparks", Rarity.Common, 3, s => { s.magnet *= 1.6f; s.coinMult += 0.25f; });
        Add(5, "Keen Eye", "+12% critical chance\nAbove 75%, overflow strengthens crits", Rarity.Rare, 4, s => s.crit += 0.12f);
        Add(6, "Wide Arc", "+20% sword reach", Rarity.Rare, 3, s => s.reach += 0.2f);
        Add(7, "Quick Step", "Dash recharges 25% faster", Rarity.Rare, 2, s => s.dashCooldown *= 0.75f);
        Add(8, "Colour Surge", "Colour Burst charges 40% faster", Rarity.Rare, 3, s => s.burstGain *= 1.4f);
        Add(9, "Thorned Cloak", "When hurt, nearby enemies take heavy damage", Rarity.Rare, 3, s => s.thorns += 40f);
        Add(10, "Living Paint", "Heal 1 heart every 12 kills (8 at the second rank)", Rarity.Rare, 2,
            s => s.lifestealEvery = s.lifestealEvery == 0 ? 12 : 8);
        Add(11, "Shockwave Finisher", "Your spin finisher sends out a damaging shockwave", Rarity.Epic, 1, s => s.shockFinisher = true);
        Add(12, "Second Wind", "+1 rescue charge on the next expedition\nEach rises with 2 hearts; maximum 2", Rarity.Epic, 2, s => s.secondWinds++);
        Add(13, "Glass Brush", "+70% sword damage\n-2 hearts", Rarity.Epic, 1, s => { s.damage *= 1.7f; s.maxHearts -= 2; });
        // Grol the Gatekeeper's own: what you learn by bringing him down.
        Add(GatebreakerId, "Gatebreaker", "Heavy blows stagger even the largest foes\nRipostes deal 30% more", Rarity.Epic, 1,
            s => s.gatebreaker = true, ChapterProgress.GatekeeperFloor);
    }

    // Lays every boon the hero owns over a fresh set of stats.
    public static void Apply(PlayerStats stats)
    {
        if (All.Count == 0) Reset();
        foreach (Upgrade u in All)
            for (int i = 0; i < u.taken; i++) u.apply(stats);
    }

    public static void Take(Upgrade u)
    {
        var offer = Save.PendingGuardian;
        if (offer == null || !Save.TakeGuardian(offer.key, u.id)) return;
        Reset();
    }

    public static int Owned
    {
        get
        {
            int n = 0;
            foreach (Upgrade u in All) n += u.taken;
            return n;
        }
    }

    // A Guardian's offer: its own signature boon while the hero lacks it, then rolled ones.
    public static List<Upgrade> Offer(int floor, int count) => JourneyStore.Transaction(() =>
    {
        if (All.Count == 0) Reset();
        var picks = All.FindAll(u => u.signature == floor && floor > 0 && u.taken < u.max);
        if (picks.Count > count) picks.RemoveRange(count, picks.Count - count);
        picks.AddRange(RollCore(count - picks.Count));
        return picks;
    });

    // Picks distinct boons, weighted so rarer ones show up less often. Signature boons are
    // never rolled: only their own Guardian offers them.
    public static List<Upgrade> Roll(int count) => JourneyStore.Transaction(() => RollCore(count));
    static List<Upgrade> RollCore(int count)
    {
        var pool = new List<Upgrade>();
        foreach (var u in All)
            if (u.taken < u.max && u.signature == 0) pool.Add(u);

        var picks = new List<Upgrade>();
        while (picks.Count < count && pool.Count > 0)
        {
            float total = 0f;
            foreach (var u in pool) total += Weight(u);
            float roll = ProductionRandom.LootValue() * total;
            Upgrade chosen = pool[pool.Count - 1];
            foreach (var u in pool)
            {
                roll -= Weight(u);
                if (roll <= 0f) { chosen = u; break; }
            }
            picks.Add(chosen);
            pool.Remove(chosen);
        }
        return picks;
    }

    static float Weight(Upgrade u)
    {
        return u.rarity == Rarity.Epic ? 1.5f : u.rarity == Rarity.Rare ? 3f : 5f;
    }
}
