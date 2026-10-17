using UnityEngine;

// Expanding, fading ground ring.
public class RingFx : MonoBehaviour
{
    public Color color = Color.white;
    public float r0 = 0.5f, r1 = 4f, duration = 0.4f;

    Renderer rend;
    float age;

    void Start()
    {
        rend = GetComponent<Renderer>();
        Apply();
    }

    void Update()
    {
        age += GameClocks.EffectsDelta;
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
        float eased = 1f - (1f - t) * (1f - t);
        transform.localScale = Vector3.one * Mathf.Lerp(r0, r1, eased);
        Color c = color;
        c.a *= 1f - t;
        Fx.Tint(rend, c);
    }
}
