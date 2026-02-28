// Assets/Scripts/Dungeon/DungeonManager.cs
// ===========================================
// Core dungeon orchestrator. Generates rooms at the cave entrance location,
// manages lighting, spawns enemies/torches/chest/boss/exit, and handles
// enter/exit transitions. Rooms are built below terrain surface extending
// in -Z from the entrance. A terrain hole is carved for the full dungeon
// length so the player can walk down a ramp into the underground rooms.

using System.Collections.Generic;
using UnityEngine;
using FantasyGame.Enemies;
using FantasyGame.RPG;
using FantasyGame.Interaction;
using FantasyGame.Utils;
using FantasyGame.World;

namespace FantasyGame.Dungeon
{
    public class DungeonManager : MonoBehaviour
    {
        public static bool IsInDungeon { get; private set; }
        public static DungeonManager Instance { get; private set; }

        // Entrance location on overworld
        private const float ENTRANCE_X = 140f;
        private const float ENTRANCE_Z = 130f;

        // Room geometry
        private const float ROOM_HEIGHT = 5f;
        private const float WALL_THICKNESS = 0.5f;
        private const float CORRIDOR_WIDTH = 4f;

        // References
        private Transform _player;
        private QuestManager _questMgr;
        private int _seed;
        private Mesh[] _dungeonMeshes;  // [0]=CaveEntrance, [1]=TorchSconce, [2]=ExitPortal
        private Mesh _slimeMesh, _skeletonMesh, _wolfMesh;

        // Runtime
        private GameObject _dungeonRoot;
        private GameObject _entranceGo;
        private Vector3 _entrancePos;
        private float _entranceTerrainY;
        private List<EnemyBase> _dungeonEnemies = new List<EnemyBase>();

        // Saved overworld state
        private Vector3 _savedPlayerPos;
        private Quaternion _savedPlayerRot;
        private float _savedFogStart, _savedFogEnd;
        private Color _savedFogColor, _savedAmbient;
        private float _savedSunIntensity;
        private Light _sunLight;

        // Shared dungeon materials
        private Material _floorMat, _wallMat, _ceilingMat;
        private Material _vcMaterial;

        // Room definitions
        private struct RoomDef
        {
            public string Name;
            public float ZOffset, YOffset;
            public float Width, Depth;
            public bool DoorNorth, DoorSouth;
            public bool OpenNorth;   // No north wall at all (for ramp entry)
            public bool NoCeiling;   // Skip ceiling (for entry room under terrain)
        }

        public void Init(Transform player, QuestManager questMgr, int seed,
            Mesh[] dungeonMeshes, Mesh slimeMesh, Mesh skeletonMesh, Mesh wolfMesh)
        {
            Instance = this;
            _player = player;
            _questMgr = questMgr;
            _seed = seed;
            _dungeonMeshes = dungeonMeshes ?? new Mesh[0];
            _slimeMesh = slimeMesh;
            _skeletonMesh = skeletonMesh;
            _wolfMesh = wolfMesh;

            // Create shared materials
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            _floorMat = new Material(shader) { color = new Color(0.20f, 0.18f, 0.16f) };
            _floorMat.SetFloat("_Smoothness", 0.1f);
            _wallMat = new Material(shader) { color = new Color(0.25f, 0.22f, 0.20f) };
            _wallMat.SetFloat("_Smoothness", 0.1f);
            _ceilingMat = new Material(shader) { color = new Color(0.15f, 0.13f, 0.12f) };
            _ceilingMat.SetFloat("_Smoothness", 0.1f);

            // VC material for Blender meshes
            var vcShader = Shader.Find("FantasyGame/PainterlyLit") ?? shader;
            _vcMaterial = new Material(vcShader);
            _vcMaterial.SetColor("_BaseColor", Color.white);

            // Find the sun light
            foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.type == LightType.Directional)
                {
                    _sunLight = light;
                    break;
                }
            }

            // Get terrain height at entrance
            // Note: flat zone is registered in GltfBootstrap before terrain generation
            _entranceTerrainY = NoiseUtils.SampleHeight(ENTRANCE_X, ENTRANCE_Z, seed);

            _entrancePos = new Vector3(ENTRANCE_X, _entranceTerrainY, ENTRANCE_Z);

            // Spawn the entrance arch on the overworld (always visible)
            SpawnEntranceOnOverworld();

            // Generate dungeon rooms underground — player walks through the
            // arch and down a ramp that descends beneath the terrain.
            GenerateDungeon();

            Debug.Log("[DungeonManager] Initialized. Entrance at " +
                $"({ENTRANCE_X}, {ENTRANCE_Z}), terrainY={_entranceTerrainY:F1}. Dungeon built.");
        }

        private void SpawnEntranceOnOverworld()
        {
            _entranceGo = new GameObject("DungeonEntrance_Overworld");
            _entranceGo.transform.position = _entrancePos;

            // Attach the cave entrance arch mesh
            if (_dungeonMeshes.Length > 0 && _dungeonMeshes[0] != null)
            {
                var meshGo = new GameObject("EntranceMesh");
                meshGo.transform.SetParent(_entranceGo.transform);
                meshGo.transform.localPosition = Vector3.zero;
                meshGo.transform.localScale = Vector3.one;
                var mf = meshGo.AddComponent<MeshFilter>();
                mf.sharedMesh = _dungeonMeshes[0];
                var mr = meshGo.AddComponent<MeshRenderer>();
                mr.sharedMaterial = new Material(_vcMaterial);
                Debug.Log($"[DungeonManager] Entrance mesh attached: {_dungeonMeshes[0].vertexCount} verts");
            }
            else
            {
                // Fallback: two stone pillars + lintel
                CreateFallbackEntrance(_entranceGo);
            }

            // Flanking torches
            SpawnOverworldTorch(_entrancePos + new Vector3(-2f, 0, 0));
            SpawnOverworldTorch(_entrancePos + new Vector3(2f, 0, 0));
        }

        private void CreateFallbackEntrance(GameObject parent)
        {
            // Left pillar
            var pillarL = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pillarL.transform.SetParent(parent.transform);
            pillarL.transform.localPosition = new Vector3(-1.3f, 1.5f, 0);
            pillarL.transform.localScale = new Vector3(0.6f, 3f, 0.6f);
            pillarL.GetComponent<Renderer>().material = _wallMat;
            Object.Destroy(pillarL.GetComponent<Collider>());

            // Right pillar
            var pillarR = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pillarR.transform.SetParent(parent.transform);
            pillarR.transform.localPosition = new Vector3(1.3f, 1.5f, 0);
            pillarR.transform.localScale = new Vector3(0.6f, 3f, 0.6f);
            pillarR.GetComponent<Renderer>().material = _wallMat;
            Object.Destroy(pillarR.GetComponent<Collider>());

            // Lintel
            var lintel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lintel.transform.SetParent(parent.transform);
            lintel.transform.localPosition = new Vector3(0, 3.2f, 0);
            lintel.transform.localScale = new Vector3(3.2f, 0.5f, 0.6f);
            lintel.GetComponent<Renderer>().material = _wallMat;
            Object.Destroy(lintel.GetComponent<Collider>());
        }

        private void SpawnOverworldTorch(Vector3 pos)
        {
            var torchGo = new GameObject("EntranceTorch");
            torchGo.transform.position = pos;

            var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            post.transform.SetParent(torchGo.transform);
            post.transform.localPosition = new Vector3(0, 0.75f, 0);
            post.transform.localScale = new Vector3(0.08f, 0.75f, 0.08f);
            var pr = post.GetComponent<Renderer>();
            if (pr != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                mat.color = new Color(0.3f, 0.2f, 0.1f);
                pr.material = mat;
            }
            Object.Destroy(post.GetComponent<Collider>());

            var light = torchGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.65f, 0.3f);
            light.intensity = 1.5f;
            light.range = 6f;
            torchGo.transform.position = pos + Vector3.up * 1.5f;
        }

        // ===================================================================
        //  PROXIMITY-BASED ENTER / EXIT DETECTION
        // ===================================================================

        private void Update()
        {
            if (_player == null) return;

            if (!IsInDungeon)
            {
                // Check if player walked into the entrance arch
                float dx = Mathf.Abs(_player.position.x - ENTRANCE_X);
                float dz = Mathf.Abs(_player.position.z - ENTRANCE_Z);
                if (dx < 2f && dz < 2f)
                {
                    TeleportIntoDungeon();
                }
            }
            else
            {
                // Fall protection — if player somehow falls below the dungeon floor,
                // teleport them back to the entry room
                float dungeonFloorY = _entranceTerrainY - 20f; // rooms are at Y offset -20
                if (_player.position.y < dungeonFloorY - 5f)
                {
                    Vector3 entryRoomCenter = _entrancePos + new Vector3(0, -20f + 0.5f, -5f);
                    var cc = _player.GetComponent<CharacterController>();
                    if (cc != null) cc.enabled = false;
                    _player.position = entryRoomCenter;
                    if (cc != null) cc.enabled = true;
                    var tpc = _player.GetComponent<Player.ThirdPersonController>();
                    if (tpc != null) tpc.ResetAfterRespawn();
                    Debug.Log("[DungeonManager] Fall protection — teleported player back to entry room.");
                }
            }
        }

        private void TeleportIntoDungeon()
        {
            // Save position for exit
            _savedPlayerPos = _player.position;
            _savedPlayerRot = _player.rotation;

            // Teleport player to center of entry room (ZOffset = -5)
            Vector3 entryRoomCenter = _entrancePos + new Vector3(0, -20f + 0.5f, -5f);
            var cc = _player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            _player.position = entryRoomCenter;
            _player.rotation = Quaternion.Euler(0, 180, 0); // face south (into dungeon)
            if (cc != null) cc.enabled = true;

            SaveLighting();
            ApplyDungeonLighting();
            ToggleOverworld(false);
            _dungeonRoot.SetActive(true);
            IsInDungeon = true;

            var tpc = _player.GetComponent<Player.ThirdPersonController>();
            if (tpc != null) tpc.ResetAfterRespawn();

            Debug.Log("[DungeonManager] Teleported player into dungeon.");
        }

        public void EnterDungeon() { TeleportIntoDungeon(); }

        public void ExitDungeon()
        {
            if (!IsInDungeon) return;

            // Teleport player back to entrance surface
            var cc = _player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            _player.position = _entrancePos + Vector3.up * 0.5f + Vector3.forward * 3f;
            _player.rotation = Quaternion.Euler(0, 0, 0);
            if (cc != null) cc.enabled = true;

            ToggleOverworld(true);
            RestoreLighting();
            IsInDungeon = false;

            var tpc = _player.GetComponent<Player.ThirdPersonController>();
            if (tpc != null) tpc.ResetAfterRespawn();

            Debug.Log("[DungeonManager] Teleported player out of dungeon.");
        }

        // ===================================================================
        //  DUNGEON GENERATION
        // ===================================================================

        private void GenerateDungeon()
        {
            _dungeonRoot = new GameObject("DungeonInterior");

            // Room definitions — all at Y=-20, computed Z offsets so rooms are perfectly flush.
            // Each room's north wall touches the previous room's south wall exactly.
            float Y = -20f;
            float z = 0f; // running Z cursor (relative to entrance)

            // Entry: starts at z=0, depth 10 → center at z=-5, south wall at z=-10
            z -= 5f; // center
            var entry = new RoomDef { Name = "Entry", ZOffset = z, YOffset = Y, Width = 10, Depth = 10, DoorNorth = false, DoorSouth = true };
            z -= 5f; // past south wall

            z -= 3f; // corridor half-depth
            var corr1 = new RoomDef { Name = "Corr1", ZOffset = z, YOffset = Y, Width = CORRIDOR_WIDTH, Depth = 6, DoorNorth = true, DoorSouth = true };
            z -= 3f;

            z -= 6f;
            var combat1 = new RoomDef { Name = "Combat1", ZOffset = z, YOffset = Y, Width = 14, Depth = 12, DoorNorth = true, DoorSouth = true };
            z -= 6f;

            z -= 3f;
            var corr2 = new RoomDef { Name = "Corr2", ZOffset = z, YOffset = Y, Width = CORRIDOR_WIDTH, Depth = 6, DoorNorth = true, DoorSouth = true };
            z -= 3f;

            z -= 6f;
            var combat2 = new RoomDef { Name = "Combat2", ZOffset = z, YOffset = Y, Width = 14, Depth = 12, DoorNorth = true, DoorSouth = true };
            z -= 6f;

            z -= 3f;
            var corr3 = new RoomDef { Name = "Corr3", ZOffset = z, YOffset = Y, Width = CORRIDOR_WIDTH, Depth = 6, DoorNorth = true, DoorSouth = true };
            z -= 3f;

            z -= 5f;
            var treasure = new RoomDef { Name = "Treasure", ZOffset = z, YOffset = Y, Width = 10, Depth = 10, DoorNorth = true, DoorSouth = true };
            z -= 5f;

            z -= 3f;
            var corr4 = new RoomDef { Name = "Corr4", ZOffset = z, YOffset = Y, Width = CORRIDOR_WIDTH, Depth = 6, DoorNorth = true, DoorSouth = true };
            z -= 3f;

            z -= 8f;
            var boss = new RoomDef { Name = "Boss", ZOffset = z, YOffset = Y, Width = 16, Depth = 16, DoorNorth = true, DoorSouth = true };
            z -= 8f;

            z -= 3f;
            var exit = new RoomDef { Name = "Exit", ZOffset = z, YOffset = Y, Width = 6, Depth = 6, DoorNorth = true, DoorSouth = false };

            var rooms = new RoomDef[] { entry, corr1, combat1, corr2, combat2, corr3, treasure, corr4, boss, exit };

            // Build each room
            foreach (var room in rooms)
            {
                Vector3 center = _entrancePos + new Vector3(0, room.YOffset, room.ZOffset);
                BuildRoom(center, room.Width, room.Depth, ROOM_HEIGHT,
                    room.DoorNorth, room.DoorSouth, room.OpenNorth, room.NoCeiling);
            }

            // Build corridor connectors between each pair of adjacent rooms
            // (floor, walls, ceiling bridging any gap at the doorway boundary)
            for (int i = 0; i < rooms.Length - 1; i++)
            {
                float southZ = rooms[i].ZOffset - rooms[i].Depth * 0.5f;
                float northZ = rooms[i + 1].ZOffset + rooms[i + 1].Depth * 0.5f;
                float gapSize = Mathf.Abs(southZ - northZ);

                // Even if gap is 0, add a connector floor slab to ensure no seam
                float connZ = (southZ + northZ) * 0.5f;
                float connDepth = Mathf.Max(gapSize, 1f); // at least 1m connector
                Vector3 connCenter = _entrancePos + new Vector3(0, Y, connZ);

                // Floor
                CreateWall(connCenter + new Vector3(0, -WALL_THICKNESS * 0.5f, 0),
                    new Vector3(CORRIDOR_WIDTH + 0.5f, WALL_THICKNESS, connDepth + 0.5f), _floorMat);

                // Ceiling
                CreateWall(connCenter + new Vector3(0, ROOM_HEIGHT + WALL_THICKNESS * 0.5f, 0),
                    new Vector3(CORRIDOR_WIDTH + 0.5f, WALL_THICKNESS, connDepth + 0.5f), _ceilingMat);

                // Side walls
                for (int side = -1; side <= 1; side += 2)
                {
                    CreateWall(connCenter + new Vector3(side * (CORRIDOR_WIDTH * 0.5f + WALL_THICKNESS * 0.5f), ROOM_HEIGHT * 0.5f, 0),
                        new Vector3(WALL_THICKNESS, ROOM_HEIGHT, connDepth + 0.5f), _wallMat);
                }
            }

            // Spawn content in rooms
            SpawnEntryRoomContent(rooms[0]);
            SpawnCombatRoom1Content(rooms[2]);
            SpawnCombatRoom2Content(rooms[4]);
            SpawnTreasureRoomContent(rooms[6]);
            SpawnBossRoomContent(rooms[8]);
            SpawnExitRoomContent(rooms[9]);

            // Torches in ALL rooms — brighter dungeon
            foreach (var room in rooms)
            {
                Vector3 center = _entrancePos + new Vector3(0, room.YOffset, room.ZOffset);
                float hw = room.Width * 0.4f;

                // Two torches on opposite walls for every room
                SpawnDungeonTorch(center + new Vector3(hw, 2f, 0));
                SpawnDungeonTorch(center + new Vector3(-hw, 2f, 0));

                // Extra torches in larger rooms
                if (room.Width > 8f)
                {
                    float hd = room.Depth * 0.4f;
                    SpawnDungeonTorch(center + new Vector3(0, 2f, hd));
                    SpawnDungeonTorch(center + new Vector3(0, 2f, -hd));
                }
            }

            Physics.SyncTransforms();

            // Hide dungeon until player enters (teleport-based)
            _dungeonRoot.SetActive(false);

            Debug.Log($"[DungeonManager] Generated {rooms.Length} underground rooms.");
        }

        // ===================================================================
        //  ROOM BUILDING
        // ===================================================================

        private void BuildRoom(Vector3 center, float width, float depth, float height,
            bool doorNorth, bool doorSouth, bool openNorth = false, bool noCeiling = false)
        {
            // Floor
            CreateWall(center + new Vector3(0, -WALL_THICKNESS * 0.5f, 0),
                new Vector3(width, WALL_THICKNESS, depth), _floorMat);

            // Ceiling (skip for entry room — terrain acts as ceiling)
            if (!noCeiling)
                CreateWall(center + new Vector3(0, height + WALL_THICKNESS * 0.5f, 0),
                    new Vector3(width + 2f, WALL_THICKNESS, depth + 2f), _ceilingMat);

            // North wall (+Z) — skip entirely if openNorth
            if (openNorth)
            {
                // No north wall — wide open for ramp entrance
            }
            else if (doorNorth)
                BuildWallWithDoor(center, width, height, depth * 0.5f, true);
            else
                CreateWall(center + new Vector3(0, height * 0.5f, depth * 0.5f),
                    new Vector3(width, height, WALL_THICKNESS), _wallMat);

            // South wall (-Z)
            if (doorSouth)
                BuildWallWithDoor(center, width, height, -depth * 0.5f, false);
            else
                CreateWall(center + new Vector3(0, height * 0.5f, -depth * 0.5f),
                    new Vector3(width, height, WALL_THICKNESS), _wallMat);

            // East wall (+X)
            CreateWall(center + new Vector3(width * 0.5f, height * 0.5f, 0),
                new Vector3(WALL_THICKNESS, height, depth), _wallMat);

            // West wall (-X)
            CreateWall(center + new Vector3(-width * 0.5f, height * 0.5f, 0),
                new Vector3(WALL_THICKNESS, height, depth), _wallMat);
        }

        private void BuildWallWithDoor(Vector3 roomCenter, float roomWidth, float height,
            float zOffset, bool isNorth)
        {
            float halfWidth = roomWidth * 0.5f;
            float halfDoor = CORRIDOR_WIDTH * 0.5f;
            float doorHeight = 3.5f;

            float leftW = halfWidth - halfDoor;
            if (leftW > 0.1f)
            {
                CreateWall(
                    roomCenter + new Vector3(-halfDoor - leftW * 0.5f, height * 0.5f, zOffset),
                    new Vector3(leftW, height, WALL_THICKNESS), _wallMat);
            }

            float rightW = halfWidth - halfDoor;
            if (rightW > 0.1f)
            {
                CreateWall(
                    roomCenter + new Vector3(halfDoor + rightW * 0.5f, height * 0.5f, zOffset),
                    new Vector3(rightW, height, WALL_THICKNESS), _wallMat);
            }

            float lintelHeight = height - doorHeight;
            if (lintelHeight > 0.1f)
            {
                CreateWall(
                    roomCenter + new Vector3(0, doorHeight + lintelHeight * 0.5f, zOffset),
                    new Vector3(CORRIDOR_WIDTH, lintelHeight, WALL_THICKNESS), _wallMat);
            }
        }

        private GameObject CreateWall(Vector3 position, Vector3 scale, Material mat)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.SetParent(_dungeonRoot.transform);
            wall.transform.position = position;
            wall.transform.localScale = scale;
            wall.GetComponent<Renderer>().material = mat;
            wall.isStatic = true;
            return wall;
        }

        /// <summary>
        /// Builds a short connecting ramp between two adjacent rooms with different floor heights.
        /// </summary>
        private void BuildRamp(Vector3 topPos, Vector3 bottomPos)
        {
            Vector3 mid = (topPos + bottomPos) * 0.5f;
            Vector3 dir = bottomPos - topPos;
            float length = dir.magnitude;
            if (length < 0.1f) return; // rooms are flush, no ramp needed

            float angle = Mathf.Atan2(dir.y, -Mathf.Abs(dir.z)) * Mathf.Rad2Deg;

            // Ramp surface
            var ramp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ramp.transform.SetParent(_dungeonRoot.transform);
            ramp.transform.position = mid;
            ramp.transform.localScale = new Vector3(CORRIDOR_WIDTH, 0.3f, length);
            ramp.transform.rotation = Quaternion.Euler(-angle, 0, 0);
            ramp.GetComponent<Renderer>().material = _floorMat;

            // Side walls along ramp
            for (int side = -1; side <= 1; side += 2)
            {
                var sideWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                sideWall.transform.SetParent(_dungeonRoot.transform);
                sideWall.transform.position = mid + new Vector3(
                    side * (CORRIDOR_WIDTH * 0.5f + WALL_THICKNESS * 0.5f), 1.5f, 0);
                sideWall.transform.localScale = new Vector3(WALL_THICKNESS, 5f, length + 1f);
                sideWall.GetComponent<Renderer>().material = _wallMat;
            }

            // Ceiling over transition
            var ceil = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ceil.transform.SetParent(_dungeonRoot.transform);
            ceil.transform.position = mid + new Vector3(0, ROOM_HEIGHT + 0.25f, 0);
            ceil.transform.localScale = new Vector3(CORRIDOR_WIDTH + WALL_THICKNESS * 2f, 0.5f, length + 1f);
            ceil.GetComponent<Renderer>().material = _ceilingMat;
        }

        // ===================================================================
        //  ROOM CONTENT SPAWNING
        // ===================================================================

        private Vector3 RoomCenter(RoomDef room)
        {
            return _entrancePos + new Vector3(0, room.YOffset, room.ZOffset);
        }

        private void SpawnEntryRoomContent(RoomDef room)
        {
            Vector3 center = RoomCenter(room);
            SpawnDungeonTorch(center + new Vector3(room.Width * 0.4f, 2f, room.Depth * 0.3f));
            SpawnDungeonTorch(center + new Vector3(-room.Width * 0.4f, 2f, -room.Depth * 0.3f));
        }

        private void SpawnCombatRoom1Content(RoomDef room)
        {
            Vector3 center = RoomCenter(room);
            SpawnRoomCornerTorches(center, room.Width, room.Depth);

            SpawnDungeonEnemy(center + new Vector3(-3f, 0.5f, 0), "DungeonSlime",
                _slimeMesh, new Color(0.2f, 0.6f, 0.2f), 40, 6, 2.5f, 12f, 18f, 1.5f, 25,
                "slime_gel", 1, 0.6f, Vector3.one * 0.8f);
            SpawnDungeonEnemy(center + new Vector3(3f, 0.5f, 2f), "DungeonSlime",
                _slimeMesh, new Color(0.2f, 0.6f, 0.2f), 40, 6, 2.5f, 12f, 18f, 1.5f, 25,
                "slime_gel", 1, 0.6f, Vector3.one * 0.8f);
            SpawnDungeonEnemy(center + new Vector3(0, 0.5f, -3f), "DungeonSlime",
                _slimeMesh, new Color(0.2f, 0.6f, 0.2f), 40, 6, 2.5f, 12f, 18f, 1.5f, 25,
                "slime_gel", 1, 0.6f, Vector3.one * 0.8f);
        }

        private void SpawnCombatRoom2Content(RoomDef room)
        {
            Vector3 center = RoomCenter(room);
            SpawnRoomCornerTorches(center, room.Width, room.Depth);

            SpawnDungeonEnemy(center + new Vector3(-4f, 0.5f, 2f), "DungeonSkeleton",
                _skeletonMesh, new Color(0.85f, 0.82f, 0.75f), 80, 15, 3f, 14f, 20f, 1.2f, 50,
                "bone_fragment", 1, 0.7f, Vector3.one);
            SpawnDungeonEnemy(center + new Vector3(4f, 0.5f, -2f), "DungeonSkeleton",
                _skeletonMesh, new Color(0.85f, 0.82f, 0.75f), 80, 15, 3f, 14f, 20f, 1.2f, 50,
                "bone_fragment", 1, 0.7f, Vector3.one);
            SpawnDungeonEnemy(center + new Vector3(0, 0.5f, 0), "DungeonWolf",
                _wolfMesh, new Color(0.4f, 0.35f, 0.3f), 60, 12, 5f, 16f, 22f, 1f, 40,
                "wolf_pelt", 1, 0.6f, Vector3.one);
        }

        private void SpawnTreasureRoomContent(RoomDef room)
        {
            Vector3 center = RoomCenter(room);
            SpawnRoomCornerTorches(center, room.Width, room.Depth);
            SpawnDungeonChest(center);
        }

        private void SpawnBossRoomContent(RoomDef room)
        {
            Vector3 center = RoomCenter(room);
            SpawnRoomCornerTorches(center, room.Width, room.Depth);
            SpawnDungeonTorch(center + new Vector3(room.Width * 0.4f, 2f, 0));
            SpawnDungeonTorch(center + new Vector3(-room.Width * 0.4f, 2f, 0));

            SpawnDungeonEnemy(center + new Vector3(0, 0.5f, 0), "DungeonGuardian",
                _skeletonMesh, new Color(0.6f, 0.15f, 0.15f), 200, 20, 2f, 18f, 25f, 1.8f, 150,
                "sword_magic", 1, 1f, Vector3.one * 1.5f);
        }

        private void SpawnExitRoomContent(RoomDef room)
        {
            Vector3 center = RoomCenter(room);
            SpawnDungeonTorch(center + new Vector3(room.Width * 0.35f, 2f, 0));
            SpawnDungeonTorch(center + new Vector3(-room.Width * 0.35f, 2f, 0));
            SpawnExitPortal(center);
        }

        private void SpawnRoomCornerTorches(Vector3 center, float width, float depth)
        {
            float hw = width * 0.4f;
            float hd = depth * 0.4f;
            SpawnDungeonTorch(center + new Vector3(hw, 2f, hd));
            SpawnDungeonTorch(center + new Vector3(-hw, 2f, hd));
            SpawnDungeonTorch(center + new Vector3(hw, 2f, -hd));
            SpawnDungeonTorch(center + new Vector3(-hw, 2f, -hd));
        }

        // ===================================================================
        //  ENTITY SPAWNING
        // ===================================================================

        private void SpawnDungeonEnemy(Vector3 pos, string name, Mesh mesh, Color color,
            int hp, int dmg, float speed, float detectRange, float chaseRange,
            float attackCooldown, int xpReward, string lootId, int lootCount,
            float lootChance, Vector3 scale)
        {
            var enemyGo = new GameObject($"DEnemy_{name}_{Random.Range(0, 9999)}");
            enemyGo.transform.SetParent(_dungeonRoot.transform);
            enemyGo.transform.position = pos;

            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material mat;

            if (mesh != null)
            {
                var mf = enemyGo.AddComponent<MeshFilter>();
                mf.sharedMesh = mesh;
                var mr = enemyGo.AddComponent<MeshRenderer>();
                mat = new Material(shader) { color = color };
                mr.material = mat;
                enemyGo.transform.localScale = scale;
            }
            else
            {
                var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                capsule.transform.SetParent(enemyGo.transform);
                capsule.transform.localPosition = new Vector3(0, 0.75f, 0);
                capsule.transform.localScale = scale;
                mat = new Material(shader) { color = color };
                capsule.GetComponent<Renderer>().material = mat;
                var col = capsule.GetComponent<Collider>();
                if (col != null) Destroy(col);
            }

            var capsuleCol = enemyGo.AddComponent<CapsuleCollider>();
            capsuleCol.height = 1.6f;
            capsuleCol.radius = 0.4f;
            capsuleCol.center = new Vector3(0, 0.8f, 0);

            var enemy = enemyGo.AddComponent<EnemyBase>();
            enemy.EnemyName = name;
            enemy.MaxHealth = hp;
            enemy.AttackDamage = dmg;
            enemy.AttackRange = 2f;
            enemy.AttackCooldown = attackCooldown;
            enemy.MoveSpeed = speed;
            enemy.DetectRange = detectRange;
            enemy.ChaseRange = chaseRange;
            enemy.XPReward = xpReward;
            enemy.LootId = lootId;
            enemy.LootCount = lootCount;
            enemy.LootChance = lootChance;

            enemy.Init(mat);

            var ai = enemyGo.AddComponent<EnemyAI>();
            ai.Init(enemy);

            var hpBar = enemyGo.AddComponent<EnemyHealthBar>();
            hpBar.Init(enemy);

            _dungeonEnemies.Add(enemy);
            enemy.OnDeath += (e) => _dungeonEnemies.Remove(e);

            Debug.Log($"[DungeonManager] Spawned {name} at {pos}");
        }

        private void SpawnDungeonChest(Vector3 pos)
        {
            var chestGo = new GameObject("DungeonChest");
            chestGo.transform.SetParent(_dungeonRoot.transform);
            chestGo.transform.position = pos;

            var lidPivot = new GameObject("LidPivot");
            lidPivot.transform.SetParent(chestGo.transform);
            lidPivot.transform.localPosition = new Vector3(0, 0.35f, -0.2f);

            var baseBox = GameObject.CreatePrimitive(PrimitiveType.Cube);
            baseBox.transform.SetParent(chestGo.transform);
            baseBox.transform.localPosition = new Vector3(0, 0.2f, 0);
            baseBox.transform.localScale = new Vector3(0.8f, 0.35f, 0.5f);
            Object.Destroy(baseBox.GetComponent<Collider>());
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            baseBox.GetComponent<Renderer>().material = new Material(shader) { color = new Color(0.6f, 0.45f, 0.15f) };

            var lid = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lid.transform.SetParent(lidPivot.transform);
            lid.transform.localPosition = new Vector3(0, 0.08f, 0.2f);
            lid.transform.localScale = new Vector3(0.82f, 0.12f, 0.52f);
            Object.Destroy(lid.GetComponent<Collider>());
            lid.GetComponent<Renderer>().material = new Material(shader) { color = new Color(0.65f, 0.5f, 0.2f) };

            var col = chestGo.AddComponent<BoxCollider>();
            col.center = new Vector3(0, 0.25f, 0);
            col.size = new Vector3(0.9f, 0.5f, 0.6f);

            var chest = chestGo.AddComponent<DungeonChest>();
            chest.Init(lidPivot.transform);
        }

        private void SpawnDungeonTorch(Vector3 pos)
        {
            var torchGo = new GameObject("DungeonTorch");
            torchGo.transform.SetParent(_dungeonRoot.transform);
            torchGo.transform.position = pos;

            if (_dungeonMeshes.Length > 1 && _dungeonMeshes[1] != null)
            {
                var meshGo = new GameObject("SconceMesh");
                meshGo.transform.SetParent(torchGo.transform);
                meshGo.transform.localPosition = Vector3.zero;
                meshGo.transform.localScale = Vector3.one * 2f;
                var mf = meshGo.AddComponent<MeshFilter>();
                mf.sharedMesh = _dungeonMeshes[1];
                var mr = meshGo.AddComponent<MeshRenderer>();
                mr.sharedMaterial = new Material(_vcMaterial);
            }
            else
            {
                var cyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                cyl.transform.SetParent(torchGo.transform);
                cyl.transform.localPosition = Vector3.zero;
                cyl.transform.localScale = new Vector3(0.05f, 0.15f, 0.05f);
                Object.Destroy(cyl.GetComponent<Collider>());
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                mat.color = new Color(0.3f, 0.2f, 0.1f);
                cyl.GetComponent<Renderer>().material = mat;
            }

            var light = torchGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.7f, 0.35f);
            light.intensity = 5f;
            light.range = 18f;
            light.shadows = LightShadows.None;
        }

        private void SpawnExitPortal(Vector3 pos)
        {
            var portalGo = new GameObject("ExitPortal");
            portalGo.transform.SetParent(_dungeonRoot.transform);
            portalGo.transform.position = pos;

            if (_dungeonMeshes.Length > 2 && _dungeonMeshes[2] != null)
            {
                var meshGo = new GameObject("PortalMesh");
                meshGo.transform.SetParent(portalGo.transform);
                meshGo.transform.localPosition = Vector3.zero;
                meshGo.transform.localScale = Vector3.one;
                var mf = meshGo.AddComponent<MeshFilter>();
                mf.sharedMesh = _dungeonMeshes[2];
                var mr = meshGo.AddComponent<MeshRenderer>();
                mr.sharedMaterial = new Material(_vcMaterial);
            }
            else
            {
                var cyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                cyl.transform.SetParent(portalGo.transform);
                cyl.transform.localPosition = new Vector3(0, 1.5f, 0);
                cyl.transform.localScale = new Vector3(1.5f, 1.5f, 1.5f);
                Object.Destroy(cyl.GetComponent<Collider>());
                var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color"));
                mat.color = new Color(0.3f, 0.2f, 0.8f, 0.5f);
                cyl.GetComponent<Renderer>().material = mat;
            }

            var light = portalGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.4f, 0.3f, 0.9f);
            light.intensity = 3f;
            light.range = 8f;

            var exit = portalGo.AddComponent<DungeonExit>();
            exit.Init();
        }

        // ===================================================================
        //  LIGHTING
        // ===================================================================

        private void SaveLighting()
        {
            _savedFogStart = RenderSettings.fogStartDistance;
            _savedFogEnd = RenderSettings.fogEndDistance;
            _savedFogColor = RenderSettings.fogColor;
            _savedAmbient = RenderSettings.ambientLight;
            _savedSunIntensity = _sunLight != null ? _sunLight.intensity : 1f;
        }

        private void ApplyDungeonLighting()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 15f;
            RenderSettings.fogEndDistance = 50f;
            RenderSettings.fogColor = new Color(0.06f, 0.04f, 0.08f);
            RenderSettings.ambientLight = new Color(0.15f, 0.12f, 0.18f);
            if (_sunLight != null)
                _sunLight.intensity = 0.15f;
        }

        private void RestoreLighting()
        {
            RenderSettings.fogStartDistance = _savedFogStart;
            RenderSettings.fogEndDistance = _savedFogEnd;
            RenderSettings.fogColor = _savedFogColor;
            RenderSettings.ambientLight = _savedAmbient;
            if (_sunLight != null)
                _sunLight.intensity = _savedSunIntensity;
        }

        // ===================================================================
        //  OVERWORLD TOGGLE
        // ===================================================================

        private void ToggleOverworld(bool enabled)
        {
            var spawner = FindAnyObjectByType<EnemySpawner>();
            if (spawner != null) spawner.enabled = enabled;
            var dayNight = FindAnyObjectByType<DayNightCycle>();
            if (dayNight != null) dayNight.enabled = enabled;
        }
    }

    /// <summary>
    /// Special chest for dungeon with guaranteed good loot.
    /// </summary>
    public class DungeonChest : Interactable
    {
        private Transform _lid;
        private float _openAngle;
        private bool _opening;

        public void Init(Transform lid)
        {
            _lid = lid;
            PromptText = "Open Chest";
            InteractRange = 2.5f;
        }

        protected override void OnInteract()
        {
            MarkUsed();
            _opening = true;

            var inventory = FindAnyObjectByType<InventoryComponent>();
            if (inventory != null)
            {
                var blade = ItemDatabase.Get("sword_magic");
                if (blade != null) inventory.Inventory.AddItem(blade);

                var potion = ItemDatabase.Get("potion_large");
                if (potion != null) inventory.Inventory.AddItem(potion, 3);

                Debug.Log("[DungeonChest] Found: Enchanted Blade + 3 Large Potions!");
            }

            if (Audio.SoundManager.Instance != null)
                Audio.SoundManager.Instance.PlayChestOpen();
            if (VFX.ParticleEffectManager.Instance != null)
                VFX.ParticleEffectManager.Instance.SpawnChestSparkle(transform.position + Vector3.up * 0.3f);
        }

        protected override void Update()
        {
            base.Update();
            if (_opening && _lid != null)
            {
                _openAngle = Mathf.Lerp(_openAngle, -110f, 5f * Time.deltaTime);
                _lid.localRotation = Quaternion.Euler(_openAngle, 0, 0);
                if (_openAngle < -105f)
                    _opening = false;
            }
        }
    }
}
