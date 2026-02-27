"""
generate_npcs.py
================
Blender Python script -- run headless:
    blender --background --python BlenderPipeline/scripts/generate_npcs.py

Generates four NPC models with vertex colors, all in one GLB:
  - Villager   (~400 tris) -- tunic, belt, boots
  - Blacksmith (~500 tris) -- stocky, apron, hammer
  - Merchant   (~450 tris) -- robed, hooded, backpack
  - Scout      (~450 tris) -- hooded cloak, quiver

Exports to BlenderPipeline/exports/npcs.glb
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
    """Apply vertex colors using a function(vertex_position) -> (r,g,b,a)."""
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
    """Create a Principled BSDF material reading vertex colors."""
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


def set_origin_bottom(obj, z_offset=0.0):
    """Move vertices so the mesh is centered in X/Y with bottom at Z=0.
    This strips out any offset_x baked into vertex positions."""
    verts = obj.data.vertices
    xs = [v.co.x for v in verts]
    ys = [v.co.y for v in verts]
    zs = [v.co.z for v in verts]
    cx = (min(xs) + max(xs)) * 0.5
    cy = (min(ys) + max(ys)) * 0.5
    min_z = min(zs) + z_offset
    for v in verts:
        v.co.x -= cx
        v.co.y -= cy
        v.co.z -= min_z
    obj.location = (0, 0, 0)


# =================================================================
#  VILLAGER -- simple tunic, belt, boots
# =================================================================
def generate_villager(offset_x=0):
    parts = []

    # Head
    add_part(parts, "V_Head", 'uv_sphere', (offset_x, 0, 1.55), (0.11, 0.12, 0.13))

    # Hair (flat cap on top)
    add_part(parts, "V_Hair", 'uv_sphere', (offset_x, 0, 1.68), (0.12, 0.12, 0.06))

    # Torso (tunic)
    add_part(parts, "V_Torso", 'cylinder', (offset_x, 0, 1.15), (0.14, 0.10, 0.20))

    # Tunic skirt (flared bottom)
    add_part(parts, "V_Skirt", 'cone', (offset_x, 0, 0.88), (0.18, 0.12, 0.10))

    # Belt
    add_part(parts, "V_Belt", 'cylinder', (offset_x, 0, 0.95), (0.15, 0.11, 0.025))

    # Arms
    for side in [-1, 1]:
        add_part(parts, f"V_UpperArm_{side}", 'cylinder',
                 (offset_x + side * 0.18, 0, 1.22), (0.04, 0.04, 0.12))
        add_part(parts, f"V_LowerArm_{side}", 'cylinder',
                 (offset_x + side * 0.18, 0, 0.98), (0.035, 0.035, 0.10))
        add_part(parts, f"V_Hand_{side}", 'uv_sphere',
                 (offset_x + side * 0.18, 0, 0.87), (0.03, 0.025, 0.035))

    # Legs
    for side in [-1, 1]:
        add_part(parts, f"V_UpperLeg_{side}", 'cylinder',
                 (offset_x + side * 0.06, 0, 0.60), (0.05, 0.045, 0.14))
        add_part(parts, f"V_Boot_{side}", 'cylinder',
                 (offset_x + side * 0.06, 0, 0.35), (0.05, 0.055, 0.12))
        add_part(parts, f"V_Sole_{side}", 'cube',
                 (offset_x + side * 0.06, -0.01, 0.24), (0.05, 0.07, 0.02))

    npc = join_parts(parts, "Villager")

    def villager_color(pos):
        lx = pos.x - offset_x
        # Head/hands -- skin tone
        if pos.z > 1.42 and pos.z < 1.65:
            return (0.85, 0.70, 0.58, 1.0)
        if pos.z > 1.65:  # Hair
            return (0.35, 0.22, 0.12, 1.0)
        # Hands
        if pos.z < 0.92 and pos.z > 0.82 and abs(lx) > 0.12:
            return (0.85, 0.70, 0.58, 1.0)
        # Belt
        if pos.z > 0.92 and pos.z < 0.98:
            return (0.35, 0.22, 0.10, 1.0)
        # Tunic
        if pos.z > 0.80 and pos.z < 1.42:
            return (0.25, 0.45, 0.20, 1.0)  # Green tunic
        # Boots
        if pos.z < 0.50:
            return (0.30, 0.18, 0.10, 1.0)
        # Pants
        return (0.40, 0.35, 0.28, 1.0)

    set_vertex_colors(npc, villager_color)
    mat = make_vc_material("VillagerMat", roughness=0.7)
    npc.data.materials.append(mat)
    smooth_shade(npc)
    set_origin_bottom(npc)

    print(f"  Villager: {len(npc.data.vertices)} verts, {len(npc.data.polygons)} polys")
    return npc


# =================================================================
#  BLACKSMITH -- stocky, apron, hammer
# =================================================================
def generate_blacksmith(offset_x=3):
    parts = []

    # Head
    add_part(parts, "B_Head", 'uv_sphere', (offset_x, 0, 1.50), (0.12, 0.13, 0.13))

    # Torso (wider/stockier)
    add_part(parts, "B_Torso", 'cylinder', (offset_x, 0, 1.12), (0.17, 0.13, 0.22))

    # Apron (front plate)
    add_part(parts, "B_Apron", 'cube', (offset_x, -0.12, 0.95), (0.13, 0.02, 0.25))

    # Apron strap
    add_part(parts, "B_Strap", 'cube', (offset_x, -0.09, 1.28), (0.02, 0.02, 0.10))

    # Belt
    add_part(parts, "B_Belt", 'cylinder', (offset_x, 0, 0.90), (0.18, 0.14, 0.03))

    # Arms (thick)
    for side in [-1, 1]:
        add_part(parts, f"B_UpperArm_{side}", 'cylinder',
                 (offset_x + side * 0.22, 0, 1.18), (0.055, 0.055, 0.14))
        add_part(parts, f"B_LowerArm_{side}", 'cylinder',
                 (offset_x + side * 0.22, 0, 0.94), (0.05, 0.05, 0.12))
        add_part(parts, f"B_Hand_{side}", 'uv_sphere',
                 (offset_x + side * 0.22, 0, 0.82), (0.04, 0.03, 0.04))

    # Hammer (right hand)
    add_part(parts, "B_HammerHandle", 'cylinder',
             (offset_x + 0.22, -0.05, 0.65), (0.015, 0.015, 0.15))
    add_part(parts, "B_HammerHead", 'cube',
             (offset_x + 0.22, -0.05, 0.50), (0.05, 0.035, 0.04))

    # Legs
    for side in [-1, 1]:
        add_part(parts, f"B_UpperLeg_{side}", 'cylinder',
                 (offset_x + side * 0.07, 0, 0.58), (0.06, 0.05, 0.15))
        add_part(parts, f"B_Boot_{side}", 'cylinder',
                 (offset_x + side * 0.07, 0, 0.32), (0.055, 0.06, 0.12))
        add_part(parts, f"B_Sole_{side}", 'cube',
                 (offset_x + side * 0.07, -0.01, 0.22), (0.055, 0.075, 0.025))

    npc = join_parts(parts, "Blacksmith")

    def blacksmith_color(pos):
        lx = pos.x - offset_x
        # Head -- skin
        if pos.z > 1.38:
            return (0.75, 0.58, 0.48, 1.0)
        # Hands
        if pos.z < 0.88 and pos.z > 0.76 and abs(lx) > 0.15:
            return (0.75, 0.58, 0.48, 1.0)
        # Hammer head (metal)
        if pos.z < 0.56 and pos.z > 0.44 and lx > 0.15:
            return (0.50, 0.50, 0.52, 1.0)
        # Hammer handle
        if pos.z < 0.80 and pos.z > 0.50 and lx > 0.18 and abs(pos.y + 0.05) < 0.05:
            return (0.45, 0.30, 0.15, 1.0)
        # Apron (leather brown)
        if pos.y < -0.08 and pos.z > 0.68 and pos.z < 1.30 and abs(lx) < 0.15:
            return (0.40, 0.25, 0.12, 1.0)
        # Belt
        if pos.z > 0.87 and pos.z < 0.93:
            return (0.30, 0.18, 0.08, 1.0)
        # Shirt (dark grey)
        if pos.z > 0.80 and pos.z < 1.38:
            return (0.25, 0.25, 0.28, 1.0)
        # Boots
        if pos.z < 0.45:
            return (0.28, 0.16, 0.08, 1.0)
        # Pants
        return (0.35, 0.30, 0.25, 1.0)

    set_vertex_colors(npc, blacksmith_color)
    mat = make_vc_material("BlacksmithMat", roughness=0.75)
    npc.data.materials.append(mat)
    smooth_shade(npc)
    set_origin_bottom(npc)

    print(f"  Blacksmith: {len(npc.data.vertices)} verts, {len(npc.data.polygons)} polys")
    return npc


# =================================================================
#  MERCHANT -- robed, hooded, backpack
# =================================================================
def generate_merchant(offset_x=6):
    parts = []

    # Head
    add_part(parts, "M_Head", 'uv_sphere', (offset_x, 0, 1.55), (0.11, 0.12, 0.13))

    # Hood
    add_part(parts, "M_Hood", 'uv_sphere', (offset_x, 0.02, 1.62), (0.14, 0.15, 0.14))

    # Robe body (long flowing)
    add_part(parts, "M_RobeUpper", 'cylinder', (offset_x, 0, 1.15), (0.15, 0.11, 0.22))

    # Robe skirt (wide flared)
    add_part(parts, "M_RobeSkirt", 'cone', (offset_x, 0, 0.72), (0.22, 0.15, 0.25))

    # Sleeves (wide)
    for side in [-1, 1]:
        add_part(parts, f"M_Sleeve_{side}", 'cone',
                 (offset_x + side * 0.20, 0, 1.08), (0.06, 0.04, 0.20),
                 rot=(0, 0, side * math.radians(-15)))

    # Hands poking out of sleeves
    for side in [-1, 1]:
        add_part(parts, f"M_Hand_{side}", 'uv_sphere',
                 (offset_x + side * 0.22, 0, 0.90), (0.025, 0.02, 0.03))

    # Backpack
    add_part(parts, "M_Pack", 'cube', (offset_x, 0.14, 1.10), (0.10, 0.08, 0.14))
    add_part(parts, "M_PackFlap", 'cube', (offset_x, 0.14, 1.26), (0.11, 0.09, 0.02))

    # Pack straps
    for side in [-1, 1]:
        add_part(parts, f"M_Strap_{side}", 'cube',
                 (offset_x + side * 0.06, 0.04, 1.20), (0.015, 0.08, 0.10))

    # Sash/belt
    add_part(parts, "M_Sash", 'cylinder', (offset_x, 0, 0.92), (0.16, 0.12, 0.025))

    npc = join_parts(parts, "Merchant")

    def merchant_color(pos):
        lx = pos.x - offset_x
        # Face -- skin
        if pos.z > 1.42 and pos.z < 1.60 and pos.y < 0.02:
            return (0.82, 0.68, 0.55, 1.0)
        # Hood
        if pos.z > 1.55:
            return (0.50, 0.22, 0.40, 1.0)  # Purple hood
        # Hands
        if pos.z < 0.96 and pos.z > 0.85 and abs(lx) > 0.16:
            return (0.82, 0.68, 0.55, 1.0)
        # Backpack
        if pos.y > 0.10 and pos.z > 0.95:
            return (0.45, 0.30, 0.18, 1.0)
        # Straps
        if abs(pos.y - 0.04) < 0.05 and abs(lx) < 0.08 and pos.z > 1.10:
            return (0.40, 0.25, 0.12, 1.0)
        # Sash
        if pos.z > 0.89 and pos.z < 0.95:
            return (0.70, 0.55, 0.15, 1.0)  # Gold sash
        # Robe
        if pos.z > 0.45:
            return (0.55, 0.25, 0.45, 1.0)  # Purple robe
        # Robe bottom (darker)
        return (0.45, 0.18, 0.35, 1.0)

    set_vertex_colors(npc, merchant_color)
    mat = make_vc_material("MerchantMat", roughness=0.65)
    npc.data.materials.append(mat)
    smooth_shade(npc)
    set_origin_bottom(npc)

    print(f"  Merchant: {len(npc.data.vertices)} verts, {len(npc.data.polygons)} polys")
    return npc


# =================================================================
#  SCOUT -- hooded cloak, quiver on back
# =================================================================
def generate_scout(offset_x=9):
    parts = []

    # Head
    add_part(parts, "S_Head", 'uv_sphere', (offset_x, 0, 1.55), (0.11, 0.12, 0.12))

    # Hood (pointed)
    add_part(parts, "S_Hood", 'cone', (offset_x, 0.02, 1.68), (0.13, 0.13, 0.10))

    # Cloak (draped over shoulders, down the back)
    add_part(parts, "S_CloakUpper", 'cylinder', (offset_x, 0.02, 1.22), (0.16, 0.12, 0.18))
    add_part(parts, "S_CloakLower", 'cone', (offset_x, 0.03, 0.85), (0.18, 0.13, 0.22))

    # Body under cloak (visible from front)
    add_part(parts, "S_Torso", 'cylinder', (offset_x, -0.02, 1.15), (0.12, 0.09, 0.18))

    # Belt
    add_part(parts, "S_Belt", 'cylinder', (offset_x, 0, 0.93), (0.14, 0.10, 0.025))

    # Arms
    for side in [-1, 1]:
        add_part(parts, f"S_UpperArm_{side}", 'cylinder',
                 (offset_x + side * 0.18, 0, 1.18), (0.04, 0.04, 0.12))
        add_part(parts, f"S_LowerArm_{side}", 'cylinder',
                 (offset_x + side * 0.18, 0, 0.95), (0.035, 0.035, 0.10))
        add_part(parts, f"S_Hand_{side}", 'uv_sphere',
                 (offset_x + side * 0.18, 0, 0.85), (0.03, 0.025, 0.03))

    # Quiver on back
    add_part(parts, "S_Quiver", 'cylinder',
             (offset_x + 0.08, 0.10, 1.18), (0.03, 0.03, 0.18))
    # Arrow tips poking out
    for i in range(3):
        add_part(parts, f"S_Arrow_{i}", 'cone',
                 (offset_x + 0.06 + i * 0.02, 0.10, 1.38), (0.008, 0.008, 0.02))

    # Legs
    for side in [-1, 1]:
        add_part(parts, f"S_UpperLeg_{side}", 'cylinder',
                 (offset_x + side * 0.06, 0, 0.58), (0.045, 0.04, 0.14))
        add_part(parts, f"S_Boot_{side}", 'cylinder',
                 (offset_x + side * 0.06, 0, 0.32), (0.045, 0.05, 0.12))
        add_part(parts, f"S_Sole_{side}", 'cube',
                 (offset_x + side * 0.06, -0.01, 0.22), (0.045, 0.065, 0.02))

    npc = join_parts(parts, "Scout")

    def scout_color(pos):
        lx = pos.x - offset_x
        # Face -- skin
        if pos.z > 1.42 and pos.z < 1.58 and pos.y < 0.01:
            return (0.80, 0.65, 0.52, 1.0)
        # Hood
        if pos.z > 1.58:
            return (0.22, 0.32, 0.18, 1.0)  # Dark green
        # Hands
        if pos.z < 0.90 and pos.z > 0.80 and abs(lx) > 0.12:
            return (0.80, 0.65, 0.52, 1.0)
        # Quiver
        if lx > 0.04 and pos.y > 0.06 and pos.z > 1.00:
            return (0.45, 0.30, 0.15, 1.0)  # Leather quiver
        # Arrow tips
        if pos.z > 1.35 and lx > 0.04:
            return (0.55, 0.55, 0.58, 1.0)  # Metal tips
        # Cloak
        if pos.y > 0.0 and pos.z > 0.70:
            return (0.25, 0.35, 0.20, 1.0)  # Green cloak
        # Belt
        if pos.z > 0.90 and pos.z < 0.96:
            return (0.35, 0.22, 0.10, 1.0)
        # Tunic (under cloak)
        if pos.z > 0.80 and pos.z < 1.42:
            return (0.35, 0.28, 0.18, 1.0)  # Brown
        # Boots
        if pos.z < 0.45:
            return (0.25, 0.18, 0.10, 1.0)
        # Pants
        return (0.30, 0.28, 0.22, 1.0)

    set_vertex_colors(npc, scout_color)
    mat = make_vc_material("ScoutMat", roughness=0.7)
    npc.data.materials.append(mat)
    smooth_shade(npc)
    set_origin_bottom(npc)

    print(f"  Scout: {len(npc.data.vertices)} verts, {len(npc.data.polygons)} polys")
    return npc


# =================================================================
#  MAIN
# =================================================================
def main():
    clear_scene()

    print("=" * 60)
    print("  Generating NPCs")
    print("=" * 60)

    villager = generate_villager(offset_x=0)
    blacksmith = generate_blacksmith(offset_x=3)
    merchant = generate_merchant(offset_x=6)
    scout = generate_scout(offset_x=9)

    export_glb(os.path.join(EXPORT_DIR, "npcs.glb"))

    print("=" * 60)
    print("  All NPCs generated!")
    print("=" * 60)


if __name__ == "__main__":
    main()
