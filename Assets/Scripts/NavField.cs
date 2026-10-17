using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// The walkable map of a tower floor. Floors are laid out in code when the hero arrives,
// so the NavMesh is baked then too, from the colliders of whatever was just built:
// the ground is walkable, and every rock, tree, chest, pillar and wall cuts a hole in it.
// F3 draws the map and each agent's route over the game.
public class NavField : MonoBehaviour
{
    public static NavField Current { get; private set; }
    public static bool Show;

    // Agents that are finding their way across this floor, for the F3 read-out.
    public static readonly List<NavFollower> Agents = new List<NavFollower>();

    public float BakeMs { get; private set; }
    public int Triangles { get; private set; }
    public float Area { get; private set; }
    public bool Ready { get; private set; }
    public string Error { get; private set; }
    public Bounds CollectionBounds { get; private set; }
    static readonly int[] profileIds = { -1, -1, -1 };
    static readonly float[] profileRadius = { 0.35f, 0.6f, 1.25f };
    static readonly float[] profileHeight = { 1f, 2.2f, 4.1f };
    readonly List<NavMeshDataInstance> instances = new List<NavMeshDataInstance>();
    readonly List<NavMeshData> data = new List<NavMeshData>();
#if UNITY_EDITOR
    public static System.Action BeforeBuildForTest;
#endif

    public static int Profile(float bodyRadius, float bodyHeight)
    {
        for (int i = 0; i < profileIds.Length; i++)
        {
            if (bodyRadius > profileRadius[i] + 0.001f || bodyHeight > profileHeight[i] + 0.001f) continue;
            if (profileIds[i] == -1)
            {
                var settings = NavMesh.CreateSettings();
                settings.agentRadius = profileRadius[i]; settings.agentHeight = profileHeight[i];
                settings.agentClimb = 0.3f; settings.agentSlope = WorldSupport.MaxWalkSlope;
                profileIds[i] = settings.agentTypeID;
            }
            return profileIds[i];
        }
        return -1; // an oversized body needs an authored compatible surface
    }

    GameObject overlay;
    float radius;

    // Bakes the floor under `root`. `ignore` are colliders that must not cut holes (the hero).
    public static NavField Bake(Transform root, float floorRadius, params Collider[] ignore)
        => BakeInternal(root, floorRadius, false, ignore);
    public static NavField BakeStaged(Transform root, float floorRadius, params Collider[] ignore)
        => BakeInternal(root, floorRadius, true, ignore);
    static NavField BakeInternal(Transform root, float floorRadius, bool staged, Collider[] ignore)
    {
        var field = root.gameObject.AddComponent<NavField>();
        field.radius = floorRadius;

        var timer = System.Diagnostics.Stopwatch.StartNew();
        var disabled = new Dictionary<Collider, bool>();
        foreach (var c in ignore) if (c != null) disabled[c] = c.enabled;
        foreach (var c in root.GetComponentsInChildren<Collider>())
            if (!WorldSupport.Geometry(c)) disabled[c] = c.enabled;
        Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
        bool any = false;
        foreach (var c in root.GetComponentsInChildren<Collider>())
        {
            if (!c.enabled || !WorldSupport.Geometry(c) || disabled.ContainsKey(c)) continue;
            Bounds world = c.bounds;
            for (int k = 0; k < 8; k++)
            {
                Vector3 corner = world.center + Vector3.Scale(world.extents, new Vector3((k & 1) == 0 ? -1 : 1,
                    (k & 2) == 0 ? -1 : 1, (k & 4) == 0 ? -1 : 1));
                Vector3 local = root.InverseTransformPoint(corner);
                if (!any) { bounds = new Bounds(local, Vector3.zero); any = true; } else bounds.Encapsulate(local);
            }
        }
        bounds.Expand(4f);
        field.CollectionBounds = bounds;
        try
        {
            foreach (var pair in disabled) pair.Key.enabled = false;
#if UNITY_EDITOR
            BeforeBuildForTest?.Invoke();
#endif
            if (!any) throw new System.InvalidOperationException("No collision geometry to bake");
            var sources = new List<NavMeshBuildSource>();
            NavMeshBuilder.CollectSources(root, ~0, NavMeshCollectGeometry.PhysicsColliders, 0,
                new List<NavMeshBuildMarkup>(), sources);
            if (staged)
            {
                Current = field;
                field.StartCoroutine(field.BuildOverFrames(sources, bounds));
                return field;
            }
            for (int i = 0; i < profileIds.Length; i++)
            {
                var settings = NavMesh.GetSettingsByID(Profile(profileRadius[i], profileHeight[i]));
                settings.agentRadius = profileRadius[i]; settings.agentHeight = profileHeight[i];
                settings.agentClimb = 0.3f; settings.agentSlope = WorldSupport.MaxWalkSlope;
                settings.overrideVoxelSize = true; settings.voxelSize = 0.12f;
                settings.buildHeightMesh = true;
                var mesh = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, root.position, root.rotation);
                if (mesh == null) throw new System.InvalidOperationException("Navigation build returned no data");
                field.data.Add(mesh); field.instances.Add(NavMesh.AddNavMeshData(mesh, root.position, root.rotation));
            }
            field.Ready = true;
            Current = field;
            field.Measure();
        }
        catch (System.Exception e)
        {
            field.Error = e.Message;
            field.RemoveMeshes();
            Debug.LogWarning("NAV unavailable: " + e.Message);
        }
        finally
        {
            foreach (var pair in disabled) if (pair.Key != null) pair.Key.enabled = pair.Value;
            timer.Stop(); field.BakeMs = (float)timer.Elapsed.TotalMilliseconds;
        }
        Debug.Log("NAV baked: " + field.BakeMs.ToString("0") + " ms, " + field.Triangles + " triangles, "
            + field.Area.ToString("0") + " m2 walkable");
        return field;
    }

    System.Collections.IEnumerator BuildOverFrames(List<NavMeshBuildSource> sources, Bounds bounds)
    {
        // Let collection's finally restore colliders before any worker starts.
        yield return null;
        var timer = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < profileIds.Length; i++)
        {
            var settings = NavMesh.GetSettingsByID(Profile(profileRadius[i], profileHeight[i]));
            settings.agentRadius = profileRadius[i]; settings.agentHeight = profileHeight[i];
            settings.agentClimb = 0.3f; settings.agentSlope = WorldSupport.MaxWalkSlope;
            settings.overrideVoxelSize = true; settings.voxelSize = 0.12f; settings.buildHeightMesh = true;
            var mesh = new NavMeshData(settings.agentTypeID) { position = transform.position, rotation = transform.rotation };
            data.Add(mesh);
            AsyncOperation job = null;
            try { job = NavMeshBuilder.UpdateNavMeshDataAsync(mesh, settings, sources, bounds); }
            catch (System.Exception e) { Error = e.Message; }
            if (job == null)
            { RemoveMeshes(); Debug.LogWarning("NAV unavailable: " + Error); yield break; }
            yield return job;
        }
        foreach (var mesh in data) instances.Add(NavMesh.AddNavMeshData(mesh, transform.position, transform.rotation));
        Ready = true; Measure();
        timer.Stop(); BakeMs += (float)timer.Elapsed.TotalMilliseconds;
        Debug.Log("NAV staged ready: " + BakeMs.ToString("0") + " ms elapsed, " + Triangles + " triangles across three profiles");
    }

    void OnDisable()
    {
        if (Current == this) Current = null;
        Ready = false;
        StopAllCoroutines();
        RemoveMeshes();
        if (overlay != null) Destroy(overlay);
    }

    void RemoveMeshes()
    {
        foreach (var instance in instances) if (instance.valid) instance.Remove();
        instances.Clear();
        foreach (var mesh in data) if (mesh != null) { NavMeshBuilder.Cancel(mesh); Destroy(mesh); }
        data.Clear();
    }

    void Update()
    {
        if (GameInput.NavDebugPressed) Show = !Show;
        if (Show && overlay == null) BuildOverlay();
        if (overlay != null) overlay.SetActive(Show);
    }

    bool Inside(Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 mid = transform.InverseTransformPoint((a + b + c) / 3f);
        mid.y = 0f;
        return mid.magnitude < radius + 0.6f;
    }

    void Measure()
    {
        NavMeshTriangulation t = NavMesh.CalculateTriangulation();
        Triangles = 0;
        Area = 0f;
        for (int i = 0; i < t.indices.Length; i += 3)
        {
            Vector3 a = t.vertices[t.indices[i]], b = t.vertices[t.indices[i + 1]], c = t.vertices[t.indices[i + 2]];
            if (!Inside(a, b, c)) continue;
            Triangles++;
            Area += Vector3.Cross(b - a, c - a).magnitude * 0.5f;
        }
    }

    // The same picture the editor's Navigation view gives, drawn in the game: blue where an
    // agent may stand, darker lines along every polygon edge.
    void BuildOverlay()
    {
        NavMeshTriangulation t = NavMesh.CalculateTriangulation();
        var verts = new List<Vector3>();
        var tris = new List<int>();
        var lines = new List<int>();
        for (int i = 0; i < t.indices.Length; i += 3)
        {
            Vector3 a = t.vertices[t.indices[i]], b = t.vertices[t.indices[i + 1]], c = t.vertices[t.indices[i + 2]];
            if (!Inside(a, b, c)) continue;
            int n = verts.Count;
            verts.Add(a + Vector3.up * 0.06f); verts.Add(b + Vector3.up * 0.06f); verts.Add(c + Vector3.up * 0.06f);
            tris.Add(n); tris.Add(n + 1); tris.Add(n + 2);
            lines.Add(n); lines.Add(n + 1); lines.Add(n + 1); lines.Add(n + 2); lines.Add(n + 2); lines.Add(n);
        }

        overlay = new GameObject("NavMesh Overlay");
        overlay.transform.SetParent(transform, false);
        Part("Fill", verts, tris, MeshTopology.Triangles, new Color(0.16f, 0.56f, 1f, 0.34f));
        Part("Edges", verts, lines, MeshTopology.Lines, new Color(0.05f, 0.25f, 0.6f, 0.75f));
    }

    void Part(string name, List<Vector3> verts, List<int> indices, MeshTopology topology, Color colour)
    {
        var go = new GameObject(name);
        go.transform.SetParent(overlay.transform, false);
        var mesh = GeneratedResources.Own(new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 }, go.transform);
        mesh.SetVertices(verts);
        mesh.SetIndices(indices, topology, 0);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = GeneratedResources.Own(Tint(colour), go.transform);
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
    }

    public static Material Tint(Color colour)
    {
        Shader s = Shader.Find("Sprites/Default");
        if (s == null) s = Shader.Find("UI/Default");
        return new Material(s) { color = colour };
    }

    // The read-out: what was baked, and what each agent is doing right now.
    void OnGUI()
    {
        if (!Show) return;
        float k = Screen.height / 720f;
        var style = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(17 * k), richText = true };
        style.normal.textColor = Color.white;
        var box = new Rect(14 * k, 150 * k, 440 * k, (76 + Agents.Count * 52) * k);
        GUI.color = new Color(0f, 0f, 0f, 0.72f);
        GUI.DrawTexture(box, Texture2D.whiteTexture);
        GUI.color = Color.white;
        float y = box.y + 8 * k;
        GUI.Label(new Rect(box.x + 10 * k, y, box.width, 26 * k), "<b><color=#5ab4ff>NAVMESH</color></b>   F3 to hide", style);
        y += 26 * k;
        GUI.Label(new Rect(box.x + 10 * k, y, box.width, 26 * k),
            Triangles + " triangles   " + Area.ToString("0") + " m2 walkable   baked in " + BakeMs.ToString("0") + " ms", style);
        y += 32 * k;
        foreach (NavFollower a in Agents)
        {
            if (a == null) continue;
            GUI.Label(new Rect(box.x + 10 * k, y, box.width, 52 * k), a.Report(), style);
            y += 52 * k;
        }
    }
}
