// Assets/Scripts/Enemies/EnemyAI.cs
// ===================================
// Simple state-machine AI: Idle → Patrol → Chase → Attack.

using UnityEngine;
using FantasyGame.RPG;

namespace FantasyGame.Enemies
{
    public enum AIState { Idle, Patrol, Chase, Attack }

    public class EnemyAI : MonoBehaviour
    {
        private EnemyBase _enemy;
        private Transform _player;
        private AIState _state = AIState.Idle;

        // Patrol
        private Vector3 _patrolCenter;
        private Vector3 _patrolTarget;
        private float _patrolWaitTimer;
        private const float PATROL_RADIUS = 8f;
        private const float PATROL_WAIT_MIN = 2f;
        private const float PATROL_WAIT_MAX = 5f;

        // Attack
        private float _attackCooldownTimer;

        // Aggro
        private float _aggroTimer;

        // Ground tracking
        private float _groundY;
        private bool _hasGround;

        public void Init(EnemyBase enemy)
        {
            _enemy = enemy;
            _patrolCenter = transform.position;
            _groundY = transform.position.y;
            _hasGround = true;
            PickNewPatrolTarget();
        }

        public void OnHit(Vector3 sourcePos)
        {
            // Getting hit always triggers chase
            _aggroTimer = 10f;
            FindPlayer();
            if (_player != null)
                _state = AIState.Chase;
        }

        private void Update()
        {
            if (_enemy == null || !_enemy.IsAlive) return;

            FindPlayer();
            float distToPlayer = _player != null
                ? Vector3.Distance(transform.position, _player.position)
                : float.MaxValue;

            // State transitions
            switch (_state)
            {
                case AIState.Idle:
                    _patrolWaitTimer -= Time.deltaTime;
                    if (_patrolWaitTimer <= 0f)
                        _state = AIState.Patrol;
                    if (distToPlayer < _enemy.DetectRange)
                        _state = AIState.Chase;
                    break;

                case AIState.Patrol:
                    MoveToward(_patrolTarget, _enemy.MoveSpeed * 0.5f);
                    if (Vector3.Distance(transform.position, _patrolTarget) < 1f)
                    {
                        _state = AIState.Idle;
                        _patrolWaitTimer = Random.Range(PATROL_WAIT_MIN, PATROL_WAIT_MAX);
                        PickNewPatrolTarget();
                    }
                    if (distToPlayer < _enemy.DetectRange)
                        _state = AIState.Chase;
                    break;

                case AIState.Chase:
                    if (_player == null || distToPlayer > _enemy.ChaseRange)
                    {
                        _state = AIState.Patrol;
                        PickNewPatrolTarget();
                        break;
                    }
                    if (distToPlayer <= _enemy.AttackRange)
                    {
                        _state = AIState.Attack;
                        break;
                    }
                    MoveToward(_player.position, _enemy.MoveSpeed);
                    break;

                case AIState.Attack:
                    if (_player == null || !_enemy.IsAlive)
                    {
                        _state = AIState.Idle;
                        break;
                    }
                    // Face player
                    FaceTarget(_player.position);

                    if (distToPlayer > _enemy.AttackRange * 1.5f)
                    {
                        _state = AIState.Chase;
                        break;
                    }

                    _attackCooldownTimer -= Time.deltaTime;
                    if (_attackCooldownTimer <= 0f)
                    {
                        PerformAttack();
                        _attackCooldownTimer = _enemy.AttackCooldown;
                    }
                    break;
            }

            // Snap to ground every frame
            SnapToGround();
        }

        private void SnapToGround()
        {
            // Use RaycastAll from high above to skip enemy/player colliders
            Vector3 rayOrigin = new Vector3(transform.position.x, transform.position.y + 50f, transform.position.z);
            RaycastHit[] hits = Physics.RaycastAll(rayOrigin, Vector3.down, 100f);

            // Find the highest terrain/ground hit, skipping non-terrain colliders
            float bestY = float.NegativeInfinity;
            bool foundTerrain = false;

            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i].collider.isTrigger) continue;
                if (hits[i].collider.GetComponent<EnemyBase>() != null) continue;
                if (hits[i].collider.GetComponent<CharacterController>() != null) continue;

                if (hits[i].point.y > bestY)
                {
                    bestY = hits[i].point.y;
                    foundTerrain = true;
                }
            }

            if (foundTerrain)
            {
                _groundY = bestY;
                _hasGround = true;

                // Snap directly to ground — no lerp drift
                Vector3 pos = transform.position;
                pos.y = _groundY + 0.05f;
                transform.position = pos;
            }
            else if (_hasGround)
            {
                // No terrain hit — hold at last known ground height
                Vector3 pos = transform.position;
                pos.y = _groundY + 0.05f;
                transform.position = pos;
            }
        }

        private void MoveToward(Vector3 target, float speed)
        {
            Vector3 dir = (target - transform.position);
            dir.y = 0;
            if (dir.sqrMagnitude < 0.01f) return;
            dir.Normalize();

            transform.position += dir * speed * Time.deltaTime;
            FaceTarget(target);
        }

        private void FaceTarget(Vector3 target)
        {
            Vector3 dir = target - transform.position;
            dir.y = 0;
            if (dir.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    Quaternion.LookRotation(dir),
                    8f * Time.deltaTime
                );
            }
        }

        private void PerformAttack()
        {
            if (_player == null) return;

            float dist = Vector3.Distance(transform.position, _player.position);
            if (dist > _enemy.AttackRange) return;

            var playerStats = _player.GetComponent<PlayerStatsComponent>();
            if (playerStats != null)
            {
                playerStats.Stats.TakeDamage(_enemy.AttackDamage);
                Debug.Log($"[EnemyAI] {_enemy.EnemyName} hit player for {_enemy.AttackDamage} damage!");
            }
        }

        private void PickNewPatrolTarget()
        {
            Vector2 offset = Random.insideUnitCircle * PATROL_RADIUS;
            Vector3 candidate = _patrolCenter + new Vector3(offset.x, 0, offset.y);

            // Find terrain height at patrol target
            if (Physics.Raycast(candidate + Vector3.up * 30f, Vector3.down, out RaycastHit hit, 60f))
            {
                candidate.y = hit.point.y;
            }
            else
            {
                candidate.y = _patrolCenter.y;
            }

            _patrolTarget = candidate;
        }

        private void FindPlayer()
        {
            if (_player != null) return;
            var playerObj = GameObject.FindGameObjectWithTag("MainCamera");
            if (playerObj != null)
            {
                // The player is the Character object — find ThirdPersonController
                var controller = FindAnyObjectByType<Player.ThirdPersonController>();
                if (controller != null)
                    _player = controller.transform;
            }
        }
    }
}
