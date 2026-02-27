"""
generate_trees.py
=================
Blender Python script — run headless:
    blender --background --python BlenderPipeline/scripts/generate_trees.py

Generates 3 low-poly tree variants with vertex colors and exports
to BlenderPipeline/exports/trees.glb

Tree variants:
  - Oak (broad, round canopy)
  - Pine (conical, layered)
  - Fantasy (twisted trunk, clustered leaves)
"""

import bpy
import bmesh
import math
import os
import random
from mathutils import Vector

# ---------------------------------------------------------------------------
# Config
# ---------------------------------------------------------------------------
EXPORT_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "exports")
EXPORT_PATH = os.path.join(EXPORT_DIR, "trees.glb")

SEED = 42
random.seed(SEED)

# Vertex colors
TRUNK_COLOR = (0.45, 0.28, 0.15, 1.0)     # Brown
LEAF_COLOR_BASE = (0.22, 0.45, 0.15, 1.0)  # Forest green
LEAF_COLOR_WARM = (0.35, 0.50, 0.18, 1.0)  # Warm green variation

# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------
def clear_scene():
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    for block in bpy.data.meshes:
        if block.users == 0:
            bpy.data.meshes.remove(block)

def set_vertex_colors(obj, color):
    """Set all vertices of an object to a single color via vertex color layer."""
    mesh = obj.data
    if not mesh.color_attributes:
        mesh.color_attributes.new(name="Color", type='BYTE_COLOR', domain='CORNER')
    color_layer = mesh.color_attributes[0]
    for i in range(len(color_layer.data)):
        color_layer.data[i].color = color

def displace_vertices(obj, amount, seed_offset=0):
    """Randomly displace vertices for organic look."""
    rng = random.Random(seed_offset)
    mesh = obj.data
    for v in mesh.vertices:
        v.co.x += rng.uniform(-amount, amount)
        v.co.y += rng.uniform(-amount, amount)
        v.co.z += rng.uniform(-amount, amount)

def smooth_shade(obj):
    for poly in obj.data.polygons:
        poly.use_smooth = True

def create_material(name, base_color):
    """Create a simple vertex-color material."""
    mat = bpy.data.materials.new(name=name)
    mat.use_nodes = True
    tree = mat.node_tree
    nodes = tree.nodes
    links = tree.links

    # Clear defaults
    for n in nodes:
        nodes.remove(n)

    # Vertex color node -> BSDF -> Output
    vc_node = nodes.new('ShaderNodeVertexColor')
    vc_node.layer_name = "Color"
    vc_node.location = (-300, 0)

    bsdf = nodes.new('ShaderNodeBsdfPrincipled')
    bsdf.location = (0, 0)
    bsdf.inputs["Roughness"].default_value = 0.8
    bsdf.inputs["Metallic"].default_value = 0.0

    output = nodes.new('ShaderNodeOutputMaterial')
    output.location = (300, 0)

    links.new(vc_node.outputs["Color"], bsdf.inputs["Base Color"])
    links.new(bsdf.outputs["BSDF"], output.inputs["Surface"])

    return mat


# ---------------------------------------------------------------------------
# Tree Generators
# ---------------------------------------------------------------------------
def create_oak_tree():
    """Broad oak-like tree with round canopy."""
    parts = []

    # Trunk - tapered cylinder
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=8, radius=0.15, depth=2.5,
        location=(0, 0, 1.25)
    )
    trunk = bpy.context.active_object
    trunk.name = "Oak_Trunk"
    # Taper top
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='DESELECT')
    bm = bmesh.from_edit_mesh(trunk.data)
    for v in bm.verts:
        if v.co.z > 0:  # Top half
            factor = 0.6
            v.co.x *= factor
            v.co.y *= factor
    bmesh.update_edit_mesh(trunk.data)
    bpy.ops.object.mode_set(mode='OBJECT')

    set_vertex_colors(trunk, TRUNK_COLOR)
    smooth_shade(trunk)
    parts.append(trunk)

    # Canopy - multiple displaced icospheres
    canopy_positions = [
        (0, 0, 3.2),
        (0.5, 0.3, 2.8),
        (-0.4, -0.3, 3.0),
        (0.2, -0.4, 3.5),
        (-0.3, 0.4, 3.3),
    ]

    for i, pos in enumerate(canopy_positions):
        bpy.ops.mesh.primitive_ico_sphere_add(
            subdivisions=2, radius=0.8 + random.uniform(-0.2, 0.2),
            location=pos
        )
        leaf = bpy.context.active_object
        leaf.name = f"Oak_Canopy_{i}"
        displace_vertices(leaf, 0.15, seed_offset=i * 7)
        # Mix leaf colors
        color = LEAF_COLOR_BASE if i % 2 == 0 else LEAF_COLOR_WARM
        set_vertex_colors(leaf, color)
        smooth_shade(leaf)
        parts.append(leaf)

    # Join all parts
    bpy.ops.object.select_all(action='DESELECT')
    for obj in parts:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()

    tree = bpy.context.active_object
    tree.name = "Tree_Oak"

    # Set origin to base
    min_z = min(v.co.z for v in tree.data.vertices)
    for v in tree.data.vertices:
        v.co.z -= min_z
    bpy.ops.object.transform_apply(location=True, rotation=False, scale=False)
    tree.location = (0, 0, 0)

    return tree


def create_pine_tree():
    """Conical pine tree with layered cone canopy."""
    parts = []

    # Trunk
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=6, radius=0.1, depth=3.0,
        location=(5, 0, 1.5)
    )
    trunk = bpy.context.active_object
    trunk.name = "Pine_Trunk"
    set_vertex_colors(trunk, TRUNK_COLOR)
    smooth_shade(trunk)
    parts.append(trunk)

    # Canopy layers - stacked cones
    cone_layers = [
        (5, 0, 2.0, 1.0, 1.0),    # (x, y, z, radius, height)
        (5, 0, 2.8, 0.8, 0.9),
        (5, 0, 3.5, 0.6, 0.8),
        (5, 0, 4.1, 0.4, 0.7),
    ]

    for i, (x, y, z, rad, h) in enumerate(cone_layers):
        bpy.ops.mesh.primitive_cone_add(
            vertices=8, radius1=rad, radius2=0.05, depth=h,
            location=(x, y, z)
        )
        cone = bpy.context.active_object
        cone.name = f"Pine_Canopy_{i}"
        displace_vertices(cone, 0.05, seed_offset=100 + i * 7)
        color = LEAF_COLOR_BASE if i % 2 == 0 else (0.18, 0.38, 0.12, 1.0)
        set_vertex_colors(cone, color)
        smooth_shade(cone)
        parts.append(cone)

    # Join
    bpy.ops.object.select_all(action='DESELECT')
    for obj in parts:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()

    tree = bpy.context.active_object
    tree.name = "Tree_Pine"

    # Set origin to base
    min_z = min(v.co.z for v in tree.data.vertices)
    for v in tree.data.vertices:
        v.co.z -= min_z
    tree.location = (5, 0, 0)  # Keep offset for separate objects in GLB

    return tree


def create_fantasy_tree():
    """Twisted fantasy tree with gnarled trunk and clustered leaves."""
    parts = []

    # Trunk - cylinder with twist deformation
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=8, radius=0.2, depth=3.0,
        location=(10, 0, 1.5)
    )
    trunk = bpy.context.active_object
    trunk.name = "Fantasy_Trunk"

    # Add loop cuts for twisting
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.mesh.subdivide(number_cuts=6)
    bpy.ops.object.mode_set(mode='OBJECT')

    # Twist and displace for gnarled look
    rng = random.Random(200)
    for v in trunk.data.vertices:
        angle = v.co.z * 0.5  # Twist around Z
        x, y = v.co.x, v.co.y
        v.co.x = x * math.cos(angle) - y * math.sin(angle)
        v.co.y = x * math.sin(angle) + y * math.cos(angle)
        # Organic displacement
        v.co.x += rng.uniform(-0.05, 0.05)
        v.co.y += rng.uniform(-0.05, 0.05)

    set_vertex_colors(trunk, (0.40, 0.25, 0.12, 1.0))  # Darker brown
    smooth_shade(trunk)
    parts.append(trunk)

    # Leaf clusters - small icospheres at branch tips
    cluster_positions = [
        (10.3, 0.2, 3.0),
        (9.7, -0.3, 3.2),
        (10.1, 0.4, 3.5),
        (10.4, -0.1, 2.6),
        (9.8, 0.3, 3.4),
        (10.2, -0.4, 3.1),
    ]

    for i, pos in enumerate(cluster_positions):
        bpy.ops.mesh.primitive_ico_sphere_add(
            subdivisions=1, radius=0.4 + rng.uniform(-0.1, 0.15),
            location=pos
        )
        leaf = bpy.context.active_object
        leaf.name = f"Fantasy_Leaves_{i}"
        displace_vertices(leaf, 0.1, seed_offset=300 + i * 11)
        # Fantasy warm-tinted greens
        warm = rng.uniform(0, 0.15)
        color = (0.25 + warm, 0.42 + warm * 0.5, 0.12, 1.0)
        set_vertex_colors(leaf, color)
        smooth_shade(leaf)
        parts.append(leaf)

    # Join
    bpy.ops.object.select_all(action='DESELECT')
    for obj in parts:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()

    tree = bpy.context.active_object
    tree.name = "Tree_Fantasy"

    # Set origin to base
    min_z = min(v.co.z for v in tree.data.vertices)
    for v in tree.data.vertices:
        v.co.z -= min_z
    tree.location = (10, 0, 0)

    return tree


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------
def main():
    print("=" * 60)
    print("  Generating tree variants")
    print("=" * 60)

    clear_scene()

    # Create materials
    mat = create_material("Tree_Mat", LEAF_COLOR_BASE)

    # Generate trees
    oak = create_oak_tree()
    pine = create_pine_tree()
    fantasy = create_fantasy_tree()

    # Apply material to all trees
    for tree in [oak, pine, fantasy]:
        if tree.data.materials:
            tree.data.materials[0] = mat
        else:
            tree.data.materials.append(mat)

    # Print stats
    for tree in [oak, pine, fantasy]:
        vert_count = len(tree.data.vertices)
        poly_count = len(tree.data.polygons)
        print(f"  {tree.name}: {vert_count} verts, {poly_count} polys")

    # Select all for export
    bpy.ops.object.select_all(action='SELECT')

    # Export
    os.makedirs(EXPORT_DIR, exist_ok=True)
    bpy.ops.export_scene.gltf(
        filepath=EXPORT_PATH,
        export_format='GLB',
        use_selection=True,
        export_apply=True,
        export_yup=True,
        export_materials='EXPORT',
    )

    file_size = os.path.getsize(EXPORT_PATH)
    print(f"  Exported: {EXPORT_PATH}")
    print(f"  File size: {file_size / 1024:.1f} KB")
    print("=" * 60)
    print("  Done!")
    print("=" * 60)


if __name__ == "__main__":
    main()
