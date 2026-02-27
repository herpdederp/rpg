// Assets/Scripts/VFX/ParticleEffectManager.cs
// ==============================================
// Code-driven particle effects using Unity's ParticleSystem.
// Spawns hit sparks, loot sparkle, campfire smoke/embers,
// enemy death poof, and heal effects — all created from code.

using UnityEngine;

namespace FantasyGame.VFX
{
    public class ParticleEffectManager : MonoBehaviour
    {
        public static ParticleEffectManager Instance { get; private set; }

        // Cached particle system prefab roots (pooled)
        private GameObject _hitSparkPrefab;
        private GameObject _deathPoofPrefab;
        private GameObject _healPrefab;
        private GameObject _lootSparklePrefab;
        private GameObject _breakPrefab;
        private GameObject _chestSparklePrefab;

        public void Init()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            CreatePrefabs();
            Debug.Log("[ParticleEffectManager] Initialized.");
        }

        // ===================================================================
        // Public API
        // ===================================================================

        public void SpawnHitSparks(Vector3 position, Vector3 direction)
        {
            SpawnEffect(_hitSparkPrefab, position, direction);
        }

        public void SpawnDeathPoof(Vector3 position)
        {
            SpawnEffect(_deathPoofPrefab, position, Vector3.up);
        }

        public void SpawnHealEffect(Vector3 position)
        {
            SpawnEffect(_healPrefab, position, Vector3.up);
        }

        public void SpawnLootSparkle(Vector3 position)
        {
            SpawnEffect(_lootSparklePrefab, position, Vector3.up);
        }

        public void SpawnBreakEffect(Vector3 position)
        {
            SpawnEffect(_breakPrefab, position, Vector3.up);
        }

        public void SpawnChestSparkle(Vector3 position)
        {
            SpawnEffect(_chestSparklePrefab, position, Vector3.up);
        }

        // ===================================================================
        // Prefab creation (all from code)
        // ===================================================================

        private void CreatePrefabs()
        {
            _hitSparkPrefab = CreateHitSparkPrefab();
            _deathPoofPrefab = CreateDeathPoofPrefab();
            _healPrefab = CreateHealPrefab();
            _lootSparklePrefab = CreateLootSparklePrefab();
            _breakPrefab = CreateBreakPrefab();
            _chestSparklePrefab = CreateChestSparklePrefab();
        }

        private GameObject CreateHitSparkPrefab()
        {
            var go = new GameObject("HitSpark_Prefab");
            go.transform.SetParent(transform);
            go.SetActive(false);

            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 0.3f;
            main.startLifetime = 0.2f;
            main.startSpeed = 8f;
            main.startSize = 0.08f;
            main.maxParticles = 12;
            main.loop = false;
            main.playOnAwake = false;
            main.startColor = new Color(1f, 0.9f, 0.5f);
            main.gravityModifier = 2f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] {
                new ParticleSystem.Burst(0f, 8, 12)
            });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 35f;
            shape.radius = 0.05f;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(new Color(1f, 0.9f, 0.4f), 0f),
                    new GradientColorKey(new Color(1f, 0.4f, 0.1f), 1f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            colorOverLifetime.color = gradient;

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, 0));

            SetupParticleRenderer(go, new Color(1f, 0.8f, 0.3f));

            return go;
        }

        private GameObject CreateDeathPoofPrefab()
        {
            var go = new GameObject("DeathPoof_Prefab");
            go.transform.SetParent(transform);
            go.SetActive(false);

            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 0.5f;
            main.startLifetime = 0.6f;
            main.startSpeed = 2f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
            main.maxParticles = 20;
            main.loop = false;
            main.playOnAwake = false;
            main.startColor = new Color(0.6f, 0.5f, 0.7f, 0.8f);
            main.gravityModifier = -0.3f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] {
                new ParticleSystem.Burst(0f, 12, 20)
            });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.3f;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(new Color(0.7f, 0.5f, 0.8f), 0f),
                    new GradientColorKey(new Color(0.4f, 0.3f, 0.5f), 1f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(0.8f, 0f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            colorOverLifetime.color = gradient;

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0, 0.5f, 1, 1.5f));

            SetupParticleRenderer(go, new Color(0.6f, 0.4f, 0.7f));

            return go;
        }

        private GameObject CreateHealPrefab()
        {
            var go = new GameObject("Heal_Prefab");
            go.transform.SetParent(transform);
            go.SetActive(false);

            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 1f;
            main.startLifetime = 0.8f;
            main.startSpeed = 1.5f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
            main.maxParticles = 30;
            main.loop = false;
            main.playOnAwake = false;
            main.startColor = new Color(0.3f, 1f, 0.4f, 0.9f);
            main.gravityModifier = -1f; // Float upward
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] {
                new ParticleSystem.Burst(0f, 20, 30)
            });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 15f;
            shape.radius = 0.5f;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(new Color(0.3f, 1f, 0.5f), 0f),
                    new GradientColorKey(new Color(0.8f, 1f, 0.4f), 1f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(0.9f, 0f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            colorOverLifetime.color = gradient;

            SetupParticleRenderer(go, new Color(0.3f, 1f, 0.5f));

            return go;
        }

        private GameObject CreateLootSparklePrefab()
        {
            var go = new GameObject("LootSparkle_Prefab");
            go.transform.SetParent(transform);
            go.SetActive(false);

            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 0.5f;
            main.startLifetime = 0.4f;
            main.startSpeed = 1f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.08f);
            main.maxParticles = 15;
            main.loop = false;
            main.playOnAwake = false;
            main.startColor = new Color(1f, 0.85f, 0.3f, 1f);
            main.gravityModifier = -0.5f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] {
                new ParticleSystem.Burst(0f, 8, 15)
            });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.2f;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(new Color(1f, 0.9f, 0.4f), 0f),
                    new GradientColorKey(new Color(1f, 0.7f, 0.2f), 1f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            colorOverLifetime.color = gradient;

            SetupParticleRenderer(go, new Color(1f, 0.85f, 0.3f));

            return go;
        }

        private GameObject CreateBreakPrefab()
        {
            var go = new GameObject("Break_Prefab");
            go.transform.SetParent(transform);
            go.SetActive(false);

            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 0.3f;
            main.startLifetime = 0.5f;
            main.startSpeed = 5f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.15f);
            main.maxParticles = 15;
            main.loop = false;
            main.playOnAwake = false;
            main.startColor = new Color(0.6f, 0.4f, 0.2f);
            main.gravityModifier = 3f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] {
                new ParticleSystem.Burst(0f, 10, 15)
            });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.2f;

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, 0.3f));

            SetupParticleRenderer(go, new Color(0.6f, 0.4f, 0.2f));

            return go;
        }

        private GameObject CreateChestSparklePrefab()
        {
            var go = new GameObject("ChestSparkle_Prefab");
            go.transform.SetParent(transform);
            go.SetActive(false);

            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 1f;
            main.startLifetime = 0.6f;
            main.startSpeed = 2f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.1f);
            main.maxParticles = 25;
            main.loop = false;
            main.playOnAwake = false;
            main.startColor = new Color(1f, 0.9f, 0.3f, 1f);
            main.gravityModifier = -0.8f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] {
                new ParticleSystem.Burst(0f, 15, 25)
            });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(0.6f, 0.3f, 0.4f);

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(new Color(1f, 1f, 0.6f), 0f),
                    new GradientColorKey(new Color(1f, 0.7f, 0.2f), 1f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            colorOverLifetime.color = gradient;

            SetupParticleRenderer(go, new Color(1f, 0.9f, 0.4f));

            return go;
        }

        // ===================================================================
        // Helpers
        // ===================================================================

        private void SetupParticleRenderer(GameObject go, Color color)
        {
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                // Use a simple unlit material
                var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                    ?? Shader.Find("Particles/Standard Unlit")
                    ?? Shader.Find("Universal Render Pipeline/Unlit");
                if (shader != null)
                {
                    var mat = new Material(shader);
                    mat.color = color;
                    // Try to enable additive blending for glow effect
                    if (mat.HasProperty("_Surface"))
                    {
                        mat.SetFloat("_Surface", 1f); // Transparent
                        mat.SetFloat("_Blend", 1f); // Additive
                    }
                    mat.renderQueue = 3100;
                    renderer.material = mat;
                }
            }
        }

        private void SpawnEffect(GameObject prefab, Vector3 position, Vector3 direction)
        {
            if (prefab == null) return;

            var instance = Instantiate(prefab, position, Quaternion.LookRotation(direction == Vector3.zero ? Vector3.up : direction));
            instance.SetActive(true);

            var ps = instance.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                ps.Play();
            }

            // Auto-destroy after particles finish
            float lifetime = 2f;
            if (ps != null)
            {
                lifetime = ps.main.duration + ps.main.startLifetime.constantMax + 0.1f;
            }
            Destroy(instance, lifetime);
        }
    }
}
