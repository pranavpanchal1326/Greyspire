using UnityEngine;

// The hero's companion: the last spark of colour. Not a creature: a mote of warm
// light that keeps by Pip's shoulder, breathing a little, shedding a few sparks.
public class Lumi : MonoBehaviour
{
    public Transform target;

    static readonly Color Warm = new Color(1f, 0.86f, 0.42f);

    Vector3 vel;
    float sparkleT;
    Renderer core, halo;

    public static Lumi Create(Transform target)
    {
        var go = new GameObject("Lumi");
        var l = go.AddComponent<Lumi>();
        l.target = target;
        l.core = Quad(go.transform, 0.16f);
        l.halo = Quad(go.transform, 0.75f);
        go.transform.position = target.position + Vector3.up * 2f;
        return l;
    }

    static Renderer Quad(Transform parent, float size)
    {
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        DestroyImmediate(q.GetComponent<Collider>());
        q.transform.SetParent(parent, false);
        q.transform.localScale = Vector3.one * size;
        var r = q.GetComponent<Renderer>();
        r.sharedMaterial = Fx.Mote;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return r;
    }

    void LateUpdate()
    {
        if (target == null || GameClocks.HardPause) return;
        float t = GameClocks.PresentationTime;
        Vector3 want = target.position + target.right * 0.75f - target.forward * 0.25f
            + Vector3.up * (1.95f + Mathf.Sin(t * 2.4f) * 0.12f);
        transform.position = Vector3.SmoothDamp(transform.position, want, ref vel, 0.22f, 40f, GameClocks.PresentationDelta);

        // Always face the eye, and breathe.
        Camera cam = Camera.main;
        if (cam != null) transform.rotation = cam.transform.rotation;
        float pulse = 0.85f + 0.15f * Mathf.Sin(t * 3.1f);
        Fx.Tint(core, new Color(2.2f, 2f, 1.5f, 1f));
        Fx.Tint(halo, new Color(Warm.r, Warm.g, Warm.b, 0.42f * pulse));

        if (!GameClocks.HardPause && Game.I.LevelRoot != null && (sparkleT -= GameClocks.PresentationDelta) <= 0f)
        {
            sparkleT = 0.3f;
            Fx.Spark(transform.position + ProductionRandom.Cosmetic.insideUnitSphere * 0.12f, Vector3.down * 0.4f + ProductionRandom.Cosmetic.insideUnitSphere * 0.3f, Warm, 0.07f, 0.7f);
        }
    }
}
