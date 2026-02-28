// Assets/Scripts/Player/ThirdPersonController.cs
// =================================================
// Simple third-person controller driven by CharacterController.
// Movement is camera-relative: W = forward from camera's perspective.
// Exposes CurrentSpeed, IsGrounded, IsJumping for CharacterAnimator.

using UnityEngine;
using UnityEngine.InputSystem;
using FantasyGame.UI;

namespace FantasyGame.Player
{
    [RequireComponent(typeof(CharacterController))]
    public class ThirdPersonController : MonoBehaviour
    {
        // -- Tuning --
        [Header("Movement")]
        [SerializeField] private float walkSpeed = 4f;
        [SerializeField] private float runSpeed = 8f;
        [SerializeField] private float rotationSmoothing = 10f;
        [SerializeField] private float gravity = -20f;

        [Header("Jump")]
        [SerializeField] private float jumpHeight = 1.2f;

        // -- State (read by CharacterAnimator) --
        public float CurrentSpeed { get; private set; }
        public bool IsGrounded => _isGrounded;
        public bool IsJumping { get; private set; }

        // -- Runtime --
        private CharacterController _cc;
        private Transform _cameraTransform;
        private Vector3 _velocity;
        private bool _isGrounded;
        private float _characterHeight;
        private float _fallTimer;
        private int _graceFrames = 10; // force grounded for first N frames

        /// <summary>
        /// Called by GltfBootstrap after the CharacterController is configured.
        /// </summary>
        public void Init(CharacterController cc, float height)
        {
            _cc = cc;
            _characterHeight = height;
        }

        /// <summary>
        /// Reset grounding state after respawn/teleport.
        /// </summary>
        public void ResetAfterRespawn()
        {
            _graceFrames = 10;
            _velocity = Vector3.zero;
            _fallTimer = 0f;
            _isGrounded = true;
            IsJumping = false;
        }

        private void Start()
        {
            if (_cc == null)
                _cc = GetComponent<CharacterController>();

            // Lock cursor for mouse-look
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void Update()
        {
            if (_cc == null) return;

            CacheCamera();

            // --- Block input when UI is open ---
            bool inputBlocked = UI.InventoryUI.IsOpen || UI.PauseMenu.IsPaused;

            // --- Ground check (CC check + raycast fallback for uneven terrain) ---
            // Grace period: force grounded for first frames to let CC settle onto terrain
            if (_graceFrames > 0)
            {
                _graceFrames--;
                _isGrounded = true;
                _velocity.y = -2f;
            }
            else
            {
                _isGrounded = _cc.isGrounded || CheckGroundRaycast();
            }

            // Reset vertical velocity when grounded
            if (_isGrounded)
            {
                if (_velocity.y < 0f)
                    _velocity.y = -2f;
                IsJumping = false;
                _fallTimer = 0f;
            }
            else
            {
                _fallTimer += Time.deltaTime;
                // Safety: if falling for too long, teleport back to terrain surface
                if (_fallTimer > 3f)
                {
                    TeleportToGround();
                    return;
                }
            }

            // --- Horizontal input ---
            Vector3 moveDir = Vector3.zero;
            float speed = 0f;

            var keyboard = Keyboard.current;
            if (keyboard != null && !inputBlocked)
            {
                float h = 0f;
                float v = 0f;
                if (keyboard.dKey.isPressed) h += 1f;
                if (keyboard.aKey.isPressed) h -= 1f;
                if (keyboard.wKey.isPressed) v += 1f;
                if (keyboard.sKey.isPressed) v -= 1f;

                if (!Mathf.Approximately(h, 0f) || !Mathf.Approximately(v, 0f))
                {
                    Vector3 forward = _cameraTransform != null
                        ? Vector3.ProjectOnPlane(_cameraTransform.forward, Vector3.up).normalized
                        : transform.forward;
                    Vector3 right = _cameraTransform != null
                        ? Vector3.ProjectOnPlane(_cameraTransform.right, Vector3.up).normalized
                        : transform.right;

                    moveDir = (forward * v + right * h).normalized;
                    speed = keyboard.leftShiftKey.isPressed ? runSpeed : walkSpeed;
                }

                // --- Jump ---
                if (_isGrounded && keyboard.spaceKey.wasPressedThisFrame)
                {
                    _velocity.y = Mathf.Sqrt(2f * Mathf.Abs(gravity) * jumpHeight);
                    IsJumping = true;
                }
            }

            CurrentSpeed = speed;

            // --- Apply gravity (clamped so we never fall too fast) ---
            _velocity.y += gravity * Time.deltaTime;
            _velocity.y = Mathf.Max(_velocity.y, -20f); // terminal velocity clamp

            // --- Single Move call combining horizontal + vertical ---
            Vector3 horizontalMove = moveDir * speed * Time.deltaTime;
            Vector3 verticalMove = new Vector3(0f, _velocity.y * Time.deltaTime, 0f);
            _cc.Move(horizontalMove + verticalMove);

            // --- Rotate character to face movement direction ---
            if (moveDir.sqrMagnitude > 0.01f)
            {
                Quaternion targetRot = Quaternion.LookRotation(moveDir, Vector3.up);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    targetRot,
                    rotationSmoothing * Time.deltaTime
                );
            }
        }

        /// <summary>
        /// Raycast fallback for ground detection on uneven procedural terrain.
        /// Uses a simple raycast from slightly above feet straight down.
        /// </summary>
        private bool CheckGroundRaycast()
        {
            // CC center is at (0, height/2, 0), so the bottom of the capsule
            // is at transform.position.y + center.y - height/2.
            // For a correctly configured CC this is approximately transform.position.y.
            float castOriginY = 0.5f; // start ray from knee height
            float castDist = castOriginY + 0.3f; // check 0.3m below feet
            Vector3 origin = transform.position + Vector3.up * castOriginY;
            return Physics.Raycast(origin, Vector3.down, castDist);
        }

        /// <summary>
        /// Emergency teleport: if character falls through the world, snap back to terrain.
        /// </summary>
        private void TeleportToGround()
        {
            Vector3 pos = transform.position;
            Vector3 rayOrigin = new Vector3(pos.x, 200f, pos.z);
            _cc.enabled = false;
            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 400f))
            {
                transform.position = hit.point + Vector3.up * 0.5f;
            }
            else
            {
                // No terrain found at all - reset to origin
                transform.position = new Vector3(0f, 50f, 0f);
            }
            _velocity = Vector3.zero;
            IsJumping = false;
            _isGrounded = true;
            _fallTimer = 0f;
            _cc.enabled = true;
            Debug.LogWarning("[ThirdPersonController] Fell through world — teleported back to ground.");
        }

        private void CacheCamera()
        {
            if (_cameraTransform == null && UnityEngine.Camera.main != null)
                _cameraTransform = UnityEngine.Camera.main.transform;
        }
    }
}
