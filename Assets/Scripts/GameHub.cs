using System;
using System.Collections.Generic;
using UnityEngine;

// Brushwick: the walkable home between climbs. It starts as a village and grows
// into a town, then a city, as the hero's best floor rises. The people here keep
// hours (GameLife.cs): the forge, the archive and the Guild are open when their
// keepers are at their posts. The north gate leads to the tower, the south gate
// to the Wilds, and the hero has a door of his own to rest behind.
public partial class Game
{
    const float HubRadius = 34f;

    static readonly string[] HubNames = { "BRUSHWICK VILLAGE", "BRUSHWICK TOWN", "BRUSHWICK CITY" };

    class Spot
    {
        public Vector3 pos;
        public string name, prompt;
        public Action act;
        public Resident who;        // the person this spot is: away when they have gone home
        public Transform owner;
    }

    readonly List<Spot> spots = new List<Spot>();
    Spot nearSpot;
    int hubPanel;                               // 0 none, 1 tower gate, 2 Guild hall, 3 a noble house's commissions, 4 rest at home
    GameState backTo = GameState.Menu;          // where Shrine / Echoes screens return to

    int HubTier => Save.BestFloor >= 20 ? 2 : Save.BestFloor >= 10 ? 1 : 0;

    // True whenever the hero can be walked around (fighting, or strolling in town).
    public bool Roaming => Player != null && JourneyStore.Error == null && startupError == null && !Player.Dead && !DefeatPending && (State == GameState.Playing || State == GameState.Hub) && hubPanel == 0 && !showMap && !Intro && Time.frameCount > resumeFrame;

    void EnterHub()
    {
        Transition(() =>
        {
            timers.Clear();
            numbers.Clear();
            Juice.Reset();
            ResetStyle();
            DefeatPending = false;
            InWilds = false;
            BuildHub();
            SnapResidents();
            sat = satTarget = 1f;
            Player.gameObject.SetActive(true);
            Player.NewRun();
            Player.Revive(new Vector3(0, 0.1f, -9f), true);
            Player.transform.rotation = Quaternion.identity;
            Cam.Face(0f);
            Cam.Snap();
            music.SetTrack(0);
            hubPanel = 0;
            showSettings = false;
            prologueDue = !Save.SeenIntro;
            SetState(GameState.Hub);
            float arrival = ArrivalTime(ref seenTown, 6f);
            if (VaneThanksDue) After(arrival + 1f, () => Toast("House Vane has sent for you. Lady Vane waits by her house.", UI.Gold));
            // Arrive over the rooftops, the tower behind them, and come down into the square.
            BeginIntro(HubNames[HubTier], "Day " + Save.Day + "  -  " + PhaseNames[PhaseOf(clockHour)] + ".  Home. Nobody draws a blade here.",
                new Vector3(30f, 21f, -40f), new Vector3(0f, 3.5f, 8f), arrival);
        });
    }

    // ------------------------------------------------------------ building the town

    void BuildHub()
    {
        TrainingActive = false;
        GameAudio.StopWorld();
        if (LevelRoot != null)
        {
            LevelRoot.gameObject.SetActive(false);
            Destroy(LevelRoot.gameObject);
        }
        LevelRoot = new GameObject("Hub").transform;
        ProductionRandom.BeginScene("town");
        PlaytestLog.Emit("scope_start", note: ProductionRandom.Scope);
        InTown = true;
        spots.Clear();
        packSpots.Clear();
        stairSpot = null;
        nearSpot = null;
        reserved.Clear();
        ClearResidents();
        LevelDef def = Levels[0];
        var rng = new System.Random(4242);
        int tier = HubTier;

        cam.backgroundColor = def.skyBottom;
        RenderSettings.fogColor = def.skyBottom;
        RenderSettings.ambientLight = def.ambient;
        sun.color = def.sun;
        BuildSky(def, rng);
        BuildMotes(def);

        var floor = new GameObject("Floor");
        floor.transform.SetParent(LevelRoot, false);
        var floorBox = floor.AddComponent<BoxCollider>();
        floorBox.size = new Vector3(240f, 2f, 240f);
        floorBox.center = new Vector3(0, -1f, 0);
        const int segments = 40;
        float segLen = 2f * Mathf.PI * HubRadius / segments + 0.5f;
        for (int i = 0; i < segments; i++)
        {
            float a = i * Mathf.PI * 2f / segments;
            var wall = new GameObject("Wall");
            wall.transform.SetParent(LevelRoot, false);
            wall.transform.position = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * (HubRadius + 0.5f) + Vector3.up * 5f;
            wall.transform.rotation = Quaternion.Euler(0, a * Mathf.Rad2Deg, 0);
            wall.AddComponent<BoxCollider>().size = new Vector3(segLen, 10f, 1f);
        }

        Disc(def.outer, 130f, -0.06f, 0f, 0f, def.outer * 0.88f);
        Disc(def.ground, HubRadius + 6f, -0.03f, 0f, 0f, def.patch);
        // The square, the road north to the tower gate and the road south to the Wilds: cobbled.
        Color stone = new Color(0.94f, 0.92f, 0.88f), worn = new Color(0.8f, 0.77f, 0.72f);
        const string cobbles = "T_UnevenBrick_BaseColor";
        Disc(stone, 9.4f, -0.012f, 0f, 0f, worn, cobbles);
        for (int i = 0; i < 8; i++) Disc(stone, 2.8f, -0.011f, 0f, 9f + i * 2.6f, worn, cobbles);
        for (int i = 0; i < 9; i++) Disc(stone, 2.5f, -0.011f, 0f, -9f - i * 2.6f, worn, cobbles);

        // Grass everywhere except the plaza and the two roads.
        Func<Vector2, bool> paved = p => p.magnitude < 9.6f || (Mathf.Abs(p.x) < 3.1f && Mathf.Abs(p.y) < 31f);
        BuildGrass(def, HubRadius + 6f, rng, 5400, paved);
        BuildFlowers(Moods[0].flowers, Vector2.zero, HubRadius + 6f, 900, rng, paved);

        for (int i = 0; i < 8; i++)
        {
            float la = (i + 0.5f) * Mathf.PI * 2f / 8f;
            Prop(new PropDef("LampPost", Kit, 0f, 0.25f), new Vector3(Mathf.Sin(la) * 10.6f, 0, Mathf.Cos(la) * 10.6f), rng, 1f, 0f);
        }
        Prop(new PropDef("Torch", Kit, 0f, 0.2f), new Vector3(-2.2f, 0, 25.5f), rng, 1f, 0f);
        Prop(new PropDef("Torch", Kit, 0f, 0.2f), new Vector3(2.2f, 0, 25.5f), rng, 1f, 0f);

        // Buildings, each put together from the village kit (Village.cs): what it is, angle from
        // north, distance from the well, and the tier of town it appears at.
        GameObject well = Village.Well(LevelRoot);
        Reserve(Vector3.zero, 1.6f);
        Village.Settle(well);
        Building(Home(4, 6, 2, 11), -58f, 17f, 0, "House Vane's lodging");
        Building(Home(6, 6, 2, 12, true), 58f, 17.5f, 0, "House Ashgrave's lodging");
        Building(Home(6, 8, 1, 13, true, true, 2), -112f, 19.5f, 0, "Smithy");
        Building(Home(4, 4, 2, 14), 112f, 16f, 0, "Archive");
        Vector3 homeDoor = Building(Home(4, 6, 2, 15), 160f, 21f, 0, "Home");
        Building(Home(6, 8, 2, 16, true), 140f, 25f, 1, "Market hall");
        Building(Home(8, 10, 2, 17, true, false, 2), -145f, 25.5f, 1, "Tavern");
        Landmark(() => Village.Windmill(LevelRoot, 18), 84f, 27f, 1);
        Building(Home(4, 8, 2, 19), -84f, 26f, 1, "House");
        Building(Home(6, 10, 1, 20, true), -170f, 28f, 1, "Lumber yard");
        Landmark(() => Village.Church(LevelRoot, 21), 37f, 31.5f, 2);
        Landmark(() => Village.Castle(LevelRoot, 23), -35f, 37.5f, 2);
        Building(Home(8, 10, 2, 26, true, true, 2), 118f, 29f, 2, "Barracks");
        Building(Home(6, 8, 2, 27), -122f, 29f, 2, "Mill house");
        Building(Tower(4, 28), 17f, 20f, 2, "Gate tower");
        Building(Tower(4, 29), -17f, 20f, 2, "Gate tower");

        // Trees and clutter round the edge.
        for (int i = 0; i < 26; i++)
        {
            float a = (i + (float)rng.NextDouble() * 0.6f) * Mathf.PI * 2f / 26f;
            float r = HubRadius + 2f + (float)rng.NextDouble() * 3f;
            if (Reserved(new Vector2(Mathf.Sin(a) * r, Mathf.Cos(a) * r), 1.5f)) continue;     // not through somebody's wall
            Prop(def.ring[i % 3], new Vector3(Mathf.Sin(a) * r, 0, Mathf.Cos(a) * r), rng, 1.2f);
        }
        // Where the townsfolk stand: nothing gets put down on their feet.
        float[,] standing =
        {
            { -104f, 12.5f }, { 104f, 12f }, { -30f, 10f }, { 132f, 13.5f }, { 38f, 12.5f }, { 46f, 13.6f }, { 146f, 13.5f }, { -166f, 22f },
            { -62f, 11.6f }, { -69f, 12.6f }, { 62f, 11.6f }, { -141f, 16.4f }, { -146f, 16.2f }, { -150f, 19.5f }, { 126f, 19.5f }, { 122f, 21.5f },
        };
        for (int i = 0; i < standing.GetLength(0); i++) Reserve(Polar(standing[i, 0], standing[i, 1]), 1.2f);
        for (int i = 0; i < RescuedSpots.GetLength(0); i++) Reserve(Polar(RescuedSpots[i, 0], RescuedSpots[i, 1]), 1.2f);
        Reserve(homeDoor, 2.2f);
        FurnishTown(tier);

        string[] clutter = { "Barrel", "Crate_Wooden", "Bag", "Barrel_Holder", "WeaponStand", "Bucket_Metal", "Crate_Metal", "Vase_2", "FarmCrate_Empty", "Pot_1_Lid" };
        for (int i = 0; i < 10 + tier * 4; i++)
        {
            Vector2 p = RandomInCircle(rng, 22f);
            if (p.magnitude < 9.8f || Mathf.Abs(p.x) < 3.6f) continue;      // the square and both roads stay clear
            if (Reserved(p, 1.6f)) continue;
            Prop(new PropDef(clutter[i % clutter.Length], Fur, 0f, 0.45f), new Vector3(p.x, 0, p.y), rng, 1f);
            Reserve(new Vector3(p.x, 0, p.y), 0.6f);
        }

        // Greenery between the houses: bushes in flower, ferns in the shade, the odd young tree.
        PropDef[] green =
        {
            new PropDef("Bush_Common_Flowers", Nat, 1.8f, 0.7f, BushSway), new PropDef("Flower_3_Group", Nat, 1.1f, 0f, BushSway),
            new PropDef("Bush_Common", Nat, 1.7f, 0.7f, BushSway), new PropDef("Flower_4_Group", Nat, 1.3f, 0f, BushSway),
            new PropDef("CommonTree_5", Nat, 6.5f, 0.4f, TreeSway), new PropDef("Plant_1", Nat, 0.9f, 0f, BushSway),
            new PropDef("Fern_1", Nat, 1.8f, 0f, BushSway), new PropDef("Rock_Medium_2", Nat, 1.6f, 0.7f),
            new PropDef("Flower_3_Single", Nat, 0.8f, 0f, BushSway), new PropDef("Plant_7_Big", Nat, 1.2f, 0f, BushSway),
            new PropDef("Flower_4_Single", Nat, 0.9f, 0f, BushSway), new PropDef("Clover_2", Nat, 0.6f, 0f, BushSway),
        };
        for (int i = 0, placed = 0; i < 120 && placed < 34; i++)
        {
            Vector2 p = RandomInCircle(rng, HubRadius - 1.5f);
            if (p.magnitude < 11.5f || Mathf.Abs(p.x) < 4f || Reserved(p, 1.4f)) continue;
            Prop(green[placed % green.Length], new Vector3(p.x, 0, p.y), rng, 1f);
            Reserve(new Vector3(p.x, 0, p.y), 0.8f);
            placed++;
        }

        // Stepping stones from the square to every door, and the small litter of a lived-in place.
        var paths = new GameObject("Paths").transform;
        paths.SetParent(LevelRoot, false);
        string[] flags = { "RockPath_Round_Small_1", "RockPath_Square_Small_1", "RockPath_Round_Small_2", "RockPath_Square_Small_2", "RockPath_Round_Small_3", "RockPath_Square_Small_3", "RockPath_Round_Thin", "RockPath_Square_Thin" };
        foreach (Vector3 door in doors)
        {
            Vector3 from = new Vector3(door.x, 0f, door.z).normalized * 9.8f, to = new Vector3(door.x, 0f, door.z);
            int steps = Mathf.Min(9, Mathf.FloorToInt((to - from).magnitude / 1.25f));
            for (int k = 1; k <= steps; k++)
            {
                Transform flag = ToonKit.SpawnNature(flags[rng.Next(flags.Length)], paths);
                if (flag == null) continue;
                Vector3 side = Vector3.Cross(Vector3.up, (to - from).normalized) * ((float)rng.NextDouble() - 0.5f) * 0.5f;
                flag.position = Vector3.Lerp(from, to, k / (float)(steps + 1)) + side + Vector3.up * 0.01f;
                flag.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f) * flag.rotation;
                flag.localScale *= 0.8f;
            }
        }
        if (Application.isPlaying) GeneratedResources.Combine(paths);
        Scatter(rng, Vector2.zero, HubRadius - 1f, 150, Color.white, p => paved(p) || Reserved(p, 0.2f));

        // The tower and its gate, due north.
        GameObject tower = Village.House(GloomTower(10), LevelRoot, "The Gloom Tower");
        tower.transform.SetPositionAndRotation(new Vector3(0f, 0f, 41f), Quaternion.Euler(0f, 180f, 0f));
        Village.Settle(tower);
        Prop(new PropDef("Pillar", Kit, 7f, 0.7f), new Vector3(-3.4f, 0, 27f), rng, 1f, 0f);
        Prop(new PropDef("Pillar", Kit, 7f, 0.7f), new Vector3(3.4f, 0, 27f), rng, 1f, 0f);
        // The Guild's colours hang on the gate.
        Hang("Banner_1_Cloth", new Vector3(-3.4f, 5.6f, 26.3f), 180f, 1.3f);
        Hang("Banner_2_Cloth", new Vector3(3.4f, 5.6f, 26.3f), 180f, 1.3f);
        Transform core = ToonKit.Spawn("Crystal", LevelRoot, 0.9f);
        core.position = new Vector3(0, 5.2f, 27f);
        core.gameObject.AddComponent<Spin>();
        AddSpot(new Vector3(0, 0, 24.5f), "The Gloom Tower", "Enter the tower", () => OpenHubPanel(1));

        // The south gate opens onto the Wilds.
        Prop(new PropDef("Pillar", Kit, 6f, 0.7f), new Vector3(-3.4f, 0, -30f), rng, 1f, 0f);
        Prop(new PropDef("Pillar", Kit, 6f, 0.7f), new Vector3(3.4f, 0, -30f), rng, 1f, 0f);
        Prop(new PropDef("Banner", Kit, 0f, 0.2f), new Vector3(5.2f, 0, -29f), rng, 1f, 0f);
        AddSpot(new Vector3(0, 0, -29f), "The Wilds", "Go out into the Wilds", () =>
        {
            if (Save.BestFloor < 1) Banner("NOT YET", 2.5f, "The Guild lets nobody through the south gate who has not cleared Floor 1.");
            else EnterWilds();
        });

        // The hero's own door.
        AddSpot(homeDoor, "Home", "Rest: sleep until morning, or pass the afternoon", () => OpenHubPanel(4));

        // The people whose work the hero needs, and the hours they keep.
        Person(Looks.Smith, "Idle_FoldArms_Loop", -104f, 12.5f, 6f, 21f, "Bram the Smith",
            "Forge: spend Sparks on lasting strength", () => { backTo = GameState.Hub; SetState(GameState.Shrine); });
        Person(Looks.Archivist, "Idle_Loop", 104f, 12f, 8f, 20f, "Sol the Archivist",
            "Archive: read recovered Echoes", () => { backTo = GameState.Hub; SetState(GameState.Codex); });
        Person(Looks.Guildmaster, "Idle_Talking_Loop", -30f, 10f, 6f, 24f, GuildDesk,
            "Guild: commissions and heroes", () => OpenBoard(GuildDesk));

        // The noble houses keep people in Brushwick: they sponsor Painters, and post work of their own.
        Formal(Person(Looks.LadyVane, "Idle_FoldArms_Loop", -62f, 11.6f, 9f, 19f, HouseVane,
            "House Vane: a noble's commissions", () => VaneGreeting(() => OpenBoard(HouseVane))));
        Person(Looks.Retainer, "Idle_Loop", -69f, 12.6f, 9f, 19f, null, null, null, "Sword_Bronze");
        Formal(Person(Looks.LordAshgrave, "Idle_Talking_Loop", 62f, 11.6f, 10f, 22f, HouseAshgrave,
            "House Ashgrave: a noble's wagers", () => OpenBoard(HouseAshgrave)));
        if (tier >= 1)
        {
            // The gate guard never stands down.
            Person(Looks.Guard, "Idle_Loop", 9f, 22f, 0f, 24f, null, null, null, "Sword_Bronze");
            Person(Looks.Guard, "Idle_Loop", -9f, 22f, 0f, 24f, null, null, null, "Sword_Bronze");
        }

        // Once Grol the Gatekeeper has fallen, his gate's bars hang by the tower door, and the gate
        // sergeant who has watched that door for forty years has something to say about it.
        if (Save.TowerFloor >= ChapterProgress.GatekeeperFloor)
        {
            Vector3 trophy = new Vector3(Mathf.Sin(18f * Mathf.Deg2Rad), 0f, Mathf.Cos(18f * Mathf.Deg2Rad)) * 23f;
            Vector3 along = Vector3.Cross(Vector3.up, trophy.normalized);
            var bars = new GameObject("Gatekeeper's bars").transform;
            bars.SetParent(LevelRoot, false);
            Village.FenceLine(bars, trophy - along * 1.4f, trophy + along * 1.4f, true);
            Prop(new PropDef("Torch", Kit, 0f, 0.2f), trophy - along * 2.1f, new System.Random(5), 1f, Mathf.Atan2(-trophy.x, -trophy.z) * Mathf.Rad2Deg);
            Prop(new PropDef("Torch", Kit, 0f, 0.2f), trophy + along * 2.1f, new System.Random(6), 1f, Mathf.Atan2(-trophy.x, -trophy.z) * Mathf.Rad2Deg);
            Person(Looks.Guard, "Idle_Talking_Loop", 13f, 21f, 7f, 21f, "Gate Sergeant Hale",
                "Talk", () => Say(Story.GatekeeperAftermath(), () => SetState(GameState.Hub)), "Sword_Bronze");
        }

        // Painters the hero has brought back out of the tower: home again, and not forgetting it.
        for (int i = 0; i < Story.Rescues.Length; i++)
        {
            if ((Save.RescuedMask & (1 << i)) == 0) continue;
            Lost lost = Story.Rescues[i];
            Person(lost.look, i % 2 == 0 ? "Idle_Loop" : "Idle_FoldArms_Loop", RescuedSpots[i, 0], RescuedSpots[i, 1], 8f, 21f, lost.name,
                "Talk", () => Say(Story.RescueTown(Story.RescueIndex(lost.id)), () => SetState(GameState.Hub)), "Sword_Bronze");
        }

        // Ordinary villagers going about their day.
        Person(Looks.VillagerWoman, "Farm_Watering", 132f, 13.5f, 7f, 17f, null, null, null);
        Person(Looks.Villager, "Idle_Talking_Loop", 38f, 12.5f, 9f, 19f, null, null, null);
        Person(Looks.VillagerWoman, "Idle_FoldArms_Loop", 46f, 13.6f, 9f, 19f, null, null, null);
        Person(Looks.Farmer, "Idle_Loop", 146f, 13.5f, 7f, 18f, null, null, null);       // minding the market stall
        Person(Looks.Farmer, "Farm_Harvest", 126f, 19.5f, 6.5f, 12f, null, null, null);  // the vegetable patch, mornings
        Person(Looks.Maid, "Farm_PlantSeed", 122f, 21.5f, 13f, 17.5f, null, null, null); // and afternoons
        if (tier >= 1)
        {
            Person(Looks.Villager, "TreeChopping_Loop", -166f, 22f, 8f, 17f, null, null, null, "Axe_Bronze");
            // The day's work done, the tavern table fills.
            Person(Looks.Farmer, "Idle_Talking_Loop", -141f, 16.4f, 17.5f, 23.5f, null, null, null);
            Person(Looks.Maid, "Idle_FoldArms_Loop", -146f, 16.2f, 18f, 23f, null, null, null);
            Person(Looks.VillagerWoman, "Dance_Loop", -150f, 19.5f, 19f, 23.5f, null, null, null);  // somebody always ends up dancing
        }

        // Two on the benches at midday, taking the weight off; a third later on.
        Seated(Looks.Elder, "Sitting_Idle_Loop", 78f, -0.55f, 10f, 17f);
        Seated(Looks.Maid, "Sitting_Talking_Loop", 78f, 0.55f, 11f, 16.5f);
        Seated(Looks.Farmer, "Sitting_Idle_Loop", -78f, 0f, 13f, 20f);

        // And people on their feet: round the well, across the square, up and down the tower road.
        Stroll(Looks.Maid, 1.3f, "Idle_Loop", 7f, 19f, Ring(6.2f, 6, 40f, true));
        Stroll(Looks.Elder, 1.05f, "Idle_FoldArms_Loop", 8f, 17f, Ring(7.6f, 7, 200f, false));
        Stroll(Looks.Traveller, 1.45f, "Idle_Loop", 9f, 21f,
            new Vector3(1.7f, 0f, 9.6f), new Vector3(1.7f, 0f, 20.6f), new Vector3(-1.7f, 0f, 20.6f), new Vector3(-1.7f, 0f, 9.6f));
        if (tier >= 1) Stroll(Looks.VillagerWoman, 1.25f, "Idle_Talking_Loop", 10f, 20f, Ring(4.6f, 5, 130f, false));
        if (tier >= 2) Stroll(Looks.Villager, 1.35f, "Idle_Loop", 8f, 22f, Ring(8.6f, 8, 300f, true));

        // The night watch: a lantern going round the square while everyone else is abed.
        Watchman(Stroll(Looks.Guard, 1.15f, "Idle_Lantern_Loop", 20f, 6f, Ring(11.8f, 10, 0f, true)));
        if (tier >= 1)
            Watchman(Stroll(Looks.Warden, 1.1f, "Idle_Lantern_Loop", 21f, 5.5f,
                new Vector3(-1.8f, 0f, -12f), new Vector3(-1.8f, 0f, -27f), new Vector3(1.8f, 0f, -27f), new Vector3(1.8f, 0f, -12f)));
        BuildPilotHub();
        DressTownServices();
        NavField.BakeStaged(LevelRoot, HubRadius, Player.GetComponent<CharacterController>());
    }

    // Where the rescued Painters stand once they are home: angle from north, distance from the well.
    static readonly float[,] RescuedSpots = { { -46f, 13.4f }, { 20f, 12.5f }, { 71f, 13.6f }, { -18f, 12.8f }, { -8f, 16.5f } };

    // ------------------------------------------------------------ furniture and townsfolk

    // Spots the random clutter and greenery must leave alone: x, z and how much room each takes.
    readonly List<Vector3> reserved = new List<Vector3>();

    void Reserve(Vector3 pos, float room) => reserved.Add(new Vector3(pos.x, pos.z, room));

    bool Reserved(Vector2 p, float clearance)
    {
        foreach (Vector3 r in reserved)
            if ((new Vector2(r.x, r.y) - p).magnitude < r.z + clearance) return true;
        return false;
    }

    static Vector3 Polar(float angle, float distance)
    {
        float a = angle * Mathf.Deg2Rad;
        return new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * distance;
    }

    // Waypoints on a circle round the well, for people walking the square.
    static Vector3[] Ring(float radius, int points, float startAngle, bool clockwise)
    {
        var route = new Vector3[points];
        for (int i = 0; i < points; i++)
            route[i] = Polar(startAngle + (clockwise ? i : -i) * 360f / points, radius);
        return route;
    }

    // A life-sized prop from the furniture kit. turn 0 faces the well.
    GameObject Furnish(string prop, float angle, float distance, float turn = 0f, float radius = 0f)
    {
        GameObject go = Humans.Furniture(prop);
        if (go == null) return null;
        Vector3 pos = Polar(angle, distance);
        go.transform.SetParent(LevelRoot, false);
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(0f, angle + 180f + turn, 0f) * go.transform.rotation;
        Reserve(pos, Mathf.Max(0.5f, radius));
        if (radius > 0f) Capsule(go, radius / go.transform.lossyScale.x, 3f / go.transform.lossyScale.x, 1.5f / go.transform.lossyScale.x);
        return go;
    }

    // What makes the square a place people use: benches, a market laid out for the day, the
    // smith's yard, the archivist's books, and a table under an awning outside the tavern.
    void FurnishTown(int tier)
    {
        Furnish("Bench", 78f, 9.4f, 0f, 0.5f);
        Furnish("Bench", -78f, 9.4f, 0f, 0.5f);

        // The market: a stall and a cart, with the day's goods on and round them.
        Furnish("Stall_Empty", 146f, 12.3f, 0f, 0.9f);
        Furnish("Barrel_Apples", 139f, 12.1f, 20f, 0.4f);
        Furnish("FarmCrate_Apple", 151.5f, 11.7f, -15f);
        Furnish("FarmCrate_Carrot", 153f, 12.5f, 30f);
        Furnish("Cage_Small", 156f, 13.4f, 15f, 0.4f);
        Dress(Polar(146f, 12.3f), 146f + 180f,
            ("Pot_1", -0.55f, 0.95f, 0.05f, 0f), ("Vase_4", -0.15f, 0.95f, -0.05f, 30f), ("Potion_2", 0.15f, 0.95f, 0.1f, 0f),
            ("Potion_1", 0.3f, 0.95f, -0.05f, 0f), ("Potion_4", 0.45f, 0.95f, 0.1f, 0f), ("SmallBottles_1", 0.65f, 0.95f, 0f, 20f),
            ("SmallBottle", 0.02f, 0.95f, 0.18f, 0f), ("Carrot", -0.35f, 0.97f, 0.2f, 70f), ("Pouch_Large", 0.78f, 0.95f, 0.18f, 0f),
            ("Coin_Pile", 0.55f, 0.95f, 0.22f, 0f), ("Coin_Pile_2", -0.7f, 0.95f, 0.24f, 0f), ("Coin", 0.42f, 0.98f, 0.26f, 0f),
            ("Key_Gold", -0.02f, 0.96f, 0.28f, 40f), ("Key_Metal", 0.1f, 0.96f, 0.28f, -30f));
        Furnish("Stall_Cart_Empty", -146f, 12.6f, 0f, 1.1f);
        Furnish("Crate_Wooden", -137f, 12.4f, 25f, 0.6f);
        Furnish("Bag", -153f, 12.2f, -30f, 0.35f);

        // The smith's yard.
        Furnish("Anvil_Log", -111f, 13.6f, 90f, 0.5f);
        Furnish("Anvil", -116f, 12.6f, 40f, 0.45f);
        Furnish("WeaponStand", -96f, 14.2f, 0f, 0.6f);
        Furnish("Workbench", -119f, 15.4f, 20f, 0.9f);
        Furnish("Whetstone", -100f, 15.6f, 30f, 0.55f);
        Furnish("Cauldron", -123f, 13.4f, 0f, 0.5f);
        Furnish("Crate_Metal", -126f, 15.2f, 10f, 0.5f);
        Furnish("Bucket_Wooden_1", -108f, 14.8f, 0f);
        Furnish("Bucket_Metal", -113f, 15.1f, 0f);
        Furnish("Chain_Coil", -105f, 15.4f, 0f);
        Furnish("Rope_2", -121f, 12.6f, 0f);
        Dress(Polar(-119f, 15.4f), -119f + 200f, ("Pickaxe_Bronze", -0.4f, 0.93f, 0f, 90f), ("Torch_Metal", 0.5f, 1.0f, 0.1f, 0f), ("Rope_3", 0.1f, 0.9f, -0.1f, 0f));

        // The archivist reads out of doors while the light lasts.
        Furnish("Bookcase_2", 97f, 14.3f, 0f, 0.6f);
        Furnish("Cabinet", 110f, 13.9f, 0f, 0.6f);
        Furnish("BookStand", 100f, 11.2f, 20f, 0.3f);
        Furnish("Nightstand_Shelf", 114f, 14.4f, 0f, 0.35f);
        Furnish("CandleStick_Stand", 94f, 12.4f, 0f, 0.3f);
        Furnish("Chair_1", 107f, 12.9f, 180f, 0.3f);
        Dress(Polar(110f, 13.9f), 110f + 180f,
            ("Book_Stack_1", -0.45f, 1f, 0f, 20f), ("Book_Stack_2", 0.42f, 1f, 0.02f, -15f), ("BookGroup_Small_1", -0.1f, 1f, -0.02f, 0f),
            ("Scroll_1", 0.12f, 1f, 0.06f, 30f), ("Scroll_2", 0.2f, 1f, -0.06f, -20f), ("CandleStick", -0.25f, 1f, 0.08f, 0f),
            ("Book_5", 0.05f, 1.03f, -0.08f, 50f), ("Candle_2", 0.6f, 1f, -0.05f, 0f));
        Dress(Polar(97f, 14.3f), 97f + 180f,
            ("BookGroup_Medium_1", 0f, 0.42f, 0f, 0f), ("BookGroup_Medium_2", 0f, 0.92f, 0f, 0f), ("BookGroup_Medium_3", 0f, 1.42f, 0f, 0f),
            ("BookGroup_Small_2", -0.3f, 1.92f, 0f, 0f), ("BookGroup_Small_3", 0.3f, 1.92f, 0f, 0f), ("Book_7", 0f, 1.95f, 0f, 90f),
            ("Book_Simplified_Single", 0.1f, 0.42f, 0.1f, 0f), ("Candle_1", 0f, 2.55f, 0f, 0f));

        if (tier < 1) return;
        // The tavern's table, under a shingled awning with a chandelier, laid for supper.
        Vector3 table = Polar(-143f, 18.4f);
        Prop(new PropDef("Pavilion", Kit, 0f, 0f), table, null, 1f, -143f + 180f);
        Furnish("Table_Large", -143f, 18.4f, 0f, 1f);
        Furnish("Stool", -139f, 17.2f, 0f, 0.25f);
        Furnish("Stool", -147f, 17.2f, 0f, 0.25f);
        Furnish("Chair_1", -140f, 19.6f, 180f, 0.3f);
        Furnish("Chair_1", -146f, 19.6f, 180f, 0.3f);
        Furnish("Barrel", -136f, 19.6f, 0f, 0.4f);
        Furnish("Barrel_Holder", -150.5f, 17.6f, 20f, 0.6f);
        Dress(table, -143f + 180f,
            ("Mug", -0.9f, 0.82f, 0.2f, 0f), ("Mug", 0.1f, 0.82f, -0.25f, 140f), ("Mug", 0.85f, 0.82f, 0.22f, 60f),
            ("Table_Plate", -0.6f, 0.82f, -0.2f, 0f), ("Table_Plate", 0.5f, 0.82f, -0.22f, 0f), ("Table_Plate", -0.1f, 0.82f, 0.25f, 0f),
            ("Table_Fork", -0.82f, 0.83f, -0.2f, 0f), ("Table_Knife", -0.38f, 0.83f, -0.2f, 0f), ("Table_Spoon", 0.3f, 0.84f, -0.22f, 0f),
            ("Chalice", 1.05f, 0.82f, -0.15f, 0f), ("Bottle_1", 0.3f, 0.82f, 0.18f, 0f), ("CandleStick_Triple", 0f, 0.82f, 0f, 90f),
            ("Pot_1_Lid", -1.15f, 0.82f, 0f, 0f));

        // Practice dummies by the tower road for the guard, and a fenced plot for the vegetables.
        Furnish("Dummy", 27f, 17f, 0f, 0.35f);
        Furnish("Dummy", 31f, 19.5f, 0f, 0.35f);
        var plot = new GameObject("Plot").transform;
        plot.SetParent(LevelRoot, false);
        Vector3 c0 = Polar(124f, 20.5f), along = Polar(124f + 90f, 1f), back = Polar(124f, 1f);
        Village.FenceLine(plot, c0 - along * 3f + back * 2.4f, c0 + along * 3f + back * 2.4f);
        Village.FenceLine(plot, c0 - along * 3f - back * 2.2f, c0 - along * 3f + back * 2.4f);
        Village.FenceLine(plot, c0 + along * 3f - back * 2.2f, c0 + along * 3f + back * 2.4f);
        Prop(new PropDef("Prop_Wagon", Kit, 0f, 1.2f), Polar(133f, 19f), null, 1f, 30f);
    }

    // Builds one of the townsfolk standing at a spot: a body, its animation, and a head that
    // turns to watch the hero go by.
    GameObject Townsperson(HumanLook look, string name, Vector3 pos, Quaternion facing, out CharAnim clips)
    {
        var go = new GameObject(name);
        go.transform.SetParent(LevelRoot, false);
        go.transform.SetPositionAndRotation(pos, facing);
        clips = null;
        Transform body = Humans.Build(look, go.transform);
        if (body == null) return go;
        clips = CharAnim.Attach(go, body);
        go.AddComponent<HeadLook>();
        return go;
    }

    // Someone sitting on one of the benches for part of the day. side slides them along it.
    void Seated(HumanLook look, string clip, float benchAngle, float side, float from, float to)
    {
        Vector3 bench = Polar(benchAngle, 9.4f);
        Quaternion facing = Quaternion.Euler(0f, benchAngle + 180f, 0f);
        GameObject go = Townsperson(look, "Seated", bench + facing * new Vector3(side, 0.05f, 0.02f), facing, out CharAnim clips);
        Capsule(go, 0.35f, 1.3f, 0.65f);
        if (clips == null) return;
        var fidget = go.AddComponent<Fidget>();
        fidget.clips = clips;
        fidget.idle = clip;
        fidget.gestures = false;
        Settle(go, clips, from, to, false, fidget);
    }

    // Someone walking a route round town between two hours of the day.
    Resident Stroll(HumanLook look, float pace, string rest, float from, float to, params Vector3[] route)
    {
        Vector3 start = route[0] + Vector3.up * 0.05f;
        Quaternion facing = Quaternion.LookRotation(route[1 % route.Length] - route[0]);
        GameObject go = Townsperson(look, "Stroller", start, facing, out CharAnim clips);
        Capsule(go, 0.35f, 1.8f, 0.9f);
        if (clips == null) return null;
        var stroller = go.AddComponent<Stroller>();
        stroller.clips = clips;
        stroller.route = route;
        stroller.pace = pace;
        stroller.rest = rest;
        return Settle(go, clips, from, to, true, stroller);
    }

    // The nobles do not stroll home. They process.
    static Resident Formal(Resident r)
    {
        if (r != null && r.clips != null && r.clips.Has("Walk_Formal_Loop")) r.clips.walkClip = "Walk_Formal_Loop";
        return r;
    }

    // Arms a stroller as one of the watch: a sword at the hip side, and a lantern's light going with them.
    void Watchman(Resident r)
    {
        if (r == null) return;
        Humans.Hold(r.model, "Sword_Bronze", false, Humans.Grip, Humans.GripAngles);
        Glow lamp = AddGlow(r.transform, 1.15f, new Color(1f, 0.7f, 0.32f), 3.4f);
        lamp.transform.localPosition += Vector3.right * 0.35f;
        r.extras = new[] { lamp.gameObject };
    }

    // A house plan: footprint, storeys, a seed for its windows and trim, whether the door is in
    // a long wall, whether it is stone all the way up, and how many chimneys.
    // Every other two-storey house has a balcony over its door; the wide ones have dormers.
    static Village.Plan Home(int w, int d, int floors, int seed, bool sideDoor = false, bool stone = false, int chimneys = 1) => new Village.Plan
    {
        w = w, d = d, floors = floors, seed = seed, sideDoor = sideDoor, stoneAll = stone, chimneys = chimneys,
        balcony = floors >= 2 && !stone && seed % 2 == 1, dormers = w >= 6,
    };

    static Village.Plan Tower(int floors, int seed) =>
        new Village.Plan { w = 4, d = 4, floors = floors, seed = seed, tower = true, stoneAll = true, chimneys = 0, vines = false };

    // The Gloom Tower: the same stone as any tower, eight metres square, drained and dark, with no light in any window.
    static Village.Plan GloomTower(int floors) => new Village.Plan
    {
        w = 8, d = 8, floors = floors, seed = 30, tower = true, stoneAll = true, chimneys = 0, vines = false,
        roofScale = 1.9f, dark = true, tint = new Color(0.46f, 0.45f, 0.62f),
    };

    // Puts up a building facing the plaza. Returns where its front door lets out.
    Vector3 Building(Village.Plan plan, float angle, float distance, int minTier, string name)
    {
        Vector3 pos = Polar(angle, distance);
        Vector3 front = -pos.normalized, right = Vector3.Cross(Vector3.up, front);
        float depth = Village.FrontDepth(plan);
        Vector3 door = pos + front * (depth + 0.9f) + right * Village.DoorOffset(plan) + Vector3.up * 0.05f;
        if (HubTier < minTier) return door;
        GameObject house = Village.House(plan, LevelRoot, name);
        house.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(front));
        Village.Settle(house);
        Reserve(pos, Mathf.Max(Village.FrontWidth(plan) * 0.5f, depth) + 1.2f);
        doors.Add(door);
        if (plan.tower) return door;
        Vector3 wall = pos + front * (depth + 0.14f) + right * Village.DoorOffset(plan);
        DoorLamp(wall + right * 1.05f + Vector3.up * 2.05f, front);
        // Something on the wall the other side of the door: pegs, a shelf, bottles; and a pot or two on the step.
        string[] fixtures = { "Peg_Rack", "Shelf_Simple", "Shelf_Small_Bottles", "Shelf_Arch" };
        string[] pots = { "Vase_4", "Pot_1", "Vase_2", "Bucket_Wooden_1", "FarmCrate_Empty" };
        if (Village.FrontWidth(plan) >= 6f) Hang(fixtures[plan.seed % fixtures.Length], wall - right * 2.1f + Vector3.up * 1.5f, Mathf.Atan2(front.x, front.z) * Mathf.Rad2Deg, 1f);
        Hang(pots[plan.seed % pots.Length], wall + right * 1.5f + front * 0.45f, plan.seed * 47f, 1f);
        return door;
    }

    // Stands one of the great works of the town (the mill, the church, the castle) facing the square.
    void Landmark(Func<Village.Site> build, float angle, float distance, int minTier)
    {
        if (HubTier < minTier) return;
        Vector3 pos = Polar(angle, distance);
        Vector3 front = -pos.normalized, right = Vector3.Cross(Vector3.up, front);
        Village.Site site = build();
        site.root.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(front));
        Village.Settle(site.root);
        Reserve(pos, site.reach);
        doors.Add(pos + front * (site.depth + 0.9f) + right * site.door + Vector3.up * 0.05f);
    }

    // Puts a prop exactly somewhere: on a wall, on a table, on a step.
    GameObject Hang(string prop, Vector3 at, float yaw, float scale)
    {
        GameObject go = Humans.Furniture(prop);
        if (go == null) return null;
        go.transform.SetParent(LevelRoot, false);
        go.transform.position = at;
        go.transform.rotation = Quaternion.Euler(0f, yaw, 0f) * go.transform.rotation;
        go.transform.localScale *= scale;
        return go;
    }

    // Lays things out on and around a piece of furniture: each is a prop, and where it sits
    // (across, up, forward) measured from the furniture's own middle.
    void Dress(Vector3 centre, float yaw, params (string prop, float x, float y, float z, float turn)[] things)
    {
        Quaternion facing = Quaternion.Euler(0f, yaw, 0f);
        foreach (var t in things) Hang(t.prop, centre + facing * new Vector3(t.x, t.y, t.z), yaw + t.turn, 1f);
    }

    // A lantern on the wall beside a door: lit, like every lamp in town, once the light goes.
    void DoorLamp(Vector3 at, Vector3 facing)
    {
        GameObject lamp = Humans.Furniture("Lantern_Wall");
        if (lamp == null) return;
        lamp.transform.SetParent(LevelRoot, false);
        lamp.transform.position = at;
        lamp.transform.rotation = Quaternion.LookRotation(facing) * lamp.transform.rotation;
        AddGlow(lamp.transform, 0f, new Color(1f, 0.72f, 0.36f), 1.5f / Mathf.Max(0.01f, lamp.transform.lossyScale.x));
    }

    // One of Brushwick's people: a realistic human with an idle of their own, at their post
    // between two hours of the day. With an action they are someone to talk to; without, they
    // are simply part of the town.
    Resident Person(HumanLook look, string idle, float angle, float distance, float from, float to, string name, string prompt, Action act, string prop = null)
    {
        Vector3 pos = Polar(angle, distance) + Vector3.up * 0.05f;
        // Everyone faces the square, except the gate guards, who watch the road.
        Quaternion facing = Quaternion.LookRotation(new Vector3(-pos.x, 0, -pos.z + (prop == "Sword_Bronze" && look == Looks.Guard ? 60f : 0f)));
        GameObject go = Townsperson(look, name ?? "Villager", pos, facing, out CharAnim clips);
        Reserve(pos, 0.6f);
        Capsule(go, 0.4f, 1.8f, 0.9f);
        Resident res = null;
        if (clips != null)
        {
            var fidget = go.AddComponent<Fidget>();
            fidget.clips = clips;
            fidget.idle = idle;
            fidget.gestures = idle.StartsWith("Idle");
            if (prop != null) Humans.Hold(go.transform.GetChild(0), prop, false, Humans.Grip, Humans.GripAngles);
            res = Settle(go, clips, from, to, false, fidget);
        }
        if (act == null) return res;

        Spot spot = AddSpot(pos, name, prompt, () =>
        {
            if (res != null && !res.AtPost)
            {
                Banner(name.ToUpper() + " IS NOT HERE", 2.4f, "Back at " + ClockText(res.from) + ". There is a bed at home if you would rather not wait.");
                ToonKit.Sfx("deny");
                return;
            }
            // The camera frames whoever is being spoken to. The first conversation is an
            // introduction; after that they get straight to business.
            shotSubject = go.transform;
            Line[] intro = Save.Met(name) ? null : Story.Meet(name);
            Save.SetMet(name);
            if (intro == null)
            {
                act();
                return;
            }
            Say(intro, () =>
            {
                SetState(GameState.Hub);
                act();
            });
        });
        spot.who = res;
        return res;
    }

    Spot AddSpot(Vector3 pos, string name, string prompt, Action act, Transform owner = null)
    {
        var spot = new Spot { pos = pos, name = name, prompt = prompt, act = act, owner = owner };
        spots.Add(spot);
        return spot;
    }

    void OpenHubPanel(int panel)
    {
        if (panel != hubPanel && panel != 0) PlaytestLog.Emit("hub_panel", value: panel);
        hubPanel = panel;
        heroView = Heroes.Selected;
        Cursor.lockState = panel == 0 ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = panel != 0;
    }

    // ------------------------------------------------------------ per-frame and UI

    void TickHub()
    {
        if (fadeDir != 0f || Intro) return;
        if (prologueDue)
        {
            // A new journey opens with how things stand, told over the square.
            prologueDue = false;
            Say(Story.Prologue, () =>
            {
                Save.SeenIntro = true;
                Save.Flush();
                SetState(GameState.Hub);
            });
            return;
        }
        if (ResumeRewards()) return;
        TickBarks(GameClocks.WorldDelta);
        if (hubPanel != 0) return;

        nearSpot = null;
        float best = 4f;
        foreach (Spot s in spots)
        {
            float d = (s.pos - Player.transform.position).magnitude;
            if (d < best && WorldSupport.CanInteract(Player.transform.position, s.pos, targetOwner: s.owner)) { best = d; nearSpot = s; }
        }
        if (nearSpot != null && Time.frameCount > resumeFrame && GameInput.InteractPressed)
        {
            ToonKit.Sfx("click");
            SpotGesture(nearSpot);
            nearSpot.act();
        }
    }

    void DrawHub(float w, float h, float cx)
    {
        // Name tags over the people and places you can use (not while a board is open over them).
        foreach (Spot s in spots)
        {
            if (hubPanel != 0 || !ToGui(s.pos + Vector3.up * 2.6f, out Vector2 p)) continue;
            float d = (s.pos - Player.transform.position).magnitude;
            if (d > 26f) continue;
            bool away = s.who != null && !s.who.AtPost;
            if (away && d > 7f) continue;       // an empty post says whose it is only to someone standing at it
            if (away) UI.Text(UI.Centered(p.x, p.y, 420, 30), s.name + "   (back " + ClockText(s.who.from) + ")", 15, new Color(0.72f, 0.72f, 0.8f));
            else UI.Text(UI.Centered(p.x, p.y, 320, 30), s.name, 18, s == nearSpot ? UI.Gold : Color.white);
        }
        if (hubPanel == 0) DrawBarks();

        if (hubPanel == 0) UI.Text(new Rect(0, 12, w, 28), HubNames[HubTier], 20, Color.white);
        DrawLevel(26, 16);
        UI.Tex(UI.Centered(34, 78, 17, 17), UI.Circle, UI.Ink);
        UI.Tex(UI.Centered(34, 78, 13, 13), UI.Circle, UI.Gold);
        UI.Text(new Rect(48, 63, 200, 30), Save.Sparks + "  Sparks", 17, UI.Gold, TextAnchor.MiddleLeft);
        if (hubPanel == 0 && CanForge) UI.Text(new Rect(150, 66, 420, 24), "enough for an upgrade at Bram's forge", 14, UI.Green, TextAnchor.MiddleLeft, false);
        DrawClock(w, 12);
        UI.Text(new Rect(26, h - 36, 760, 24), ControlGuide.Key(ControlAction.Dash) + " run    " + ControlGuide.Key(ControlAction.Block) + " walk    " + ControlGuide.Key(ControlAction.Interact) + " talk    " + ControlGuide.Key(ControlAction.Status) + " status    " + ControlGuide.Key(ControlAction.Pause) + " menu", 15, new Color(0.85f, 0.85f, 0.95f), TextAnchor.MiddleLeft, false);
        if (hubPanel == 0)
        {
            DrawObjective(26, 100);
            bool next = NextStep() != null;
            if (next) DrawNextStep(26, 124);
            float below = next ? 152 : 132;
            DrawMission(26, below);
            DrawDaily(26, ActiveMission != null ? below + 90 : below);
            DrawSystem(w, cx);
        }

        if (bannerT > 0f) DrawBanner(w, 150f, 54);

        if (hubPanel == 0 && nearSpot != null) DrawPrompt(h, cx, nearSpot.prompt);

        if (hubPanel == 1) DrawGatePanel(w, h, cx);
        else if (hubPanel == 2) DrawGuild(w, h, cx);
        else if (hubPanel == 3) DrawHouse(w, h, cx);
        else if (hubPanel == 4) DrawRest(w, h, cx);
        else if (hubPanel == 5) DrawMastery(w, h, cx);
    }

    // What the story wants next, in one line under the level bar.
    void DrawObjective(float x, float y)
    {
        string text = Objective();
        UI.Fill(new Rect(x, y + 4, 4, 18), UI.Gold);
        UI.Text(new Rect(x + 12, y, 560, 26), text, 15, new Color(1f, 0.95f, 0.75f), TextAnchor.MiddleLeft);
    }

    // The tower door. There is one way in, and it starts where the last climb ended.
    void DrawGatePanel(float w, float h, float cx)
    {
        Dim(w, h, 0.6f);
        int cleared = Mathf.Clamp(Save.TowerFloor, 0, TowerFloors);
        int next = Mathf.Min(cleared + 1, TowerFloors);
        bool done = cleared >= TowerFloors;
        UI.Text(new Rect(0, 70, w, 66), "THE GLOOM TOWER", 52, UI.Purple);
        UI.Text(new Rect(0, 136, w, 26), "Thirty floors, climbed one at a time. What you have cleared stays cleared.", 17, Color.white, TextAnchor.MiddleCenter, false);

        // Thirty floors as a column of pips, ten to a row: cleared in gold, Guardians marked, the next one pulsing.
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f);
        for (int f = 1; f <= TowerFloors; f++)
        {
            int row = (f - 1) / 10, col = (f - 1) % 10;
            var pip = UI.Centered(cx + (col - 4.5f) * 44f, 300f - row * 44f, 30, 30);
            bool boss = f == TowerFloors, guard = GuardianFloor(f);
            Color c = f <= cleared ? UI.Gold : f == next ? Color.Lerp(Color.white, UI.Red, pulse) : new Color(0.3f, 0.27f, 0.4f);
            UI.Tex(UI.Grow(pip, guard || boss ? 6 : 3), UI.Circle, guard || boss ? UI.Purple : UI.Ink);
            UI.Tex(pip, UI.Circle, c);
            UI.Text(pip, f.ToString(), 13, f <= cleared ? UI.Ink : Color.white, TextAnchor.MiddleCenter, false);
        }
        UI.Text(new Rect(0, 326, w, 22), "purple ring: a Floor Guardian        " + cleared + " of " + TowerFloors + " cleared" + (Gloom > 0 || Save.GloomTier > 0 ? "        Gloom tier " + Save.GloomTier : ""),
            14, new Color(0.8f, 0.8f, 0.92f), TextAnchor.MiddleCenter, false);

        string what = next == TowerFloors ? "The Gloom King" : GuardianFloor(next) ? "Guardian: " + GuardianName(next) : Levels[FloorCatalog.At(next).theme].name;
        UI.Text(new Rect(0, 360, w, 30), "Floor " + next + "   -   " + what, 22, UI.Gold);
        if (UI.Button(UI.Centered(cx, 430, 460, 66), done ? "FACE THE GLOOM KING AGAIN" : "ENTER FLOOR " + next, UI.Red, 28))
        {
            hubPanel = 0;
            StartClimb();
        }

        // The tower beaten: it can be climbed again from the bottom, harder and richer.
        if (done && Save.GloomTier < Save.GloomUnlocked)
        {
            int tier = Save.GloomTier + 1;
            if (UI.Button(UI.Centered(cx, 506, 560, 52), "CLIMB AGAIN FROM FLOOR 1  -  GLOOM TIER " + tier, UI.Purple, 19))
            {
                JourneyStore.Transaction(() =>
                {
                    Save.GloomTier = tier;
                    Save.TowerFloor = 0;
                });
                hubPanel = 0;
                StartClimb();
            }
            UI.Text(new Rect(0, 536, w, 22), "+" + tier * 25 + "% enemy health, +" + tier * 25 + "% Sparks. Your level, stats and boons come with you.", 14,
                new Color(0.8f, 0.8f, 0.92f), TextAnchor.MiddleCenter, false);
        }
        int unfinished = UnfinishedRescue();
        if (unfinished >= 0 && UI.Button(UI.Centered(cx, 578, Mathf.Min(560, w - 60), 36),
            "RETURN FOR " + Story.Rescues[unfinished].name.ToUpper() + "  -  FLOOR " + Story.Rescues[unfinished].floor, UI.Blue, 17))
        {
            hubPanel = 0;
            ReturnForRescue(unfinished);
        }
        if (UI.Button(UI.Centered(cx, h - 90, 240, 52), "NOT YET", new Color(0.4f, 0.4f, 0.5f))) OpenHubPanel(0);
    }
}
