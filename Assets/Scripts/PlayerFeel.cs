using UnityEngine;

// Targeting and short-lived input intent are shared by movement, combat and the camera.
public partial class PlayerController
{
    const float LockRange = 18f;
    enum BufferedAction { None, Light, Heavy, Throw, Dodge }
    BufferedAction bufferedAction;
    float bufferedUntil, lockHidden;
    Vector3 bufferedDirection;
    readonly RaycastHit[] sightHits = new RaycastHit[32];

    public Enemy LockedTarget { get; private set; }
    public Vector3 GroundVelocity { get; private set; }
    public bool Grounded => cc != null && cc.isGrounded;
    public bool Dodging => dashT > 0f;
    public bool Attacking => attackT >= 0f || move != Move.None;

    void ClearIntent()
    {
        bufferedAction = BufferedAction.None;
        bufferedUntil = 0f;
        queued = false;
        jumpBuffer = heldT = 0f;
    }

    void Buffer(BufferedAction action, Vector3 direction)
    {
        // A dodge takes precedence over an offensive input on the same frame.
        if (bufferedAction == BufferedAction.Dodge && Time.time < bufferedUntil && action != BufferedAction.Dodge) return;
        PlaytestLog.Emit("input_buffer", source: this, note: action.ToString());
        bufferedAction = action;
        bufferedDirection = direction;
        bufferedUntil = Time.time + Access.Buffer;
    }

    void ConsumeBufferedAction(Vector3 direction, float availableAt = float.NaN)
    {
        if (bufferedAction == BufferedAction.None) return;
        // Light contact is resolved by TickAttack before its cancel window. A
        // long frame must not expire a dodge that was valid when that window opened.
        if (float.IsNaN(availableAt) && bufferedAction == BufferedAction.Dodge && attackT >= 0f
            && attackT < attackDur * Weapon.dodgeCancel) return;
        float at = float.IsNaN(availableAt) ? Time.time : availableAt;
        if (at > bufferedUntil) { PlaytestLog.Emit("input_expired", source: this, note: bufferedAction.ToString()); bufferedAction = BufferedAction.None; return; }
        Vector3 wish = direction.sqrMagnitude > 0.01f ? direction : bufferedDirection;
        bool ground = cc.isGrounded;
        bool free = move == Move.None;
        BufferedAction action = bufferedAction;
        if (action == BufferedAction.Dodge)
        {
            // Light attacks can be cancelled at the weapon family's window; heavy moves keep their commitment.
            bool cancel = free && (attackT < 0f || attackT >= attackDur * Weapon.dodgeCancel);
            cancel |= move == Move.Block || move == Move.Charge;
            cancel |= move != Move.None && move != Move.Plunge && move != Move.Slide && moveHit && moveT >= moveDur * 0.8f;
            if (dashCd > 0f || dashT > 0f || !cancel) return;
            bufferedAction = BufferedAction.None;
            StartDash(wish);
            return;
        }
        if (action == BufferedAction.Light)
        {
            if (move == Move.Block && bashCd <= 0f && Weapon.CanBash) { bufferedAction = BufferedAction.None; StartBash(); return; }
            if (!free) return;
            if (dashT > 0f || dashGrace > 0f) { bufferedAction = BufferedAction.None; StartStab(wish); return; }
            if (attackT >= 0f)
            {
                // Store one follow-up only during the end of this swing, never an entire spammed chain.
                if (attackT >= attackDur * 0.45f) { queued = true; bufferedAction = BufferedAction.None; }
                return;
            }
            bufferedAction = BufferedAction.None;
            if (!ground) StartPlunge(); else StartAttack(wish);
            return;
        }
        if (!free || dashT > 0f) return;
        if (action == BufferedAction.Heavy && ground)
        {
            if (attackT >= 0f && attackT < attackDur * 0.65f) return;
            bufferedAction = BufferedAction.None;
            if (Game.HasSkill(Game.SkillKick) && (attackT >= 0f || comboReset > 0f)) StartKick();
            else StartHeavy(0f);
        }
        else if (action == BufferedAction.Throw && attackT < 0f && throwCd <= 0f)
        {
            bufferedAction = BufferedAction.None;
            StartThrow();
        }
    }

    public void ClearLock()
    {
        LockedTarget = null;
        lockHidden = 0f;
    }

    bool TargetVisible(Enemy enemy)
    {
        if (enemy == null || !enemy.CombatEligible || !enemy.gameObject.activeInHierarchy) return false;
        Vector3 from = transform.position + Vector3.up * 1.2f;
        Vector3 to = enemy.transform.position + Vector3.up * Mathf.Clamp(enemy.height * 0.65f, 0.45f, 1.6f) - from;
        int count = Physics.RaycastNonAlloc(from, to.normalized, sightHits, to.magnitude, ~0, QueryTriggerInteraction.Ignore);
        // A full buffer is treated as obstructed rather than locking through unknown geometry.
        if (count == sightHits.Length) return false;
        for (int i = 0; i < count; i++)
        {
            Collider obstacle = sightHits[i].collider;
            if (obstacle.GetComponentInParent<Actor>() != null || obstacle.GetComponentInParent<Pickup>() != null) continue;
            return false;
        }
        return true;
    }

    Enemy FindLockTarget(Enemy exclude = null, int sideOfScreen = 0)
    {
        Camera view = Game.I.Cam.GetComponent<Camera>();
        Enemy best = null;
        float score = float.MaxValue;
        float oldX = exclude != null ? view.WorldToViewportPoint(exclude.transform.position).x : 0.5f;
        foreach (Enemy enemy in Enemy.All)
        {
            if (enemy == exclude || enemy == null || !enemy.CombatEligible) continue;
            float distance = (enemy.transform.position - transform.position).magnitude;
            if (distance > LockRange || !TargetVisible(enemy)) continue;
            Vector3 screen = view.WorldToViewportPoint(enemy.transform.position + Vector3.up * enemy.height * 0.6f);
            if (screen.z <= 0f || screen.x < 0.03f || screen.x > 0.97f || screen.y < 0.03f || screen.y > 0.97f) continue;
            if (sideOfScreen != 0 && (screen.x - oldX) * sideOfScreen < 0.025f) continue;
            float candidate = sideOfScreen == 0
                ? Mathf.Abs(screen.x - 0.5f) * 12f + Mathf.Abs(screen.y - 0.5f) * 4f + distance * 0.12f
                : Mathf.Abs(screen.x - oldX) * 12f + distance * 0.08f;
            if (candidate < score) { score = candidate; best = enemy; }
        }
        return best;
    }

    void TickTargeting(float dt)
    {
        if (Game.I.InTown || Dead) { ClearLock(); return; }
        if (LockedTarget != null)
        {
            if (!LockedTarget.CombatEligible || !LockedTarget.gameObject.activeInHierarchy)
            {
                LockedTarget = FindLockTarget(LockedTarget);
                lockHidden = 0f;
            }
            else if ((LockedTarget.transform.position - transform.position).sqrMagnitude > 22f * 22f) ClearLock();
            else
            {
                lockHidden = TargetVisible(LockedTarget) ? 0f : lockHidden + dt;
                if (lockHidden > 0.65f) ClearLock();
            }
        }
        if (GameInput.LockPressed)
        {
            if (LockedTarget != null) ClearLock();
            else { LockedTarget = FindLockTarget(); lockHidden = 0f; }
        }
        int cycle = GameInput.TargetCycle;
        if (LockedTarget != null && cycle != 0)
        {
            Enemy next = FindLockTarget(LockedTarget, cycle) ?? FindLockTarget(LockedTarget);
            if (next != null) { LockedTarget = next; lockHidden = 0f; }
        }
    }

    Enemy CombatTarget(float range, Vector3 wish)
    {
        if (LockedTarget != null && LockedTarget.CombatEligible && (LockedTarget.transform.position - transform.position).magnitude - LockedTarget.radius <= range
            && TargetVisible(LockedTarget)) return LockedTarget;
        Vector3 forward = wish.sqrMagnitude > 0.01f ? wish.normalized : transform.forward;
        Enemy best = null;
        float bestScore = float.MaxValue;
        foreach (Enemy enemy in Enemy.All)
        {
            if (enemy == null || !enemy.CombatEligible || !enemy.gameObject.activeInHierarchy) continue;
            Vector3 to = Flat(enemy.transform.position - transform.position);
            float distance = Vector3.Distance(enemy.transform.position, transform.position) - enemy.radius;
            if (distance > range) continue;
            float alignment = Vector3.Dot(forward, to.normalized);
            if (to.magnitude > 1f && alignment < 0.25f) continue;
            if (!TargetVisible(enemy)) continue;
            float score = distance + (1f - alignment) * 3f;
            if (score < bestScore) { bestScore = score; best = enemy; }
        }
        return best;
    }
}
