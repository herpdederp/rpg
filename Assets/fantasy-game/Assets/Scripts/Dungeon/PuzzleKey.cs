// Assets/Scripts/Dungeon/PuzzleKey.cs
// =====================================
// Colored key pickup. Extends Interactable.
// Adds a quest item to the player's inventory when picked up.

using UnityEngine;
using FantasyGame.RPG;

namespace FantasyGame.Dungeon
{
    public class PuzzleKey : Interaction.Interactable
    {
        private string _keyItemId;
        private Color _keyColor;
        private string _keyName;
        private GameObject _visual;

        public void Init(string keyItemId, string keyName, Color keyColor, GameObject visual)
        {
            _keyItemId = keyItemId;
            _keyName = keyName;
            _keyColor = keyColor;
            _visual = visual;

            PromptText = $"Pick Up {keyName}";
            InteractRange = 2.5f;
        }

        protected override void OnInteract()
        {
            MarkUsed();

            var inventory = FindAnyObjectByType<InventoryComponent>();
            if (inventory != null)
            {
                var item = ItemDatabase.Get(_keyItemId);
                if (item != null)
                {
                    inventory.Inventory.AddItem(item);
                    Debug.Log($"[PuzzleKey] Picked up: {_keyName}");
                }
            }

            // Hide the key visual
            if (_visual != null)
                _visual.SetActive(false);

            if (Audio.SoundManager.Instance != null)
                Audio.SoundManager.Instance.PlayChestOpen();

            if (VFX.ParticleEffectManager.Instance != null)
                VFX.ParticleEffectManager.Instance.SpawnChestSparkle(transform.position + Vector3.up * 0.3f);
        }
    }
}
