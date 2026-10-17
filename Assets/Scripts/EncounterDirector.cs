using System.Collections.Generic;
using UnityEngine;

// What each enemy is for in a mixed fight (master plan section 7, package C02).
public enum EnemyRole
{
    Duelist,        // imps and sword bandits: circle, wait for an opening, strike, step out
    ShieldCarrier,  // holds a lane in front of a ranged ally; answers repeated frontal light attacks
    Skirmisher,     // slimes: work round the flanks while they wait their turn
    Artillery,      // throwers: keep their distance and need a clear line to shoot
    Caller,         // a ranged pack member whose call rallies the others; hit it to break the call
    Heavy,          // brutes: own the space in front of them with slow, dangerous slams
    Sapper,         // bombers: run in and blow up
    Guardian,       // the boss: its own move language, outside the director until package C03
}

// Coordinates every damaging commitment in a fight, so a crowd reads as a fight rather than
// a pile-up: a bounded number of attacks at once, never two starting in the same instant,
// and never more than one from where the hero cannot see it, which also waits longer and
// announces itself. Enemies that are not allowed to commit reposition instead.
public static class EncounterDirector
{
    public const float MinGap = 0.3f;           // seconds between the starts of two commitments
    public const float UnseenExtraWindup = 0.25f;
    public const int MaxUnseen = 1;
    public const float MinWindup = 0.35f;       // no commitment lands with less warning than this

    // Tests compare coordinated and uncoordinated fights; the game always coordinates.
    public static bool Enabled = true;

    // How much may be committed at once: grows with the tower, as the duelists' cap did.
    public static int Budget => Mathf.Clamp(2 + (Game.I != null ? Game.I.Floor : 1) / 8, 2, 4);

    public static int Weight(EnemyRole role) => role == EnemyRole.Heavy || role == EnemyRole.Sapper ? 2 : 1;

    struct Hold { public Enemy enemy; public int weight; public bool unseen; }
    static readonly List<Hold> holds = new List<Hold>();

    // Turn order: who has been kept waiting, since when, and when they last asked. Someone
    // kept waiting much longer than the asker goes first, so slow, heavy roles are never
    // starved by quick ones that keep refilling the slots.
    public const float YieldMargin = 0.75f, StillAsking = 0.6f;
    struct Wait { public float since, asked; public int weight; }
    static readonly Dictionary<Enemy, Wait> waits = new Dictionary<Enemy, Wait>();
    static float lastStart = -999f;

    // A bounded record of commitments, read by EncounterRoleTest.
    public struct Commit { public float at; public EnemyRole role; public int weight; public bool unseen, forced; public string spawnId; }
    public static readonly List<Commit> Log = new List<Commit>();
    const int LogLimit = 512;

    // Tests drive the director's clock directly; negative means the simulation clock.
    public static float ClockOverride = -1f;
    static float Now => ClockOverride >= 0f ? ClockOverride : GameClocks.SimulationTime;

    public static void Reset()
    {
        holds.Clear();
        waits.Clear();
        Log.Clear();
        lastStart = -999f;
    }

    static void Prune()
    {
        for (int i = holds.Count - 1; i >= 0; i--)
            if (holds[i].enemy == null || !holds[i].enemy.CombatEligible || !holds[i].enemy.gameObject.activeInHierarchy) holds.RemoveAt(i);
    }

    public static int Committed { get { Prune(); int n = 0; foreach (var h in holds) n += h.weight; return n; } }
    public static int UnseenCommitted { get { Prune(); int n = 0; foreach (var h in holds) if (h.unseen) n++; return n; } }
    public static bool Holds(Enemy e) { foreach (var h in holds) if (h.enemy == e) return true; return false; }
    public static IEnumerable<Enemy> UnseenAttackers() { Prune(); foreach (var h in holds) if (h.unseen) yield return h.enemy; }

    // Whether the hero can see this enemy: on screen, in front of the camera, and not far behind
    // the way the hero is looking.
    public static bool Unseen(Enemy e)
    {
        if (Game.I == null || Game.I.Cam == null) return false;
        Camera view = Game.I.Cam.GetComponent<Camera>();
        if (view == null) return false;
        Vector3 vp = view.WorldToViewportPoint(e.transform.position + Vector3.up * Mathf.Min(1.2f, e.height * 0.6f));
        return vp.z <= 0f || vp.x < 0.04f || vp.x > 0.96f || vp.y < 0.04f || vp.y > 0.96f;
    }

    // Whether a commitment would be granted now, without taking it. Asking counts as waiting
    // for a turn, so an enemy that holds back until it is allowed (a brute) still earns priority.
    public static bool Allows(Enemy e, EnemyRole role)
    {
        if (!Enabled || Holds(e)) return true;
        float since = waits.TryGetValue(e, out Wait mine) ? mine.since : Now;
        waits[e] = new Wait { since = since, asked = Now, weight = Weight(role) };
        return Committed + Weight(role) <= Budget && Now - lastStart >= MinGap && (!Unseen(e) || UnseenCommitted < MaxUnseen) && !Yields(e, since);
    }

    // Asks to start a damaging commitment. A forced one (a bomber's fuse, a reaction already
    // under way) always goes ahead but still occupies the budget, so others wait.
    public static bool TryCommit(Enemy e, EnemyRole role, bool force = false)
    {
        if (e == null) return false;
        if (Holds(e)) return true;
        bool unseen = Unseen(e);
        int weight = Weight(role);
        if (Enabled && !force)
        {
            float since = waits.TryGetValue(e, out Wait mine) ? mine.since : Now;
            waits[e] = new Wait { since = since, asked = Now, weight = weight };
            if (Committed + weight > Budget || Now - lastStart < MinGap || unseen && UnseenCommitted >= MaxUnseen || Yields(e, since)) return false;
        }
        waits.Remove(e);
        holds.Add(new Hold { enemy = e, weight = weight, unseen = unseen });
        lastStart = Now;
        if (Log.Count >= LogLimit) Log.RemoveAt(0);
        Log.Add(new Commit { at = Now, role = role, weight = weight, unseen = unseen, forced = force, spawnId = e.SpawnId });
        return true;
    }

    // True when another enemy, still asking, has waited YieldMargin longer than this one.
    static bool Yields(Enemy e, float since)
    {
        foreach (var pair in waits)
        {
            Enemy other = pair.Key;
            if (other == e || other == null || !other.CombatEligible || !other.gameObject.activeInHierarchy) continue;
            if (Now - pair.Value.asked > StillAsking) continue;
            if (pair.Value.since + YieldMargin < since) return true;
        }
        return false;
    }

    public static bool CommittedUnseen(Enemy e) { foreach (var h in holds) if (h.enemy == e) return h.unseen; return false; }

    public static void Release(Enemy e)
    {
        for (int i = holds.Count - 1; i >= 0; i--) if (holds[i].enemy == e) holds.RemoveAt(i);
    }
}
