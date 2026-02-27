// Assets/Scripts/Interaction/Interactable.cs
// =============================================
// Base class for all interactable objects in the world.
// Shows a prompt when the player is nearby, triggers on E key.

using UnityEngine;
using UnityEngine.InputSystem;

namespace FantasyGame.Interaction
{
    public abstract class Interactable : MonoBehaviour
    {
        public string PromptText = "Interact";
        public float InteractRange = 3f;

        protected Transform _player;
        protected bool _playerInRange;
        private bool _used; // For single-use interactables

        public bool IsUsed => _used;

        protected virtual void Update()
        {
            if (_used) return;

            FindPlayer();
            if (_player == null) return;

            float dist = Vector3.Distance(transform.position, _player.position);
            _playerInRange = dist <= InteractRange;

            if (_playerInRange)
            {
                var kb = Keyboard.current;
                if (kb != null && kb.eKey.wasPressedThisFrame)
                {
                    OnInteract();
                }
            }
        }

        protected abstract void OnInteract();

        protected void MarkUsed()
        {
            _used = true;
        }

        protected virtual void OnGUI()
        {
            if (!_playerInRange || _used) return;

            var cam = UnityEngine.Camera.main;
            if (cam == null) return;

            Vector3 screenPos = cam.WorldToScreenPoint(transform.position + Vector3.up * 1.5f);
            if (screenPos.z < 0) return;

            float scale = Screen.height / 1080f;
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(16 * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            style.normal.textColor = new Color(1f, 0.9f, 0.6f);

            // Drop shadow
            float x = screenPos.x;
            float y = Screen.height - screenPos.y;
            var shadowStyle = new GUIStyle(style);
            shadowStyle.normal.textColor = new Color(0, 0, 0, 0.7f);
            GUI.Label(new Rect(x - 101, y - 11, 202, 32), $"[E] {PromptText}", shadowStyle);
            GUI.Label(new Rect(x - 100, y - 12, 200, 30), $"[E] {PromptText}", style);
        }

        protected void FindPlayer()
        {
            if (_player != null) return;
            var controller = FindAnyObjectByType<Player.ThirdPersonController>();
            if (controller != null)
                _player = controller.transform;
        }
    }
}
