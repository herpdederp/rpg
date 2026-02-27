using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FantasyGame.World
{
    public class VegetationPlacer : MonoBehaviour
    {
        private const int MIN_TREES_PER_CHUNK = 3;
        private const int MAX_TREES_PER_CHUNK = 8;
        private const int MIN_ROCKS_PER_CHUNK = 2;
        private const int MAX_ROCKS_PER_CHUNK = 5;
        private const float MIN_TREE_HEIGHT = 2f;
        private const float MAX_TREE_HEIGHT = 20f;
        private const float MIN_SLOPE_FOR_TREES = 0.65f;

        private Mesh[] _treeMeshes;
        private Mesh[] _rockMeshes;
        private Material _vegetationMaterial;
        private TerrainGenerator _terrain;
        private Dictionary<Vector2Int, List<GameObject>> _chunkVegetation = new Dictionary<Vector2Int, List<GameObject>>();
        private Transform _vegetationParent;

        public void Init(Mesh[] treeMeshes, Mesh[] rockMeshes, Material vegetationMat, TerrainGenerator terrain)
        {
            _treeMeshes = treeMeshes;
            _rockMeshes = rockMeshes;
            _vegetationMaterial = vegetationMat;
            _vegetationMaterial.enableInstancing = true;
            _terrain = terrain;

            _vegetationParent = new GameObject("Vegetation").transform;
        }

        /// <summary>
        /// Place vegetation on a terrain chunk. Call after chunk is generated.
        /// </summary>
        public void PlaceOnChunk(int chunkX, int chunkZ)
        {
            Vector2Int key = new Vector2Int(chunkX, chunkZ);
            if (_chunkVegetation.ContainsKey(key)) return;
            if (_treeMeshes == null || _treeMeshes.Length == 0) return;

            int chunkSize = TerrainGenerator.CHUNK_VERTS - 1;
            float originX = chunkX * chunkSize;
            float originZ = chunkZ * chunkSize;

            // Deterministic random per chunk
            int seed = chunkX * 73856093 ^ chunkZ * 19349663 ^ _terrain.Seed;
            var rng = new System.Random(seed);

            var objects = new List<GameObject>();

            // Place trees
            int treeCount = rng.Next(MIN_TREES_PER_CHUNK, MAX_TREES_PER_CHUNK + 1);
            for (int i = 0; i < treeCount; i++)
            {
                float x = originX + (float)(rng.NextDouble() * chunkSize);
                float z = originZ + (float)(rng.NextDouble() * chunkSize);
                float height = _terrain.GetHeight(x, z);
                float slope = _terrain.GetSlope(x, z);

                // Only place trees on gentle slopes within height range
                if (slope < MIN_SLOPE_FOR_TREES || height < MIN_TREE_HEIGHT || height > MAX_TREE_HEIGHT)
                    continue;

                int meshIdx = rng.Next(0, _treeMeshes.Length);
                float scale = 0.8f + (float)rng.NextDouble() * 0.5f;
                float rotY = (float)(rng.NextDouble() * 360.0);

                var go = CreateVegetationObject(
                    $"Tree_{chunkX}_{chunkZ}_{i}",
                    _treeMeshes[meshIdx],
                    new Vector3(x, height, z),
                    Quaternion.Euler(0, rotY, 0),
                    Vector3.one * scale
                );
                objects.Add(go);
            }

            // Place rocks
            if (_rockMeshes != null && _rockMeshes.Length > 0)
            {
                int rockCount = rng.Next(MIN_ROCKS_PER_CHUNK, MAX_ROCKS_PER_CHUNK + 1);
                for (int i = 0; i < rockCount; i++)
                {
                    float x = originX + (float)(rng.NextDouble() * chunkSize);
                    float z = originZ + (float)(rng.NextDouble() * chunkSize);
                    float height = _terrain.GetHeight(x, z);

                    int meshIdx = rng.Next(0, _rockMeshes.Length);
                    float scale = 0.6f + (float)rng.NextDouble() * 0.8f;
                    float rotY = (float)(rng.NextDouble() * 360.0);
                    float tiltX = (float)(rng.NextDouble() * 10.0 - 5.0);
                    float tiltZ = (float)(rng.NextDouble() * 10.0 - 5.0);

                    var go = CreateVegetationObject(
                        $"Rock_{chunkX}_{chunkZ}_{i}",
                        _rockMeshes[meshIdx],
                        new Vector3(x, height, z),
                        Quaternion.Euler(tiltX, rotY, tiltZ),
                        Vector3.one * scale
                    );
                    objects.Add(go);
                }
            }

            _chunkVegetation[key] = objects;
        }

        /// <summary>
        /// Remove vegetation for an unloaded chunk.
        /// </summary>
        public void RemoveChunk(Vector2Int chunkKey)
        {
            if (!_chunkVegetation.TryGetValue(chunkKey, out var objects)) return;

            foreach (var go in objects)
            {
                if (go != null) Destroy(go);
            }
            _chunkVegetation.Remove(chunkKey);
        }

        private GameObject CreateVegetationObject(string name, Mesh mesh, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_vegetationParent);
            go.transform.position = position;
            go.transform.rotation = rotation;
            go.transform.localScale = scale;
            go.isStatic = true;

            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;

            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = _vegetationMaterial;
            mr.shadowCastingMode = ShadowCastingMode.On;
            mr.receiveShadows = true;

            return go;
        }
    }
}
