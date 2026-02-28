// Assets/Scripts/Dungeon/PushableBlock.cs
// ==========================================
// Pushable stone block for dungeon puzzles. Player pushes by walking
// into it. Uses proximity detection in Update() since CharacterController
// doesn't fire OnCollisionStay reliably with Rigidbody objects.
// The block has a non-trigger BoxCollider for plate detection.

using UnityEngine;

namespace FantasyGame.Dungeon
{
    public class PushableBlock : MonoBehaviour
    {
        private float _pushSpeed = 2.5f;
        private float _pushRange = 1.5f; // How close player must be to push
        private Transform _player;
        private CharacterController _playerCC;
        private Rigidbody _rb;
        private float _blockHalfSize = 0.6f;

        // Additional trigger collider for plate detection
        private BoxCollider _triggerCollider;

        public void Init()
        {
            _rb = GetComponent<Rigidbody>();
            if (_rb == null)
            {
                _rb = gameObject.AddComponent<Rigidbody>();
            }
            _rb.mass = 10f;
            _rb.linearDamping = 5f; // High drag so it stops quickly
            _rb.angularDamping = 10f;
            _rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.useGravity = false; // We freeze Y anyway

            // The main collider is not a trigger (for physics blocking)
            var mainCol = GetComponent<BoxCollider>();
            if (mainCol != null)
                mainCol.isTrigger = false;

            // Add a slightly larger trigger collider for pressure plate detection
            _triggerCollider = gameObject.AddComponent<BoxCollider>();
            _triggerCollider.isTrigger = true;
            _triggerCollider.size = new Vector3(0.9f, 0.9f, 0.9f); // Slightly smaller than visual
            _triggerCollider.center = Vector3.zero;
        }

        private void Update()
        {
            if (_player == null)
            {
                var controller = FindAnyObjectByType<Player.ThirdPersonController>();
                if (controller != null)
                {
                    _player = controller.transform;
                    _playerCC = _player.GetComponent<CharacterController>();
                }
                return;
            }

            if (_playerCC == null) return;

            // Check if player is close enough and moving toward the block
            Vector3 toBlock = transform.position - _player.position;
            toBlock.y = 0; // Only horizontal
            float dist = toBlock.magnitude;

            if (dist > _pushRange || dist < 0.1f) return;

            // Get player's movement direction
            Vector3 playerVel = _playerCC.velocity;
            playerVel.y = 0;
            if (playerVel.magnitude < 0.1f) return;

            // Check if player is moving toward the block
            Vector3 pushDir = toBlock.normalized;
            float dot = Vector3.Dot(playerVel.normalized, pushDir);

            if (dot > 0.5f) // Moving toward block
            {
                // Snap push direction to cardinal (N/S/E/W)
                Vector3 cardinal = SnapToCardinal(pushDir);

                // Move the block
                Vector3 newPos = transform.position + cardinal * _pushSpeed * Time.deltaTime;
                _rb.MovePosition(newPos);
            }
        }

        private Vector3 SnapToCardinal(Vector3 dir)
        {
            if (Mathf.Abs(dir.x) > Mathf.Abs(dir.z))
                return new Vector3(Mathf.Sign(dir.x), 0, 0);
            else
                return new Vector3(0, 0, Mathf.Sign(dir.z));
        }
    }
}
