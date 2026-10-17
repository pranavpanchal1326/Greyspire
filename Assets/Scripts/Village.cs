using System.Collections.Generic;
using UnityEngine;

// Houses built from the modular village kit in Resources/Village: timber, plaster,
// stone and tile, put together wall by wall the way the kit intends. A house is a
// footprint on a two-metre grid, a number of three-metre storeys, a ring of wall
// pieces round each storey (plain, windowed, or the one with the door), a post at
// every corner, and a tiled roof with a gable at each end. Towers are the same
// thing, square and tall, under a spire.
public static partial class Village
{
    public const float Storey = 3f;

    public class Plan
    {
        public int w = 4, d = 6;            // footprint in metres: across the front, and front to back (even numbers)
        public int floors = 2;
        public bool stoneGround = true;     // the ground floor in rough stone, the storeys above in plaster and timber
        public bool stoneAll;               // stone all the way up (towers, the keep)
        public bool sideDoor;               // the door in a long wall, the roof ridge running across the front
        public bool tower;                  // a spire instead of a pitched roof
        public bool door = true;
        public int chimneys = 1;
        public bool vines = true;
        public int seed = 1;
        public Color tint = Color.white;    // multiplies every surface (the Gloom Tower is drained and dark)
        public float roofScale = 1f;        // towers wider than the spire was made for
        public bool dark;                   // nobody lives here: no window is ever lit
        public bool balcony;                // a timber balcony over the front door
        public bool dormers;                // windows standing out of the roof
        public bool lancets;                // tall narrow windows only, all the way round (a church)
        public bool bare;                   // no roof at all: a curtain wall, open to the sky
    }

    static readonly Dictionary<string, GameObject> pieces = new Dictionary<string, GameObject>();
    static readonly Dictionary<(string, Color, int), Material> skins = new Dictionary<(string, Color, int), Material>();

    // Window glass comes in a few households' worth: each goes dark at its own bedtime.
    // The last kind is never lit at all (the Gloom Tower's).
    const int Unlit = 3;
    static readonly float[] Bedtime = { 22f, 23.4f, 99f };
    static readonly List<Material>[] panes = { new List<Material>(), new List<Material>(), new List<Material>() };

    // Which texture each of the kit's materials wears.
    static readonly (string material, string texture)[] Skins =
    {
        ("MI_WoodTrim", "T_WoodTrim_BaseColor"), ("MI_RockTrim", "T_RockTrim_BaseColor"), ("MI_Plaster", "T_Plaster_BaseColor"),
        ("MI_UnevenBrick", "T_UnevenBrick_BaseColor"), ("MI_RedBrick", "T_RedBrick_BaseColor"), ("MI_Brick", "T_Brick_BaseColor"),
        ("MI_RoundTiles", "T_RoundTiles_BaseColor"),
    };

    static readonly Color Glass = new Color(0.2f, 0.26f, 0.36f), Lamplight = new Color(1.5f, 0.95f, 0.45f), Iron = new Color(0.2f, 0.2f, 0.24f);
    static readonly Color Ivy = new Color(0.36f, 0.56f, 0.26f);

    public static bool Ready => Load("Wall_Plaster_Straight") != null;

    static GameObject Load(string piece)
    {
        if (pieces.TryGetValue(piece, out GameObject p)) return p;
        p = Resources.Load<GameObject>("Village/" + piece);
        pieces[piece] = p;
        return p;
    }

    static Material Skin(string material, Color tint, int household)
    {
        bool glass = material.StartsWith("MI_WindowGlass");
        if (!glass) household = 0;
        if (skins.TryGetValue((material, tint, household), out Material m) && m != null) return m;
        if (glass)
        {
            // Dark by day; after dusk the windows are lit from inside (Lamps() turns _Emit up and down).
            m = new Material(ToonKit.Mat(Glass * tint, true));
            m.SetColor("_Emit", new Color(Lamplight.r, Lamplight.g, Lamplight.b, 0f));
            if (household < Unlit) panes[household].Add(m);
        }
        else if (material.StartsWith("MI_Vine"))
            m = new Material(ToonKit.Mat(Ivy * tint, true, 0.3f, Resources.Load<Texture2D>("Village/Tex/T_VineLeaf"), true));
        else if (material.StartsWith("MI_Metal"))
            m = new Material(ToonKit.Mat(Iron * tint, true));
        else
        {
            Texture tex = null;
            foreach (var skin in Skins)
            {
                if (!material.StartsWith(skin.material)) continue;
                tex = Resources.Load<Texture2D>("Village/Tex/" + skin.texture);
                break;
            }
            m = new Material(ToonKit.Mat(tint, true, 0f, tex));
        }
        // Glass shines, fired tiles a little, iron some; timber, plaster and stone are matt.
        m.SetFloat("_Rim", glass ? 0.5f : 0.06f);
        m.SetFloat("_Spec", glass ? 0.75f : material.StartsWith("MI_RoundTiles") ? 0.22f : material.StartsWith("MI_Metal") ? 0.45f : 0.04f);
        skins[(material, tint, household)] = m;
        return m;
    }

    // Lights the windows for the hour: night is 0 by day and 1 after dark. Each household
    // puts its lamps out at its own bedtime, and nobody lights one again before dusk.
    public static void Lamps(float night, float hour)
    {
        for (int h = 0; h < panes.Length; h++)
        {
            float abed = hour >= 16f ? Mathf.Clamp01((hour - Bedtime[h]) * 3f) : Bedtime[h] > 24f ? 0f : 1f;
            float lit = night * (1f - abed);
            for (int i = panes[h].Count - 1; i >= 0; i--)
            {
                Material m = panes[h][i];
                if (m == null) { panes[h].RemoveAt(i); continue; }
                m.SetColor("_Emit", new Color(Lamplight.r, Lamplight.g, Lamplight.b, lit));
            }
        }
    }

    // Stands one piece of the kit in a building. yaw 0 leaves its outer face looking down +z.
    public static Transform Put(Transform root, string piece, Vector3 pos, float yaw, Color tint, float scale = 1f, int household = 0)
    {
        GameObject prefab = Load(piece);
        if (prefab == null) return null;
        GameObject go = Object.Instantiate(prefab, root);
        go.name = piece;
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * prefab.transform.localRotation;
        go.transform.localScale = prefab.transform.localScale * scale;
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
        {
            Material[] mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++) mats[i] = Skin(mats[i] != null ? mats[i].name : "", tint, household);
            r.sharedMaterials = mats;
        }
        return go.transform;
    }

    // Which way a stone quoin is turned at each corner (-x-z, +x-z, -x+z, +x+z) so it wraps the walls.
    static readonly float[] QuoinYaw = { 0f, 270f, 90f, 180f };

    // Builds a house. Its front (the wall with the door) looks down the returned object's +z.
    public static GameObject House(Plan p, Transform parent, string name = "House")
    {
        var root = new GameObject(name);
        root.transform.SetParent(parent, false);
        // A side door means the whole body is turned a quarter, so the door still ends up in front.
        var body = new GameObject("Body").transform;
        body.SetParent(root.transform, false);
        if (p.sideDoor) body.localRotation = Quaternion.Euler(0f, -90f, 0f);
        var rng = new System.Random(p.seed * 7919 + p.w * 31 + p.d);
        int doorSide = p.sideDoor ? 1 : 0;

        // The window this household favours: one kind downstairs, another above.
        string[] kinds = { "Wide_Flat", "Thin_Round", "Wide_Round" };
        string low = kinds[rng.Next(kinds.Length)], high = kinds[rng.Next(kinds.Length)];
        bool shutters = rng.Next(3) > 0;
        string doorShape = rng.Next(2) == 0 ? "Flat" : "Round";
        string doorLeaf = "Door_" + new[] { 1, 2, 4, 8 }[rng.Next(4)] + "_" + doorShape;

        for (int storey = 0; storey < p.floors; storey++)
        {
            bool stone = p.stoneAll || (p.stoneGround && storey == 0);
            string skin = stone ? "UnevenBrick" : "Plaster";
            float y = storey * Storey;
            for (int side = 0; side < 4; side++)
            {
                int length = side % 2 == 0 ? p.w : p.d;
                float reach = (side % 2 == 0 ? p.d : p.w) * 0.5f;
                float yaw = side * 90f;
                Quaternion turn = Quaternion.Euler(0f, yaw, 0f);
                int count = length / 2;
                for (int i = 0; i < count; i++)
                {
                    Vector3 at = turn * new Vector3(-length * 0.5f + 1f + i * 2f, y, reach);
                    bool doorBay = side == doorSide && i == (count - 1) / 2;
                    bool isDoor = p.door && storey == 0 && doorBay;
                    if (isDoor)
                    {
                        Put(body, "Wall_" + skin + "_Door_" + doorShape, at, yaw, p.tint);
                        // The leaf hangs from its hinge: half a door to one side of the opening.
                        Put(body, doorLeaf, at + turn * new Vector3(0.53f, 0f, -0.04f), yaw, p.tint, 1f, p.dark ? Unlit : rng.Next(Unlit));
                        // A stone step at the threshold.
                        Put(body, "Prop_ExteriorBorder_Straight1", at, yaw, p.tint);
                        continue;
                    }
                    if (p.balcony && storey == 1 && doorBay && !stone)
                    {
                        // A door onto a balcony: boards on two braces, a rail on three sides.
                        Put(body, "Wall_Plaster_Door_Flat", at, yaw, p.tint);
                        Put(body, "Door_1_Flat", at + turn * new Vector3(0.53f, 0f, -0.04f), yaw, p.tint, 1f, p.dark ? Unlit : rng.Next(Unlit));
                        Put(body, "Floor_WoodDark_Half3", at + turn * new Vector3(0f, 0.02f, 1.02f), yaw, p.tint);
                        Put(body, "Balcony_Simple_Straight", at + turn * new Vector3(0f, 0f, 0.02f), yaw, p.tint);
                        Put(body, "Balcony_Simple_Straight", at + turn * new Vector3(0f, 0f, 0.02f), yaw + 90f, p.tint);
                        Put(body, "Balcony_Simple_Straight", at + turn * new Vector3(0f, 0f, 0.02f), yaw - 90f, p.tint);
                        Put(body, "Prop_Support", at + turn * new Vector3(-0.85f, -1.62f, 0f), yaw, p.tint, 0.55f);
                        Put(body, "Prop_Support", at + turn * new Vector3(0.85f, -1.62f, 0f), yaw, p.tint, 0.55f);
                        continue;
                    }

                    // Towers are mostly blank stone with a slit here and there; houses look out on every side but the back.
                    bool window = p.tower ? rng.Next(100) < 30 && storey > 0
                        : p.lancets ? side % 2 == 1 || storey > 0
                        : p.bare ? false
                        : side == 2 ? rng.Next(100) < 35
                        : storey == 0 ? rng.Next(100) < 65 : rng.Next(100) < 80;
                    if (!window)
                    {
                        string plain = !stone && rng.Next(100) < 45 ? "Wall_Plaster_WoodGrid" : "Wall_" + skin + "_Straight";
                        Put(body, plain, at, yaw, p.tint);
                        continue;
                    }
                    string kind = p.tower || p.lancets ? "Thin_Round" : storey == 0 ? low : high;
                    Put(body, "Wall_" + skin + "_Window_" + kind, at, yaw, p.tint);
                    Put(body, "Window_" + kind + "1", at, yaw, p.tint, 1f, p.dark ? Unlit : rng.Next(Unlit));
                    if (shutters && !p.tower && !p.lancets && rng.Next(100) < 70)
                        Put(body, "WindowShutters_" + kind + (rng.Next(4) == 0 ? "_Closed" : "_Open"), at, yaw, p.tint);
                }
            }
            // A timber post up every corner; dressed stone quoins where the building is stone throughout.
            for (int c = 0; c < 4; c++)
            {
                var corner = new Vector3((c % 2 == 0 ? -1 : 1) * p.w * 0.5f, y, (c < 2 ? -1 : 1) * p.d * 0.5f);
                if (p.stoneAll) Put(body, "Corner_Exterior_Brick", corner, QuoinYaw[c], p.tint);
                else Put(body, stone ? "Corner_ExteriorWide_Wood" : "Corner_Exterior_Wood", corner, 0f, p.tint);
            }
        }

        float top = p.floors * Storey;
        if (p.bare)
        {
            // A wall walk: a timber cap along the top, and stone merlons standing on it.
            for (int side = 0; side < 4; side++)
            {
                int length = side % 2 == 0 ? p.w : p.d;
                float reach = (side % 2 == 0 ? p.d : p.w) * 0.5f;
                Quaternion turn = Quaternion.Euler(0f, side * 90f, 0f);
                for (int k = 0; k < length; k += 2)
                    Put(body, "Prop_Brick" + (1 + rng.Next(4)), turn * new Vector3(-length * 0.5f + 0.5f + k, top + 0.3f, reach - 0.1f), side * 90f, p.tint, 2.4f);
                for (int i = 0; i < length / 2; i++)
                    Put(body, "Wall_BottomCover", turn * new Vector3(-length * 0.5f + 1f + i * 2f, top, reach), side * 90f, p.tint);
            }
        }
        else if (p.tower)
        {
            Put(body, "Roof_Tower_RoundTiles", new Vector3(0f, top, 0f), 0f, p.tint, p.roofScale);
        }
        else
        {
            Put(body, "Roof_RoundTiles_" + p.w + "x" + p.d, new Vector3(0f, top, 0f), 0f, p.tint);
            Put(body, "Roof_Front_Brick" + p.w, new Vector3(0f, top, p.d * 0.5f), 0f, p.tint);
            Put(body, "Roof_Front_Brick" + p.w, new Vector3(0f, top, -p.d * 0.5f), 180f, p.tint);
            // Chimneys stand through the slope, a little way down from the ridge.
            for (int c = 0; c < p.chimneys; c++)
            {
                float cx = (c % 2 == 0 ? 1f : -1f) * p.w * 0.22f;
                float cz = (c == 0 ? -1f : 1f) * (p.d * 0.5f - 1.6f);
                Put(body, c % 2 == 0 ? "Prop_Chimney" : "Prop_Chimney2", new Vector3(cx, top + p.w * 0.16f, cz), 0f, p.tint);
            }
            // Dormers stand out of the slope on either side, low down near the eaves.
            if (p.dormers && p.w >= 6)
            {
                float ridge = p.w == 6 ? 4.89f : 6f;
                float out0 = p.w * 0.5f - 0.5f, up = ridge * 0.5f / (p.w * 0.5f);
                for (float z = p.d >= 10 ? -p.d * 0.25f : 0f; z <= p.d * 0.26f; z += p.d >= 10 ? p.d * 0.5f : 99f)
                {
                    Put(body, "Roof_Dormer_RoundTile", new Vector3(-out0, top + up, z), 0f, p.tint, 1f, p.dark ? Unlit : rng.Next(Unlit));
                    Put(body, "Roof_Dormer_RoundTile", new Vector3(out0, top + up, z), 180f, p.tint, 1f, p.dark ? Unlit : rng.Next(Unlit));
                }
            }
        }

        if (p.vines)
        {
            // Ivy hanging from under the eaves on a couple of walls.
            string[] vines = { "Prop_Vine1", "Prop_Vine2", "Prop_Vine4", "Prop_Vine5", "Prop_Vine6" };
            for (int v = 0; v < 2 + rng.Next(2); v++)
            {
                int side = rng.Next(4);
                int length = side % 2 == 0 ? p.w : p.d;
                float reach = (side % 2 == 0 ? p.d : p.w) * 0.5f;
                Quaternion turn = Quaternion.Euler(0f, side * 90f, 0f);
                float along = ((float)rng.NextDouble() - 0.5f) * (length - 1.6f);
                Put(body, vines[rng.Next(vines.Length)], turn * new Vector3(along, top - 0.4f - (float)rng.NextDouble() * 0.6f, reach + 0.1f), side * 90f, p.tint);
            }
        }

        // Solid where the walls are.
        var wall = root.AddComponent<BoxCollider>();
        float fw = p.sideDoor ? p.d : p.w, fd = p.sideDoor ? p.w : p.d;
        wall.center = new Vector3(0f, top * 0.5f, 0f);
        wall.size = new Vector3(fw + 0.2f, top, fd + 0.2f);
        return root;
    }

    // How far the front wall stands from the middle of a house, how wide that wall is, and how
    // far along it (to the right, seen from inside) the door is.
    public static float FrontDepth(Plan p) => (p.sideDoor ? p.w : p.d) * 0.5f;

    public static float FrontWidth(Plan p) => p.sideDoor ? p.d : p.w;

    public static float DoorOffset(Plan p)
    {
        int count = (int)FrontWidth(p) / 2;
        return -FrontWidth(p) * 0.5f + 1f + (count - 1) / 2 * 2f;
    }

    // Call once a building is where it will stay: its many pieces are drawn as a few.
    // Anything under a part named "Sails" is left free to move.
    public static void Settle(GameObject building)
    {
        if (!Application.isPlaying) return;
        var still = new List<GameObject>();
        foreach (MeshFilter mf in building.GetComponentsInChildren<MeshFilter>())
        {
            bool moving = false;
            for (Transform t = mf.transform; t != null && t != building.transform; t = t.parent)
                if (t.name == "Sails") { moving = true; break; }
            if (!moving) still.Add(mf.gameObject);
        }
        GeneratedResources.Combine(building.transform, still.ToArray());
    }
}
