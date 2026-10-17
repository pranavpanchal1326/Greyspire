using UnityEngine;

// Visual effects built from code: particle bursts, expanding rings, and the
// ground markers that telegraph enemy attacks.
public static class Fx
{
    static Material flatMat;
    static Mesh ringMesh, discMesh;

    // Unlit, alpha-blended, vertex/tint coloured. Used for rings and trails.
    public static Material Flat
    {
        get
        {
            if (flatMat == null)
            {
                Shader s = Shader.Find("Sprites/Default");
                if (s == null) s = Shader.Find("UI/Default");
                flatMat = new Material(s);
            }
            return flatMat;
        }
    }

    static Material moteMat;

    // Soft round sprite for drifting ambient motes.
    public static Material Mote
    {
        get
        {
            if (moteMat != null) return moteMat;
            const int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size - 0.5f, dy = (y + 0.5f) / size - 0.5f;
                    float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) * 2f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }
            }
            tex.Apply();
            moteMat = new Material(Flat.shader) { mainTexture = tex };
            return moteMat;
        }
    }

    static Transform Root => Game.I != null ? Game.I.LevelRoot : null;

    // Two shared particle systems do the soft, glowing work: streaks that stretch
    // along their velocity (sparks) and puffs that swell and fade (dust, flashes).
    static ParticleSystem streaks, puffs;

    static ParticleSystem Emitter(ref ParticleSystem ps, string name, bool stretch)
    {
        if (ps != null) return ps;
        var go = new GameObject(name);
        Object.DontDestroyOnLoad(go);
        ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 800;
        main.gravityModifier = stretch ? 1.6f : -0.04f;
        var emission = ps.emission;
        emission.enabled = false;
        var shape = ps.shape;
        shape.enabled = false;

        var fade = new Gradient();
        fade.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(stretch ? 1f : 0.6f, 0.4f), new GradientAlphaKey(0f, 1f) });
        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = fade;

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f,
            stretch ? AnimationCurve.Linear(0f, 1f, 1f, 0.15f) : AnimationCurve.Linear(0f, 0.5f, 1f, 1.6f));

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = Mote;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        if (stretch)
        {
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.05f;
            r.lengthScale = 1.5f;
        }
        ps.Play();
        return ps;
    }

    static void Emit(bool stretch, Vector3 pos, Vector3 velocity, Color c, float size, float life)
    {
        ParticleSystem ps = stretch ? Emitter(ref streaks, "FxStreaks", true) : Emitter(ref puffs, "FxPuffs", false);
        var p = new ParticleSystem.EmitParams
        {
            position = pos, velocity = velocity, startColor = c, startSize = size, startLifetime = life,
        };
        ps.Emit(p, 1);
    }

    // A soft round light on a card. Whoever owns it keeps it turned to the camera.
    public static Renderer Billboard(Transform parent, float size, Color c)
    {
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Object.DestroyImmediate(q.GetComponent<Collider>());
        q.transform.SetParent(parent, false);
        q.transform.localScale = Vector3.one * size;
        var r = q.GetComponent<Renderer>();
        r.sharedMaterial = Mote;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        Tint(r, c);
        return r;
    }

    // One small mote of light with a velocity: Lumi's sparks.
    public static void Spark(Vector3 pos, Vector3 velocity, Color c, float size, float life)
    {
        Emit(false, pos, velocity, c, size, life);
    }

    // A translucent copy of a character frozen in its current pose, fading out: dash afterimages.
    public static void Ghost(Transform model, Color c)
    {
        DashGhostPool.Emit(model, c);
    }

    // A short bright bloom of light at a point: the moment of impact.
    public static void Flash(Vector3 pos, Color c, float size = 1.6f)
    {
        c.a = 0.85f;
        Emit(false, pos, Vector3.zero, c, size, 0.14f);
    }

    // Something breaking apart: a flash, sparks that arc and fall, and a breath of smoke in the
    // same colour that hangs a moment and thins.
    public static void Burst(Vector3 pos, Color c, int count, float force = 5f, float size = 1f)
    {
        if (count >= 3) Flash(pos, Color.Lerp(c, Color.white, 0.35f), 1f + count * 0.07f * size);
        Color hot = Color.Lerp(c, Color.white, 0.3f);
        for (int i = 0; i < count; i++)
            Emit(true, pos, ProductionRandom.Cosmetic.onUnitSphere * force * 1.2f + Vector3.up * force * 0.5f, hot, ProductionRandom.Cosmetic.Range(0.1f, 0.2f) * size, ProductionRandom.Cosmetic.Range(0.3f, 0.6f));
        int puffs = Mathf.Max(1, count / 3);
        Color smoke = new Color(c.r * 0.55f, c.g * 0.55f, c.b * 0.55f, 0.38f);
        for (int i = 0; i < puffs; i++)
            Emit(false, pos + ProductionRandom.Cosmetic.insideUnitSphere * 0.25f * size, ProductionRandom.Cosmetic.insideUnitSphere * force * 0.16f + Vector3.up * 0.5f,
                smoke, ProductionRandom.Cosmetic.Range(0.5f, 1.1f) * size, ProductionRandom.Cosmetic.Range(0.5f, 0.9f));
    }

    // Bright streaks thrown along a direction, with a flash at the point of contact: sword hits.
    public static void Sparks(Vector3 pos, Vector3 dir, Color c, int count)
    {
        Flash(pos, Color.Lerp(c, Color.white, 0.5f), 1.3f + count * 0.06f);
        for (int i = 0; i < count * 2; i++)
        {
            Vector3 v = (dir + ProductionRandom.Cosmetic.insideUnitSphere * 0.8f).normalized * ProductionRandom.Cosmetic.Range(6f, 14f);
            Emit(true, pos, v, c, ProductionRandom.Cosmetic.Range(0.12f, 0.24f), ProductionRandom.Cosmetic.Range(0.18f, 0.38f));
        }
    }

    // Soft puffs kicked up along the ground: footsteps, landings, dashes.
    public static void Dust(Vector3 pos, int count = 5)
    {
        for (int i = 0; i < count; i++)
        {
            Vector2 r = ProductionRandom.Cosmetic.insideUnitCircle.normalized * ProductionRandom.Cosmetic.Range(0.8f, 2.2f);
            Emit(false, pos + Vector3.up * 0.15f, new Vector3(r.x, ProductionRandom.Cosmetic.Range(0.3f, 0.9f), r.y),
                new Color(0.96f, 0.94f, 0.88f, 0.55f), ProductionRandom.Cosmetic.Range(0.35f, 0.7f), ProductionRandom.Cosmetic.Range(0.35f, 0.6f));
        }
    }

    static readonly System.Collections.Generic.Dictionary<int, Mesh> arcMeshes = new System.Collections.Generic.Dictionary<int, Mesh>();
    static Mesh streakMesh;

    // The cut of a blade, drawn in the air: a bright arc that sweeps a little further round and
    // fades in a fraction of a second. rot turns the arc's plane (flat, slanted, on edge) and
    // points its middle forward; mirrored arcs sweep the other way.
    public static void Slash(Vector3 pos, Quaternion rot, float radius, float arcDegrees, Color c, float seconds, bool mirrored)
    {
        var go = new GameObject("Slash");
        go.transform.SetParent(Root, false);
        go.transform.SetPositionAndRotation(pos, rot);
        go.AddComponent<MeshFilter>().sharedMesh = ArcMesh(Mathf.RoundToInt(arcDegrees));
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = Flat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        var s = go.AddComponent<SlashFx>();
        s.colour = c;
        s.life = seconds;
        s.size = new Vector3(mirrored ? -radius : radius, radius, radius);
        s.sweep = arcDegrees >= 359f ? 200f : mirrored ? -50f : 50f;
    }

    // A blade of light that stays with whatever carries it (the hero's thrown blade).
    public static Renderer Streak(Transform parent, float width, float length, Color c)
    {
        var go = new GameObject("Streak");
        go.transform.SetParent(parent, false);
        go.transform.localScale = new Vector3(width, width, length);
        go.AddComponent<MeshFilter>().sharedMesh = StreakMesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = Flat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        Tint(r, c);
        return r;
    }

    // A lunge: a streak of light along the line of the blade.
    public static void Thrust(Vector3 pos, Vector3 dir, float length, Color c, float seconds)
    {
        if (dir.sqrMagnitude < 0.001f) return;
        var go = new GameObject("Thrust");
        go.transform.SetParent(Root, false);
        go.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(dir));
        go.AddComponent<MeshFilter>().sharedMesh = StreakMesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = Flat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        var s = go.AddComponent<SlashFx>();
        s.colour = c;
        s.life = seconds;
        s.size = new Vector3(0.55f, 0.55f, length);
        s.sweep = 0f;
    }

    // A band of arc lying in the xz plane, centred on +z: sharp along its outer edge, soft
    // inside, and fading from its leading end back to nothing at its tail.
    static Mesh ArcMesh(int degrees)
    {
        if (arcMeshes.TryGetValue(degrees, out Mesh cached) && cached != null) return cached;
        int seg = Mathf.Max(12, degrees / 6);
        float[] radii = { degrees >= 359 ? 0.8f : 0.58f, 0.94f, 1f };
        float[] across = { 0f, 1f, 0.55f };
        var verts = new Vector3[(seg + 1) * 3];
        var cols = new Color[verts.Length];
        var tris = new int[seg * 12];
        for (int i = 0; i <= seg; i++)
        {
            float t = i / (float)seg;
            float a = Mathf.Lerp(-degrees * 0.5f, degrees * 0.5f, t) * Mathf.Deg2Rad;
            // A full circle is bright all round; an arc trails off behind the blade.
            float along = degrees >= 359 ? 0.12f + 0.88f * t * t : Mathf.Pow(t, 1.6f) * Mathf.Clamp01((1f - t) * 12f);
            for (int k = 0; k < 3; k++)
            {
                verts[i * 3 + k] = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * radii[k];
                cols[i * 3 + k] = new Color(1f, 1f, 1f, along * across[k]);
            }
            if (i == seg) break;
            int v = i * 3, n = v + 3, q = i * 12;
            tris[q] = v; tris[q + 1] = n; tris[q + 2] = n + 1; tris[q + 3] = v; tris[q + 4] = n + 1; tris[q + 5] = v + 1;
            tris[q + 6] = v + 1; tris[q + 7] = n + 1; tris[q + 8] = n + 2; tris[q + 9] = v + 1; tris[q + 10] = n + 2; tris[q + 11] = v + 2;
        }
        var mesh = new Mesh { name = "Arc" + degrees, vertices = verts, colors = cols, triangles = tris };
        mesh.RecalculateBounds();
        arcMeshes[degrees] = mesh;
        return mesh;
    }

    // Two crossed blades of light, a unit long down +z, bright at the point.
    static Mesh StreakMesh
    {
        get
        {
            if (streakMesh != null) return streakMesh;
            var verts = new[]
            {
                new Vector3(-0.5f, 0f, 0f), new Vector3(0.5f, 0f, 0f), new Vector3(0f, 0f, 1f),
                new Vector3(0f, -0.5f, 0f), new Vector3(0f, 0.5f, 0f), new Vector3(0f, 0f, 1f),
            };
            Color tail = new Color(1f, 1f, 1f, 0f), tip = Color.white;
            streakMesh = new Mesh { name = "Streak", vertices = verts, colors = new[] { tail, tail, tip, tail, tail, tip }, triangles = new[] { 0, 1, 2, 3, 4, 5 } };
            streakMesh.RecalculateBounds();
            return streakMesh;
        }
    }

    // A flat ring on the ground that grows from r0 to r1 and fades out.
    public static void Ring(Vector3 pos, Color c, float r0, float r1, float seconds)
    {
        var go = MakeMesh("Ring", RingMesh, pos);
        var r = go.AddComponent<RingFx>();
        r.color = c; r.r0 = r0; r.r1 = r1; r.duration = seconds;
        go.AddComponent<SurfaceMarker>();
    }

    // "Something is about to land here": outline + a disc that fills over time.
    public static void Telegraph(Vector3 pos, float radius, float seconds, Color c)
    {
        var holder = new GameObject("Telegraph");
        holder.transform.SetParent(Root, false);
        holder.transform.position = WorldSupport.TryGround(pos, out var support) ? support.point + Vector3.up * 0.06f : pos;
        var t = holder.AddComponent<TelegraphFx>();
        t.Setup(Child(holder.transform, RingMesh), Child(holder.transform, DiscMesh), radius, seconds, c);
        foreach (var filter in holder.GetComponentsInChildren<MeshFilter>()) filter.gameObject.AddComponent<SurfaceMarker>();
    }

    public static void Shockwave(Vector3 pos, float maxRadius, float speed, int damage, Actor emitter = null)
    {
        var go = MakeMesh("Shockwave", RingMesh, pos);
        var s = go.AddComponent<Shockwave>();
        s.maxRadius = maxRadius; s.speed = speed; s.damage = damage;
        s.emitter = emitter; s.actionId = emitter != null ? emitter.ActionId : "";
        go.AddComponent<SurfaceMarker>();
    }

    public static void Tint(Renderer r, Color c)
    {
        var block = new MaterialPropertyBlock();
        block.SetColor("_Color", c);
        r.SetPropertyBlock(block);
    }

    static GameObject MakeMesh(string name, Mesh mesh, Vector3 pos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(Root, false);
        go.transform.position = new Vector3(pos.x, 0.07f, pos.z);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = Flat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return go;
    }

    static Renderer Child(Transform parent, Mesh mesh)
    {
        var go = MakeMesh("Part", mesh, Vector3.zero);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.zero;
        return go.GetComponent<Renderer>();
    }

    // Unit-radius annulus lying flat on the ground.
    static Mesh RingMesh
    {
        get
        {
            if (ringMesh != null) return ringMesh;
            const int seg = 64;
            const float inner = 0.78f;
            var verts = new Vector3[seg * 2];
            var cols = new Color[seg * 2];
            var tris = new int[seg * 6];
            for (int i = 0; i < seg; i++)
            {
                float a = i * Mathf.PI * 2f / seg;
                Vector3 d = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a));
                verts[i * 2] = d * inner;
                verts[i * 2 + 1] = d;
                cols[i * 2] = new Color(1f, 1f, 1f, 0f);
                cols[i * 2 + 1] = Color.white;
                int n = (i + 1) % seg;
                int t = i * 6;
                tris[t] = i * 2; tris[t + 1] = i * 2 + 1; tris[t + 2] = n * 2 + 1;
                tris[t + 3] = i * 2; tris[t + 4] = n * 2 + 1; tris[t + 5] = n * 2;
            }
            ringMesh = new Mesh { name = "Ring", vertices = verts, colors = cols, triangles = tris };
            ringMesh.RecalculateBounds();
            return ringMesh;
        }
    }

    static Mesh DiscMesh
    {
        get
        {
            if (discMesh != null) return discMesh;
            const int seg = 48;
            var verts = new Vector3[seg + 1];
            var tris = new int[seg * 3];
            verts[0] = Vector3.zero;
            for (int i = 0; i < seg; i++)
            {
                float a = i * Mathf.PI * 2f / seg;
                verts[i + 1] = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a));
                tris[i * 3] = 0;
                tris[i * 3 + 1] = i + 1;
                tris[i * 3 + 2] = (i + 1) % seg + 1;
            }
            discMesh = new Mesh { name = "Disc", vertices = verts, triangles = tris };
            discMesh.RecalculateBounds();
            return discMesh;
        }
    }
}
