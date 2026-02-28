// Assets/Scripts/Dungeon/DungeonManager.cs
// ===========================================
// Core dungeon orchestrator. Generates rooms at the cave entrance location,
// manages lighting, spawns puzzle elements/torches/chest/exit, and handles
// enter/exit transitions. Rooms are built below terrain surface. Player
// teleports into the dungeon when walking near the entrance arch.

using System.Collections.Generic;
using UnityEngine;
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

        // Runtime
        private GameObject _dungeonRoot;
        private GameObject _entranceGo;
        private Vector3 _entrancePos;
        private float _entranceTerrainY;

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
            Mesh[] dungeonMeshes)
        {
            Instance = this;
            _player = player;
            _questMgr = questMgr;
            _seed = seed;
            _dungeonMeshes = dungeonMeshes ?? new Mesh[0];

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

            // Register puzzle items in ItemDatabase
            ItemDatabase.Register(new ItemData("red_key", "Red Key", ItemType.Quest, 0,
                "An ornate red key.", new Color(0.9f, 0.2f, 0.2f)));
            ItemDatabase.Register(new ItemData("blue_key", "Blue Key", ItemType.Quest, 0,
                "A shimmering blue key.", new Color(0.3f, 0.5f, 1f)));
            ItemDatabase.Register(new ItemData("dungeon_exit_token", "Dungeon Token", ItemType.Quest, 0,
                "Proof of dungeon completion.", new Color(1f, 0.85f, 0.3f)));

            // Get terrain height at entrance
            _entranceTerrainY = NoiseUtils.SampleHeight(ENTRANCE_X, ENTRANCE_Z, seed);
            _entrancePos = new Vector3(ENTRANCE_X, _entranceTerrainY, ENTRANCE_Z);

            // Spawn the entrance arch on the overworld (always visible)
            SpawnEntranceOnOverworld();

            // Generate dungeon rooms underground
            GenerateDungeon();

            Debug.Log("[DungeonManager] Initialized. Entrance at " +
                $"({ENTRANCE_X}, {ENTRANCE_Z}), terrainY={_entranceTerrainY:F1}. Puzzle dungeon built.");
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
            float Y = -20f;
            float z = 0f; // running Z cursor (relative to entrance)

            // Entry: starts at z=0, depth 10 -> center at z=-5, south wall at z=-10
            z -= 5f;
            var entry = new RoomDef { Name = "Entry", ZOffset = z, YOffset = Y, Width = 10, Depth = 10, DoorNorth = false, DoorSouth = true };
            z -= 5f;

            z -= 3f;
            var corr1 = new RoomDef { Name = "Corr1", ZOffset = z, YOffset = Y, Width = CORRIDOR_WIDTH, Depth = 6, DoorNorth = true, DoorSouth = true };
            z -= 3f;

            z -= 6f;
            var pushPlate = new RoomDef { Name = "PushPlate", ZOffset = z, YOffset = Y, Width = 14, Depth = 12, DoorNorth = true, DoorSouth = true };
            z -= 6f;

            z -= 3f;
            var redKeyDoor = new RoomDef { Name = "RedKeyDoor", ZOffset = z, YOffset = Y, Width = CORRIDOR_WIDTH, Depth = 6, DoorNorth = true, DoorSouth = true };
            z -= 3f;

            z -= 6f;
            var switchRoom = new RoomDef { Name = "SwitchRoom", ZOffset = z, YOffset = Y, Width = 14, Depth = 12, DoorNorth = true, DoorSouth = true };
            z -= 6f;

            z -= 3f;
            var corr3 = new RoomDef { Name = "Corr3", ZOffset = z, YOffset = Y, Width = CORRIDOR_WIDTH, Depth = 6, DoorNorth = true, DoorSouth = true };
            z -= 3f;

            z -= 5f;
            var treasure = new RoomDef { Name = "Treasure", ZOffset = z, YOffset = Y, Width = 10, Depth = 10, DoorNorth = true, DoorSouth = true };
            z -= 5f;

            z -= 3f;
            var blueKeyDoor = new RoomDef { Name = "BlueKeyDoor", ZOffset = z, YOffset = Y, Width = CORRIDOR_WIDTH, Depth = 6, DoorNorth = true, DoorSouth = true };
            z -= 3f;

            z -= 8f;
            var finalChallenge = new RoomDef { Name = "FinalChallenge", ZOffset = z, YOffset = Y, Width = 16, Depth = 16, DoorNorth = true, DoorSouth = true };
            z -= 8f;

            z -= 3f;
            var exit = new RoomDef { Name = "Exit", ZOffset = z, YOffset = Y, Width = 6, Depth = 6, DoorNorth = true, DoorSouth = false };

            var rooms = new RoomDef[] { entry, corr1, pushPlate, redKeyDoor, switchRoom, corr3, treasure, blueKeyDoor, finalChallenge, exit };

            // Build each room
            foreach (var room in rooms)
            {
                Vector3 center = _entrancePos + new Vector3(0, room.YOffset, room.ZOffset);
                BuildRoom(center, room.Width, room.Depth, ROOM_HEIGHT,
                    room.DoorNorth, room.DoorSouth, room.OpenNorth, room.NoCeiling);
            }

            // Build corridor connectors between each pair of adjacent rooms
            for (int i = 0; i < rooms.Length - 1; i++)
            {
                float southZ = rooms[i].ZOffset - rooms[i].Depth * 0.5f;
                float northZ = rooms[i + 1].ZOffset + rooms[i + 1].Depth * 0.5f;
                float gapSize = Mathf.Abs(southZ - northZ);

                float connZ = (southZ + northZ) * 0.5f;
                float connDepth = Mathf.Max(gapSize, 1f);
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

            // Spawn puzzle content in rooms
            SpawnEntryRoomContent(rooms[0]);         // Tutorial pressure plate
            SpawnPushPlateRoom(rooms[2]);             // Push block + plate + red key
            SpawnRedKeyDoorContent(rooms[3]);         // Red key door
            SpawnSwitchRoom(rooms[4]);                // Switch sequence puzzle
            SpawnTreasureRoomContent(rooms[6]);       // Chest + blue key
            SpawnBlueKeyDoorContent(rooms[7]);        // Blue key door
            SpawnFinalChallenge(rooms[8]);            // 2 blocks + 2 plates + switch
            SpawnExitRoomContent(rooms[9]);           // Exit portal

            // Torches in ALL rooms
            foreach (var room in rooms)
            {
                Vector3 center = _entrancePos + new Vector3(0, room.YOffset, room.ZOffset);
                float hw = room.Width * 0.4f;

                SpawnDungeonTorch(center + new Vector3(hw, 2f, 0));
                SpawnDungeonTorch(center + new Vector3(-hw, 2f, 0));

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

            Debug.Log($"[DungeonManager] Generated {rooms.Length} puzzle dungeon rooms.");
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

            // Ceiling
            if (!noCeiling)
                CreateWall(center + new Vector3(0, height + WALL_THICKNESS * 0.5f, 0),
                    new Vector3(width + 2f, WALL_THICKNESS, depth + 2f), _ceilingMat);

            // North wall (+Z)
            if (openNorth)
            {
                // No north wall
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

        // ===================================================================
        //  PUZZLE ROOM CONTENT SPAWNING
        // ===================================================================

        private Vector3 RoomCenter(RoomDef room)
        {
            return _entrancePos + new Vector3(0, room.YOffset, room.ZOffset);
        }

        /// <summary>
        /// Creates a PuzzleDoor at the south doorway of a room.
        /// The door blocks the corridor-width opening at the south wall.
        /// </summary>
        private PuzzleDoor SpawnPuzzleDoor(RoomDef room)
        {
            Vector3 center = RoomCenter(room);
            Vector3 doorPos = center + new Vector3(0, 1.75f, -room.Depth * 0.5f);

            var doorGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            doorGo.name = $"PuzzleDoor_{room.Name}";
            doorGo.transform.SetParent(_dungeonRoot.transform);
            doorGo.transform.position = doorPos;
            doorGo.transform.localScale = new Vector3(CORRIDOR_WIDTH - 0.1f, 3.5f, 0.4f);

            var door = doorGo.AddComponent<PuzzleDoor>();
            door.Init(3.5f);
            return door;
        }

        // --- Room 0: Entry — Tutorial pressure plate ---
        private void SpawnEntryRoomContent(RoomDef room)
        {
            Vector3 center = RoomCenter(room);

            // Puzzle door at south exit
            var door = SpawnPuzzleDoor(room);

            // Pressure plate in center of room
            var plateGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plateGo.name = "PressurePlate_Tutorial";
            plateGo.transform.SetParent(_dungeonRoot.transform);
            plateGo.transform.position = center + new Vector3(0, 0.05f, 0);
            plateGo.transform.localScale = new Vector3(2f, 0.1f, 2f);

            var plate = plateGo.AddComponent<PressurePlate>();
            plate.Init(door, stayDown: true);

            // Hint sign on north wall
            SpawnHintText(center + new Vector3(0, 2.5f, room.Depth * 0.45f),
                "Step on the plate to open the way.", 0.4f);

            Debug.Log("[DungeonManager] Entry room: tutorial pressure plate spawned.");
        }

        // --- Room 2: Push Block Puzzle ---
        private void SpawnPushPlateRoom(RoomDef room)
        {
            Vector3 center = RoomCenter(room);

            // Door at south exit
            var door = SpawnPuzzleDoor(room);

            // Pressure plate on the right side of the room
            var plateGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plateGo.name = "PressurePlate_PushBlock";
            plateGo.transform.SetParent(_dungeonRoot.transform);
            plateGo.transform.position = center + new Vector3(3f, 0.05f, -2f);
            plateGo.transform.localScale = new Vector3(2f, 0.1f, 2f);

            var plate = plateGo.AddComponent<PressurePlate>();
            plate.Init(door, stayDown: false);

            // Pushable block on the left side
            var blockGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blockGo.name = "PushableBlock_1";
            blockGo.transform.SetParent(_dungeonRoot.transform);
            blockGo.transform.position = center + new Vector3(-3f, 0.6f, 1f);
            blockGo.transform.localScale = new Vector3(1.2f, 1.2f, 1.2f);
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            blockGo.GetComponent<Renderer>().material = new Material(shader) { color = new Color(0.45f, 0.4f, 0.35f) };

            var block = blockGo.AddComponent<PushableBlock>();
            block.Init();

            // Red Key on a pedestal in the back of the room
            SpawnKeyPickup(center + new Vector3(-4f, 0.8f, -4f), "red_key", "Red Key",
                new Color(0.9f, 0.2f, 0.2f));

            // Hint text
            SpawnHintText(center + new Vector3(0, 2.5f, room.Depth * 0.45f),
                "Push the block. Find the key.", 0.35f);

            Debug.Log("[DungeonManager] Push plate room: block + plate + red key spawned.");
        }

        // --- Room 3: Red Key Door ---
        private void SpawnRedKeyDoorContent(RoomDef room)
        {
            Vector3 center = RoomCenter(room);

            // Puzzle door (physical gate) blocking the corridor
            var gate = SpawnPuzzleDoor(room);

            // Key door interactable at the gate position
            var keyDoorGo = new GameObject("KeyDoor_Red");
            keyDoorGo.transform.SetParent(_dungeonRoot.transform);
            keyDoorGo.transform.position = center + new Vector3(0, 1f, 0);

            var col = keyDoorGo.AddComponent<BoxCollider>();
            col.size = new Vector3(CORRIDOR_WIDTH, 3.5f, 1f);
            col.center = new Vector3(0, 0.75f, 0);

            var keyDoor = keyDoorGo.AddComponent<KeyDoor>();
            keyDoor.Init("red_key", "Red Key", gate);

            // Decorative red markers on walls
            SpawnColoredMarker(center + new Vector3(CORRIDOR_WIDTH * 0.4f, 2f, 0), new Color(0.9f, 0.2f, 0.2f));
            SpawnColoredMarker(center + new Vector3(-CORRIDOR_WIDTH * 0.4f, 2f, 0), new Color(0.9f, 0.2f, 0.2f));

            Debug.Log("[DungeonManager] Red key door spawned.");
        }

        // --- Room 4: Switch Sequence ---
        private void SpawnSwitchRoom(RoomDef room)
        {
            Vector3 center = RoomCenter(room);

            // Door at south exit
            var door = SpawnPuzzleDoor(room);

            // Sequence puzzle manager
            var puzzleGo = new GameObject("SwitchSequencePuzzle");
            puzzleGo.transform.SetParent(_dungeonRoot.transform);
            puzzleGo.transform.position = center;
            var seqPuzzle = puzzleGo.AddComponent<SwitchSequencePuzzle>();

            // Three switches on walls — labeled I, II, III visually
            var switches = new PuzzleSwitch[3];

            // Switch I (index 0) — left wall
            switches[0] = SpawnWallSwitch(center + new Vector3(-room.Width * 0.45f, 1.5f, 2f),
                null, seqPuzzle, 0);

            // Switch II (index 1) — right wall
            switches[1] = SpawnWallSwitch(center + new Vector3(room.Width * 0.45f, 1.5f, -2f),
                null, seqPuzzle, 1);

            // Switch III (index 2) — back wall
            switches[2] = SpawnWallSwitch(center + new Vector3(0, 1.5f, -room.Depth * 0.45f),
                null, seqPuzzle, 2);

            // Correct order: III (2), I (0), II (1)
            seqPuzzle.Init(new int[] { 2, 0, 1 }, door, switches);

            // Labels near each switch
            SpawnHintText(center + new Vector3(-room.Width * 0.45f, 2.8f, 2f), "I", 0.5f);
            SpawnHintText(center + new Vector3(room.Width * 0.45f, 2.8f, -2f), "II", 0.5f);
            SpawnHintText(center + new Vector3(0, 2.8f, -room.Depth * 0.45f), "III", 0.5f);

            // Hint on north wall
            SpawnHintText(center + new Vector3(0, 2.5f, room.Depth * 0.45f),
                "The order: III - I - II", 0.4f);

            Debug.Log("[DungeonManager] Switch sequence room spawned.");
        }

        // --- Room 6: Treasure + Blue Key ---
        private void SpawnTreasureRoomContent(RoomDef room)
        {
            Vector3 center = RoomCenter(room);
            SpawnRoomCornerTorches(center, room.Width, room.Depth);

            // Dungeon chest (existing functionality)
            SpawnDungeonChest(center + new Vector3(-2f, 0, 0));

            // Blue key pickup on the other side
            SpawnKeyPickup(center + new Vector3(3f, 0.8f, 0), "blue_key", "Blue Key",
                new Color(0.3f, 0.5f, 1f));

            Debug.Log("[DungeonManager] Treasure room: chest + blue key spawned.");
        }

        // --- Room 7: Blue Key Door ---
        private void SpawnBlueKeyDoorContent(RoomDef room)
        {
            Vector3 center = RoomCenter(room);

            var gate = SpawnPuzzleDoor(room);

            var keyDoorGo = new GameObject("KeyDoor_Blue");
            keyDoorGo.transform.SetParent(_dungeonRoot.transform);
            keyDoorGo.transform.position = center + new Vector3(0, 1f, 0);

            var col = keyDoorGo.AddComponent<BoxCollider>();
            col.size = new Vector3(CORRIDOR_WIDTH, 3.5f, 1f);
            col.center = new Vector3(0, 0.75f, 0);

            var keyDoor = keyDoorGo.AddComponent<KeyDoor>();
            keyDoor.Init("blue_key", "Blue Key", gate);

            SpawnColoredMarker(center + new Vector3(CORRIDOR_WIDTH * 0.4f, 2f, 0), new Color(0.3f, 0.5f, 1f));
            SpawnColoredMarker(center + new Vector3(-CORRIDOR_WIDTH * 0.4f, 2f, 0), new Color(0.3f, 0.5f, 1f));

            Debug.Log("[DungeonManager] Blue key door spawned.");
        }

        // --- Room 8: Final Challenge — 2 push blocks + 2 plates + switch ---
        private void SpawnFinalChallenge(RoomDef room)
        {
            Vector3 center = RoomCenter(room);

            // Final door at south exit
            var finalDoor = SpawnPuzzleDoor(room);

            // Create a multi-condition tracker
            var trackerGo = new GameObject("FinalPuzzleTracker");
            trackerGo.transform.SetParent(_dungeonRoot.transform);
            trackerGo.transform.position = center;
            var tracker = trackerGo.AddComponent<FinalPuzzleTracker>();

            // Plate 1 — left side
            var plate1Go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plate1Go.name = "FinalPlate_1";
            plate1Go.transform.SetParent(_dungeonRoot.transform);
            plate1Go.transform.position = center + new Vector3(-4f, 0.05f, -3f);
            plate1Go.transform.localScale = new Vector3(2f, 0.1f, 2f);
            var plate1 = plate1Go.AddComponent<PressurePlate>();
            plate1.Init(null, stayDown: false); // No direct door link — tracker manages

            // Plate 2 — right side
            var plate2Go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plate2Go.name = "FinalPlate_2";
            plate2Go.transform.SetParent(_dungeonRoot.transform);
            plate2Go.transform.position = center + new Vector3(4f, 0.05f, -3f);
            plate2Go.transform.localScale = new Vector3(2f, 0.1f, 2f);
            var plate2 = plate2Go.AddComponent<PressurePlate>();
            plate2.Init(null, stayDown: false);

            // Block 1 — left back
            var block1Go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block1Go.name = "FinalBlock_1";
            block1Go.transform.SetParent(_dungeonRoot.transform);
            block1Go.transform.position = center + new Vector3(-4f, 0.6f, 4f);
            block1Go.transform.localScale = new Vector3(1.2f, 1.2f, 1.2f);
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            block1Go.GetComponent<Renderer>().material = new Material(shader) { color = new Color(0.5f, 0.4f, 0.35f) };
            var block1 = block1Go.AddComponent<PushableBlock>();
            block1.Init();

            // Block 2 — right back
            var block2Go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block2Go.name = "FinalBlock_2";
            block2Go.transform.SetParent(_dungeonRoot.transform);
            block2Go.transform.position = center + new Vector3(4f, 0.6f, 4f);
            block2Go.transform.localScale = new Vector3(1.2f, 1.2f, 1.2f);
            block2Go.GetComponent<Renderer>().material = new Material(shader) { color = new Color(0.5f, 0.4f, 0.35f) };
            var block2 = block2Go.AddComponent<PushableBlock>();
            block2.Init();

            // Switch on back wall
            var sw = SpawnWallSwitch(center + new Vector3(0, 1.5f, room.Depth * 0.45f),
                null, null, 0);

            // Initialize tracker
            tracker.Init(plate1, plate2, sw, finalDoor);

            // Hint
            SpawnHintText(center + new Vector3(0, 2.5f, -room.Depth * 0.45f + 1f),
                "Two stones, two plates, one switch.", 0.35f);

            Debug.Log("[DungeonManager] Final challenge room spawned.");
        }

        // --- Room 9: Exit ---
        private void SpawnExitRoomContent(RoomDef room)
        {
            Vector3 center = RoomCenter(room);
            SpawnDungeonTorch(center + new Vector3(room.Width * 0.35f, 2f, 0));
            SpawnDungeonTorch(center + new Vector3(-room.Width * 0.35f, 2f, 0));
            SpawnExitPortal(center);
        }

        // ===================================================================
        //  PUZZLE HELPER SPAWNERS
        // ===================================================================

        private PuzzleSwitch SpawnWallSwitch(Vector3 pos, PuzzleDoor door,
            SwitchSequencePuzzle puzzle, int index)
        {
            var switchGo = new GameObject($"PuzzleSwitch_{index}");
            switchGo.transform.SetParent(_dungeonRoot.transform);
            switchGo.transform.position = pos;

            // Base plate on wall
            var basePlate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            basePlate.transform.SetParent(switchGo.transform);
            basePlate.transform.localPosition = Vector3.zero;
            basePlate.transform.localScale = new Vector3(0.4f, 0.6f, 0.15f);
            Object.Destroy(basePlate.GetComponent<Collider>());
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            basePlate.GetComponent<Renderer>().material = new Material(shader) { color = new Color(0.35f, 0.3f, 0.25f) };

            // Lever arm
            var leverArm = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            leverArm.transform.SetParent(switchGo.transform);
            leverArm.transform.localPosition = new Vector3(0, 0, 0.1f);
            leverArm.transform.localScale = new Vector3(0.1f, 0.25f, 0.1f);
            leverArm.transform.localRotation = Quaternion.Euler(-45f, 0, 0);
            Object.Destroy(leverArm.GetComponent<Collider>());

            // Add a collider to the parent for interaction
            var col = switchGo.AddComponent<BoxCollider>();
            col.size = new Vector3(0.6f, 0.8f, 0.4f);

            var sw = switchGo.AddComponent<PuzzleSwitch>();
            sw.Init(leverArm.transform, door, puzzle, index);

            return sw;
        }

        private void SpawnKeyPickup(Vector3 pos, string keyItemId, string keyName, Color keyColor)
        {
            var keyGo = new GameObject($"PuzzleKey_{keyItemId}");
            keyGo.transform.SetParent(_dungeonRoot.transform);
            keyGo.transform.position = pos;

            // Pedestal
            var pedestal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pedestal.transform.SetParent(keyGo.transform);
            pedestal.transform.localPosition = new Vector3(0, -0.3f, 0);
            pedestal.transform.localScale = new Vector3(0.5f, 0.3f, 0.5f);
            Object.Destroy(pedestal.GetComponent<Collider>());
            pedestal.GetComponent<Renderer>().material = _wallMat;

            // Key visual — body
            var keyVisual = new GameObject("KeyVisual");
            keyVisual.transform.SetParent(keyGo.transform);
            keyVisual.transform.localPosition = Vector3.zero;

            var keyBody = GameObject.CreatePrimitive(PrimitiveType.Cube);
            keyBody.transform.SetParent(keyVisual.transform);
            keyBody.transform.localPosition = new Vector3(0, 0, 0);
            keyBody.transform.localScale = new Vector3(0.12f, 0.45f, 0.06f);
            Object.Destroy(keyBody.GetComponent<Collider>());
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var keyMat = new Material(shader) { color = keyColor };
            keyMat.SetFloat("_Smoothness", 0.7f);
            keyBody.GetComponent<Renderer>().material = keyMat;

            // Key handle (ring)
            var keyHandle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            keyHandle.transform.SetParent(keyVisual.transform);
            keyHandle.transform.localPosition = new Vector3(0, 0.3f, 0);
            keyHandle.transform.localScale = new Vector3(0.2f, 0.04f, 0.2f);
            Object.Destroy(keyHandle.GetComponent<Collider>());
            keyHandle.GetComponent<Renderer>().material = keyMat;

            // Glow light
            var glow = keyGo.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = keyColor;
            glow.intensity = 2f;
            glow.range = 4f;

            // Collider for interaction
            var col = keyGo.AddComponent<BoxCollider>();
            col.size = new Vector3(0.5f, 0.8f, 0.5f);
            col.center = new Vector3(0, 0, 0);

            var key = keyGo.AddComponent<PuzzleKey>();
            key.Init(keyItemId, keyName, keyColor, keyVisual);

            // Slow rotation
            keyGo.AddComponent<SlowSpin>();
        }

        private void SpawnHintText(Vector3 pos, string text, float scale)
        {
            var hintGo = new GameObject($"Hint_{text.Substring(0, Mathf.Min(10, text.Length))}");
            hintGo.transform.SetParent(_dungeonRoot.transform);
            hintGo.transform.position = pos;

            var hint = hintGo.AddComponent<HintLabel>();
            hint.Text = text;
            hint.DisplayRange = 8f;
        }

        private void SpawnColoredMarker(Vector3 pos, Color color)
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.transform.SetParent(_dungeonRoot.transform);
            marker.transform.position = pos;
            marker.transform.localScale = new Vector3(0.3f, 0.3f, 0.15f);
            Object.Destroy(marker.GetComponent<Collider>());

            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(shader) { color = color };
            mat.SetFloat("_Smoothness", 0.6f);
            marker.GetComponent<Renderer>().material = mat;

            // Small glow
            var light = marker.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = 1f;
            light.range = 3f;
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
            var spawner = FindAnyObjectByType<Enemies.EnemySpawner>();
            if (spawner != null) spawner.enabled = enabled;
            var dayNight = FindAnyObjectByType<DayNightCycle>();
            if (dayNight != null) dayNight.enabled = enabled;
        }
    }

    // ===================================================================
    //  HELPER COMPONENTS
    // ===================================================================

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

    /// <summary>
    /// Slow Y-axis rotation for key pickups.
    /// </summary>
    public class SlowSpin : MonoBehaviour
    {
        private void Update()
        {
            transform.Rotate(0, 45f * Time.deltaTime, 0);
        }
    }

    /// <summary>
    /// World-space hint label that shows text when player is nearby.
    /// Uses IMGUI for rendering.
    /// </summary>
    public class HintLabel : MonoBehaviour
    {
        public string Text = "";
        public float DisplayRange = 8f;

        private Transform _player;

        private void Update()
        {
            if (_player == null)
            {
                var controller = FindAnyObjectByType<Player.ThirdPersonController>();
                if (controller != null) _player = controller.transform;
            }
        }

        private void OnGUI()
        {
            if (_player == null || string.IsNullOrEmpty(Text)) return;

            float dist = Vector3.Distance(transform.position, _player.position);
            if (dist > DisplayRange) return;

            var cam = UnityEngine.Camera.main;
            if (cam == null) return;

            Vector3 screenPos = cam.WorldToScreenPoint(transform.position);
            if (screenPos.z < 0) return;

            float alpha = Mathf.Clamp01(1f - (dist / DisplayRange));
            float scale = Screen.height / 1080f;

            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(14 * scale),
                fontStyle = FontStyle.Italic,
                alignment = TextAnchor.MiddleCenter
            };
            style.normal.textColor = new Color(0.9f, 0.85f, 0.6f, alpha * 0.9f);

            float x = screenPos.x;
            float y = Screen.height - screenPos.y;
            GUI.Label(new Rect(x - 150, y - 15, 300, 30), Text, style);
        }
    }

    /// <summary>
    /// Tracks multiple puzzle conditions for the final room.
    /// Door opens only when both plates are pressed AND switch is on.
    /// </summary>
    public class FinalPuzzleTracker : MonoBehaviour
    {
        private PressurePlate _plate1, _plate2;
        private PuzzleSwitch _switch;
        private PuzzleDoor _door;
        private bool _doorOpened;

        public void Init(PressurePlate plate1, PressurePlate plate2,
            PuzzleSwitch sw, PuzzleDoor door)
        {
            _plate1 = plate1;
            _plate2 = plate2;
            _switch = sw;
            _door = door;
        }

        private void Update()
        {
            if (_doorOpened || _door == null) return;

            bool allConditionsMet = _plate1 != null && _plate1.IsPressed
                && _plate2 != null && _plate2.IsPressed
                && _switch != null && _switch.IsOn;

            if (allConditionsMet)
            {
                _door.Open();
                _door.Lock();
                _doorOpened = true;
                Debug.Log("[FinalPuzzleTracker] All conditions met! Door opened!");
            }
        }
    }
}
