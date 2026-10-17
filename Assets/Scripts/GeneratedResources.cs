using System.Collections.Generic;
using UnityEngine;

// Own only freshly generated native assets. Shared cached/imported materials,
// meshes and textures are intentionally never registered by these call sites.
public sealed class GeneratedResources : MonoBehaviour
{
    readonly HashSet<Object> owned = new HashSet<Object>();
    bool released;
    public static int LiveAssets { get; private set; }
    public static T Own<T>(T asset, Transform owner) where T : Object
    {
        if (asset == null || owner == null) return asset;
        var scope = owner.GetComponent<GeneratedResources>();
        if (scope == null) scope = owner.gameObject.AddComponent<GeneratedResources>();
        if (scope.owned.Add(asset)) LiveAssets++;
        return asset;
    }
    // Unity creates native combined meshes during runtime static batching.
    // Compare identities, so imported source meshes are never claimed here.
    public static void Combine(Transform root, GameObject[] selected = null)
    {
        var before = new HashSet<Mesh>();
        foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true)) before.Add(filter.sharedMesh);
        if (selected == null) StaticBatchingUtility.Combine(root.gameObject);
        else StaticBatchingUtility.Combine(selected, root.gameObject);
        foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            if (filter.sharedMesh != null && !before.Contains(filter.sharedMesh)) Own(filter.sharedMesh, root);
    }
    public void Release()
    {
        if (released) return; released = true;
        foreach (var asset in owned) { if (asset != null) Destroy(asset); LiveAssets--; }
        owned.Clear();
    }
    void OnDestroy() => Release();
}
