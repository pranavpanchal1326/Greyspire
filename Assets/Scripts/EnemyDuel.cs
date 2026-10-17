using UnityEngine;

// How the sword-carriers fight. Imps and bandits do not just run at the hero:
// they close in, circle, wait for an opening, strike and step back out. Only a
// few press the attack at any moment, so a crowd reads as a fight rather than a
// pile-up. Some raise a shield, which light blows glance off.
public partial class Enemy
{
    enum Duel { Approach, Circle, Close, Windup, Strike, Recover }

    const float RingDistance = 3.4f;    // how far out they circle
    const float StrikeReach = 1.5f;

    Duel duel;
    float duelT;
    bool duelist, guards, guarding;
    Vector3 faceDir;        // where to look this frame, if not straight at the hero

    bool Pressing => duel == Duel.Close || duel == Duel.Windup || duel == Duel.Strike;

    Vector3 TickDuelist(PlayerController player, Vector3 dir, float dist, float dt, ref bool turn)
    {
        duelT -= dt;
        guarding = false;
        if (TickCounter(player, dir, dist, dt)) return Vector3.zero;
        switch (duel)
        {
            case Duel.Circle:
            {
                if (dist > RingDistance + radius + 5f)
                {
                    duel = Duel.Approach;
                    return dir * speed;
                }
                if (duelT <= 0f)
                {
                    // How many press at once, and when, is the encounter director's call.
                    if (cooldown <= 0f && Commit())
                    {
                        duel = Duel.Close;
                        duelT = 2.2f;
                        return dir * speed;
                    }
                    duelT = aiRandom.Range(0.5f, 1.3f);
                    if (aiRandom.value < 0.4f) strafe = -strafe;
                }
                float off = dist - (RingDistance + radius);
                // A shield-bearer plants its feet behind the shield instead of prowling, and if a
                // thrower or caller is near, it holds the lane in front of them.
                guarding = guards && dist < 7f;
                Enemy ward = guarding && Role == EnemyRole.ShieldCarrier ? ScreenedAlly() : null;
                if (ward != null)
                {
                    Vector3 toPost = ScreenPoint(player, ward) - transform.position;
                    toPost.y = 0f;
                    return toPost.magnitude > 0.5f ? toPost.normalized * speed * 0.55f : Vector3.zero;
                }
                if (guarding) return Mathf.Abs(off) > 1.2f ? dir * Mathf.Sign(off) * speed * 0.4f : Vector3.zero;

                // Prowl round the hero, watching over one shoulder.
                Vector3 tangent = Vector3.Cross(Vector3.up, dir) * strafe;
                faceDir = (dir + tangent * 0.9f).normalized;
                return tangent * speed * 0.42f + dir * Mathf.Clamp(off, -1f, 1f) * speed * 0.45f;
            }

            case Duel.Close:
                if (dist < radius + StrikeReach + 0.5f)
                {
                    BeginAction("melee");
                    duel = Duel.Windup;
                    duelT = 0.42f + UnseenWarning();
                    Tell(bandit ? 1.05f : 0.95f);
                    SetFlash(0.6f);
                    if (clips != null) clips.Once(meleeClip, 0.75f);
                    return Vector3.zero;
                }
                if (duelT <= 0f) { Backoff(0.2f); ReleaseCommit(); }
                return dir * speed * 1.25f;

            case Duel.Windup:
                turn = duelT > 0.12f;
                if (duelT <= 0f)
                {
                    duel = Duel.Strike;
                    duelT = 0.24f;
                    lungeDir = transform.forward;
                    lungeHit = false;
                    // Its cut shows in the air too, in the colour of a threat.
                    Fx.Slash(transform.position + Vector3.up * (height * 0.6f), transform.rotation * Quaternion.Euler(0f, 0f, 18f),
                        radius + StrikeReach + 0.3f, 130f, new Color(1f, 0.42f, 0.36f), 0.16f, strafe > 0f);
                }
                return Vector3.zero;

            case Duel.Strike:
                turn = false;
                if (!lungeHit && dist < radius + StrikeReach && Vector3.Dot(dir, lungeDir) > 0.2f)
                {
                    lungeHit = true;
                    HitPlayer(player, 1f, transform.position, 9f);
                }
                if (duelT <= 0f)
                {
                    Backoff(aiRandom.Range(0.5f, 0.9f));
                    cooldown = aiRandom.Range(1.3f, 2.4f);
                    ReleaseCommit();
                }
                return dist > radius + 0.8f ? lungeDir * 8.5f : Vector3.zero;

            case Duel.Recover:
                if (duelT <= 0f)
                {
                    duel = Duel.Circle;
                    duelT = aiRandom.Range(0.6f, 2f);
                }
                // Step back out of reach, still watching the hero.
                return dist < RingDistance + radius ? -dir * speed * 0.5f : Vector3.zero;

            default:
                if (dist < RingDistance + radius + 1f)
                {
                    duel = Duel.Circle;
                    duelT = aiRandom.Range(0.2f, 1.4f);
                }
                return dir * speed;
        }
    }

    void Backoff(float seconds)
    {
        duel = Duel.Recover;
        duelT = seconds;
        if (commitHold <= 0f) ReleaseCommit();
    }

    // Launched, staggered or stunned: whatever it was about to do, it is not doing it now.
    void BreakOff()
    {
        guarding = false;
        if (duelist && duel != Duel.Approach) Backoff(0.4f);
        ReleaseCommit();
    }

    // A hit knocks the rank and file out of a swing they have started. Elites push through.
    // Returns false when the enemy shrugs the blow off without flinching.
    bool Flinch()
    {
        if (!duelist) return true;
        if (duel == Duel.Strike) return false;
        if (!Pressing) return true;
        if (elite) return false;
        Backoff(0.45f);
        return true;
    }

    // A raised shield. Light blows from the front glance off (returns false: no damage);
    // a heavy blow breaks the guard and leaves its owner reeling; anything from behind lands.
    bool Guard(Vector3 from, bool heavy)
    {
        Vector3 toSrc = from - transform.position;
        toSrc.y = 0f;
        if (toSrc.sqrMagnitude > 0.01f && Vector3.Dot(toSrc.normalized, transform.forward) < 0.2f) return true;
        if (heavy)
        {
            Game.I.Toast("GUARD BROKEN!", UI.Gold);
            ToonKit.Sfx("crit", 0.9f, 0.7f, position: transform.position + Vector3.up);
            Stagger(1.1f);
            return true;
        }
        Vector3 at = transform.position + Vector3.up * 1.2f + transform.forward * 0.5f;
        Fx.Sparks(at, transform.forward, Color.white, 4);
        ToonKit.Sfx("hit", 0.8f, 1.7f, position: transform.position + Vector3.up);
        knock = -transform.forward * 3f;
        punch = 0.6f;
        Game.I.Toast("BLOCKED - break the guard with a heavy blow", Color.white);
        if (clips != null) clips.Once("Block_Hit", 0.2f);
        NoteBlocked();
        return false;
    }
}
