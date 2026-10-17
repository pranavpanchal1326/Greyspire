using UnityEngine;

// Ground marker that warns where an attack will land. The inner disc fills
// the outline over the wind-up, so "full" always means "now".
public class TelegraphFx : MonoBehaviour
{
    Renderer ring, disc;
    Color color;
    float radius, duration, age;

    public void Setup(Renderer ring, Renderer disc, float radius, float duration, Color color)
    {
        this.ring = ring; this.disc = disc;
        this.radius = radius; this.duration = duration; this.color = color;
        ring.transform.localScale = Vector3.one * radius;
        Apply();
    }

    void Update()
    {
        age += GameClocks.SimulationDelta;
        if (age >= duration)
        {
            Destroy(gameObject);
            return;
        }
        Apply();
    }

    void Apply()
    {
        float t = Mathf.Clamp01(age / duration);
        disc.transform.localScale = Vector3.one * Mathf.Max(0.01f, radius * t);
        Color c = color;
        c.a = 0.85f;
        Fx.Tint(ring, c);
        // The expanding shape retains timing at zero effects; rapid pulsing does not.
        c.a = 0.35f + 0.15f * Mathf.PingPong(age * 6f, 1f) * Save.Flashes;
        Fx.Tint(disc, c);
    }
}
