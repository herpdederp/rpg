"""
generate_humanoid.py
====================
Blender Python script — run headless:
    blender --background --python BlenderPipeline/scripts/generate_humanoid.py

Generates a low-poly humanoid mesh in T-pose (~3000 tris) and exports
to BlenderPipeline/exports/humanoid.glb

The model is built from scaled primitives joined into a single mesh,
with vertex groups pre-named for the skeleton that rig_and_export.py
will create. This keeps both scripts decoupled but compatible.
"""

import bpy
import bmesh
import math
import os
import sys
from mathutils import Vector, Matrix

# ---------------------------------------------------------------------------
# Config
# ---------------------------------------------------------------------------
EXPORT_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "exports")
EXPORT_PATH = os.path.join(EXPORT_DIR, "humanoid.glb")

# Body proportions (in Blender units ≈ metres)
# All positions are for a character ~1.75m tall, origin at feet
BODY = {
    # (location_xyz, scale_xyz, subdivisions_or_segments)
    # Torso — stretched cube
    "Hips":         {"pos": (0, 0, 0.95),  "scale": (0.28, 0.18, 0.12)},
    "Spine":        {"pos": (0, 0, 1.10),  "scale": (0.26, 0.17, 0.15)},
    "Chest":        {"pos": (0, 0, 1.30),  "scale": (0.30, 0.18, 0.16)},
    "Neck":         {"pos": (0, 0, 1.52),  "scale": (0.08, 0.08, 0.06)},
    "Head":         {"pos": (0, 0, 1.66),  "scale": (0.12, 0.14, 0.14)},

    # Left arm (T-pose: extended along +X)
    "UpperArm_L":   {"pos": (0.42, 0, 1.38), "scale": (0.14, 0.06, 0.06)},
    "LowerArm_L":   {"pos": (0.70, 0, 1.38), "scale": (0.14, 0.05, 0.05)},
    "Hand_L":       {"pos": (0.92, 0, 1.38), "scale": (0.06, 0.03, 0.08)},

    # Right arm (mirrored)
    "UpperArm_R":   {"pos": (-0.42, 0, 1.38), "scale": (0.14, 0.06, 0.06)},
    "LowerArm_R":   {"pos": (-0.70, 0, 1.38), "scale": (0.14, 0.05, 0.05)},
    "Hand_R":       {"pos": (-0.92, 0, 1.38), "scale": (0.06, 0.03, 0.08)},

    # Left leg
    "UpperLeg_L":   {"pos": (0.12, 0, 0.70),  "scale": (0.08, 0.08, 0.18)},
    "LowerLeg_L":   {"pos": (0.12, 0, 0.35),  "scale": (0.06, 0.06, 0.17)},
    "Foot_L":       {"pos": (0.12, 0.06, 0.06), "scale": (0.06, 0.12, 0.04)},

    # Right leg (mirrored)
    "UpperLeg_R":   {"pos": (-0.12, 0, 0.70),  "scale": (0.08, 0.08, 0.18)},
    "LowerLeg_R":   {"pos": (-0.12, 0, 0.35),  "scale": (0.06, 0.06, 0.17)},
    "Foot_R":       {"pos": (-0.12, 0.06, 0.06), "scale": (0.06, 0.12, 0.04)},
}

# Subdivision level per part — more for organic shapes, less for blocky bits
SUBDIV = {
    "Head": 2,
}
DEFAULT_SUBDIV = 1

# Vertex group assignments: which bones influence each part
# Maps part name -> list of (bone_name, weight) pairs
WEIGHTS = {
    "Hips":         [("Hips", 1.0)],
    "Spine":        [("Spine", 1.0)],
    "Chest":        [("Chest", 0.8), ("Spine", 0.2)],
    "Neck":         [("Neck", 1.0)],
    "Head":         [("Head", 1.0)],
    "UpperArm_L":   [("UpperArm_L", 1.0)],
    "LowerArm_L":   [("LowerArm_L", 1.0)],
    "Hand_L":       [("Hand_L", 1.0)],
    "UpperArm_R":   [("UpperArm_R", 1.0)],
    "LowerArm_R":   [("LowerArm_R", 1.0)],
    "Hand_R":       [("Hand_R", 1.0)],
    "UpperLeg_L":   [("UpperLeg_L", 1.0)],
    "LowerLeg_L":   [("LowerLeg_L", 1.0)],
    "Foot_L":       [("Foot_L", 1.0)],
    "UpperLeg_R":   [("UpperLeg_R", 1.0)],
    "LowerLeg_R":   [("LowerLeg_R", 1.0)],
    "Foot_R":       [("Foot_R", 1.0)],
}


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------
def clear_scene():
    """Remove all objects from the scene."""
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    # Purge orphan data
    for block in bpy.data.meshes:
        if block.users == 0:
            bpy.data.meshes.remove(block)


def create_body_part(name: str, pos: tuple, scale: tuple, subdiv: int) -> bpy.types.Object:
    """
    Create a subdivided cube at the given position/scale.
    Returns the Blender object.
    """
    bpy.ops.mesh.primitive_cube_add(location=pos)
    obj = bpy.context.active_object
    obj.name = name
    obj.scale = scale

    # Apply scale so geometry is baked
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)

    # Subdivision surface for rounder shapes
    if subdiv > 0:
        mod = obj.modifiers.new(name="Subsurf", type='SUBSURF')
        mod.levels = subdiv
        mod.render_levels = subdiv
        bpy.ops.object.modifier_apply(modifier=mod.name)

    return obj


def assign_vertex_group(obj: bpy.types.Object, group_name: str, weight: float):
    """Assign ALL vertices of obj to the named vertex group at the given weight."""
    if group_name not in obj.vertex_groups:
        obj.vertex_groups.new(name=group_name)
    vg = obj.vertex_groups[group_name]
    indices = [v.index for v in obj.data.vertices]
    vg.add(indices, weight, 'REPLACE')


def smooth_shade(obj: bpy.types.Object):
    """Apply smooth shading to an object."""
    for poly in obj.data.polygons:
        poly.use_smooth = True


def create_material() -> bpy.types.Material:
    """Create a simple grey PBR material for the humanoid."""
    mat = bpy.data.materials.new(name="Humanoid_Mat")
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Base Color"].default_value = (0.55, 0.45, 0.40, 1.0)
        bsdf.inputs["Roughness"].default_value = 0.7
        bsdf.inputs["Metallic"].default_value = 0.0
    return mat


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------
def main():
    print("=" * 60)
    print("  Generating low-poly humanoid")
    print("=" * 60)

    clear_scene()

    material = create_material()
    parts = []

    # --- Create each body part ---
    for part_name, props in BODY.items():
        subdiv = SUBDIV.get(part_name, DEFAULT_SUBDIV)
        obj = create_body_part(part_name, props["pos"], props["scale"], subdiv)

        # Assign vertex groups for rigging
        for bone_name, weight in WEIGHTS.get(part_name, []):
            assign_vertex_group(obj, bone_name, weight)

        # Material
        if obj.data.materials:
            obj.data.materials[0] = material
        else:
            obj.data.materials.append(material)

        smooth_shade(obj)
        parts.append(obj)

    # --- Join all parts into a single mesh ---
    bpy.ops.object.select_all(action='DESELECT')
    for obj in parts:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()

    humanoid = bpy.context.active_object
    humanoid.name = "Humanoid"

    # --- Apply a light decimate to hit ~3000 tris ---
    # Count current tris
    bpy.context.view_layer.update()
    depsgraph = bpy.context.evaluated_depsgraph_get()
    eval_obj = humanoid.evaluated_get(depsgraph)
    eval_mesh = eval_obj.to_mesh()
    current_tris = sum(len(p.vertices) - 2 for p in eval_mesh.polygons)
    eval_obj.to_mesh_clear()
    print(f"  Pre-decimate triangle count: {current_tris}")

    TARGET_TRIS = 3000
    if current_tris > TARGET_TRIS:
        ratio = TARGET_TRIS / current_tris
        mod = humanoid.modifiers.new(name="Decimate", type='DECIMATE')
        mod.ratio = ratio
        bpy.ops.object.modifier_apply(modifier=mod.name)

    # Final count
    depsgraph = bpy.context.evaluated_depsgraph_get()
    eval_obj = humanoid.evaluated_get(depsgraph)
    eval_mesh = eval_obj.to_mesh()
    final_tris = sum(len(p.vertices) - 2 for p in eval_mesh.polygons)
    eval_obj.to_mesh_clear()
    print(f"  Final triangle count: {final_tris}")

    # --- Recalculate normals ---
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.mesh.normals_make_consistent(inside=False)
    bpy.ops.object.mode_set(mode='OBJECT')

    # --- Set origin to base of feet ---
    # Find lowest Z vertex
    min_z = min(v.co.z for v in humanoid.data.vertices)
    humanoid.location.z -= min_z
    bpy.ops.object.transform_apply(location=True, rotation=False, scale=False)

    # --- Export glTF ---
    os.makedirs(EXPORT_DIR, exist_ok=True)

    bpy.ops.export_scene.gltf(
        filepath=EXPORT_PATH,
        export_format='GLB',
        use_selection=True,
        export_apply=True,
        export_yup=True,            # Unity expects Y-up
        export_materials='EXPORT',
    )

    print(f"  Exported: {EXPORT_PATH}")
    file_size = os.path.getsize(EXPORT_PATH)
    print(f"  File size: {file_size / 1024:.1f} KB")
    print("=" * 60)
    print("  Done! Run rig_and_export.py next.")
    print("=" * 60)


if __name__ == "__main__":
    main()
