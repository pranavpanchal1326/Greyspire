using System;
using UnityEngine;

public enum DamageKind { Physical, Projectile, Magic, Environment }
public enum HitOutcome { Miss, Immune, Blocked, Parried, Damaged, Killed, Defeated, Revived }

// Attack metadata travels with the request; the victim is never used as a mailbox.
public readonly struct DamageRequest
{
    public readonly float amount, push, stagger, launch;
    public readonly Vector3 from;
    public readonly bool heavy, critical;
    public readonly Actor source;
    public readonly DamageKind kind;
    public readonly string actionId;
    public DamageRequest(float amount, Vector3 from, float push, bool heavy = false, bool critical = false,
        Actor source = null, DamageKind kind = DamageKind.Physical, float stagger = 0f, float launch = 0f, string actionId = null)
    {
        this.amount = amount; this.from = from; this.push = push; this.heavy = heavy;
        this.critical = critical; this.source = source; this.kind = kind;
        this.stagger = stagger; this.launch = launch;
        this.actionId = actionId ?? (source != null ? source.ActionId : "");
    }
    public DamageRequest WithAmount(float value) => new DamageRequest(value, from, push, heavy, critical, source, kind, stagger, launch, actionId);
}

public readonly struct HitResult
{
    public readonly HitOutcome outcome;
    public readonly float damage;
    public readonly bool guardBroken, staggered, launched;
    public bool Damaged => damage > 0f;
    public bool StopsProjectile => Damaged || outcome == HitOutcome.Blocked || outcome == HitOutcome.Parried;
    public HitResult(HitOutcome outcome, float damage = 0f, bool guardBroken = false, bool staggered = false, bool launched = false)
    { this.outcome = outcome; this.damage = damage; this.guardBroken = guardBroken; this.staggered = staggered; this.launched = launched; }
}

public static class CombatContact
{
    static readonly RaycastHit[] CoverHits = new RaycastHit[32];
    public static bool ValidDamage(float amount) => amount > 0f && !float.IsNaN(amount) && !float.IsInfinity(amount);

    public static bool Solid(Collider collider)
    {
        return collider != null && !collider.isTrigger && collider.GetComponentInParent<Actor>() == null
            && collider.GetComponentInParent<Companion>() == null && collider.GetComponentInParent<Pickup>() == null;
    }

    // Test every solid hit: an ignored actor cannot hide a wall farther along the ray.
    public static bool ClearPath(Vector3 from, Vector3 to)
    {
        Vector3 delta = to - from;
        float length = delta.magnitude;
        if (length < 0.001f) return true;
        int count = Physics.RaycastNonAlloc(from, delta / length, CoverHits, length, ~0, QueryTriggerInteraction.Ignore);
        if (count == CoverHits.Length)
        {
            foreach (var hit in Physics.RaycastAll(from, delta / length, length, ~0, QueryTriggerInteraction.Ignore))
                if (Solid(hit.collider)) return false;
        }
        else for (int i = 0; i < count; i++) if (Solid(CoverHits[i].collider)) return false;
        return true;
    }

    public static bool CanContact(Vector3 origin, Actor target, float halfHeight = 0.8f, bool ignoreCover = false)
    {
        if (target == null || !target.CombatEligible) return false;
        Capsule(target, out Vector3 centre, out Vector3 axis, out float radius);
        float halfBody = Mathf.Abs(axis.y) + radius;
        float bottom = centre.y - halfBody, top = centre.y + halfBody, height = top - bottom;
        if (top < origin.y - halfHeight || bottom > origin.y + halfHeight) return false;
        Vector3 point = centre;
        point.y = Mathf.Clamp(origin.y, bottom + Mathf.Min(0.15f, height * 0.25f), top - Mathf.Min(0.15f, height * 0.25f));
        return ignoreCover || ClearPath(origin, point);
    }

    // Sweep the authoritative capsule directly. Newly spawned/moved controllers
    // need not wait for a physics step to register their query shape.
    public static float SweepActor(Vector3 from, Vector3 direction, float length, float projectileRadius, Actor actor, float motionFraction = 1f)
    {
        if (actor == null || !actor.CombatEligible) return float.PositiveInfinity;
        // Work in the translating actor's frame, then convert contact time back
        // to distance along the projectile's world-space segment.
        Vector3 motion = actor.ContactMotion;
        Vector3 relative = direction * length - motion * Mathf.Clamp01(motionFraction);
        float relativeLength = relative.magnitude;
        float entry = SweepStationaryActor(from + motion, relativeLength > 0.000001f ? relative / relativeLength : Vector3.forward,
            relativeLength, projectileRadius, actor);
        return float.IsInfinity(entry) ? entry : relativeLength > 0.000001f ? entry / relativeLength * length : 0f;
    }

    static float SweepStationaryActor(Vector3 from, Vector3 direction, float length, float projectileRadius, Actor actor)
    {
        Capsule(actor, out Vector3 centre, out Vector3 axis, out float bodyRadius);
        Vector3 a = centre - axis, b = centre + axis, ba = b - a, oa = from - a;
        float radius = bodyRadius + projectileRadius, baba = Vector3.Dot(ba, ba);
        float nearest = baba > 0.000001f ? Mathf.Clamp01(Vector3.Dot(oa, ba) / baba) : 0f;
        if ((from - (a + ba * nearest)).sqrMagnitude <= radius * radius) return 0f;
        float best = Mathf.Min(SphereEntry(from, direction, a, radius), SphereEntry(from, direction, b, radius));
        if (baba > 0.000001f)
        {
            float bard = Vector3.Dot(ba, direction), baoa = Vector3.Dot(ba, oa);
            float qa = baba - bard * bard;
            float qb = baba * Vector3.Dot(direction, oa) - baoa * bard;
            float qc = baba * oa.sqrMagnitude - baoa * baoa - radius * radius * baba;
            float discriminant = qb * qb - qa * qc;
            if (qa > 0.000001f && discriminant >= 0f)
            {
                float entry = (-qb - Mathf.Sqrt(discriminant)) / qa;
                float along = baoa + entry * bard;
                if (entry >= 0f && along >= 0f && along <= baba) best = Mathf.Min(best, entry);
            }
        }
        return best <= length ? best : float.PositiveInfinity;
    }

    static float SphereEntry(Vector3 from, Vector3 direction, Vector3 centre, float radius)
    {
        Vector3 offset = from - centre;
        float forward = Vector3.Dot(offset, direction);
        float discriminant = forward * forward - (offset.sqrMagnitude - radius * radius);
        if (discriminant < 0f) return float.PositiveInfinity;
        float entry = -forward - Mathf.Sqrt(discriminant);
        return entry >= 0f ? entry : float.PositiveInfinity;
    }

    static void Capsule(Actor actor, out Vector3 centre, out Vector3 axis, out float radius)
    {
        var controller = actor.GetComponent<CharacterController>();
        Vector3 scale = actor.transform.lossyScale;
        radius = (controller != null ? controller.radius : 0.4f) * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
        float height = (controller != null ? controller.height : 1.8f) * Mathf.Abs(scale.y);
        centre = controller != null ? actor.transform.TransformPoint(controller.center) : actor.transform.position + Vector3.up * height * 0.5f;
        axis = actor.transform.up * Mathf.Max(0f, height * 0.5f - radius);
    }
}
