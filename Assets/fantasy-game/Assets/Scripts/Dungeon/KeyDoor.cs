// Assets/Scripts/Dungeon/KeyDoor.cs
// ===================================
// Locked door requiring a specific key item to unlock.
// Extends Interactable for E-key interaction.

using UnityEngine;
using FantasyGame.RPG;

namespace FantasyGame.Dungeon
{
    public class KeyDoor : Interaction.Interactable
    {
        private string _requiredKeyId;
        private string _keyDisplayName;
        private PuzzleDoor _linkedDoor;
        private float _messageTimer;
        private string _messageText;

        public void Init(string requiredKeyId, string keyDisplayName, PuzzleDoor linkedDoor)
        {
            _requiredKeyId = requiredKeyId;
            _keyDisplayName = keyDisplayName;
            _linkedDoor = linkedDoor;

            PromptText = "Unlock Door";
            InteractRange = 3f;
        }

        protected override void OnInteract()
        {
            var inventory = FindAnyObjectByType<InventoryComponent>();
            if (inventory == null) return;

            if (inventory.Inventory.HasItem(_requiredKeyId))
            {
                // Unlock!
                inventory.Inventory.RemoveItem(_requiredKeyId);
                if (_linkedDoor != null)
                {
                    _linkedDoor.Open();
                    _linkedDoor.Lock();
                }
                MarkUsed();

                _messageText = "Door Unlocked!";
                _messageTimer = 2f;

                if (Audio.SoundManager.Instance != null)
                    Audio.SoundManager.Instance.PlayItemPickup();

                Debug.Log($"[KeyDoor] Unlocked with {_requiredKeyId}");
            }
            else
            {
                // Show "requires key" message
                _messageText = $"Requires {_keyDisplayName}";
                _messageTimer = 2f;
                Debug.Log($"[KeyDoor] Locked — needs {_requiredKeyId}");
            }
        }

        protected override void Update()
        {
            base.Update();
            if (_messageTimer > 0)
                _messageTimer -= Time.deltaTime;
        }

        protected override void OnGUI()
        {
            base.OnGUI();

            // Show message feedback
            if (_messageTimer > 0 && !string.IsNullOrEmpty(_messageText))
            {
                float alpha = Mathf.Clamp01(_messageTimer);
                float scale = Screen.height / 1080f;
                var style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = Mathf.RoundToInt(20 * scale),
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };

                bool isLocked = !IsUsed;
                style.normal.textColor = isLocked
                    ? new Color(1f, 0.3f, 0.3f, alpha)
                    : new Color(0.3f, 1f, 0.3f, alpha);

                GUI.Label(new Rect(0, Screen.height * 0.35f, Screen.width, 40 * scale),
                    _messageText, style);
            }
        }
    }
}
