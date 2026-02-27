using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using FantasyGame.Utils;

namespace FantasyGame.World
{
    public class TerrainGenerator : MonoBehaviour
    {
        public const int CHUNK_VERTS = 65;       // vertices per side (64 quads + 1)
        public const float WORLD_SCALE = 1f;     // 1 vertex = 1 world unit
        public const int VIEW_RANGE = 2;         // chunks in each direction around player

        private Dictionary<Vector2Int, GameObject> _loadedChunks = new Dictionary<Vector2Int, GameObject>();
        private int _seed;
        private Material _terrainMaterial;
        private Transform _chunksParent;

        public int Seed => _seed;

        public void Init(int seed, Material terrainMat)
        {
            _seed = seed;
            _terrainMaterial = terrainMat;
            _terrainMaterial.enableInstancing = true;

            _chunksParent = new GameObject("Terrain").transform;
        }

        /// <summary>
        /// Call each frame with the player position. Loads/unloads chunks as needed.
        /// </summary>
        public void UpdateChunks(Vector3 playerPos)
        {
            int chunkSize = CHUNK_VERTS - 1;
            int playerChunkX = Mathf.FloorToInt(playerPos.x / chunkSize);
            int playerChunkZ = Mathf.FloorToInt(playerPos.z / chunkSize);

            // Load chunks in range
            for (int x = playerChunkX - VIEW_RANGE; x <= playerChunkX + VIEW_RANGE; x++)
            {
                for (int z = playerChunkZ - VIEW_RANGE; z <= playerChunkZ + VIEW_RANGE; z++)
                {
                    Vector2Int key = new Vector2Int(x, z);
                    if (!_loadedChunks.ContainsKey(key))
                    {
                        var chunk = SpawnChunk(x, z);
                        _loadedChunks[key] = chunk;
                    }
                }
            }

            // Unload distant chunks
            var toRemove = new List<Vector2Int>();
            foreach (var kvp in _loadedChunks)
            {
                if (Mathf.Abs(kvp.Key.x - playerChunkX) > VIEW_RANGE + 1 ||
                    Mathf.Abs(kvp.Key.y - playerChunkZ) > VIEW_RANGE + 1)
                {
                    toRemove.Add(kvp.Key);
                }
            }
            foreach (var key in toRemove)
            {
                Destroy(_loadedChunks[key]);
                _loadedChunks.Remove(key);
            }
        }

        /// <summary>
        /// Sample terrain height at any world position.
        /// </summary>
        public float GetHeight(float worldX, float worldZ)
        {
            return NoiseUtils.SampleHeight(worldX, worldZ, _seed);
        }

        /// <summary>
        /// Get slope at a world position (1.0 = flat, 0.0 = vertical).
        /// </summary>
        public float GetSlope(float worldX, float worldZ)
        {
            return NoiseUtils.GetSlope(worldX, worldZ, _seed);
        }

        public bool IsChunkLoaded(Vector2Int chunkCoord)
        {
            return _loadedChunks.ContainsKey(chunkCoord);
        }

        public IEnumerable<Vector2Int> GetLoadedChunks()
        {
            return _loadedChunks.Keys;
        }

        private GameObject SpawnChunk(int chunkX, int chunkZ)
        {
            int chunkSize = CHUNK_VERTS - 1;
            float originX = chunkX * chunkSize;
            float originZ = chunkZ * chunkSize;

            Mesh mesh = BuildChunkMesh(originX, originZ);

            var go = new GameObject($"Chunk_{chunkX}_{chunkZ}");
            go.transform.SetParent(_chunksParent);
            go.transform.position = new Vector3(originX, 0f, originZ);
            go.isStatic = true;

            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;

            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = _terrainMaterial;
            mr.shadowCastingMode = ShadowCastingMode.On;
            mr.receiveShadows = true;

            var mc = go.AddComponent<MeshCollider>();
            mc.sharedMesh = mesh;

            return go;
        }

        private Mesh BuildChunkMesh(float originX, float originZ)
        {
            int vertCount = CHUNK_VERTS * CHUNK_VERTS;
            int chunkSize = CHUNK_VERTS - 1;

            var vertices = new Vector3[vertCount];
            var normals = new Vector3[vertCount];
            var colors = new Color[vertCount];

            // Generate vertices with heights and colors
            for (int z = 0; z < CHUNK_VERTS; z++)
            {
                for (int x = 0; x < CHUNK_VERTS; x++)
                {
                    int idx = z * CHUNK_VERTS + x;
                    float worldX = originX + x * WORLD_SCALE;
                    float worldZ = originZ + z * WORLD_SCALE;

                    float height = NoiseUtils.SampleHeight(worldX, worldZ, _seed);
                    vertices[idx] = new Vector3(x * WORLD_SCALE, height, z * WORLD_SCALE);

                    // Analytical normal from noise derivatives
                    normals[idx] = NoiseUtils.SampleNormal(worldX, worldZ, _seed);

                    // Vertex color based on height and slope
                    float slope = normals[idx].y;
                    colors[idx] = GetVertexColor(height, slope);
                }
            }

            // Build triangle indices
            int quadCount = chunkSize * chunkSize;
            var triangles = new int[quadCount * 6];
            int tri = 0;

            for (int z = 0; z < chunkSize; z++)
            {
                for (int x = 0; x < chunkSize; x++)
                {
                    int bl = z * CHUNK_VERTS + x;
                    int br = bl + 1;
                    int tl = bl + CHUNK_VERTS;
                    int tr = tl + 1;

                    triangles[tri++] = bl;
                    triangles[tri++] = tl;
                    triangles[tri++] = br;
                    triangles[tri++] = br;
                    triangles[tri++] = tl;
                    triangles[tri++] = tr;
                }
            }

            var mesh = new Mesh();
            mesh.name = $"TerrainChunk";
            mesh.indexFormat = IndexFormat.UInt16; // 65*65 = 4225 verts, fits in UInt16
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.colors = colors;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();

            return mesh;
        }

        private Color GetVertexColor(float height, float slope)
        {
            // Base colors for different height zones
            Color lowland = new Color(0.18f, 0.28f, 0.12f);    // Dark forest green
            Color meadow = new Color(0.30f, 0.45f, 0.18f);     // Grass green
            Color highland = new Color(0.40f, 0.38f, 0.30f);    // Muted grey-green
            Color peak = new Color(0.50f, 0.47f, 0.42f);        // Rocky grey-brown
            Color rock = new Color(0.42f, 0.40f, 0.35f);        // Cliff rock

            // Height-based blending
            Color heightColor;
            if (height < 5f)
                heightColor = Color.Lerp(lowland, meadow, height / 5f);
            else if (height < 15f)
                heightColor = Color.Lerp(meadow, highland, (height - 5f) / 10f);
            else if (height < 25f)
                heightColor = Color.Lerp(highland, peak, (height - 15f) / 10f);
            else
                heightColor = peak;

            // Steep slopes get rock color regardless of height
            if (slope < 0.7f)
            {
                float rockBlend = Mathf.InverseLerp(0.7f, 0.4f, slope);
                heightColor = Color.Lerp(heightColor, rock, rockBlend);
            }

            return heightColor;
        }
    }
}
