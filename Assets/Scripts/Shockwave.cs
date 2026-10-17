using UnityEngine;

// A ring that rolls outward along the ground. Jump over it or dash through.
public class Shockwave : MonoBehaviour
{
    public float maxRadius = 14f, speed = 9f;
    public int damage = 1;
    public Actor emitter;
    public string actionId;

    Renderer rend;
    float radius = 1f;
    bool hit;

    void Start()
    {
        rend = GetComponent<Renderer>();
    }

    void Update()
    {
        if (Game.I.State != Game.GameState.Playing) return;
        Advance(GameClocks.SimulationDelta);
    }

    void Advance(float dt)
    {
        if (dt <= 0f || radius >= maxRadius) return;
        float previous = radius;
        radius = Mathf.Min(maxRadius, radius + speed * dt);
        transform.localScale = Vector3.one * radius;
        Color c = new Color(1f, 0.45f, 0.3f, Mathf.Clamp01((maxRadius - radius) / 3f));
        Fx.Tint(rend, c);

        PlayerController p = Game.I.Player;
        Vector3 to = p.transform.position - transform.position;
        float height = to.y;
        to.y = 0f;
        if (!hit && CrossesRing(to, p.ContactMotion, previous * 0.93f, radius * 0.93f)
            && height >= -0.2f && height < 0.6f
            && CombatContact.CanContact(transform.position + Vector3.up * 0.3f, p, 0.3f))
        {
            // Only one chance per wave: a dodge (or a hit) both use it up.
            hit = true;
            p.ReceiveHit(new DamageRequest(damage, transform.position, 12f, heavy: damage >= 2f, source: emitter, actionId: actionId));
        }
        // Resolve the final segment before retiring the wave.
        if (radius >= maxRadius) { gameObject.SetActive(false); Destroy(gameObject); }
    }

    static bool CrossesRing(Vector3 end, Vector3 motion, float startRadius, float endRadius)
    {
        motion.y = 0f;
        Vector3 start = end - motion;
        float delta = endRadius - startRadius;
        // The signed radial distance is continuous. Test both band boundaries,
        // including tangencies and targets moving across the ring within a frame.
        if (Mathf.Abs(start.magnitude - startRadius) <= 0.55f || Mathf.Abs(end.magnitude - endRadius) <= 0.55f) return true;
        foreach (float band in new[] { -0.55f, 0.55f })
        {
            float r = startRadius + band;
            float a = motion.sqrMagnitude - delta * delta;
            float b = 2f * (Vector3.Dot(start, motion) - r * delta);
            float c = start.sqrMagnitude - r * r;
            if (Mathf.Abs(a) < 0.000001f)
            {
                if (Mathf.Abs(b) > 0.000001f && Root(-c / b, r, delta)) return true;
                continue;
            }
            float d = b * b - 4f * a * c;
            if (d < 0f) continue;
            float q = Mathf.Sqrt(d);
            if (Root((-b - q) / (2f * a), r, delta) || Root((-b + q) / (2f * a), r, delta)) return true;
        }
        return false;
    }

    static bool Root(float t, float radius, float delta) => t >= 0f && t <= 1f && radius + delta * t >= 0f;
}
