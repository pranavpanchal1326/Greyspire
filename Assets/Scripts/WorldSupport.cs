using UnityEngine;

// All positions are world-space feet, never an implicit y=0 plane. Queries are
// bounded around the caller's level so a bridge cannot steal its lower route.
public static class WorldSupport
{
    public const float MaxWalkSlope = 50f;
    public readonly struct Surface
    {
        public readonly Vector3 point, normal;
        public readonly Collider collider;
        public Surface(RaycastHit hit) { point = hit.point; normal = hit.normal; collider = hit.collider; }
    }
    static readonly RaycastHit[] hits = new RaycastHit[32];
    static readonly Collider[] overlaps = new Collider[32];
    public static bool Geometry(Collider c) => CombatContact.Solid(c)
        && c.GetComponentInParent<Resident>() == null && c.GetComponentInParent<Drained>() == null
        && c.GetComponentInParent<NavFollower>() == null;

    public static bool TryGround(Vector3 feet, out Surface surface, float rise = 0.35f, float drop = 1f, float slope = MaxWalkSlope)
    {
        surface = default;
        if (rise < 0f || drop < 0f) return false;
        int count = Physics.RaycastNonAlloc(feet + Vector3.up * rise, Vector3.down, hits, rise + drop,
            ~0, QueryTriggerInteraction.Ignore);
        RaycastHit[] found = count == hits.Length ? Physics.RaycastAll(feet + Vector3.up * rise,
            Vector3.down, rise + drop, ~0, QueryTriggerInteraction.Ignore) : hits;
        if (found != hits) count = found.Length;
        float best = float.PositiveInfinity, minNormal = Mathf.Cos(slope * Mathf.Deg2Rad);
        for (int i = 0; i < count; i++)
        {
            var hit = found[i];
            if (!Geometry(hit.collider) || hit.distance >= best) continue;
            // A nearer unwalkable face is a failure, not permission to stand on
            // a different surface concealed beneath it.
            best = hit.distance;
            surface = new Surface(hit);
        }
        return surface.collider != null && surface.normal.y >= minNormal;
    }

    public static bool ClearBody(Vector3 feet, float radius, float height, Transform owner = null)
    {
        height = Mathf.Max(height, radius * 2f);
        Vector3 bottom = feet + Vector3.up * (radius + 0.06f), top = feet + Vector3.up * (height - radius + 0.06f);
        int count = Physics.OverlapCapsuleNonAlloc(bottom, top, radius, overlaps, ~0, QueryTriggerInteraction.Ignore);
        Collider[] found = count == overlaps.Length ? Physics.OverlapCapsule(bottom, top, radius, ~0,
            QueryTriggerInteraction.Ignore) : overlaps;
        if (found != overlaps) count = found.Length;
        for (int i = 0; i < count; i++)
            if (Geometry(found[i]) && (owner == null || !found[i].transform.IsChildOf(owner))) return false;
        return true;
    }

    public static bool TryStand(Vector3 near, float radius, float height, out Vector3 feet, Transform owner = null,
        float rise = 0.35f, float drop = 1f)
    {
        feet = near;
        if (!TryGround(near, out Surface support, rise, drop)) return false;
        float slopeClearance = radius * (1f / Mathf.Max(0.64f, support.normal.y) - 1f);
        if (!ClearBody(support.point + Vector3.up * slopeClearance, radius, height, owner)) return false;
        feet = support.point + Vector3.up * 0.05f;
        return true;
    }

    // Collision checked fallback for pets, residents, and story departures.
    // Long frames are split into bounded steps; unsupported ledges stop travel.
    public static bool TryMove(Transform owner, Vector3 displacement, float radius, float height)
    {
        displacement.y = 0f;
        displacement = Vector3.ClampMagnitude(displacement, 128f * 0.15f);
        int steps = Mathf.Clamp(Mathf.CeilToInt(displacement.magnitude / 0.15f), 1, 128);
        Vector3 delta = displacement / steps;
        bool moved = false;
        for (int i = 0; i < steps; i++)
        {
            Vector3 from = owner.position;
            if (!TryGround(from + delta, out var support, 0.35f, 0.5f)) break;
            Vector3 next = support.point + Vector3.up * 0.05f;
            // Match a controller's step clearance at a riser/bridge lip. The
            // forward support must be within step height on the same local level.
            if (delta.sqrMagnitude > 0f && TryGround(from + delta + delta.normalized * radius, out var ahead, 0.35f, 0.5f)
                && ahead.point.y > support.point.y && ahead.point.y - support.point.y <= 0.35f)
                next.y = ahead.point.y + 0.05f;
            float clearance = radius * (1f / Mathf.Max(0.64f, support.normal.y) - 1f);
            if (!ClearBody(next + Vector3.up * clearance, radius, height, owner)) break;
            float lift = 0.06f + Mathf.Max(0f, next.y - from.y) + clearance;
            Vector3 a = from + Vector3.up * (radius + lift), b = from + Vector3.up * (Mathf.Max(height, 2f * radius) - radius + lift);
            int count = Physics.CapsuleCastNonAlloc(a, b, radius, delta.normalized, hits, delta.magnitude, ~0, QueryTriggerInteraction.Ignore);
            RaycastHit[] found = count == hits.Length ? Physics.CapsuleCastAll(a, b, radius, delta.normalized,
                delta.magnitude, ~0, QueryTriggerInteraction.Ignore) : hits;
            if (found != hits) count = found.Length;
            bool blocked = false;
            for (int h = 0; h < count; h++)
                if (Geometry(found[h].collider) && !found[h].transform.IsChildOf(owner)) { blocked = true; break; }
            if (blocked) break;
            owner.position = next;
            moved |= (owner.position - from).sqrMagnitude > 0.000001f;
        }
        return moved;
    }

    public static bool CanInteract(Vector3 feet, Vector3 spot, float range = 4f, Transform targetOwner = null)
    {
        return Vector3.Distance(feet, spot) < range && Mathf.Abs(feet.y - spot.y) < 1.2f
            && TryGround(feet, out _, 0.35f, 0.6f) && TryGround(spot, out _, 0.35f, 0.6f)
            && ClearSight(feet + Vector3.up * 1.1f, spot + Vector3.up * 1.1f, targetOwner);
    }

    public static bool ClearSight(Vector3 from, Vector3 to, Transform targetOwner = null)
    {
        Vector3 delta = to - from;
        int count = Physics.RaycastNonAlloc(from, delta.normalized, hits, delta.magnitude, ~0, QueryTriggerInteraction.Ignore);
        RaycastHit[] found = count == hits.Length ? Physics.RaycastAll(from, delta.normalized, delta.magnitude, ~0,
            QueryTriggerInteraction.Ignore) : hits;
        if (found != hits) count = found.Length;
        for (int i = 0; i < count; i++) if (Geometry(found[i].collider)
            && (targetOwner == null || !found[i].transform.IsChildOf(targetOwner))) return false;
        return true;
    }
}
