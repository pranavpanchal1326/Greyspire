using UnityEngine;

// A soft light halo that always faces the camera. Barely there in daylight,
// bright and gently flickering once night falls.
public class Glow : MonoBehaviour
{
    public Color colour = new Color(1f, 0.7f, 0.3f);
    public bool flat;       // lies where it was put (a pool of light on the ground) instead of facing the camera

    Renderer rend;
    float seed;

    void Start()
    {
        rend = GetComponent<Renderer>();
        seed = ProductionRandom.Cosmetic.value * 10f;
    }

    void LateUpdate()
    {
        Camera cam = Camera.main;
        if (cam == null || rend == null || GameClocks.HardPause) return;
        if (!flat) transform.rotation = cam.transform.rotation;
        float night = Game.I != null ? Game.I.Night : 0f;
        float flicker = 0.85f + 0.15f * Mathf.Sin(Time.time * 7f + seed) * Mathf.Sin(Time.time * 2.3f + seed * 2f);
        Color c = colour;
        c.a = flat ? night * 0.34f * flicker : Mathf.Lerp(0.18f, 0.95f, night) * flicker;
        Fx.Tint(rend, c);
    }
}
