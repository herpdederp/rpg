// Assets/Scripts/Camera/ThirdPersonCamera.cs
// =============================================
// Orbital camera that follows the character.
// Mouse X/Y to orbit, scroll wheel to zoom.
// Handles collision with environment via spherecast.

using UnityEngine;
using UnityEngine.InputSystem;

namespace FantasyGame.Camera
{
    public class ThirdPersonCamera : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 targetOffset = new Vector3(0, 1.4f, 0);

        [Header("Orbit")]
        [SerializeField] private float distance = 5f;
        [SerializeField] private float minDistance = 1.5f;
        [SerializeField] private float maxDistance = 15f;
        [SerializeField] private float scrollSpeed = 3f;

        [Header("Rotation")]
        [SerializeField] private float mouseSensitivity = 3f;
        [SerializeField] private float minPitch = -30f;
        [SerializeField] private float maxPitch = 75f;
        [SerializeField] private float smoothSpeed = 12f;

        [Header("Collision")]
        [SerializeField] private float collisionRadius = 0.25f;
        [SerializeField] private LayerMask collisionLayers = ~0;

        // Runtime state
        private float _yaw;
        private float _pitch = 15f;
        private float _currentDistance;
        private Vector3 _smoothVelocity;

        /// <summary>
        /// Called by GltfBootstrap after instantiation.
        /// </summary>
        public void Init(Transform followTarget)
        {
            target = followTarget;

            // Initialise orbit angles from current camera orientation
            Vector3 angles = transform.eulerAngles;
            _yaw = angles.y;
            _pitch = angles.x;
            _currentDistance = distance;
        }

        private void LateUpdate()
        {
            if (target == null) return;

            var mouse = Mouse.current;
            if (mouse != null)
            {
                // --- Input ---
                Vector2 delta = mouse.delta.ReadValue();
                _yaw += delta.x * mouseSensitivity * 0.1f;
                _pitch -= delta.y * mouseSensitivity * 0.1f;
                _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);

                // Scroll zoom
                float scroll = mouse.scroll.ReadValue().y;
                distance -= scroll * scrollSpeed * 0.01f;
                distance = Mathf.Clamp(distance, minDistance, maxDistance);
            }

            // --- Compute desired position ---
            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0);
            Vector3 focusPoint = target.position + targetOffset;
            Vector3 desiredPosition = focusPoint - rotation * Vector3.forward * distance;

            // --- Collision check ---
            _currentDistance = distance;
            if (Physics.SphereCast(
                    focusPoint,
                    collisionRadius,
                    (desiredPosition - focusPoint).normalized,
                    out RaycastHit hit,
                    distance,
                    collisionLayers))
            {
                _currentDistance = Mathf.Max(hit.distance - collisionRadius, minDistance);
            }

            Vector3 finalPosition = focusPoint - rotation * Vector3.forward * _currentDistance;

            // --- Apply ---
            transform.position = Vector3.SmoothDamp(
                transform.position,
                finalPosition,
                ref _smoothVelocity,
                1f / smoothSpeed
            );
            transform.LookAt(focusPoint);
        }
    }
}
