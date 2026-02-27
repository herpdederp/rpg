// Assets/Scripts/Enemies/EnemySpawner.cs
// ========================================
// Spawns enemies around the player in loaded terrain chunks.
// Each enemy type has a config with stats, model mesh, and material.

using System.Collections.Generic;
using UnityEngine;
using FantasyGame.RPG;
using FantasyGame.Utils;

namespace FantasyGame.Enemies
{
    [System.Serializable]
    public class EnemyConfig
    {
        public string Name;
        public int MaxHealth;
        public int AttackDamage;
        public float AttackRange;
        public float AttackCooldown;
        public float MoveSpeed;
        public float DetectRange;
        public float ChaseRange;
        public int XPReward;
        public string LootId;
        public int LootCount;
        public float LootChance;

        // Visual
        public Mesh Mesh;
        public Material Material;
        public Vector3 Scale;
        public Color BaseColor;

        // Spawn rules
        public float MinSpawnHeight;   // Terrain height range for this enemy
        public float MaxSpawnHeight;
        public int MaxAlive;           // Max living at once
    }

    public class EnemySpawner : MonoBehaviour
    {
        private Transform _player;
        private List<EnemyConfig> _configs = new List<EnemyConfig>();
        private Dictionary<string, List<EnemyBase>> _activeEnemies = new Dictionary<string, List<EnemyBase>>();

        private float _spawnTimer;
        private const float SPAWN_INTERVAL = 3f;     // Check every 3 seconds
        private const float MIN_SPAWN_DIST = 20f;    // Don't spawn too close
        private const float MAX_SPAWN_DIST = 50f;    // Don't spawn too far
        private const float DESPAWN_DIST = 70f;       // Remove if too far

        public void Init(Transform player)
        {
            _player = player;
            _spawnTimer = 2f; // Initial delay
            SetupDefaultConfigs();
        }

        public void SetEnemyMesh(string enemyName, Mesh mesh)
        {
            foreach (var config in _configs)
            {
                if (config.Name == enemyName && mesh != null)
                {
                    config.Mesh = mesh;
                    // Create a material for this enemy type if not already set
                    if (config.Material == null)
                    {
                        var shader = Shader.Find("FantasyGame/PainterlyLit")
                            ?? Shader.Find("Universal Render Pipeline/Lit")
                            ?? Shader.Find("Standard");
                        config.Material = new Material(shader);
                        config.Material.SetColor("_BaseColor", Color.white); // vertex colors handle color
                        config.Material.enableInstancing = true;
                    }
                    Debug.Log($"[EnemySpawner] Set mesh for {enemyName}: {mesh.vertexCount} verts");
                }
            }
        }

        private void SetupDefaultConfigs()
        {
            // ---- SLIME ----
            var slime = new EnemyConfig
            {
                Name = "Slime",
                MaxHealth = 20,
                AttackDamage = 3,
                AttackRange = 1.5f,
                AttackCooldown = 2f,
                MoveSpeed = 1.5f,
                DetectRange = 8f,
                ChaseRange = 12f,
                XPReward = 10,
                LootId = "slime_gel",
                LootCount = 1,
                LootChance = 0.6f,
                Scale = new Vector3(0.6f, 0.6f, 0.6f),
                BaseColor = new Color(0.3f, 0.8f, 0.3f),
                MinSpawnHeight = -5f,
                MaxSpawnHeight = 10f,
                MaxAlive = 6
            };
            _configs.Add(slime);
            _activeEnemies["Slime"] = new List<EnemyBase>();

            // ---- SKELETON ----
            var skeleton = new EnemyConfig
            {
                Name = "Skeleton",
                MaxHealth = 40,
                AttackDamage = 8,
                AttackRange = 2f,
                AttackCooldown = 1.2f,
                MoveSpeed = 2.5f,
                DetectRange = 14f,
                ChaseRange = 20f,
                XPReward = 25,
                LootId = "bone_fragment",
                LootCount = 1,
                LootChance = 0.5f,
                Scale = new Vector3(1f, 1f, 1f),
                BaseColor = new Color(0.85f, 0.85f, 0.75f),
                MinSpawnHeight = 3f,
                MaxSpawnHeight = 20f,
                MaxAlive = 4
            };
            _configs.Add(skeleton);
            _activeEnemies["Skeleton"] = new List<EnemyBase>();

            // ---- WOLF ----
            var wolf = new EnemyConfig
            {
                Name = "Wolf",
                MaxHealth = 30,
                AttackDamage = 6,
                AttackRange = 2f,
                AttackCooldown = 0.8f,
                MoveSpeed = 4f,
                DetectRange = 16f,
                ChaseRange = 25f,
                XPReward = 20,
                LootId = "wolf_pelt",
                LootCount = 1,
                LootChance = 0.4f,
                Scale = new Vector3(0.8f, 0.6f, 1.0f),
                BaseColor = new Color(0.5f, 0.45f, 0.4f),
                MinSpawnHeight = 5f,
                MaxSpawnHeight = 25f,
                MaxAlive = 3
            };
            _configs.Add(wolf);
            _activeEnemies["Wolf"] = new List<EnemyBase>();
        }

        private void Update()
        {
            if (_player == null) return;

            _spawnTimer -= Time.deltaTime;

            // Cleanup dead/destroyed enemies and despawn far ones
            CleanupEnemies();

            if (_spawnTimer <= 0f)
            {
                _spawnTimer = SPAWN_INTERVAL;
                TrySpawnEnemies();
            }
        }

        private void CleanupEnemies()
        {
            foreach (var kvp in _activeEnemies)
            {
                var list = kvp.Value;
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    if (list[i] == null)
                    {
                        list.RemoveAt(i);
                        continue;
                    }

                    // Safety: destroy if fallen below world
                    if (list[i].transform.position.y < -50f)
                    {
                        Destroy(list[i].gameObject);
                        list.RemoveAt(i);
                        continue;
                    }

                    // Despawn if too far from player
                    float dist = Vector3.Distance(list[i].transform.position, _player.position);
                    if (dist > DESPAWN_DIST)
                    {
                        Destroy(list[i].gameObject);
                        list.RemoveAt(i);
                    }
                }
            }
        }

        private void TrySpawnEnemies()
        {
            foreach (var config in _configs)
            {
                var activeList = _activeEnemies[config.Name];
                if (activeList.Count >= config.MaxAlive) continue;

                // Try to find a valid spawn position
                Vector3? spawnPos = FindSpawnPosition(config);
                if (spawnPos.HasValue)
                {
                    SpawnEnemy(config, spawnPos.Value);
                }
            }
        }

        private Vector3? FindSpawnPosition(EnemyConfig config)
        {
            // Try several random positions around the player
            for (int attempt = 0; attempt < 5; attempt++)
            {
                float angle = Random.value * Mathf.PI * 2f;
                float dist = Random.Range(MIN_SPAWN_DIST, MAX_SPAWN_DIST);
                Vector3 offset = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * dist;
                Vector3 candidate = _player.position + offset;

                // Raycast down to find terrain
                if (Physics.Raycast(candidate + Vector3.up * 50f, Vector3.down, out RaycastHit hit, 100f))
                {
                    float terrainHeight = hit.point.y;

                    // Check if height is within range for this enemy type
                    if (terrainHeight >= config.MinSpawnHeight && terrainHeight <= config.MaxSpawnHeight)
                    {
                        // Make sure it's not too steep (slope check)
                        if (hit.normal.y > 0.7f)
                        {
                            // Keep enemies away from flat zones (villages, etc.)
                            if (!IsNearFlatZone(candidate.x, candidate.z, 16f))
                            {
                                return hit.point + Vector3.up * 0.1f;
                            }
                        }
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Returns true if the position is within (zone radius + falloff + buffer) of any flat zone.
        /// </summary>
        private bool IsNearFlatZone(float worldX, float worldZ, float buffer)
        {
            for (int i = 0; i < NoiseUtils.Zones.Count; i++)
            {
                var zone = NoiseUtils.Zones[i];
                float dx = worldX - zone.CenterX;
                float dz = worldZ - zone.CenterZ;
                float dist = Mathf.Sqrt(dx * dx + dz * dz);
                if (dist < zone.Radius + zone.FalloffRadius + buffer)
                    return true;
            }
            return false;
        }

        private void SpawnEnemy(EnemyConfig config, Vector3 position)
        {
            var enemyGo = new GameObject($"Enemy_{config.Name}_{Random.Range(0, 9999)}");
            enemyGo.transform.position = position;

            // Add visual mesh
            if (config.Mesh != null)
            {
                var meshFilter = enemyGo.AddComponent<MeshFilter>();
                meshFilter.sharedMesh = config.Mesh;
                var meshRenderer = enemyGo.AddComponent<MeshRenderer>();

                // Create per-instance material so hit flash doesn't affect all enemies
                Material sourceMat = config.Material;
                if (sourceMat == null)
                {
                    var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                    sourceMat = new Material(shader);
                    sourceMat.color = config.BaseColor;
                }
                var mat = new Material(sourceMat);
                meshRenderer.material = mat;
            }
            else
            {
                // Fallback: primitive capsule
                var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                capsule.transform.SetParent(enemyGo.transform);
                capsule.transform.localPosition = new Vector3(0, 0.75f, 0);
                capsule.transform.localScale = config.Scale;

                // Color it
                var renderer = capsule.GetComponent<Renderer>();
                if (renderer != null)
                {
                    var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                    mat.color = config.BaseColor;
                    renderer.material = mat;
                }

                // Remove capsule's collider (EnemyBase adds its own)
                var col = capsule.GetComponent<Collider>();
                if (col != null) Destroy(col);
            }

            enemyGo.transform.localScale = config.Mesh != null ? config.Scale : Vector3.one;

            // Add EnemyBase
            var enemy = enemyGo.AddComponent<EnemyBase>();
            enemy.EnemyName = config.Name;
            enemy.MaxHealth = config.MaxHealth;
            enemy.AttackDamage = config.AttackDamage;
            enemy.AttackRange = config.AttackRange;
            enemy.AttackCooldown = config.AttackCooldown;
            enemy.MoveSpeed = config.MoveSpeed;
            enemy.DetectRange = config.DetectRange;
            enemy.ChaseRange = config.ChaseRange;
            enemy.XPReward = config.XPReward;
            enemy.LootId = config.LootId;
            enemy.LootCount = config.LootCount;
            enemy.LootChance = config.LootChance;

            // Create material for hit flash
            var enemyRenderer = enemyGo.GetComponentInChildren<Renderer>();
            Material enemyMat = null;
            if (enemyRenderer != null)
            {
                enemyMat = enemyRenderer.material; // Already instanced
            }
            else
            {
                enemyMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                enemyMat.color = config.BaseColor;
            }

            enemy.Init(enemyMat);

            // Health bar
            var healthBar = enemyGo.AddComponent<EnemyHealthBar>();
            healthBar.Init(enemy);

            // Track
            _activeEnemies[config.Name].Add(enemy);
            enemy.OnDeath += (e) =>
            {
                _activeEnemies[config.Name].Remove(e);
            };

            Debug.Log($"[EnemySpawner] Spawned {config.Name} at {position}");
        }
    }
}
