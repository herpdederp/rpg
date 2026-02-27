// Assets/Scripts/Interaction/BreakableObject.cs
// ================================================
// Crates and barrels that break when hit (IDamageable) or interacted with.
// Spawns fragments on break and may drop loot.

using UnityEngine;
using FantasyGame.Combat;
using FantasyGame.RPG;
using FantasyGame.Enemies;

namespace FantasyGame.Interaction
{
    public class BreakableObject : MonoBehaviour, IDamageable
    {
        public string ObjectName = "Crate";
        public int MaxHealth = 10;
        public string LootId = "";
        public float LootChance = 0.3f;

        public int CurrentHealth { get; private set; }
        public bool IsAlive => CurrentHealth > 0;

        private bool _broken;

        public void Init(int health, string lootId, float lootChance)
        {
            MaxHealth = health;
            CurrentHealth = health;
            LootId = lootId;
            LootChance = lootChance;

            // Add collider for hit detection
            if (GetComponent<Collider>() == null)
            {
                var box = gameObject.AddComponent<BoxCollider>();
                box.size = new Vector3(0.8f, 0.8f, 0.8f);
                box.center = new Vector3(0, 0.4f, 0);
            }
        }

        public void TakeDamage(int amount, Vector3 sourcePosition)
        {
            if (_broken) return;

            CurrentHealth -= amount;
            Debug.Log($"[Breakable] {ObjectName} took {amount} damage, HP: {CurrentHealth}/{MaxHealth}");

            // Shake effect
            StartCoroutine(ShakeEffect());

            if (CurrentHealth <= 0)
            {
                Break();
            }
        }

        private void Break()
        {
            if (_broken) return;
            _broken = true;

            // Sound + VFX
            if (Audio.SoundManager.Instance != null)
                Audio.SoundManager.Instance.PlayBreakObject();
            if (VFX.ParticleEffectManager.Instance != null)
                VFX.ParticleEffectManager.Instance.SpawnBreakEffect(transform.position + Vector3.up * 0.3f);

            // Drop loot
            if (!string.IsNullOrEmpty(LootId) && Random.value <= LootChance)
            {
                var item = ItemDatabase.Get(LootId);
                if (item != null)
                {
                    var lootGo = new GameObject($"Loot_{item.Name}");
                    lootGo.transform.position = transform.position + Vector3.up * 0.5f;

                    var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    sphere.transform.SetParent(lootGo.transform);
                    sphere.transform.localScale = Vector3.one * 0.3f;
                    sphere.transform.localPosition = Vector3.zero;

                    var renderer = sphere.GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color"));
                        mat.color = item.DisplayColor;
                        renderer.material = mat;
                    }

                    var sphereCol = sphere.GetComponent<Collider>();
                    if (sphereCol != null) Destroy(sphereCol);

                    var trigger = lootGo.AddComponent<SphereCollider>();
                    trigger.radius = 1.0f;
                    trigger.isTrigger = true;

                    var pickup = lootGo.AddComponent<LootPickup>();
                    pickup.Init(item, 1);
                    lootGo.AddComponent<LootBob>();

                    Debug.Log($"[Breakable] {ObjectName} dropped {item.Name}!");
                }
            }

            // Spawn fragments
            StartCoroutine(BreakAnimation());
        }

        private System.Collections.IEnumerator ShakeEffect()
        {
            Vector3 originalPos = transform.position;
            for (int i = 0; i < 4; i++)
            {
                transform.position = originalPos + Random.insideUnitSphere * 0.05f;
                yield return null;
            }
            transform.position = originalPos;
        }

        private System.Collections.IEnumerator BreakAnimation()
        {
            // Spawn 4-6 small fragment cubes
            int fragCount = Random.Range(4, 7);
            var fragments = new GameObject[fragCount];

            var renderer = GetComponentInChildren<Renderer>();
            Color fragColor = renderer != null && renderer.material.HasProperty("_BaseColor")
                ? renderer.material.GetColor("_BaseColor")
                : new Color(0.6f, 0.4f, 0.2f);

            for (int i = 0; i < fragCount; i++)
            {
                var frag = GameObject.CreatePrimitive(PrimitiveType.Cube);
                frag.name = "Fragment";
                float size = Random.Range(0.08f, 0.2f);
                frag.transform.localScale = Vector3.one * size;
                frag.transform.position = transform.position + Vector3.up * 0.3f + Random.insideUnitSphere * 0.2f;

                var fragRenderer = frag.GetComponent<Renderer>();
                if (fragRenderer != null)
                {
                    var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                    mat.color = fragColor * Random.Range(0.7f, 1.0f);
                    fragRenderer.material = mat;
                }

                // Remove collider from fragments
                var col = frag.GetComponent<Collider>();
                if (col != null) Destroy(col);

                fragments[i] = frag;
            }

            // Hide original
            var myRenderer = GetComponentInChildren<Renderer>();
            if (myRenderer != null) myRenderer.enabled = false;
            var myCol = GetComponent<Collider>();
            if (myCol != null) myCol.enabled = false;

            // Animate fragments flying out and falling
            Vector3[] velocities = new Vector3[fragCount];
            for (int i = 0; i < fragCount; i++)
            {
                velocities[i] = Random.insideUnitSphere * 3f + Vector3.up * 2f;
            }

            float timer = 1.5f;
            while (timer > 0)
            {
                timer -= Time.deltaTime;
                for (int i = 0; i < fragCount; i++)
                {
                    if (fragments[i] == null) continue;
                    velocities[i] += Vector3.down * 9.8f * Time.deltaTime;
                    fragments[i].transform.position += velocities[i] * Time.deltaTime;
                    fragments[i].transform.Rotate(Random.insideUnitSphere * 360f * Time.deltaTime);

                    // Shrink near end
                    if (timer < 0.5f)
                    {
                        float s = timer / 0.5f;
                        fragments[i].transform.localScale *= (1f - Time.deltaTime * 3f);
                    }
                }
                yield return null;
            }

            // Cleanup
            for (int i = 0; i < fragCount; i++)
            {
                if (fragments[i] != null) Destroy(fragments[i]);
            }
            Destroy(gameObject);
        }
    }
}
