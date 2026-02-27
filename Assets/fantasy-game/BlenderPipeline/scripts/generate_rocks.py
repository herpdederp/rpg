"""
generate_rocks.py
=================
Blender Python script — run headless:
    blender --background --python BlenderPipeline/scripts/generate_rocks.py

Generates 3 low-poly rock variants with vertex colors and exports
to BlenderPipeline/exports/rocks.glb

Rock variants:
  - Boulder (rounded, heavy)
  - Standing Stone (tall, monolithic)
  - Rock Cluster (several small rocks grouped)
"""

import bpy
import math
import os
import random
from mathutils import Vector

# ---------------------------------------------------------------------------
# Config
# ---------------------------------------------------------------------------
EXPORT_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "exports")
EXPORT_PATH = os.path.join(EXPORT_DIR, "rocks.glb")

SEED = 99
random.seed(SEED)

ROCK_COLOR = (0.48, 0.44, 0.38, 1.0)        # Grey-brown
ROCK_DARK = (0.35, 0.32, 0.28, 1.0)          # Dark rock
MOSS_COLOR = (0.30, 0.40, 0.22, 1.0)         # Mossy green

# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------
def clear_scene():
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    for block in bpy.data.meshes:
        if block.users == 0:
            bpy.data.meshes.remove(block)

def set_vertex_colors_with_moss(obj, base_color, moss_color, moss_threshold=0.5):
    """Color vertices based on their face normal direction - top faces get moss."""
    mesh = obj.data
    mesh.update()

    if not mesh.color_attributes:
        mesh.color_attributes.new(name="Color", type='BYTE_COLOR', domain='CORNER')
    color_layer = mesh.color_attributes[0]

    for poly in mesh.polygons:
        # Use the face normal (Z-up in Blender)
        normal_z = poly.normal.z
        for loop_idx in poly.loop_indices:
            if normal_z > moss_threshold:
                blend = (normal_z - moss_threshold) / (1.0 - moss_threshold)
                r = base_color[0] * (1 - blend) + moss_color[0] * blend
                g = base_color[1] * (1 - blend) + moss_color[1] * blend
                b = base_color[2] * (1 - blend) + moss_color[2] * blend
                color_layer.data[loop_idx].color = (r, g, b, 1.0)
            else:
                color_layer.data[loop_idx].color = base_color

def displace_vertices(obj, amount, seed_offset=0):
    """Randomly displace vertices along their normals for organic look."""
    rng = random.Random(seed_offset)
    mesh = obj.data
    mesh.update()
    for v in mesh.vertices:
        displacement = rng.uniform(-amount, amount)
        v.co += v.normal * displacement

def smooth_shade(obj):
    for poly in obj.data.polygons:
        poly.use_smooth = True

def create_material(name):
    """Create a vertex-color material."""
    mat = bpy.data.materials.new(name=name)
    mat.use_nodes = True
    tree = mat.node_tree
    nodes = tree.nodes
    links = tree.links

    for n in nodes:
        nodes.remove(n)

    vc_node = nodes.new('ShaderNodeVertexColor')
    vc_node.layer_name = "Color"
    vc_node.location = (-300, 0)

    bsdf = nodes.new('ShaderNodeBsdfPrincipled')
    bsdf.location = (0, 0)
    bsdf.inputs["Roughness"].default_value = 0.9
    bsdf.inputs["Metallic"].default_value = 0.0

    output = nodes.new('ShaderNodeOutputMaterial')
    output.location = (300, 0)

    links.new(vc_node.outputs["Color"], bsdf.inputs["Base Color"])
    links.new(bsdf.outputs["BSDF"], output.inputs["Surface"])

    return mat


# ---------------------------------------------------------------------------
# Rock Generators
# ---------------------------------------------------------------------------
def create_boulder():
    """Large rounded boulder."""
    bpy.ops.mesh.primitive_ico_sphere_add(
        subdivisions=2, radius=0.8,
        location=(0, 0, 0.6)
    )
    rock = bpy.context.active_object
    rock.name = "Rock_Boulder"

    # Flatten slightly
    rock.scale = (1.0, 0.9, 0.7)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)

    # Organic displacement
    displace_vertices(rock, 0.12, seed_offset=500)

    # Recalculate normals before coloring
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.mesh.normals_make_consistent(inside=False)
    bpy.ops.object.mode_set(mode='OBJECT')

    set_vertex_colors_with_moss(rock, ROCK_COLOR, MOSS_COLOR, moss_threshold=0.6)
    smooth_shade(rock)

    # Set origin to base
    min_z = min(v.co.z for v in rock.data.vertices)
    for v in rock.data.vertices:
        v.co.z -= min_z
    rock.location = (0, 0, 0)

    return rock


def create_standing_stone():
    """Tall monolithic standing stone."""
    bpy.ops.mesh.primitive_cube_add(location=(3, 0, 1.2))
    stone = bpy.context.active_object
    stone.name = "Rock_StandingStone"
    stone.scale = (0.3, 0.25, 1.2)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)

    # Subdivide for smoother deformation
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.mesh.subdivide(number_cuts=3)
    bpy.ops.object.mode_set(mode='OBJECT')

    # Organic displacement
    displace_vertices(stone, 0.06, seed_offset=600)

    # Taper the top slightly
    for v in stone.data.vertices:
        if v.co.z > 0.5:
            factor = 1.0 - (v.co.z - 0.5) * 0.15
            v.co.x *= factor
            v.co.y *= factor

    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.normals_make_consistent(inside=False)
    bpy.ops.object.mode_set(mode='OBJECT')

    set_vertex_colors_with_moss(stone, ROCK_DARK, MOSS_COLOR, moss_threshold=0.7)
    smooth_shade(stone)

    min_z = min(v.co.z for v in stone.data.vertices)
    for v in stone.data.vertices:
        v.co.z -= min_z
    stone.location = (3, 0, 0)

    return stone


def create_rock_cluster():
    """Group of 4 small rocks."""
    parts = []
    rng = random.Random(700)

    cluster_configs = [
        (6.0, 0.0, 0.25, 0.35),    # (x, y, z_offset, radius)
        (6.3, 0.25, 0.2, 0.25),
        (5.8, -0.2, 0.15, 0.20),
        (6.15, -0.15, 0.18, 0.28),
    ]

    for i, (x, y, z_off, rad) in enumerate(cluster_configs):
        bpy.ops.mesh.primitive_ico_sphere_add(
            subdivisions=1, radius=rad,
            location=(x, y, z_off)
        )
        rock = bpy.context.active_object
        rock.name = f"Cluster_Rock_{i}"

        # Random squash
        sx = rng.uniform(0.7, 1.1)
        sy = rng.uniform(0.7, 1.0)
        sz = rng.uniform(0.5, 0.8)
        rock.scale = (sx, sy, sz)
        bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)

        displace_vertices(rock, 0.05, seed_offset=700 + i * 13)

        bpy.ops.object.mode_set(mode='EDIT')
        bpy.ops.mesh.normals_make_consistent(inside=False)
        bpy.ops.object.mode_set(mode='OBJECT')

        color = ROCK_COLOR if i % 2 == 0 else ROCK_DARK
        set_vertex_colors_with_moss(rock, color, MOSS_COLOR, moss_threshold=0.5)
        smooth_shade(rock)
        parts.append(rock)

    # Join all
    bpy.ops.object.select_all(action='DESELECT')
    for obj in parts:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()

    cluster = bpy.context.active_object
    cluster.name = "Rock_Cluster"

    min_z = min(v.co.z for v in cluster.data.vertices)
    for v in cluster.data.vertices:
        v.co.z -= min_z
    cluster.location = (6, 0, 0)

    return cluster


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------
def main():
    print("=" * 60)
    print("  Generating rock variants")
    print("=" * 60)

    clear_scene()

    mat = create_material("Rock_Mat")

    boulder = create_boulder()
    standing = create_standing_stone()
    cluster = create_rock_cluster()

    for rock in [boulder, standing, cluster]:
        if rock.data.materials:
            rock.data.materials[0] = mat
        else:
            rock.data.materials.append(mat)

    # Stats
    for rock in [boulder, standing, cluster]:
        vert_count = len(rock.data.vertices)
        poly_count = len(rock.data.polygons)
        print(f"  {rock.name}: {vert_count} verts, {poly_count} polys")

    bpy.ops.object.select_all(action='SELECT')

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
