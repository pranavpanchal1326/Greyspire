using UnityEngine;

// Grol the Gatekeeper, the Guardian of floor 5 (package C03). A small, coherent move language:
//   Hammerfall  - the overhead slam in front of him (red disc ahead)    -> step aside
//   Gate Sweep  - a full turn with the bar at arm's length (red ring round him) -> get out, or roll through late
//   Bar Rush    - from phase 2: a straight charge down a red lane        -> put a pillar behind you
// A charge that ends in one of the arena's stone pillars shatters it and leaves him stunned and
// open; into the bars, only briefly. At 60% and 30% he roars and tears at the gate (no damage
// gets through while he does), and each phase quickens him.
public partial class Enemy
{
    public bool Gatekeeper { get; private set; }
    public bool Dormant { get; private set; }
    public int GuardianPhase { get; private set; } = 1;

    enum GrolMove { None, Hammer, Sweep, RushWindup, Rush }

    public const float HammerWindup = 0.8f, SweepWindup = 1f, SweepRadius = 4.8f, RushWindup = 1f, RushSpeed = 13f, RushMax = 1.6f;
    public const float PillarStun = 2.5f, WallStun = 1f, PhaseRoar = 1.2f, GatekeeperScale = 1.3f;
    public static readonly float[] PhaseAt = { 0.6f, 0.3f };

    GrolMove grol;
    float grolT, roarT;
    Vector3 rushDir;
    bool rushHit;
    int grolActions, rushesQueued;

    public bool Roaring => roarT > 0f;
    public bool Rushing => grol == GrolMove.Rush;
    public bool RushPending => grol == GrolMove.RushWindup;
    public float MoveTime => grolT;

    // Makes this Brute the floor's Gatekeeper. He keeps the gate until Wake().
    public void MakeGatekeeper()
    {
        Gatekeeper = true;
        Dormant = true;
        Role = EnemyRole.Guardian;
        // A Guardian should dwarf the brutes of the floors below it.
        transform.localScale *= GatekeeperScale;
    }

    public void Wake()
    {
        if (!Gatekeeper || !Dormant) return;
        Dormant = false;
        alerted = provoked = true;
        cooldown = 0.8f;
        ToonKit.Sfx("roar", 1f, 0.75f, position: transform.position + Vector3.up);
        if (clips != null) clips.Once("Taunt", 1f);
        Game.I.Cam.Shake(0.4f);
    }

    // Damage does not get through while he keeps the shut gate, or while he roars between phases.
    bool GatekeeperImmune => Gatekeeper && (Dormant || roarT > 0f);

    Vector3 TickGatekeeper(PlayerController player, Vector3 dir, float dist, float dt, ref bool turn)
    {
        if (Dormant)
        {
            if (clips != null) clips.Loop(clips.idleClip, 1f, 0.3f);
            faceDir = Game.I.GatekeeperFacing(transform.position);
            return Vector3.zero;
        }

        // Phase changes: at 60% and 30% he roars and tears at the gate.
        int wanted = hp <= maxHp * PhaseAt[1] ? 3 : hp <= maxHp * PhaseAt[0] ? 2 : 1;
        if (wanted > GuardianPhase && grol != GrolMove.Rush)
        {
            GuardianPhase = wanted;
            roarT = PhaseRoar;
            grol = GrolMove.None;
            windup = -1f;
            punch = 1.5f;
            Fx.Ring(transform.position, UI.Red, 1f, 9f, 0.6f);
            Game.I.Cam.Shake(0.6f);
            ToonKit.Sfx("roar", 1f, 0.65f, position: transform.position + Vector3.up);
            if (clips != null) clips.Once("Taunt", PhaseRoar);
            Game.I.OnGatekeeperPhase(this, GuardianPhase);
            foreach (var e in All) if (e != this) e.Shove(transform.position, 6f);
        }
        if (roarT > 0f) { roarT -= dt; turn = false; return Vector3.zero; }

        switch (grol)
        {
            case GrolMove.Hammer:
                turn = false;
                if ((grolT -= dt) > 0f) return Vector3.zero;
                Slam(player, transform.position + transform.forward * 1.6f, 3.6f, GuardianPhase >= 3 ? 1 : 0);
                EndGrolMove(GuardianPhase >= 3 ? 1.3f : 1.8f);
                return Vector3.zero;

            case GrolMove.Sweep:
                turn = false;
                if ((grolT -= dt) > 0f) return Vector3.zero;
                Fx.Ring(transform.position, Warn, 0.5f, SweepRadius, 0.3f);
                Fx.Slash(transform.position + Vector3.up * 1.3f, transform.rotation, SweepRadius, 360f, new Color(1f, 0.45f, 0.4f, 0.8f), 0.26f, false);
                ToonKit.Sfx("slam", 0.8f, 1.2f, position: transform.position + Vector3.up);
                Game.I.Cam.Shake(0.3f);
                Vector3 off = player.transform.position - transform.position;
                if (off.y < 1.6f && new Vector2(off.x, off.z).magnitude < SweepRadius) HitPlayer(player, 2f, transform.position, 14f);
                EndGrolMove(GuardianPhase >= 3 ? 1.3f : 1.8f);
                return Vector3.zero;

            case GrolMove.RushWindup:
                turn = grolT > 0.25f;   // he settles his aim before he goes, then cannot turn
                if (turn)
                {
                    rushDir = dir;
                    rushDir.y = 0f;
                }
                if ((grolT -= dt) > 0f) return Vector3.zero;
                grol = GrolMove.Rush;
                grolT = RushMax;
                rushHit = false;
                lungeT = RushMax;      // keeps navigation from bending the charge
                ToonKit.Sfx("roar", 0.8f, 1.1f, position: transform.position + Vector3.up);
                return Vector3.zero;

            case GrolMove.Rush:
            {
                turn = false;
                faceDir = rushDir;
                grolT -= dt;
                lungeT = Mathf.Max(lungeT, 0.05f);
                Vector3 pos = transform.position, toHero = player.transform.position - pos;
                if (!rushHit && toHero.y < 1.6f && new Vector2(toHero.x, toHero.z).magnitude < radius * transform.localScale.x + 0.9f)
                {
                    rushHit = true;
                    HitPlayer(player, 2f, pos - rushDir, 18f);
                }
                // What the charge ends in: a pillar (shattered, he is stunned and open), the bars, or nothing.
                int pillar = Game.I.GatePillarAhead(pos, rushDir, radius * transform.localScale.x + 0.35f);
                if (pillar >= 0)
                {
                    Game.I.BreakGatePillar(pillar);
                    EndGrolMove(0f);
                    Stagger(PillarStun);
                    Game.I.Word(pos + Vector3.up * (height * transform.localScale.y + 0.6f), "Stunned", UI.Gold, 34);
                    return Vector3.zero;
                }
                if (Game.I.GatekeeperWallAhead(pos, rushDir, radius * transform.localScale.x + 0.3f))
                {
                    // Enraged, the bars only turn him round: he winds up a second charge at once.
                    // Only stone stops him.
                    if (rushesQueued > 0)
                    {
                        rushesQueued--;
                        Fx.Sparks(pos + Vector3.up * 1.4f + rushDir * radius, -rushDir, Color.white, 8);
                        ToonKit.Sfx("slam", 0.7f, 1.4f, position: pos + Vector3.up);
                        BeginRush();
                        return Vector3.zero;
                    }
                    EndGrolMove(0f);
                    Stagger(WallStun);
                    Fx.Sparks(pos + Vector3.up * 1.4f + rushDir * radius, -rushDir, Color.white, 8);
                    ToonKit.Sfx("slam", 0.7f, 1.4f, position: pos + Vector3.up);
                    return Vector3.zero;
                }
                if (grolT <= 0f)
                {
                    // Spent, in the open: in the last phase he turns and goes again.
                    if (rushesQueued > 0) { rushesQueued--; BeginRush(); return Vector3.zero; }
                    EndGrolMove(1.4f);
                    return Vector3.zero;
                }
                return rushDir * RushSpeed;
            }
        }

        // Choosing the next move.
        if (cooldown > 0f)
        {
            if (anim != null) anim.move = dist > 3f ? 0.6f : 0f;
            return dist > 3f ? dir * speed * (1f + 0.15f * (GuardianPhase - 1)) : Vector3.zero;
        }
        grolActions++;
        bool canRush = GuardianPhase >= 2;
        if (canRush && (dist > 6f || grolActions % 3 == 0))
        {
            rushesQueued = GuardianPhase >= 3 ? 1 : 0;
            BeginRush();
            return Vector3.zero;
        }
        if (dist > 3.4f)
        {
            if (anim != null) anim.move = 0.6f;
            return dir * speed * (1f + 0.15f * (GuardianPhase - 1));
        }
        BeginAction("gatekeeper");
        if (aiRandom.value < 0.5f)
        {
            grol = GrolMove.Hammer;
            grolT = HammerWindup;
            Tell(0.6f);
            if (clips != null) clips.Once("2H_Melee_Attack_Chop", 1.5f);
            Fx.Telegraph(transform.position + transform.forward * 1.6f, 3.6f, HammerWindup, Warn);
        }
        else
        {
            grol = GrolMove.Sweep;
            grolT = SweepWindup;
            Tell(0.55f);
            if (clips != null) clips.Once("Sword_Regular_C", SweepWindup + 0.6f);
            Fx.Telegraph(transform.position, SweepRadius, SweepWindup, Warn);
        }
        return Vector3.zero;
    }

    void BeginRush()
    {
        BeginAction("gatekeeper-rush");
        grol = GrolMove.RushWindup;
        grolT = RushWindup;
        Vector3 lane = Game.I.Player.transform.position - transform.position;
        lane.y = 0f;
        rushDir = lane.sqrMagnitude > 0.01f ? lane.normalized : transform.forward;
        SetFlash(0.8f);
        if (clips != null) clips.Once("Taunt", RushWindup);
        Fx.Thrust(transform.position + Vector3.up * 0.15f + rushDir * 1.2f, rushDir, 18f, new Color(1f, 0.25f, 0.2f, 0.55f), RushWindup);
        ToonKit.Sfx("roar", 0.6f, 1.4f, position: transform.position + Vector3.up);
    }

    void EndGrolMove(float recover)
    {
        grol = GrolMove.None;
        grolT = 0f;
        lungeT = 0f;
        cooldown = recover;
    }

    // A stagger or launch cuts his move short.
    void CancelGrolMove()
    {
        if (!Gatekeeper) return;
        grol = GrolMove.None;
        rushesQueued = 0;
        lungeT = 0f;
    }
}
