using UnityEngine;

// Builds a slime: a dome of wet jelly (Resources/Gel.shader) with a dark knot of
// something at its heart, where the Gloom that moves it sits. No face, no eyes:
// it is a thing, not a pet. The tame one, Mop, is the same made small and blue,
// with a heart that glows.
public static class Gel
{
    public static readonly Color Green = new Color(0.22f, 0.33f, 0.11f), GreenDeep = new Color(0.03f, 0.07f, 0.02f);
    public static readonly Color Blue = new Color(0.25f, 0.5f, 0.85f), BlueDeep = new Color(0.03f, 0.08f, 0.22f);

    static Mesh dome, knot;
    static Shader shader;

    // About a metre across and three quarters of one high at scale 1. glow > 0 lights the heart.
    public static Transform Build(Transform parent, float scale, Color colour, Color deep, float glow = 0f)
    {
        if (shader == null) shader = Resources.Load<Shader>("Gel");
        var root = new GameObject("Slime").transform;
        root.SetParent(parent, false);
        root.localScale = Vector3.one * scale;

        var body = new GameObject("Body");
        body.transform.SetParent(root, false);
        body.AddComponent<MeshFilter>().sharedMesh = Dome();
        var mr = body.AddComponent<MeshRenderer>();
        var mat = GeneratedResources.Own(new Material(shader != null ? shader : Shader.Find("Standard")), root);
        mat.SetColor("_Color", colour);
        mat.SetColor("_Deep", deep);
        mr.sharedMaterial = mat;

        // The heart: seen dimly through the jelly, a little off centre.
        var heart = new GameObject("Heart");
        heart.transform.SetParent(root, false);
        heart.transform.localPosition = new Vector3(0.05f, 0.27f, 0.04f);
        heart.transform.localScale = new Vector3(0.24f, 0.2f, 0.26f);
        heart.AddComponent<MeshFilter>().sharedMesh = Knot();
        var hr = heart.AddComponent<MeshRenderer>();
        var hm = GeneratedResources.Own(new Material(ToonKit.Mat(glow > 0f ? colour : deep * 0.6f)), root);
        hm.SetFloat("_Spec", 0.6f);
        if (glow > 0f) hm.SetColor("_Emit", new Color(colour.r + 0.3f, colour.g + 0.3f, colour.b + 0.3f, glow));
        hr.sharedMaterial = hm;
        hr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return root;
    }

    // A ball flattened where it sits on the ground, sagging a little under its own weight.
    static Mesh Dome()
    {
        if (dome != null) return dome;
        const int around = 28, up = 18;
        var verts = new Vector3[(around + 1) * (up + 1)];
        var tris = new int[around * up * 6];
        for (int j = 0; j <= up; j++)
        {
            float v = j / (float)up, polar = v * Mathf.PI;           // 0 at the crown, pi at the foot
            float y = Mathf.Cos(polar), ring = Mathf.Sin(polar);
            for (int i = 0; i <= around; i++)
            {
                float a = i % around * Mathf.PI * 2f / around;
                float bulge = 1f + 0.12f * Mathf.Clamp01(-y + 0.2f);  // wider toward the foot
                float h = y > -0.25f ? (y + 0.25f) / 1.25f * 0.72f : (y + 0.25f) * 0.05f;
                float r = y > -0.25f ? ring * bulge : ring * bulge * Mathf.Lerp(1f, 0.6f, Mathf.InverseLerp(-0.25f, -1f, y));
                verts[j * (around + 1) + i] = new Vector3(Mathf.Sin(a) * r * 0.52f, h + 0.02f, Mathf.Cos(a) * r * 0.52f);
            }
        }
        int q = 0;
        for (int j = 0; j < up; j++)
            for (int i = 0; i < around; i++)
            {
                int a = j * (around + 1) + i, b = a + around + 1;
                tris[q++] = a; tris[q++] = b; tris[q++] = a + 1;
                tris[q++] = a + 1; tris[q++] = b; tris[q++] = b + 1;
            }
        dome = new Mesh { name = "SlimeDome", vertices = verts, triangles = tris };
        dome.RecalculateNormals();
        dome.RecalculateBounds();
        return dome;
    }

    static Mesh Knot()
    {
        if (knot != null) return knot;
        var tmp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        knot = tmp.GetComponent<MeshFilter>().sharedMesh;
        if (Application.isPlaying) Object.Destroy(tmp); else Object.DestroyImmediate(tmp);
        return knot;
    }
}
