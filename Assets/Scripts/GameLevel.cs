using System.Collections.Generic;
using UnityEngine;

// Level data and arena construction. Everything scenic uses "world" materials,
// which is what lets the arena fade from grey to full colour as waves fall.
public partial class Game
{
    struct Wave
    {
        public int slimes, minions, bigSlimes, spitters, bombers, brutes;
        public bool boss;

        public Wave(int slimes, int minions, int bigSlimes, int spitters, int bombers, int brutes, bool boss = false)
        {
            this.slimes = slimes; this.minions = minions; this.bigSlimes = bigSlimes; this.spitters = spitters;
            this.bombers = bombers; this.brutes = brutes; this.boss = boss;
        }
    }

    // One kind of scenery object. tex == null means one of the Blender props in
    // Resources/Models; Nat (or NatDry, for sandstone rocks) means a tree, bush or
    // rock from Resources/Nature; otherwise a textured model from Resources/Env.
    // Each is scaled so its largest dimension is "height" (height for trees, width for rocks).
    struct PropDef
    {
        public string model, tex;
        public float height, radius, sway;
        public Color leaves;    // recolours the foliage (autumn gold, marsh green)
        public Color wash;      // a coat over everything: snow, ash. Alpha is how thick
        public bool box;        // solid as a box fitted to the model (buildings), not a round post

        public PropDef(string model, string tex, float height, float radius, float sway = 0f)
        {
            this.model = model; this.tex = tex; this.height = height; this.radius = radius; this.sway = sway;
            leaves = default;
            wash = default;
            box = false;
        }

        public PropDef Leaves(Color c) { leaves = c; return this; }
        public PropDef Wash(Color c) { wash = c; return this; }
        public PropDef Box() { box = true; return this; }
    }

    const string Hex = "hexagons_medieval", Dun = "dungeon_texture", Hal = "halloweenbits_texture";
    const string Nat = "@nature", NatDry = "@nature-dry";
    // Kit: something built from the village kit (Village.Make). Fur: a prop from the props kit.
    // For both, a height of nought means "as big as it was made".
    const string Kit = "@kit", Fur = "@props";
    const float TreeSway = 0.3f, BushSway = 0.6f;

    class LevelDef
    {
        public string name;
        public Color ground, patch, outer, skyTop, skyBottom, ambient, sun;
        public Color[] accents;     // flowers, pebbles, glints scattered on the ground
        public PropDef[] ring, inner;   // boundary, obstacles
    }

    static readonly LevelDef[] Levels =
    {
        new LevelDef
        {
            name = "Sunny Meadow",
            // Grass the colour of grass: the old lime read as plastic.
            ground = new Color(0.36f, 0.55f, 0.27f), patch = new Color(0.29f, 0.46f, 0.23f),
            outer = new Color(0.26f, 0.41f, 0.23f),
            skyTop = new Color(0.27f, 0.52f, 0.92f), skyBottom = new Color(0.74f, 0.88f, 0.97f),
            ambient = new Color(0.6f, 0.66f, 0.76f), sun = new Color(1f, 0.96f, 0.86f),
            accents = new[] { new Color(1f, 0.45f, 0.6f), new Color(1f, 0.9f, 0.3f), Color.white, new Color(0.3f, 0.65f, 0.28f) },
            ring = new[]
            {
                new PropDef("CommonTree_1", Nat, 7.5f, 0.45f, TreeSway), new PropDef("CommonTree_3", Nat, 9f, 0.45f, TreeSway),
                new PropDef("CommonTree_2", Nat, 8f, 0.45f, TreeSway), new PropDef("Rock_Medium_1", Nat, 2.8f, 1.2f),
                new PropDef("Bush_Common_Flowers", Nat, 2f, 0.8f, BushSway), new PropDef("LeanTo", Kit, 0f, 1.6f),
                new PropDef("CommonTree_5", Nat, 7f, 0.45f, TreeSway), new PropDef("Banner", Kit, 0f, 0.2f),
                new PropDef("CommonTree_4", Nat, 9.5f, 0.45f, TreeSway),
            },
            inner = new[]
            {
                new PropDef("Rock_Medium_2", Nat, 2.2f, 1f), new PropDef("Crate_Wooden", Fur, 0f, 0.6f),
                new PropDef("Bush_Common", Nat, 1.9f, 0.8f, BushSway), new PropDef("Barrel", Fur, 0f, 0.45f),
                new PropDef("Rock_Medium_3", Nat, 2.6f, 1.2f), new PropDef("Prop_Wagon", Kit, 0f, 1.3f),
            },
        },
        new LevelDef
        {
            name = "Scorched Dunes",
            ground = new Color(0.9f, 0.76f, 0.5f), patch = new Color(0.82f, 0.65f, 0.4f),
            outer = new Color(0.76f, 0.57f, 0.36f),
            skyTop = new Color(1f, 0.55f, 0.35f), skyBottom = new Color(1f, 0.86f, 0.6f),
            ambient = new Color(0.78f, 0.62f, 0.55f), sun = new Color(1f, 0.9f, 0.75f),
            accents = new[] { new Color(0.8f, 0.55f, 0.35f), new Color(1f, 0.5f, 0.3f), new Color(0.35f, 0.7f, 0.4f) },
            ring = new[]
            {
                new PropDef("DeadTree_2", Nat, 7f, 0.4f, 0.1f), new PropDef("Pillar", Kit, 4.6f, 0.6f),
                new PropDef("Rock_Medium_1", NatDry, 3.2f, 1.4f), new PropDef("Rock_Medium_2", NatDry, 4.2f, 1.8f),
                new PropDef("Pillar", Kit, 3.4f, 0.5f), new PropDef("Rubble", Kit, 0f, 1.1f),
                new PropDef("DeadTree_1", Nat, 6.5f, 0.4f, 0.1f), new PropDef("Rock_Medium_3", NatDry, 2.6f, 1.2f),
            },
            inner = new[]
            {
                new PropDef("Vase_2", Fur, 1.1f, 0.4f), new PropDef("Rock_Medium_2", NatDry, 2f, 0.9f),
                new PropDef("Pillar", Kit, 4.2f, 0.6f), new PropDef("Barrel", Fur, 0f, 0.45f),
                new PropDef("Rubble", Kit, 0f, 1.1f),
            },
        },
        new LevelDef
        {
            name = "Crystal Hollow",
            ground = new Color(0.4f, 0.36f, 0.7f), patch = new Color(0.48f, 0.42f, 0.8f),
            outer = new Color(0.24f, 0.2f, 0.46f),
            skyTop = new Color(0.08f, 0.05f, 0.25f), skyBottom = new Color(0.45f, 0.25f, 0.6f),
            ambient = new Color(0.52f, 0.46f, 0.78f), sun = new Color(0.82f, 0.86f, 1f),
            accents = new[] { new Color(0.35f, 0.95f, 1f), new Color(1f, 0.5f, 0.95f), new Color(0.7f, 0.6f, 1f) },
            ring = new[]
            {
                new PropDef("DeadTree_2", Nat, 8.5f, 0.45f, 0.1f), new PropDef("Crystal", null, 2.2f, 0.65f),
                new PropDef("Pillar", Kit, 5.2f, 0.6f), new PropDef("TwistedTree_2", Nat, 11f, 0.7f, 0.2f).Wash(new Color(0.6f, 0.45f, 1f, 0.55f)),
                new PropDef("Banner", Kit, 0f, 0.25f), new PropDef("Torch", Kit, 0f, 0.2f),
                new PropDef("DeadTree_4", Nat, 9f, 0.45f, 0.1f), new PropDef("Fence", Kit, 0f, 0.9f),
            },
            inner = new[]
            {
                new PropDef("Headstone", Kit, 0f, 0.45f), new PropDef("Crystal", null, 2.2f, 0.65f),
                new PropDef("LampPost", Kit, 0f, 0.25f), new PropDef("Crate_Metal", Fur, 0f, 0.6f),
                new PropDef("Cage_Small", Fur, 1.1f, 0.5f), new PropDef("CandleStick_Stand", Fur, 0f, 0.3f),
            },
        },
    };

    Transform skyDome;

    void BuildLevel(int index)
    {
        TrainingActive = false;
        GameAudio.StopWorld();
        if (LevelRoot != null)
        {
            LevelRoot.gameObject.SetActive(false);
            Destroy(LevelRoot.gameObject);
        }
        LevelRoot = new GameObject("Level").transform;
        ProductionRandom.BeginScene("arena:" + index);
        PlaytestLog.Emit("scope_start", note: ProductionRandom.Scope);
        InTown = false;
        spots.Clear();
        packSpots.Clear();
        ClearResidents();
        stairSpot = null;
        nearSpot = null;
        LevelDef def = Levels[index];
        var rng = new System.Random(FloorCatalog.ArenaSeed(index));

        cam.backgroundColor = def.skyBottom;
        RenderSettings.fogColor = def.skyBottom;
        RenderSettings.ambientLight = def.ambient;
        sun.color = def.sun;

        BuildSky(def, rng);
        BuildHeart();
        BuildMotes(def);

        // Solid floor + a ring of invisible walls keep everyone inside.
        var floor = new GameObject("Floor");
        floor.transform.SetParent(LevelRoot, false);
        var floorBox = floor.AddComponent<BoxCollider>();
        floorBox.size = new Vector3(200f, 2f, 200f);
        floorBox.center = new Vector3(0, -1f, 0);

        const int segments = 32;
        float segLen = 2f * Mathf.PI * ArenaRadius / segments + 0.5f;
        for (int i = 0; i < segments; i++)
        {
            float a = i * Mathf.PI * 2f / segments;
            var wall = new GameObject("Wall");
            wall.transform.SetParent(LevelRoot, false);
            wall.transform.position = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * (ArenaRadius + 0.5f) + Vector3.up * 5f;
            wall.transform.rotation = Quaternion.Euler(0, a * Mathf.Rad2Deg, 0);
            wall.AddComponent<BoxCollider>().size = new Vector3(segLen, 10f, 1f);
        }

        Disc(def.outer, 120f, -0.06f, 0f, 0f, def.outer * 0.88f);
        Disc(def.ground, ArenaRadius + 2.5f, -0.03f, 0f, 0f, def.patch);

        BuildGrass(def, ArenaRadius + 9f, rng, index == 1 ? 900 : 3000, null);
        Scatter(rng, Vector2.zero, ArenaRadius + 6f, 70, LeafTint(def), p => p.magnitude < 3f);
        BuildFlowers(def.accents, Vector2.zero, ArenaRadius + 9f, index == 1 ? 120 : 520, rng, null);

        // Boundary ring of props, then a few obstacles inside.
        for (int i = 0; i < 30; i++)
        {
            float a = (i + (float)rng.NextDouble() * 0.5f) * Mathf.PI * 2f / 30f;
            float r = ArenaRadius + 1.8f + (float)rng.NextDouble() * 2.5f;
            Prop(def.ring[i % def.ring.Length], new Vector3(Mathf.Sin(a) * r, 0, Mathf.Cos(a) * r), rng, 1.15f);
        }
        var placed = new List<Vector2>();
        int tries = 0;
        while (placed.Count < 10 && tries++ < 200)
        {
            Vector2 p = RandomInCircle(rng, ArenaRadius - 3.5f);
            if (p.magnitude < 7f) continue;
            // Keep the area around the hero start clear, for the fight and for the menu camera.
            if ((p - new Vector2(StartPos.x, StartPos.z)).magnitude < 8f) continue;
            bool ok = true;
            foreach (Vector2 q in placed)
                if ((q - p).magnitude < 5f) { ok = false; break; }
            if (!ok) continue;
            placed.Add(p);
            Prop(def.inner[placed.Count % def.inner.Length], new Vector3(p.x, 0, p.y), rng, 1f);
        }

    }

    // The sky (its cloud is in the shader), and the hills and mountains that close the horizon.
    void BuildSky(LevelDef def, System.Random rng, float hillRing = 52f)
    {
        // Every scene starts from the same midday light; town and the Wilds then run the day cycle.
        sun.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
        sun.shadowStrength = 0.75f;
        skyMat = null;
        skyDef = def;
        starField = null;
        sunGlows.Clear();
        Shader skyShader = Resources.Load<Shader>("Sky");
        if (skyShader != null)
        {
            var dome = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            DestroyImmediate(dome.GetComponent<Collider>());
            dome.name = "Sky";
            dome.transform.SetParent(LevelRoot, false);
            dome.transform.localScale = Vector3.one * 600f;
            var mat = new Material(skyShader);
            GeneratedResources.Own(mat, LevelRoot);
            mat.SetColor("_Top", def.skyTop);
            mat.SetColor("_Bottom", def.skyBottom);
            mat.SetColor("_CloudLit", Color.Lerp(def.skyBottom, Color.white, 0.75f));
            mat.SetColor("_CloudDark", Color.Lerp(def.skyBottom, def.skyTop, 0.35f) * 0.82f);
            var r = dome.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            skyDome = dome.transform;
            skyMat = mat;
            BuildStars(dome.transform);

            // The sun: a soft glowing disc with a wide halo, bright enough to bloom.
            Vector3 toSun = -sun.transform.forward;
            for (int i = 0; i < 2; i++)
            {
                var glow = GameObject.CreatePrimitive(PrimitiveType.Quad);
                DestroyImmediate(glow.GetComponent<Collider>());
                glow.name = "SunGlow";
                glow.transform.SetParent(dome.transform, false);
                glow.transform.localPosition = toSun * (0.42f - i * 0.01f);
                glow.transform.localRotation = Quaternion.LookRotation(toSun);
                glow.transform.localScale = Vector3.one * (i == 0 ? 0.11f : 0.42f);
                var gr = glow.GetComponent<Renderer>();
                gr.sharedMaterial = Fx.Mote;
                gr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                Fx.Tint(gr, new Color(def.sun.r, def.sun.g, def.sun.b, i == 0 ? 1f : 0.28f));
                sunGlows.Add(gr);
            }
        }

        BuildHorizon(def, rng, hillRing);
    }

    // The edge of the world: a ring of low hills in the land's own colour, and behind them a
    // ring of mountains that the haze turns to silhouettes.
    void BuildHorizon(LevelDef def, System.Random rng, float inner)
    {
        float seed = (float)rng.NextDouble() * 50f;
        Color rock = new Color(0.4f, 0.42f, 0.46f);
        Ridge(inner, inner + 60f, 11f, 0.045f, seed, Color.Lerp(def.outer, def.ground, 0.3f));
        Ridge(inner + 40f, inner + 150f, 46f, 0.016f, seed + 9f, Color.Lerp(Color.Lerp(def.outer, rock, 0.6f), def.skyBottom, 0.2f));
    }

    // One ring of terrain between two radii. Height comes from ridged noise read round the
    // circle, so the ring closes on itself without a seam.
    void Ridge(float r0, float r1, float height, float grain, float seed, Color colour)
    {
        const int around = 144, across = 9;
        var verts = new Vector3[(around + 1) * across];
        var tris = new int[around * (across - 1) * 6];
        for (int i = 0; i <= around; i++)
        {
            float a = i % around * Mathf.PI * 2f / around;
            Vector3 dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
            for (int j = 0; j < across; j++)
            {
                float t = j / (float)(across - 1), r = Mathf.Lerp(r0, r1, t);
                float nx = dir.x * r * grain + seed, nz = dir.z * r * grain + seed * 1.7f;
                float n = Mathf.PerlinNoise(nx, nz) * 0.6f + Mathf.PerlinNoise(nx * 2.3f + 5f, nz * 2.3f + 5f) * 0.3f + Mathf.PerlinNoise(nx * 5.1f, nz * 5.1f) * 0.1f;
                float ridged = 1f - Mathf.Abs(n * 2f - 1f);
                // Nothing at the near edge, full height by the middle, and it stays up at the back.
                float profile = Mathf.SmoothStep(0f, 1f, t * 2.2f) * Mathf.Lerp(1f, 0.75f, Mathf.Clamp01(t * 2f - 1f));
                verts[i * across + j] = dir * r + Vector3.up * (height * profile * (0.25f + 0.75f * ridged * ridged) - 0.6f);
            }
        }
        int q = 0;
        for (int i = 0; i < around; i++)
            for (int j = 0; j < across - 1; j++)
            {
                int v = i * across + j, n = v + across;
                tris[q++] = v; tris[q++] = v + 1; tris[q++] = n;
                tris[q++] = n; tris[q++] = v + 1; tris[q++] = n + 1;
            }
        var mesh = new Mesh { name = "Ridge", vertices = verts, triangles = tris };
            GeneratedResources.Own(mesh, LevelRoot);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        var go = new GameObject("Horizon");
        go.transform.SetParent(LevelRoot, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        var mat = new Material(ToonKit.Mat(colour, true));
            GeneratedResources.Own(mat, LevelRoot);
        mat.SetFloat("_Spec", 0f);
        mat.SetFloat("_Rim", 0f);
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    // The Colour Heart: the landmark at the centre of every arena. Its shell is
    // drained like the rest of the world; the core is the colour still trapped inside.
    void BuildHeart()
    {
        var holder = new GameObject("ColourHeart");
        holder.transform.SetParent(LevelRoot, false);

        Transform plinth = Village.Make("Pillar", holder.transform);
        if (plinth != null) plinth.localScale = new Vector3(3.4f, 0.3f, 3.4f);
        Transform shell = ToonKit.Spawn("Crystal", holder.transform, 1.5f, true);
        shell.localPosition = Vector3.up * 0.9f;
        Transform core = ToonKit.Spawn("Crystal", holder.transform, 0.55f);
        core.localPosition = Vector3.up * 4.6f;
        core.gameObject.AddComponent<Spin>();

        Capsule(holder, 1.3f, 4.2f, 2.1f);
    }

    // Motes of stray colour drifting on the wind, even while the arena is grey.
    void BuildMotes(LevelDef def)
    {
        var go = new GameObject("Motes");
        go.transform.SetParent(LevelRoot, false);
        go.transform.position = new Vector3(0, 4f, 0);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;
        main.prewarm = true;
        main.startLifetime = 9f;
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.24f);
        main.startColor = new ParticleSystem.MinMaxGradient(def.accents[0], def.accents[1]);
        main.maxParticles = 200;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.rateOverTime = 18f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(56f, 7f, 56f);

        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(0.3f, 0.9f);
        vel.y = new ParticleSystem.MinMaxCurve(-0.1f, 0.25f);
        vel.z = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);

        var fade = new Gradient();
        fade.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.2f), new GradientAlphaKey(0.9f, 0.8f), new GradientAlphaKey(0f, 1f) });
        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = fade;

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.4f;
        noise.frequency = 0.3f;

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = Fx.Mote;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ps.Play();
    }

    static Texture2D bladeShade, petalShade;

    // A strip of texture, dark at one end and light at the other: grass is darker at the root
    // where it stands in its own shade; a flower is deeper at its heart.
    static Texture2D Gradient(Color from, Color to)
    {
        var tex = new Texture2D(1, 16, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < 16; y++) tex.SetPixel(0, y, Color.Lerp(from, to, y / 15f));
        tex.Apply();
        return tex;
    }

    // Grass: tens of thousands of fine blades in four shades, each a narrow curved strip that
    // tapers to a point, darker at the root. Built as a few big meshes that the shader bends
    // with the wind and presses flat round the hero's feet. skip(point) keeps roads and
    // plazas clear.
    void BuildGrass(LevelDef def, float radius, System.Random rng, int tufts, System.Func<Vector2, bool> skip, Vector2 centre = default)
    {
        Shader toon = Resources.Load<Shader>("Toon");
        if (toon == null) return;
        if (bladeShade == null) bladeShade = Gradient(new Color(0.5f, 0.52f, 0.46f), Color.white);
        Color[] shades =
        {
            def.patch * 0.92f,
            Color.Lerp(def.ground, Color.white, 0.1f),
            Color.Lerp(def.patch, def.ground, 0.5f),
            Color.Lerp(def.ground, new Color(0.62f, 0.6f, 0.34f), 0.28f),     // a little of last year's straw
        };

        for (int s = 0; s < shades.Length; s++)
        {
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            for (int t = 0; t < tufts / shades.Length; t++)
            {
                Vector2 c = centre + RandomInCircle(rng, radius);
                // Drawn before the test, so what is kept does not shift what follows.
                float tall = Mathf.Lerp(0.7f, 1.3f, (float)rng.NextDouble());
                if (skip != null && skip(c))
                {
                    for (int b = 0; b < 20; b++) rng.NextDouble();
                    continue;
                }
                for (int b = 0; b < 5; b++)
                {
                    float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                    float spread = (float)rng.NextDouble() * 0.2f;
                    float w = Mathf.Lerp(0.016f, 0.034f, (float)rng.NextDouble());
                    float h = Mathf.Lerp(0.2f, 0.52f, (float)rng.NextDouble()) * tall;
                    Vector3 lean = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                    Vector3 across = new Vector3(Mathf.Cos(a), 0f, -Mathf.Sin(a)) * w;
                    Vector3 root = new Vector3(c.x, 0f, c.y) + lean * spread;
                    // It rises nearly straight, then bows over toward its tip.
                    Vector3 mid = root + lean * (h * 0.1f) + Vector3.up * (h * 0.58f);
                    Vector3 tip = root + lean * (h * 0.36f) + Vector3.up * h;
                    int i = verts.Count;
                    verts.Add(root - across); verts.Add(root + across);
                    verts.Add(mid - across * 0.7f); verts.Add(mid + across * 0.7f);
                    verts.Add(tip);
                    uvs.Add(new Vector2(0.5f, 0f)); uvs.Add(new Vector2(0.5f, 0f));
                    uvs.Add(new Vector2(0.5f, 0.6f)); uvs.Add(new Vector2(0.5f, 0.6f));
                    uvs.Add(new Vector2(0.5f, 1f));
                    // Upward normals: blades are lit like the ground they grow from, on both sides.
                    for (int k = 0; k < 5; k++) normals.Add(Vector3.up);
                    tris.AddRange(new[] { i, i + 1, i + 2, i + 1, i + 3, i + 2, i + 2, i + 3, i + 4 });
                    tris.AddRange(new[] { i + 2, i + 1, i, i + 2, i + 3, i + 1, i + 4, i + 3, i + 2 });
                }
            }
            if (verts.Count == 0) continue;

            var mesh = new Mesh { name = "Grass", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            GeneratedResources.Own(mesh, LevelRoot);
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();

            var mat = new Material(toon) { color = new Color(shades[s].r, shades[s].g, shades[s].b, 1f), mainTexture = bladeShade };
            GeneratedResources.Own(mat, LevelRoot);
            mat.SetFloat("_World", 1f);
            mat.SetFloat("_Sway", 16f);
            mat.SetFloat("_OutlineWidth", 0f);
            mat.SetFloat("_Rim", 0f);
            mat.SetFloat("_Spec", 0f);

            var go = new GameObject("Grass");
            go.transform.SetParent(LevelRoot, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }

    void TickScenery(float udt)
    {
        if (skyDome != null) skyDome.position = cam.transform.position;
        TickSky(udt);
        TickMood(udt);
        // Shafts of light fall from wherever the sun (or the moon) stands.
        post.sunDir = -sun.transform.forward;
        post.rays = Mathf.Lerp(0.8f, 0.3f, Night);
    }

    static Vector2 RandomInCircle(System.Random rng, float radius)
    {
        float a = (float)rng.NextDouble() * Mathf.PI * 2f;
        float r = Mathf.Sqrt((float)rng.NextDouble()) * radius;
        return new Vector2(Mathf.Sin(a) * r, Mathf.Cos(a) * r);
    }

    // A disc of ground. The shader mottles it with a second colour and frays its rim, so the
    // land reads as earth and grass rather than flat circles laid on one another. patch is
    // the second colour; leave it out for a slightly darker shade of the first.
    // tex names a texture of the village kit (cobbles, paving), laid in world space.
    void Disc(Color c, float radius, float y, float x = 0f, float z = 0f, Color patch = default, string tex = null, float tile = 0.25f)
    {
        var go = ToonKit.Primitive(PrimitiveType.Cylinder, c, LevelRoot, true);
        go.name = "Ground";
        go.transform.position = new Vector3(x, y - 0.05f, z);
        go.transform.localScale = new Vector3(radius * 2f, 0.05f, radius * 2f);
        var r = go.GetComponent<Renderer>();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        // Flat discs want neither an outline hull nor rim/gloss highlights.
        var block = new MaterialPropertyBlock();
        block.SetFloat("_OutlineWidth", 0f);
        block.SetFloat("_Rim", 0f);
        block.SetFloat("_Spec", 0f);
        block.SetFloat("_Ground", 1f);
        if (patch.a <= 0f) patch = c * 0.9f;
        block.SetColor("_Color2", new Color(patch.r, patch.g, patch.b, 1f));
        // How far in from the rim the fraying reaches, in metres: more on big lands, a touch on a path.
        block.SetFloat("_Fray", Mathf.Clamp(radius * 0.22f, 0.45f, 4.5f));
        block.SetFloat("_Radius", radius);
        Texture2D surface = tex != null ? Resources.Load<Texture2D>("Village/Tex/" + tex) : null;
        if (surface != null)
        {
            block.SetTexture("_MainTex", surface);
            block.SetFloat("_Tile", tile);
            block.SetFloat("_Spec", 0.06f);
            Texture bump = ToonKit.BumpFor(surface);
            if (bump != null)
            {
                block.SetTexture("_BumpMap", bump);
                block.SetFloat("_Bump", 1f);
            }
        }
        r.SetPropertyBlock(block);
    }

    GameObject Prop(PropDef d, Vector3 pos, System.Random rng, float scaleBoost, float fixedYaw = float.NaN)
    {
        var holder = new GameObject(d.model);
        holder.transform.SetParent(LevelRoot, false);
        holder.transform.position = pos;
        // A fixed yaw means "place exactly": buildings, gate pieces.
        bool exact = !float.IsNaN(fixedYaw);
        float vary = exact ? scaleBoost : Mathf.Lerp(0.85f, 1.2f, (float)rng.NextDouble()) * scaleBoost;
        float yaw = exact ? fixedYaw : (float)rng.NextDouble() * 360f;

        bool nature = d.tex == Nat || d.tex == NatDry;
        Transform model = d.tex == Kit ? Village.Make(d.model, holder.transform)
            : d.tex == Fur ? Furnishing(d.model, holder.transform)
            : nature ? ToonKit.SpawnNature(d.model, holder.transform, d.sway, d.leaves, d.tex == NatDry, d.wash)
            : d.tex != null ? ToonKit.SpawnEnv(d.model, d.tex, holder.transform, d.sway) : null;
        float scale = vary;
        bool boxed = false;
        if (model != null)
        {
            // Environment packs use their own scale: size each model to the wanted height
            // and sit its base on the ground.
            Renderer[] rs = model.GetComponentsInChildren<Renderer>();
            if (rs.Length > 0)
            {
                Bounds b = rs[0].bounds;
                foreach (Renderer r in rs) b.Encapsulate(r.bounds);
                if (d.height > 0f) scale = d.height / Mathf.Max(0.01f, Mathf.Max(b.size.x, b.size.y, b.size.z)) * vary;
                else if (exact) scale = scaleBoost;
                model.localPosition += Vector3.down * (b.min.y - pos.y);
                if (d.box)
                {
                    // Walls you walk into where the walls are, a little inside the eaves. The holder
                    // is still unturned and unscaled here, so the model's bounds are its own shape.
                    var wall = holder.AddComponent<BoxCollider>();
                    wall.center = new Vector3(b.center.x - pos.x, b.size.y * 0.5f, b.center.z - pos.z);
                    wall.size = new Vector3(b.size.x * 0.8f, b.size.y, b.size.z * 0.8f);
                    boxed = true;
                }
            }
        }
        else
        {
            ToonKit.Spawn(d.tex == null ? d.model : "Rock", holder.transform, 1f, true, d.sway);
        }
        holder.transform.rotation = Quaternion.Euler(0, yaw, 0);
        holder.transform.localScale = Vector3.one * scale;

        // Lanterns, torches and fires carry a halo that comes alive at night, and throw a pool of light on the ground.
        Vector3 flame = d.tex == Kit ? Village.LightOf(d.model) : Vector3.zero;
        if (flame != Vector3.zero)
        {
            Color warm = d.model == "LampPost" ? new Color(1f, 0.8f, 0.42f) : new Color(1f, 0.6f, 0.22f);
            AddGlow(holder.transform, 0f, warm, 2.2f).transform.localPosition = flame;
            Glow pool = AddGlow(holder.transform, 0f, warm, d.model == "LampPost" ? 9f : 6f);
            pool.flat = true;
            pool.transform.localPosition = new Vector3(flame.x, 0.06f, flame.z);
            pool.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        }

        if (d.radius <= 0f || boxed) return holder;
        // Collider sizes are given in world units, so undo the holder scale.
        float h = Mathf.Max(3f, d.radius * vary * 2f) / scale;
        Capsule(holder, d.radius * vary / scale, h, h * 0.5f);
        return holder;
    }

    // The small things underfoot: clumps of real grass, pebbles, fallen petals, clover, stepping
    // stones. Each is a model from the nature kit, its size, and how much the wind moves it.
    static readonly (string model, float size, float sway)[] Litter =
    {
        ("Grass_Common_Short", 0.55f, 0.6f), ("Grass_Common_Tall", 0.5f, 0.6f), ("Grass_Wispy_Short", 0.6f, 0.6f), ("Grass_Wispy_Tall", 0.5f, 0.6f),
        ("Grass_Common_Short", 0.45f, 0.6f), ("Grass_Wispy_Short", 0.5f, 0.6f), ("Grass_Common_Tall", 0.4f, 0.6f),
        ("Pebble_Round_1", 1f, 0f), ("Pebble_Round_2", 1f, 0f), ("Pebble_Round_3", 1f, 0f), ("Pebble_Round_4", 1f, 0f), ("Pebble_Round_5", 1f, 0f),
        ("Pebble_Square_1", 1f, 0f), ("Pebble_Square_2", 1f, 0f), ("Pebble_Square_3", 1f, 0f), ("Pebble_Square_4", 1f, 0f),
        ("Pebble_Square_5", 1f, 0f), ("Pebble_Square_6", 1f, 0f),
        ("Petal_1", 0.9f, 0f), ("Petal_2", 0.8f, 0f), ("Petal_3", 0.8f, 0f), ("Petal_4", 0.9f, 0f), ("Petal_5", 0.7f, 0f),
        ("Clover_1", 0.35f, 0.6f), ("Clover_2", 0.35f, 0.6f), ("Plant_7", 0.8f, 0.6f), ("Plant_7_Big", 0.8f, 0.6f),
        ("Flower_3_Single", 0.3f, 0.6f), ("Flower_4_Single", 0.3f, 0.6f), ("RockPath_Round_Wide", 0.7f, 0f), ("RockPath_Square_Wide", 0.7f, 0f),
    };

    static readonly Color MeadowGreen = new Color(0.36f, 0.55f, 0.27f);

    // The colour a land's ground gives to the kit's (green) grass: none in a meadow, gold in the dunes, violet in the hollow.
    static Color LeafTint(LevelDef look) => new Color(look.ground.r / MeadowGreen.r, look.ground.g / MeadowGreen.g, look.ground.b / MeadowGreen.b, 1f);

    // Strews the small things over a circle of ground, then draws them as one.
    void Scatter(System.Random rng, Vector2 centre, float radius, int count, Color leaf, System.Func<Vector2, bool> skip, int layer = 0)
    {
        var root = new GameObject("Scatter").transform;
        root.SetParent(LevelRoot, false);
        for (int i = 0; i < count; i++)
        {
            Vector2 p = centre + RandomInCircle(rng, radius);
            var item = Litter[rng.Next(Litter.Length)];
            float yaw = (float)rng.NextDouble() * 360f, vary = Mathf.Lerp(0.8f, 1.25f, (float)rng.NextDouble());
            if (skip != null && skip(p)) continue;
            Transform t = ToonKit.SpawnNature(item.model, root, item.sway, leaf);
            if (t == null) continue;
            t.position = new Vector3(p.x, 0f, p.y);
            t.rotation = Quaternion.Euler(0f, yaw, 0f) * t.rotation;
            t.localScale *= item.size * vary;
            foreach (Renderer r in t.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        if (layer != 0) SetLayer(root, layer);
        if (Application.isPlaying) GeneratedResources.Combine(root);
    }

    // A prop from the props kit, as scenery (it drains with the rest of the world).
    static Transform Furnishing(string prop, Transform parent)
    {
        GameObject go = Humans.Furniture(prop, true);
        if (go == null) return null;
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    static void Capsule(GameObject go, float radius, float height, float centerY)
    {
        var c = go.AddComponent<CapsuleCollider>();
        c.radius = radius;
        c.height = height;
        c.center = new Vector3(0, centerY, 0);
    }
}
