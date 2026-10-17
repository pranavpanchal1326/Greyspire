using System;
using System.Collections.Generic;
using UnityEngine;

// Things that fly: the bolts of gloom that Puglins and the Gloom King fire at the
// hero, the knives bandits throw, and the blade of colour the hero throws back.
// A bolt is a knot of dark with a violet corona, dragging a tail of light. A knife
// is a knife, turning end over end. The hero's blade is a streak of blue-white.
public class Projectile : MonoBehaviour
{
    static readonly Color BoltColor = new Color(0.78f, 0.36f, 0.95f);
    static readonly Color BladeColor = new Color(0.5f, 0.85f, 1f);
    static readonly Color SteelColor = new Color(0.82f, 0.84f, 0.9f);

    Vector3 velocity;
    Color colour = BoltColor;
    float life = 4f, emberT, damage;
    bool friendly, knife, spent;
    Actor emitter;
    string actionId;
    const float HitRadius = 0.25f;
    readonly RaycastHit[] sweepHits = new RaycastHit[32];
    readonly Collider[] startOverlaps = new Collider[16];
    struct ActorContact { public Actor actor; public float distance; public int order; }
    ActorContact[] actorContacts = new ActorContact[32];
    static readonly IComparer<ActorContact> ActorOrder = Comparer<ActorContact>.Create((a, b) =>
    {
        int distance = a.distance.CompareTo(b.distance);
        return distance != 0 ? distance : a.order.CompareTo(b.order);
    });
    Transform face;         // the lights: always turned to the eye
    Transform spin;         // the knife itself
    Renderer corona;

    static Projectile Make(string name, Vector3 origin, Vector3 dir, float speed)
    {
        var go = new GameObject(name);
        go.transform.SetParent(Game.I.LevelRoot, false);
        go.transform.SetPositionAndRotation(origin, Quaternion.LookRotation(dir));
        var p = go.AddComponent<Projectile>();
        p.velocity = dir * speed;
        return p;
    }

    void Lights()
    {
        face = new GameObject("Lights").transform;
        face.SetParent(transform, false);
    }

    void Tail(float width, float seconds, Color from)
    {
        var trail = gameObject.AddComponent<TrailRenderer>();
        trail.sharedMaterial = Fx.Flat;
        trail.time = seconds;
        trail.startWidth = width;
        trail.endWidth = 0f;
        trail.minVertexDistance = 0.08f;
        trail.numCapVertices = 3;
        trail.startColor = from;
        trail.endColor = new Color(from.r, from.g, from.b, 0f);
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trail.receiveShadows = false;
    }

    public static void Fire(Vector3 origin, Vector3 dir, float speed = 11f, bool knife = false, Actor emitter = null)
    {
        Projectile p = Make(knife ? "Knife" : "Bolt", origin, dir, speed);
        p.emitter = emitter; p.actionId = emitter != null ? emitter.ActionId : "";
        p.knife = knife;
        if (knife)
        {
            p.colour = SteelColor;
            GameObject blade = Humans.Furniture("Table_Knife");
            if (blade != null)
            {
                p.spin = new GameObject("Spin").transform;
                p.spin.SetParent(p.transform, false);
                blade.transform.SetParent(p.spin, false);
                blade.transform.localScale *= 1.9f;
                blade.transform.localPosition = new Vector3(0f, 0f, 0.1f);     // turns about its balance, not its tip
            }
            p.Tail(0.05f, 0.1f, new Color(0.9f, 0.92f, 1f, 0.35f));
            return;
        }
        p.Lights();
        p.corona = Fx.Billboard(p.face, 1.05f, new Color(BoltColor.r, BoltColor.g, BoltColor.b, 0.38f));
        Fx.Billboard(p.face, 0.5f, new Color(1.5f, 0.8f, 1.7f, 0.95f)).transform.localPosition = Vector3.back * 0.01f;
        Fx.Billboard(p.face, 0.24f, new Color(0.07f, 0.02f, 0.12f, 1f)).transform.localPosition = Vector3.back * 0.02f;
        p.Tail(0.3f, 0.32f, new Color(BoltColor.r, BoltColor.g, BoltColor.b, 0.55f));
    }

    // The hero's throw: fast, straight, and it stops at the first enemy it meets.
    public static void Throw(Vector3 origin, Vector3 dir, float speed, float damage)
    {
        Projectile p = Make("ThrownBlade", origin, dir, speed);
        p.emitter = Game.I.Player; p.actionId = p.emitter.ActionId;
        p.friendly = true;
        p.damage = damage;
        p.colour = BladeColor;
        p.life = 1.6f;
        Fx.Streak(p.transform, 0.3f, 1.1f, new Color(1.5f, 1.9f, 2.2f, 0.95f)).transform.localPosition = Vector3.back * 0.75f;
        p.Lights();
        p.corona = Fx.Billboard(p.face, 0.7f, new Color(BladeColor.r, BladeColor.g, BladeColor.b, 0.4f));
        Fx.Billboard(p.face, 0.2f, new Color(2f, 2.2f, 2.4f, 1f)).transform.localPosition = Vector3.back * 0.01f;
        p.Tail(0.24f, 0.2f, new Color(BladeColor.r, BladeColor.g, BladeColor.b, 0.5f));
    }

    void Update()
    {
        if (Game.I.State == Game.GameState.Playing) Advance(GameClocks.SimulationDelta);
    }

    void Advance(float dt)
    {
        if (spent || dt <= 0f) return;
        Vector3 start = transform.position;
        Vector3 dir = velocity.normalized;
        float activeTime = Mathf.Min(dt, Mathf.Max(0f, life));
        if (activeTime <= 0f) { Pop(); return; }
        float step = velocity.magnitude * activeTime;
        life -= dt;

        // SphereCast does not report colliders already overlapping its start.
        int overlaps = Physics.OverlapSphereNonAlloc(start, HitRadius, startOverlaps, ~0, QueryTriggerInteraction.Ignore);
        Collider[] initial = overlaps == startOverlaps.Length
            ? Physics.OverlapSphere(start, HitRadius, ~0, QueryTriggerInteraction.Ignore) : startOverlaps;
        int initialCount = initial == startOverlaps ? overlaps : initial.Length;
        for (int i = 0; i < initialCount; i++)
            if (CombatContact.Solid(initial[i])) { Pop(); return; }
        float solidDistance = float.PositiveInfinity;
        if (step > 0f)
        {
            int count = Physics.SphereCastNonAlloc(start, HitRadius, dir, sweepHits, step, ~0, QueryTriggerInteraction.Ignore);
            RaycastHit[] hits = count == sweepHits.Length
                ? Physics.SphereCastAll(start, HitRadius, dir, step, ~0, QueryTriggerInteraction.Ignore) : sweepHits;
            if (hits != sweepHits) count = hits.Length;
            for (int i = 0; i < count; i++)
                if (CombatContact.Solid(hits[i].collider)) solidDistance = Mathf.Min(solidDistance, hits[i].distance);
        }
        int actorCount = friendly ? Enemy.All.Count : 1;
        if (actorContacts.Length < actorCount) Array.Resize(ref actorContacts, actorCount);
        int candidates = 0;
        for (int i = 0; i < actorCount; i++)
        {
            Actor actor = friendly ? (Actor)Enemy.All[i] : Game.I.Player;
            float distance = CombatContact.SweepActor(start, dir, step, HitRadius, actor, activeTime / dt);
            if (float.IsInfinity(distance) || distance >= solidDistance) continue;
            actorContacts[candidates++] = new ActorContact { actor = actor, distance = distance, order = i };
        }
        Array.Sort(actorContacts, 0, candidates, ActorOrder);
        for (int i = 0; i < candidates; i++)
            if (Contact(actorContacts[i].actor, start + dir * actorContacts[i].distance, start)) return;
        if (!float.IsInfinity(solidDistance))
        {
            transform.position = start + dir * solidDistance;
            Pop();
            return;
        }
        transform.position = start + dir * step;
        if (face != null && Camera.main != null) face.rotation = Camera.main.transform.rotation;
        if (spin != null) spin.Rotate(1000f * dt, 0f, 0f, Space.Self);
        if (corona != null) corona.transform.localScale = Vector3.one * (friendly ? 0.7f : 1.05f) * (1f + Mathf.Sin(life * 26f) * 0.1f);
        if (!knife && (emberT -= dt) <= 0f)
        {
            emberT = 0.045f;
            Fx.Spark(transform.position + ProductionRandom.Cosmetic.insideUnitSphere * 0.12f, -dir * 1.5f + ProductionRandom.Cosmetic.insideUnitSphere * 0.7f,
                new Color(colour.r, colour.g, colour.b, 0.8f), ProductionRandom.Cosmetic.Range(0.06f, 0.13f), ProductionRandom.Cosmetic.Range(0.25f, 0.5f));
        }
        if (life <= 0f) Pop();
    }

    bool Contact(Actor actor, Vector3 point, Vector3 from)
    {
        if (actor == null || !actor.CombatEligible || (friendly ? !(actor is Enemy) : !(actor is PlayerController))) return false;
        var result = actor.ReceiveHit(new DamageRequest(friendly ? damage : 1f, from, friendly ? 8f : 7f,
            source: emitter, kind: DamageKind.Projectile, actionId: actionId));
        // Dodge/immunity lets a bolt continue; a block/parry consumes the contact.
        if (!result.StopsProjectile) return false;
        transform.position = point;
        if (friendly && result.Damaged)
        {
            Fx.Sparks(point, -velocity.normalized + Vector3.up * 0.4f, colour, 6);
            ToonKit.Sfx("hit", 0.9f, 1.4f, position: transform.position + Vector3.up);
            Game.I.AddCombo(1);
            Game.I.Player.AddBurst(4f);
        }
        Pop();
        return true;
    }

    // Steel rings off whatever it strikes; a bolt goes out in a puff of its own dark light.
    void Pop()
    {
        if (spent) return;
        spent = true;
        if (knife) Fx.Sparks(transform.position, -velocity.normalized + Vector3.up * 0.5f, SteelColor, 3);
        else Fx.Burst(transform.position, colour, 7, 2.6f, 0.8f);
        gameObject.SetActive(false);
        Destroy(gameObject);
    }
}
