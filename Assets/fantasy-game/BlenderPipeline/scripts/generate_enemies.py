"""
generate_enemies.py
===================
Blender Python script — run headless:
    blender --background --python BlenderPipeline/scripts/generate_enemies.py

Generates three enemy models with vertex colors:
  - Slime  (~200 tris)  — green blob
  - Skeleton (~600 tris) — bony humanoid
  - Wolf (~400 tris)     — quadruped canine

Exports each to BlenderPipeline/exports/<name>.glb
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

    # Build vertex index -> position map
    vert_positions = {v.index: v.co.copy() for v in mesh.vertices}

    # Color each corner (loop)
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


# =================================================================
#  SLIME — A squishy green blob with darker underbelly
# =================================================================
def generate_slime():
    print("=" * 60)
    print("  Generating Slime")
    print("=" * 60)
    clear_scene()

    # Base: UV sphere, squashed vertically, slightly irregular
    bpy.ops.mesh.primitive_uv_sphere_add(
        segments=12, ring_count=8,
        radius=0.5, location=(0, 0, 0.3)
    )
    slime = bpy.context.active_object
    slime.name = "Slime"

    # Squash into blob shape
    slime.scale = (1.0, 1.0, 0.6)
    bpy.ops.object.transform_apply(scale=True)

    # Deform vertices for organic blobby look
    mesh = slime.data
    for v in mesh.vertices:
        # Flatten bottom
        if v.co.z < 0.05:
            v.co.z = 0.0
        # Add slight bulge variation
        dist_xz = math.sqrt(v.co.x ** 2 + v.co.y ** 2)
        if v.co.z > 0.1:
            wobble = math.sin(math.atan2(v.co.y, v.co.x) * 3) * 0.04
            v.co.x *= (1.0 + wobble)
            v.co.y *= (1.0 + wobble)

    # Eyes — two small white spheres
    for side in [-1, 1]:
        bpy.ops.mesh.primitive_uv_sphere_add(
            segments=6, ring_count=4,
            radius=0.06, location=(side * 0.15, -0.35, 0.35)
        )
        eye = bpy.context.active_object
        eye.name = f"SlimeEye_{side}"

    # Pupils — tiny dark spheres
    for side in [-1, 1]:
        bpy.ops.mesh.primitive_uv_sphere_add(
            segments=4, ring_count=3,
            radius=0.03, location=(side * 0.15, -0.39, 0.35)
        )
        pupil = bpy.context.active_object
        pupil.name = f"SlimePupil_{side}"

    # Join all
    bpy.ops.object.select_all(action='SELECT')
    bpy.context.view_layer.objects.active = slime
    bpy.ops.object.join()

    # Vertex colors
    def slime_color(pos):
        # Green body with darker bottom
        height_factor = max(0, min(1, pos.z / 0.4))
        g = 0.55 + height_factor * 0.3
        r = 0.15 + height_factor * 0.1
        b = 0.1
        # Eyes are white/dark
        if abs(pos.y + 0.35) < 0.08 and pos.z > 0.28:
            dist_eye = min(
                math.sqrt((pos.x - 0.15) ** 2 + (pos.y + 0.35) ** 2),
                math.sqrt((pos.x + 0.15) ** 2 + (pos.y + 0.35) ** 2)
            )
            if dist_eye < 0.05:
                return (0.1, 0.1, 0.1, 1.0)  # Pupil
            elif dist_eye < 0.08:
                return (0.95, 0.95, 0.95, 1.0)  # Eye white
        return (r, g, b, 1.0)

    set_vertex_colors(slime, slime_color)

    # Material
    mat = make_vc_material("SlimeMat", roughness=0.3)
    slime.data.materials.append(mat)

    # Smooth shade
    for poly in slime.data.polygons:
        poly.use_smooth = True

    # Origin at bottom center
    bpy.context.scene.cursor.location = Vector((0, 0, 0))
    bpy.ops.object.origin_set(type='ORIGIN_CURSOR')

    print(f"  Slime: {len(slime.data.vertices)} verts, {len(slime.data.polygons)} polys")
    export_glb(os.path.join(EXPORT_DIR, "slime.glb"))


# =================================================================
#  SKELETON — Bony humanoid figure
# =================================================================
def generate_skeleton():
    print("=" * 60)
    print("  Generating Skeleton")
    print("=" * 60)
    clear_scene()

    parts = []

    def add_part(name, prim, loc, scl):
        if prim == 'cube':
            bpy.ops.mesh.primitive_cube_add(location=loc)
        elif prim == 'cylinder':
            bpy.ops.mesh.primitive_cylinder_add(location=loc, vertices=6, radius=1, depth=1)
        elif prim == 'uv_sphere':
            bpy.ops.mesh.primitive_uv_sphere_add(location=loc, segments=8, ring_count=6)
        obj = bpy.context.active_object
        obj.name = name
        obj.scale = scl
        bpy.ops.object.transform_apply(scale=True)
        parts.append(obj)
        return obj

    # Head (skull)
    add_part("Skull", 'uv_sphere', (0, 0, 1.65), (0.12, 0.14, 0.15))

    # Jaw
    add_part("Jaw", 'cube', (0, -0.02, 1.52), (0.08, 0.10, 0.03))

    # Eye sockets (dark indentations — small spheres we'll color dark)
    add_part("EyeL", 'uv_sphere', (-0.05, -0.12, 1.68), (0.025, 0.025, 0.03))
    add_part("EyeR", 'uv_sphere', (0.05, -0.12, 1.68), (0.025, 0.025, 0.03))

    # Spine (chain of small cylinders)
    for i in range(4):
        z = 1.35 - i * 0.12
        add_part(f"Spine_{i}", 'cylinder', (0, 0, z), (0.04, 0.04, 0.06))

    # Ribcage (curved bars)
    for i in range(3):
        z = 1.3 - i * 0.1
        for side in [-1, 1]:
            add_part(f"Rib_{i}_{side}", 'cube',
                     (side * 0.08, -0.02, z), (0.06, 0.015, 0.015))

    # Pelvis
    add_part("Pelvis", 'cube', (0, 0, 0.85), (0.12, 0.06, 0.04))

    # Upper arms
    for side in [-1, 1]:
        add_part(f"UpperArm_{side}", 'cylinder',
                 (side * 0.22, 0, 1.3), (0.025, 0.025, 0.12))

    # Lower arms
    for side in [-1, 1]:
        add_part(f"LowerArm_{side}", 'cylinder',
                 (side * 0.22, 0, 1.05), (0.02, 0.02, 0.12))

    # Hands
    for side in [-1, 1]:
        add_part(f"Hand_{side}", 'cube',
                 (side * 0.22, 0, 0.92), (0.03, 0.02, 0.04))

    # Upper legs
    for side in [-1, 1]:
        add_part(f"UpperLeg_{side}", 'cylinder',
                 (side * 0.06, 0, 0.65), (0.03, 0.03, 0.12))

    # Lower legs
    for side in [-1, 1]:
        add_part(f"LowerLeg_{side}", 'cylinder',
                 (side * 0.06, 0, 0.4), (0.025, 0.025, 0.12))

    # Feet
    for side in [-1, 1]:
        add_part(f"Foot_{side}", 'cube',
                 (side * 0.06, -0.03, 0.28), (0.03, 0.06, 0.015))

    # Join all parts
    bpy.ops.object.select_all(action='DESELECT')
    for p in parts:
        p.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()

    skeleton = bpy.context.active_object
    skeleton.name = "Skeleton"

    # Vertex colors — bone white with dark eye sockets
    def skeleton_color(pos):
        # Eye sockets — dark
        for ex in [-0.05, 0.05]:
            eye_dist = math.sqrt((pos.x - ex) ** 2 + (pos.y + 0.12) ** 2 + (pos.z - 1.68) ** 2)
            if eye_dist < 0.04:
                return (0.1, 0.05, 0.05, 1.0)

        # Bone white with slight variation
        base = 0.82 + (pos.z * 0.02)
        r = min(1.0, base + 0.03)
        g = min(1.0, base)
        b = min(1.0, base - 0.05)
        return (r, g, b, 1.0)

    set_vertex_colors(skeleton, skeleton_color)

    mat = make_vc_material("SkeletonMat", roughness=0.7)
    skeleton.data.materials.append(mat)

    for poly in skeleton.data.polygons:
        poly.use_smooth = True

    # Origin at feet
    bpy.context.scene.cursor.location = Vector((0, 0, 0.28))
    bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
    skeleton.location = (0, 0, 0)

    print(f"  Skeleton: {len(skeleton.data.vertices)} verts, {len(skeleton.data.polygons)} polys")
    export_glb(os.path.join(EXPORT_DIR, "skeleton.glb"))


# =================================================================
#  WOLF — Quadruped canine
# =================================================================
def generate_wolf():
    print("=" * 60)
    print("  Generating Wolf")
    print("=" * 60)
    clear_scene()

    parts = []

    def add_part(name, prim, loc, scl):
        if prim == 'cube':
            bpy.ops.mesh.primitive_cube_add(location=loc)
        elif prim == 'cylinder':
            bpy.ops.mesh.primitive_cylinder_add(location=loc, vertices=6, radius=1, depth=1)
        elif prim == 'uv_sphere':
            bpy.ops.mesh.primitive_uv_sphere_add(location=loc, segments=8, ring_count=6)
        elif prim == 'cone':
            bpy.ops.mesh.primitive_cone_add(location=loc, vertices=6, radius1=1, radius2=0, depth=1)
        obj = bpy.context.active_object
        obj.name = name
        obj.scale = scl
        bpy.ops.object.transform_apply(scale=True)
        parts.append(obj)
        return obj

    # Body (elongated ellipsoid)
    body = add_part("Body", 'uv_sphere', (0, 0, 0.45), (0.18, 0.4, 0.15))

    # Head
    add_part("Head", 'uv_sphere', (0, -0.55, 0.52), (0.12, 0.15, 0.11))

    # Snout
    add_part("Snout", 'cube', (0, -0.72, 0.48), (0.06, 0.10, 0.05))

    # Nose
    add_part("Nose", 'uv_sphere', (0, -0.82, 0.49), (0.025, 0.02, 0.02))

    # Ears
    for side in [-1, 1]:
        add_part(f"Ear_{side}", 'cone',
                 (side * 0.08, -0.50, 0.65), (0.03, 0.03, 0.06))

    # Eyes
    for side in [-1, 1]:
        add_part(f"Eye_{side}", 'uv_sphere',
                 (side * 0.07, -0.62, 0.55), (0.02, 0.015, 0.02))

    # Front legs
    for side in [-1, 1]:
        # Upper
        add_part(f"FrontLegUp_{side}", 'cylinder',
                 (side * 0.1, -0.28, 0.22), (0.03, 0.03, 0.12))
        # Lower
        add_part(f"FrontLegLow_{side}", 'cylinder',
                 (side * 0.1, -0.28, 0.06), (0.025, 0.025, 0.08))

    # Back legs
    for side in [-1, 1]:
        # Upper
        add_part(f"BackLegUp_{side}", 'cylinder',
                 (side * 0.1, 0.28, 0.22), (0.035, 0.035, 0.12))
        # Lower
        add_part(f"BackLegLow_{side}", 'cylinder',
                 (side * 0.1, 0.28, 0.06), (0.025, 0.025, 0.08))

    # Tail (cone)
    add_part("Tail", 'cone', (0, 0.55, 0.55), (0.03, 0.15, 0.03))
    # Rotate tail upward
    tail = parts[-1]
    tail.rotation_euler = (math.radians(-30), 0, 0)
    bpy.ops.object.transform_apply(rotation=True)

    # Join all
    bpy.ops.object.select_all(action='DESELECT')
    for p in parts:
        p.select_set(True)
    bpy.context.view_layer.objects.active = body
    bpy.ops.object.join()

    wolf = bpy.context.active_object
    wolf.name = "Wolf"

    # Vertex colors
    def wolf_color(pos):
        # Nose — dark
        if pos.y < -0.78 and pos.z > 0.46 and pos.z < 0.52:
            return (0.1, 0.08, 0.08, 1.0)

        # Eyes — amber
        for side in [-1, 1]:
            eye_dist = math.sqrt((pos.x - side * 0.07) ** 2 + (pos.y + 0.62) ** 2 + (pos.z - 0.55) ** 2)
            if eye_dist < 0.025:
                return (0.85, 0.65, 0.15, 1.0)

        # Underbelly — lighter
        if pos.z < 0.25:
            return (0.6, 0.55, 0.5, 1.0)

        # Back — darker
        if pos.z > 0.55:
            return (0.38, 0.33, 0.28, 1.0)

        # Default fur
        r = 0.5 + (pos.z * 0.05)
        g = 0.45 + (pos.z * 0.04)
        b = 0.38 + (pos.z * 0.02)
        return (r, g, b, 1.0)

    set_vertex_colors(wolf, wolf_color)

    mat = make_vc_material("WolfMat", roughness=0.8)
    wolf.data.materials.append(mat)

    for poly in wolf.data.polygons:
        poly.use_smooth = True

    # Origin at bottom
    bpy.context.scene.cursor.location = Vector((0, 0, 0))
    bpy.ops.object.origin_set(type='ORIGIN_CURSOR')

    print(f"  Wolf: {len(wolf.data.vertices)} verts, {len(wolf.data.polygons)} polys")
    export_glb(os.path.join(EXPORT_DIR, "wolf.glb"))


# =================================================================
#  MAIN
# =================================================================
def main():
    generate_slime()
    generate_skeleton()
    generate_wolf()

    print("=" * 60)
    print("  All enemies generated!")
    print("=" * 60)


if __name__ == "__main__":
    main()
