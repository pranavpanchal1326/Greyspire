using UnityEngine;

// Roles in a mixed fight (package C02): who may commit to an attack and when (through the
// EncounterDirector), what the rest do while they wait, the pack's caller, and the shield
// carrier that screens a ranged ally and answers repeated frontal light attacks.
public partial class Enemy
{
    public EnemyRole Role { get; private set; }

    // Caller: a rallying call every so often; hitting the caller during it breaks the call.
    public const float CallWindup = 1.2f, CallCooldown = 9f, CallRadius = 10f, RallySeconds = 5f;
    public const float RallySpeed = 1.2f, RallyRecovery = 1.5f;
    // Shield carrier: three frontal light blows blocked inside this window bring a counter-bash.
    public const int BlocksBeforeCounter = 3;
    public const float BlockWindow = 5f, CounterWindup = 0.5f, CounterCooldown = 6f, CounterReach = 2.3f;

    float callCd = 2.5f, callWind = -1f, rallyT, bashWind = -1f, blockWindowT, counterCd, commitHold = -1f;
    int blockedRun;

    public bool Calling => callWind > 0f;
    public bool Rallied => rallyT > 0f;
    public bool Countering => bashWind > 0f;
    public int BlockedRun => blockedRun;

    void AssignRole()
    {
        switch (kind)
        {
            case Kind.Minion: Role = duelist && guards ? EnemyRole.ShieldCarrier : duelist ? EnemyRole.Duelist : EnemyRole.Skirmisher; break;
            case Kind.Spitter: Role = EnemyRole.Artillery; break;
            case Kind.Bomber: Role = EnemyRole.Sapper; break;
            case Kind.Brute: Role = EnemyRole.Heavy; break;
            case Kind.Boss: Role = EnemyRole.Guardian; break;
            default: Role = EnemyRole.Skirmisher; break;
        }
    }

    // When a pack of three or more wakes and nobody leads it, its first thrower (by stable
    // spawn identity, so a replay chooses the same one) becomes the caller.
    void OrganisePack()
    {
        int awake = 0;
        Enemy caller = null, candidate = null;
        foreach (Enemy mate in All)
        {
            if (!mate.CombatEligible || !mate.alerted || (mate.transform.position - transform.position).sqrMagnitude > CallRadius * CallRadius) continue;
            awake++;
            if (mate.Role == EnemyRole.Caller) caller = mate;
            if (mate.Role == EnemyRole.Artillery && (candidate == null || string.CompareOrdinal(mate.SpawnId, candidate.SpawnId) < 0)) candidate = mate;
        }
        if (caller == null && candidate != null && awake >= 3) candidate.Role = EnemyRole.Caller;
    }

    // Asks the director for a damaging commitment. Returns false while this enemy must wait.
    bool Commit(bool force = false) => Training || IsBoss || Role == EnemyRole.Guardian || EncounterDirector.TryCommit(this, Role, force);

    // Extra warning for a commitment the hero cannot see: a longer wind-up, and a snarl from
    // where it is coming, so the sound carries its direction.
    float UnseenWarning()
    {
        if (!EncounterDirector.CommittedUnseen(this)) return 0f;
        ToonKit.Sfx("roar", 0.55f, bandit ? 1.8f : 1.5f, position: transform.position + Vector3.up);
        return EncounterDirector.UnseenExtraWindup;
    }

    void ReleaseCommit(float after = 0f)
    {
        if (after > 0f) { commitHold = after; return; }
        commitHold = -1f;
        EncounterDirector.Release(this);
    }

    // The sound of an attack being wound up, from where it is coming (package S01). Bigger
    // things are lower. A Threat cue: it can take a voice from anything lesser, never lose one.
    public int Tells { get; private set; }
    public int HitsLanded { get; private set; }     // melee blows that reached the hero
    void Tell(float pitch)
    {
        Tells++;
        ToonKit.Sfx("tell", 0.85f, pitch, position: transform.position + Vector3.up * Mathf.Min(1.5f, height * 0.6f));
    }

    // Heavy things are heard coming: a brute's or a Guardian's footfalls as it walks.
    float footT;
    public int Footfalls { get; private set; }
    void Footfall(Vector3 vel, float dt)
    {
        if (kind != Kind.Brute || vel.sqrMagnitude < 0.25f) { footT = 0f; return; }
        footT -= dt;
        if (footT > 0f) return;
        footT = Mathf.Lerp(0.75f, 0.42f, Mathf.Clamp01(vel.magnitude / 6f));
        Footfalls++;
        ToonKit.Sfx("step", Gatekeeper ? 0.75f : 0.6f, Gatekeeper ? 0.5f : 0.6f, position: transform.position + Vector3.up * 0.2f);
        if (Gatekeeper) Game.I.Cam.Shake(0.06f);
    }

    // Timers every role shares; runs before the role's own behaviour each frame.
    void TickRoles(float dt)
    {
        if (rallyT > 0f) rallyT -= dt;
        if (counterCd > 0f) counterCd -= dt;
        if (blockWindowT > 0f && (blockWindowT -= dt) <= 0f) blockedRun = 0;
        if (commitHold > 0f && (commitHold -= dt) <= 0f) ReleaseCommit();
    }

    // ------------------------------------------------------------ caller

    // Returns true while the call is under way (the caller does nothing else).
    bool TickCaller(float dt)
    {
        if (Role != EnemyRole.Caller) return false;
        if (callWind > 0f)
        {
            callWind -= dt;
            if (callWind <= 0f) Rally();
            return true;
        }
        callCd -= dt;
        if (callCd > 0f || windup > 0f) return false;
        callWind = CallWindup;
        callCd = CallCooldown;
        BeginAction("call");
        SetFlash(0.7f);
        Fx.Telegraph(transform.position, CallRadius, CallWindup, UI.Gold);
        ToonKit.Sfx("roar", 0.9f, bandit ? 1.35f : 1.15f, position: transform.position + Vector3.up);
        if (clips != null) clips.Once(clips.Has("Spellcast_Raise") ? "Spellcast_Raise" : "Cheer", CallWindup);
        return true;
    }

    void Rally()
    {
        int rallied = 0;
        foreach (Enemy mate in All)
        {
            if (mate == this || !mate.CombatEligible || mate.IsBoss) continue;
            if ((mate.transform.position - transform.position).sqrMagnitude > CallRadius * CallRadius) continue;
            mate.rallyT = RallySeconds;
            mate.SetFlash(0.4f);
            rallied++;
        }
        Fx.Ring(transform.position, UI.Gold, 1f, CallRadius, 0.45f);
        if (rallied > 0) Game.I.Toast("The pack rallies: break the caller's next signal", UI.Gold);
    }

    // A hit that lands during the call breaks it: the counterplay is to go for the caller.
    void BreakCall()
    {
        if (callWind <= 0f) return;
        callWind = -1f;
        callCd = CallCooldown * 0.6f;
        Game.I.Word(transform.position + Vector3.up * (height + 0.6f), "Signal broken", UI.Gold, 28);
        ToonKit.Sfx("deny", 0.8f, 0.9f, position: transform.position + Vector3.up);
    }

    // ------------------------------------------------------------ shield carrier

    // The ranged ally a shield carrier stands in front of, if one is near.
    Enemy ScreenedAlly()
    {
        Enemy best = null;
        float bestSq = 12f * 12f;
        foreach (Enemy mate in All)
        {
            if (mate == this || !mate.CombatEligible || (mate.Role != EnemyRole.Artillery && mate.Role != EnemyRole.Caller)) continue;
            float sq = (mate.transform.position - transform.position).sqrMagnitude;
            if (sq < bestSq) { bestSq = sq; best = mate; }
        }
        return best;
    }

    // Where to stand to hold the lane between the hero and that ally.
    Vector3 ScreenPoint(PlayerController player, Enemy ally)
    {
        Vector3 lane = ally.transform.position - player.transform.position;
        lane.y = 0f;
        if (lane.sqrMagnitude < 0.01f) return transform.position;
        return player.transform.position + lane.normalized * Mathf.Min(RingDistance + radius, lane.magnitude * 0.6f);
    }

    // A frontal light blow glanced off the shield: three in a short space and it answers.
    void NoteBlocked()
    {
        if (Role != EnemyRole.ShieldCarrier) return;
        blockedRun++;
        blockWindowT = BlockWindow;
        if (blockedRun < BlocksBeforeCounter || counterCd > 0f || bashWind > 0f || !Commit()) return;
        blockedRun = 0;
        counterCd = CounterCooldown;
        bashWind = CounterWindup + UnseenWarning();
        BeginAction("counter");
        SetFlash(0.8f);
        Fx.Telegraph(transform.position + transform.forward * 1.3f, 1.6f, bashWind, Warn);
        if (clips != null) clips.Once("Shield_Bash", bashWind + 0.2f);
        Game.I.Word(transform.position + Vector3.up * (height + 0.5f), "Counter", Warn, 26);
    }

    // Returns true while the counter-bash is under way.
    bool TickCounter(PlayerController player, Vector3 dir, float dist, float dt)
    {
        if (bashWind <= 0f) return false;
        guarding = false;
        bashWind -= dt;
        if (bashWind > 0f) return true;
        if (dist < radius + CounterReach && Vector3.Dot(dir, transform.forward) > 0.3f) HitPlayer(player, 1f, transform.position, 14f);
        Fx.Sparks(transform.position + Vector3.up * 1.1f + transform.forward * 0.8f, transform.forward, Color.white, 6);
        ToonKit.Sfx("slam", 0.6f, 1.5f, position: transform.position + Vector3.up);
        ReleaseCommit(0.2f);
        Backoff(0.5f);
        return true;
    }

    // Whatever it had committed to is off: a stagger, a launch, death.
    void CancelCommitments()
    {
        bashWind = -1f;
        BreakCall();
        ReleaseCommit();
    }
}
