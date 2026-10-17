using System.Collections.Generic;
using UnityEngine;

// The bigger works built from the village kit: the well, the windmill, the church
// and the castle. And the small ones that stand about a town and a road: lamp
// posts, torches, banners, stone pillars, headstones, lean-tos, fences.
public static partial class Village
{
    // A landmark, and what the town needs to know to stand it somewhere: how far its front
    // is from its middle, how far along the front the door is, and how much room it takes.
    public class Site
    {
        public GameObject root;
        public float depth, door, reach;
    }

    // The kit's models lie on their sides inside their own transform: local z is up and
    // local y is depth. Stretches a piece in world terms.
    static Transform Stretch(Transform t, float across, float up, float deep)
    {
        if (t != null) t.localScale = Vector3.Scale(t.localScale, new Vector3(across, deep, up));
        return t;
    }

    // Lays an upright piece on its side: what stood up now runs along the piece's own -x.
    static Transform Lay(Transform t, float roll = 90f)
    {
        if (t != null) t.localRotation = Quaternion.Euler(0f, 0f, roll) * t.localRotation;
        return t;
    }

    static Transform Prop(Transform root, string prop, Vector3 pos, float yaw, float scale = 1f)
    {
        GameObject go = Humans.Furniture(prop, true);
        if (go == null) return null;
        go.transform.SetParent(root, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * go.transform.localRotation;
        go.transform.localScale *= scale;
        return go.transform;
    }

    static GameObject Root(string name, Transform parent)
    {
        var root = new GameObject(name);
        root.transform.SetParent(parent, false);
        return root;
    }

    // ------------------------------------------------------------ the well

    // A round kerb of fitted stone, dark water in it, two posts, a windlass and a shingled roof.
    public static GameObject Well(Transform parent)
    {
        GameObject root = Root("Well", parent);
        Transform t = root.transform;
        var rng = new System.Random(7);
        const float radius = 0.98f;
        const int stones = 13;
        for (int course = 0; course < 4; course++)
            for (int k = 0; k < stones; k++)
            {
                float a = (k + course * 0.5f) * 360f / stones;
                Vector3 at = Quaternion.Euler(0f, a, 0f) * new Vector3(0f, 0.13f + course * 0.27f, radius);
                Put(t, "Prop_Brick" + (1 + rng.Next(4)), at, a, Color.white, 1.28f);
            }

        GameObject water = ToonKit.Primitive(PrimitiveType.Cylinder, new Color(0.06f, 0.11f, 0.16f), t, true);
        water.name = "Water";
        water.transform.localPosition = new Vector3(0f, 0.62f, 0f);
        water.transform.localScale = new Vector3(1.86f, 0.02f, 1.86f);
        var wr = water.GetComponent<Renderer>();
        wr.sharedMaterial = GeneratedResources.Own(new Material(wr.sharedMaterial), wr.transform);
        wr.sharedMaterial.SetFloat("_Spec", 0.9f);
        wr.sharedMaterial.SetFloat("_Rim", 0.8f);
        wr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        Stretch(Put(t, "Corner_ExteriorWide_Wood", new Vector3(-1.3f, 0f, 0f), 0f, Color.white), 1f, 0.92f, 1f);
        Stretch(Put(t, "Corner_ExteriorWide_Wood", new Vector3(1.3f, 0f, 0f), 0f, Color.white), 1f, 0.92f, 1f);
        // The windlass: a beam from post to post, with the rope wound on it.
        Lay(Stretch(Put(t, "Corner_Exterior_Wood", new Vector3(1.3f, 1.85f, 0f), 0f, Color.white), 1f, 0.87f, 1f));
        Put(t, "Roof_Wooden_2x1", new Vector3(0f, 2.68f, 0f), 0f, Color.white, 1.2f);
        Put(t, "Roof_Wooden_2x1", new Vector3(0f, 2.68f, 0f), 180f, Color.white, 1.2f);
        Prop(t, "Bucket_Wooden_1", new Vector3(0.35f, 1.18f, 0.9f), 30f);
        Prop(t, "Rope_1", new Vector3(-1.5f, 0f, 0.9f), 0f);

        var c = root.AddComponent<CapsuleCollider>();
        c.radius = 1.3f;
        c.height = 3f;
        c.center = new Vector3(0f, 1.5f, 0f);
        return root;
    }

    // ------------------------------------------------------------ the windmill

    // A mill tower, stone below and timber above, with four lattice sails turning on the front.
    public static Site Windmill(Transform parent, int seed)
    {
        GameObject root = Root("Windmill", parent);
        var plan = new Plan { w = 4, d = 4, floors = 4, seed = seed, tower = true, chimneys = 0, vines = false };
        House(plan, root.transform, "Mill");

        var sails = new GameObject("Sails");
        sails.transform.SetParent(root.transform, false);
        sails.transform.localPosition = new Vector3(0f, plan.floors * Storey - 1.2f, 2.75f);
        Transform hub = sails.transform;
        // The axle, out of the wall; then four arms, each a beam with a lattice down one side.
        Lay(Stretch(Put(hub, "Corner_ExteriorWide_Wood", new Vector3(0f, 0f, 0.25f), 90f, Color.white), 1f, 0.3f, 1f));
        for (int k = 0; k < 4; k++)
        {
            var arm = new GameObject("Arm").transform;
            arm.SetParent(hub, false);
            arm.localRotation = Quaternion.Euler(0f, 0f, k * 90f);
            Stretch(Put(arm, "Corner_Exterior_Wood", Vector3.zero, 0f, Color.white), 0.8f, 2.05f, 0.8f);
            for (int s = 0; s < 2; s++)
                Lay(Put(arm, "Prop_WoodenFence_Single", new Vector3(-0.06f, 2.2f + s * 2.06f, 0f), 0f, Color.white));
        }
        sails.AddComponent<Turn>().degreesPerSecond = 16f;
        return new Site { root = root, depth = 2f, door = DoorOffset(plan), reach = 4.2f };
    }

    // ------------------------------------------------------------ the church

    // A stone nave with lancet windows, a bell tower at the west door, and an iron-railed yard.
    public static Site Church(Transform parent, int seed)
    {
        GameObject root = Root("Church", parent);
        var nave = new Plan { w = 6, d = 12, floors = 2, seed = seed, stoneAll = true, lancets = true, chimneys = 0, door = false, vines = true };
        House(nave, root.transform, "Nave");
        var tower = new Plan { w = 4, d = 4, floors = 6, seed = seed + 1, tower = true, stoneAll = true, chimneys = 0, vines = false };
        GameObject bell = House(tower, root.transform, "Bell tower");
        bell.transform.localPosition = new Vector3(0f, 0f, 8f);

        // The churchyard: iron railings down both sides, and the dead of Brushwick on the east.
        var yard = Root("Yard", root.transform).transform;
        for (int side = -1; side <= 1; side += 2)
            for (int k = 0; k < 8; k++)
                Put(yard, k % 3 == 1 ? "Prop_MetalFence_Ornament" : "Prop_MetalFence_Simple", new Vector3(side * 6.4f, 0f, -5f + k * 1.95f * 0.75f), 90f, Color.white, 0.75f);
        var rng = new System.Random(seed);
        for (int k = 0; k < 6; k++)
            Headstone(yard, new Vector3(4.6f + (k % 2) * 1.1f, 0f, -4.2f + k * 1.5f), 80f + rng.Next(30), 0.8f + (float)rng.NextDouble() * 0.35f);
        return new Site { root = root, depth = 10f, door = DoorOffset(tower), reach = 9.5f };
    }

    // ------------------------------------------------------------ the castle

    // A keep with a tower at each corner, and in front of it a walled court with a gate.
    public static Site Castle(Transform parent, int seed)
    {
        GameObject root = Root("Castle", parent);
        Color stone = new Color(0.9f, 0.9f, 0.96f);
        var keep = new Plan { w = 8, d = 12, floors = 3, seed = seed, stoneAll = true, chimneys = 2, dormers = true, vines = true, tint = stone, door = true };
        House(keep, root.transform, "Keep").transform.localPosition = new Vector3(0f, 0f, -4f);
        for (int c = 0; c < 4; c++)
        {
            var tower = new Plan { w = 4, d = 4, floors = c < 2 ? 6 : 5, seed = seed + 1 + c, tower = true, stoneAll = true, chimneys = 0, vines = false, door = false, tint = stone };
            House(tower, root.transform, "Tower").transform.localPosition = new Vector3(c % 2 == 0 ? -5f : 5f, 0f, c < 2 ? -11f : 3f);
        }
        // The court: a wall two storeys high with a walk along the top, and the gate in the middle of it.
        var front = new Plan { w = 14, d = 2, floors = 2, seed = seed + 9, stoneAll = true, bare = true, chimneys = 0, vines = false, tint = stone };
        House(front, root.transform, "Gate wall").transform.localPosition = new Vector3(0f, 0f, 9f);
        for (int side = -1; side <= 1; side += 2)
        {
            var wing = new Plan { w = 2, d = 4, floors = 2, seed = seed + 11 + side, stoneAll = true, bare = true, chimneys = 0, vines = false, door = false, tint = stone };
            House(wing, root.transform, "Wall").transform.localPosition = new Vector3(side * 6f, 0f, 6f);
        }
        // The house's colours either side of the gate.
        Prop(root.transform, "Banner_1", new Vector3(-2.4f, 4.6f, 10.12f), -90f, 1.4f);
        Prop(root.transform, "Banner_2", new Vector3(3.9f, 4.6f, 10.12f), -90f, 1.4f);
        return new Site { root = root, depth = 10f, door = DoorOffset(front), reach = 13f };
    }

    // ------------------------------------------------------------ small works

    static void Headstone(Transform root, Vector3 pos, float yaw, float size)
    {
        // The stone's own middle is its pivot: stand it so a hand's breadth is in the earth.
        Stretch(Put(root, "Prop_Brick3", pos + Vector3.up * 0.42f * size, yaw, new Color(0.8f, 0.82f, 0.86f), size), 1.7f, 4.4f, 0.8f);
    }

    // Where a composite's lamp burns, for the halo the game hangs on it: zero for things that give no light.
    public static Vector3 LightOf(string name)
    {
        switch (name)
        {
            case "LampPost": return new Vector3(0f, 2.55f, 1.02f);
            case "Torch": return new Vector3(0f, 1.95f, 0.36f);
            case "Campfire": return new Vector3(0f, 0.9f, 0f);
            case "Pavilion": return new Vector3(0f, 2.6f, 0f);
        }
        return Vector3.zero;
    }

    // Makes one of the small works by name, or failing that a single piece of the kit.
    public static Transform Make(string name, Transform parent)
    {
        Transform t = Root(name, parent).transform;
        switch (name)
        {
            case "LampPost":
                // A timber post with an iron lantern on a bracket.
                Stretch(Put(t, "Corner_ExteriorWide_Wood", Vector3.zero, 0f, Color.white), 0.7f, 1.1f, 0.7f);
                Prop(t, "Lantern_Wall", new Vector3(0f, 1.9f, 0.08f), 0f);
                break;
            case "Torch":
                Stretch(Put(t, "Corner_Exterior_Wood", Vector3.zero, 0f, Color.white), 0.9f, 0.62f, 0.9f);
                Prop(t, "Torch_Metal", new Vector3(0f, 1.6f, 0.05f), 0f);
                break;
            case "Campfire":
                Prop(t, "Cauldron", Vector3.zero, 0f);
                for (int k = 0; k < 5; k++)
                    Put(t, "Prop_Brick" + (1 + k % 4), Quaternion.Euler(0f, k * 72f, 0f) * new Vector3(0f, 0.08f, 0.75f), k * 72f, new Color(0.6f, 0.6f, 0.62f), 1.1f);
                break;
            case "Pillar":
                // A square stone pier: four dressed quoins back to back.
                for (int c = 0; c < 4; c++)
                    Put(t, "Corner_Exterior_Brick", new Vector3(c % 2 == 0 ? -0.17f : 0.17f, 0f, c < 2 ? -0.17f : 0.17f), QuoinYaw[c], Color.white);
                break;
            case "Rubble":
                for (int k = 0; k < 7; k++)
                    Put(t, "Prop_Brick" + (1 + k % 4), Quaternion.Euler(0f, k * 51f, 0f) * new Vector3(0f, 0.2f + (k % 3) * 0.16f, 0.25f + k * 0.13f), k * 67f, Color.white, 2.2f + k % 3 * 0.5f);
                Prop(t, "Vase_Rubble_Medium", new Vector3(0.7f, 0f, -0.5f), 0f, 1.4f);
                break;
            case "Headstone":
                Headstone(t, Vector3.zero, 0f, 1f);
                break;
            case "LeanTo":
                // A shingled roof on two posts, open to the fire: somewhere to sleep out of the rain.
                Stretch(Put(t, "Corner_Exterior_Wood", new Vector3(-1.55f, 0f, 2.1f), 0f, Color.white), 1f, 1.02f, 1f);
                Stretch(Put(t, "Corner_Exterior_Wood", new Vector3(1.55f, 0f, 2.1f), 0f, Color.white), 1f, 1.02f, 1f);
                Stretch(Put(t, "Corner_Exterior_Wood", new Vector3(-1.55f, 0f, 0.1f), 0f, Color.white), 1f, 0.44f, 1f);
                Stretch(Put(t, "Corner_Exterior_Wood", new Vector3(1.55f, 0f, 0.1f), 0f, Color.white), 1f, 0.44f, 1f);
                Put(t, "Roof_Wooden_2x1", new Vector3(0f, 1.4f, 0f), 0f, Color.white, 1.45f);
                Prop(t, "Bed_Twin2", new Vector3(0f, 0f, 0.9f), 90f, 0.9f);
                break;
            case "Pavilion":
                // Four posts and a shingled roof over a table, with a chandelier hanging under the ridge.
                for (int c = 0; c < 4; c++)
                    Stretch(Put(t, "Corner_Exterior_Wood", new Vector3(c % 2 == 0 ? -2.1f : 2.1f, 0f, c < 2 ? -1.5f : 1.5f), 0f, Color.white), 1f, 1.05f, 1f);
                Put(t, "Roof_Wooden_2x1", new Vector3(0f, 3.1f, 0f), 0f, Color.white, 2f);
                Put(t, "Roof_Wooden_2x1", new Vector3(0f, 3.1f, 0f), 180f, Color.white, 2f);
                Prop(t, "Chandelier", new Vector3(0f, 3.75f, 0f), 0f);
                break;
            case "Banner":
                Stretch(Put(t, "Corner_Exterior_Wood", Vector3.zero, 0f, Color.white), 0.8f, 1.25f, 0.8f);
                Prop(t, "Banner_1", new Vector3(0f, 2.75f, 0f), 0f);
                break;
            case "Fence":
                Put(t, "Prop_WoodenFence_Single", Vector3.zero, 0f, Color.white);
                Put(t, "Prop_WoodenFence_Extension1", new Vector3(2.04f, 0f, 0f), 0f, Color.white);
                break;
            default:
                if (Put(t, name, Vector3.zero, 0f, Color.white) != null) break;
                Object.Destroy(t.gameObject);
                return null;
        }
        return t;
    }

    // A run of fence between two points: wooden rails, or iron ones.
    public static void FenceLine(Transform root, Vector3 from, Vector3 to, bool iron = false)
    {
        Vector3 along = to - from;
        float length = along.magnitude;
        if (length < 0.5f) return;
        float yaw = Mathf.Atan2(along.x, along.z) * Mathf.Rad2Deg - 90f;
        float step = iron ? 1.95f * 0.5f : 2.04f;
        int count = Mathf.Max(1, Mathf.RoundToInt(length / step));
        for (int i = 0; i < count; i++)
        {
            Vector3 at = from + along * ((i + 0.5f) / count);
            if (iron) Put(root, i % 3 == 1 ? "Prop_MetalFence_Ornament" : "Prop_MetalFence_Simple", at, yaw, Color.white, 0.5f);
            else Put(root, i % 2 == 0 ? "Prop_WoodenFence_Single" : "Prop_WoodenFence_Extension" + (1 + i / 2 % 2), at, yaw, Color.white);
        }
    }
}
