"""
generate_dungeon.py
===================
Blender Python script -- run headless:
    blender --background --python BlenderPipeline/scripts/generate_dungeon.py

Generates three dungeon-themed models with vertex colors, all in one GLB:
  - Cave Entrance Arch  (~300 tris) -- two stone pillars + rough arch
  - Torch Sconce        (~80 tris)  -- wall bracket + flame tip
  - Exit Portal         (~250 tris) -- circular stone ring with rune accents

Exports to BlenderPipeline/exports/dungeon.glb
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
    """Move vertices so the mesh is centered in X/Y with bottom at Z=0."""
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
#  CAVE ENTRANCE ARCH -- two rough stone pillars + arch on top
# =================================================================
def generate_cave_entrance(offset_x=0):
    parts = []

    # Left pillar (rough stone, slightly tapered)
    add_part(parts, "CE_PillarL", 'cube', (offset_x - 1.30, 0, 1.50), (0.35, 0.40, 1.50))
    # Pillar detail stones
    add_part(parts, "CE_StoneL1", 'cube', (offset_x - 1.55, 0.15, 0.40), (0.15, 0.18, 0.25))
    add_part(parts, "CE_StoneL2", 'cube', (offset_x - 1.10, -0.20, 1.00), (0.12, 0.15, 0.20))
    add_part(parts, "CE_StoneL3", 'cube', (offset_x - 1.50, 0.05, 2.20), (0.18, 0.20, 0.18))

    # Right pillar
    add_part(parts, "CE_PillarR", 'cube', (offset_x + 1.30, 0, 1.50), (0.35, 0.40, 1.50))
    # Pillar detail stones
    add_part(parts, "CE_StoneR1", 'cube', (offset_x + 1.55, -0.15, 0.50), (0.15, 0.18, 0.22))
    add_part(parts, "CE_StoneR2", 'cube', (offset_x + 1.10, 0.20, 1.20), (0.12, 0.15, 0.18))
    add_part(parts, "CE_StoneR3", 'cube', (offset_x + 1.48, -0.08, 2.10), (0.16, 0.20, 0.20))

    # Arch keystone (top center)
    add_part(parts, "CE_Keystone", 'cube', (offset_x, 0, 3.20), (0.30, 0.35, 0.25))

    # Arch segments (left and right curves approximated with angled blocks)
    add_part(parts, "CE_ArchL1", 'cube',
             (offset_x - 0.90, 0, 2.90), (0.40, 0.32, 0.20),
             rot=(0, 0, math.radians(15)))
    add_part(parts, "CE_ArchL2", 'cube',
             (offset_x - 0.45, 0, 3.10), (0.35, 0.30, 0.18),
             rot=(0, 0, math.radians(8)))

    add_part(parts, "CE_ArchR1", 'cube',
             (offset_x + 0.90, 0, 2.90), (0.40, 0.32, 0.20),
             rot=(0, 0, math.radians(-15)))
    add_part(parts, "CE_ArchR2", 'cube',
             (offset_x + 0.45, 0, 3.10), (0.35, 0.30, 0.18),
             rot=(0, 0, math.radians(-8)))

    # Base stones (wider at ground level)
    add_part(parts, "CE_BaseL", 'cube', (offset_x - 1.40, 0, 0.15), (0.50, 0.50, 0.15))
    add_part(parts, "CE_BaseR", 'cube', (offset_x + 1.40, 0, 0.15), (0.50, 0.50, 0.15))

    # Moss/vine detail cubes at base
    add_part(parts, "CE_MossL", 'cube', (offset_x - 1.60, 0.30, 0.25), (0.08, 0.10, 0.12))
    add_part(parts, "CE_MossR", 'cube', (offset_x + 1.60, -0.25, 0.20), (0.10, 0.08, 0.10))

    # Lintel beam (dark wood, sits under arch)
    add_part(parts, "CE_Lintel", 'cylinder',
             (offset_x, 0, 2.70), (0.06, 0.06, 0.90),
             rot=(0, math.radians(90), 0))

    entrance = join_parts(parts, "CaveEntrance")

    def entrance_color(pos):
        lx = pos.x - offset_x
        # Moss patches (green tint at base)
        if pos.z < 0.40 and (abs(lx) > 1.40 or abs(pos.y) > 0.25):
            return (0.22, 0.30, 0.18, 1.0)
        # Keystone (slightly lighter)
        if pos.z > 3.0 and abs(lx) < 0.40:
            return (0.42, 0.40, 0.38, 1.0)
        # Arch segments
        if pos.z > 2.60:
            return (0.38, 0.35, 0.32, 1.0)
        # Lintel (dark wood)
        if pos.z > 2.55 and pos.z < 2.85 and abs(lx) < 1.0:
            return (0.28, 0.18, 0.10, 1.0)
        # Detail stones (varied grey)
        if (abs(abs(lx) - 1.30) > 0.20 and pos.z > 0.20 and pos.z < 2.50):
            return (0.40, 0.37, 0.33, 1.0)
        # Main pillars
        if abs(lx) > 0.80:
            grain = math.sin(pos.z * 8) * 0.02
            return (0.35 + grain, 0.32 + grain, 0.30, 1.0)
        # Base stones
        if pos.z < 0.20:
            return (0.38, 0.36, 0.34, 1.0)
        return (0.36, 0.33, 0.30, 1.0)

    set_vertex_colors(entrance, entrance_color)
    mat = make_vc_material("CaveEntranceMat", roughness=0.9)
    entrance.data.materials.append(mat)
    smooth_shade(entrance)
    set_origin_bottom(entrance)

    print(f"  Cave Entrance: {len(entrance.data.vertices)} verts, {len(entrance.data.polygons)} polys")
    return entrance


# =================================================================
#  TORCH SCONCE -- wall bracket + flame tip
# =================================================================
def generate_torch_sconce(offset_x=6):
    parts = []

    # Wall mount plate
    add_part(parts, "TS_Plate", 'cube', (offset_x, 0, 0.25), (0.08, 0.08, 0.06))

    # Bracket arm (angled upward)
    add_part(parts, "TS_Arm", 'cylinder',
             (offset_x, -0.08, 0.28), (0.02, 0.02, 0.12),
             rot=(math.radians(45), 0, 0))

    # Cup/holder
    add_part(parts, "TS_Cup", 'cylinder', (offset_x, -0.15, 0.38), (0.04, 0.04, 0.03))

    # Flame (small cone, tip up)
    add_part(parts, "TS_Flame", 'cone',
             (offset_x, -0.15, 0.48), (0.03, 0.03, 0.06))

    # Flame glow (small sphere)
    add_part(parts, "TS_Glow", 'uv_sphere', (offset_x, -0.15, 0.46), (0.025, 0.025, 0.03))

    sconce = join_parts(parts, "TorchSconce")

    def sconce_color(pos):
        lx = pos.x - offset_x
        # Flame (bright orange-yellow)
        if pos.z > 0.42:
            t = (pos.z - 0.42) / 0.12
            r = 1.0
            g = 0.7 - t * 0.3
            b = 0.1
            return (r, g, b, 1.0)
        # Cup (dark iron)
        if pos.z > 0.34 and pos.z < 0.42:
            return (0.25, 0.22, 0.20, 1.0)
        # Bracket arm (iron)
        if abs(pos.y + 0.08) < 0.06 and pos.z > 0.20:
            return (0.30, 0.28, 0.26, 1.0)
        # Wall plate (iron)
        return (0.28, 0.25, 0.23, 1.0)

    set_vertex_colors(sconce, sconce_color)
    mat = make_vc_material("TorchMat", roughness=0.7, metallic=0.3)
    sconce.data.materials.append(mat)
    smooth_shade(sconce)
    set_origin_bottom(sconce)

    print(f"  Torch Sconce: {len(sconce.data.vertices)} verts, {len(sconce.data.polygons)} polys")
    return sconce


# =================================================================
#  EXIT PORTAL -- circular stone ring with rune accents
# =================================================================
def generate_exit_portal(offset_x=12):
    parts = []

    # Main ring (torus)
    add_part(parts, "EP_Ring", 'torus', (offset_x, 0, 1.40), (1.20, 1.20, 1.20))

    # Base platform (stone slab)
    add_part(parts, "EP_Base", 'cube', (offset_x, 0, 0.10), (1.50, 0.50, 0.10))

    # Support pillars (left and right)
    add_part(parts, "EP_PillarL", 'cube', (offset_x - 1.10, 0, 0.90), (0.15, 0.20, 0.80))
    add_part(parts, "EP_PillarR", 'cube', (offset_x + 1.10, 0, 0.90), (0.15, 0.20, 0.80))

    # Rune stones (small cubes around the ring)
    for i in range(6):
        angle = (i / 6.0) * math.pi * 2
        rx = offset_x + math.cos(angle) * 1.05
        rz = 1.40 + math.sin(angle) * 1.05
        if rz > 0.20:  # Only above ground
            add_part(parts, f"EP_Rune_{i}", 'cube',
                     (rx, 0, rz), (0.08, 0.06, 0.08))

    # Inner glow circle (flat disc)
    add_part(parts, "EP_GlowDisc", 'cylinder',
             (offset_x, 0, 1.40), (0.70, 0.70, 0.02),
             rot=(math.radians(90), 0, 0))

    # Top capstone
    add_part(parts, "EP_Cap", 'cube', (offset_x, 0, 2.60), (0.25, 0.18, 0.12))

    portal = join_parts(parts, "ExitPortal")

    def portal_color(pos):
        lx = pos.x - offset_x
        # Inner glow disc (bright blue-purple)
        dist_center = math.sqrt(lx**2 + (pos.z - 1.40)**2)
        if abs(pos.y) < 0.05 and dist_center < 0.80 and dist_center > 0.15:
            t = dist_center / 0.80
            return (0.3 + t * 0.2, 0.2, 0.8 - t * 0.2, 1.0)
        # Rune stones (glowing blue)
        for i in range(6):
            angle = (i / 6.0) * math.pi * 2
            rx = math.cos(angle) * 1.05
            rz = 1.40 + math.sin(angle) * 1.05
            if abs(lx - rx) < 0.12 and abs(pos.z - rz) < 0.12:
                return (0.3, 0.4, 0.9, 1.0)
        # Main torus ring (dark stone with purple tint)
        if dist_center > 0.85 and dist_center < 1.55 and pos.z > 0.20:
            return (0.32, 0.28, 0.38, 1.0)
        # Top capstone
        if pos.z > 2.45:
            return (0.35, 0.30, 0.40, 1.0)
        # Pillars (dark stone)
        if abs(abs(lx) - 1.10) < 0.20 and pos.z < 1.80:
            return (0.30, 0.28, 0.26, 1.0)
        # Base platform (grey stone)
        if pos.z < 0.22:
            return (0.38, 0.36, 0.34, 1.0)
        return (0.33, 0.30, 0.32, 1.0)

    set_vertex_colors(portal, portal_color)
    mat = make_vc_material("PortalMat", roughness=0.65, metallic=0.1)
    portal.data.materials.append(mat)
    smooth_shade(portal)
    set_origin_bottom(portal)

    print(f"  Exit Portal: {len(portal.data.vertices)} verts, {len(portal.data.polygons)} polys")
    return portal


# =================================================================
#  MAIN
# =================================================================
def main():
    clear_scene()

    print("=" * 60)
    print("  Generating Dungeon Models")
    print("=" * 60)

    generate_cave_entrance(offset_x=0)
    generate_torch_sconce(offset_x=6)
    generate_exit_portal(offset_x=12)

    export_glb(os.path.join(EXPORT_DIR, "dungeon.glb"))

    print("=" * 60)
    print("  All dungeon models generated!")
    print("=" * 60)


if __name__ == "__main__":
    main()
