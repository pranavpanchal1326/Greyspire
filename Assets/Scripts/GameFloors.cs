using System.Collections.Generic;
using UnityEngine;

// A floor of the tower. Each one is a pocket of the world the tower swallowed:
// a wide stretch of land to cross, with packs of monsters that hold their ground
// until the hero comes near, a chest or two off the path, and at the far end
// the stairs up, sealed until every monster on the floor is dead. The way back
// down to Brushwick is where the hero came in. Some floors still hold a Painter
// who went up and never came home: drained grey, waiting to be painted back.
public partial class Game
{
    const float FloorRadius = 44f;
    const float PackAggro = 14f;

    static readonly Vector3 FloorStart = new Vector3(0f, 0.1f, -37f);
    static readonly Vector3 StairsAt = new Vector3(0f, 0f, 38f);
    static readonly Vector3 WayDownAt = new Vector3(0f, 0f, -41.5f);
    static readonly Color SealRed = new Color(1.6f, 0.3f, 0.35f, 0.5f), SealOpen = new Color(0.7f, 1.5f, 2f, 0.6f);

    readonly List<Vector2> packSpots = new List<Vector2>();
    bool stairsOpen;
    Renderer stairSeal;
    Spot stairSpot;
    Transform shotSubject;      // who the camera frames for a "them" shot: whoever the hero is talking to

    // The path from where you arrive to the stairs. It bows out round the Colour Heart in the middle.
    static float PathX(float z) => 9f * Mathf.Cos(z * Mathf.PI / 76f);

    static bool OnPath(Vector2 p, float clearance) => Mathf.Abs(p.x - PathX(p.y)) < clearance && Mathf.Abs(p.y) < 41f;

    void BuildFloor(int chapter, int index)
    {
        TrainingActive = false;
        GameAudio.StopWorld();
        if (LevelRoot != null)
        {
            LevelRoot.gameObject.SetActive(false);
            Destroy(LevelRoot.gameObject);
        }
        LevelRoot = new GameObject("Floor").transform;
        ProductionRandom.BeginScene(FloorCatalog.Legacy(chapter,index).id + ":gloom:" + Gloom);
        PlaytestLog.Emit("scope_start", note: ProductionRandom.Scope);
        InTown = false;
        spots.Clear();
        packSpots.Clear();
        ClearResidents();
        stairsOpen = false;
        stairSeal = null;
        stairSpot = null;
        nearSpot = null;
        Arena = ArenaState.None;
        Keeper = null;
        LevelDef def = Levels[chapter];
        int floorNumber = FloorCatalog.Legacy(chapter, index).number;
        // Floor 5 keeps a walled arena before its stairs: nothing else is put there.
        bool arena = IsGatekeeperFloor(floorNumber);
        // Every floor is laid out differently, and the same floor is the same on every climb.
        var rng = new System.Random(FloorCatalog.At(floorNumber).LayoutSeed);

        cam.backgroundColor = def.skyBottom;
        RenderSettings.fogColor = def.skyBottom;
        RenderSettings.ambientLight = def.ambient;
        sun.color = def.sun;
        BuildSky(def, rng, FloorRadius + 32f);
        BuildHeart();
        BuildMotes(def);

        var floor = new GameObject("Floor");
        floor.transform.SetParent(LevelRoot, false);
        var floorBox = floor.AddComponent<BoxCollider>();
        floorBox.size = new Vector3(320f, 2f, 320f);
        floorBox.center = new Vector3(0, -1f, 0);
        const int segments = 56;
        float segLen = 2f * Mathf.PI * FloorRadius / segments + 0.5f;
        for (int i = 0; i < segments; i++)
        {
            float a = i * Mathf.PI * 2f / segments;
            var wall = new GameObject("Wall");
            wall.transform.SetParent(LevelRoot, false);
            wall.transform.position = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * (FloorRadius + 0.5f) + Vector3.up * 5f;
            wall.transform.rotation = Quaternion.Euler(0, a * Mathf.Rad2Deg, 0);
            wall.AddComponent<BoxCollider>().size = new Vector3(segLen, 10f, 1f);
        }

        // The ground: the shader breaks each colour up into patches, so no flat discs show.
        Disc(def.outer, 190f, -0.06f, 0f, 0f, def.outer * 0.88f);
        Disc(def.ground, FloorRadius + 2.5f, -0.03f, 0f, 0f, def.patch);
        if (floorNumber == ChapterProgress.MillraceFloor) { BuildMillraceFloor(def, rng); return; }
        if (floorNumber == PilotProgress.Floor) { BuildPilotFloor(def, rng); return; }
        Color path = Color.Lerp(def.ground, new Color(0.8f, 0.72f, 0.56f), 0.75f);
        for (float z = -42f; z <= 40f; z += 2.1f) Disc(path, 2.2f, -0.006f, PathX(z), z, path * 0.9f);

        BuildGrass(def, FloorRadius + 9f, rng, chapter == 1 ? 2600 : 10000, p => OnPath(p, 2.3f) || arena && ArenaReserved(p));
        BuildFlowers(def.accents, Vector2.zero, FloorRadius + 9f, chapter == 1 ? 320 : 1600, rng, p => OnPath(p, 2.3f) || arena && ArenaReserved(p));

        Scatter(rng, Vector2.zero, FloorRadius, 130, LeafTint(def), p => OnPath(p, 2.2f) || p.magnitude < 3f || arena && ArenaReserved(p));

        var start = new Vector2(FloorStart.x, FloorStart.z);
        var stairs = new Vector2(StairsAt.x, StairsAt.z);

        // A drained Painter stands a little off the path, where anyone climbing will see them.
        int lost = Story.RescueOnFloor(floorNumber);
        bool statue = lost >= 0 && (Save.RescuedMask & (1 << lost)) == 0;
        var statueAt = new Vector2(PathX(12f) + 5.5f, 12f);

        // Where the monster packs will stand: spread out, away from the arrival point and off the path.
        var encounterLayout = new System.Random(7919 * floorNumber);
        for (int tries = 0; packSpots.Count < 6 && tries < 500; tries++)
        {
            Vector2 p = RandomInCircle(encounterLayout, FloorRadius - 7f);
            if (p.magnitude < 11f || (p - start).magnitude < 18f || (p - stairs).magnitude < 10f || OnPath(p, 4f)) continue;
            if (arena && ArenaReserved(p)) continue;
            if (statue && (p - statueAt).magnitude < 9f) continue;
            if (TooClose(packSpots, p, 15f)) continue;
            packSpots.Add(p);
        }

        // The edge of the floor.
        for (int i = 0; i < 58; i++)
        {
            float a = (i + (float)rng.NextDouble() * 0.5f) * Mathf.PI * 2f / 58f;
            // The way down is kept clear.
            if (Mathf.Abs(Mathf.DeltaAngle(a * Mathf.Rad2Deg, 180f)) < 9f) continue;
            float r = FloorRadius + 1.8f + (float)rng.NextDouble() * 2.6f;
            Prop(def.ring[i % def.ring.Length], new Vector3(Mathf.Sin(a) * r, 0, Mathf.Cos(a) * r), rng, 1.15f);
        }

        // Groves, rocks and ruins across the floor: cover to fight round, and reasons to leave the path.
        var placed = new List<Vector2>();
        if (statue) placed.Add(statueAt);
        for (int tries = 0; placed.Count < 38 && tries < 700; tries++)
        {
            Vector2 p = RandomInCircle(rng, FloorRadius - 4f);
            if (p.magnitude < 7f || OnPath(p, 3.6f) || (p - start).magnitude < 9f || (p - stairs).magnitude < 8f) continue;
            if (arena && ArenaReserved(p)) continue;
            if (TooClose(packSpots, p, 6f) || TooClose(placed, p, 5.5f)) continue;
            PropDef d = placed.Count % 3 == 2 ? def.inner[placed.Count / 3 % def.inner.Length] : def.ring[placed.Count % def.ring.Length];
            Prop(d, new Vector3(p.x, 0, p.y), rng, 1.05f);
            placed.Add(p);
        }

        BuildStairs(rng, floorNumber);
        BuildWayDown(rng);
        if (arena) BuildGatekeeperArena(rng);
        if (statue)
        {
            BuildStatue(lost, new Vector3(statueAt.x, 0f, statueAt.y));
            FrameStatue(new Vector3(statueAt.x, 0f, statueAt.y), rng);
        }

        // Something worth leaving the path for: a chest on every floor, a second on the even ones.
        // A chest is opened once, ever: coming back to a floor does not fill it again.
        for (int c = 0; c < (index % 2 == 1 ? 2 : 1); c++)
        {
            if (!FreeSpot(rng, placed, start, 16f, out Vector2 at)) continue;
            placed.Add(at);
            if (Save.TowerChest(floorNumber, c)) continue;
            GameObject chest = Prop(new PropDef("Chest_Wood", Fur, 1.5f, 0.6f), new Vector3(at.x, 0, at.y), rng, 1f, (float)rng.NextDouble() * 360f);
            int reward = 6 + floorNumber * 2, which = c;
            Spot spot = null;
            spot = AddSpot(new Vector3(at.x, 0, at.y), "Treasure", "Open the chest", () =>
            {
                if (!JourneyStore.Transaction(() =>
                {
                    if (Save.TowerChest(floorNumber, which)) return false;
                    Save.OpenTowerChest(floorNumber, which);
                    coins = checked(coins + reward);
                    return true;
                })) return;
                coinPunch = 1f;
                Toast("+" + reward + " SPARKS", UI.Gold);
                Fx.Burst(chest.transform.position + Vector3.up, UI.Gold, 16, 7f);
                ToonKit.Sfx("buy");
                chest.SetActive(false);
                spots.Remove(spot);
            }, chest.transform);
        }

        // Every third floor, a spring of colour the Gloom missed: two hearts back, once a visit.
        if (index % 3 == 1 && FreeSpot(rng, placed, start, 20f, out Vector2 well))
        {
            GameObject spring = Prop(new PropDef("Crystal", null, 2.2f, 0.5f), new Vector3(well.x, 0, well.y), rng, 0.7f, 0f);
            AddGlow(spring.transform, 1.6f, new Color(0.5f, 0.9f, 1f), 3.2f);
            Spot spot = null;
            spot = AddSpot(new Vector3(well.x, 0, well.y), "Colour Spring", "Drink: restore 2 hearts", () =>
            {
                Player.Heal(2);
                heartPunch = 1f;
                Fx.Ring(spring.transform.position, UI.Blue, 0.4f, 5f, 0.5f);
                Fx.Burst(spring.transform.position + Vector3.up, UI.Blue, 12, 6f);
                ToonKit.Sfx("heart");
                Toast("+2 HEARTS", UI.Blue);
                spots.Remove(spot);
            }, spring.transform);
        }

        // Everything that stands on the floor is in place: bake the map agents walk on.
        NavField.BakeStaged(LevelRoot, FloorRadius, Player.GetComponent<CharacterController>());
    }

    static bool TooClose(List<Vector2> points, Vector2 p, float distance)
    {
        foreach (Vector2 q in points) if ((q - p).magnitude < distance) return true;
        return false;
    }

    // Somewhere off the path, clear of scenery and monster packs, a fair walk from the arrival point.
    bool FreeSpot(System.Random rng, List<Vector2> taken, Vector2 start, float fromStart, out Vector2 at)
    {
        at = Vector2.zero;
        for (int tries = 0; tries < 200; tries++)
        {
            at = RandomInCircle(rng, FloorRadius - 6f);
            if (at.magnitude < 8f || OnPath(at, 4f) || (at - start).magnitude < fromStart) continue;
            if (Arena != ArenaState.None && ArenaReserved(at)) continue;
            if (TooClose(taken, at, 4.5f) || TooClose(packSpots, at, 5f)) continue;
            return true;
        }
        return false;
    }

    // Two pillars, the tower's crystal turning between them, and a seal across the gap.
    void BuildStairs(System.Random rng, int floorNumber)
    {
        Prop(new PropDef("Pillar", Kit, 7f, 0.7f), StairsAt + Vector3.left * 3.3f, rng, 1f, 0f);
        Prop(new PropDef("Pillar", Kit, 7f, 0.7f), StairsAt + Vector3.right * 3.3f, rng, 1f, 0f);
        Transform core = ToonKit.Spawn("Crystal", LevelRoot, 0.8f);
        core.position = StairsAt + Vector3.up * 5.4f;
        core.gameObject.AddComponent<Spin>();

        var seal = GameObject.CreatePrimitive(PrimitiveType.Quad);
        DestroyImmediate(seal.GetComponent<Collider>());
        seal.name = "StairSeal";
        seal.transform.SetParent(LevelRoot, false);
        seal.transform.position = StairsAt + Vector3.up * 2.6f;
        seal.transform.localScale = new Vector3(7.5f, 6.5f, 1f);
        stairSeal = seal.GetComponent<Renderer>();
        stairSeal.sharedMaterial = Fx.Mote;
        stairSeal.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        Fx.Tint(stairSeal, SealRed);

        string sealed0 = GuardianFloor(floorNumber) ? "Sealed. The Guardian holds this floor" : "Sealed. Defeat every monster on this floor";
        stairSpot = AddSpot(StairsAt + Vector3.back * 1.5f, "Stairs (sealed)", sealed0, ClimbStairs);
    }

    // Where the hero came in: the stairs back down, and out to Brushwick.
    void BuildWayDown(System.Random rng)
    {
        Prop(new PropDef("Pillar", Kit, 4.6f, 0.55f), WayDownAt + Vector3.left * 2.8f, rng, 1f, 0f);
        Prop(new PropDef("Pillar", Kit, 4.6f, 0.55f), WayDownAt + Vector3.right * 2.8f, rng, 1f, 0f);
        Prop(new PropDef("LampPost", Kit, 0f, 0.25f), WayDownAt + new Vector3(1.6f, 0f, 0.6f), rng, 1f, 0f);
        AddSpot(WayDownAt + Vector3.forward * 0.8f, "The Way Down", "Leave the tower for Brushwick (cleared floors stay cleared)", () =>
        {
            if (clearPending) return;
            if (Hunted())
            {
                Banner("NOT WHILE YOU ARE HUNTED", 1.8f, "Shake them off, or finish them.");
                ToonKit.Sfx("deny");
                return;
            }
            LeaveTower();
        });
    }

    // One of the lost Painters, drained to grey stone mid-stride. Once the floor is clear the
    // hero can paint them back, and they go home to Brushwick.
    void BuildStatue(int who, Vector3 pos)
    {
        Lost lost = Story.Rescues[who];
        var holder = new GameObject("Drained Painter");
        holder.transform.SetParent(LevelRoot, false);
        holder.transform.SetPositionAndRotation(pos + Vector3.up * 0.05f, Quaternion.Euler(0f, -25f, 0f));
        Transform body = Humans.Build(lost.look, holder.transform);
        if (body == null)
        {
            Destroy(holder);
            return;
        }
        // A drained Painter runs on the story's Animator Controller: its first state, Stone, is
        // their guard held still. Without the controller (assets not built) the old pose is used.
        var controller = Resources.Load<RuntimeAnimatorController>("Story/Painter");
        Animator actor = controller != null ? body.GetComponentInChildren<Animator>() : null;
        CharAnim clips = null;
        if (actor != null)
        {
            actor.runtimeAnimatorController = controller;
            actor.applyRootMotion = false;
            actor.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }
        else clips = CharAnim.Attach(holder, body);
        Humans.Hold(body, "Sword_Bronze", false, Humans.Grip, Humans.GripAngles);
        // Caught with their guard up, and held there.
        if (clips != null) clips.Loop("Blocking", 0f, 0f);
        Capsule(holder, 0.45f, 1.8f, 0.9f);
        var stone = holder.AddComponent<Drained>();

        Spot spot = null;
        spot = AddSpot(pos, "A drained Painter", "Paint them back", () =>
        {
            if (waveActive)
            {
                Banner("THE GLOOM STILL HOLDS THEM", 2f, "Clear the floor, then paint them back.");
                ToonKit.Sfx("deny");
                return;
            }
            if (!Save.BeginRescue(lost.id)) return;
            spots.Remove(spot);
            if (actor != null)
            {
                RescueScene(holder, stone, actor, who, pos);
                return;
            }
            stone.Thaw();
            if (clips != null) clips.Loop("Idle", 1f, 0.6f);
            Flash(UI.Gold, 0.4f);
            Fx.Ring(pos, UI.Gold, 0.4f, 6f, 0.7f);
            ToonKit.Sfx("restore");
            shotSubject = holder.transform;
            ResumeRescue(who, () =>
            {
                SetState(GameState.Playing);
                SystemMessage("PAINTER RESCUED", lost.name + " is going home to Brushwick.    +2 stat points");
                Fx.Burst(pos + Vector3.up, UI.Gold, 14, 6f);
                Fx.Ring(pos, Color.white, 0.3f, 3f, 0.4f);
                holder.SetActive(false);
            });
        });
    }

    // Puts the floor's monsters out in packs. A Guardian, when there is one, stands at the stairs.
    void SpawnPacks(Wave w)
    {
        var kinds = new List<Enemy.Kind>();
        for (int i = 0; i < w.slimes; i++) kinds.Add(Enemy.Kind.Slime);
        for (int i = 0; i < w.minions; i++) kinds.Add(Enemy.Kind.Minion);
        for (int i = 0; i < w.bigSlimes; i++) kinds.Add(Enemy.Kind.BigSlime);
        for (int i = 0; i < w.spitters; i++) kinds.Add(Enemy.Kind.Spitter);
        for (int i = 0; i < w.bombers; i++) kinds.Add(Enemy.Kind.Bomber);
        for (int i = kinds.Count - 1; i > 0; i--)
        {
            int j = ProductionRandom.Spawn.Range(0, i + 1);
            Enemy.Kind tmp = kinds[i]; kinds[i] = kinds[j]; kinds[j] = tmp;
        }

        int floor = Floor;
        // Retrying the Gatekeeper: his floor's packs were beaten on an earlier attempt.
        if (IsGatekeeperFloor(floor) && ChapterProgress.GatekeeperCheckpoint) kinds.Clear();
        float eliteChance = (level == 0 ? 0.04f : 0.12f) + Gloom * 0.05f;
        int packs = Mathf.Clamp(Mathf.CeilToInt(kinds.Count / 3f), 1, Mathf.Max(1, packSpots.Count));
        for (int i = 0; i < kinds.Count; i++)
        {
            Vector2 centre = packSpots.Count > 0 ? packSpots[i % packs] : new Vector2(0f, 14f);
            Vector2 off = ProductionRandom.Spawn.insideUnitCircle * 2.8f;
            Enemy e = Enemy.Create(kinds[i], new Vector3(centre.x + off.x, 0.1f, centre.y + off.y), level, floor > 1 && ProductionRandom.Spawn.value < eliteChance);
            e.aggro = PackAggro;
        }
        for (int i = 0; i < w.brutes; i++)
        {
            bool guardian = i == 0 && GuardianFloor(floor);
            Vector3 guard = StairsAt + new Vector3((i - (w.brutes - 1) * 0.5f) * 4f, 0.1f, guardian ? -9f : -8f);
            Enemy e = Enemy.Create(Enemy.Kind.Brute, guard, level, guardian);
            e.aggro = PackAggro + (guardian ? 6f : 2f);
            if (guardian) e.title = GuardianName(floor);
            if (guardian && Arena != ArenaState.None) BindGatekeeper(e);
        }
    }

    void UnsealStairs()
    {
        if (stairSpot == null) return;
        stairsOpen = true;
        stairSpot.name = "Stairs";
        stairSpot.prompt = "Climb to Floor " + (Floor + 1);
        if (stairSeal != null) Fx.Tint(stairSeal, SealOpen);
        Fx.Ring(StairsAt, UI.Blue, 1f, 9f, 0.8f);
    }

    // Up to the next floor: a new stretch of land, new packs. One floor at a time, always.
    void ClimbStairs()
    {
        if (!stairsOpen)
        {
            Banner(Enemy.All.Count + (Enemy.All.Count == 1 ? " MONSTER REMAINS" : " MONSTERS REMAIN"), 1.8f);
            ToonKit.Sfx("deny");
            return;
        }
        if (clearPending) return;
        stairsOpen = false;
        Transition(() =>
        {
            var definition = FloorCatalog.At(Mathf.Min(Floor + 1, TowerFloors));
            level = definition.theme;
            int next = definition.layout;
            Player.Heal(1);
            EnterFloor(next);
        });
    }

    // Chests, springs, the stairs and the way down: whatever is in reach, and the key to use it.
    void TickFloor()
    {
        if (Player.Dead || DefeatPending) { nearSpot = null; return; }
        if (fadeDir != 0f) return;
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

    void DrawFloorHud(float w, float h, float cx)
    {
        if (TrainingActive && PracticeTarget != null)
            UI.Text(new Rect(0,70,w,64),"PRACTICE — no rewards     Last accepted hit: "+PracticeTarget.LastTrainingDamage.ToString("0.#")+"\nGold cue: guard late or evade, then counter. Hold attack to charge a heavy.\nThe return marker behind the starting point leaves practice.",14,UI.Gold);
        Vector3 me = Player.transform.position;
        foreach (Spot s in spots)
        {
            if ((s.pos - me).magnitude > 34f || !ToGui(s.pos + Vector3.up * 2.8f, out Vector2 p)) continue;
            UI.Text(UI.Centered(p.x, p.y, 320, 30), s.name, 18, s == nearSpot ? UI.Gold : s == stairSpot && stairsOpen ? UI.Blue : Color.white);
        }

        // What is left to do on this floor.
        if (waveActive && Enemy.All.Count > 0 && !awakening)
            UI.Text(new Rect(0, 38, w, 22), "Monsters on this floor:  " + Enemy.All.Count, 15, new Color(0.9f, 0.9f, 1f), TextAnchor.MiddleCenter, false);
        else if (stairsOpen)
            UI.Text(new Rect(0, 40, w, 22), "Floor cleared. The stairs are open, to the north.", 15, UI.Blue, TextAnchor.MiddleCenter, false);

        DrawMinimap(w, h, 46f);
        if (nearSpot != null) DrawPrompt(h, cx, nearSpot.prompt);
    }
}
