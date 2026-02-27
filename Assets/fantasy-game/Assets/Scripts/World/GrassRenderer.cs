using System.Collections.Generic;
using UnityEngine;
using FantasyGame.Utils;

namespace FantasyGame.World
{
    public class GrassRenderer : MonoBehaviour
    {
        private const int GRASS_PER_CHUNK = 150;
        private const float MIN_GRASS_HEIGHT = 2f;
        private const float MAX_GRASS_HEIGHT = 16f;
        private const float MIN_SLOPE_FOR_GRASS = 0.7f;
        private const float BLADE_WIDTH = 0.3f;
        private const float BLADE_HEIGHT = 0.6f;
        private const int MAX_INSTANCES_PER_CALL = 1023; // Unity limit

        private Mesh _grassMesh;
        private Material _grassMaterial;
        private TerrainGenerator _terrain;

        // Per-chunk grass instance data
        private Dictionary<Vector2Int, Matrix4x4[]> _chunkGrass = new Dictionary<Vector2Int, Matrix4x4[]>();

        public void Init(Material grassMat, TerrainGenerator terrain)
        {
            _grassMaterial = grassMat;
            _grassMaterial.enableInstancing = true;
            _terrain = terrain;
            _grassMesh = BuildGrassBladeMesh();
        }

        /// <summary>
        /// Generate grass positions for a chunk.
        /// </summary>
        public void GenerateForChunk(int chunkX, int chunkZ)
        {
            Vector2Int key = new Vector2Int(chunkX, chunkZ);
            if (_chunkGrass.ContainsKey(key)) return;

            int chunkSize = TerrainGenerator.CHUNK_VERTS - 1;
            float originX = chunkX * chunkSize;
            float originZ = chunkZ * chunkSize;

            int seed = chunkX * 48611 ^ chunkZ * 96293 ^ _terrain.Seed * 3;
            var rng = new System.Random(seed);

            var matrices = new List<Matrix4x4>();

            for (int i = 0; i < GRASS_PER_CHUNK; i++)
            {
                float x = originX + (float)(rng.NextDouble() * chunkSize);
                float z = originZ + (float)(rng.NextDouble() * chunkSize);
                float height = _terrain.GetHeight(x, z);
                float slope = _terrain.GetSlope(x, z);

                // Only on meadow-height gentle slopes
                if (slope < MIN_SLOPE_FOR_GRASS || height < MIN_GRASS_HEIGHT || height > MAX_GRASS_HEIGHT)
                    continue;

                float rotY = (float)(rng.NextDouble() * 360.0);
                float scale = 0.7f + (float)rng.NextDouble() * 0.6f;

                var pos = new Vector3(x, height, z);
                var rot = Quaternion.Euler(0, rotY, 0);
                var scl = new Vector3(scale, scale + (float)rng.NextDouble() * 0.3f, scale);

                matrices.Add(Matrix4x4.TRS(pos, rot, scl));
            }

            _chunkGrass[key] = matrices.ToArray();
        }

        /// <summary>
        /// Remove grass data for an unloaded chunk.
        /// </summary>
        public void RemoveChunk(Vector2Int chunkKey)
        {
            _chunkGrass.Remove(chunkKey);
        }

        /// <summary>
        /// Call every frame in Update to render all grass instances.
        /// </summary>
        public void RenderGrass()
        {
            if (_grassMesh == null || _grassMaterial == null) return;

            foreach (var kvp in _chunkGrass)
            {
                var matrices = kvp.Value;
                int offset = 0;
                while (offset < matrices.Length)
                {
                    int count = Mathf.Min(MAX_INSTANCES_PER_CALL, matrices.Length - offset);

                    // Extract sub-array for this batch
                    if (offset == 0 && count == matrices.Length)
                    {
                        Graphics.DrawMeshInstanced(_grassMesh, 0, _grassMaterial, matrices);
                    }
                    else
                    {
                        var batch = new Matrix4x4[count];
                        System.Array.Copy(matrices, offset, batch, 0, count);
                        Graphics.DrawMeshInstanced(_grassMesh, 0, _grassMaterial, batch);
                    }
                    offset += count;
                }
            }
        }

        /// <summary>
        /// Build a crossed-quad grass blade mesh (X shape from two quads).
        /// </summary>
        private Mesh BuildGrassBladeMesh()
        {
            // Two crossed quads forming an X when viewed from above
            float hw = BLADE_WIDTH * 0.5f;
            float h = BLADE_HEIGHT;

            var vertices = new Vector3[]
            {
                // Quad 1 (front-back)
                new Vector3(-hw, 0, 0),
                new Vector3(hw, 0, 0),
                new Vector3(hw, h, 0),
                new Vector3(-hw, h, 0),

                // Quad 2 (left-right, rotated 90°)
                new Vector3(0, 0, -hw),
                new Vector3(0, 0, hw),
                new Vector3(0, h, hw),
                new Vector3(0, h, -hw),
            };

            var uvs = new Vector2[]
            {
                new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1),
                new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1),
            };

            var normals = new Vector3[]
            {
                Vector3.back, Vector3.back, Vector3.back, Vector3.back,
                Vector3.left, Vector3.left, Vector3.left, Vector3.left,
            };

            var triangles = new int[]
            {
                // Quad 1 front
                0, 2, 1, 0, 3, 2,
                // Quad 1 back
                0, 1, 2, 0, 2, 3,
                // Quad 2 front
                4, 6, 5, 4, 7, 6,
                // Quad 2 back
                4, 5, 6, 4, 6, 7,
            };

            var mesh = new Mesh();
            mesh.name = "GrassBlade";
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.normals = normals;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();

            return mesh;
        }
    }
}
