# Fantasy Game — Unity + Blender CLI Pipeline

Code-driven 3D fantasy game. No manual editor work beyond hitting Play.

## Project Structure

```
fantasy-game/
├── Assets/                          # Unity assets root
│   ├── Scripts/
│   │   ├── Player/                  # Player controller, input, stats
│   │   ├── Camera/                  # Third-person camera rig
│   │   ├── Loading/                 # Runtime glTF loader
│   │   └── Utils/                   # Shared helpers
│   ├── Shaders/                     # Custom shaders
│   ├── Prefabs/                     # Runtime-generated prefabs
│   ├── Scenes/                      # Minimal bootstrap scene
│   └── StreamingAssets/Models/      # glTF models loaded at runtime
├── BlenderPipeline/
│   ├── scripts/                     # Blender Python automation
│   ├── exports/                     # glTF output (copied to StreamingAssets)
│   └── source/                      # .blend source files
├── Packages/
├── ProjectSettings/
├── docs/
└── tools/                           # CLI build/export helpers
```

## Quick Start

### 1. Generate the humanoid mesh
```bash
blender --background --python BlenderPipeline/scripts/generate_humanoid.py
```

### 2. Rig and export
```bash
blender --background --python BlenderPipeline/scripts/rig_and_export.py
```

### 3. Deploy models to Unity StreamingAssets
```bash
bash tools/sync_models.sh
```

### 4. Open Unity and hit Play
`GltfBootstrap` loads the rigged model and attaches the third-person controller.

## Dependencies

- **Unity 2022.3+ LTS** with packages:
  - `com.unity.cloud.gltfast` (GLTFast — runtime glTF import)
  - `com.cysharp.unitask` (UniTask — async/await)
- **Blender 3.6+** (headless CLI only)
