// Assets/Scripts/Interaction/WorldObjectSpawner.cs
// ==================================================
// Spawns interactable objects around the world: chests, crates,
// barrels, campfires, and NPCs at predefined or procedural locations.
// Uses Blender-generated meshes when available, falls back to primitives.

using UnityEngine;
using FantasyGame.RPG;
using FantasyGame.Utils;

namespace FantasyGame.Interaction
{
    public class WorldObjectSpawner : MonoBehaviour
    {
        private Transform _player;
        private QuestManager _questManager;
        private int _terrainSeed;

        // Blender meshes: npcs[0]=Villager, [1]=Blacksmith, [2]=Merchant, [3]=Scout
        private Mesh[] _npcMeshes;
        // Props: [0]=Campfire, [1]=Chest, [2]=Crate, [3]=Barrel, [4]=TrainingDummy, [5]=Signpost
        private Mesh[] _propMeshes;
        // Items: [0]=Potion, [1]=Shield, [2]=Axe, [3]=Helmet, [4]=Bone, [5]=Pelt
        private Mesh[] _itemMeshes;

        private Material _vcMaterial; // Shared vertex-color material

        public void Init(Transform player, QuestManager questManager, int seed = 12345,
            Mesh[] npcMeshes = null, Mesh[] propMeshes = null, Mesh[] itemMeshes = null)
        {
            _player = player;
            _questManager = questManager;
            _terrainSeed = seed;
            _npcMeshes = npcMeshes ?? new Mesh[0];
            _propMeshes = propMeshes ?? new Mesh[0];
            _itemMeshes = itemMeshes ?? new Mesh[0];

            // Create a shared vertex-color material (PainterlyLit or fallback)
            var shader = Shader.Find("FantasyGame/PainterlyLit")
                ?? Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Standard");
            _vcMaterial = new Material(shader);
            _vcMaterial.SetColor("_BaseColor", Color.white);
            _vcMaterial.enableInstancing = true;

            Debug.Log($"[WorldObjectSpawner] Shader={shader?.name ?? "NULL"}, Meshes: npcs={_npcMeshes.Length}, props={_propMeshes.Length}, items={_itemMeshes.Length}");

            SpawnFixedObjects();
        }

        private void SpawnFixedObjects()
        {
            // --- NPC outside village (south road) ---
            SpawnNPC(
                new Vector3(80f, 0, 30f),
                "Elder Oakbeard",
                new DialogueLine[]
                {
                    new DialogueLine("Elder Oakbeard", "Welcome, adventurer! These lands have become dangerous."),
                    new DialogueLine("Elder Oakbeard", "Slimes have overrun the lowlands. Could you help clear them out?"),
                    new DialogueLine("Elder Oakbeard", "Defeat 5 Slimes and return to me for a reward."),
                },
                new DialogueLine[]
                {
                    new DialogueLine("Elder Oakbeard", "Well done! The lowlands are safer now."),
                    new DialogueLine("Elder Oakbeard", "Take these potions as thanks."),
                },
                "kill_slimes",
                0 // Villager mesh
            );

            // --- Second NPC (west trail) ---
            SpawnNPC(
                new Vector3(30f, 0, 80f),
                "Scout Fern",
                new DialogueLine[]
                {
                    new DialogueLine("Scout Fern", "I've been tracking wolf packs in these hills."),
                    new DialogueLine("Scout Fern", "Bring me 3 Wolf Pelts and I'll trade you something special."),
                },
                new DialogueLine[]
                {
                    new DialogueLine("Scout Fern", "Impressive! You're a true hunter."),
                    new DialogueLine("Scout Fern", "This enchanted blade is yours. Use it well!"),
                },
                "collect_pelts",
                3 // Scout mesh
            );

            // --- Campfires ---
            SpawnCampfire(new Vector3(75f, 0, 35f));
            SpawnCampfire(new Vector3(130f, 0, 85f));

            // --- Treasure chests scattered around the region ---
            SpawnChest(new Vector3(50f, 0, 60f));
            SpawnChest(new Vector3(120f, 0, 40f));
            SpawnChest(new Vector3(40f, 0, 120f));

            // --- Breakable crates along south road ---
            SpawnBreakable(new Vector3(82f, 0, 38f), "Crate", "potion_small", 0.5f);
            SpawnBreakable(new Vector3(83f, 0, 39f), "Crate", "potion_small", 0.3f);
            SpawnBreakable(new Vector3(81f, 0, 37f), "Barrel", "potion_small", 0.4f);

            // --- More breakables further out ---
            SpawnBreakable(new Vector3(110f, 0, 70f), "Crate", "potion_large", 0.3f);
            SpawnBreakable(new Vector3(45f, 0, 100f), "Barrel", "bone_fragment", 0.4f);

            // ============================================
            // VILLAGE PLATEAU (flat area at 80, 80)
            // ============================================

            // Village NPC: Blacksmith
            SpawnNPC(
                new Vector3(80f, 0, 80f),
                "Blacksmith Grond",
                new DialogueLine[]
                {
                    new DialogueLine("Blacksmith Grond", "Welcome to the plateau outpost, traveler."),
                    new DialogueLine("Blacksmith Grond", "Skeletons have been raiding our supplies from the highlands."),
                    new DialogueLine("Blacksmith Grond", "Defeat 3 of them and I'll forge you a fine blade."),
                },
                new DialogueLine[]
                {
                    new DialogueLine("Blacksmith Grond", "Well done! The highlands are quieter now."),
                    new DialogueLine("Blacksmith Grond", "As promised, here's your iron blade. Swing it well!"),
                },
                "kill_skeletons",
                1 // Blacksmith mesh
            );

            // Village NPC: Merchant (flavor, no quest)
            SpawnNPC(
                new Vector3(83f, 0, 84f),
                "Merchant Thalia",
                new DialogueLine[]
                {
                    new DialogueLine("Merchant Thalia", "Business has been slow since the wolves moved in nearby."),
                    new DialogueLine("Merchant Thalia", "If you happen to gather wolf pelts, I know a scout near the spawn who trades for them."),
                },
                new DialogueLine[]
                {
                    new DialogueLine("Merchant Thalia", "Thank you for making the roads safer."),
                },
                "", // No quest
                2 // Merchant mesh
            );

            // Village campfire (central gathering point)
            SpawnCampfire(new Vector3(78f, 0, 82f));

            // Village treasure chest
            SpawnChest(new Vector3(85f, 0, 76f));

            // Village supply crates/barrels
            SpawnBreakable(new Vector3(75f, 0, 78f), "Crate", "potion_large", 0.5f);
            SpawnBreakable(new Vector3(76f, 0, 79f), "Barrel", "potion_small", 0.4f);
            SpawnBreakable(new Vector3(74f, 0, 77f), "Crate", "potion_small", 0.3f);

            // Training dummies
            SpawnTrainingDummy(new Vector3(72f, 0, 83f));
            SpawnTrainingDummy(new Vector3(73f, 0, 85f));

            Debug.Log("[WorldObjectSpawner] Fixed world objects placed (including village plateau).");
        }

        // =================================================================
        // Mesh helper: attach a Blender mesh or fall back to primitive
        // =================================================================
        private bool AttachMesh(GameObject parent, Mesh mesh, Vector3 localPos, Vector3 localScale)
        {
            if (mesh == null)
            {
                Debug.LogWarning($"[WorldObjectSpawner] AttachMesh: null mesh for {parent.name}");
                return false;
            }

            var meshGo = new GameObject("Mesh");
            meshGo.transform.SetParent(parent.transform);
            meshGo.transform.localPosition = localPos;
            meshGo.transform.localScale = localScale;

            var mf = meshGo.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = meshGo.AddComponent<MeshRenderer>();
            mr.sharedMaterial = new Material(_vcMaterial);

            Debug.Log($"[WorldObjectSpawner] AttachMesh OK: {parent.name} -> {mesh.name} ({mesh.vertexCount} verts, bounds={mesh.bounds})");
            return true;
        }

        private Mesh GetMesh(Mesh[] arr, int index)
        {
            if (arr != null && index >= 0 && index < arr.Length)
                return arr[index];
            return null;
        }

        // =================================================================
        // NPC
        // =================================================================
        private void SpawnNPC(Vector3 pos, string name, DialogueLine[] lines,
            DialogueLine[] completeLines, string questId, int meshIndex = -1)
        {
            pos = SnapToTerrain(pos);

            var npcGo = new GameObject($"NPC_{name}");
            npcGo.transform.position = pos;

            Mesh npcMesh = GetMesh(_npcMeshes, meshIndex);
            if (!AttachMesh(npcGo, npcMesh, Vector3.zero, Vector3.one))
            {
                // Fallback: capsule body + sphere head
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

                var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                body.transform.SetParent(npcGo.transform);
                body.transform.localPosition = new Vector3(0, 0.75f, 0);
                body.transform.localScale = new Vector3(0.4f, 0.5f, 0.4f);
                var bodyCol = body.GetComponent<Collider>();
                if (bodyCol != null) Object.Destroy(bodyCol);

                var bodyRenderer = body.GetComponent<Renderer>();
                if (bodyRenderer != null)
                {
                    var mat = new Material(shader);
                    if (name.Contains("Elder"))
                        mat.color = new Color(0.3f, 0.5f, 0.25f);
                    else if (name.Contains("Blacksmith"))
                        mat.color = new Color(0.25f, 0.25f, 0.3f);
                    else if (name.Contains("Merchant"))
                        mat.color = new Color(0.6f, 0.3f, 0.5f);
                    else
                        mat.color = new Color(0.5f, 0.35f, 0.25f);
                    bodyRenderer.material = mat;
                }

                var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                head.transform.SetParent(npcGo.transform);
                head.transform.localPosition = new Vector3(0, 1.5f, 0);
                head.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
                var headCol = head.GetComponent<Collider>();
                if (headCol != null) Object.Destroy(headCol);

                var headRenderer = head.GetComponent<Renderer>();
                if (headRenderer != null)
                {
                    var mat = new Material(shader);
                    mat.color = new Color(0.85f, 0.7f, 0.6f);
                    headRenderer.material = mat;
                }
            }

            var dialogue = npcGo.AddComponent<DialogueNPC>();
            dialogue.Init(name, lines, completeLines, questId);
        }

        // =================================================================
        // Campfire
        // =================================================================
        private void SpawnCampfire(Vector3 pos)
        {
            pos = SnapToTerrain(pos);

            var fireGo = new GameObject("Campfire");
            fireGo.transform.position = pos;

            Mesh campfireMesh = GetMesh(_propMeshes, 0);
            // Campfire model is ~0.11m tall, ~0.83m wide — scale up 3x for game
            if (!AttachMesh(fireGo, campfireMesh, Vector3.zero, Vector3.one * 3f))
            {
                // Fallback: primitive logs + stones
                for (int i = 0; i < 3; i++)
                {
                    var log = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    log.transform.SetParent(fireGo.transform);
                    float angle = i * 60f;
                    log.transform.localPosition = new Vector3(
                        Mathf.Cos(angle * Mathf.Deg2Rad) * 0.15f, 0.05f,
                        Mathf.Sin(angle * Mathf.Deg2Rad) * 0.15f
                    );
                    log.transform.localScale = new Vector3(0.08f, 0.15f, 0.08f);
                    log.transform.localRotation = Quaternion.Euler(0, angle, 75f);
                    var logCol = log.GetComponent<Collider>();
                    if (logCol != null) Object.Destroy(logCol);
                    var logRenderer = log.GetComponent<Renderer>();
                    if (logRenderer != null)
                    {
                        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                        mat.color = new Color(0.35f, 0.2f, 0.1f);
                        logRenderer.material = mat;
                    }
                }

                for (int i = 0; i < 6; i++)
                {
                    var stone = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    stone.transform.SetParent(fireGo.transform);
                    float angle = i * 60f * Mathf.Deg2Rad;
                    stone.transform.localPosition = new Vector3(
                        Mathf.Cos(angle) * 0.35f, 0.05f,
                        Mathf.Sin(angle) * 0.35f
                    );
                    stone.transform.localScale = new Vector3(0.12f, 0.08f, 0.12f);
                    var stoneCol = stone.GetComponent<Collider>();
                    if (stoneCol != null) Object.Destroy(stoneCol);
                    var stoneRenderer = stone.GetComponent<Renderer>();
                    if (stoneRenderer != null)
                    {
                        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                        mat.color = new Color(0.4f, 0.38f, 0.35f);
                        stoneRenderer.material = mat;
                    }
                }
            }

            var campfire = fireGo.AddComponent<Campfire>();
            campfire.Init();

            var pointLight = fireGo.AddComponent<Light>();
            pointLight.type = LightType.Point;
            pointLight.color = new Color(1f, 0.7f, 0.3f);
            pointLight.intensity = 2f;
            pointLight.range = 8f;
            pointLight.transform.localPosition = Vector3.up * 0.5f;
        }

        // =================================================================
        // Treasure Chest
        // =================================================================
        private void SpawnChest(Vector3 pos)
        {
            pos = SnapToTerrain(pos);

            var chestGo = new GameObject("TreasureChest");
            chestGo.transform.position = pos;

            // The chest needs a lid pivot for the open animation regardless of mesh
            var lidPivot = new GameObject("LidPivot");
            lidPivot.transform.SetParent(chestGo.transform);
            lidPivot.transform.localPosition = new Vector3(0, 0.35f, -0.2f);

            Mesh chestMesh = GetMesh(_propMeshes, 1);
            if (!AttachMesh(chestGo, chestMesh, Vector3.zero, Vector3.one))
            {
                // Fallback: primitive cubes
                var baseBox = GameObject.CreatePrimitive(PrimitiveType.Cube);
                baseBox.transform.SetParent(chestGo.transform);
                baseBox.transform.localPosition = new Vector3(0, 0.2f, 0);
                baseBox.transform.localScale = new Vector3(0.6f, 0.3f, 0.4f);
                var baseCol = baseBox.GetComponent<Collider>();
                if (baseCol != null) Object.Destroy(baseCol);
                var baseRenderer = baseBox.GetComponent<Renderer>();
                if (baseRenderer != null)
                {
                    var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                    mat.color = new Color(0.5f, 0.3f, 0.15f);
                    baseRenderer.material = mat;
                }

                var lid = GameObject.CreatePrimitive(PrimitiveType.Cube);
                lid.transform.SetParent(lidPivot.transform);
                lid.transform.localPosition = new Vector3(0, 0.08f, 0.2f);
                lid.transform.localScale = new Vector3(0.62f, 0.12f, 0.42f);
                var lidCol = lid.GetComponent<Collider>();
                if (lidCol != null) Object.Destroy(lidCol);
                var lidRenderer = lid.GetComponent<Renderer>();
                if (lidRenderer != null)
                {
                    var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                    mat.color = new Color(0.55f, 0.35f, 0.15f);
                    lidRenderer.material = mat;
                }

                var lockSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                lockSphere.transform.SetParent(chestGo.transform);
                lockSphere.transform.localPosition = new Vector3(0, 0.25f, 0.21f);
                lockSphere.transform.localScale = Vector3.one * 0.06f;
                var lockCol = lockSphere.GetComponent<Collider>();
                if (lockCol != null) Object.Destroy(lockCol);
                var lockRenderer = lockSphere.GetComponent<Renderer>();
                if (lockRenderer != null)
                {
                    var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                    mat.color = new Color(0.9f, 0.75f, 0.2f);
                    mat.SetFloat("_Metallic", 0.8f);
                    lockRenderer.material = mat;
                }
            }

            var chestCol = chestGo.AddComponent<BoxCollider>();
            chestCol.size = new Vector3(0.6f, 0.5f, 0.4f);
            chestCol.center = new Vector3(0, 0.25f, 0);

            var chest = chestGo.AddComponent<TreasureChest>();
            chest.Init(lidPivot.transform);
        }

        // =================================================================
        // Breakable (Crate / Barrel)
        // =================================================================
        private void SpawnBreakable(Vector3 pos, string type, string lootId, float lootChance)
        {
            pos = SnapToTerrain(pos);

            var breakGo = new GameObject($"Breakable_{type}");
            breakGo.transform.position = pos;

            // Props: [2]=Crate, [3]=Barrel
            int meshIndex = type == "Barrel" ? 3 : 2;
            Mesh breakMesh = GetMesh(_propMeshes, meshIndex);

            if (!AttachMesh(breakGo, breakMesh, Vector3.zero, Vector3.one))
            {
                // Fallback: primitives
                if (type == "Barrel")
                {
                    var barrel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    barrel.transform.SetParent(breakGo.transform);
                    barrel.transform.localPosition = new Vector3(0, 0.35f, 0);
                    barrel.transform.localScale = new Vector3(0.35f, 0.35f, 0.35f);
                    var bCol = barrel.GetComponent<Collider>();
                    if (bCol != null) Object.Destroy(bCol);
                    var bRenderer = barrel.GetComponent<Renderer>();
                    if (bRenderer != null)
                    {
                        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                        mat.color = new Color(0.45f, 0.3f, 0.15f);
                        bRenderer.material = mat;
                    }
                }
                else
                {
                    var crate = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    crate.transform.SetParent(breakGo.transform);
                    crate.transform.localPosition = new Vector3(0, 0.3f, 0);
                    crate.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
                    var cCol = crate.GetComponent<Collider>();
                    if (cCol != null) Object.Destroy(cCol);
                    var cRenderer = crate.GetComponent<Renderer>();
                    if (cRenderer != null)
                    {
                        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                        mat.color = new Color(0.55f, 0.4f, 0.2f);
                        cRenderer.material = mat;
                    }
                }
            }

            var breakable = breakGo.AddComponent<BreakableObject>();
            breakable.ObjectName = type;
            breakable.Init(10, lootId, lootChance);
        }

        // =================================================================
        // Training Dummy
        // =================================================================
        private void SpawnTrainingDummy(Vector3 pos)
        {
            pos = SnapToTerrain(pos);

            var dummyGo = new GameObject("TrainingDummy");
            dummyGo.transform.position = pos;

            Mesh dummyMesh = GetMesh(_propMeshes, 4);
            if (!AttachMesh(dummyGo, dummyMesh, Vector3.zero, Vector3.one))
            {
                // Fallback: primitives
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

                var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pole.transform.SetParent(dummyGo.transform);
                pole.transform.localPosition = new Vector3(0, 0.75f, 0);
                pole.transform.localScale = new Vector3(0.1f, 0.75f, 0.1f);
                var poleCol = pole.GetComponent<Collider>();
                if (poleCol != null) Object.Destroy(poleCol);
                var poleRenderer = pole.GetComponent<Renderer>();
                if (poleRenderer != null)
                {
                    var mat = new Material(shader);
                    mat.color = new Color(0.45f, 0.30f, 0.15f);
                    poleRenderer.material = mat;
                }

                var crossbar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                crossbar.transform.SetParent(dummyGo.transform);
                crossbar.transform.localPosition = new Vector3(0, 1.2f, 0);
                crossbar.transform.localScale = new Vector3(0.08f, 0.35f, 0.08f);
                crossbar.transform.localRotation = Quaternion.Euler(0, 0, 90f);
                var crossCol = crossbar.GetComponent<Collider>();
                if (crossCol != null) Object.Destroy(crossCol);
                var crossRenderer = crossbar.GetComponent<Renderer>();
                if (crossRenderer != null)
                {
                    var mat = new Material(shader);
                    mat.color = new Color(0.45f, 0.30f, 0.15f);
                    crossRenderer.material = mat;
                }

                var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                head.transform.SetParent(dummyGo.transform);
                head.transform.localPosition = new Vector3(0, 1.7f, 0);
                head.transform.localScale = new Vector3(0.25f, 0.25f, 0.25f);
                var headCol = head.GetComponent<Collider>();
                if (headCol != null) Object.Destroy(headCol);
                var headRenderer = head.GetComponent<Renderer>();
                if (headRenderer != null)
                {
                    var mat = new Material(shader);
                    mat.color = new Color(0.7f, 0.6f, 0.45f);
                    headRenderer.material = mat;
                }
            }
        }

        // =================================================================
        // Terrain snapping
        // =================================================================
        private Vector3 SnapToTerrain(Vector3 pos)
        {
            // Use NoiseUtils directly — raycasts are unreliable during init
            // because MeshColliders may not be fully baked yet
            float height = NoiseUtils.SampleHeight(pos.x, pos.z, _terrainSeed);
            return new Vector3(pos.x, height + 0.02f, pos.z);
        }
    }
}
