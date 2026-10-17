using UnityEditor;
using UnityEngine;

// Reusable translated greybox: the lower route is deliberately below y=-15.
public static class TerrainCourse
{
    public static readonly Vector3 Origin = new Vector3(300f, -20f, 300f);
    public static GameObject Barrier { get; private set; }
    public static Vector3 At(float x, float y, float z) => Origin + new Vector3(x, y, z);
    public static GameObject Box(Transform root, string name, Vector3 local, Vector3 size)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name; go.transform.SetParent(root, false); go.transform.localPosition = local; go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = new Material(Shader.Find("Standard")) { color = new Color(0.36f, 0.48f, 0.58f) };
        return go;
    }
    public static Transform Build()
    {
        var root = new GameObject("G2 terrain course").transform; root.position = Origin;
        Box(root, "Lower route", new Vector3(0f, -0.5f, 0f), new Vector3(80f, 1f, 80f));
        Box(root, "Bridge over lower route", new Vector3(0f, 7.7f, 4f), new Vector3(24f, 0.6f, 8f));
        var ramp = Box(root, "Uphill and downhill", new Vector3(-9f, 3.8f, -10f), new Vector3(6f, 0.4f, Mathf.Sqrt(20f * 20f + 8f * 8f) + 0.4f));
        ramp.transform.localRotation = Quaternion.Euler(-Mathf.Atan2(8f, 20f) * Mathf.Rad2Deg, 0f, 0f);
        Box(root, "Convex corner", new Vector3(-25f, 2f, 18f), new Vector3(6f, 4f, 6f));
        Box(root, "Concave corner A", new Vector3(-20f, 2f, 24f), new Vector3(12f, 4f, 0.5f));
        Box(root, "Concave corner B", new Vector3(-14f, 2f, 20f), new Vector3(0.5f, 4f, 8f));
        Box(root, "Room left wall", new Vector3(16f, 2f, 0f), new Vector3(0.5f, 4f, 8.5f));
        Box(root, "Room right wall", new Vector3(24f, 2f, 0f), new Vector3(0.5f, 4f, 8.5f));
        Box(root, "Room back wall", new Vector3(20f, 2f, 4f), new Vector3(8f, 4f, 0.5f));
        Box(root, "Narrow door left", new Vector3(17.5f, 2f, -4f), new Vector3(3f, 4f, 0.5f));
        Box(root, "Narrow door right", new Vector3(22.5f, 2f, -4f), new Vector3(3f, 4f, 0.5f));
        Barrier = Box(root, "Closing barrier", new Vector3(20f, 1.5f, -7f), new Vector3(2f, 3f, 0.3f));
        Barrier.SetActive(false);
        Box(root, "Overhead beam", new Vector3(-22f, 1.5f, -5f), new Vector3(6f, 0.3f, 4f));
        for (int i = 0; i < 10; i++)
            Box(root, "Stair " + i, new Vector3(20f, (i + 1) * 0.1f, 12f + i * 0.6f), new Vector3(5f, (i + 1) * 0.2f, 0.6f));
        Box(root, "Stair landing", new Vector3(20f, 1f, 19f), new Vector3(5f, 2f, 3f));
        Physics.SyncTransforms();
        return root;
    }
    [MenuItem("Greyspire/Create G2 Terrain Course")]
    public static void CreateInEditor() { Selection.activeGameObject = Build().gameObject; }
    public static void Capture()
    {
        var cameraGo = new GameObject("Terrain evidence camera"); var camera = cameraGo.AddComponent<Camera>();
        camera.transform.position = At(58f, 45f, -62f); camera.transform.LookAt(At(0f, 3f, 0f));
        camera.backgroundColor = new Color(0.08f, 0.12f, 0.18f); camera.clearFlags = CameraClearFlags.SolidColor;
        camera.farClipPlane = 250f; camera.fieldOfView = 55f;
        var target = new RenderTexture(1400, 1000, 24); camera.targetTexture = target;
        bool fog = RenderSettings.fog; RenderSettings.fog = false; camera.Render(); RenderSettings.fog = fog;
        var previous = RenderTexture.active; RenderTexture.active = target;
        var pixels = new Texture2D(1400, 1000, TextureFormat.RGB24, false);
        pixels.ReadPixels(new Rect(0f, 0f, 1400f, 1000f), 0, 0); pixels.Apply();
        System.IO.Directory.CreateDirectory("Logs"); System.IO.File.WriteAllBytes("Logs/terrain-course.png", pixels.EncodeToPNG());
        RenderTexture.active = previous; camera.targetTexture = null; target.Release();
        Object.DestroyImmediate(pixels); Object.DestroyImmediate(target); Object.DestroyImmediate(cameraGo);
    }
}
