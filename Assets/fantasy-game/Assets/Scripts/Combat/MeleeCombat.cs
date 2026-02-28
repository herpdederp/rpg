// Assets/Scripts/Combat/MeleeCombat.cs
// =====================================
// Handles melee attack input, swing timing, hitbox detection, and damage.
// Reads stats from PlayerStatsComponent for damage calculation.

using UnityEngine;
using UnityEngine.InputSystem;
using FantasyGame.RPG;
using FantasyGame.UI;

namespace FantasyGame.Combat
{
    public class MeleeCombat : MonoBehaviour
    {
        // --- Config ---
        private const float ATTACK_RANGE = 2.5f;
        private const float ATTACK_ANGLE = 90f;        // degrees of the swing arc
        private const int STAMINA_COST = 10;
        private const float ATTACK_COOLDOWN = 0.5f;     // base cooldown (modified by dex)
        private const float ATTACK_ANIM_DURATION = 0.4f;

        // --- State ---
        public bool IsAttacking { get; private set; }
        public float AttackTimer { get; private set; }

        private PlayerStatsComponent _statsComponent;
        private InventoryComponent _inventoryComponent;
        private float _cooldownTimer;
        private Transform _swordTransform;

        // --- Events ---
        public System.Action OnAttackStart;
        public System.Action OnAttackHit;

        public void Init(PlayerStatsComponent stats, Transform swordMount)
        {
            _statsComponent = stats;
            _swordTransform = swordMount;
            _inventoryComponent = GetComponent<InventoryComponent>();
        }

        private void Update()
        {
            // Cooldown
            if (_cooldownTimer > 0f)
            {
                _cooldownTimer -= Time.deltaTime;
            }

            // Attack timer
            if (IsAttacking)
            {
                AttackTimer -= Time.deltaTime;

                // Check for hits at the peak of the swing (halfway through)
                if (AttackTimer <= ATTACK_ANIM_DURATION * 0.5f && AttackTimer > ATTACK_ANIM_DURATION * 0.5f - Time.deltaTime)
                {
                    PerformHitDetection();
                }

                if (AttackTimer <= 0f)
                {
                    IsAttacking = false;
                }
            }

            // Input (blocked when inventory or pause menu is open)
            if (InventoryUI.IsOpen || PauseMenu.IsPaused) return;
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                TryAttack();
            }
        }

        private void TryAttack()
        {
            if (IsAttacking) return;
            if (_cooldownTimer > 0f) return;

            if (_statsComponent == null || !_statsComponent.Stats.SpendStamina(STAMINA_COST))
            {
                Debug.Log("[MeleeCombat] Not enough stamina!");
                return;
            }

            // Start attack
            IsAttacking = true;
            float speedMod = _statsComponent != null ? _statsComponent.Stats.AttackSpeed : 1f;
            AttackTimer = ATTACK_ANIM_DURATION / speedMod;
            _cooldownTimer = ATTACK_COOLDOWN / speedMod;

            OnAttackStart?.Invoke();
        }

        private void PerformHitDetection()
        {
            // Base damage from stats (5 + STR * 2)
            float damage = _statsComponent != null ? _statsComponent.Stats.AttackDamage : 10f;

            // Add equipped weapon damage bonus
            if (_inventoryComponent != null && _inventoryComponent.Inventory.EquippedWeapon != null)
            {
                damage += _inventoryComponent.Inventory.EquippedWeapon.Value;
            }

            // Overlap sphere to find enemies in range
            Collider[] hits = Physics.OverlapSphere(transform.position + transform.forward * 1.0f, ATTACK_RANGE);

            bool hitSomething = false;
            foreach (var hit in hits)
            {
                if (hit.transform == transform) continue;
                if (hit.transform.IsChildOf(transform)) continue;

                // Check angle (only hit things in front of us)
                Vector3 dirToTarget = (hit.transform.position - transform.position).normalized;
                float angle = Vector3.Angle(transform.forward, dirToTarget);
                if (angle > ATTACK_ANGLE * 0.5f) continue;

                // Apply damage to anything with IDamageable
                var damageable = hit.GetComponent<IDamageable>();
                if (damageable != null)
                {
                    damageable.TakeDamage(Mathf.RoundToInt(damage), transform.position);
                    hitSomething = true;
                    Debug.Log($"[MeleeCombat] Hit {hit.name} for {damage:F0} damage (weapon bonus: {(_inventoryComponent?.Inventory.EquippedWeapon?.Value ?? 0)})!");
                }
            }

            if (hitSomething)
            {
                OnAttackHit?.Invoke();
            }
        }
    }

    /// <summary>
    /// Interface for anything that can take damage.
    /// </summary>
    public interface IDamageable
    {
        void TakeDamage(int amount, Vector3 sourcePosition);
        bool IsAlive { get; }
    }
}
