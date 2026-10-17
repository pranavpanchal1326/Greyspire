using System.Collections.Generic;
using UnityEngine;

// The Wilds: one large open map between Brushwick (south) and the Gloom Tower
// (north). Ten regions, each with its own scenery, weather and enemies: meadow,
// pine forest, a lake, desert, graveyard, crystal fields, an autumn vale, a
// marsh, snowy heights and a burnt scar. A road runs through the middle; camps
// leave you alone until you come close; there are chests to find and regions
// to discover. Sparks picked up here are banked on the way home.
public partial class Game
{
    const float WildsRadius = 165f;
    const float GateZ = 156f;           // the town gate sits this far south, the tower door this far north
    const int TreeLayer = 8, SmallLayer = 9;   // scenery layers, so far-off detail can be skipped

    public bool InWilds { get; private set; }

    class Region
    {
        public string id, name;
        public int mood;
        public Vector2 centre;
        public float radius;
        public int tier;                // enemy strength, in tower-floor terms
        public LevelDef look;           // ground and grass colours
        public PropDef[] props;
        public int propCount, grass, flowers;
        public Enemy.Kind[] camp;       // what its monster dens are made of
        public int bandits;             // how many of its four camps are bandit camps instead
    }

    // Standing water (wade through it) or lava (walk round it).
    struct Pool
    {
        public Vector2 centre;
        public float radius;
        public bool lava;

        public Pool(float x, float z, float radius, bool lava)
        {
            centre = new Vector2(x, z); this.radius = radius; this.lava = lava;
        }
    }

    static LevelDef Ground(Color ground, Color patch) => new LevelDef { ground = ground, patch = patch };

    static readonly LevelDef ForestLook = Ground(new Color(0.27f, 0.5f, 0.28f), new Color(0.2f, 0.41f, 0.24f));
    static readonly LevelDef ShoreLook = Ground(new Color(0.48f, 0.72f, 0.42f), new Color(0.4f, 0.63f, 0.38f));
    static readonly LevelDef GraveLook = Ground(new Color(0.45f, 0.47f, 0.42f), new Color(0.38f, 0.42f, 0.38f));
    static readonly LevelDef AutumnLook = Ground(new Color(0.8f, 0.66f, 0.3f), new Color(0.76f, 0.5f, 0.24f));
    static readonly LevelDef MarshLook = Ground(new Color(0.3f, 0.44f, 0.3f), new Color(0.24f, 0.36f, 0.28f));
    static readonly LevelDef FrostLook = Ground(new Color(0.9f, 0.94f, 0.98f), new Color(0.78f, 0.86f, 0.95f));
    static readonly LevelDef EmberLook = Ground(new Color(0.27f, 0.22f, 0.24f), new Color(0.42f, 0.24f, 0.18f));

    // Leaf colours, and coats laid over whole trees and rocks (alpha is how thick the coat lies).
    static readonly Color GoldLeaf = new Color(2.4f, 1.25f, 0.5f), AmberLeaf = new Color(2.8f, 0.9f, 0.4f), MurkLeaf = new Color(0.6f, 0.8f, 0.7f);
    static readonly Color Snow = new Color(0.93f, 0.96f, 1f, 0.55f), Rime = new Color(0.9f, 0.94f, 1f, 0.4f);
    static readonly Color Ash = new Color(0.13f, 0.11f, 0.12f, 0.6f), Moss = new Color(0.28f, 0.4f, 0.24f, 0.4f);
    static readonly Color Arcane = new Color(0.6f, 0.45f, 1f, 0.55f), Grief = new Color(0.36f, 0.33f, 0.46f, 0.6f);

    static readonly Vector2 LakeCentre = new Vector2(88f, -52f);
    const float LakeRadius = 21f;

    static readonly Pool[] Pools =
    {
        new Pool(-125f, 12f, 7f, false), new Pool(-104f, 30f, 5.5f, false), new Pool(-118f, 38f, 6f, false), new Pool(-100f, 8f, 4.5f, false),
        new Pool(88f, 98f, 6f, true), new Pool(104f, 114f, 5f, true), new Pool(100f, 92f, 4f, true), new Pool(82f, 116f, 4.5f, true),
    };

    static Region[] regionDefinitions;
    static Region[] RegionDefinitions => regionDefinitions ?? (regionDefinitions = CreateRegions());
    public static int RegionDefinitionCount => RegionDefinitions.Length;
    public static string RegionId(int index) => index >= 0 && index < RegionDefinitions.Length ? RegionDefinitions[index].id : "";
    public static int RegionIndex(string id) => System.Array.FindIndex(RegionDefinitions, r => r.id == id);
    public static int RegionTier(string id) { int index = RegionIndex(id); return index >= 0 ? RegionDefinitions[index].tier : 0; }
    static string RegionName(int index) => RegionDefinitions[Mathf.Clamp(index, 0, RegionDefinitions.Length - 1)].name;
    static Mood MoodForRegion(int index) => Moods[Mathf.Clamp(RegionDefinitions[Mathf.Clamp(index, 0, RegionDefinitions.Length - 1)].mood, 0, Moods.Length - 1)];

    Region[] regions;
    int wildFloor = 1, regionNow = -1;
    bool showMap;

    public int RegionCount => regions != null ? regions.Length : 0;

    // A good place to stand and look at a region: on the side nearest the road, looking in.
    public Vector3 RegionSpot(int index, out float yaw)
    {
        Region r = regions[index];
        Vector2 toRoad = new Vector2(RoadX(r.centre.y) - r.centre.x, 0f);
        Vector2 from = toRoad.magnitude > 20f ? r.centre + toRoad.normalized * (r.radius * 0.62f) : r.centre + new Vector2(0f, -r.radius * 0.5f);
        Vector2 look = r.centre - from;
        yaw = Mathf.Atan2(look.x, look.y) * Mathf.Rad2Deg;
        return new Vector3(from.x, 0.1f, from.y);
    }

    // Identity, appearance and terrain travel together when the definitions reorder.
    Region[] MakeRegions() => (Region[])RegionDefinitions.Clone();
    static Region[] CreateRegions()
    {
        const Enemy.Kind S = Enemy.Kind.Slime, M = Enemy.Kind.Minion, B = Enemy.Kind.BigSlime,
            W = Enemy.Kind.Spitter, X = Enemy.Kind.Bomber, T = Enemy.Kind.Brute;
        return new[]
        {
            new Region
            {
                id = "greenreach", mood = 0, name = "Greenreach Meadow", centre = new Vector2(0, -105), radius = 60, tier = 1, look = Levels[0], grass = 7000, flowers = 900,
                propCount = 64, camp = new[] { S, S, S, M }, bandits = 2,
                props = new[]
                {
                    new PropDef("CommonTree_1", Nat, 7.5f, 0.45f, TreeSway), new PropDef("Rock_Medium_1", Nat, 2.6f, 1.1f),
                    new PropDef("CommonTree_2", Nat, 8f, 0.45f, TreeSway), new PropDef("Bush_Common_Flowers", Nat, 2f, 0.8f, BushSway),
                    new PropDef("Flower_3_Group", Nat, 1.1f, 0f, BushSway), new PropDef("CommonTree_5", Nat, 7f, 0.45f, TreeSway),
                    new PropDef("LeanTo", Kit, 0f, 1.6f), new PropDef("Flower_4_Group", Nat, 1.3f, 0f, BushSway),
                    new PropDef("CommonTree_4", Nat, 9.5f, 0.45f, TreeSway), new PropDef("Plant_1", Nat, 0.9f, 0f, BushSway),
                    new PropDef("Banner", Kit, 0f, 0.2f), new PropDef("Barrel", Fur, 0f, 0.45f),
                },
            },
            new Region
            {
                id = "whispering-woods", mood = 1, name = "Whispering Woods", centre = new Vector2(-85, -55), radius = 52, tier = 6, look = ForestLook, grass = 6000, flowers = 500,
                propCount = 130, camp = new[] { M, M, W, B, S }, bandits = 3,
                props = new[]
                {
                    new PropDef("Pine_1", Nat, 9f, 0.45f, TreeSway), new PropDef("Pine_2", Nat, 9.5f, 0.45f, TreeSway),
                    new PropDef("Fern_1", Nat, 2.2f, 0f, BushSway), new PropDef("CommonTree_3", Nat, 10f, 0.45f, TreeSway),
                    new PropDef("Pine_4", Nat, 12f, 0.5f, TreeSway), new PropDef("Mushroom_Common", Nat, 0.7f, 0f),
                    new PropDef("Pine_3", Nat, 9f, 0.45f, TreeSway), new PropDef("Rock_Medium_3", Nat, 3f, 1.3f),
                    new PropDef("Pine_5", Nat, 10f, 0.5f, TreeSway), new PropDef("Bush_Common", Nat, 1.9f, 0.8f, BushSway),
                    new PropDef("CommonTree_4", Nat, 10f, 0.45f, TreeSway), new PropDef("Mushroom_Laetiporus", Nat, 1f, 0f),
                },
            },
            new Region
            {
                id = "mirror-lake", mood = 2, name = "Mirror Lake", centre = LakeCentre, radius = 48, tier = 4, look = ShoreLook, grass = 4600, flowers = 800,
                propCount = 50, camp = new[] { S, S, B, M }, bandits = 1,
                props = new[]
                {
                    new PropDef("CommonTree_2", Nat, 7.5f, 0.45f, TreeSway), new PropDef("Rock_Medium_2", Nat, 2.2f, 1f),
                    new PropDef("Plant_1_Big", Nat, 2.2f, 0f, BushSway), new PropDef("Bush_Common_Flowers", Nat, 1.9f, 0.8f, BushSway),
                    new PropDef("CommonTree_5", Nat, 7f, 0.45f, TreeSway), new PropDef("Flower_3_Group", Nat, 1.1f, 0f, BushSway),
                    new PropDef("Rock_Medium_1", Nat, 3f, 1.3f), new PropDef("Flower_4_Group", Nat, 1.3f, 0f, BushSway),
                },
            },
            new Region
            {
                id = "sunscorch", mood = 3, name = "Sunscorch Dunes", centre = new Vector2(48, 40), radius = 45, tier = 12, look = Levels[1], grass = 1100, flowers = 140,
                propCount = 54, camp = new[] { X, M, M, W, X }, bandits = 3,
                props = new[]
                {
                    new PropDef("Cactus", null, 2.2f, 0.45f, 0.3f), new PropDef("Pillar", Kit, 5f, 0.6f),
                    new PropDef("Rock_Medium_1", NatDry, 3.4f, 1.5f), new PropDef("Cactus", null, 2.2f, 0.45f, 0.3f),
                    new PropDef("Rubble", Kit, 0f, 1.1f), new PropDef("Pillar", Kit, 4.4f, 0.55f),
                    new PropDef("Rock_Medium_3", NatDry, 6.5f, 2.8f), new PropDef("DeadTree_1", Nat, 6.5f, 0.4f, 0.1f),
                    new PropDef("Rock_Medium_2", NatDry, 2.4f, 1.1f),
                },
            },
            new Region
            {
                id = "hollow-graves", mood = 4, name = "Hollow Graves", centre = new Vector2(-48, 45), radius = 45, tier = 18, look = GraveLook, grass = 4000, flowers = 160,
                propCount = 72, camp = new[] { M, M, M, W, T },
                props = new[]
                {
                    new PropDef("DeadTree_3", Nat, 10f, 0.45f, 0.1f), new PropDef("Headstone", Kit, 0f, 0.45f),
                    new PropDef("Headstone", Kit, 0f, 0.5f), new PropDef("DeadTree_5", Nat, 12f, 0.5f, 0.1f),
                    new PropDef("LampPost", Kit, 0f, 0.25f), new PropDef("Fence", Kit, 0f, 0.9f),
                    new PropDef("TwistedTree_3", Nat, 13f, 0.8f, 0.2f).Wash(Grief), new PropDef("Cage_Small", Fur, 1.1f, 0.5f),
                    new PropDef("Crate_Metal", Fur, 0f, 0.6f), new PropDef("Mushroom_Laetiporus", Nat, 1f, 0f),
                    new PropDef("CandleStick_Stand", Fur, 0f, 0.3f),
                },
            },
            new Region
            {
                id = "shardfield", mood = 5, name = "Shardfield", centre = new Vector2(0, 128), radius = 36, tier = 26, look = Levels[2], grass = 3000, flowers = 600,
                propCount = 44, camp = new[] { T, W, X, M, W },
                props = new[]
                {
                    new PropDef("Crystal", null, 2.2f, 0.65f), new PropDef("Pillar", Kit, 5.2f, 0.6f),
                    new PropDef("TwistedTree_1", Nat, 13f, 0.8f, 0.2f).Wash(Arcane), new PropDef("Crystal", null, 2.2f, 0.65f),
                    new PropDef("Torch", Kit, 0f, 0.2f), new PropDef("Banner", Kit, 0f, 0.25f),
                },
            },
            new Region
            {
                id = "goldleaf", mood = 6, name = "Goldleaf Vale", centre = new Vector2(115, 20), radius = 45, tier = 8, look = AutumnLook, grass = 5200, flowers = 500,
                propCount = 84, camp = new[] { M, S, B, M, W }, bandits = 3,
                props = new[]
                {
                    new PropDef("TwistedTree_1", Nat, 14f, 0.8f, 0.2f), new PropDef("CommonTree_1", Nat, 8f, 0.45f, TreeSway).Leaves(GoldLeaf),
                    new PropDef("Bush_Common", Nat, 2f, 0.8f, BushSway), new PropDef("CommonTree_3", Nat, 10f, 0.45f, TreeSway).Leaves(AmberLeaf),
                    new PropDef("Rock_Medium_2", Nat, 2.6f, 1.1f), new PropDef("TwistedTree_4", Nat, 15f, 0.8f, 0.2f),
                    new PropDef("Mushroom_Laetiporus", Nat, 1.1f, 0f), new PropDef("CommonTree_4", Nat, 9.5f, 0.45f, TreeSway).Leaves(GoldLeaf),
                    new PropDef("Fern_1", Nat, 2f, 0f, BushSway), new PropDef("TwistedTree_5", Nat, 13f, 0.8f, 0.2f),
                    new PropDef("Flower_4_Group", Nat, 1.3f, 0f, BushSway),
                },
            },
            new Region
            {
                id = "sunken-fen", mood = 7, name = "Sunken Fen", centre = new Vector2(-115, 20), radius = 45, tier = 10, look = MarshLook, grass = 5200, flowers = 260,
                propCount = 84, camp = new[] { B, S, W, M, B }, bandits = 1,
                props = new[]
                {
                    new PropDef("CommonTree_2", Nat, 9f, 0.45f, TreeSway).Leaves(MurkLeaf), new PropDef("DeadTree_2", Nat, 10f, 0.45f, 0.1f).Wash(Moss),
                    new PropDef("Plant_1_Big", Nat, 2.6f, 0f, BushSway), new PropDef("Fern_1", Nat, 2.6f, 0f, BushSway),
                    new PropDef("TwistedTree_2", Nat, 14f, 0.8f, 0.2f).Wash(Moss), new PropDef("Mushroom_Laetiporus", Nat, 1.4f, 0f),
                    new PropDef("Rock_Medium_1", Nat, 2.4f, 1f).Wash(Moss), new PropDef("Clover_1", Nat, 0.9f, 0f, BushSway),
                    new PropDef("Mushroom_Common", Nat, 0.8f, 0f), new PropDef("DeadTree_4", Nat, 11f, 0.45f, 0.1f).Wash(Moss),
                    new PropDef("CommonTree_5", Nat, 8f, 0.45f, TreeSway).Leaves(MurkLeaf),
                },
            },
            new Region
            {
                id = "frostfall", mood = 8, name = "Frostfall Heights", centre = new Vector2(-95, 105), radius = 48, tier = 21, look = FrostLook, grass = 1500, flowers = 0,
                propCount = 88, camp = new[] { M, T, W, M, M }, bandits = 2,
                props = new[]
                {
                    new PropDef("Pine_1", Nat, 9.5f, 0.45f, TreeSway).Wash(Snow), new PropDef("Pine_4", Nat, 12.5f, 0.5f, TreeSway).Wash(Snow),
                    new PropDef("Rock_Medium_3", Nat, 4f, 1.7f).Wash(Snow), new PropDef("Pine_2", Nat, 9f, 0.45f, TreeSway).Wash(Snow),
                    new PropDef("DeadTree_1", Nat, 8f, 0.4f, 0.1f).Wash(Rime), new PropDef("Pine_5", Nat, 10f, 0.5f, TreeSway).Wash(Snow),
                    new PropDef("Rock_Medium_1", Nat, 7f, 3f).Wash(Snow), new PropDef("Pine_3", Nat, 9f, 0.45f, TreeSway).Wash(Snow),
                },
            },
            new Region
            {
                id = "emberscar", mood = 9, name = "Emberscar", centre = new Vector2(95, 105), radius = 48, tier = 24, look = EmberLook, grass = 900, flowers = 0,
                propCount = 62, camp = new[] { X, T, X, M, W },
                props = new[]
                {
                    new PropDef("DeadTree_3", Nat, 10f, 0.45f, 0.1f).Wash(Ash), new PropDef("Rock_Medium_2", Nat, 3f, 1.3f).Wash(Ash),
                    new PropDef("DeadTree_5", Nat, 12f, 0.5f, 0.1f).Wash(Ash), new PropDef("Rock_Medium_3", Nat, 6.5f, 2.8f).Wash(Ash),
                    new PropDef("DeadTree_1", Nat, 7f, 0.4f, 0.1f).Wash(Ash), new PropDef("Rubble", Kit, 0f, 1.1f),
                    new PropDef("Rock_Medium_1", Nat, 4f, 1.7f).Wash(Ash),
                },
            },
        };
    }

    // The road winds from the town gate (south) to the tower door (north).
    static float RoadX(float z) => Mathf.Sin(z * 0.03f) * 14f;

    void EnterWilds()
    {
        if (InTown && ResumeRewards()) return;
        Transition(() =>
        {
            timers.Clear();
            numbers.Clear();
            Juice.Reset();
            ResetStyle();
            if (Player.Hero != Heroes.All[Heroes.Selected]) Player.SetHero(Heroes.Selected);
            Save.BeginExpedition(true);
            DefeatPending = false;
            Player.NewRun();
            Player.weak = 1f;
            kills = combo = maxCombo = 0;
            shownCoins = 0f;
            Gloom = 0;
            level = 0;
            wave = -1;
            waveActive = false;
            awakening = false;
            pending = 0;
            hintStep = -1;
            hubPanel = 0;
            regionNow = -1;
            showMap = false;
            InWilds = true;

            BuildWilds();
            sat = satTarget = 1f;
            Pickup.AttractAll = false;
            Player.gameObject.SetActive(true);
            Player.Revive(new Vector3(RoadX(-GateZ + 6f), 0.1f, -GateZ + 6f), true);
            Player.transform.rotation = Quaternion.identity;
            Cam.Face(0f);
            Cam.Snap();
            music.SetTrack(1);
            SetState(GameState.Playing);
            // Open high over the gate, looking up the road across the land, then come down to the hero.
            BeginIntro("THE WILDS", "Bandit country, and worse. The Tower lies north.",
                new Vector3(-24f, 30f, -GateZ + 2f), new Vector3(RoadX(-GateZ + 62f), 0f, -GateZ + 62f), ArrivalTime(ref seenWilds, 6.5f));
        });
    }

    // Back to town, banking whatever was picked up out here.
    void LeaveWilds()
    {
        Save.BankCarried();
        EnterHub();
    }

    // ------------------------------------------------------------ building

    void BuildWilds()
    {
        GameAudio.StopWorld();
        if (LevelRoot != null)
        {
            LevelRoot.gameObject.SetActive(false);
            Destroy(LevelRoot.gameObject);
        }
        LevelRoot = new GameObject("Wilds").transform;
        ProductionRandom.BeginScene("wilds");
        PlaytestLog.Emit("scope_start", note: ProductionRandom.Scope);
        InTown = false;
        spots.Clear();
        packSpots.Clear();
        stairSpot = null;
        nearSpot = null;
        ClearResidents();
        regions = MakeRegions();
        LevelDef def = Levels[0];
        var rng = new System.Random(90210);

        cam.backgroundColor = def.skyBottom;
        RenderSettings.fogColor = def.skyBottom;
        RenderSettings.ambientLight = def.ambient;
        sun.color = def.sun;
        BuildSky(def, rng, WildsRadius + 45f);
        BuildAmbience();

        var floor = new GameObject("Floor");
        floor.transform.SetParent(LevelRoot, false);
        var floorBox = floor.AddComponent<BoxCollider>();
        floorBox.size = new Vector3(600f, 2f, 600f);
        floorBox.center = new Vector3(0, -1f, 0);
        const int segments = 120;
        float segLen = 2f * Mathf.PI * WildsRadius / segments + 0.5f;
        for (int i = 0; i < segments; i++)
        {
            float a = i * Mathf.PI * 2f / segments;
            var wall = new GameObject("Wall");
            wall.transform.SetParent(LevelRoot, false);
            wall.transform.position = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * (WildsRadius + 0.5f) + Vector3.up * 5f;
            wall.transform.rotation = Quaternion.Euler(0, a * Mathf.Rad2Deg, 0);
            wall.AddComponent<BoxCollider>().size = new Vector3(segLen, 10f, 1f);
        }

        // Ground: one base colour, then each region painted over it.
        // Each region's ground breaks up into patches of its second colour, and its edge is
        // ragged, so one land runs into the next instead of stopping at a ruled circle.
        Disc(def.outer, 340f, -0.09f, 0f, 0f, def.outer * 0.88f);
        Disc(def.ground, WildsRadius + 6f, -0.08f, 0f, 0f, def.patch);
        for (int i = 0; i < regions.Length; i++)
        {
            Region r = regions[i];
            Disc(r.look.ground, r.radius + 3f, -0.07f + i * 0.003f, r.centre.x, r.centre.y, r.look.patch);
        }

        // The lake: a sandy shore, then water you have to walk around.
        Disc(new Color(0.93f, 0.86f, 0.62f), LakeRadius + 5f, -0.025f, LakeCentre.x, LakeCentre.y);
        Water(LakeCentre, LakeRadius, -0.02f);
        Block("Water", LakeCentre, LakeRadius - 0.5f);

        // Marsh pools lie in the grass and can be waded; the lava in the scar cannot.
        foreach (Pool pool in Pools)
        {
            if (!pool.lava)
            {
                Disc(new Color(0.2f, 0.3f, 0.22f), pool.radius + 1.2f, -0.025f, pool.centre.x, pool.centre.y);
                Water(pool.centre, pool.radius, -0.02f);
                continue;
            }
            Disc(new Color(0.12f, 0.09f, 0.1f), pool.radius + 1.5f, -0.025f, pool.centre.x, pool.centre.y);
            Disc(new Color(1f, 0.42f, 0.08f), pool.radius, -0.02f, pool.centre.x, pool.centre.y);
            LavaGlow(pool);
            Block("Lava", pool.centre, pool.radius - 0.4f);
        }

        // The road.
        Color road = new Color(0.78f, 0.7f, 0.55f);
        for (float z = -GateZ - 2f; z <= GateZ + 2f; z += 2.3f) Disc(road, 2.6f, -0.012f, RoadX(z), z, road * 0.9f);

        // Thin grass over the whole map, so the land between regions is never bare.
        BuildGrass(def, WildsRadius, rng, 15000, p => !Open(p, 2.6f) || InRegion(p));

        // Scenery, grass, camps and chests, region by region.
        for (int i = 0; i < regions.Length; i++)
        {
            Region r = regions[i];
            int placed = 0, tries = 0;
            while (placed < r.propCount && tries++ < r.propCount * 8)
            {
                Vector2 p = r.centre + RandomInCircle(rng, r.radius);
                PropDef d = r.props[placed % r.props.Length];
                // Small things (flowers, ferns, mushrooms) may come close to the road; trees and rocks keep off it.
                if (!Open(p, d.radius > 0f ? 3.6f : 2.8f)) continue;
                GameObject prop = Prop(d, new Vector3(p.x, 0, p.y), rng, 1.1f);
                SetLayer(prop.transform, d.radius > 0f ? TreeLayer : SmallLayer);
                placed++;
            }

            Vector2 centre = r.centre;
            BuildGrass(r.look, r.radius + 3f, rng, r.grass, p => !Open(p, 2.6f), centre);
            if (r.flowers > 0) BuildFlowers(MoodForRegion(i).flowers, centre, r.radius, r.flowers, rng, p => !Open(p, 2.6f));
            Scatter(rng, centre, r.radius, 60, LeafTint(r.look), p => !Open(p, 2.4f), SmallLayer);

            var encounterRandom = new System.Random(unchecked((int)ProductionRandom.Hash("wilds:encounters-v1:" + r.id)));
            for (int c = 0; c < 4; c++)
            {
                Vector2 at = CampSpot(r, encounterRandom);
                wildFloor = r.tier;
                // Some camps are monster dens; the rest belong to bandits, who have a fire and a tent.
                bool bandits = c < r.bandits;
                if (bandits) BanditCamp(at, rng);
                // The commission in hand may have business at this region's first bandit camp.
                Mission job = ActiveMission;
                bool business = bandits && c == 0 && job != null && job.region == i && !MissionReady;
                if (business && job.job == Job.Recover) Strongbox(at, job);
                int size = 3 + encounterRandom.Next(3);
                for (int e = 0; e < size; e++)
                {
                    Vector2 off = RandomInCircle(encounterRandom, 3.5f);
                    Enemy.Kind kind;
                    if (bandits)
                    {
                        // Fighters and knife-throwers; in the rougher regions a chief leads them.
                        kind = e == 0 && r.tier >= 8 ? Enemy.Kind.Brute : encounterRandom.Next(100) < 30 ? Enemy.Kind.Spitter : Enemy.Kind.Minion;
                        off = off.normalized * Mathf.Max(1.8f, off.magnitude);
                    }
                    else
                    {
                        kind = r.camp[encounterRandom.Next(r.camp.Length)];
                        // One heavy hitter per camp at most.
                        if (kind == Enemy.Kind.Brute && e > 0) kind = Enemy.Kind.Minion;
                    }
                    // A wanted chief is always at home, and is not an ordinary chief.
                    bool wanted = business && job.job == Job.Chief && e == 0;
                    if (wanted) kind = Enemy.Kind.Brute;
                    Enemy enemy = Enemy.Create(kind, new Vector3(at.x + off.x, 0.1f, at.y + off.y), r.tier / 10, wanted || encounterRandom.Next(100) < 8, bandits);
                    enemy.BindObjective(r.id, wanted ? job.targetId : "");
                    enemy.aggro = 20f;
                    if (wanted) enemy.title = job.target;
                    // Bandits stand round their fire, facing it.
                    if (bandits) enemy.transform.rotation = Quaternion.LookRotation(new Vector3(-off.x, 0f, -off.y));
                }
            }

            var chestRandom = new System.Random(unchecked((int)ProductionRandom.Hash("wilds:treasure-v1:" + r.id)));
            for (int c = 0; c < 2; c++)
            {
                int chestSlot = c;
                string chestRegion = r.id, chestExpedition = Save.ExpeditionId;
                Vector2 at = CampSpot(r, chestRandom);
                if (Save.WildChestOpened(chestRegion, chestSlot)) continue;
                GameObject chest = Prop(new PropDef("Chest_Wood", Fur, 1.5f, 0.6f), new Vector3(at.x, 0, at.y), chestRandom, 1f, (float)chestRandom.NextDouble() * 360f);
                int reward = 10 + r.tier * 2;
                Spot spot = null;
                spot = AddSpot(new Vector3(at.x, 0, at.y), "Treasure", "Open the chest", () =>
                {
                    // Treasure is Sparks. Strength is not found in a box: it is earned in a fight.
                    if (!Save.OpenWildChest(chestRegion, chestSlot, chestExpedition)) return;
                    coinPunch = 1f;
                    Toast("+" + reward + " SPARKS", UI.Gold);
                    Fx.Burst(chest.transform.position + Vector3.up, UI.Gold, 16, 7f);
                    ToonKit.Sfx("buy");
                    chest.SetActive(false);
                    spots.Remove(spot);
                }, chest.transform);
            }
        }

        // Far-off detail is skipped: trees once the fog has them, small plants a little past arm's reach.
        var cull = new float[32];
        cull[TreeLayer] = 200f;
        cull[SmallLayer] = 75f;
        cam.layerCullDistances = cull;
        cam.layerCullSpherical = true;

        // South: the way home. North: the tower.
        Prop(new PropDef("Pillar", Kit, 6f, 0.7f), new Vector3(RoadX(-GateZ) - 3.4f, 0, -GateZ), rng, 1f, 0f);
        Prop(new PropDef("Pillar", Kit, 6f, 0.7f), new Vector3(RoadX(-GateZ) + 3.4f, 0, -GateZ), rng, 1f, 0f);
        // Beyond the gate: the first houses of Brushwick, and the watchtower over them.
        Outskirt(Home(4, 6, 2, 41), new Vector3(-12f, 0f, -GateZ - 13f), 20f);
        Outskirt(Home(6, 8, 2, 42, true), new Vector3(15f, 0f, -GateZ - 15f), -25f);
        Outskirt(Tower(5, 43), new Vector3(27f, 0f, -GateZ - 9f), -40f);
        AddSpot(new Vector3(RoadX(-GateZ), 0, -GateZ), "Brushwick", "Return to town (banks your Sparks)", LeaveWilds);

        // The Gloom Tower: the door faces back down the road.
        Outskirt(GloomTower(12), new Vector3(RoadX(GateZ), 0f, GateZ + 14f), 180f);
        Prop(new PropDef("Pillar", Kit, 7f, 0.7f), new Vector3(RoadX(GateZ) - 3.4f, 0, GateZ), rng, 1f, 0f);
        Prop(new PropDef("Pillar", Kit, 7f, 0.7f), new Vector3(RoadX(GateZ) + 3.4f, 0, GateZ), rng, 1f, 0f);
        AddSpot(new Vector3(RoadX(GateZ - 1f), 0, GateZ - 1f), "The Gloom Tower", "Enter the tower", () => OpenHubPanel(1));
    }

    // A building of the village kit standing outside the map's edge: scenery, seen from the road.
    void Outskirt(Village.Plan plan, Vector3 pos, float yaw)
    {
        GameObject house = Village.House(plan, LevelRoot, plan.dark ? "The Gloom Tower" : "House");
        house.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
        Village.Settle(house);
    }

    bool InRegion(Vector2 p)
    {
        foreach (Region r in regions)
            if ((p - r.centre).magnitude < r.radius) return true;
        return false;
    }

    // A fire with a tent and stores beside it: somebody lives here, and not by honest work.
    void BanditCamp(Vector2 at, System.Random rng)
    {
        Vector3 centre = new Vector3(at.x, 0f, at.y);
        float turn = (float)rng.NextDouble() * 360f;
        Vector3 back = Quaternion.Euler(0f, turn, 0f) * Vector3.forward;
        Prop(new PropDef("Campfire", Kit, 0f, 0.5f), centre, rng, 1f, 0f);
        Prop(new PropDef("Bed_Twin1", Fur, 0f, 0.9f), centre + Quaternion.Euler(0f, -110f, 0f) * back * 5.2f, rng, 1f, turn + 70f);
        Prop(new PropDef("LeanTo", Kit, 0f, 1.6f), centre + back * 6.2f, rng, 1f, turn + 180f);
        Prop(new PropDef("Barrel", Fur, 0f, 0.45f), centre + Quaternion.Euler(0f, 55f, 0f) * back * 5.4f, rng, 1f);
        Prop(new PropDef("Crate_Wooden", Fur, 0f, 0.65f), centre + Quaternion.Euler(0f, -50f, 0f) * back * 5.6f, rng, 1f);
        Prop(new PropDef("Banner", Kit, 0f, 0.2f), centre + Quaternion.Euler(0f, 115f, 0f) * back * 6.6f, rng, 1f);
    }

    // What the bandits took: it sits by their fire until the hero takes it back.
    void Strongbox(Vector2 camp, Mission job)
    {
        GameObject box = Humans.Furniture("Chest_Wood");
        if (box == null) return;
        Vector3 pos = new Vector3(camp.x + 1.6f, 0f, camp.y - 1.2f);
        box.transform.SetParent(LevelRoot, false);
        box.transform.position = pos;
        Spot spot = null;
        var entitlement = new QuestEvent(job.id, Save.MissionAttempt, Save.ExpeditionId, System.Guid.NewGuid().ToString("N"), job.regionId, job.targetId, ObjectiveCause.Recover);
        spot = AddSpot(pos, job.target, "Take it back", () =>
        {
            if (!Save.RecordObjective(entitlement)) return;
            Fx.Burst(pos + Vector3.up, UI.Gold, 14, 6f);
            ToonKit.Sfx("buy");
            box.SetActive(false);
            spots.Remove(spot);
        }, box.transform);
    }

    // Something you have to walk round: an unseen pillar where the water or lava is.
    void Block(string name, Vector2 centre, float radius)
    {
        var go = new GameObject(name);
        go.transform.SetParent(LevelRoot, false);
        go.transform.position = new Vector3(centre.x, 0, centre.y);
        Capsule(go, radius, 60f, 0f);
    }

    // The light a lava pool throws: a soft, over-bright disc that the bloom picks up.
    void LavaGlow(Pool pool)
    {
        var glow = GameObject.CreatePrimitive(PrimitiveType.Quad);
        DestroyImmediate(glow.GetComponent<Collider>());
        glow.name = "LavaGlow";
        glow.transform.SetParent(LevelRoot, false);
        glow.transform.position = new Vector3(pool.centre.x, 0.04f, pool.centre.y);
        glow.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        glow.transform.localScale = Vector3.one * pool.radius * 3f;
        var r = glow.GetComponent<Renderer>();
        r.sharedMaterial = Fx.Mote;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        Fx.Tint(r, new Color(2.4f, 1f, 0.25f, 0.8f));
    }

    static void SetLayer(Transform t, int layer)
    {
        t.gameObject.layer = layer;
        for (int i = 0; i < t.childCount; i++) SetLayer(t.GetChild(i), layer);
    }

    // Is this spot free of road, water, lava and the two gates?
    bool Open(Vector2 p, float roadClearance)
    {
        if (p.magnitude > WildsRadius - 3f) return false;
        if (Mathf.Abs(p.x - RoadX(p.y)) < roadClearance) return false;
        if ((p - LakeCentre).magnitude < LakeRadius + 2.5f) return false;
        foreach (Pool pool in Pools)
            if ((p - pool.centre).magnitude < pool.radius + 1.5f) return false;
        if (p.y < -GateZ + 10f || p.y > GateZ - 8f) return false;
        return true;
    }

    Vector2 CampSpot(Region r, System.Random rng)
    {
        Vector2 p = r.centre;
        for (int i = 0; i < 40; i++)
        {
            p = r.centre + RandomInCircle(rng, r.radius - 6f);
            if (Open(p, 9f) && (p - new Vector2(0, -GateZ + 6f)).magnitude > 32f) break;
        }
        return p;
    }

    // ------------------------------------------------------------ per frame and HUD

    void TickWilds()
    {
        if (Player.Dead || DefeatPending) { nearSpot = null; return; }
        if (hubPanel != 0 || fadeDir != 0f) return;
        Vector3 pos = Player.transform.position;
        var here = new Vector2(pos.x, pos.z);

        if (GameInput.MapPressed && !Intro)
        {
            SetMap(!showMap);
        }
        if (showMap) { nearSpot = null; return; }
        nearSpot = null;
        float best = 4f;
        foreach (Spot s in spots)
        {
            float d = (s.pos - pos).magnitude;
            if (d < best && WorldSupport.CanInteract(Player.transform.position, s.pos, targetOwner: s.owner)) { best = d; nearSpot = s; }
        }
        if (nearSpot != null && Time.frameCount > resumeFrame && GameInput.InteractPressed)
        {
            ToonKit.Sfx("click");
            SpotGesture(nearSpot);
            nearSpot.act();
            return;
        }

        // Which region are we in? A new one has to be clearly nearer than the old before the
        // hero counts as having crossed over, so walking a border does not flicker between two.
        int now = 0;
        float score0 = float.MaxValue;
        for (int i = 0; i < regions.Length; i++)
        {
            float s = (here - regions[i].centre).magnitude / regions[i].radius;
            if (s < score0) { score0 = s; now = i; }
        }
        if (now == regionNow) return;
        if (regionNow >= 0 && score0 > (here - regions[regionNow].centre).magnitude / regions[regionNow].radius - 0.07f) return;
        bool arriving = regionNow < 0;
        regionNow = now;
        music.SetTrack(ScoreForRegion(regions[now].id));
        wildFloor = regions[now].tier;
        ApplyAmbience(now);
        if (arriving)
        {
            // Stepping out of the gate: the arrival has already said where this is.
            Save.DiscoverRegion(regions[now].id, Save.ExpeditionId, false);
            return;
        }
        Banner(regions[now].name.ToUpper(), 2.6f, Danger(regions[now].tier));
        if ((Save.RegionMask & (1 << now)) != 0) return;
        // Finding a new land is worth a purse from the Guild's mapmakers, not a level.
        int purse = 8 + regions[now].tier;
        int levelBefore = Save.Level;
        if (!Save.DiscoverRegion(regions[now].id, Save.ExpeditionId, true)) return;
        coinPunch = 1f;
        Toast("NEW REGION DISCOVERED   +" + purse + " SPARKS", UI.Gold);
        ToonKit.Sfx("epic");
        PresentXp(levelBefore);
    }

    // How a region's danger compares with the hero, in words: the tower floor it is a match for.
    static string Danger(int tier)
    {
        int reach = Save.TowerFloor + 1;
        string word = tier <= reach - 4 ? "Safe ground for you now" : tier <= reach + 2 ? "A fair fight" : tier <= reach + 8 ? "Dangerous" : "Deadly: far beyond you";
        return word + "   (as strong as floor " + tier + ")";
    }

    void DrawWildsHud(float w, float h, float cx)
    {
        string region = regionNow >= 0 ? regions[regionNow].name : "The Wilds";
        UI.Text(new Rect(0, 12, w, 28), region.ToUpper(), 20, Color.white);
        int found = 0;
        for (int i = 0; i < regions.Length; i++) if ((Save.RegionMask & (1 << i)) != 0) found++;
        UI.Text(new Rect(0, 38, w, 24), "Regions discovered  " + found + " / " + regions.Length + (Night > 0.6f ? "      Night: the Gloom is restless, kills are worth more" : ""),
            15, new Color(0.85f, 0.85f, 0.95f), TextAnchor.MiddleCenter, false);

        Vector3 me = Player.transform.position;
        foreach (Spot s in spots)
        {
            if ((s.pos - me).magnitude > 30f || !ToGui(s.pos + Vector3.up * 2.6f, out Vector2 p)) continue;
            UI.Text(UI.Centered(p.x, p.y, 320, 30), s.name, 18, s == nearSpot ? UI.Gold : Color.white);
        }

        DrawMinimap(w, h, 70f);
        UI.Text(new Rect(w - 300, h - 216, 280, 20), ControlGuide.Key(ControlAction.Map) + "  -  map", 14, new Color(0.85f, 0.85f, 0.95f), TextAnchor.MiddleRight, false);
        if (showMap) DrawMap(w, h, cx);
        if (hubPanel == 0 && nearSpot != null) DrawPrompt(h, cx, nearSpot.prompt);
        if (hubPanel == 1) DrawGatePanel(w, h, cx);
    }

    // What the E key will do here. In a fight it sits above the skill bar.
    void DrawPrompt(float h, float cx, string prompt)
    {
        var r = UI.Centered(cx, h - (State == GameState.Hub ? 84f : 132f), Mathf.Min(680f, UI.W - 60f), 50);
        UI.Framed(r, new Color(0.078f, 0.082f, 0.1f, 0.94f), UI.Gold, 3f);
        UI.Text(r, "[ " + ControlGuide.Key(ControlAction.Interact) + " ]   " + prompt, 19, Color.white);
    }

    // Minimap: turns with the camera, so "up" is the way you are looking. Enemies in red, places
    // of interest in blue, treasure in gold. Out in the Wilds the commission's region is a gold ring.
    void DrawMinimap(float w, float h, float range)
    {
        const float R = 82f;
        Vector3 me = Player.transform.position;
        Vector2 mc = new Vector2(w - R - 26f, h - R - 26f);
        UI.Tex(UI.Centered(mc.x, mc.y, R * 2 + 10, R * 2 + 10), UI.Circle, UI.Ink);
        UI.Tex(UI.Centered(mc.x, mc.y, R * 2, R * 2), UI.Circle, new Color(0.16f, 0.22f, 0.2f, 0.92f));
        Quaternion turn = Quaternion.Euler(0, -cam.transform.eulerAngles.y, 0);
        foreach (Enemy e in Enemy.All)
        {
            Vector3 d = turn * (e.transform.position - me);
            if (d.magnitude > range) continue;
            UI.Tex(UI.Centered(mc.x + d.x / range * R, mc.y - d.z / range * R, e.elite ? 9 : 6, e.elite ? 9 : 6), UI.Circle, e.elite ? UI.Gold : UI.Red);
        }
        foreach (Spot s in spots)
        {
            // Places of interest stay pinned to the rim when out of range, like a compass.
            Vector3 d = turn * (s.pos - me);
            d.y = 0f;
            if (d.magnitude > range) d = d.normalized * range;
            UI.Tex(UI.Centered(mc.x + d.x / range * R, mc.y - d.z / range * R, 11, 11), UI.Circle,
                s.name == "Treasure" ? UI.Gold : UI.Blue);
        }
        Mission job = ActiveMission;
        if (InWilds && job != null && job.region >= 0 && !MissionReady)
        {
            Vector2 c = regions[job.region].centre;
            Vector3 d = turn * (new Vector3(c.x, 0f, c.y) - me);
            d.y = 0f;
            if (d.magnitude > range) d = d.normalized * range;
            float pulse = 15f + Mathf.Sin(Time.unscaledTime * 5f) * 3f;
            UI.Tex(UI.Centered(mc.x + d.x / range * R, mc.y - d.z / range * R, pulse, pulse), UI.Circle, UI.Gold);
            UI.Tex(UI.Centered(mc.x + d.x / range * R, mc.y - d.z / range * R, pulse - 6f, pulse - 6f), UI.Circle, new Color(0.16f, 0.22f, 0.2f));
        }
        UI.Tex(UI.Centered(mc.x, mc.y, 10, 10), UI.Circle, Color.white);
    }

    // The whole of the Wilds on one sheet. Regions you have walked into are drawn and named;
    // the rest is blank paper with a question mark, waiting.
    void DrawMap(float w, float h, float cx)
    {
        const float half = 262f;
        float k = half / (WildsRadius + 8f);
        Vector2 mid = new Vector2(cx, h * 0.5f + 6f);
        Dim(w, h, 0.45f);
        UI.Framed(UI.Centered(mid.x, mid.y, half * 2 + 44, half * 2 + 170), new Color(0.078f, 0.082f, 0.1f, 0.97f), UI.Gold, 4f);
        UI.Text(new Rect(mid.x - half, mid.y - half - 36, half * 2, 30), "THE WILDS", 24, UI.Gold);
        UI.Tex(UI.Centered(mid.x, mid.y, half * 2, half * 2), UI.Circle, new Color(0.2f, 0.26f, 0.22f));

        Mission job = ActiveMission;
        for (int i = 0; i < regions.Length; i++)
        {
            Region r = regions[i];
            bool known = (Save.RegionMask & (1 << i)) != 0;
            Vector2 at = mid + new Vector2(r.centre.x, -r.centre.y) * k;
            float size = r.radius * 2f * k;
            Color land = known ? Color.Lerp(r.look.ground, Color.white, 0.08f) : new Color(0.3f, 0.3f, 0.36f);
            land.a = known ? 0.95f : 0.8f;
            if (job != null && job.region == i && !MissionReady)
                UI.Tex(UI.Centered(at.x, at.y, size + 12f, size + 12f), UI.Circle, UI.Gold);
            UI.Tex(UI.Centered(at.x, at.y, size, size), UI.Circle, land);
        }
        // The road, the two gates, and the lake.
        for (float z = -GateZ; z <= GateZ; z += 6f)
            UI.Tex(UI.Centered(mid.x + RoadX(z) * k, mid.y - z * k, 5, 5), UI.Circle, new Color(0.92f, 0.84f, 0.62f));
        if ((Save.RegionMask & (1 << RegionIndex("mirror-lake"))) != 0)
            UI.Tex(UI.Centered(mid.x + LakeCentre.x * k, mid.y - LakeCentre.y * k, LakeRadius * 2 * k, LakeRadius * 2 * k), UI.Circle, new Color(0.3f, 0.65f, 0.95f));
        for (int i = 0; i < regions.Length; i++)
        {
            Region r = regions[i];
            bool known = (Save.RegionMask & (1 << i)) != 0;
            Vector2 at = mid + new Vector2(r.centre.x, -r.centre.y) * k;
            UI.Text(UI.Centered(at.x, at.y, 190, 24), known ? r.name : "?", known ? 13 : 24, known ? Color.white : new Color(0.7f, 0.7f, 0.78f));
        }
        UI.Text(UI.Centered(mid.x + RoadX(-GateZ) * k, mid.y + GateZ * k + 12, 160, 20), "Brushwick", 13, UI.Blue);
        UI.Text(UI.Centered(mid.x + RoadX(GateZ) * k, mid.y - GateZ * k - 12, 160, 20), "The Gloom Tower", 13, UI.Purple);
        foreach (Spot s in spots)
            if (s.name == "Treasure") UI.Tex(UI.Centered(mid.x + s.pos.x * k, mid.y - s.pos.z * k, 7, 7), UI.Circle, UI.Gold);

        // You are here, with a tick for the way you are facing.
        Vector3 me = Player.transform.position, f = Player.transform.forward;
        Vector2 here = mid + new Vector2(me.x, -me.z) * k;
        UI.Tex(UI.Centered(here.x + f.x * 9f, here.y - f.z * 9f, 7, 7), UI.Circle, Color.white);
        UI.Tex(UI.Centered(here.x, here.y, 16, 16), UI.Circle, UI.Ink);
        UI.Tex(UI.Centered(here.x, here.y, 11, 11), UI.Circle, Color.white);
        UI.Text(new Rect(mid.x - half, mid.y + half + 8, half * 2, 42), ControlGuide.Key(ControlAction.Map) + " or B / Esc: close\nWhite: you     Gold: treasure and your commission", 13, new Color(0.85f, 0.85f, 0.95f), TextAnchor.MiddleCenter, false);
        if (UI.Button(UI.Centered(cx, h - 22, 200, 32), "CLOSE MAP", UI.Blue, 17)) SetMap(false);
    }
}
