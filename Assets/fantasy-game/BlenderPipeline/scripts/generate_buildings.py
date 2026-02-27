"""
generate_buildings.py
=====================
Blender Python script -- run headless:
    blender --background --python BlenderPipeline/scripts/generate_buildings.py

Generates four village building models with vertex colors, all in one GLB:
  - Cottage          (~600 tris) -- stone base, wood walls, sloped roof
  - Blacksmith Forge (~700 tris) -- open workshop, anvil, chimney
  - Market Stall     (~400 tris) -- wood frame, fabric canopy, counter
  - Watchtower       (~500 tris) -- tall post structure, platform, railing

Exports to BlenderPipeline/exports/buildings.glb
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
#  COTTAGE -- stone foundation, wooden walls, sloped thatched roof
# =================================================================
def generate_cottage(offset_x=0):
    parts = []

    # Stone foundation (slightly wider than walls)
    add_part(parts, "CO_Foundation", 'cube', (offset_x, 0, 0.15), (1.60, 1.60, 0.15))

    # Wooden walls -- four sides as thin cubes
    # Front wall (with door gap -- two pieces)
    add_part(parts, "CO_WallFrontL", 'cube', (offset_x - 0.85, 0, 1.10), (0.55, 0.08, 0.80))
    add_part(parts, "CO_WallFrontR", 'cube', (offset_x + 0.85, 0, 1.10), (0.55, 0.08, 0.80))
    # Above door
    add_part(parts, "CO_WallFrontTop", 'cube', (offset_x, 0, 1.70), (1.40, 0.08, 0.20))

    # Back wall
    add_part(parts, "CO_WallBack", 'cube', (offset_x, 1.50, 1.10), (1.40, 0.08, 0.80))

    # Side walls
    add_part(parts, "CO_WallLeft", 'cube', (offset_x - 1.40, 0.75, 1.10), (0.08, 0.75, 0.80))
    add_part(parts, "CO_WallRight", 'cube', (offset_x + 1.40, 0.75, 1.10), (0.08, 0.75, 0.80))

    # Door frame
    add_part(parts, "CO_DoorFrameL", 'cube', (offset_x - 0.30, -0.02, 0.90), (0.04, 0.06, 0.60))
    add_part(parts, "CO_DoorFrameR", 'cube', (offset_x + 0.30, -0.02, 0.90), (0.04, 0.06, 0.60))

    # Door (slightly recessed)
    add_part(parts, "CO_Door", 'cube', (offset_x, 0.02, 0.80), (0.25, 0.03, 0.50))

    # Window on right wall (hole approximated by frame)
    add_part(parts, "CO_WindowFrame", 'cube', (offset_x + 1.42, 0.75, 1.20), (0.04, 0.20, 0.20))

    # Window shutters
    add_part(parts, "CO_ShutterL", 'cube', (offset_x + 1.44, 0.55, 1.20), (0.02, 0.08, 0.18))
    add_part(parts, "CO_ShutterR", 'cube', (offset_x + 1.44, 0.95, 1.20), (0.02, 0.08, 0.18))

    # Roof -- two sloped planes meeting at ridge
    # Left roof slope
    add_part(parts, "CO_RoofL", 'cube',
             (offset_x - 0.80, 0.75, 2.10), (0.95, 0.90, 0.08),
             rot=(0, math.radians(25), 0))
    # Right roof slope
    add_part(parts, "CO_RoofR", 'cube',
             (offset_x + 0.80, 0.75, 2.10), (0.95, 0.90, 0.08),
             rot=(0, math.radians(-25), 0))
    # Ridge beam
    add_part(parts, "CO_Ridge", 'cylinder',
             (offset_x, 0.75, 2.35), (0.04, 0.04, 0.95),
             rot=(math.radians(90), 0, 0))

    # Roof overhang trim (front and back)
    add_part(parts, "CO_TrimFront", 'cube', (offset_x, -0.10, 1.90), (1.55, 0.04, 0.04))
    add_part(parts, "CO_TrimBack", 'cube', (offset_x, 1.60, 1.90), (1.55, 0.04, 0.04))

    # Chimney
    add_part(parts, "CO_Chimney", 'cube', (offset_x + 1.10, 1.20, 2.20), (0.15, 0.15, 0.40))
    add_part(parts, "CO_ChimneyTop", 'cube', (offset_x + 1.10, 1.20, 2.62), (0.18, 0.18, 0.04))

    # Floor (interior)
    add_part(parts, "CO_Floor", 'cube', (offset_x, 0.75, 0.31), (1.30, 0.72, 0.01))

    building = join_parts(parts, "Cottage")

    def cottage_color(pos):
        lx = pos.x - offset_x
        # Chimney (grey stone)
        if lx > 0.90 and pos.y > 1.0 and pos.z > 2.0:
            return (0.45, 0.42, 0.40, 1.0)
        # Roof (dark thatch brown)
        if pos.z > 1.85:
            return (0.32, 0.22, 0.12, 1.0)
        # Foundation (grey stone)
        if pos.z < 0.32:
            return (0.50, 0.48, 0.45, 1.0)
        # Door (dark wood)
        if abs(lx) < 0.32 and abs(pos.y) < 0.10 and pos.z < 1.35:
            return (0.30, 0.18, 0.08, 1.0)
        # Window frame / shutters (dark wood)
        if lx > 1.35 and pos.z > 1.0 and pos.z < 1.45:
            return (0.28, 0.16, 0.06, 1.0)
        # Walls (warm wood)
        if pos.z > 0.30 and pos.z < 1.90:
            grain = math.sin(pos.z * 15) * 0.02
            return (0.55 + grain, 0.38 + grain, 0.20, 1.0)
        return (0.50, 0.35, 0.18, 1.0)

    set_vertex_colors(building, cottage_color)
    mat = make_vc_material("CottageMat", roughness=0.8)
    building.data.materials.append(mat)
    smooth_shade(building)
    set_origin_bottom(building)

    print(f"  Cottage: {len(building.data.vertices)} verts, {len(building.data.polygons)} polys")
    return building


# =================================================================
#  BLACKSMITH FORGE -- open-front workshop with anvil and chimney
# =================================================================
def generate_forge(offset_x=6):
    parts = []

    # Stone floor platform
    add_part(parts, "FO_Platform", 'cube', (offset_x, 0, 0.10), (1.50, 1.25, 0.10))

    # Back wall (stone)
    add_part(parts, "FO_WallBack", 'cube', (offset_x, 1.15, 1.10), (1.40, 0.12, 0.90))

    # Side walls (half-height, open front)
    add_part(parts, "FO_WallLeft", 'cube', (offset_x - 1.40, 0.55, 0.70), (0.08, 0.60, 0.50))
    add_part(parts, "FO_WallRight", 'cube', (offset_x + 1.40, 0.55, 0.70), (0.08, 0.60, 0.50))

    # Support posts (front corners)
    add_part(parts, "FO_PostFL", 'cylinder', (offset_x - 1.30, -0.05, 1.00), (0.06, 0.06, 0.80))
    add_part(parts, "FO_PostFR", 'cylinder', (offset_x + 1.30, -0.05, 1.00), (0.06, 0.06, 0.80))

    # Roof (sloped, lower at front)
    add_part(parts, "FO_Roof", 'cube',
             (offset_x, 0.55, 1.90), (1.55, 0.80, 0.06),
             rot=(math.radians(-8), 0, 0))

    # Forge/hearth (back center, stone block with opening)
    add_part(parts, "FO_Hearth", 'cube', (offset_x, 1.00, 0.50), (0.50, 0.25, 0.30))
    add_part(parts, "FO_HearthWall", 'cube', (offset_x, 1.10, 0.90), (0.55, 0.15, 0.10))
    # Glowing embers inside
    add_part(parts, "FO_Embers", 'cube', (offset_x, 0.92, 0.45), (0.35, 0.10, 0.20))

    # Chimney (rises from hearth)
    add_part(parts, "FO_Chimney", 'cube', (offset_x, 1.10, 1.80), (0.22, 0.22, 0.70))
    add_part(parts, "FO_ChimneyTop", 'cube', (offset_x, 1.10, 2.55), (0.26, 0.26, 0.04))

    # Anvil (front of forge)
    add_part(parts, "FO_AnvilBase", 'cube', (offset_x + 0.50, 0.30, 0.30), (0.12, 0.12, 0.10))
    add_part(parts, "FO_AnvilTop", 'cube', (offset_x + 0.50, 0.30, 0.45), (0.18, 0.10, 0.05))
    add_part(parts, "FO_AnvilHorn", 'cone',
             (offset_x + 0.50, 0.15, 0.45), (0.04, 0.06, 0.08),
             rot=(math.radians(90), 0, 0))

    # Water quench barrel
    add_part(parts, "FO_Barrel", 'cylinder', (offset_x - 0.80, 0.50, 0.35), (0.18, 0.18, 0.25))

    # Tool rack on back wall
    add_part(parts, "FO_Rack", 'cube', (offset_x - 0.60, 1.08, 1.30), (0.40, 0.03, 0.04))
    # Hanging tools
    for i in range(3):
        add_part(parts, f"FO_Tool_{i}", 'cylinder',
                 (offset_x - 0.80 + i * 0.20, 1.05, 1.10), (0.015, 0.015, 0.15))

    # Bellows (near forge)
    add_part(parts, "FO_Bellows", 'cube', (offset_x - 0.35, 0.85, 0.45), (0.10, 0.15, 0.12))

    building = join_parts(parts, "BlacksmithForge")

    def forge_color(pos):
        lx = pos.x - offset_x
        # Chimney
        if abs(lx) < 0.28 and pos.y > 1.0 and pos.z > 1.40:
            return (0.40, 0.38, 0.36, 1.0)
        # Embers (orange-red glow)
        if abs(lx) < 0.40 and pos.y > 0.80 and pos.y < 1.05 and pos.z > 0.30 and pos.z < 0.70:
            return (0.90, 0.40, 0.10, 1.0)
        # Anvil (dark iron)
        if lx > 0.30 and lx < 0.72 and pos.y > 0.15 and pos.y < 0.45 and pos.z > 0.25 and pos.z < 0.55:
            return (0.30, 0.28, 0.30, 1.0)
        # Water barrel (dark wood)
        if lx < -0.60 and pos.z < 0.65 and pos.z > 0.10:
            return (0.32, 0.20, 0.10, 1.0)
        # Tools
        if abs(pos.y - 1.05) < 0.08 and pos.z > 0.95 and pos.z < 1.40:
            return (0.40, 0.38, 0.35, 1.0)
        # Bellows (leather)
        if lx < -0.20 and lx > -0.50 and pos.y > 0.68 and pos.z < 0.60:
            return (0.42, 0.28, 0.14, 1.0)
        # Roof (dark wood)
        if pos.z > 1.82:
            return (0.30, 0.20, 0.10, 1.0)
        # Hearth stone
        if pos.y > 0.90 and pos.z > 0.20 and pos.z < 1.10:
            return (0.38, 0.35, 0.32, 1.0)
        # Stone walls and floor
        if pos.z < 0.22:
            return (0.48, 0.45, 0.42, 1.0)
        # Side walls (stone)
        if abs(lx) > 1.30:
            return (0.46, 0.43, 0.40, 1.0)
        # Back wall
        if pos.y > 1.05:
            return (0.44, 0.41, 0.38, 1.0)
        # Wooden posts
        if abs(lx) > 1.20 and pos.z > 0.50:
            return (0.42, 0.28, 0.14, 1.0)
        return (0.45, 0.30, 0.15, 1.0)

    set_vertex_colors(building, forge_color)
    mat = make_vc_material("ForgeMat", roughness=0.75, metallic=0.1)
    building.data.materials.append(mat)
    smooth_shade(building)
    set_origin_bottom(building)

    print(f"  Forge: {len(building.data.vertices)} verts, {len(building.data.polygons)} polys")
    return building


# =================================================================
#  MARKET STALL -- wooden frame with fabric canopy and counter
# =================================================================
def generate_market_stall(offset_x=12):
    parts = []

    # Four corner posts
    for sx in [-1, 1]:
        for sy in [-1, 1]:
            add_part(parts, f"MS_Post_{sx}_{sy}", 'cylinder',
                     (offset_x + sx * 1.10, sy * 0.80, 0.90), (0.04, 0.04, 0.90))

    # Counter (front, waist-height)
    add_part(parts, "MS_Counter", 'cube', (offset_x, -0.80, 0.80), (1.15, 0.12, 0.04))
    # Counter supports
    add_part(parts, "MS_CounterLeg_L", 'cube', (offset_x - 0.90, -0.80, 0.40), (0.04, 0.04, 0.38))
    add_part(parts, "MS_CounterLeg_R", 'cube', (offset_x + 0.90, -0.80, 0.40), (0.04, 0.04, 0.38))

    # Back shelf
    add_part(parts, "MS_Shelf", 'cube', (offset_x, 0.70, 1.00), (1.00, 0.15, 0.03))

    # Canopy frame (top rails)
    add_part(parts, "MS_RailFront", 'cube', (offset_x, -0.80, 1.80), (1.15, 0.03, 0.03))
    add_part(parts, "MS_RailBack", 'cube', (offset_x, 0.80, 1.90), (1.15, 0.03, 0.03))
    add_part(parts, "MS_RailLeft", 'cube', (offset_x - 1.10, 0, 1.85), (0.03, 0.82, 0.03))
    add_part(parts, "MS_RailRight", 'cube', (offset_x + 1.10, 0, 1.85), (0.03, 0.82, 0.03))

    # Fabric canopy (slightly draped -- two angled planes)
    add_part(parts, "MS_CanopyL", 'cube',
             (offset_x - 0.55, 0, 1.92), (0.62, 0.85, 0.02),
             rot=(0, math.radians(5), 0))
    add_part(parts, "MS_CanopyR", 'cube',
             (offset_x + 0.55, 0, 1.92), (0.62, 0.85, 0.02),
             rot=(0, math.radians(-5), 0))

    # Canopy valance (front drape)
    add_part(parts, "MS_Valance", 'cube', (offset_x, -0.82, 1.72), (1.10, 0.02, 0.08))

    # Display goods on counter (small objects)
    for i in range(4):
        x = offset_x - 0.60 + i * 0.40
        add_part(parts, f"MS_Goods_{i}", 'cube',
                 (x, -0.80, 0.90), (0.10, 0.08, 0.05))

    # Hanging goods from canopy (sausages/herbs)
    for i in range(3):
        x = offset_x - 0.50 + i * 0.50
        add_part(parts, f"MS_Hanging_{i}", 'cylinder',
                 (x, -0.40, 1.55), (0.02, 0.02, 0.12))

    # Sign board on front
    add_part(parts, "MS_Sign", 'cube', (offset_x, -0.90, 1.50), (0.30, 0.02, 0.12))

    building = join_parts(parts, "MarketStall")

    def stall_color(pos):
        lx = pos.x - offset_x
        # Canopy fabric (warm red/orange stripes)
        if pos.z > 1.88:
            stripe = int((lx + 2) * 3) % 2
            if stripe:
                return (0.72, 0.25, 0.12, 1.0)  # Red stripe
            return (0.80, 0.65, 0.30, 1.0)  # Cream stripe
        # Valance
        if pos.z > 1.62 and pos.z < 1.82 and abs(pos.y + 0.82) < 0.05:
            return (0.72, 0.25, 0.12, 1.0)
        # Sign
        if abs(pos.y + 0.90) < 0.05 and pos.z > 1.36 and pos.z < 1.64:
            return (0.50, 0.35, 0.18, 1.0)
        # Display goods (various colors)
        if abs(pos.y + 0.80) < 0.12 and pos.z > 0.84 and pos.z < 0.98:
            idx = int((lx + 1.5) * 2) % 3
            colors = [
                (0.75, 0.55, 0.15, 1.0),  # Golden
                (0.60, 0.25, 0.15, 1.0),  # Brown
                (0.40, 0.55, 0.25, 1.0),  # Green
            ]
            return colors[idx]
        # Hanging goods
        if pos.z > 1.40 and pos.z < 1.70 and abs(pos.y + 0.40) < 0.08:
            return (0.55, 0.28, 0.15, 1.0)
        # Counter (lighter wood)
        if abs(pos.y + 0.80) < 0.15 and pos.z > 0.74 and pos.z < 0.86:
            return (0.58, 0.42, 0.22, 1.0)
        # Shelf
        if abs(pos.y - 0.70) < 0.18 and pos.z > 0.96 and pos.z < 1.06:
            return (0.52, 0.36, 0.18, 1.0)
        # Posts (dark wood)
        return (0.40, 0.26, 0.12, 1.0)

    set_vertex_colors(building, stall_color)
    mat = make_vc_material("StallMat", roughness=0.7)
    building.data.materials.append(mat)
    smooth_shade(building)
    set_origin_bottom(building)

    print(f"  Market Stall: {len(building.data.vertices)} verts, {len(building.data.polygons)} polys")
    return building


# =================================================================
#  WATCHTOWER -- tall structure with platform and railing
# =================================================================
def generate_watchtower(offset_x=18):
    parts = []

    # Four main support posts (tall)
    for sx in [-1, 1]:
        for sy in [-1, 1]:
            add_part(parts, f"WT_Post_{sx}_{sy}", 'cylinder',
                     (offset_x + sx * 0.60, sy * 0.60, 1.80), (0.06, 0.06, 1.80))

    # Cross braces (X pattern on two sides)
    # Front face
    add_part(parts, "WT_BraceF1", 'cube',
             (offset_x, -0.60, 1.00), (0.55, 0.02, 0.03),
             rot=(0, 0, math.radians(35)))
    add_part(parts, "WT_BraceF2", 'cube',
             (offset_x, -0.60, 1.00), (0.55, 0.02, 0.03),
             rot=(0, 0, math.radians(-35)))

    # Back face
    add_part(parts, "WT_BraceB1", 'cube',
             (offset_x, 0.60, 1.00), (0.55, 0.02, 0.03),
             rot=(0, 0, math.radians(35)))
    add_part(parts, "WT_BraceB2", 'cube',
             (offset_x, 0.60, 1.00), (0.55, 0.02, 0.03),
             rot=(0, 0, math.radians(-35)))

    # Side braces
    add_part(parts, "WT_BraceL", 'cube',
             (offset_x - 0.60, 0, 1.00), (0.02, 0.55, 0.03),
             rot=(math.radians(35), 0, 0))
    add_part(parts, "WT_BraceR", 'cube',
             (offset_x + 0.60, 0, 1.00), (0.02, 0.55, 0.03),
             rot=(math.radians(-35), 0, 0))

    # Platform floor
    add_part(parts, "WT_Platform", 'cube', (offset_x, 0, 2.80), (0.80, 0.80, 0.04))

    # Platform planks (visual detail)
    for i in range(4):
        z = 2.82
        y = -0.60 + i * 0.40
        add_part(parts, f"WT_Plank_{i}", 'cube',
                 (offset_x, y, z), (0.78, 0.02, 0.005))

    # Railing posts (on platform)
    for sx in [-1, 1]:
        for sy in [-1, 1]:
            add_part(parts, f"WT_RailPost_{sx}_{sy}", 'cylinder',
                     (offset_x + sx * 0.75, sy * 0.75, 3.30), (0.03, 0.03, 0.45))

    # Railing horizontal bars
    for z in [3.10, 3.55]:
        add_part(parts, f"WT_RailFront_{z}", 'cube',
                 (offset_x, -0.75, z), (0.72, 0.025, 0.025))
        add_part(parts, f"WT_RailBack_{z}", 'cube',
                 (offset_x, 0.75, z), (0.72, 0.025, 0.025))
        add_part(parts, f"WT_RailLeft_{z}", 'cube',
                 (offset_x - 0.75, 0, z), (0.025, 0.72, 0.025))
        add_part(parts, f"WT_RailRight_{z}", 'cube',
                 (offset_x + 0.75, 0, z), (0.025, 0.72, 0.025))

    # Ladder (leaning against front)
    add_part(parts, "WT_LadderL", 'cube',
             (offset_x - 0.12, -0.55, 1.40), (0.02, 0.04, 1.35))
    add_part(parts, "WT_LadderR", 'cube',
             (offset_x + 0.12, -0.55, 1.40), (0.02, 0.04, 1.35))
    # Ladder rungs
    for i in range(6):
        z = 0.30 + i * 0.42
        add_part(parts, f"WT_Rung_{i}", 'cube',
                 (offset_x, -0.55, z), (0.10, 0.025, 0.02))

    # Pointed roof (small, conical)
    add_part(parts, "WT_Roof", 'cone',
             (offset_x, 0, 3.90), (0.85, 0.85, 0.50))

    # Roof support beam
    add_part(parts, "WT_RoofPost", 'cylinder',
             (offset_x, 0, 3.50), (0.04, 0.04, 0.40))

    # Base platform
    add_part(parts, "WT_Base", 'cube', (offset_x, 0, 0.04), (0.85, 0.85, 0.04))

    building = join_parts(parts, "Watchtower")

    def tower_color(pos):
        lx = pos.x - offset_x
        # Roof (dark thatch)
        if pos.z > 3.60:
            return (0.30, 0.20, 0.10, 1.0)
        # Railing (lighter wood)
        if pos.z > 2.90 and (abs(lx) > 0.65 or abs(pos.y) > 0.65):
            return (0.52, 0.38, 0.20, 1.0)
        # Platform
        if pos.z > 2.72 and pos.z < 2.90:
            return (0.48, 0.34, 0.18, 1.0)
        # Ladder rungs
        if abs(lx) < 0.15 and abs(pos.y + 0.55) < 0.08 and pos.z < 2.80:
            return (0.50, 0.36, 0.18, 1.0)
        # Ladder sides
        if abs(abs(lx) - 0.12) < 0.04 and abs(pos.y + 0.55) < 0.08:
            return (0.45, 0.30, 0.14, 1.0)
        # Cross braces (medium wood)
        dist_side = min(abs(lx) - 0.60 if abs(lx) > 0.50 else 10,
                        abs(pos.y) - 0.60 if abs(pos.y) > 0.50 else 10)
        if dist_side < 0.05 and pos.z > 0.50 and pos.z < 1.50:
            return (0.42, 0.28, 0.13, 1.0)
        # Main posts (dark wood)
        if (abs(abs(lx) - 0.60) < 0.10 and abs(abs(pos.y) - 0.60) < 0.10):
            return (0.38, 0.24, 0.12, 1.0)
        # Base
        if pos.z < 0.10:
            return (0.44, 0.40, 0.36, 1.0)
        return (0.42, 0.28, 0.14, 1.0)

    set_vertex_colors(building, tower_color)
    mat = make_vc_material("TowerMat", roughness=0.78)
    building.data.materials.append(mat)
    smooth_shade(building)
    set_origin_bottom(building)

    print(f"  Watchtower: {len(building.data.vertices)} verts, {len(building.data.polygons)} polys")
    return building


# =================================================================
#  MAIN
# =================================================================
def main():
    clear_scene()

    print("=" * 60)
    print("  Generating Buildings")
    print("=" * 60)

    generate_cottage(offset_x=0)
    generate_forge(offset_x=6)
    generate_market_stall(offset_x=12)
    generate_watchtower(offset_x=18)

    export_glb(os.path.join(EXPORT_DIR, "buildings.glb"))

    print("=" * 60)
    print("  All buildings generated!")
    print("=" * 60)


if __name__ == "__main__":
    main()
