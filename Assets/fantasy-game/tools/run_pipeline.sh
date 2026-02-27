#!/usr/bin/env bash
# run_pipeline.sh — Full Blender-to-Unity pipeline in one command
#
# Usage:
#   bash tools/run_pipeline.sh [--blender /path/to/blender]
#
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"

# Find Blender
BLENDER="${BLENDER:-blender}"
if [[ "${1:-}" == "--blender" ]]; then
    BLENDER="$2"
fi

if ! command -v "$BLENDER" &>/dev/null; then
    echo "ERROR: Blender not found. Set BLENDER env var or pass --blender /path"
    exit 1
fi

echo "=== Fantasy Game Asset Pipeline ==="
echo "Blender: $("$BLENDER" --version 2>&1 | head -1)"
echo ""

echo "[1/7] Generating humanoid mesh..."
"$BLENDER" --background --python "$PROJECT_ROOT/BlenderPipeline/scripts/generate_humanoid.py" 2>&1 | tail -5
echo ""

echo "[2/7] Rigging and exporting humanoid..."
"$BLENDER" --background --python "$PROJECT_ROOT/BlenderPipeline/scripts/rig_and_export.py" 2>&1 | tail -5
echo ""

echo "[3/7] Generating animations (Idle, Walk, Run, Jump, Attack)..."
"$BLENDER" --background --python "$PROJECT_ROOT/BlenderPipeline/scripts/generate_animations.py" 2>&1 | tail -5
echo ""

echo "[4/7] Generating trees..."
"$BLENDER" --background --python "$PROJECT_ROOT/BlenderPipeline/scripts/generate_trees.py" 2>&1 | tail -5
echo ""

echo "[5/7] Generating rocks..."
"$BLENDER" --background --python "$PROJECT_ROOT/BlenderPipeline/scripts/generate_rocks.py" 2>&1 | tail -5
echo ""

echo "[6/7] Generating sword..."
"$BLENDER" --background --python "$PROJECT_ROOT/BlenderPipeline/scripts/generate_sword.py" 2>&1 | tail -5
echo ""

echo "[7/8] Generating enemies (Slime, Skeleton, Wolf)..."
"$BLENDER" --background --python "$PROJECT_ROOT/BlenderPipeline/scripts/generate_enemies.py" 2>&1 | tail -5
echo ""

echo "[8/8] Syncing to Unity..."
bash "$PROJECT_ROOT/tools/sync_models.sh"
echo ""

echo "=== Pipeline complete ==="
echo "Open Unity and hit Play."
