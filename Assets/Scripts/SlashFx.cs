using UnityEngine;

// The life of a slash arc or a thrust streak: it swells a touch, sweeps a little
// further the way the blade was going, and is gone. It runs on game time, so
// hit-stop holds the cut in the air for the instant a blow lands.
public class SlashFx : MonoBehaviour
{
    public Color colour = Color.white;
    public Vector3 size = Vector3.one;
    public float life = 0.16f;
    public float sweep = 50f;       // degrees the arc turns on over its life

    Renderer rend;
    Quaternion start;
    float age;

    void Start()
    {
        rend = GetComponent<Renderer>();
        start = transform.rotation;
        Apply(0f);
    }

    void Update()
    {
        age += GameClocks.EffectsDelta;
        if (age >= life)
        {
            Destroy(gameObject);
            return;
        }
        Apply(age / life);
    }

    void Apply(float k)
    {
        float ease = 1f - (1f - k) * (1f - k);
        transform.localScale = size * Mathf.Lerp(0.86f, 1.1f, ease);
        if (sweep != 0f) transform.rotation = start * Quaternion.Euler(0f, sweep * ease, 0f);
        // Brighter than white at first, so the bloom catches it.
        float a = Mathf.Pow(1f - k, 1.4f) * colour.a;
        Fx.Tint(rend, new Color(colour.r * 1.6f, colour.g * 1.6f, colour.b * 1.6f, a));
    }
}
