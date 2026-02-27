using System.Collections.Generic;
using UnityEngine;

namespace FantasyGame.World
{
    /// <summary>
    /// Coordinates all world systems: terrain, vegetation, grass.
    /// Drives chunk loading/unloading based on player position.
    /// </summary>
    public class WorldManager : MonoBehaviour
    {
        private TerrainGenerator _terrain;
        private VegetationPlacer _vegetation;
        private GrassRenderer _grass;
        private EnvironmentSetup _environment;

        private Transform _playerTransform;
        private HashSet<Vector2Int> _previousChunks = new HashSet<Vector2Int>();

        public TerrainGenerator Terrain => _terrain;

        /// <summary>
        /// Initialize the world. Call before spawning the player.
        /// </summary>
        public void Init(int seed, Material terrainMat, Material vegetationMat, Material grassMat,
                         Mesh[] treeMeshes, Mesh[] rockMeshes)
        {
            // Environment (lighting, fog, sky, post-processing)
            _environment = gameObject.AddComponent<EnvironmentSetup>();
            _environment.Init();

            // Terrain
            _terrain = gameObject.AddComponent<TerrainGenerator>();
            _terrain.Init(seed, terrainMat);

            // Vegetation
            _vegetation = gameObject.AddComponent<VegetationPlacer>();
            _vegetation.Init(treeMeshes, rockMeshes, vegetationMat, _terrain);

            // Grass
            _grass = gameObject.AddComponent<GrassRenderer>();
            _grass.Init(grassMat, _terrain);

            Debug.Log($"[WorldManager] Initialized with seed {seed}");
        }

        /// <summary>
        /// Set the player transform to track for chunk loading.
        /// </summary>
        public void SetPlayer(Transform player)
        {
            _playerTransform = player;

            // Force initial chunk load around player
            UpdateWorld();
        }

        /// <summary>
        /// Get terrain height at a world position.
        /// </summary>
        public float GetTerrainHeight(float x, float z)
        {
            return _terrain.GetHeight(x, z);
        }

        private void Update()
        {
            if (_playerTransform == null) return;

            UpdateWorld();

            // Render grass every frame (GPU instanced, no GameObjects)
            _grass.RenderGrass();
        }

        private void UpdateWorld()
        {
            Vector3 playerPos = _playerTransform.position;

            // Update terrain chunks
            _terrain.UpdateChunks(playerPos);

            // Track which chunks are now loaded
            var currentChunks = new HashSet<Vector2Int>(_terrain.GetLoadedChunks());

            // Add vegetation and grass to newly loaded chunks
            foreach (var chunk in currentChunks)
            {
                if (!_previousChunks.Contains(chunk))
                {
                    _vegetation.PlaceOnChunk(chunk.x, chunk.y);
                    _grass.GenerateForChunk(chunk.x, chunk.y);
                }
            }

            // Remove vegetation and grass from unloaded chunks
            foreach (var chunk in _previousChunks)
            {
                if (!currentChunks.Contains(chunk))
                {
                    _vegetation.RemoveChunk(chunk);
                    _grass.RemoveChunk(chunk);
                }
            }

            _previousChunks = currentChunks;
        }
    }
}
