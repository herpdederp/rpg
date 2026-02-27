"""
generate_items.py
=================
Blender Python script -- run headless:
    blender --background --python BlenderPipeline/scripts/generate_items.py

Generates six pickup/loot item models with vertex colors, all in one GLB:
  - Health Potion  (~80 tris)  -- round flask with red liquid
  - Shield         (~120 tris) -- round wooden shield with metal boss
  - Axe            (~100 tris) -- one-handed battle axe
  - Helmet         (~120 tris) -- simple iron helm
  - Bone Fragment  (~50 tris)  -- curved bone piece
  - Wolf Pelt      (~60 tris)  -- flat draped fur

Exports to BlenderPipeline/exports/items.glb
"""

import bpy
import bmesh
import os
import math
from mathutils import Vector

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
EXPORT_DIR = os.path.join(SCRIPT_DIR, "..", "exports")


def clear_scene():
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    for block in bpy.data.meshes:
        if block.users == 0:
            bpy.data.meshes.remove(block)
    for mat in bpy.data.materials:
        if mat.users == 0:
            bpy.data.materials.remove(mat)


def set_vertex_colors(obj, color_func):
    mesh = obj.data
    if not mesh.color_attributes:
        mesh.color_attributes.new(name="Color", type='BYTE_COLOR', domain='CORNER')
    color_layer = mesh.color_attributes[0]
    vert_positions = {v.index: v.co.copy() for v in mesh.vertices}
    for poly in mesh.polygons:
        for li in poly.loop_indices:
            loop = mesh.loops[li]
            vi = loop.vertex_index
            color_layer.data[li].color = color_func(vert_positions[vi])


def make_vc_material(name, roughness=0.6, metallic=0.0):
    mat = bpy.data.materials.new(name=name)
    mat.use_nodes = True
    tree = mat.node_tree
    for n in tree.nodes:
        tree.nodes.remove(n)

    vc_node = tree.nodes.new('ShaderNodeVertexColor')
    vc_node.layer_name = "Color"
    vc_node.location = (-300, 0)

    bsdf = tree.nodes.new('ShaderNodeBsdfPrincipled')
    bsdf.location = (0, 0)
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Metallic"].default_value = metallic

    output = tree.nodes.new('ShaderNodeOutputMaterial')
    output.location = (300, 0)

    tree.links.new(vc_node.outputs["Color"], bsdf.inputs["Base Color"])
    tree.links.new(bsdf.outputs["BSDF"], output.inputs["Surface"])
    return mat


def export_glb(filepath):
    bpy.ops.object.select_all(action='SELECT')
    os.makedirs(os.path.dirname(filepath), exist_ok=True)
    bpy.ops.export_scene.gltf(
        filepath=filepath,
        export_format='GLB',
        use_selection=True,
        export_apply=True,
        export_yup=True,
        export_materials='EXPORT',
    )
    size = os.path.getsize(filepath)
    print(f"  Exported: {filepath} ({size / 1024:.1f} KB)")


def add_part(parts, name, prim, loc, scl, rot=None):
    if prim == 'cube':
        bpy.ops.mesh.primitive_cube_add(location=loc)
    elif prim == 'cylinder':
        bpy.ops.mesh.primitive_cylinder_add(location=loc, vertices=8, radius=1, depth=1)
    elif prim == 'uv_sphere':
        bpy.ops.mesh.primitive_uv_sphere_add(location=loc, segments=10, ring_count=8)
    elif prim == 'cone':
        bpy.ops.mesh.primitive_cone_add(location=loc, vertices=8, radius1=1, radius2=0, depth=1)
    obj = bpy.context.active_object
    obj.name = name
    obj.scale = scl
    if rot:
        obj.rotation_euler = rot
    bpy.ops.object.transform_apply(scale=True, rotation=True)
    parts.append(obj)
    return obj


def join_parts(parts, final_name):
    bpy.ops.object.select_all(action='DESELECT')
    for p in parts:
        p.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()
    obj = bpy.context.active_object
    obj.name = final_name
    return obj


def smooth_shade(obj):
    for poly in obj.data.polygons:
        poly.use_smooth = True


def set_origin_center(obj):
    bpy.ops.object.origin_set(type='ORIGIN_GEOMETRY', center='BOUNDS')


def set_origin_bottom(obj):
    lowest = min(v.co.z for v in obj.data.vertices)
    bpy.context.scene.cursor.location = Vector((0, 0, lowest))
    bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
    obj.location = (0, 0, 0)


# =================================================================
#  HEALTH POTION -- round flask with red liquid
# =================================================================
def generate_health_potion(offset_x=0):
    parts = []

    # Flask body (round bottom)
    add_part(parts, "HP_Body", 'uv_sphere', (offset_x, 0, 0.10), (0.07, 0.07, 0.08))

    # Neck
    add_part(parts, "HP_Neck", 'cylinder', (offset_x, 0, 0.22), (0.025, 0.025, 0.05))

    # Cork stopper
    add_part(parts, "HP_Cork", 'cylinder', (offset_x, 0, 0.28), (0.03, 0.03, 0.02))

    # Liquid level indicator (inner sphere, slightly smaller)
    add_part(parts, "HP_Liquid", 'uv_sphere', (offset_x, 0, 0.08), (0.055, 0.055, 0.06))

    item = join_parts(parts, "HealthPotion")

    def potion_color(pos):
        lx = pos.x - offset_x
        dist = math.sqrt(lx ** 2 + pos.y ** 2)
        # Cork
        if pos.z > 0.25:
            return (0.55, 0.40, 0.22, 1.0)
        # Neck (glass)
        if pos.z > 0.18 and dist < 0.04:
            return (0.75, 0.80, 0.82, 1.0)
        # Liquid (visible through glass -- red)
        if dist < 0.05 and pos.z < 0.16:
            return (0.85, 0.12, 0.10, 1.0)
        # Glass body
        return (0.70, 0.75, 0.78, 1.0)

    set_vertex_colors(item, potion_color)
    mat = make_vc_material("PotionMat", roughness=0.2, metallic=0.0)
    item.data.materials.append(mat)
    smooth_shade(item)
    set_origin_bottom(item)

    print(f"  Health Potion: {len(item.data.vertices)} verts, {len(item.data.polygons)} polys")
    return item


# =================================================================
#  SHIELD -- round wooden shield with metal boss
# =================================================================
def generate_shield(offset_x=1.5):
    parts = []

    # Shield disc (flattened sphere for slight convexity)
    add_part(parts, "SH_Disc", 'uv_sphere', (offset_x, 0, 0.25), (0.22, 0.03, 0.22))

    # Central boss (metal dome)
    add_part(parts, "SH_Boss", 'uv_sphere', (offset_x, -0.03, 0.25), (0.06, 0.04, 0.06))

    # Rim ring
    add_part(parts, "SH_Rim", 'cylinder', (offset_x, 0, 0.25), (0.23, 0.23, 0.015),
             rot=(math.radians(90), 0, 0))

    # Cross braces (decorative)
    add_part(parts, "SH_BraceH", 'cube', (offset_x, -0.01, 0.25), (0.20, 0.008, 0.015))
    add_part(parts, "SH_BraceV", 'cube', (offset_x, -0.01, 0.25), (0.015, 0.008, 0.20))

    # Handle (back)
    add_part(parts, "SH_Handle", 'cube', (offset_x, 0.04, 0.25), (0.06, 0.015, 0.02))

    item = join_parts(parts, "Shield")

    def shield_color(pos):
        lx = pos.x - offset_x
        dist = math.sqrt(lx ** 2 + (pos.z - 0.25) ** 2)
        # Boss (metal)
        if dist < 0.07 and pos.y < 0.0:
            return (0.55, 0.52, 0.48, 1.0)
        # Rim (metal)
        if dist > 0.20:
            return (0.48, 0.45, 0.40, 1.0)
        # Cross braces (metal)
        if (abs(lx) < 0.02 or abs(pos.z - 0.25) < 0.02) and pos.y < 0.0:
            return (0.50, 0.48, 0.42, 1.0)
        # Handle (leather)
        if pos.y > 0.02:
            return (0.38, 0.22, 0.10, 1.0)
        # Wood sections (quadrant coloring for painted look)
        if lx > 0 and pos.z > 0.25:
            return (0.55, 0.30, 0.12, 1.0)  # Warm wood
        if lx < 0 and pos.z < 0.25:
            return (0.55, 0.30, 0.12, 1.0)
        return (0.48, 0.28, 0.14, 1.0)  # Slightly darker wood

    set_vertex_colors(item, shield_color)
    mat = make_vc_material("ShieldMat", roughness=0.65, metallic=0.1)
    item.data.materials.append(mat)
    smooth_shade(item)
    set_origin_bottom(item)

    print(f"  Shield: {len(item.data.vertices)} verts, {len(item.data.polygons)} polys")
    return item


# =================================================================
#  AXE -- one-handed battle axe
# =================================================================
def generate_axe(offset_x=3):
    parts = []

    # Handle
    add_part(parts, "AX_Handle", 'cylinder', (offset_x, 0, 0.30), (0.018, 0.018, 0.28))

    # Axe head (wedge shape -- cube stretched + tapered)
    add_part(parts, "AX_Head", 'cube', (offset_x + 0.08, 0, 0.52), (0.08, 0.015, 0.06))

    # Axe blade edge (thinner)
    add_part(parts, "AX_Blade", 'cube', (offset_x + 0.17, 0, 0.52), (0.02, 0.005, 0.07))

    # Axe head back (poll)
    add_part(parts, "AX_Poll", 'cube', (offset_x - 0.02, 0, 0.52), (0.02, 0.02, 0.03))

    # Handle wrap (leather grip)
    add_part(parts, "AX_Grip", 'cylinder', (offset_x, 0, 0.12), (0.022, 0.022, 0.10))

    # Pommel
    add_part(parts, "AX_Pommel", 'uv_sphere', (offset_x, 0, 0.02), (0.025, 0.025, 0.02))

    item = join_parts(parts, "Axe")

    def axe_color(pos):
        lx = pos.x - offset_x
        # Blade edge (bright metal)
        if lx > 0.14 and abs(pos.z - 0.52) < 0.08:
            return (0.70, 0.68, 0.65, 1.0)
        # Axe head (darker metal)
        if lx > 0.02 and abs(pos.z - 0.52) < 0.08:
            return (0.48, 0.46, 0.44, 1.0)
        # Poll
        if lx < 0.0 and abs(pos.z - 0.52) < 0.04:
            return (0.45, 0.43, 0.40, 1.0)
        # Grip (leather)
        if pos.z < 0.22 and pos.z > 0.02:
            return (0.35, 0.20, 0.08, 1.0)
        # Pommel (metal)
        if pos.z < 0.05:
            return (0.50, 0.48, 0.42, 1.0)
        # Handle (wood)
        return (0.50, 0.35, 0.18, 1.0)

    set_vertex_colors(item, axe_color)
    mat = make_vc_material("AxeMat", roughness=0.5, metallic=0.3)
    item.data.materials.append(mat)
    smooth_shade(item)
    set_origin_bottom(item)

    print(f"  Axe: {len(item.data.vertices)} verts, {len(item.data.polygons)} polys")
    return item


# =================================================================
#  HELMET -- simple iron helm
# =================================================================
def generate_helmet(offset_x=4.5):
    parts = []

    # Dome (half sphere)
    add_part(parts, "HE_Dome", 'uv_sphere', (offset_x, 0, 0.15), (0.14, 0.12, 0.12))

    # Nose guard
    add_part(parts, "HE_Nose", 'cube', (offset_x, -0.13, 0.10), (0.015, 0.04, 0.08))

    # Brim (wide disc around base)
    add_part(parts, "HE_Brim", 'cylinder', (offset_x, 0, 0.05), (0.16, 0.14, 0.015))

    # Ridge crest on top
    add_part(parts, "HE_Crest", 'cube', (offset_x, 0, 0.26), (0.015, 0.10, 0.04))

    # Cheek guards
    for side in [-1, 1]:
        add_part(parts, f"HE_Cheek_{side}", 'cube',
                 (offset_x + side * 0.13, 0.02, 0.06), (0.02, 0.05, 0.06))

    item = join_parts(parts, "Helmet")

    def helmet_color(pos):
        lx = pos.x - offset_x
        # Nose guard (slightly different metal)
        if pos.y < -0.10 and abs(lx) < 0.025:
            return (0.42, 0.40, 0.38, 1.0)
        # Crest (brighter)
        if pos.z > 0.22 and abs(lx) < 0.025:
            return (0.55, 0.52, 0.48, 1.0)
        # Brim
        if pos.z < 0.07:
            return (0.40, 0.38, 0.35, 1.0)
        # Dome (iron with slight wear variation)
        height_factor = (pos.z - 0.05) / 0.25
        base = 0.45 + height_factor * 0.05
        return (base, base - 0.02, base - 0.04, 1.0)

    set_vertex_colors(item, helmet_color)
    mat = make_vc_material("HelmetMat", roughness=0.55, metallic=0.5)
    item.data.materials.append(mat)
    smooth_shade(item)
    set_origin_bottom(item)

    print(f"  Helmet: {len(item.data.vertices)} verts, {len(item.data.polygons)} polys")
    return item


# =================================================================
#  BONE FRAGMENT -- curved bone piece
# =================================================================
def generate_bone_fragment(offset_x=6):
    parts = []

    # Main bone shaft (curved cylinder)
    add_part(parts, "BN_Shaft", 'cylinder', (offset_x, 0, 0.06), (0.02, 0.02, 0.12),
             rot=(0, math.radians(15), 0))

    # Joint knob at one end
    add_part(parts, "BN_KnobA", 'uv_sphere', (offset_x - 0.03, 0, 0.16), (0.03, 0.025, 0.025))

    # Joint knob at other end
    add_part(parts, "BN_KnobB", 'uv_sphere', (offset_x + 0.03, 0, -0.02), (0.025, 0.02, 0.02))

    item = join_parts(parts, "BoneFragment")

    def bone_color(pos):
        # Slightly yellowish white bone
        height_factor = (pos.z + 0.05) / 0.25
        r = 0.85 + height_factor * 0.02
        g = 0.80 + height_factor * 0.01
        b = 0.70
        # Joint knobs slightly darker
        lx = pos.x - offset_x
        if (abs(lx + 0.03) < 0.04 and pos.z > 0.12) or (abs(lx - 0.03) < 0.04 and pos.z < 0.02):
            r -= 0.08
            g -= 0.08
            b -= 0.05
        return (min(1.0, r), min(1.0, g), min(1.0, b), 1.0)

    set_vertex_colors(item, bone_color)
    mat = make_vc_material("BoneMat", roughness=0.7)
    item.data.materials.append(mat)
    smooth_shade(item)
    set_origin_bottom(item)

    print(f"  Bone Fragment: {len(item.data.vertices)} verts, {len(item.data.polygons)} polys")
    return item


# =================================================================
#  WOLF PELT -- flat draped fur
# =================================================================
def generate_wolf_pelt(offset_x=7.5):
    parts = []

    # Main pelt body (flat stretched shape)
    add_part(parts, "WP_Body", 'cube', (offset_x, 0, 0.03), (0.18, 0.12, 0.02))

    # Head part (slightly raised, smaller)
    add_part(parts, "WP_Head", 'uv_sphere', (offset_x, -0.15, 0.05), (0.06, 0.05, 0.03))

    # Leg stubs (four corners, small)
    for sx in [-1, 1]:
        for sy in [-1, 1]:
            add_part(parts, f"WP_Leg_{sx}_{sy}", 'cube',
                     (offset_x + sx * 0.15, sy * 0.08, 0.02), (0.04, 0.02, 0.015))

    # Tail
    add_part(parts, "WP_Tail", 'cone', (offset_x, 0.16, 0.04), (0.02, 0.06, 0.015),
             rot=(math.radians(90), 0, 0))

    item = join_parts(parts, "WolfPelt")

    def pelt_color(pos):
        lx = pos.x - offset_x
        # Head area (darker)
        if pos.y < -0.10:
            return (0.38, 0.32, 0.25, 1.0)
        # Spine stripe (darker along center)
        if abs(lx) < 0.04:
            return (0.40, 0.35, 0.28, 1.0)
        # Belly area (lighter)
        if abs(lx) > 0.12:
            return (0.58, 0.52, 0.45, 1.0)
        # Main fur
        return (0.48, 0.42, 0.35, 1.0)

    set_vertex_colors(item, pelt_color)
    mat = make_vc_material("PeltMat", roughness=0.85)
    item.data.materials.append(mat)
    smooth_shade(item)
    set_origin_bottom(item)

    print(f"  Wolf Pelt: {len(item.data.vertices)} verts, {len(item.data.polygons)} polys")
    return item


# =================================================================
#  MAIN
# =================================================================
def main():
    clear_scene()

    print("=" * 60)
    print("  Generating Items")
    print("=" * 60)

    generate_health_potion(offset_x=0)
    generate_shield(offset_x=1.5)
    generate_axe(offset_x=3)
    generate_helmet(offset_x=4.5)
    generate_bone_fragment(offset_x=6)
    generate_wolf_pelt(offset_x=7.5)

    export_glb(os.path.join(EXPORT_DIR, "items.glb"))

    print("=" * 60)
    print("  All items generated!")
    print("=" * 60)


if __name__ == "__main__":
    main()
