using System.Collections.Generic;
using UnityEngine;

// Each level owns a bounded cosmetic pool; saturation may omit an afterimage,
// never an attack. Native baked meshes live exactly as long as this scope.
public sealed class DashGhostPool : MonoBehaviour
{
    public const int Capacity = 192;
    public int Created => slots.Count;
    public int Dropped { get; private set; }
    readonly List<GhostFade> slots = new List<GhostFade>();
    int cursor;
    Transform cachedModel;
    SkinnedMeshRenderer[] skins;
    MeshFilter[] filters;
    static DashGhostPool current;

    public static void Emit(Transform model, Color colour)
    {
        if (model == null || Game.I == null || Game.I.LevelRoot == null) return;
        if (current == null || current.transform.parent != Game.I.LevelRoot)
        {
            var go = new GameObject("Dash ghosts");
            go.transform.SetParent(Game.I.LevelRoot, false);
            current = go.AddComponent<DashGhostPool>();
        }
        current.Capture(model, colour);
    }
    GhostFade Rent()
    {
        for (int i = 0; i < slots.Count; i++)
        {
            int at = (cursor + i) % slots.Count;
            if (slots[at].gameObject.activeSelf) continue;
            cursor = (at + 1) % slots.Count;
            return slots[at];
        }
        if (slots.Count >= Capacity) { Dropped++; return null; }
        var go = new GameObject("Ghost"); go.transform.SetParent(transform, false);
        go.AddComponent<MeshFilter>();
        var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = Fx.Flat;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        var fade = go.AddComponent<GhostFade>(); fade.pooled = true;
        slots.Add(fade); return fade;
    }
    void Capture(Transform model, Color colour)
    {
        if (cachedModel != model)
        {
            cachedModel = model;
            skins = model.GetComponentsInChildren<SkinnedMeshRenderer>();
            filters = model.GetComponentsInChildren<MeshFilter>();
        }
        foreach (var skin in skins)
        {
            if (skin == null) continue;
            var slot = Rent(); if (slot == null) continue;
            if (slot.baked == null) slot.baked = GeneratedResources.Own(new Mesh { name = "Dash pose" }, transform);
            skin.BakeMesh(slot.baked, true);
            slot.ResetPose(slot.baked, skin.transform.position, skin.transform.rotation, Vector3.one, colour);
        }
        foreach (var filter in filters)
        {
            if (filter == null || filter.sharedMesh == null || filter.GetComponent<TrailRenderer>() != null) continue;
            var slot = Rent(); if (slot != null)
                slot.ResetPose(filter.sharedMesh, filter.transform.position, filter.transform.rotation, filter.transform.lossyScale, colour);
        }
    }
}
