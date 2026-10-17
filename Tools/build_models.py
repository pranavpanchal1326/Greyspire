# Generates every model used by the game and exports them as FBX for Unity.
#
#   blender --background --python Tools/build_models.py
#
# Each model is also saved as a .blend in /Blender so it can be opened and
# tweaked by hand; re-export over the FBX in Assets/Resources/Models to see
# the change in game. Models face -Y in Blender (= +Z / forward in Unity).
# Part names (Body, Head, ArmL, ArmR, LegL, LegR, Sword) are looked up by
# the game's procedural animator, so keep them when editing.

import math
import os
import random

import bpy

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
FBX_DIR = os.path.join(ROOT, "Assets", "Resources", "Models")
BLEND_DIR = os.path.join(ROOT, "Blender")


# ---------------------------------------------------------------- helpers

def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def mat(name, rgb):
    m = bpy.data.materials.get(name)
    if m:
        return m
    m = bpy.data.materials.new(name)
    m.diffuse_color = (*rgb, 1.0)
    try:
        m.use_nodes = True
    except Exception:
        pass
    if m.node_tree:
        bsdf = m.node_tree.nodes.get("Principled BSDF")
        if bsdf:
            bsdf.inputs["Base Color"].default_value = (*rgb, 1.0)
    return m


def _finish(m, smooth=True):
    o = bpy.context.active_object
    o.data.materials.append(m)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    if smooth:
        bpy.ops.object.shade_smooth()
    return o


def sphere(loc, scale, m, seg=24, rings=12):
    if isinstance(scale, (int, float)):
        scale = (scale, scale, scale)
    bpy.ops.mesh.primitive_uv_sphere_add(segments=seg, ring_count=rings, radius=1, location=loc, scale=scale)
    return _finish(m)


def cone(loc, r1, r2, depth, m, rot=(0, 0, 0), verts=20):
    bpy.ops.mesh.primitive_cone_add(vertices=verts, radius1=r1, radius2=r2, depth=depth, location=loc, rotation=rot)
    return _finish(m)


def cyl(loc, r, depth, m, rot=(0, 0, 0), verts=20):
    return cone(loc, r, r, depth, m, rot, verts)


def box(loc, size, m, bevel=0.04, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc, rotation=rot, scale=size)
    o = _finish(m)
    if bevel > 0:
        b = o.modifiers.new("Bevel", 'BEVEL')
        b.width = bevel
        b.segments = 3
        w = o.modifiers.new("WN", 'WEIGHTED_NORMAL')
        w.keep_sharp = False
        bpy.ops.object.modifier_apply(modifier=b.name)
        bpy.ops.object.modifier_apply(modifier=w.name)
    return o


def part(name, objs, pivot=(0, 0, 0)):
    """Join objs into one mesh called name, with its origin at pivot."""
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    if len(objs) > 1:
        bpy.ops.object.join()
    o = bpy.context.active_object
    o.name = name
    o.data.name = name
    bpy.context.scene.cursor.location = pivot
    bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
    return o


def parent(child, par):
    bpy.context.view_layer.update()
    child.parent = par
    child.matrix_parent_inverse = par.matrix_world.inverted()


def export(name):
    os.makedirs(FBX_DIR, exist_ok=True)
    os.makedirs(BLEND_DIR, exist_ok=True)
    bpy.ops.object.select_all(action='DESELECT')
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(BLEND_DIR, name + ".blend"))
    bpy.ops.export_scene.fbx(
        filepath=os.path.join(FBX_DIR, name + ".fbx"),
        use_selection=False,
        object_types={'MESH'},
        apply_scale_options='FBX_SCALE_ALL',
        axis_forward='-Z',
        axis_up='Y',
        bake_space_transform=True,
        mesh_smooth_type='OFF',
        bake_anim=False,
    )
    print("exported", name)


# ---------------------------------------------------------------- characters

def eyes(x, y, z, r, white, dark):
    out = []
    for s in (-1, 1):
        out.append(sphere((s * x, y, z), (r, r * 0.6, r * 1.2), white, 16, 8))
        out.append(sphere((s * x, y - r * 0.45, z), (r * 0.5, r * 0.4, r * 0.65), dark, 12, 6))
    return out


def humanoid(skin, shirt, pants, shoe, dark, white, bulk=1.0):
    """Shared biped build. Right side is -X (model faces -Y)."""
    b = bulk
    parts = {}
    for side, s in (("L", 1), ("R", -1)):
        leg = [
            cyl((s * 0.13 * b, 0, 0.32), 0.1 * b, 0.46, pants),
            sphere((s * 0.13 * b, -0.05, 0.08), (0.13 * b, 0.19 * b, 0.09), shoe, 16, 8),
        ]
        parts["Leg" + side] = part("Leg" + side, leg, (s * 0.13 * b, 0, 0.55))
        arm = [
            cyl((s * 0.4 * b, 0, 0.88), 0.075 * b, 0.42, shirt),
            sphere((s * 0.4 * b, 0, 1.08), 0.11 * b, shirt, 16, 8),
            sphere((s * 0.4 * b, 0, 0.64), 0.1 * b, skin, 16, 8),
        ]
        parts["Arm" + side] = part("Arm" + side, arm, (s * 0.4 * b, 0, 1.08))
    body = [
        sphere((0, 0, 0.86), (0.3 * b, 0.23 * b, 0.36), shirt),
        cyl((0, 0, 0.6), 0.24 * b, 0.1, dark),
    ]
    parts["Body"] = part("Body", body, (0, 0, 0.55))
    return parts


def build_hero():
    reset()
    skin = mat("Skin", (1.0, 0.78, 0.6))
    shirt = mat("Shirt", (0.16, 0.55, 0.95))
    pants = mat("Pants", (0.22, 0.24, 0.42))
    shoe = mat("Shoe", (0.55, 0.3, 0.15))
    hair = mat("Hair", (0.95, 0.55, 0.1))
    dark = mat("Dark", (0.08, 0.08, 0.12))
    white = mat("White", (1, 1, 1))
    steel = mat("Steel", (0.82, 0.88, 0.95))
    gold = mat("Gold", (1.0, 0.8, 0.15))
    scarf = mat("Scarf", (0.95, 0.2, 0.25))

    p = humanoid(skin, shirt, pants, shoe, dark, white)
    # Red scarf: the splash of colour that reads from behind, where the camera sits.
    wrap = [
        cyl((0, 0, 1.17), 0.2, 0.12, scarf, verts=16),
        box((0.08, 0.3, 1.05), (0.16, 0.42, 0.04), scarf, 0.015, rot=(math.radians(-35), 0, math.radians(-12))),
        box((-0.04, 0.4, 0.95), (0.14, 0.5, 0.04), scarf, 0.015, rot=(math.radians(-50), 0, math.radians(10))),
    ]
    p["Body"] = part("Body", [p["Body"]] + wrap, (0, 0, 0.55))

    head = [
        sphere((0, 0, 1.45), 0.3, skin),
        sphere((0, 0.04, 1.53), (0.32, 0.31, 0.26), hair),
        cone((0, -0.2, 1.72), 0.12, 0.0, 0.3, hair, rot=(math.radians(60), 0, 0), verts=12),
        cone((0.14, -0.1, 1.74), 0.09, 0.0, 0.22, hair, rot=(math.radians(40), math.radians(25), 0), verts=12),
        cone((-0.14, -0.1, 1.74), 0.09, 0.0, 0.22, hair, rot=(math.radians(40), math.radians(-25), 0), verts=12),
    ] + eyes(0.11, -0.27, 1.44, 0.06, white, dark)
    p["Head"] = part("Head", head, (0, 0, 1.18))

    sword = [
        cyl((-0.4, -0.02, 0.64), 0.03, 0.22, shoe, rot=(math.radians(90), 0, 0), verts=12),
        box((-0.4, -0.15, 0.64), (0.34, 0.06, 0.09), gold, 0.015),
        box((-0.4, -0.72, 0.64), (0.14, 1.08, 0.035), steel, 0.012),
        cone((-0.4, -1.34, 0.64), 0.07, 0.0, 0.18, steel, rot=(math.radians(90), 0, 0), verts=4),
        sphere((-0.4, 0.1, 0.64), 0.05, gold, 12, 6),
    ]
    p["Sword"] = part("Sword", sword, (-0.4, 0, 0.64))

    parent(p["Sword"], p["ArmR"])
    for n in ("Head", "ArmL", "ArmR"):
        parent(p[n], p["Body"])
    export("Hero")


def build_brute():
    reset()
    skin = mat("Skin", (0.85, 0.25, 0.22))
    belly = mat("Belly", (0.95, 0.6, 0.45))
    pants = mat("Pants", (0.3, 0.2, 0.15))
    dark = mat("Dark", (0.08, 0.08, 0.12))
    white = mat("White", (1, 1, 1))
    horn = mat("Horn", (0.95, 0.92, 0.8))

    p = humanoid(skin, skin, pants, dark, dark, white, bulk=1.25)
    extra = [sphere((0, -0.14, 0.85), (0.26, 0.16, 0.26), belly)]
    p["Body"] = part("Body", [p["Body"]] + extra, (0, 0, 0.55))

    head = [
        sphere((0, 0, 1.38), (0.25, 0.24, 0.22), skin),
        cone((0.2, 0, 1.58), 0.07, 0.0, 0.3, horn, rot=(0, math.radians(30), 0), verts=10),
        cone((-0.2, 0, 1.58), 0.07, 0.0, 0.3, horn, rot=(0, math.radians(-30), 0), verts=10),
        box((0, -0.2, 1.47), (0.36, 0.06, 0.05), dark, 0.015, rot=(0, 0, 0)),
        sphere((0.07, -0.2, 1.27), (0.03, 0.03, 0.06), horn, 8, 6),
        sphere((-0.07, -0.2, 1.27), (0.03, 0.03, 0.06), horn, 8, 6),
    ] + eyes(0.1, -0.21, 1.4, 0.045, white, dark)
    p["Head"] = part("Head", head, (0, 0, 1.2))

    # Big fists so the slam reads clearly.
    for side, s in (("L", 1), ("R", -1)):
        fist = sphere((s * 0.5, 0, 0.6), 0.17, skin, 16, 8)
        p["Arm" + side] = part("Arm" + side, [p["Arm" + side], fist], (s * 0.5, 0, 1.08))

    for n in ("Head", "ArmL", "ArmR"):
        parent(p[n], p["Body"])
    export("Brute")


def build_slime():
    reset()
    green = mat("Slime", (0.35, 0.85, 0.3))
    light = mat("SlimeLight", (0.7, 1.0, 0.6))
    dark = mat("Dark", (0.08, 0.08, 0.12))
    white = mat("White", (1, 1, 1))
    objs = [
        sphere((0, 0, 0.36), (0.5, 0.5, 0.4), green),
        sphere((0, 0, 0.12), (0.56, 0.56, 0.14), green),
        sphere((0.18, -0.2, 0.62), (0.1, 0.08, 0.06), light, 12, 6),
    ] + eyes(0.16, -0.42, 0.42, 0.09, white, dark)
    part("Body", objs, (0, 0, 0))
    export("Slime")


def build_spitter():
    reset()
    robe = mat("Robe", (0.55, 0.3, 0.85))
    trim = mat("Trim", (1.0, 0.8, 0.15))
    skin = mat("Skin", (0.6, 0.9, 0.85))
    dark = mat("Dark", (0.08, 0.08, 0.12))
    white = mat("White", (1, 1, 1))
    body = [
        cone((0, 0, 0.55), 0.38, 0.16, 0.8, robe),
        cyl((0, 0, 0.17), 0.39, 0.06, trim),
        sphere((0.34, -0.1, 0.7), 0.09, skin, 12, 6),
        sphere((-0.34, -0.1, 0.7), 0.09, skin, 12, 6),
    ]
    b = part("Body", body, (0, 0, 0))
    head = [
        sphere((0, 0, 1.15), 0.27, skin),
        cyl((0, 0, 1.32), 0.42, 0.04, robe),
        cone((0, 0.03, 1.62), 0.26, 0.0, 0.6, robe, rot=(math.radians(-10), 0, 0)),
        cyl((0, 0, 1.37), 0.265, 0.07, trim),
        sphere((0, -0.26, 1.06), (0.06, 0.04, 0.05), dark, 12, 6),
    ] + eyes(0.1, -0.24, 1.17, 0.055, white, dark)
    h = part("Head", head, (0, 0, 0.95))
    parent(h, b)
    export("Spitter")


# ---------------------------------------------------------------- props

def build_coin():
    reset()
    gold = mat("Gold", (1.0, 0.8, 0.15))
    deep = mat("GoldDeep", (0.9, 0.55, 0.05))
    r = (math.radians(90), 0, 0)
    part("Coin", [
        cyl((0, 0, 0), 0.25, 0.06, gold, rot=r, verts=24),
        cyl((0, 0, 0), 0.17, 0.08, deep, rot=r, verts=24),
    ])
    export("Coin")


def build_heart():
    reset()
    red = mat("Heart", (1.0, 0.2, 0.3))
    part("Heart", [
        sphere((0.11, 0, 0.1), (0.15, 0.1, 0.15), red, 16, 8),
        sphere((-0.11, 0, 0.1), (0.15, 0.1, 0.15), red, 16, 8),
        cone((0, 0, -0.09), 0.0, 0.235, 0.3, red, verts=16),
    ])
    bpy.context.active_object.scale = (1, 0.45, 1)
    export("Heart")


def build_tree():
    reset()
    trunk = mat("Trunk", (0.5, 0.32, 0.18))
    leaf = mat("Leaf", (0.25, 0.7, 0.3))
    leaf2 = mat("LeafLight", (0.45, 0.82, 0.35))
    part("Tree", [
        cone((0, 0, 0.8), 0.24, 0.15, 1.6, trunk, verts=12),
        sphere((0, 0, 2.2), (1.0, 1.0, 0.85), leaf, 20, 10),
        sphere((0.55, 0.2, 2.75), 0.65, leaf2, 16, 8),
        sphere((-0.5, -0.3, 2.65), 0.6, leaf, 16, 8),
        sphere((0, 0.1, 3.2), 0.55, leaf2, 16, 8),
    ])
    export("Tree")


def build_rock():
    reset()
    grey = mat("Rock", (0.55, 0.56, 0.62))
    random.seed(7)
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2, radius=0.7, location=(0, 0, 0.4), scale=(1.1, 0.9, 0.7))
    o = _finish(grey)
    for v in o.data.vertices:
        v.co *= 1 + random.uniform(-0.12, 0.12)
        v.co.z = max(v.co.z, -0.38)
    part("Rock", [o])
    export("Rock")


def build_crate():
    reset()
    wood = mat("Wood", (0.78, 0.55, 0.3))
    band = mat("Band", (0.5, 0.32, 0.18))
    objs = [box((0, 0, 0.5), (1, 1, 1), wood, 0.05)]
    for z in (0.12, 0.88):
        objs.append(box((0, 0, z), (1.06, 1.06, 0.14), band, 0.02))
    part("Crate", objs)
    export("Crate")


def build_pillar():
    reset()
    stone = mat("Stone", (0.85, 0.82, 0.72))
    part("Pillar", [
        cyl((0, 0, 1.6), 0.38, 3.0, stone, verts=16),
        box((0, 0, 0.12), (1.1, 1.1, 0.24), stone, 0.05),
        box((0, 0, 3.2), (1.1, 1.1, 0.24), stone, 0.05),
    ])
    export("Pillar")


def build_cactus():
    reset()
    green = mat("Cactus", (0.3, 0.65, 0.35))
    pink = mat("Flower", (1.0, 0.45, 0.7))
    r = (0, math.radians(90), 0)
    part("Cactus", [
        cyl((0, 0, 0.9), 0.28, 1.8, green, verts=14),
        sphere((0, 0, 1.8), 0.28, green, 14, 8),
        cyl((0.4, 0, 0.9), 0.14, 0.5, green, rot=r, verts=12),
        cyl((0.62, 0, 1.2), 0.14, 0.6, green, verts=12),
        sphere((0.62, 0, 1.5), 0.14, green, 12, 6),
        cyl((-0.4, 0, 1.2), 0.13, 0.5, green, rot=r, verts=12),
        cyl((-0.6, 0, 1.42), 0.13, 0.45, green, verts=12),
        sphere((-0.6, 0, 1.65), 0.13, green, 12, 6),
        sphere((0, 0, 2.1), (0.14, 0.14, 0.08), pink, 12, 6),
    ])
    export("Cactus")


def build_crystal():
    reset()
    c1 = mat("Crystal", (0.35, 0.9, 1.0))
    c2 = mat("CrystalDeep", (0.55, 0.45, 1.0))
    objs = []
    for x, y, h, r, tilt, m in ((0, 0, 2.0, 0.35, 0, c1), (0.45, 0.1, 1.2, 0.25, 18, c2),
                                (-0.4, -0.15, 1.4, 0.28, -15, c2), (0.05, -0.45, 0.9, 0.2, 8, c1)):
        t = (math.radians(tilt * 0.5), math.radians(tilt), 0)
        objs.append(cyl((x, y, h * 0.35), r, h * 0.7, m, rot=t, verts=6))
        objs.append(cone((x + math.sin(t[1]) * h * 0.5, y - math.sin(t[0]) * h * 0.5, h * 0.85),
                         r, 0.0, h * 0.3, m, rot=t, verts=6))
    part("Crystal", objs)
    export("Crystal")


def build_gloomking():
    reset()
    skin = mat("Skin", (0.32, 0.27, 0.42))
    robe = mat("Robe", (0.18, 0.15, 0.26))
    dark = mat("Dark", (0.06, 0.05, 0.1))
    glow = mat("Glow", (1.0, 0.9, 0.25))
    gold = mat("Gold", (0.95, 0.75, 0.2))
    cape = mat("Cape", (0.5, 0.12, 0.3))

    p = humanoid(skin, robe, robe, dark, gold, glow, bulk=1.3)
    extra = [
        cone((0, 0.2, 0.8), 0.5, 0.3, 1.0, cape, rot=(math.radians(-12), 0, 0), verts=16),
        sphere((0.36, 0, 1.14), (0.2, 0.2, 0.13), gold, 16, 8),
        sphere((-0.36, 0, 1.14), (0.2, 0.2, 0.13), gold, 16, 8),
    ]
    p["Body"] = part("Body", [p["Body"]] + extra, (0, 0, 0.55))

    head = [
        sphere((0, 0, 1.42), (0.27, 0.26, 0.26), skin),
        cyl((0, 0, 1.64), 0.22, 0.1, gold, verts=16),
        box((0, -0.2, 1.5), (0.4, 0.06, 0.05), dark, 0.015),
        sphere((0.1, -0.23, 1.42), (0.055, 0.04, 0.035), glow, 12, 6),
        sphere((-0.1, -0.23, 1.42), (0.055, 0.04, 0.035), glow, 12, 6),
    ]
    for i in range(5):
        a = i * math.tau / 5
        head.append(cone((math.sin(a) * 0.19, math.cos(a) * 0.19, 1.78), 0.05, 0.0, 0.2, gold, verts=8))
    p["Head"] = part("Head", head, (0, 0, 1.2))

    for side, s in (("L", 1), ("R", -1)):
        fist = sphere((s * 0.52, 0, 0.6), 0.18, skin, 16, 8)
        p["Arm" + side] = part("Arm" + side, [p["Arm" + side], fist], (s * 0.52, 0, 1.08))

    for n in ("Head", "ArmL", "ArmR"):
        parent(p[n], p["Body"])
    export("GloomKing")


def build_bomber():
    reset()
    shell = mat("Shell", (0.16, 0.16, 0.22))
    feet = mat("Feet", (0.95, 0.6, 0.15))
    fuse = mat("Fuse", (0.8, 0.7, 0.5))
    spark = mat("Spark", (1.0, 0.45, 0.1))
    dark = mat("Dark", (0.08, 0.08, 0.12))
    white = mat("White", (1, 1, 1))
    for side, s in (("L", 1), ("R", -1)):
        part("Leg" + side, [sphere((s * 0.18, -0.04, 0.08), (0.14, 0.2, 0.09), feet, 16, 8)], (s * 0.18, 0, 0.2))
    body = [
        sphere((0, 0, 0.52), 0.42, shell),
        cyl((0, 0, 0.95), 0.1, 0.1, fuse, verts=12),
        cyl((0, 0.04, 1.08), 0.025, 0.2, fuse, rot=(math.radians(-20), 0, 0), verts=8),
        sphere((0, 0.08, 1.2), 0.07, spark, 12, 6),
        box((0.13, -0.36, 0.66), (0.16, 0.04, 0.04), dark, 0.01, rot=(0, math.radians(20), 0)),
        box((-0.13, -0.36, 0.66), (0.16, 0.04, 0.04), dark, 0.01, rot=(0, math.radians(-20), 0)),
    ] + eyes(0.13, -0.36, 0.56, 0.07, white, dark)
    part("Body", body, (0, 0, 0.2))
    export("Bomber")


def build_lumi():
    reset()
    core = mat("Lumi", (1.0, 0.95, 0.55))
    wing = mat("Wing", (0.75, 0.95, 1.0))
    dark = mat("Dark", (0.08, 0.08, 0.12))
    part("Lumi", [
        sphere((0, 0, 0), 0.16, core, 16, 8),
        sphere((0.2, 0.05, 0.08), (0.13, 0.02, 0.08), wing, 12, 6),
        sphere((-0.2, 0.05, 0.08), (0.13, 0.02, 0.08), wing, 12, 6),
        cone((0, 0, 0.24), 0.04, 0.0, 0.14, core, verts=8),
        sphere((0.06, -0.14, 0.02), (0.025, 0.02, 0.035), dark, 8, 6),
        sphere((-0.06, -0.14, 0.02), (0.025, 0.02, 0.035), dark, 8, 6),
    ])
    export("Lumi")


for fn in (build_gloomking, build_bomber, build_lumi, build_hero, build_brute, build_slime, build_spitter, build_coin, build_heart,
           build_tree, build_rock, build_crate, build_pillar, build_cactus, build_crystal):
    fn()
print("ALL MODELS DONE")
