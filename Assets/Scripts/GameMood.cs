using System.Collections.Generic;
using UnityEngine;

// Atmosphere: the things that make each place feel different without changing
// how it plays. Colour grading, fog and drifting particles per region, flowers
// through the grass, and animated water.
public partial class Game
{
    struct Mood
    {
        public Color tint;          // multiplied over the final picture
        public float saturation;
        public Color fog;
        public float fogEnd;
        public Color moteA, moteB;  // ambient particles drift between these colours
        public float moteSize, moteRate;
        public Vector3 drift;       // which way they float
        public Color[] flowers;
    }

    // Same order as the Wilds regions.
    static readonly Mood[] Moods =
    {
        new Mood    // Greenreach Meadow: bright, pollen in the air
        {
            tint = new Color(1f, 0.99f, 0.96f), saturation = 1.14f, fog = new Color(0.72f, 0.9f, 1f), fogEnd = 135f,
            moteA = new Color(1f, 0.95f, 0.6f), moteB = Color.white, moteSize = 0.16f, moteRate = 16f, drift = new Vector3(0.6f, 0.15f, 0.2f),
            flowers = new[] { new Color(1f, 0.45f, 0.6f), new Color(1f, 0.9f, 0.3f), Color.white },
        },
        new Mood    // Whispering Woods: green shade, leaves falling
        {
            tint = new Color(0.92f, 1f, 0.93f), saturation = 1.08f, fog = new Color(0.55f, 0.78f, 0.62f), fogEnd = 90f,
            moteA = new Color(0.55f, 0.85f, 0.35f), moteB = new Color(0.9f, 0.8f, 0.3f), moteSize = 0.2f, moteRate = 20f, drift = new Vector3(0.4f, -0.7f, 0.3f),
            flowers = new[] { Color.white, new Color(0.65f, 0.6f, 1f) },
        },
        new Mood    // Mirror Lake: clear and cool, light off the water
        {
            tint = new Color(0.96f, 1f, 1f), saturation = 1.16f, fog = new Color(0.7f, 0.92f, 1f), fogEnd = 140f,
            moteA = new Color(0.75f, 0.95f, 1f), moteB = Color.white, moteSize = 0.13f, moteRate = 14f, drift = new Vector3(0.2f, 0.3f, 0.1f),
            flowers = new[] { new Color(1f, 0.9f, 0.3f), new Color(1f, 0.55f, 0.7f) },
        },
        new Mood    // Sunscorch Dunes: hot, sand on the wind
        {
            tint = new Color(1f, 0.95f, 0.84f), saturation = 1.2f, fog = new Color(1f, 0.86f, 0.62f), fogEnd = 110f,
            moteA = new Color(1f, 0.85f, 0.55f), moteB = new Color(0.95f, 0.7f, 0.4f), moteSize = 0.14f, moteRate = 26f, drift = new Vector3(2.4f, 0.1f, 0.6f),
            flowers = new[] { new Color(1f, 0.5f, 0.25f) },
        },
        new Mood    // Hollow Graves: drained and misty
        {
            tint = new Color(0.9f, 0.93f, 1f), saturation = 0.78f, fog = new Color(0.62f, 0.64f, 0.72f), fogEnd = 62f,
            moteA = new Color(0.8f, 0.8f, 0.95f, 0.6f), moteB = new Color(0.6f, 0.9f, 0.7f, 0.6f), moteSize = 0.34f, moteRate = 12f, drift = new Vector3(0.25f, 0.12f, 0.1f),
            flowers = new[] { new Color(0.8f, 0.7f, 1f) },
        },
        new Mood    // Shardfield: crystal light rising
        {
            tint = new Color(0.97f, 0.94f, 1f), saturation = 1.22f, fog = new Color(0.55f, 0.45f, 0.8f), fogEnd = 100f,
            moteA = new Color(0.4f, 0.95f, 1f), moteB = new Color(1f, 0.5f, 0.95f), moteSize = 0.15f, moteRate = 24f, drift = new Vector3(0.1f, 0.9f, 0.1f),
            flowers = new[] { new Color(0.4f, 0.95f, 1f), new Color(1f, 0.5f, 0.95f) },
        },
        new Mood    // Goldleaf Vale: warm late light, leaves coming down
        {
            tint = new Color(1f, 0.96f, 0.86f), saturation = 1.2f, fog = new Color(1f, 0.82f, 0.56f), fogEnd = 120f,
            moteA = new Color(1f, 0.6f, 0.15f), moteB = new Color(0.9f, 0.25f, 0.1f), moteSize = 0.22f, moteRate = 26f, drift = new Vector3(0.5f, -0.8f, 0.3f),
            flowers = new[] { new Color(1f, 0.75f, 0.2f), new Color(0.95f, 0.4f, 0.2f) },
        },
        new Mood    // Sunken Fen: green murk, fireflies over the water
        {
            tint = new Color(0.9f, 1f, 0.9f), saturation = 0.95f, fog = new Color(0.42f, 0.56f, 0.44f), fogEnd = 58f,
            moteA = new Color(0.8f, 1f, 0.4f), moteB = new Color(0.5f, 1f, 0.7f), moteSize = 0.12f, moteRate = 22f, drift = new Vector3(0.15f, 0.2f, 0.1f),
            flowers = new[] { new Color(0.75f, 0.6f, 1f), Color.white },
        },
        new Mood    // Frostfall Heights: cold and bright, snow falling
        {
            tint = new Color(0.94f, 0.97f, 1f), saturation = 0.92f, fog = new Color(0.86f, 0.92f, 1f), fogEnd = 85f,
            moteA = Color.white, moteB = new Color(0.85f, 0.93f, 1f), moteSize = 0.13f, moteRate = 60f, drift = new Vector3(0.6f, -1.6f, 0.2f),
            flowers = new[] { Color.white },
        },
        new Mood    // Emberscar: smoke-dark, embers rising off the lava
        {
            tint = new Color(1f, 0.9f, 0.82f), saturation = 1.1f, fog = new Color(0.4f, 0.24f, 0.2f), fogEnd = 80f,
            moteA = new Color(2f, 0.8f, 0.2f), moteB = new Color(1.6f, 0.3f, 0.1f), moteSize = 0.11f, moteRate = 34f, drift = new Vector3(0.3f, 1.3f, 0.1f),
            flowers = new[] { new Color(1f, 0.4f, 0.1f) },
        },
    };

    PostFx post;
    ParticleSystem ambience;

    // Eases the picture toward the mood of wherever the hero is standing.
    void TickMood(float udt)
    {
        if (post == null) return;
        bool wild = InWilds && regionNow >= 0;
        Mood m = wild ? MoodForRegion(regionNow) : Moods[0];
        float k = 1f - Mathf.Exp(-udt * 0.9f);

        // The golden hours warm the whole picture; the night cools it.
        Color tint = m.tint * Color.Lerp(Color.white, new Color(1.07f, 0.97f, 0.86f), Dusk) * Color.Lerp(Color.white, new Color(0.84f, 0.92f, 1.08f), Night);
        post.tint = Color.Lerp(post.tint, tint, k);
        // The moods were tuned on a hotter picture: taken down a notch, colour reads as paint, not neon.
        post.saturation = Mathf.Lerp(post.saturation, (wild ? m.saturation : 1.1f) * 0.8f, k);
        RenderSettings.fogEndDistance = Mathf.Lerp(RenderSettings.fogEndDistance, wild ? m.fogEnd : 130f, k);
        if (wild)
        {
            RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, Color.Lerp(skyBottomNow, m.fog * Mathf.Lerp(0.3f, 1f, 1f - Night), 0.6f), k);
            if (ambience != null) ambience.transform.position = Player.transform.position + Vector3.up * 4f;
        }
    }

    // One particle system that travels with the hero; its look changes per region.
    void BuildAmbience()
    {
        var go = new GameObject("Ambience");
        go.transform.SetParent(LevelRoot, false);
        ambience = go.AddComponent<ParticleSystem>();
        ambience.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ambience.main;
        main.loop = true;
        main.startLifetime = 7f;
        main.startSpeed = 0f;
        main.maxParticles = 260;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var shape = ambience.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(46f, 8f, 46f);

        var fade = new Gradient();
        fade.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.2f), new GradientAlphaKey(0.9f, 0.75f), new GradientAlphaKey(0f, 1f) });
        var col = ambience.colorOverLifetime;
        col.enabled = true;
        col.color = fade;

        var noise = ambience.noise;
        noise.enabled = true;
        noise.strength = 0.5f;
        noise.frequency = 0.35f;

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = Fx.Mote;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        ApplyAmbience(0);
        ambience.Play();
    }

    void ApplyAmbience(int region)
    {
        if (ambience == null) return;
        Mood m = InWilds ? MoodForRegion(region) : Moods[0];
        var main = ambience.main;
        main.startColor = new ParticleSystem.MinMaxGradient(m.moteA, m.moteB);
        main.startSize = new ParticleSystem.MinMaxCurve(m.moteSize * 0.5f, m.moteSize * 1.5f);
        var emission = ambience.emission;
        emission.rateOverTime = m.moteRate;
        var vel = ambience.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(m.drift.x * 0.5f, m.drift.x * 1.5f);
        vel.y = new ParticleSystem.MinMaxCurve(m.drift.y * 0.5f, m.drift.y * 1.5f);
        vel.z = new ParticleSystem.MinMaxCurve(m.drift.z * 0.5f, m.drift.z * 1.5f);
    }

    // Wildflowers through the grass: small five-petalled heads held face-up at blade height,
    // deeper in colour at the heart. One mesh per colour.
    void BuildFlowers(Color[] colours, Vector2 centre, float radius, int count, System.Random rng, System.Func<Vector2, bool> skip)
    {
        Shader toon = Resources.Load<Shader>("Toon");
        if (toon == null || colours == null) return;
        if (petalShade == null) petalShade = Gradient(new Color(0.95f, 0.7f, 0.25f), Color.white);
        const int points = 10;
        foreach (Color colour in colours)
        {
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            for (int n = 0; n < count / colours.Length; n++)
            {
                Vector2 c = centre + RandomInCircle(rng, radius);
                float turn = (float)rng.NextDouble() * Mathf.PI * 2f;
                float s = Mathf.Lerp(0.032f, 0.06f, (float)rng.NextDouble());
                float height = Mathf.Lerp(0.2f, 0.44f, (float)rng.NextDouble());
                float tip = ((float)rng.NextDouble() - 0.5f) * 0.5f;
                if (skip != null && skip(c)) continue;
                Vector3 mid = new Vector3(c.x, height, c.y);
                // Tilted a little, each its own way, so a meadow of them does not look stamped.
                Vector3 up = new Vector3(Mathf.Sin(turn) * tip, 1f, Mathf.Cos(turn) * tip).normalized;
                Vector3 right = Vector3.Cross(up, Vector3.forward).normalized, fwd = Vector3.Cross(right, up);
                int i = verts.Count;
                verts.Add(mid);
                uvs.Add(new Vector2(0.5f, 0f));
                normals.Add(Vector3.up);
                for (int k = 0; k < points; k++)
                {
                    float a = turn + k * Mathf.PI * 2f / points;
                    float reach = k % 2 == 0 ? s : s * 0.42f;       // out to a petal's end, in to the notch between two
                    verts.Add(mid + (right * Mathf.Cos(a) + fwd * Mathf.Sin(a)) * reach + up * (k % 2 == 0 ? 0.008f : 0f));
                    uvs.Add(new Vector2(0.5f, 1f));
                    normals.Add(Vector3.up);
                }
                for (int k = 0; k < points; k++)
                {
                    int a = i + 1 + k, b = i + 1 + (k + 1) % points;
                    tris.Add(i); tris.Add(b); tris.Add(a);
                    tris.Add(i); tris.Add(a); tris.Add(b);
                }
            }
            if (verts.Count == 0) continue;

            var mesh = new Mesh { name = "Flowers", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            GeneratedResources.Own(mesh, LevelRoot);
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();

            var mat = new Material(toon) { color = new Color(colour.r, colour.g, colour.b, 1f), mainTexture = petalShade };
            GeneratedResources.Own(mat, LevelRoot);
            mat.SetFloat("_World", 1f);
            mat.SetFloat("_Sway", 16f);
            mat.SetFloat("_OutlineWidth", 0f);
            mat.SetFloat("_Rim", 0f);
            mat.SetFloat("_Spec", 0f);

            var go = new GameObject("Flowers");
            go.transform.SetParent(LevelRoot, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }

    // A disc of animated water.
    void Water(Vector2 centre, float radius, float y)
    {
        Shader shader = Resources.Load<Shader>("Water");
        if (shader == null)
        {
            Disc(new Color(0.25f, 0.62f, 0.95f), radius, y, centre.x, centre.y);
            return;
        }
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        DestroyImmediate(go.GetComponent<Collider>());
        go.name = "WaterSurface";
        go.transform.SetParent(LevelRoot, false);
        go.transform.position = new Vector3(centre.x, y - 0.05f, centre.y);
        go.transform.localScale = new Vector3(radius * 2f, 0.05f, radius * 2f);
        var r = go.GetComponent<Renderer>();
        r.sharedMaterial = GeneratedResources.Own(new Material(shader), r.transform);
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }
}
