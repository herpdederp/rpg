// Assets/Scripts/World/WaterRenderer.cs
// ========================================
// Procedural water planes that spawn around the player.
// Creates small lakes/ponds at low terrain areas.
// Uses a custom water shader for animated waves and transparency.

using UnityEngine;
using System.Collections.Generic;

namespace FantasyGame.World
{
    public class WaterRenderer : MonoBehaviour
    {
        private Transform _player;
        private Material _waterMat;
        private List<GameObject> _waterPlanes = new List<GameObject>();
        private TerrainGenerator _terrain;

        // Water level: water appears below this height relative to average terrain
        private const float WATER_HEIGHT_OFFSET = -2f;
        private const float WATER_PLANE_SIZE = 30f;
        private const int MAX_WATER_PLANES = 4;

        // Pre-calculated lake positions (seeded)
        private Vector3[] _lakePositions;
        private float[] _lakeSizes;
        private float _waterLevel;

        public void Init(Transform player, TerrainGenerator terrain, float waterLevel)
        {
            _player = player;
            _terrain = terrain;
            _waterLevel = waterLevel;

            CreateWaterMaterial();
            FindLakePositions();
            SpawnWaterPlanes();

            Debug.Log($"[WaterRenderer] Initialized with {_lakePositions.Length} lakes at water level {_waterLevel:F1}.");
        }

        private void CreateWaterMaterial()
        {
            // Try to use the custom water shader, fall back to transparent unlit
            var shader = Shader.Find("FantasyGame/PainterlyWater");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null)
                    shader = Shader.Find("Unlit/Color");
            }

            _waterMat = new Material(shader);
            _waterMat.name = "WaterMat";

            // Set water properties
            if (_waterMat.HasProperty("_BaseColor"))
                _waterMat.SetColor("_BaseColor", new Color(0.15f, 0.35f, 0.5f, 0.65f));
            if (_waterMat.HasProperty("_DeepColor"))
                _waterMat.SetColor("_DeepColor", new Color(0.05f, 0.15f, 0.3f, 0.8f));
            if (_waterMat.HasProperty("_WaveSpeed"))
                _waterMat.SetFloat("_WaveSpeed", 0.8f);
            if (_waterMat.HasProperty("_WaveHeight"))
                _waterMat.SetFloat("_WaveHeight", 0.15f);
            if (_waterMat.HasProperty("_FresnelPower"))
                _waterMat.SetFloat("_FresnelPower", 3f);

            // Make it transparent
            _waterMat.color = new Color(0.15f, 0.35f, 0.5f, 0.65f);

            // URP transparent surface setup
            if (_waterMat.HasProperty("_Surface"))
            {
                _waterMat.SetFloat("_Surface", 1f); // Transparent
                _waterMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                _waterMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                _waterMat.SetInt("_ZWrite", 0);
                _waterMat.renderQueue = 3000;
                _waterMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
            else
            {
                _waterMat.renderQueue = 3000;
            }
        }

        private void FindLakePositions()
        {
            // Use seeded random for consistent lake placement
            var oldState = Random.state;
            Random.InitState(54321);

            var lakes = new List<Vector3>();
            var sizes = new List<float>();

            // Place lakes at specific low-lying areas
            Vector2[] candidates = {
                new Vector2(20f, 35f),
                new Vector2(-25f, 15f),
                new Vector2(45f, -10f),
                new Vector2(-10f, 45f),
            };

            foreach (var candidate in candidates)
            {
                float terrainH = 0f;
                if (_terrain != null)
                    terrainH = _terrain.GetHeight(candidate.x, candidate.y);

                // Only place water in lower terrain areas
                if (terrainH < _waterLevel + 3f)
                {
                    float lakeY = Mathf.Min(terrainH - 0.5f, _waterLevel);
                    lakes.Add(new Vector3(candidate.x, lakeY, candidate.y));
                    sizes.Add(Random.Range(12f, 25f));
                }
            }

            _lakePositions = lakes.ToArray();
            _lakeSizes = sizes.ToArray();

            Random.state = oldState;
        }

        private void SpawnWaterPlanes()
        {
            for (int i = 0; i < _lakePositions.Length; i++)
            {
                CreateWaterPlane(_lakePositions[i], _lakeSizes[i], i);
            }
        }

        private void CreateWaterPlane(Vector3 center, float size, int index)
        {
            var waterGo = new GameObject($"Water_Lake_{index}");
            waterGo.transform.position = center;
            waterGo.transform.SetParent(transform);

            // Create a subdivided plane mesh for better wave deformation
            var mesh = CreateSubdividedPlane(size, 16);
            var mf = waterGo.AddComponent<MeshFilter>();
            mf.mesh = mesh;

            var mr = waterGo.AddComponent<MeshRenderer>();
            mr.material = _waterMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            _waterPlanes.Add(waterGo);
        }

        private Mesh CreateSubdividedPlane(float size, int subdivisions)
        {
            int vertCount = (subdivisions + 1) * (subdivisions + 1);
            var vertices = new Vector3[vertCount];
            var uvs = new Vector2[vertCount];
            var normals = new Vector3[vertCount];
            var colors = new Color[vertCount];

            float halfSize = size * 0.5f;
            float step = size / subdivisions;

            int v = 0;
            for (int z = 0; z <= subdivisions; z++)
            {
                for (int x = 0; x <= subdivisions; x++)
                {
                    float px = -halfSize + x * step;
                    float pz = -halfSize + z * step;

                    // Circular shape: reduce opacity near edges
                    float dist = Mathf.Sqrt(px * px + pz * pz) / halfSize;
                    float alpha = Mathf.Clamp01(1f - dist * dist);

                    vertices[v] = new Vector3(px, 0, pz);
                    uvs[v] = new Vector2((float)x / subdivisions, (float)z / subdivisions);
                    normals[v] = Vector3.up;
                    colors[v] = new Color(0.15f, 0.35f, 0.5f, alpha * 0.65f);
                    v++;
                }
            }

            // Triangles
            int triCount = subdivisions * subdivisions * 6;
            var triangles = new int[triCount];
            int t = 0;
            for (int z = 0; z < subdivisions; z++)
            {
                for (int x = 0; x < subdivisions; x++)
                {
                    int bl = z * (subdivisions + 1) + x;
                    int br = bl + 1;
                    int tl = bl + (subdivisions + 1);
                    int tr = tl + 1;

                    triangles[t++] = bl;
                    triangles[t++] = tl;
                    triangles[t++] = br;
                    triangles[t++] = br;
                    triangles[t++] = tl;
                    triangles[t++] = tr;
                }
            }

            var mesh = new Mesh();
            mesh.name = "WaterPlane";
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.normals = normals;
            mesh.colors = colors;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();

            return mesh;
        }

        private void Update()
        {
            // Animate water shader time
            if (_waterMat != null && _waterMat.HasProperty("_WaveTime"))
            {
                _waterMat.SetFloat("_WaveTime", Time.time);
            }
        }
    }
}
