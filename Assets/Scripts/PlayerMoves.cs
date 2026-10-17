using UnityEngine;

// The hero's moves beyond the light combo: slams, the plunge, the dash stab, the
// launcher kick, blocking and the shield bash, the thrown blade, the slide, and
// the two System skills (Brush Wave and Colour Burst).
public partial class PlayerController
{
    const float ThrowCooldown = 1.4f;
    const float BashCooldown = 1.5f;

    // Only one of these can be active at a time.
    enum Move { None, Charge, Heavy, Plunge, Stab, Kick, Block, Bash, Throw, Slide }
    Move move;
    float moveT, moveDur, charge, heldT, blockT, ghostT, throwCd, bashCd;
    bool moveHit;
    float movePower = 1f;
    GrowthPolicy.ContactPlan movePlan;
    Enemy aimed;

    public float ThrowReady => 1f - Mathf.Clamp01(throwCd / (ThrowCooldown * (PilotProgress.Has("duelist") ? 1.25f : 1f)));

    Enemy Nearest(float range)
    {
        Enemy best = null;
        foreach (var e in Enemy.All)
        {
            if (e == null || !e.CombatEligible || !e.gameObject.activeInHierarchy) continue;
            float d = Vector3.Distance(e.transform.position, transform.position) - e.radius;
            if (d < range) { range = d; best = e; }
        }
        return best;
    }

    // The enemy the camera is most nearly looking at, within range: what a throw goes for.
    Enemy InSights(float range)
    {
        if (LockedTarget != null && LockedTarget.CombatEligible && (LockedTarget.transform.position - transform.position).sqrMagnitude <= range * range
            && TargetVisible(LockedTarget)) return LockedTarget;
        Vector3 look = Flat(Game.I.Cam.transform.forward).normalized;
        Enemy best = null;
        float bestScore = 0.35f;
        foreach (var e in Enemy.All)
        {
            if (e == null || !e.CombatEligible || !e.gameObject.activeInHierarchy || !TargetVisible(e)) continue;
            Vector3 to = Flat(e.transform.position - transform.position);
            float d = to.magnitude;
            if (Vector3.Distance(e.transform.position, transform.position) > range || d < 0.01f) continue;
            float score = Vector3.Dot(to / d, look) - d / range * 0.25f;
            if (score > bestScore) { bestScore = score; best = e; }
        }
        return best;
    }

    void Face(Enemy e)
    {
        if (e == null) return;
        Vector3 to = Flat(e.transform.position - transform.position);
        if (to.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(to);
    }

    // clipSpeed > 0 plays the clip at that pace and lets the rest blend away (for clips
    // that carry a long recovery); otherwise the whole clip is fitted to the move.
    void Begin(Move m, float duration, string clip, float clipSpeed = 0f)
    {
        move = m;
        moveT = 0f;
        moveDur = duration / Mathf.Lerp(1f, stats.attackSpeed, 0.5f);
        movePower = 1f;
        moveHit = false;
        attackT = -1f;
        queued = false;
        heldT = 0f;
        trail.emitting = true;
        if (clips == null || clip == null) return;
        if (m == Move.Plunge) { clips.Once(clip, moveDur, 0.04f); return; } // contact is physical landing
        float fraction = m == Move.Stab ? 0.35f : m == Move.Kick ? 0.4f : m == Move.Bash ? 0.3f : m == Move.Throw ? 0.45f : 0.5f;
        float length = clips.Length(clip);
        if (length <= 0f) return;
        float contact = clipSpeed > 0f ? duration * fraction * clipSpeed : length * fraction;
        contact = Mathf.Min(contact, length * 0.85f);
        movePlan = new GrowthPolicy.ContactPlan(moveDur, contact, fraction);
        moveDur = movePlan.duration; movePower = movePlan.power;
        clips.Contact(clip, movePlan, m == Move.Throw && planar.magnitude > 2.5f);
    }

    void StartCharge()
    {
        move = Move.Charge;
        charge = 0f;
        heldT = 0f;
        ghostT = 0f;
        if (clips != null) clips.Release();
    }

    // Overhead slam. charge 0..1 scales the damage and the blast.
    void StartHeavy(float power)
    {
        BeginAction("StartHeavy");
        charge = power;
        Face(CombatTarget(5f, Vector3.zero));
        Begin(Move.Heavy, 0.5f, "2H_Melee_Attack_Chop");
        if (MasteryHeavyTime > 1f)
        {
            moveDur *= MasteryHeavyTime;
            if (clips != null && clips.Has("2H_Melee_Attack_Chop"))
            {
                movePlan = new GrowthPolicy.ContactPlan(moveDur, movePlan.clipContact, movePlan.fraction);
                moveDur = movePlan.duration;
                clips.Contact("2H_Melee_Attack_Chop", movePlan);
            }
        }
        lunge = 5f;
        ToonKit.Sfx("swing", 1f, 0.7f, position: transform.position + Vector3.up);
    }

    // Attack in mid-air: drop like a stone and burst on landing.
    void StartPlunge()
    {
        BeginAction("StartPlunge");
        Begin(Move.Plunge, 2f, "2H_Melee_Attack_Chop");
        vy = -30f;
        ToonKit.Sfx("swing", 1f, 0.8f, position: transform.position + Vector3.up);
    }

    // Attack out of a dash: a long piercing lunge.
    void StartStab(Vector3 dir)
    {
        BeginAction("StartStab");
        dashT = 0f;
        Enemy target = CombatTarget(7f, dir);
        if (target != null) Face(target);
        else if (dir.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(dir);
        Begin(Move.Stab, 0.32f, "1H_Melee_Attack_Stab", 2f);
        lunge = 16f;
        ToonKit.Sfx("swing", 1f, 1.4f, position: transform.position + Vector3.up);
    }

    // Level 4: a kick that sends enemies into the air, where they take extra damage.
    void StartKick()
    {
        BeginAction("StartKick");
        Face(CombatTarget(4f, Vector3.zero));
        Begin(Move.Kick, 0.34f, "Unarmed_Melee_Attack_Kick");
        trail.emitting = false;
        lunge = 4f;
        comboReset = 0.9f;
        ToonKit.Sfx("swing", 1f, 1.2f, position: transform.position + Vector3.up);
    }

    // A light blade has no held guard: one press gives one short deflection, and the button
    // must be let go before the next.
    bool guardSpent;

    void StartBlock()
    {
        if (guardSpent) return;
        move = Move.Block;
        blockT = 0f;
        heldT = 0f;
        Face(CombatTarget(9f, Vector3.zero));
        if (clips != null) clips.Release();
        ToonKit.Sfx("dash", 0.5f, 1.5f, position: transform.position + Vector3.up);
    }

    // Attack while guarding: drive the shield forward. It breaks guards and leaves
    // whatever it hits wide open for a riposte.
    void StartBash()
    {
        BeginAction("StartBash");
        if (bashCd > 0f) return;
        bashCd = BashCooldown;
        Face(CombatTarget(5f, Vector3.zero));
        Begin(Move.Bash, 0.4f, "Shield_Bash", 2f);
        trail.emitting = false;
        lunge = 13f;
        invuln = Mathf.Max(invuln, 0.25f);
        ToonKit.Sfx("dash", 0.8f, 0.8f, position: transform.position + Vector3.up);
    }

    // A blade of colour thrown at whatever the camera is looking at.
    void StartThrow()
    {
        BeginAction("StartThrow");
        throwCd = ThrowCooldown * (PilotProgress.Has("duelist") ? 1.25f : 1f);
        aimed = InSights(24f);
        if (aimed != null) Face(aimed);
        else transform.rotation = Quaternion.Euler(0f, Game.I.Cam.transform.eulerAngles.y, 0f);
        Begin(Move.Throw, 0.42f, "Throw");
        trail.emitting = false;
        ToonKit.Sfx("swing", 0.8f, 1.5f, position: transform.position + Vector3.up);
    }

    // Guard at a full sprint: drop and slide, feet first, through anything in the way.
    void StartSlide()
    {
        sprinting = false;
        dashHits.Clear();
        Begin(Move.Slide, 0.62f, null);
        trail.emitting = false;
        ghostT = 0f;
        if (clips != null) clips.OnceAt("Slide_Loop", 1f, moveDur, 0.08f);
        ToonKit.Sfx("dash", 0.8f, 0.7f, position: transform.position + Vector3.up);
    }

    void TickMove(float dt)
    {
        moveT += dt;
        switch (move)
        {
            case Move.Charge:
                charge = Mathf.Min(1f, charge + dt / 0.9f);
                if ((ghostT -= dt) <= 0f)
                {
                    // Light gathers in while the swing winds up.
                    ghostT = 0.07f;
                    Vector3 around = transform.position + Vector3.up + ProductionRandom.Cosmetic.onUnitSphere * 2.2f;
                    Fx.Sparks(around, (transform.position + Vector3.up - around).normalized, charge >= 1f ? UI.Gold : Color.white, 1);
                }
                if (!GameInput.AttackHeld) StartHeavy(charge);
                break;

            case Move.Heavy:
                if (!moveHit && moveT >= moveDur * 0.5f)
                {
                    moveHit = true;
                    // The overhead chop: an arc standing on edge in front of him.
                    Fx.Slash(transform.position + Vector3.up * 1.2f + transform.forward * 0.4f, transform.rotation * Quaternion.Euler(0f, 0f, 88f),
                        2.6f + charge, 170f, charge >= 1f ? UI.Gold : Color.white, 0.2f, false);
                    bool breaker = charge >= 0.6f && PilotProgress.Has("breaker");
                    Slam(transform.position + transform.forward * 1.6f, (3.1f + charge * 1.6f) * stats.reach * (breaker && PilotProgress.Has("breaker", 2) ? 1.25f : 1f), Mathf.Lerp(2.6f, 5f, charge) * (breaker ? 1.2f : 1f), 0.9f);
                }
                if (moveT >= moveDur) End();
                break;

            case Move.Plunge:
                vy = Mathf.Min(vy, -30f);
                if (cc.isGrounded)
                {
                    Slam(transform.position, 3.6f * stats.reach, 2.3f, 0.9f);
                    if (clips == null) model.localScale = Vector3.Scale(modelScale, new Vector3(1.3f, 0.7f, 1.3f));
                    End();
                }
                break;

            case Move.Stab:
                if (!moveHit && moveT >= moveDur * 0.35f)
                {
                    moveHit = true;
                    int n = 0;
                    Vector3 fwd = transform.forward;
                    Fx.Thrust(transform.position + Vector3.up * 1.15f, fwd, 4.8f * stats.reach, new Color(0.85f, 0.95f, 1f), 0.16f);
                    for (int i = Enemy.All.Count - 1; i >= 0; i--)
                    {
                        Enemy e = Enemy.All[i];
                        Vector3 to = Flat(e.transform.position - transform.position);
                        float along = Vector3.Dot(to, fwd);
                        if (along < -0.5f || along > 4.8f * stats.reach || (to - fwd * along).magnitude > 1.3f + e.radius) continue;
                        if (Hit(e, stats.damage * weak * 1.7f, ProductionRandom.Combat.value < stats.crit, 12f, true).Damaged) n++;
                    }
                    Landed(n, 0.07f, 0.18f);
                }
                if (moveT >= moveDur) End();
                break;

            case Move.Kick:
                if (!moveHit && moveT >= moveDur * 0.4f)
                {
                    moveHit = true;
                    int n = 0;
                    for (int i = Enemy.All.Count - 1; i >= 0; i--)
                    {
                        Enemy e = Enemy.All[i];
                        if (!InFront(e, 2.6f * stats.reach, 0.2f)) continue;
                        if (!Hit(e, stats.damage * weak * 1.2f, false, 2f, true, launch: 1.15f).Damaged) continue;
                        n++;
                    }
                    Landed(n, 0.06f, 0.15f);
                }
                if (moveT >= moveDur) End();
                break;

            case Move.Block:
                TickGuard(dt, Access.Guard);
                break;

            case Move.Bash:
                if (!moveHit && moveT >= moveDur * 0.3f)
                {
                    moveHit = true;
                    int n = 0;
                    for (int i = Enemy.All.Count - 1; i >= 0; i--)
                    {
                        Enemy e = Enemy.All[i];
                        if (!InFront(e, 2.7f * stats.reach, 0.1f)) continue;
                        if (!Hit(e, stats.damage * weak * 1.1f, false, 14f, true, stagger: 1.2f).Damaged) continue;
                        n++;
                    }
                    if (n > 0)
                    {
                        Vector3 guard = transform.position + Vector3.up * 1.1f + transform.forward * 0.8f;
                        Fx.Flash(guard, Color.white, 2.2f);
                        Fx.Sparks(guard, transform.forward, Color.white, 6);
                        Game.I.Cam.Kick(5f);
                    }
                    Landed(n, 0.08f, 0.25f);
                }
                if (moveT >= moveDur) End();
                break;

            case Move.Throw:
                if (!moveHit && moveT >= moveDur * 0.45f)
                {
                    moveHit = true;
                    Vector3 origin = transform.position + Vector3.up * 1.45f + transform.forward * 0.5f;
                    Vector3 aim = aimed != null && aimed.CombatEligible
                        ? (aimed.transform.position + Vector3.up * Mathf.Min(1.1f, aimed.height * 0.5f) - origin).normalized
                        : transform.forward;
                    Projectile.Throw(origin, aim, 30f, stats.damage * weak * 1.4f * movePower);
                    Fx.Flash(origin, UI.Blue, 1.2f);
                }
                if (moveT >= moveDur) End();
                break;

            case Move.Slide:
                if (moveT < moveDur * 0.8f) lunge = Mathf.Max(lunge, 11f);
                if ((ghostT -= dt) <= 0f)
                {
                    ghostT = 0.06f;
                    Fx.Dust(transform.position, 1);
                }
                for (int i = Enemy.All.Count - 1; i >= 0; i--)
                {
                    Enemy e = Enemy.All[i];
                    if (dashHits.Contains(e) || Flat(e.transform.position - transform.position).magnitude > 1.3f + e.radius) continue;
                    // Taking the legs out from under something leaves it open.
                    var result = Hit(e, stats.damage * weak * 0.9f, false, 5f, true, stagger: 0.9f);
                    if (result.outcome == HitOutcome.Miss) continue;
                    dashHits.Add(e);
                    if (!result.Damaged) continue;
                    Landed(1, 0.05f, 0.12f);
                }
                if (moveT >= moveDur)
                {
                    End();
                    if (clips != null) clips.Once("Slide_Exit", 0.3f);
                }
                break;
        }
    }

    // A raised guard turns to meet the nearest threat and lasts while the button is held,
    // except a light blade's deflection, which drops by itself.
    void TickGuard(float dt, bool held)
    {
        blockT += dt;
        Enemy threat = CombatTarget(9f, Vector3.zero);
        if (threat != null)
        {
            Vector3 to = Flat(threat.transform.position - transform.position);
            if (to.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to), 8f * dt);
        }
        if (!held) End();
        else if (Weapon.guard == GuardStyle.Deflect && blockT >= Weapon.deflectHold) { guardSpent = true; Access.ReleaseGuard(); End(); }
    }

    bool InFront(Enemy e, float range, float minDot)
    {
        Vector3 to = Flat(e.transform.position - transform.position);
        float dist = to.magnitude - e.radius;
        return dist <= range && (dist < 0.6f || Vector3.Dot(to.normalized, transform.forward) >= minDot);
    }

    void End()
    {
        move = Move.None;
        charge = 0f;
        trail.emitting = dashT > 0f;
        if (clips != null) clips.Release();
    }

    void CancelOffense()
    {
        attackT = -1f;
        hitDone = moveHit = true;
        lunge = 0f;
        ClearIntent();
        End();
    }

    // A ground-shaking area hit: everything in the radius is damaged and knocked into the air.
    void Slam(Vector3 centre, float radius, float damageMult, float launch)
    {
        int n = 0;
        for (int i = Enemy.All.Count - 1; i >= 0; i--)
        {
            Enemy e = Enemy.All[i];
            if (Flat(e.transform.position - centre).magnitude - e.radius > radius) continue;
            bool crit = ProductionRandom.Combat.value < stats.crit;
            if (!Hit(e, stats.damage * weak * damageMult, crit, 10f, true, launch: launch).Damaged) continue;
            n++;
        }
        Fx.Ring(centre, charge >= 1f ? UI.Gold : Color.white, 0.5f, radius, 0.3f);
        Fx.Burst(centre + Vector3.up * 0.3f, new Color(0.95f, 0.9f, 0.75f), 8, 6f);
        Fx.Dust(centre, 8);
        Game.I.Cam.Shake(0.3f + charge * 0.3f);
        Game.I.Cam.Kick(5f + charge * 5f);
        ToonKit.Sfx("slam", 0.9f, 1.1f, position: transform.position + Vector3.up);
        Landed(n, 0.11f, 0f);
    }

    void Landed(int hits, float hitstop, float shake)
    {
        if (hits == 0) return;
        strikes++;
        Juice.Hitstop(hitstop);
        if (shake > 0f) Game.I.Cam.Shake(shake);
        ToonKit.Sfx("hit", 1f, 1.2f, position: transform.position + Vector3.up);
        Game.I.AddCombo(hits);
    }

    // Level 5 skill: a cutting wave that travels straight ahead through everything.
    void BrushWave()
    {
        CancelOffense();
        waveCd = WaveCooldown;
        attackT = -1f;
        queued = false;
        if (clips != null) clips.Once(Hero.combo[0], 0.35f, 0.03f);
        Vector3 origin = transform.position, fwd = transform.forward;
        ToonKit.Sfx("swing", 1f, 0.7f, position: transform.position + Vector3.up);
        ToonKit.Sfx("perfect", 0.5f, 1.3f, position: transform.position + Vector3.up);
        Game.I.Cam.Shake(0.25f);
        Game.I.Cam.Kick(5f);
        for (int i = 1; i <= 6; i++)
            Fx.Ring(origin + fwd * i * 1.6f, i % 2 == 0 ? UI.Blue : Color.white, 0.3f, 1.7f, 0.2f + i * 0.04f);

        Game.I.Lines(0.8f);
        styleTag = 31;
        for (int i = Enemy.All.Count - 1; i >= 0; i--)
        {
            Enemy e = Enemy.All[i];
            Vector3 to = Flat(e.transform.position - origin);
            float along = Vector3.Dot(to, fwd);
            float side = (to - fwd * along).magnitude;
            if (along > -0.5f && along < 10.5f && side < 1.4f + e.radius) Hit(e, stats.damage * 2.5f, false, 12f, true, DamageKind.Magic);
        }
        styleTag = -1;
    }

    void ColourBurst()
    {
        CancelOffense();
        burst = 0f;
        // Both hands thrown wide as the colour goes out of him.
        if (clips != null && attackT < 0f && move == Move.None) clips.Once("Spell_Simple_Shoot", 0.5f, 0.05f);
        invuln = Mathf.Max(invuln, 1.2f);
        Juice.Slow(0.35f);
        Game.I.Cam.Shake(0.6f);
        Game.I.Cam.Kick(12f);
        Game.I.Flash(Color.white, 0.5f);
        Game.I.Impact(1f);
        Game.I.Lines(1f);
        Game.I.CutIn("COLOUR BURST", UI.Gold);
        Game.I.AddStyle(40f);
        ToonKit.Sfx("burst", position: transform.position + Vector3.up);

        Vector3 pos = transform.position;
        Fx.Ring(pos, UI.Red, 1f, BurstRadius, 0.45f);
        Fx.Ring(pos, UI.Gold, 0.5f, BurstRadius * 0.85f, 0.55f);
        Fx.Ring(pos, UI.Blue, 0.2f, BurstRadius * 0.7f, 0.65f);
        Fx.Burst(pos + Vector3.up, UI.Gold, 10, 9f);
        Fx.Burst(pos + Vector3.up, UI.Blue, 10, 9f);

        for (int i = Enemy.All.Count - 1; i >= 0; i--)
        {
            Enemy e = Enemy.All[i];
            if (Flat(e.transform.position - pos).magnitude - e.radius >= BurstRadius) continue;
            // Colour Burst explicitly penetrates cover; its vertical band remains bounded.
            if (CombatContact.CanContact(pos + Vector3.up * 1.1f, e, 1.5f, true))
                e.ReceiveHit(new DamageRequest(stats.damage * 6f, pos, 16f, true, source: this, kind: DamageKind.Magic));
        }
    }
}
