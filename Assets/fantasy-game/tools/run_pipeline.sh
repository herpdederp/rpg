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

echo "[1/3] Generating humanoid mesh..."
"$BLENDER" --background --python "$PROJECT_ROOT/BlenderPipeline/scripts/generate_humanoid.py" 2>&1 | tail -5
echo ""

echo "[2/3] Rigging and exporting..."
"$BLENDER" --background --python "$PROJECT_ROOT/BlenderPipeline/scripts/rig_and_export.py" 2>&1 | tail -5
echo ""

echo "[3/3] Syncing to Unity..."
bash "$PROJECT_ROOT/tools/sync_models.sh"
echo ""

echo "=== Pipeline complete ==="
echo "Open Unity and hit Play."
