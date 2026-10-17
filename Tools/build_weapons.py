# Builds the weapon-family props that the Quaternius kit does not provide, in the
# kit's own conventions, and exports them next to the kit props:
#
#   blender --background --python Tools/build_weapons.py
#
#   Greataxe_Iron  1.2 m bearded great axe (Bram, great-axe family)
#   Seax_Steel     slender single-edged long knife (Nix, light-blade family, right hand)
#   Dagger_Steel   short parrying dagger (Nix, left hand)
#
# Conventions copied from Sword_Bronze.fbx: the origin is where the hand closes, the
# weapon's long axis is +Z, its flat faces look along +/-Y and its edges along +/-X.
# Every face is UV-mapped onto a region of T_Trim_Props_BaseColor (the sheet the kit's
# own weapons use), so the props pick up the same paint, normal map and shading.
# The .blend is saved to Blender/Weapons.blend for hand edits.

import math
import os

import bpy
import bmesh

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
PROPS = os.path.join(ROOT, "Assets", "Resources", "Humans", "Props")
BLEND = os.path.join(ROOT, "Blender", "Weapons.blend")

# Regions of T_Trim_Props_BaseColor (u0, v0, u1, v1), measured from the sheet.
DARK_WOOD = (0.02, 0.84, 0.98, 0.90)
STEEL = (0.43, 0.13, 0.77, 0.34)
BRIGHT_STEEL = (0.01, 0.50, 0.30, 0.55)
LEATHER = (0.44, 0.40, 0.53, 0.47)


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def material():
    m = bpy.data.materials.new("MI_Trim_Props_Vertex")
    m.use_nodes = True
    nodes = m.node_tree.nodes
    bsdf = nodes.get("Principled BSDF")
    tex = nodes.new("ShaderNodeTexImage")
    tex.image = bpy.data.images.load(os.path.join(PROPS, "T_Trim_Props_BaseColor.png"))
    m.node_tree.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    return m


class Builder:
    """Accumulates parts into one bmesh; each part carries its own UV region."""

    def __init__(self):
        self.bm = bmesh.new()
        self.uv = self.bm.loops.layers.uv.new("UVMap")

    def _map(self, faces, region, ua, va):
        # ua/va: functions from a vertex position to the two coordinates spread over the region.
        verts = {v for f in faces for v in f.verts}
        us = [ua(v.co) for v in verts]
        vs = [va(v.co) for v in verts]
        umin, umax = min(us), max(us)
        vmin, vmax = min(vs), max(vs)
        u0, v0, u1, v1 = region
        for f in faces:
            for loop in f.loops:
                a = (ua(loop.vert.co) - umin) / max(1e-6, umax - umin)
                b = (va(loop.vert.co) - vmin) / max(1e-6, vmax - vmin)
                loop[self.uv].uv = (u0 + a * (u1 - u0), v0 + b * (v1 - v0))

    def tube(self, z0, z1, r0, r1, region, sides=8, cap=True):
        """A tapered prism along Z (haft, grip, ferrule)."""
        bm = self.bm
        lo, hi = [], []
        for i in range(sides):
            a = 2 * math.pi * (i + 0.5) / sides
            lo.append(bm.verts.new((r0 * math.cos(a), r0 * math.sin(a), z0)))
            hi.append(bm.verts.new((r1 * math.cos(a), r1 * math.sin(a), z1)))
        faces = []
        for i in range(sides):
            j = (i + 1) % sides
            faces.append(bm.faces.new((lo[i], lo[j], hi[j], hi[i])))
        # Wrap the texture round the tube: u along the length, v round the circumference.
        self._map(faces, region, lambda c: c.z, lambda c: (math.atan2(c.y, c.x) + math.pi))
        if cap:
            caps = [bm.faces.new(list(reversed(lo))), bm.faces.new(hi)]
            self._map(caps, region, lambda c: c.x, lambda c: c.y)
        return faces

    def plate(self, outline, thick, region, centre):
        """A flat blade from an XZ outline. thick(x, z) gives the half-thickness at a point,
        so blades thin towards their edges; centre is an interior point for the fan."""
        bm = self.bm
        n = len(outline)
        cf = bm.verts.new((centre[0], thick(*centre), centre[1]))
        cb = bm.verts.new((centre[0], -thick(*centre), centre[1]))
        front = [bm.verts.new((x, thick(x, z), z)) for x, z in outline]
        back = [bm.verts.new((x, -thick(x, z), z)) for x, z in outline]
        faces = []
        for i in range(n):
            j = (i + 1) % n
            faces.append(bm.faces.new((cf, front[i], front[j])))
            faces.append(bm.faces.new((cb, back[j], back[i])))
            faces.append(bm.faces.new((front[j], front[i], back[i], back[j])))
        self._map(faces, region, lambda c: c.z, lambda c: c.x)
        return faces

    def finish(self, name, mat):
        bmesh.ops.recalc_face_normals(self.bm, faces=self.bm.faces)
        mesh = bpy.data.meshes.new(name)
        self.bm.to_mesh(mesh)
        self.bm.free()
        for p in mesh.polygons:
            p.use_smooth = False
        mesh.materials.append(mat)
        obj = bpy.data.objects.new(name, mesh)
        bpy.context.scene.collection.objects.link(obj)
        return obj


def greataxe(mat):
    b = Builder()
    # Ash haft, 1.2 m overall: the right hand closes at the origin, a hand's width above the
    # leather wrap, so the head sits about 0.7 m out, where a Dane axe's weight is controllable.
    b.tube(-0.30, 0.80, 0.021, 0.019, DARK_WOOD, sides=8)
    b.tube(-0.34, -0.29, 0.026, 0.026, STEEL, sides=8)                 # iron butt cap
    b.tube(-0.26, 0.10, 0.024, 0.024, LEATHER, sides=8, cap=False)     # leather grip wrap
    b.tube(0.56, 0.82, 0.030, 0.026, STEEL, sides=8)                   # langets and socket
    # The bearded head: a long hooked edge forward (+X) that drops well below the socket.
    lift = -0.18
    edge = [(0.024, 0.99), (0.08, 1.0), (0.16, 1.03), (0.24, 1.08), (0.30, 1.125), (0.345, 1.105),
            (0.372, 1.03), (0.382, 0.94), (0.376, 0.84), (0.355, 0.74), (0.32, 0.65), (0.27, 0.58),
            (0.22, 0.555), (0.18, 0.57), (0.15, 0.62), (0.12, 0.70), (0.09, 0.78), (0.06, 0.84), (0.024, 0.87)]
    edge = [(x, z + lift) for x, z in edge]
    b.plate(edge, lambda x, z: max(0.0025, 0.016 - 0.04 * max(0.0, x - 0.04)), STEEL, (0.15, 0.90 + lift))
    # A short square poll behind the socket balances the head.
    poll = [(-0.024, 0.86), (-0.024, 0.97), (-0.07, 0.955), (-0.095, 0.93), (-0.095, 0.895), (-0.07, 0.875)]
    poll = [(x, z + lift) for x, z in poll]
    b.plate(poll, lambda x, z: 0.018, STEEL, (-0.05, 0.915 + lift))
    # A top spike.
    b.tube(0.82, 0.91, 0.016, 0.002, BRIGHT_STEEL, sides=6)
    return b.finish("Greataxe_Iron", mat)


def seax(mat):
    b = Builder()
    b.tube(-0.12, 0.05, 0.016, 0.017, LEATHER, sides=8)                # grip
    b.tube(-0.135, -0.12, 0.021, 0.021, STEEL, sides=8)                # pommel
    b.tube(0.05, 0.065, 0.03, 0.03, STEEL, sides=8)                    # small bolster
    # Broken-back seax blade, 0.52 m: straight edge forward, spine angling to a sharp point.
    blade = [(-0.02, 0.065), (0.026, 0.065), (0.032, 0.30), (0.03, 0.44), (0.0, 0.575),
             (-0.016, 0.49), (-0.024, 0.38), (-0.022, 0.20)]
    b.plate(blade, lambda x, z: max(0.0015, 0.0055 - 0.12 * max(0.0, x)), BRIGHT_STEEL, (0.0, 0.3))
    return b.finish("Seax_Steel", mat)


def dagger(mat):
    b = Builder()
    b.tube(-0.08, 0.035, 0.014, 0.015, LEATHER, sides=8)
    b.tube(-0.095, -0.08, 0.019, 0.019, STEEL, sides=8)
    # Crossguard.
    guard = [(-0.065, 0.035), (0.065, 0.035), (0.07, 0.05), (0.06, 0.055), (-0.06, 0.055), (-0.07, 0.05)]
    b.plate(guard, lambda x, z: 0.011, STEEL, (0.0, 0.045))
    # Leaf blade, 0.26 m, double-edged with a raised ridge.
    blade = [(-0.019, 0.055), (0.019, 0.055), (0.022, 0.13), (0.016, 0.23), (0.0, 0.315),
             (-0.016, 0.23), (-0.022, 0.13)]
    b.plate(blade, lambda x, z: max(0.0012, 0.0045 - 0.15 * abs(x)), BRIGHT_STEEL, (0.0, 0.15))
    return b.finish("Dagger_Steel", mat)


def export(obj):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.fbx(
        filepath=os.path.join(PROPS, obj.name + ".fbx"),
        use_selection=True,
        object_types={"MESH"},
        mesh_smooth_type="FACE",
        path_mode="STRIP",
        embed_textures=False,
    )


def main():
    reset()
    mat = material()
    made = [greataxe(mat), seax(mat), dagger(mat)]
    for i, obj in enumerate(made):
        export(obj)
        obj.location.x = i * 0.6     # spread out in the .blend only, after export
    os.makedirs(os.path.dirname(BLEND), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=BLEND)
    for obj in made:
        dims = obj.dimensions
        print("WEAPON", obj.name, "faces", len(obj.data.polygons), "size", tuple(round(d, 3) for d in dims))


main()
