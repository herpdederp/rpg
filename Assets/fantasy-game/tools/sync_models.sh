#!/usr/bin/env bash
# sync_models.sh — Copy Blender exports to Unity StreamingAssets
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"

SRC="$PROJECT_ROOT/BlenderPipeline/exports"
DST="$PROJECT_ROOT/Assets/StreamingAssets/Models"

mkdir -p "$DST"

if [ ! -d "$SRC" ] || [ -z "$(ls -A "$SRC" 2>/dev/null)" ]; then
    echo "ERROR: No exports found in $SRC"
    echo "Run the Blender scripts first:"
    echo "  blender --background --python BlenderPipeline/scripts/generate_humanoid.py"
    echo "  blender --background --python BlenderPipeline/scripts/rig_and_export.py"
    exit 1
fi

count=0
for f in "$SRC"/*.glb "$SRC"/*.gltf; do
    [ -e "$f" ] || continue
    cp -v "$f" "$DST/"
    count=$((count + 1))
done

echo ""
echo "Synced $count model(s) to $DST"
