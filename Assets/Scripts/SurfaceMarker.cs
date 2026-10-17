using UnityEngine;

// Ground effects share the attack's bounded ground-height band. Invalid portions
// are omitted instead of painting a second floor or an unsupported ledge.
[RequireComponent(typeof(MeshFilter))]
public class SurfaceMarker : MonoBehaviour
{
    Mesh mesh;
    Vector3[] source, points;
    int[] triangles;
    bool[] supported;
    readonly System.Collections.Generic.List<int> visible = new System.Collections.Generic.List<int>();
    void Awake()
    {
        mesh = GetComponent<MeshFilter>().mesh;
        source = mesh.vertices; points = new Vector3[source.Length]; supported = new bool[source.Length];
        triangles = mesh.triangles;
    }
    void LateUpdate() => Project();
    public void Project()
    {
        visible.Clear();
        for (int i = 0; i < source.Length; i++)
        {
            Vector3 world = transform.TransformPoint(source[i]);
            supported[i] = WorldSupport.TryGround(world, out var support, 0.35f, 0.6f);
            points[i] = supported[i] ? transform.InverseTransformPoint(support.point + Vector3.up * 0.06f) : source[i];
        }
        for (int i = 0; i < triangles.Length; i += 3)
            if (supported[triangles[i]] && supported[triangles[i + 1]] && supported[triangles[i + 2]])
            { visible.Add(triangles[i]); visible.Add(triangles[i + 1]); visible.Add(triangles[i + 2]); }
        mesh.vertices = points; mesh.SetTriangles(visible, 0); mesh.RecalculateBounds();
    }
    void OnDestroy() { if (mesh != null) Destroy(mesh); }
}
