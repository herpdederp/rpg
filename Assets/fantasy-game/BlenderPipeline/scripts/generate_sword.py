"""
generate_sword.py
=================
Blender Python script — run headless:
    blender --background --python BlenderPipeline/scripts/generate_sword.py

Generates a simple fantasy sword with vertex colors.
Exports to BlenderPipeline/exports/sword.glb
"""

import bpy
import os
from mathutils import Vector

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
EXPORT_DIR = os.path.join(SCRIPT_DIR, "..", "exports")
EXPORT_PATH = os.path.join(EXPORT_DIR, "sword.glb")

# Colors
BLADE_COLOR = (0.75, 0.75, 0.80, 1.0)      # Silver steel
BLADE_EDGE = (0.85, 0.85, 0.90, 1.0)       # Bright edge
GUARD_COLOR = (0.55, 0.45, 0.20, 1.0)       # Bronze
GRIP_COLOR = (0.35, 0.22, 0.12, 1.0)        # Dark leather
POMMEL_COLOR = (0.55, 0.45, 0.20, 1.0)      # Bronze


def clear_scene():
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    for block in bpy.data.meshes:
        if block.users == 0:
            bpy.data.meshes.remove(block)


def create_part(name, location, scale, color, prim='cube'):
    if prim == 'cube':
        bpy.ops.mesh.primitive_cube_add(location=location)
    elif prim == 'cylinder':
        bpy.ops.mesh.primitive_cylinder_add(location=location, vertices=8, radius=1, depth=1)
    elif prim == 'uv_sphere':
        bpy.ops.mesh.primitive_uv_sphere_add(location=location, segments=8, ring_count=6)

    obj = bpy.context.active_object
    obj.name = name
    obj.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)

    # Add vertex colors
    mesh = obj.data
    if not mesh.color_attributes:
        mesh.color_attributes.new(name="Color", type='BYTE_COLOR', domain='CORNER')
    color_layer = mesh.color_attributes[0]
    for i in range(len(color_layer.data)):
        color_layer.data[i].color = color

    return obj


def main():
    print("=" * 60)
    print("  Generating sword")
    print("=" * 60)

    clear_scene()

    parts = []

    # Blade (elongated cube, tapered) — Z up in Blender
    blade = create_part("Blade", (0, 0, 0.55), (0.02, 0.005, 0.35), BLADE_COLOR)
    parts.append(blade)

    # Blade tip (smaller, slightly forward)
    tip = create_part("BladeTip", (0, 0, 0.92), (0.012, 0.004, 0.05), BLADE_EDGE)
    parts.append(tip)

    # Cross guard
    guard = create_part("Guard", (0, 0, 0.18), (0.08, 0.015, 0.015), GUARD_COLOR)
    parts.append(guard)

    # Grip
    grip = create_part("Grip", (0, 0, 0.08), (0.015, 0.015, 0.08), GRIP_COLOR, prim='cylinder')
    parts.append(grip)

    # Pommel
    pommel = create_part("Pommel", (0, 0, -0.02), (0.02, 0.02, 0.02), POMMEL_COLOR, prim='uv_sphere')
    parts.append(pommel)

    # Join all parts
    bpy.ops.object.select_all(action='DESELECT')
    for p in parts:
        p.select_set(True)
    bpy.context.view_layer.objects.active = blade
    bpy.ops.object.join()

    sword = bpy.context.active_object
    sword.name = "Sword"

    # Set origin to grip area (where the hand holds it)
    bpy.context.scene.cursor.location = Vector((0, 0, 0.1))
    bpy.ops.object.origin_set(type='ORIGIN_CURSOR')

    # Smooth shade
    for poly in sword.data.polygons:
        poly.use_smooth = True

    # Create vertex color material
    mat = bpy.data.materials.new(name="SwordMat")
    mat.use_nodes = True
    tree = mat.node_tree
    for n in tree.nodes:
        tree.nodes.remove(n)

    vc_node = tree.nodes.new('ShaderNodeVertexColor')
    vc_node.layer_name = "Color"
    vc_node.location = (-300, 0)

    bsdf = tree.nodes.new('ShaderNodeBsdfPrincipled')
    bsdf.location = (0, 0)
    bsdf.inputs["Roughness"].default_value = 0.3
    bsdf.inputs["Metallic"].default_value = 0.7

    output = tree.nodes.new('ShaderNodeOutputMaterial')
    output.location = (300, 0)

    tree.links.new(vc_node.outputs["Color"], bsdf.inputs["Base Color"])
    tree.links.new(bsdf.outputs["BSDF"], output.inputs["Surface"])

    sword.data.materials.append(mat)

    # Stats
    print(f"  Sword: {len(sword.data.vertices)} verts, {len(sword.data.polygons)} polys")

    # Export
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
