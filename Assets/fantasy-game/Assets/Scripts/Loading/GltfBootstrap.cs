// Assets/Scripts/Loading/GltfBootstrap.cs
// =========================================
// Master bootstrap: initializes the world, loads glTF models,
// spawns the character, and sets up the camera.
// Uses [RuntimeInitializeOnLoadMethod] so it works without
// any manual scene setup — just hit Play.
//
// Works on both Desktop (standalone) and WebGL.

using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using GLTFast;
using FantasyGame.World;
using FantasyGame.RPG;
using FantasyGame.Combat;
using FantasyGame.UI;
using FantasyGame.Enemies;
using FantasyGame.Interaction;
using FantasyGame.Audio;
using FantasyGame.VFX;
using FantasyGame.Utils;
using FantasyGame.Dungeon;

namespace FantasyGame.Loading
{
    public class GltfBootstrap : MonoBehaviour
    {
        private const string MODEL_FILENAME = "humanoid_rigged.glb";
        private const string TREES_FILENAME = "trees.glb";
        private const string ROCKS_FILENAME = "rocks.glb";
        private const string SWORD_FILENAME = "sword.glb";
        private const string SLIME_FILENAME = "slime.glb";
        private const string SKELETON_FILENAME = "skeleton.glb";
        private const string WOLF_FILENAME = "wolf.glb";
        private const string NPCS_FILENAME = "npcs.glb";
        private const string PROPS_FILENAME = "props.glb";
        private const string ITEMS_FILENAME = "items.glb";
        private const string BUILDINGS_FILENAME = "buildings.glb";
        private const string DUNGEON_FILENAME = "dungeon.glb";
        private const int WORLD_SEED = 12345;

        private WorldManager _worldManager;
        private Mesh _swordMesh;
        private Mesh _slimeMesh;
        private Mesh _skeletonMesh;
        private Mesh _wolfMesh;
        private Mesh[] _npcMeshes;
        private Mesh[] _propMeshes;
        private Mesh[] _itemMeshes;
        private Mesh[] _buildingMeshes;
        private Mesh[] _dungeonMeshes;
        private EnemySpawner _enemySpawner;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBootstrap()
        {
            if (FindAnyObjectByType<GltfBootstrap>() != null)
            {
                Debug.Log("[GltfBootstrap] Instance already in scene, skipping auto-create.");
                return;
            }

            Debug.Log("[GltfBootstrap] Auto-creating bootstrap...");
            var go = new GameObject("GameBootstrap");
            go.AddComponent<GltfBootstrap>();
        }

        private async void Start()
        {
            try
            {
                Debug.Log("[GltfBootstrap] Start() running...");
                await InitializeGame();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[GltfBootstrap] Exception during setup:\n{ex}");
            }
        }

        private async Task InitializeGame()
        {
            // --- Create materials ---
            Material terrainMat = CreatePainterlyMaterial("TerrainMat", Color.white);
            Material vegetationMat = CreatePainterlyMaterial("VegetationMat", Color.white);
            Material grassMat = CreateGrassMaterial();

            // --- Load vegetation assets ---
            Mesh[] treeMeshes = await LoadMeshesFromGlb(TREES_FILENAME);
            Mesh[] rockMeshes = await LoadMeshesFromGlb(ROCKS_FILENAME);
            Mesh[] swordMeshes = await LoadMeshesFromGlb(SWORD_FILENAME);
            _swordMesh = swordMeshes.Length > 0 ? swordMeshes[0] : null;

            // Enemy meshes
            Mesh[] slimeMeshes = await LoadMeshesFromGlb(SLIME_FILENAME);
            Mesh[] skeletonMeshes = await LoadMeshesFromGlb(SKELETON_FILENAME);
            Mesh[] wolfMeshes = await LoadMeshesFromGlb(WOLF_FILENAME);
            _slimeMesh = slimeMeshes.Length > 0 ? slimeMeshes[0] : null;
            _skeletonMesh = skeletonMeshes.Length > 0 ? skeletonMeshes[0] : null;
            _wolfMesh = wolfMeshes.Length > 0 ? wolfMeshes[0] : null;

            Debug.Log($"[GltfBootstrap] Loaded {treeMeshes.Length} tree meshes, {rockMeshes.Length} rock meshes, sword={_swordMesh != null}");
            Debug.Log($"[GltfBootstrap] Enemy meshes: slime={_slimeMesh != null}, skeleton={_skeletonMesh != null}, wolf={_wolfMesh != null}");

            // NPC, prop, and item meshes
            _npcMeshes = await LoadMeshesFromGlb(NPCS_FILENAME);
            _propMeshes = await LoadMeshesFromGlb(PROPS_FILENAME);
            _itemMeshes = await LoadMeshesFromGlb(ITEMS_FILENAME);
            _buildingMeshes = await LoadMeshesFromGlb(BUILDINGS_FILENAME);
            _dungeonMeshes = await LoadMeshesFromGlb(DUNGEON_FILENAME);
            Debug.Log($"[GltfBootstrap] World meshes: npcs={_npcMeshes.Length}, props={_propMeshes.Length}, items={_itemMeshes.Length}, buildings={_buildingMeshes.Length}, dungeon={_dungeonMeshes.Length}");

            // --- Register flat zones (must happen before any terrain generation) ---
            NoiseUtils.RegisterFlatZone(80f, 80f, 16f, 12f, 12f); // Village plateau

            // Dungeon entrance: flat zone so the entrance arch sits on level ground
            float dungeonEntranceY = NoiseUtils.SampleHeight(140f, 130f, WORLD_SEED);
            NoiseUtils.RegisterFlatZone(140f, 130f, 6f, 4f, dungeonEntranceY);

            // --- Initialize world ---
            var worldGo = new GameObject("WorldManager");
            _worldManager = worldGo.AddComponent<WorldManager>();
            _worldManager.Init(WORLD_SEED, terrainMat, vegetationMat, grassMat, treeMeshes, rockMeshes);

            // Pre-generate terrain chunks around spawn and dungeon entrance
            var spawnPos = new Vector3(140f, 0f, 135f);
            _worldManager.Terrain.UpdateChunks(spawnPos);
            _worldManager.Terrain.UpdateChunks(new Vector3(80f, 0f, 80f)); // village too
            // Wait a frame so MeshColliders are baked by physics
            await Task.Yield();

            // --- Load and setup character ---
            await LoadAndSetupCharacter();

            Debug.Log("[GltfBootstrap] Game initialized! WASD to move, mouse to look, Space to jump.");
        }

        private async Task LoadAndSetupCharacter()
        {
            string modelUrl = BuildModelUrl(MODEL_FILENAME);
            Debug.Log($"[GltfBootstrap] Loading character from: {modelUrl}");

            var gltf = new GltfImport();
            bool success = await gltf.Load(modelUrl);

            if (!success)
            {
                Debug.LogError("[GltfBootstrap] Failed to load character glTF.");
                return;
            }

            // Instantiate with GameObjectInstantiator (needed for animations)
            var characterRoot = new GameObject("Character");

            // Spawn near dungeon entrance
            float spawnX = 140f, spawnZ = 135f;
            float spawnHeight = _worldManager.GetTerrainHeight(spawnX, spawnZ);
            characterRoot.transform.position = new Vector3(spawnX, spawnHeight + 0.5f, spawnZ);

            var instantiator = new GameObjectInstantiator(gltf, characterRoot.transform);
            bool instantiated = await gltf.InstantiateMainSceneAsync(instantiator);

            if (!instantiated)
            {
                Debug.LogError("[GltfBootstrap] Failed to instantiate character.");
                return;
            }

            Debug.Log($"[GltfBootstrap] Character instantiated. Children: {characterRoot.transform.childCount}");

            // Log and fix child offsets — GLTFast may place the scene root node
            // with a position offset that lifts the model off the ground
            for (int i = 0; i < characterRoot.transform.childCount; i++)
            {
                var child = characterRoot.transform.GetChild(i);
                Debug.Log($"[GltfBootstrap] Child[{i}]: '{child.name}' localPos={child.localPosition} localScale={child.localScale}");
                // Zero out any vertical offset in the imported scene root
                if (child.localPosition.y > 0.01f || child.localPosition.y < -0.01f)
                {
                    Debug.Log($"[GltfBootstrap] Zeroing child Y offset: {child.localPosition.y:F3}");
                    child.localPosition = new Vector3(child.localPosition.x, 0f, child.localPosition.z);
                }
            }

            var renderers = characterRoot.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                Debug.LogWarning("[GltfBootstrap] No renderers found! Creating debug cube.");
                CreateDebugCube(characterRoot.transform);
            }

            // Attach systems
            SetupCharacterController(characterRoot);
            SetupAnimation(characterRoot);
            SetupCamera(characterRoot.transform);

            // --- Phase 2: RPG, Combat, Inventory, HUD ---
            SetupRPGSystems(characterRoot);

            // Snap character to actual ground using a raycast on the terrain collider
            SnapToGround(characterRoot);

            // Tell world manager to track the player
            _worldManager.SetPlayer(characterRoot.transform);

            // --- Phase 3: Enemies ---
            SetupEnemySpawner(characterRoot.transform);

            // --- Phase 4: World Interaction ---
            SetupWorldInteraction(characterRoot);

            // --- Phase 5: Polish (Audio, VFX, Minimap, Pause, Water) ---
            SetupPolishSystems(characterRoot);
        }

        /// <summary>
        /// Load meshes from a GLB file in StreamingAssets/Models/.
        /// Returns an array of meshes (one per object in the GLB).
        /// Returns empty array if file doesn't exist or fails to load.
        /// </summary>
        private async Task<Mesh[]> LoadMeshesFromGlb(string filename)
        {
            string url = BuildModelUrl(filename);
            Debug.Log($"[GltfBootstrap] Loading meshes from: {url}");

            var gltf = new GltfImport();
            bool success = await gltf.Load(url);

            if (!success)
            {
                Debug.LogWarning($"[GltfBootstrap] Could not load {filename} — vegetation will be empty.");
                return new Mesh[0];
            }

            // Instantiate to a temp parent, extract meshes, then destroy
            var tempParent = new GameObject($"_temp_{filename}");
            tempParent.SetActive(false);

            var instantiator = new GameObjectInstantiator(gltf, tempParent.transform);
            await gltf.InstantiateMainSceneAsync(instantiator);

            var meshFilters = tempParent.GetComponentsInChildren<MeshFilter>(true);
            var meshes = new List<Mesh>();

            Debug.Log($"[GltfBootstrap] Found {meshFilters.Length} MeshFilters in {filename}");
            foreach (var mf in meshFilters)
            {
                if (mf.sharedMesh != null && mf.sharedMesh.vertexCount > 0)
                {
                    // Clone the mesh so it survives temp object destruction
                    var clonedMesh = Object.Instantiate(mf.sharedMesh);
                    clonedMesh.name = mf.sharedMesh.name;
                    meshes.Add(clonedMesh);
                    Debug.Log($"[GltfBootstrap]   mesh '{clonedMesh.name}': {clonedMesh.vertexCount} verts, colors={clonedMesh.colors.Length}, bounds={clonedMesh.bounds}");
                }
                else
                {
                    Debug.LogWarning($"[GltfBootstrap]   skipped mesh (null or empty) from {mf.gameObject.name}");
                }
            }

            Destroy(tempParent);
            Debug.Log($"[GltfBootstrap] Extracted {meshes.Count} meshes from {filename}");
            return meshes.ToArray();
        }

        private string BuildModelUrl(string filename)
        {
            string basePath = Application.streamingAssetsPath;

            if (basePath.StartsWith("http", System.StringComparison.OrdinalIgnoreCase)
                || basePath.StartsWith("jar:", System.StringComparison.OrdinalIgnoreCase))
            {
                return basePath + "/Models/" + filename;
            }
            else
            {
                // Check standard StreamingAssets first
                string localPath = Path.Combine(basePath, "Models", filename);
                if (System.IO.File.Exists(localPath))
                    return new System.Uri(localPath).AbsoluteUri;

                // Fallback: check fantasy-game subdirectory (nested project structure)
                string altPath = Path.Combine(Application.dataPath, "fantasy-game", "Assets",
                    "StreamingAssets", "Models", filename);
                if (System.IO.File.Exists(altPath))
                {
                    Debug.Log($"[GltfBootstrap] Using alt path for {filename}: {altPath}");
                    return new System.Uri(altPath).AbsoluteUri;
                }

                // Return standard path even if file doesn't exist (will fail gracefully)
                return new System.Uri(localPath).AbsoluteUri;
            }
        }

        // ===================================================================
        // Material creation
        // ===================================================================

        private Material CreatePainterlyMaterial(string name, Color tint)
        {
            var shader = Shader.Find("FantasyGame/PainterlyLit");
            if (shader == null)
            {
                Debug.LogWarning("[GltfBootstrap] PainterlyLit shader not found, using fallback.");
                shader = FindAvailableShader();
            }

            var mat = new Material(shader);
            mat.name = name;
            mat.SetColor("_BaseColor", tint);
            mat.SetColor("_ShadowColor", new Color(0.3f, 0.25f, 0.45f));
            mat.SetFloat("_BandCount", 3f);
            mat.SetFloat("_RimPower", 3f);
            mat.SetFloat("_RimStrength", 0.4f);
            mat.SetColor("_RimColor", new Color(1f, 0.9f, 0.7f));
            mat.enableInstancing = true;
            return mat;
        }

        private Material CreateGrassMaterial()
        {
            var shader = Shader.Find("FantasyGame/PainterlyGrass");
            if (shader == null)
            {
                Debug.LogWarning("[GltfBootstrap] PainterlyGrass shader not found, using fallback.");
                shader = FindAvailableShader();
            }

            var mat = new Material(shader);
            mat.name = "GrassMat";
            mat.SetColor("_BaseColor", new Color(0.28f, 0.45f, 0.18f));
            mat.SetColor("_TipColor", new Color(0.48f, 0.60f, 0.22f));
            mat.enableInstancing = true;
            return mat;
        }

        // ===================================================================
        // Character setup (same as before)
        // ===================================================================

        private void SetupCharacterController(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>();
            Bounds bounds = new Bounds(root.transform.position, Vector3.zero);
            foreach (var r in renderers)
                bounds.Encapsulate(r.bounds);

            float height = bounds.size.y;
            float radius = Mathf.Max(bounds.size.x, bounds.size.z) * 0.3f;

            if (height < 0.01f) height = 1.8f;
            if (radius < 0.01f) radius = 0.3f;

            // Compute center relative to root transform — this accounts for
            // models whose pivot isn't at their feet
            Vector3 localCenter = root.transform.InverseTransformPoint(bounds.center);
            // Adjust so the CC bottom sits at the model's lowest point
            float bottomY = localCenter.y - height * 0.5f;
            float centerY = bottomY + height * 0.5f;

            var cc = root.AddComponent<CharacterController>();
            cc.height = height;
            cc.radius = radius;
            cc.center = new Vector3(0, centerY, 0);
            cc.skinWidth = 0.08f;
            cc.minMoveDistance = 0f;
            cc.slopeLimit = 45f;
            cc.stepOffset = Mathf.Min(0.4f, height * 0.3f);

            Debug.Log($"[GltfBootstrap] CC: height={height:F2}, radius={radius:F2}, center.y={centerY:F2}, bottomY={bottomY:F2}");

            var controller = root.AddComponent<Player.ThirdPersonController>();
            controller.Init(cc, height);
        }

        private void SetupAnimation(GameObject root)
        {
            var anim = root.GetComponentInChildren<Animation>();
            if (anim == null)
            {
                Debug.LogWarning("[GltfBootstrap] No Animation component found on imported model.");
                return;
            }

            var controller = root.GetComponent<Player.ThirdPersonController>();
            if (controller == null) return;

            var characterAnimator = root.AddComponent<Player.CharacterAnimator>();
            characterAnimator.Init(anim, controller);
            Debug.Log($"[GltfBootstrap] Animation setup complete. Clips: {anim.GetClipCount()}");
        }

        private void SetupCamera(Transform target)
        {
            UnityEngine.Camera cam = UnityEngine.Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera");
                camGo.tag = "MainCamera";
                cam = camGo.AddComponent<UnityEngine.Camera>();
                camGo.AddComponent<AudioListener>();
            }

            // Camera background: don't clear to solid color, sky dome handles it
            cam.clearFlags = CameraClearFlags.Skybox;

            var existing = cam.GetComponent<FantasyGame.Camera.ThirdPersonCamera>();
            if (existing != null) Destroy(existing);

            var follow = cam.gameObject.AddComponent<FantasyGame.Camera.ThirdPersonCamera>();
            follow.Init(target);
        }

        private void SetupRPGSystems(GameObject characterRoot)
        {
            // --- Stats ---
            var statsComp = characterRoot.AddComponent<PlayerStatsComponent>();
            statsComp.Init();

            // --- Inventory ---
            var inventoryComp = characterRoot.AddComponent<InventoryComponent>();
            inventoryComp.Init();

            // --- Sword visual (attach to right hand bone) ---
            Transform swordMount = AttachSwordToHand(characterRoot);

            // --- Combat ---
            var combat = characterRoot.AddComponent<MeleeCombat>();
            combat.Init(statsComp, swordMount);

            // Tell CharacterAnimator about combat
            var charAnim = characterRoot.GetComponent<Player.CharacterAnimator>();
            if (charAnim != null)
                charAnim.SetCombat(combat);

            // --- HUD ---
            var hudGo = new GameObject("GameHUD");
            var hud = hudGo.AddComponent<GameHUD>();
            hud.Init(statsComp.Stats);

            // --- Inventory UI (Tab to toggle) ---
            var invUIGo = new GameObject("InventoryUI");
            var invUI = invUIGo.AddComponent<InventoryUI>();
            invUI.Init(inventoryComp.Inventory, statsComp.Stats, characterRoot.transform);

            // --- Stat point input (1=STR, 2=DEX, 3=VIT) ---
            var statInput = characterRoot.AddComponent<StatPointInput>();
            statInput.Init(statsComp.Stats);

            // --- Potion input (Q to use potion) ---
            var potionInput = characterRoot.AddComponent<PotionInput>();
            potionInput.Init(inventoryComp.Inventory, statsComp.Stats);

            Debug.Log("[GltfBootstrap] RPG systems initialized: Stats, Inventory, Combat, HUD, InventoryUI");
        }

        private Transform AttachSwordToHand(GameObject characterRoot)
        {
            // Try to find the right hand bone in the hierarchy
            Transform handBone = FindBoneRecursive(characterRoot.transform, "Hand_R");
            if (handBone == null)
                handBone = FindBoneRecursive(characterRoot.transform, "hand_R");
            if (handBone == null)
                handBone = FindBoneRecursive(characterRoot.transform, "HandR");

            var swordGo = new GameObject("Sword");

            if (_swordMesh != null)
            {
                var mf = swordGo.AddComponent<MeshFilter>();
                mf.sharedMesh = _swordMesh;
                var mr = swordGo.AddComponent<MeshRenderer>();
                mr.sharedMaterial = CreatePainterlyMaterial("SwordMat", Color.white);
            }
            else
            {
                // Fallback: simple cube sword
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.transform.SetParent(swordGo.transform);
                cube.transform.localScale = new Vector3(0.05f, 0.8f, 0.05f);
                cube.transform.localPosition = new Vector3(0, 0.4f, 0);
                var collider = cube.GetComponent<Collider>();
                if (collider != null) Destroy(collider);
            }

            if (handBone != null)
            {
                swordGo.transform.SetParent(handBone);
                swordGo.transform.localPosition = Vector3.zero;
                swordGo.transform.localRotation = Quaternion.identity;
                swordGo.transform.localScale = Vector3.one;
                Debug.Log($"[GltfBootstrap] Sword attached to bone: {handBone.name}");
            }
            else
            {
                // Fallback: attach to character root offset to the right
                swordGo.transform.SetParent(characterRoot.transform);
                swordGo.transform.localPosition = new Vector3(0.5f, 1.0f, 0.3f);
                Debug.LogWarning("[GltfBootstrap] Hand_R bone not found, sword attached to root.");
            }

            return swordGo.transform;
        }

        private Transform FindBoneRecursive(Transform parent, string boneName)
        {
            if (parent.name == boneName) return parent;
            for (int i = 0; i < parent.childCount; i++)
            {
                var found = FindBoneRecursive(parent.GetChild(i), boneName);
                if (found != null) return found;
            }
            return null;
        }

        private void SetupEnemySpawner(Transform player)
        {
            var spawnerGo = new GameObject("EnemySpawner");
            _enemySpawner = spawnerGo.AddComponent<EnemySpawner>();
            _enemySpawner.Init(player);

            // Assign meshes if loaded
            if (_slimeMesh != null)
                _enemySpawner.SetEnemyMesh("Slime", _slimeMesh);
            if (_skeletonMesh != null)
                _enemySpawner.SetEnemyMesh("Skeleton", _skeletonMesh);
            if (_wolfMesh != null)
                _enemySpawner.SetEnemyMesh("Wolf", _wolfMesh);

            Debug.Log("[GltfBootstrap] Enemy spawner initialized.");
        }

        private void SetupWorldInteraction(GameObject characterRoot)
        {
            // --- Quest Manager ---
            var questGo = new GameObject("QuestManager");
            var questMgr = questGo.AddComponent<QuestManager>();
            var statsComp = characterRoot.GetComponent<PlayerStatsComponent>();
            questMgr.Init(statsComp != null ? statsComp.Stats : null);

            // --- Day/Night Cycle ---
            var dayNightGo = new GameObject("DayNightCycle");
            var dayNight = dayNightGo.AddComponent<DayNightCycle>();
            dayNight.Init();

            // --- World Object Spawner (NPCs, chests, campfires, crates) ---
            var spawnerGo = new GameObject("WorldObjectSpawner");
            var worldSpawner = spawnerGo.AddComponent<WorldObjectSpawner>();
            worldSpawner.Init(characterRoot.transform, questMgr, WORLD_SEED, _npcMeshes, _propMeshes, _itemMeshes, _buildingMeshes);

            // --- Dungeon Manager ---
            var dungeonGo = new GameObject("DungeonManager");
            var dungeonMgr = dungeonGo.AddComponent<DungeonManager>();
            dungeonMgr.Init(characterRoot.transform, questMgr, WORLD_SEED,
                _dungeonMeshes);

            Debug.Log("[GltfBootstrap] Phase 4: World Interaction initialized (quests, NPCs, chests, day/night, dungeon).");
        }

        private void SetupPolishSystems(GameObject characterRoot)
        {
            // --- Sound Manager ---
            var soundGo = new GameObject("SoundManager");
            var soundMgr = soundGo.AddComponent<SoundManager>();
            soundMgr.Init(characterRoot.transform);

            // --- Particle Effect Manager ---
            var vfxGo = new GameObject("ParticleEffectManager");
            var vfxMgr = vfxGo.AddComponent<ParticleEffectManager>();
            vfxMgr.Init();

            // --- Minimap ---
            var minimapGo = new GameObject("MinimapHUD");
            var minimap = minimapGo.AddComponent<MinimapHUD>();
            minimap.Init(characterRoot.transform);

            // --- Pause Menu ---
            var pauseGo = new GameObject("PauseMenu");
            var pauseMenu = pauseGo.AddComponent<PauseMenu>();
            pauseMenu.Init();

            // --- Water ---
            var waterGo = new GameObject("WaterRenderer");
            var water = waterGo.AddComponent<WaterRenderer>();
            float waterLevel = _worldManager.GetTerrainHeight(0, 0) - 1.5f;
            water.Init(characterRoot.transform, _worldManager.Terrain, waterLevel);

            // --- Wire up combat events to sound + VFX ---
            var combat = characterRoot.GetComponent<MeleeCombat>();
            if (combat != null)
            {
                combat.OnAttackStart += () => soundMgr.PlaySwordSwing();
                combat.OnAttackHit += () =>
                {
                    soundMgr.PlaySwordHit();
                    // VFX spawned from enemy hit handler
                };
            }

            // --- Wire up stats events to sound ---
            var statsComp = characterRoot.GetComponent<PlayerStatsComponent>();
            if (statsComp != null)
            {
                statsComp.Stats.OnLevelUp += (lvl) => soundMgr.PlayLevelUp();
                statsComp.Stats.OnHealthChanged += (cur, max) =>
                {
                    if (cur < max) soundMgr.PlayPlayerHurt();
                };
                statsComp.Stats.OnDeath += () => soundMgr.PlayPlayerDeath();
            }

            Debug.Log("[GltfBootstrap] Phase 5: Polish systems initialized (audio, VFX, minimap, pause, water).");
        }

        private void SnapToGround(GameObject character)
        {
            // Raycast down from high above to find the actual terrain surface
            Vector3 origin = character.transform.position + Vector3.up * 100f;
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 500f))
            {
                var cc = character.GetComponent<CharacterController>();
                if (cc != null)
                {
                    // Disable CC briefly to teleport (CharacterController blocks transform.position)
                    cc.enabled = false;
                    character.transform.position = hit.point + Vector3.up * 0.02f;
                    cc.enabled = true;
                    // Push CC down so isGrounded registers immediately
                    cc.Move(Vector3.down * 0.1f);
                    Debug.Log($"[GltfBootstrap] Snapped character to ground at Y={hit.point.y:F1}, isGrounded={cc.isGrounded}");
                }
                else
                {
                    character.transform.position = hit.point + Vector3.up * 0.1f;
                }
            }
            else
            {
                Debug.LogWarning("[GltfBootstrap] Raycast couldn't find ground — character may fall.");
            }
        }

        private void CreateDebugCube(Transform parent)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "DebugCube";
            cube.transform.SetParent(parent);
            cube.transform.localPosition = new Vector3(0, 1f, 0);
            var shader = FindAvailableShader();
            if (shader != null)
            {
                var mat = new Material(shader);
                mat.color = Color.red;
                cube.GetComponent<Renderer>().material = mat;
            }
        }

        private static Shader FindAvailableShader()
        {
            string[] shaderNames = {
                "FantasyGame/PainterlyLit",
                "Universal Render Pipeline/Lit",
                "Universal Render Pipeline/Simple Lit",
                "Universal Render Pipeline/Unlit",
                "Standard",
                "Unlit/Color"
            };
            foreach (var name in shaderNames)
            {
                var s = Shader.Find(name);
                if (s != null) return s;
            }
            return null;
        }
    }
}
