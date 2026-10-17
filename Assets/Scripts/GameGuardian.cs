using System.Collections.Generic;
using UnityEngine;

// Floor 5's Guardian arena (package C03): an iron-railed ring before the stairs, four stone
// pillars inside it, a south gate the hero comes in by and a north gate to the stairs.
// Grol keeps the gate shut until the rest of the floor is quiet; then it opens, and walking in
// shuts it behind the hero. A checkpoint receipt lets a fallen hero retry from his gate.
public partial class Game
{
    public static readonly Vector3 ArenaCentre = new Vector3(0f, 0f, 25f);
    public const float KeeperArenaRadius = 11f, GateWidth = 3.6f, PillarRing = 6.5f;
    static readonly Vector3 ArenaEntrance = new Vector3(0f, 0.1f, 25f - 11f - 3f);

    public enum ArenaState { None, Shut, Open, Sealed, Won }
    public ArenaState Arena { get; private set; }
    public Enemy Keeper { get; private set; }
    public bool KeeperIntel { get; private set; }

    class Gate { public GameObject bars; public Collider block; public Renderer seal; public bool shut; }
    Gate southGate, northGate;
    readonly List<Transform> pillars = new List<Transform>();
    readonly List<bool> pillarBroken = new List<bool>();
    public int PillarsStanding { get { int n = 0; foreach (bool b in pillarBroken) if (!b) n++; return n; } }

    bool IsGatekeeperFloor(int floor) => floor == ChapterProgress.GatekeeperFloor && GuardianFloor(floor);

    // Inside the arena or its walk-round: nothing else on the floor is placed there.
    static bool ArenaReserved(Vector2 p) => (p - new Vector2(ArenaCentre.x, ArenaCentre.z)).magnitude < KeeperArenaRadius + 2.5f;

    // Where the hero arrives on floor 5: at the foot of the floor, or at the Gatekeeper's gate
    // when the checkpoint says only he is left.
    Vector3 FloorArrival(int floor) => IsGatekeeperFloor(floor) && ChapterProgress.GatekeeperCheckpoint ? ArenaEntrance : FloorStart;

    void BuildGatekeeperArena(System.Random rng)
    {
        Arena = ArenaState.Shut;
        Keeper = null;
        pillars.Clear();
        pillarBroken.Clear();
        var root = new GameObject("Gatekeeper arena").transform;
        root.SetParent(LevelRoot, false);

        // A paved ring floor, so the arena reads as a place apart.
        Disc(new Color(0.42f, 0.4f, 0.38f), KeeperArenaRadius - 0.2f, -0.004f, ArenaCentre.x, ArenaCentre.z, new Color(0.36f, 0.34f, 0.33f));

        // The railing: two sweeps from gate to gate (north gateway at 0 degrees, south at 180),
        // each made of short straight runs that are solid to walk into.
        float gateHalf = Mathf.Asin(GateWidth * 0.5f / KeeperArenaRadius) * Mathf.Rad2Deg;
        const int runs = 9;
        foreach (float start in new[] { 0f, 180f })
        {
            float a0 = start + gateHalf, span = 180f - 2f * gateHalf;
            for (int k = 0; k < runs; k++) Rail(root, a0 + span * k / runs, a0 + span * (k + 1) / runs);
        }
        southGate = MakeGate(root, 180f);
        northGate = MakeGate(root, 0f);

        // Four stone pillars: what a charging Gatekeeper can be brought up against.
        for (int i = 0; i < 4; i++)
        {
            float a = (45f + i * 90f) * Mathf.Deg2Rad;
            Vector3 at = ArenaCentre + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * PillarRing;
            GameObject p = Prop(new PropDef("Pillar", Kit, 3.4f, 0.65f), at, rng, 1f, 0f);
            p.name = "Gate pillar " + i;
            pillars.Add(p.transform);
            pillarBroken.Add(false);
        }
        // Torches at the gateways.
        foreach (float side in new[] { -1f, 1f })
        {
            Prop(new PropDef("Torch", Kit, 0f, 0.2f), ArenaCentre + new Vector3(side * (GateWidth * 0.5f + 0.8f), 0f, -KeeperArenaRadius - 0.6f), rng, 1f, 180f);
            Prop(new PropDef("Torch", Kit, 0f, 0.2f), ArenaCentre + new Vector3(side * (GateWidth * 0.5f + 0.8f), 0f, KeeperArenaRadius + 0.6f), rng, 1f, 0f);
        }
        if (ChapterProgress.GatekeeperCheckpoint) OpenGate(southGate, false);
    }

    void Rail(Transform root, float a0, float a1)
    {
        Vector3 from = ArenaCentre + Quaternion.Euler(0f, a0, 0f) * Vector3.forward * KeeperArenaRadius;
        Vector3 to = ArenaCentre + Quaternion.Euler(0f, a1, 0f) * Vector3.forward * KeeperArenaRadius;
        Village.FenceLine(root, from, to, true);
        var wall = new GameObject("Arena rail");
        wall.transform.SetParent(root, false);
        wall.transform.position = (from + to) * 0.5f + Vector3.up * 1.5f;
        wall.transform.rotation = Quaternion.LookRotation(to - from);
        wall.AddComponent<BoxCollider>().size = new Vector3(0.4f, 3f, (to - from).magnitude + 0.3f);
    }

    Gate MakeGate(Transform root, float angle)
    {
        Vector3 mid = ArenaCentre + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * KeeperArenaRadius;
        Vector3 across = Quaternion.Euler(0f, angle + 90f, 0f) * Vector3.forward;
        var gate = new Gate { bars = new GameObject(angle == 0f ? "North gate" : "South gate") };
        gate.bars.transform.SetParent(root, false);
        Village.FenceLine(gate.bars.transform, mid - across * GateWidth * 0.5f, mid + across * GateWidth * 0.5f, true);
        var block = new GameObject("Gate block");
        block.transform.SetParent(gate.bars.transform, false);
        block.transform.position = mid + Vector3.up * 1.5f;
        block.transform.rotation = Quaternion.LookRotation(across);
        var box = block.AddComponent<BoxCollider>();
        box.size = new Vector3(0.4f, 3f, GateWidth + 0.4f);
        gate.block = box;
        // A red seal hangs in the shut gateway, like the stairs' seal.
        var seal = GameObject.CreatePrimitive(PrimitiveType.Quad);
        DestroyImmediate(seal.GetComponent<Collider>());
        seal.name = "Gate seal";
        seal.transform.SetParent(gate.bars.transform, false);
        seal.transform.position = mid + Vector3.up * 1.5f;
        seal.transform.rotation = Quaternion.LookRotation(Quaternion.Euler(0f, angle, 0f) * Vector3.forward);
        seal.transform.localScale = new Vector3(GateWidth, 3f, 1f);
        gate.seal = seal.GetComponent<Renderer>();
        gate.seal.sharedMaterial = Fx.Mote;
        gate.seal.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        Fx.Tint(gate.seal, SealRed);
        gate.shut = true;
        return gate;
    }

    void OpenGate(Gate gate, bool announce)
    {
        if (gate == null || !gate.shut) return;
        gate.shut = false;
        gate.bars.SetActive(false);
        Physics.SyncTransforms();
        if (!announce) return;
        Fx.Ring(gate.bars.transform.position, UI.Gold, 0.5f, 5f, 0.6f);
        ToonKit.Sfx("restore", 0.8f, 0.8f);
    }

    void ShutGate(Gate gate)
    {
        if (gate == null || gate.shut) return;
        gate.shut = true;
        gate.bars.SetActive(true);
        Physics.SyncTransforms();
        Fx.Ring(gate.bars.transform.position, UI.Red, 0.5f, 5f, 0.5f);
        ToonKit.Sfx("slam", 0.8f, 0.7f);
        Cam.Shake(0.3f);
    }

    public bool SouthGateShut => southGate != null && southGate.shut;
    public bool NorthGateShut => northGate != null && northGate.shut;

    // Called as the floor's packs are put out: the floor's first brute is the Gatekeeper.
    void BindGatekeeper(Enemy e)
    {
        Keeper = e;
        e.MakeGatekeeper();
        e.transform.position = new Vector3(ArenaCentre.x, e.transform.position.y, ArenaCentre.z + 3f);
        e.transform.rotation = Quaternion.LookRotation(Vector3.back);
    }

    public Vector3 GatekeeperFacing(Vector3 from)
    {
        Vector3 d = new Vector3(ArenaEntrance.x, from.y, ArenaEntrance.z) - from;
        return d.sqrMagnitude > 0.01f ? d.normalized : Vector3.back;
    }

    void TickArena()
    {
        if (Arena == ArenaState.None || Keeper == null || State != GameState.Playing || !waveActive) return;
        switch (Arena)
        {
            case ArenaState.Shut:
                // The rest of the floor is quiet: the gate opens, and the checkpoint is kept.
                if (pending == 0 && Enemy.All.Count == 1 && Enemy.All[0] == Keeper)
                {
                    if (ChapterProgress.ReachGatekeeper(ChapterProgress.GatekeeperToken)) PlaytestLog.Emit("checkpoint");
                    Arena = ArenaState.Open;
                    OpenGate(southGate, true);
                    Banner("THE GATE OPENS", 2.2f, "Grol the Gatekeeper waits inside");
                }
                else if (ChapterProgress.GatekeeperCheckpoint && Enemy.All.Count == 1)
                {
                    Arena = ArenaState.Open;
                    OpenGate(southGate, false);
                }
                break;
            case ArenaState.Open:
                Vector3 off = Player.transform.position - ArenaCentre;
                off.y = 0f;
                if (off.magnitude < KeeperArenaRadius - 1.5f) SealArena();
                break;
        }
    }

    void SealArena()
    {
        Arena = ArenaState.Sealed;
        PlaytestLog.Emit("guardian_engage", note: Keeper != null ? Keeper.title : "");
        ShutGate(southGate);
        Keeper.Wake();
        Banner(Keeper.title.ToUpper(), 2.4f, "Floor Guardian");
        // What Rhea told the hero on floor 4, if he asked her: it changes how the fight opens.
        KeeperIntel = Save.Consequence("rescue:rhea:guardian-intel");
        if (KeeperIntel)
        {
            MarkPillars(true);
            Toast("Rhea: it cannot turn once it charges. Put stone at your back.", UI.Gold);
        }
    }

    public void OnGatekeeperPhase(Enemy keeper, int phase)
    {
        Word(keeper.transform.position + Vector3.up * 4.4f, phase == 2 ? "He tears the bars loose" : "Enraged", UI.Red, 34);
        if (phase == 2)
        {
            MarkPillars(true);
            Toast(KeeperIntel ? "Now. Bring him into the stone." : "Lumi: he's going to charge! Get a pillar behind you, then step aside!", UI.Gold);
        }
        else Toast("He will not stop at one charge now.", UI.Red);
    }

    void MarkPillars(bool on)
    {
        for (int i = 0; i < pillars.Count; i++)
        {
            if (pillarBroken[i] || pillars[i] == null) continue;
            var glow = pillars[i].Find("Pillar mark");
            if (on && glow == null) AddGlow(pillars[i], 1.2f, UI.Gold, 2.4f).name = "Pillar mark";
        }
    }

    public bool PillarMarked(int i) => i >= 0 && i < pillars.Count && pillars[i] != null && pillars[i].Find("Pillar mark") != null;

    // The pillar a charge from pos along dir is about to hit, if any.
    public int GatePillarAhead(Vector3 pos, Vector3 dir, float reach)
    {
        for (int i = 0; i < pillars.Count; i++)
        {
            if (pillarBroken[i] || pillars[i] == null) continue;
            Vector3 to = pillars[i].position - pos;
            to.y = 0f;
            float along = Vector3.Dot(to, dir);
            if (along < -0.2f || along > reach + 0.7f) continue;
            if ((to - dir * along).magnitude < 0.65f + reach * 0.6f) return i;
        }
        return -1;
    }

    public bool PillarStanding(int i) => i >= 0 && i < pillarBroken.Count && !pillarBroken[i];

    public void BreakGatePillar(int i)
    {
        if (!PillarStanding(i)) return;
        pillarBroken[i] = true;
        Transform p = pillars[i];
        Vector3 at = p.position;
        Fx.Burst(at + Vector3.up * 1.5f, new Color(0.75f, 0.72f, 0.66f), 22, 8f, 1.4f);
        Fx.Dust(at, 10);
        ToonKit.Sfx("boom", 0.9f, 0.8f, position: at + Vector3.up);
        Cam.Shake(0.5f);
        p.gameObject.SetActive(false);
        var rubble = new GameObject("Pillar rubble").transform;
        rubble.SetParent(LevelRoot, false);
        rubble.position = at;
        Village.Make("Rubble", rubble);
        Physics.SyncTransforms();
    }

    // Whether a charge from pos along dir is about to meet the arena's railing.
    public bool GatekeeperWallAhead(Vector3 pos, Vector3 dir, float reach)
    {
        Vector3 next = pos + dir * reach - ArenaCentre;
        next.y = 0f;
        return next.magnitude > KeeperArenaRadius - 0.4f;
    }

    // The Gatekeeper has fallen: both gates open, and the checkpoint has done its work.
    void PassArena()
    {
        if (Arena == ArenaState.None) return;
        Arena = ArenaState.Won;
        PlaytestLog.Emit("guardian_won");
        OpenGate(southGate, true);
        OpenGate(northGate, true);
        ChapterProgress.PassGatekeeper(ChapterProgress.GatekeeperToken);
    }
}
