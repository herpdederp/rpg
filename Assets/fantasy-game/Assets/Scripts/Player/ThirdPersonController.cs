// Assets/Scripts/Player/ThirdPersonController.cs
// =================================================
// Simple third-person controller driven by CharacterController.
// Movement is camera-relative: W = forward from camera's perspective.
// Exposes CurrentSpeed, IsGrounded, IsJumping for CharacterAnimator.

using UnityEngine;
using UnityEngine.InputSystem;

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

        /// <summary>
        /// Called by GltfBootstrap after the CharacterController is configured.
        /// </summary>
        public void Init(CharacterController cc, float height)
        {
            _cc = cc;
            _characterHeight = height;
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
            CacheCamera();
            HandleMovement();
            HandleJump();
            ApplyGravity();
        }

        private void CacheCamera()
        {
            if (_cameraTransform == null && UnityEngine.Camera.main != null)
                _cameraTransform = UnityEngine.Camera.main.transform;
        }

        private void HandleMovement()
        {
            _isGrounded = _cc.isGrounded;

            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                CurrentSpeed = 0f;
                return;
            }

            // Raw input from WASD
            float h = 0f;
            float v = 0f;
            if (keyboard.dKey.isPressed) h += 1f;
            if (keyboard.aKey.isPressed) h -= 1f;
            if (keyboard.wKey.isPressed) v += 1f;
            if (keyboard.sKey.isPressed) v -= 1f;

            if (Mathf.Approximately(h, 0f) && Mathf.Approximately(v, 0f))
            {
                CurrentSpeed = 0f;
                return;
            }

            // Camera-relative direction
            Vector3 forward = _cameraTransform != null
                ? Vector3.ProjectOnPlane(_cameraTransform.forward, Vector3.up).normalized
                : transform.forward;
            Vector3 right = _cameraTransform != null
                ? Vector3.ProjectOnPlane(_cameraTransform.right, Vector3.up).normalized
                : transform.right;

            Vector3 moveDir = (forward * v + right * h).normalized;

            // Speed (hold Shift to run)
            float speed = keyboard.leftShiftKey.isPressed ? runSpeed : walkSpeed;

            CurrentSpeed = speed;
            _cc.Move(moveDir * speed * Time.deltaTime);

            // Rotate character to face movement direction
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

        private void HandleJump()
        {
            var keyboard = Keyboard.current;
            if (_isGrounded && keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
            {
                // v = sqrt(2 * |gravity| * height)
                _velocity.y = Mathf.Sqrt(2f * Mathf.Abs(gravity) * jumpHeight);
                IsJumping = true;
            }
        }

        private void ApplyGravity()
        {
            if (_isGrounded && _velocity.y < 0f)
            {
                IsJumping = false;
                _velocity.y = -2f; // small downward to stay grounded
            }

            _velocity.y += gravity * Time.deltaTime;
            _cc.Move(_velocity * Time.deltaTime);
        }
    }
}
