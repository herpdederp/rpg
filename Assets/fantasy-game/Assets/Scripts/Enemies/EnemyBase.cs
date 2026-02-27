// Assets/Scripts/Enemies/EnemyBase.cs
// =====================================
// Base class for all enemies. Implements IDamageable, health, death,
// XP reward, and loot drops.

using UnityEngine;
using FantasyGame.Combat;
using FantasyGame.RPG;

namespace FantasyGame.Enemies
{
    public class EnemyBase : MonoBehaviour, IDamageable
    {
        // --- Config (set by subclass or spawner) ---
        public string EnemyName = "Enemy";
        public int MaxHealth = 30;
        public int AttackDamage = 5;
        public float AttackRange = 2f;
        public float AttackCooldown = 1.5f;
        public float MoveSpeed = 2.5f;
        public float DetectRange = 12f;
        public float ChaseRange = 18f;
        public int XPReward = 15;
        public string LootId = "";
        public int LootCount = 1;
        public float LootChance = 0.5f;

        // --- Runtime ---
        public int CurrentHealth { get; private set; }
        public bool IsAlive => CurrentHealth > 0;

        private float _hitFlashTimer;
        private Material _material;
        private Color _originalColor;
        private EnemyAI _ai;

        public event System.Action<EnemyBase> OnDeath;

        public virtual void Init(Material sharedMat)
        {
            CurrentHealth = MaxHealth;
            _material = sharedMat;

            var renderer = GetComponentInChildren<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = _material;
                if (_material.HasProperty("_BaseColor"))
                    _originalColor = _material.GetColor("_BaseColor");
            }

            // Add AI
            _ai = gameObject.AddComponent<EnemyAI>();
            _ai.Init(this);

            // Add capsule collider for hit detection
            if (GetComponent<Collider>() == null)
            {
                var col = gameObject.AddComponent<CapsuleCollider>();
                col.height = 1.5f;
                col.radius = 0.4f;
                col.center = new Vector3(0, 0.75f, 0);
            }
        }

        public void TakeDamage(int amount, Vector3 sourcePosition)
        {
            if (!IsAlive) return;

            CurrentHealth -= amount;
            _hitFlashTimer = 0.15f;

            // Knockback
            Vector3 knockDir = (transform.position - sourcePosition).normalized;
            knockDir.y = 0;
            transform.position += knockDir * 0.5f;

            // Sound + VFX
            if (Audio.SoundManager.Instance != null)
                Audio.SoundManager.Instance.PlayEnemyHit();
            if (VFX.ParticleEffectManager.Instance != null)
                VFX.ParticleEffectManager.Instance.SpawnHitSparks(transform.position + Vector3.up * 0.8f, knockDir);

            // Alert AI
            if (_ai != null)
                _ai.OnHit(sourcePosition);

            if (CurrentHealth <= 0)
            {
                Die();
            }
        }

        private void Die()
        {
            // Drop loot
            if (!string.IsNullOrEmpty(LootId) && Random.value <= LootChance)
            {
                SpawnLootPickup();
            }

            // Grant XP to player
            var player = FindAnyObjectByType<PlayerStatsComponent>();
            if (player != null)
            {
                player.Stats.AddXP(XPReward);
                Debug.Log($"[Enemy] {EnemyName} defeated! +{XPReward} XP");
            }

            // Report kill to quest manager
            var questMgr = FindAnyObjectByType<Interaction.QuestManager>();
            if (questMgr != null)
                questMgr.ReportEnemyKill(EnemyName);

            // Sound + VFX
            if (Audio.SoundManager.Instance != null)
                Audio.SoundManager.Instance.PlayEnemyDeath();
            if (VFX.ParticleEffectManager.Instance != null)
                VFX.ParticleEffectManager.Instance.SpawnDeathPoof(transform.position + Vector3.up * 0.5f);

            OnDeath?.Invoke(this);

            // Death effect: shrink and destroy
            StartCoroutine(DeathAnimation());
        }

        private System.Collections.IEnumerator DeathAnimation()
        {
            // Disable AI and collider
            if (_ai != null) _ai.enabled = false;
            var col = GetComponent<Collider>();
            if (col != null) col.enabled = false;

            float timer = 0.5f;
            Vector3 startScale = transform.localScale;
            while (timer > 0)
            {
                timer -= Time.deltaTime;
                float t = timer / 0.5f;
                transform.localScale = startScale * t;
                transform.position += Vector3.down * Time.deltaTime * 2f;
                yield return null;
            }

            Destroy(gameObject);
        }

        private void SpawnLootPickup()
        {
            var item = ItemDatabase.Get(LootId);
            if (item == null) return;

            var lootGo = new GameObject($"Loot_{item.Name}");
            lootGo.transform.position = transform.position + Vector3.up * 0.5f;

            // Visual: small colored sphere
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

            // Remove sphere's collider, add trigger collider on parent
            var sphereCol = sphere.GetComponent<Collider>();
            if (sphereCol != null) Destroy(sphereCol);

            var trigger = lootGo.AddComponent<SphereCollider>();
            trigger.radius = 1.0f;
            trigger.isTrigger = true;

            var pickup = lootGo.AddComponent<LootPickup>();
            pickup.Init(item, LootCount);

            // Bob animation
            lootGo.AddComponent<LootBob>();
        }

        private void Update()
        {
            if (_hitFlashTimer > 0)
            {
                _hitFlashTimer -= Time.deltaTime;
                // Flash white on hit
                if (_material != null && _material.HasProperty("_BaseColor"))
                {
                    _material.SetColor("_BaseColor",
                        _hitFlashTimer > 0 ? Color.white : _originalColor);
                }
            }
        }
    }
}
