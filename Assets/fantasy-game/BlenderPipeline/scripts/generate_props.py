"""
generate_props.py
=================
Blender Python script -- run headless:
    blender --background --python BlenderPipeline/scripts/generate_props.py

Generates six prop models with vertex colors, all in one GLB:
  - Campfire       (~300 tris) -- log ring with stone surround
  - Treasure Chest (~250 tris) -- wooden box with metal bands
  - Crate          (~100 tris) -- wooden crate with plank lines
  - Barrel         (~150 tris) -- wooden barrel with hoops
  - Training Dummy (~200 tris) -- padded post with straw head
  - Signpost       (~80 tris)  -- wooden post with sign plank

Exports to BlenderPipeline/exports/props.glb
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
    elif prim == 'torus':
        bpy.ops.mesh.primitive_torus_add(location=loc, major_radius=1, minor_radius=0.25,
                                          major_segments=12, minor_segments=6)
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
#  CAMPFIRE -- logs in a ring with stone surround
# =================================================================
def generate_campfire(offset_x=0):
    parts = []

    # Stone ring (6 stones)
    for i in range(6):
        angle = i * math.pi * 2 / 6
        x = offset_x + math.cos(angle) * 0.35
        y = math.sin(angle) * 0.35
        add_part(parts, f"CF_Stone_{i}", 'uv_sphere', (x, y, 0.06), (0.07, 0.06, 0.05))

    # Logs (3 logs in triangle)
    for i in range(3):
        angle = i * math.pi * 2 / 3 + math.pi / 6
        x = offset_x + math.cos(angle) * 0.12
        y = math.sin(angle) * 0.12
        add_part(parts, f"CF_Log_{i}", 'cylinder', (x, y, 0.08),
                 (0.04, 0.04, 0.20),
                 rot=(math.radians(90), 0, angle))

    # Charred center base
    add_part(parts, "CF_Ash", 'cylinder', (offset_x, 0, 0.02), (0.15, 0.15, 0.02))

    # Embers (small spheres in center)
    for i in range(4):
        angle = i * math.pi * 2 / 4 + 0.3
        x = offset_x + math.cos(angle) * 0.06
        y = math.sin(angle) * 0.06
        add_part(parts, f"CF_Ember_{i}", 'uv_sphere', (x, y, 0.10), (0.025, 0.025, 0.02))

    prop = join_parts(parts, "Campfire")

    def campfire_color(pos):
        lx = pos.x - offset_x
        dist = math.sqrt(lx ** 2 + pos.y ** 2)
        # Stones (outer ring)
        if dist > 0.25 and pos.z < 0.15:
            return (0.42, 0.40, 0.38, 1.0)
        # Embers (center, hot)
        if dist < 0.10 and pos.z > 0.06:
            return (0.90, 0.35, 0.08, 1.0)
        # Ash
        if dist < 0.16 and pos.z < 0.05:
            return (0.15, 0.12, 0.10, 1.0)
        # Logs
        if pos.z > 0.02 and pos.z < 0.20:
            return (0.35, 0.20, 0.10, 1.0)
        return (0.30, 0.18, 0.08, 1.0)

    set_vertex_colors(prop, campfire_color)
    mat = make_vc_material("CampfireMat", roughness=0.85)
    prop.data.materials.append(mat)
    smooth_shade(prop)
    set_origin_bottom(prop)

    print(f"  Campfire: {len(prop.data.vertices)} verts, {len(prop.data.polygons)} polys")
    return prop


# =================================================================
#  TREASURE CHEST -- wooden box with metal bands
# =================================================================
def generate_chest(offset_x=2):
    parts = []

    # Main box body
    add_part(parts, "CH_Body", 'cube', (offset_x, 0, 0.18), (0.28, 0.18, 0.15))

    # Lid (slightly arched -- squashed sphere top)
    add_part(parts, "CH_Lid", 'cube', (offset_x, 0, 0.36), (0.29, 0.19, 0.06))
    add_part(parts, "CH_LidTop", 'cylinder', (offset_x, 0, 0.40), (0.28, 0.18, 0.04),
             rot=(math.radians(90), 0, 0))

    # Metal bands (horizontal stripes)
    for z in [0.10, 0.25, 0.34]:
        add_part(parts, f"CH_Band_{z}", 'cube', (offset_x, 0, z), (0.30, 0.20, 0.012))

    # Lock plate (front)
    add_part(parts, "CH_Lock", 'cube', (offset_x, -0.19, 0.24), (0.04, 0.01, 0.05))

    # Lock keyhole
    add_part(parts, "CH_Keyhole", 'uv_sphere', (offset_x, -0.20, 0.24), (0.012, 0.008, 0.015))

    # Corner reinforcements
    for sx in [-1, 1]:
        for sy in [-1, 1]:
            add_part(parts, f"CH_Corner_{sx}_{sy}", 'cube',
                     (offset_x + sx * 0.27, sy * 0.17, 0.18), (0.025, 0.025, 0.16))

    # Hinges (back)
    for sx in [-1, 1]:
        add_part(parts, f"CH_Hinge_{sx}", 'cylinder',
                 (offset_x + sx * 0.15, 0.19, 0.32), (0.02, 0.02, 0.015))

    prop = join_parts(parts, "TreasureChest")

    def chest_color(pos):
        lx = pos.x - offset_x
        # Metal bands
        for z in [0.10, 0.25, 0.34]:
            if abs(pos.z - z) < 0.015:
                return (0.55, 0.50, 0.30, 1.0)  # Brass
        # Lock
        if abs(pos.y + 0.19) < 0.03 and abs(pos.z - 0.24) < 0.06 and abs(lx) < 0.05:
            return (0.60, 0.55, 0.25, 1.0)  # Gold lock
        # Corner reinforcements
        if abs(abs(lx) - 0.27) < 0.04 and abs(abs(pos.y) - 0.17) < 0.04:
            return (0.50, 0.45, 0.28, 1.0)
        # Hinges
        if pos.y > 0.16 and pos.z > 0.29:
            return (0.45, 0.42, 0.30, 1.0)
        # Lid
        if pos.z > 0.32:
            return (0.42, 0.25, 0.12, 1.0)  # Darker wood
        # Body
        return (0.50, 0.32, 0.15, 1.0)  # Wood

    set_vertex_colors(prop, chest_color)
    mat = make_vc_material("ChestMat", roughness=0.7)
    prop.data.materials.append(mat)
    smooth_shade(prop)
    set_origin_bottom(prop)

    print(f"  Chest: {len(prop.data.vertices)} verts, {len(prop.data.polygons)} polys")
    return prop


# =================================================================
#  CRATE -- wooden crate with plank detail
# =================================================================
def generate_crate(offset_x=4):
    parts = []

    # Main body
    add_part(parts, "CR_Body", 'cube', (offset_x, 0, 0.25), (0.24, 0.24, 0.24))

    # Plank lines (raised strips for detail)
    for i in range(3):
        z = 0.10 + i * 0.15
        # Front
        add_part(parts, f"CR_PlankF_{i}", 'cube', (offset_x, -0.25, z), (0.22, 0.005, 0.02))
        # Back
        add_part(parts, f"CR_PlankB_{i}", 'cube', (offset_x, 0.25, z), (0.22, 0.005, 0.02))

    # Cross braces on sides
    for side in [-1, 1]:
        add_part(parts, f"CR_BraceH_{side}", 'cube',
                 (offset_x + side * 0.25, 0, 0.25), (0.005, 0.22, 0.02))
        add_part(parts, f"CR_BraceV_{side}", 'cube',
                 (offset_x + side * 0.25, 0, 0.25), (0.005, 0.02, 0.22))

    # Top edge rim
    add_part(parts, "CR_Rim", 'cube', (offset_x, 0, 0.49), (0.25, 0.25, 0.01))

    prop = join_parts(parts, "Crate")

    def crate_color(pos):
        lx = pos.x - offset_x
        # Plank lines / braces (slightly darker)
        if abs(abs(pos.y) - 0.25) < 0.01 or abs(abs(lx) - 0.25) < 0.01:
            return (0.38, 0.24, 0.10, 1.0)
        # Rim
        if pos.z > 0.47:
            return (0.42, 0.28, 0.12, 1.0)
        # Main wood with slight grain variation
        grain = math.sin(pos.z * 20) * 0.03
        return (0.52 + grain, 0.35 + grain, 0.18, 1.0)

    set_vertex_colors(prop, crate_color)
    mat = make_vc_material("CrateMat", roughness=0.8)
    prop.data.materials.append(mat)
    set_origin_bottom(prop)

    print(f"  Crate: {len(prop.data.vertices)} verts, {len(prop.data.polygons)} polys")
    return prop


# =================================================================
#  BARREL -- wooden barrel with metal hoops
# =================================================================
def generate_barrel(offset_x=6):
    parts = []

    # Main barrel body (cylinder, slightly wider at middle)
    add_part(parts, "BA_Body", 'cylinder', (offset_x, 0, 0.35), (0.18, 0.18, 0.33))

    # Belly bulge (slightly wider ring at middle)
    add_part(parts, "BA_Belly", 'cylinder', (offset_x, 0, 0.35), (0.20, 0.20, 0.12))

    # Metal hoops
    for z in [0.12, 0.35, 0.58]:
        add_part(parts, f"BA_Hoop_{z}", 'cylinder', (offset_x, 0, z), (0.21, 0.21, 0.015))

    # Top cap
    add_part(parts, "BA_Cap", 'cylinder', (offset_x, 0, 0.68), (0.17, 0.17, 0.01))

    # Bung hole on side
    add_part(parts, "BA_Bung", 'cylinder', (offset_x, -0.20, 0.40), (0.025, 0.025, 0.01),
             rot=(math.radians(90), 0, 0))

    prop = join_parts(parts, "Barrel")

    def barrel_color(pos):
        lx = pos.x - offset_x
        dist = math.sqrt(lx ** 2 + pos.y ** 2)
        # Metal hoops
        for z in [0.12, 0.35, 0.58]:
            if abs(pos.z - z) < 0.02 and dist > 0.18:
                return (0.45, 0.42, 0.38, 1.0)  # Iron grey
        # Bung
        if abs(pos.y + 0.20) < 0.04 and abs(pos.z - 0.40) < 0.04:
            return (0.30, 0.20, 0.08, 1.0)  # Dark cork
        # Top cap
        if pos.z > 0.66:
            return (0.48, 0.32, 0.16, 1.0)
        # Barrel staves (alternating tones)
        angle = math.atan2(pos.y, lx)
        stave = int((angle / (2 * math.pi) + 0.5) * 8) % 2
        if stave:
            return (0.50, 0.33, 0.16, 1.0)
        return (0.55, 0.38, 0.20, 1.0)

    set_vertex_colors(prop, barrel_color)
    mat = make_vc_material("BarrelMat", roughness=0.75)
    prop.data.materials.append(mat)
    smooth_shade(prop)
    set_origin_bottom(prop)

    print(f"  Barrel: {len(prop.data.vertices)} verts, {len(prop.data.polygons)} polys")
    return prop


# =================================================================
#  TRAINING DUMMY -- padded post with crossbar and straw head
# =================================================================
def generate_training_dummy(offset_x=8):
    parts = []

    # Main post
    add_part(parts, "TD_Post", 'cylinder', (offset_x, 0, 0.60), (0.05, 0.05, 0.58))

    # Base plate
    add_part(parts, "TD_Base", 'cylinder', (offset_x, 0, 0.03), (0.20, 0.20, 0.03))

    # Crossbar
    add_part(parts, "TD_Crossbar", 'cylinder', (offset_x, 0, 1.00), (0.035, 0.035, 0.28),
             rot=(0, 0, math.radians(90)))

    # Padding wraps on crossbar ends
    for side in [-1, 1]:
        add_part(parts, f"TD_Pad_{side}", 'uv_sphere',
                 (offset_x + side * 0.28, 0, 1.00), (0.06, 0.05, 0.06))

    # Straw head (rough sphere)
    add_part(parts, "TD_Head", 'uv_sphere', (offset_x, 0, 1.28), (0.10, 0.09, 0.11))

    # Burlap body wrap (cylinder around post)
    add_part(parts, "TD_Wrap", 'cylinder', (offset_x, 0, 0.82), (0.08, 0.07, 0.15))

    # Target circle (flat disc on front)
    add_part(parts, "TD_Target", 'cylinder', (offset_x, -0.08, 0.82), (0.06, 0.06, 0.005),
             rot=(math.radians(90), 0, 0))

    prop = join_parts(parts, "TrainingDummy")

    def dummy_color(pos):
        lx = pos.x - offset_x
        dist = math.sqrt(lx ** 2 + pos.y ** 2)
        # Head (straw/burlap yellow)
        if pos.z > 1.18:
            return (0.72, 0.60, 0.38, 1.0)
        # Target (red circle)
        if abs(pos.y + 0.08) < 0.02 and abs(pos.z - 0.82) < 0.08 and abs(lx) < 0.07:
            d = math.sqrt(lx ** 2 + (pos.z - 0.82) ** 2)
            if d < 0.03:
                return (0.85, 0.15, 0.10, 1.0)  # Center red
            return (0.80, 0.20, 0.12, 1.0)  # Ring red
        # Burlap wrap
        if pos.z > 0.65 and pos.z < 1.00 and dist < 0.10:
            return (0.65, 0.52, 0.32, 1.0)
        # Padding
        if abs(lx) > 0.20 and abs(pos.z - 1.00) < 0.08:
            return (0.60, 0.48, 0.30, 1.0)
        # Crossbar / post (wood)
        if dist < 0.06:
            return (0.42, 0.28, 0.14, 1.0)
        # Base plate
        if pos.z < 0.06:
            return (0.35, 0.25, 0.12, 1.0)
        return (0.45, 0.30, 0.15, 1.0)

    set_vertex_colors(prop, dummy_color)
    mat = make_vc_material("DummyMat", roughness=0.8)
    prop.data.materials.append(mat)
    smooth_shade(prop)
    set_origin_bottom(prop)

    print(f"  Training Dummy: {len(prop.data.vertices)} verts, {len(prop.data.polygons)} polys")
    return prop


# =================================================================
#  SIGNPOST -- wooden post with directional sign
# =================================================================
def generate_signpost(offset_x=10):
    parts = []

    # Main post
    add_part(parts, "SP_Post", 'cylinder', (offset_x, 0, 0.55), (0.04, 0.04, 0.55))

    # Sign plank (angled)
    add_part(parts, "SP_Sign", 'cube', (offset_x + 0.18, 0, 0.90), (0.22, 0.015, 0.08))

    # Sign plank arrow tip
    add_part(parts, "SP_Arrow", 'cone', (offset_x + 0.42, 0, 0.90), (0.01, 0.08, 0.08),
             rot=(0, math.radians(90), 0))

    # Post cap (small ball on top)
    add_part(parts, "SP_Cap", 'uv_sphere', (offset_x, 0, 1.12), (0.05, 0.05, 0.04))

    # Base
    add_part(parts, "SP_Base", 'cube', (offset_x, 0, 0.02), (0.10, 0.10, 0.02))

    prop = join_parts(parts, "Signpost")

    def signpost_color(pos):
        lx = pos.x - offset_x
        # Sign plank (lighter wood)
        if abs(pos.z - 0.90) < 0.10 and lx > 0.0:
            return (0.58, 0.42, 0.22, 1.0)
        # Cap
        if pos.z > 1.06:
            return (0.50, 0.38, 0.18, 1.0)
        # Post
        return (0.40, 0.26, 0.12, 1.0)

    set_vertex_colors(prop, signpost_color)
    mat = make_vc_material("SignpostMat", roughness=0.82)
    prop.data.materials.append(mat)
    smooth_shade(prop)
    set_origin_bottom(prop)

    print(f"  Signpost: {len(prop.data.vertices)} verts, {len(prop.data.polygons)} polys")
    return prop


# =================================================================
#  MAIN
# =================================================================
def main():
    clear_scene()

    print("=" * 60)
    print("  Generating Props")
    print("=" * 60)

    generate_campfire(offset_x=0)
    generate_chest(offset_x=2)
    generate_crate(offset_x=4)
    generate_barrel(offset_x=6)
    generate_training_dummy(offset_x=8)
    generate_signpost(offset_x=10)

    export_glb(os.path.join(EXPORT_DIR, "props.glb"))

    print("=" * 60)
    print("  All props generated!")
    print("=" * 60)


if __name__ == "__main__":
    main()
