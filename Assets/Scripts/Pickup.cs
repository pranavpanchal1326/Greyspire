using UnityEngine;

// Sparks (coins) and hearts dropped by enemies. They pop out, settle, then fly
// to the player when close, or from anywhere once the wave is cleared.
public class Pickup : MonoBehaviour
{
    public enum Kind { Coin, Heart }

    public static bool AttractAll;

    Kind kind;
    int value = 1;
    Vector3 velocity;
    Transform face;         // the part that is light, and always turns to the eye
    float age;
    Vector3 origin;
    readonly RaycastHit[] contacts = new RaycastHit[24];

    public static void Spawn(Kind kind, Vector3 pos, int value)
    {
        var go = new GameObject(kind.ToString());
        go.transform.SetParent(Game.I.LevelRoot, false);
        go.transform.position = pos;
        var p = go.AddComponent<Pickup>();
        if (kind == Kind.Coin)
        {
            // A Spark is what it says: a mote of loose colour, bright enough to bloom.
            p.face = new GameObject("Spark").transform;
            p.face.SetParent(go.transform, false);
            Fx.Billboard(p.face, 0.13f, new Color(2.4f, 2f, 1.1f, 1f));
            Fx.Billboard(p.face, 0.6f, new Color(1f, 0.8f, 0.35f, 0.45f));
        }
        else
        {
            // A draught of red: a stoppered bottle, glowing a little.
            GameObject bottle = Humans.Furniture("Potion_1");
            if (bottle != null)
            {
                bottle.transform.SetParent(go.transform, false);
                bottle.transform.localScale *= 2.4f;
                bottle.transform.localPosition = Vector3.down * 0.15f;
                foreach (Renderer glass in bottle.GetComponentsInChildren<Renderer>())
                {
                    Material[] mats = glass.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        mats[i] = GeneratedResources.Own(new Material(mats[i]) { color = new Color(0.9f, 0.2f, 0.22f) }, go.transform);
                        mats[i].SetFloat("_Spec", 0.8f);
                        mats[i].SetColor("_Emit", new Color(0.9f, 0.12f, 0.12f, 0.45f));
                    }
                    glass.sharedMaterials = mats;
                }
            }
            p.face = new GameObject("Glow").transform;
            p.face.SetParent(go.transform, false);
            Fx.Billboard(p.face, 0.9f, new Color(1f, 0.25f, 0.25f, 0.3f));
        }
        p.kind = kind;
        p.origin = pos;
        p.value = value;
        Vector2 r = ProductionRandom.Scatter.insideUnitCircle * 3.5f;
        p.velocity = new Vector3(r.x, ProductionRandom.Scatter.Range(4f, 7f), r.y);
    }

    void Update()
    {
        if (Game.I.State != Game.GameState.Playing) return;
        if (Game.I.DefeatPending) return;
        Advance(GameClocks.SimulationDelta);
    }

    void Advance(float dt)
    {
        if (dt <= 0f) return;
        age += dt;
        transform.Rotate(0, 120f * dt, 0, Space.World);
        if (face != null && Camera.main != null) face.rotation = Camera.main.transform.rotation;

        PlayerController player = Game.I.Player;
        Vector3 to = player.transform.position + Vector3.up * 0.9f - transform.position;
        float dist = to.magnitude;

        if (age > 0.35f && (AttractAll || (dist < player.stats.magnet
            && CombatContact.ClearPath(transform.position, player.transform.position + Vector3.up * 0.9f)))
            && !player.Dead && !Game.I.DefeatPending)
        {
            // Accelerates as it gets close so collecting feels snappy.
            float speed = (AttractAll ? 26f : 12f) + age * 6f;
            transform.position += to.normalized * Mathf.Min(dist, speed * dt);
            if (dist < 0.8f)
            {
                Game.I.Collect(kind, value);
                Destroy(gameObject);
            }
            return;
        }

        velocity.y -= 20f * dt;
        Vector3 start = transform.position, travel = velocity * dt;
        Vector3 pos = start + travel;
        int count = Physics.SphereCastNonAlloc(start, 0.18f, travel.normalized, contacts, travel.magnitude, ~0, QueryTriggerInteraction.Ignore);
        RaycastHit[] found = count == contacts.Length ? Physics.SphereCastAll(start, 0.18f, travel.normalized,
            travel.magnitude, ~0, QueryTriggerInteraction.Ignore) : contacts;
        if (found != contacts) count = found.Length;
        float nearest = float.PositiveInfinity;
        RaycastHit contact = default;
        for (int i = 0; i < count; i++)
            if (WorldSupport.Geometry(found[i].collider) && found[i].distance < nearest)
            { nearest = found[i].distance; contact = found[i]; }
        if (!float.IsInfinity(nearest))
        {
            pos = start + travel.normalized * Mathf.Max(0f, nearest - 0.02f);
            velocity = Vector3.Reflect(velocity, contact.normal) * 0.35f;
        }
        transform.position = pos;

        // An inaccessible drop is recovered, never silently discarded. Clear-
        // wave attraction remains an intentional magical collection policy.
        if (pos.y < origin.y - 15f || age > 30f)
        {
            Vector3 recover = age > 30f ? player.transform.position : origin;
            if (!WorldSupport.TryGround(recover, out var support, 0.35f, 2f)
                && !WorldSupport.TryGround(player.transform.position, out support, 0.35f, 2f)) return;
            transform.position = support.point + Vector3.up * 0.5f;
            velocity = Vector3.zero; age = 1f;
        }
    }
}
