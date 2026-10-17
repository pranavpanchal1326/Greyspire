using UnityEngine;

// The first-zone environment pass (package V01, see GREYSPIRE_FIRST_ZONE_ART_GUIDE.md). The
// route prototypes proved collision and navigation with flat-coloured boxes; this dresses them
// in the village kit's own stone, paving and timber without moving a single proven surface:
// every existing collider stays as it was, only what is drawn changes, plus details placed
// clear of the walking lines. Generated meshes are owned by the floor they belong to.
public partial class Game
{
    // Kit surfaces, and how many metres one repeat of each covers.
    const string StoneTex = "T_Brick_BaseColor", PavingTex = "T_UnevenBrick_BaseColor", WoodTex = "T_WoodTrim_BaseColor";
    const float StoneTile = 2f, PavingTile = 2f, WoodTile = 1.5f;
    static readonly Color StoneTint = new Color(0.86f, 0.86f, 0.82f), PavingTint = new Color(0.92f, 0.9f, 0.84f),
        WoodTint = new Color(0.86f, 0.74f, 0.62f), UnderTint = new Color(1f, 0.97f, 0.9f);
    static readonly Color LanternLight = new Color(1f, 0.78f, 0.5f);

    static Texture2D KitTex(string name) => Resources.Load<Texture2D>("Village/Tex/" + name);

    static Material Surface(string tex, Color tint) => ToonKit.Mat(tint, tex: KitTex(tex));

    // A unit cube whose texture repeats every tile metres at the given world size, with tangents
    // so the kit's normal maps light it properly. Drawn in place of a primitive cube's mesh; the
    // cube's own BoxCollider is untouched.
    static Mesh TiledBox(Vector3 size, float tile)
    {
        var v = new Vector3[24]; var n = new Vector3[24]; var uv = new Vector2[24]; var t = new int[36];
        // Each face: its normal, and the two axes (and their world lengths) its texture runs along.
        var faces = new (Vector3 normal, Vector3 u, Vector3 w, float uLen, float wLen)[]
        {
            (Vector3.up, Vector3.right, Vector3.forward, size.x, size.z),
            (Vector3.down, Vector3.right, Vector3.back, size.x, size.z),
            (Vector3.forward, Vector3.left, Vector3.up, size.x, size.y),
            (Vector3.back, Vector3.right, Vector3.up, size.x, size.y),
            (Vector3.right, Vector3.forward, Vector3.up, size.z, size.y),
            (Vector3.left, Vector3.back, Vector3.up, size.z, size.y),
        };
        for (int f = 0; f < 6; f++)
        {
            var (normal, u, w, uLen, wLen) = faces[f];
            Vector3 c = normal * 0.5f;
            int b = f * 4;
            v[b] = c - u * 0.5f - w * 0.5f; v[b + 1] = c + u * 0.5f - w * 0.5f; v[b + 2] = c + u * 0.5f + w * 0.5f; v[b + 3] = c - u * 0.5f + w * 0.5f;
            uv[b] = Vector2.zero; uv[b + 1] = new Vector2(uLen / tile, 0f); uv[b + 2] = new Vector2(uLen / tile, wLen / tile); uv[b + 3] = new Vector2(0f, wLen / tile);
            for (int k = 0; k < 4; k++) n[b + k] = normal;
            int[] tri = { b, b + 2, b + 1, b, b + 3, b + 2 };
            // Wind each face so it faces outward.
            if (Vector3.Dot(Vector3.Cross(v[b + 2] - v[b], v[b + 1] - v[b]), normal) < 0f) tri = new[] { b, b + 1, b + 2, b, b + 2, b + 3 };
            System.Array.Copy(tri, 0, t, f * 6, 6);
        }
        var mesh = new Mesh { name = "Dressed box", vertices = v, normals = n, uv = uv, triangles = t };
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return mesh;
    }

    // Who frees a dressed mesh. An object that is already inactive (an opened shortcut gate)
    // never receives OnDestroy, so its mesh would outlive the floor: the floor owns it instead.
    Transform Owner(GameObject drawn) => drawn.activeInHierarchy ? drawn.transform : LevelRoot;

    // Draws a primitive box (a route prototype) in a kit surface. Its collider is untouched.
    void Dress(GameObject box, string tex, float tile, Color tint)
    {
        if (box == null) return;
        var filter = box.GetComponent<MeshFilter>();
        if (filter == null) return;
        filter.sharedMesh = GeneratedResources.Own(TiledBox(box.transform.lossyScale, tile), Owner(box));
        box.GetComponent<Renderer>().sharedMaterial = Surface(tex, tint);
    }

    // Draws a ramp (its 8 corners: top quad 0-3 from the low end, bottom 4-7) with a paved walking
    // surface and stone sides. The ramp's own collision mesh is untouched.
    void DressRamp(GameObject ramp)
    {
        var filter = ramp != null ? ramp.GetComponent<MeshFilter>() : null;
        if (filter == null || filter.sharedMesh == null || filter.sharedMesh.vertexCount != 8) return;
        Vector3[] c = filter.sharedMesh.vertices;
        var v = new System.Collections.Generic.List<Vector3>();
        var uv = new System.Collections.Generic.List<Vector2>();
        var top = new System.Collections.Generic.List<int>();
        var sides = new System.Collections.Generic.List<int>();
        void Quad(int a, int b2, int d, int e, System.Collections.Generic.List<int> into, bool paved)
        {
            int s = v.Count;
            Vector3[] q = { c[a], c[b2], c[d], c[e] };
            Vector3 normal = Vector3.Cross(q[1] - q[0], q[2] - q[0]).normalized;
            foreach (Vector3 p in q)
            {
                v.Add(p);
                // Paving runs along the slope; stone sides run along their own face.
                if (paved) uv.Add(new Vector2(p.x / PavingTile, new Vector2(p.z, p.y).magnitude * Mathf.Sign(p.z) / PavingTile));
                else if (Mathf.Abs(normal.x) > 0.5f) uv.Add(new Vector2(p.z / StoneTile, p.y / StoneTile));
                else uv.Add(new Vector2(p.x / StoneTile, p.y / StoneTile));
            }
            into.AddRange(new[] { s, s + 1, s + 2, s, s + 2, s + 3 });
        }
        // Same corner order and winding as the collision mesh's triangles.
        Quad(0, 2, 3, 1, top, true);       // walking surface
        Quad(0, 4, 6, 2, sides, false);    // -x side
        Quad(1, 3, 7, 5, sides, false);    // +x side
        Quad(0, 1, 5, 4, sides, false);    // low end
        Quad(2, 6, 7, 3, sides, false);    // high end
        var mesh = new Mesh { name = ramp.name + " dressed" };
        mesh.SetVertices(v);
        mesh.SetUVs(0, uv);
        mesh.subMeshCount = 2;
        mesh.SetTriangles(top, 0);
        mesh.SetTriangles(sides, 1);
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        filter.sharedMesh = GeneratedResources.Own(mesh, Owner(ramp));
        ramp.GetComponent<Renderer>().sharedMaterials = new[] { Surface(PavingTex, PavingTint), Surface(StoneTex, StoneTint) };
    }

    // A hanging lantern with a soft, shadowless fill: light where a lower route would be gloomy.
    void Lantern(Vector3 at, float yaw, float range)
    {
        var holder = new GameObject("Lantern");
        holder.transform.SetParent(LevelRoot, false);
        holder.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
        Transform lamp = Furnishing("Lantern_Wall", holder.transform);
        if (lamp != null) lamp.localScale *= 1.4f;
        AddGlow(holder.transform, 0f, LanternLight, 2.4f).transform.localPosition = new Vector3(0f, 0.15f, 0.25f);
        var light = new GameObject("Lantern light").AddComponent<Light>();
        light.transform.SetParent(holder.transform, false);
        light.transform.localPosition = new Vector3(0f, -0.2f, 0.6f);
        light.type = LightType.Point;
        light.range = range;
        light.intensity = 1.6f;
        light.color = LanternLight;
        light.shadows = LightShadows.None;
    }

    // A loose detail from the kit, with no collider: never in anyone's way.
    void Detail(string piece, Vector3 at, float yaw, float scale = 1f, Color? tint = null)
    {
        Village.Put(LevelRoot, piece, at, yaw, tint ?? Color.white, scale);
    }

    // ------------------------------------------------------------ floor 2, the Millrace

    void DressMillrace(System.Random rng)
    {
        Dress(LevelRoot.Find("Millrace bridge")?.gameObject, PavingTex, PavingTile, PavingTint);
        DressRamp(LevelRoot.Find("Millrace south ramp")?.gameObject);
        DressRamp(LevelRoot.Find("Millrace north ramp")?.gameObject);
        foreach (Transform t in LevelRoot)
        {
            if (t.name == "Millrace bridge parapet" || t.name == "Millrace sluice pier") Dress(t.gameObject, StoneTex, StoneTile, StoneTint);
            else if (t.name.StartsWith("Millrace route") || t.name.StartsWith("Millrace winch")) Dress(t.gameObject, WoodTex, WoodTile, WoodTint);
        }
        Dress(millraceGate, WoodTex, WoodTile, WoodTint);

        // Stone piers carry the deck's corners down to the ground, clear of the crossing lane.
        foreach (int sx in new[] { -1, 1 })
            foreach (int sz in new[] { -1, 1 })
                Prop(new PropDef("Pillar", Kit, MillraceDeck - 0.3f, 0.45f), new Vector3(sx * 4.4f, 0f, sz * 6.3f), rng, 1f, 0f);

        // The underpass was nearly black: lanterns on both faces of each sluice pier light the
        // lower crossing from either bank, hung clear of head height.
        foreach (int sz in new[] { -1, 1 })
            foreach (int sx in new[] { -1, 1 })
                Lantern(new Vector3(sx * 0.42f, 3.2f, sz * 4.2f), sx < 0 ? 270f : 90f, 10f);

        // The winch reads as a mechanism: its chain coiled at the foot of the post, a barrel of grease.
        Prop(new PropDef("Chain_Coil", Fur, 0.45f, 0f), new Vector3(-6.3f, 0f, 2.2f), rng, 1f, 30f);
        Prop(new PropDef("Barrel", Fur, 1.1f, 0.4f), new Vector3(-7.9f, 0f, 2.4f), rng, 1f, 20f);
        // A lantern at the route board, so the choice is lit.
        Lantern(new Vector3(4f, 2.25f, -29.9f), 180f, 6f);
        // Worn stone where the ramps meet the meadow.
        for (int i = 0; i < 6; i++)
        {
            float x = -4f + i * 1.6f;
            Detail("Prop_Brick" + (1 + i % 4), new Vector3(x, 0.02f, -27.6f - (i % 2) * 0.4f), i * 37f, 1.6f);
            Detail("Prop_Brick" + (1 + (i + 2) % 4), new Vector3(x, 0.02f, 27.6f + (i % 2) * 0.4f), i * 53f, 1.6f);
        }
    }

    // ------------------------------------------------------------ floor 3, the survey ruin

    void DressSurveyRuin(System.Random rng)
    {
        foreach (string wing in new[] { "South ruin wing", "North ruin wing" })
        {
            Transform w = LevelRoot.Find(wing);
            if (w == null) continue;
            Dress(w.gameObject, StoneTex, StoneTile, StoneTint);
            // Battlements along the top, and fallen stone at the foot: a fortified wall gone to ruin.
            Vector3 c = w.position, s = w.localScale;
            float top = c.y + s.y * 0.5f;
            for (float x = -s.x * 0.5f + 0.6f; x <= s.x * 0.5f - 0.5f; x += 2f)
                foreach (float z in new[] { -s.z * 0.5f + 0.35f, s.z * 0.5f - 0.35f })
                    Merlon(new Vector3(c.x + x, top + 0.5f, c.z + z), (int)((x + 50f) * 7f) % 5 == 0);
            for (float z = -s.z * 0.5f + 1.6f; z <= s.z * 0.5f - 1.5f; z += 2f)
                foreach (float x in new[] { -s.x * 0.5f + 0.35f, s.x * 0.5f - 0.35f })
                    Merlon(new Vector3(c.x + x, top + 0.5f, c.z + z), false);
            foreach (float x in new[] { -9f, -2f, 6f })
            {
                float face = c.z < 0f ? c.z + s.z * 0.5f + 0.9f : c.z - s.z * 0.5f - 0.9f;
                Detail("Prop_Brick" + (1 + (int)(x + 20) % 4), new Vector3(c.x + x, 0.02f, face), x * 23f, 2.2f);
                Detail("Prop_Brick" + (1 + (int)(x + 21) % 4), new Vector3(c.x + x + 0.8f, 0.02f, face + 0.3f), x * 41f, 1.8f);
            }
        }
        Dress(LevelRoot.Find("Survey overlook")?.gameObject, PavingTex, PavingTile, PavingTint);
        Dress(LevelRoot.Find("Overlook outer rail")?.gameObject, StoneTex, StoneTile, StoneTint);
        DressRamp(LevelRoot.Find("South survey ramp")?.gameObject);
        DressRamp(LevelRoot.Find("North survey ramp")?.gameObject);
        Dress(pilotGate, WoodTex, WoodTile, WoodTint);

        // The courier's rest point on the overlook: a lean-to against the wind, clear of the chest
        // and of where the courier stands.
        var rest = new GameObject("Courier rest").transform;
        rest.SetParent(LevelRoot, false);
        rest.SetPositionAndRotation(new Vector3(-22.4f, 3.2f, -5.2f), Quaternion.Euler(0f, 90f, 0f));
        Village.Make("LeanTo", rest);
        Lantern(new Vector3(-19.6f, 5.1f, -3.4f), 270f, 6f);
    }

    // One merlon of a battlement (no collider: nobody walks on top of the ruin).
    void Merlon(Vector3 at, bool broken)
    {
        var m = new GameObject("Merlon", typeof(MeshFilter), typeof(MeshRenderer));
        m.transform.SetParent(LevelRoot, false);
        m.transform.position = broken ? at - Vector3.up * 0.3f : at;
        m.transform.localScale = new Vector3(1.1f, broken ? 0.4f : 1f, 0.7f);
        Dress(m, StoneTex, StoneTile, StoneTint);
    }

    // ------------------------------------------------------------ town services

    // The Field desk (mastery, practice and courier reports) was only a floating label, and the
    // Guild's commission desk had no desk. Both get the town's own furniture, set just behind
    // where the hero stands to use them, facing the square. (Town places go by angle and distance.)
    public const float FieldDeskAngle = 125.5f, FieldDeskDistance = 9.5f, GuildDeskAngle = -30f, GuildDeskDistance = 8.7f;

    void DressTownServices()
    {
        Furnish("Table_Large", FieldDeskAngle, FieldDeskDistance, 90f, 0.9f).name = "Field desk table";
        Dress(Polar(FieldDeskAngle, FieldDeskDistance), FieldDeskAngle + 180f,
            ("BookStand", -0.55f, 0.92f, 0f, 0f), ("Scroll_1", 0.05f, 0.92f, 0.12f, 20f), ("Scroll_2", 0.3f, 0.92f, -0.1f, -30f),
            ("CandleStick", 0.72f, 0.92f, 0.05f, 0f), ("Book_Stack_1", -0.12f, 0.92f, -0.18f, 10f), ("Pouch_Large", 0.5f, 0.92f, 0.2f, 0f));
        Furnish("Chair_1", FieldDeskAngle, FieldDeskDistance + 0.9f, 0f, 0.3f);
        Prop(new PropDef("Banner", Kit, 0f, 0.2f), Polar(FieldDeskAngle - 7f, FieldDeskDistance + 1.4f), null, 1f, FieldDeskAngle + 180f).name = "Field desk banner";
        Prop(new PropDef("LampPost", Kit, 0f, 0.25f), Polar(FieldDeskAngle + 8f, FieldDeskDistance + 0.6f), null, 1f, FieldDeskAngle + 180f);

        Furnish("Table_Large", GuildDeskAngle, GuildDeskDistance, 90f, 0.9f).name = "Guild desk table";
        Dress(Polar(GuildDeskAngle, GuildDeskDistance), GuildDeskAngle + 180f,
            ("Book_5", -0.5f, 0.92f, 0f, 30f), ("Scroll_1", -0.1f, 0.92f, 0.1f, -10f), ("Coin_Pile", 0.35f, 0.92f, -0.1f, 0f),
            ("CandleStick", 0.7f, 0.92f, 0.1f, 0f), ("Key_Metal", 0.15f, 0.93f, 0.2f, 40f));
        Prop(new PropDef("Banner", Kit, 0f, 0.2f), Polar(GuildDeskAngle + 6f, GuildDeskDistance + 2.4f), null, 1f, GuildDeskAngle + 180f).name = "Guild banner";
    }

    // ------------------------------------------------------------ the drained Painter

    // A path of stepping stones from the trail to the statue, and a broken ring of standing rocks
    // round it: the rescue reads as a place before the hero is close. The ring stays well clear of
    // the story camera and the Painter's walk away.
    void FrameStatue(Vector3 statue, System.Random rng)
    {
        Vector3 trail = new Vector3(PathX(statue.z), 0f, statue.z);
        Vector3 toward = statue - trail; toward.y = 0f;
        float length = toward.magnitude;
        Vector3 dir = length > 0.1f ? toward / length : Vector3.right;
        for (float d = 1.6f; d < length - 1.6f; d += 1.1f)
        {
            Vector3 p = trail + dir * d + Vector3.Cross(Vector3.up, dir) * ((int)(d * 3f) % 2 == 0 ? 0.25f : -0.25f);
            Disc(new Color(0.7f, 0.68f, 0.62f), 0.42f, -0.004f, p.x, p.z, new Color(0.62f, 0.6f, 0.55f));
        }
        float facing = Mathf.Atan2(-dir.x, -dir.z) * Mathf.Rad2Deg;
        for (int i = 0; i < 6; i++)
        {
            float a = facing + 50f + i * 52f;           // the ring opens toward the trail
            Vector3 p = statue + Quaternion.Euler(0f, a, 0f) * Vector3.forward * 4.2f;
            Prop(new PropDef("Rock_Medium_" + (1 + i % 3), Nat, 1.1f + (i % 2) * 0.5f, 0.45f), p, rng, 1f, a * 1.7f);
        }
        Glow pool = AddGlow(LevelRoot, 0f, new Color(0.75f, 0.8f, 0.95f), 5f);
        pool.flat = true;
        pool.transform.position = statue + Vector3.up * 0.06f;
        pool.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
    }
}
