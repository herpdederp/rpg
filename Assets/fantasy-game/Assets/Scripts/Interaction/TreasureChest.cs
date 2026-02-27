// Assets/Scripts/Interaction/TreasureChest.cs
// ==============================================
// Openable chest that gives random loot. Single use.
// Lid animates open, sparkle effect, items added to inventory.

using UnityEngine;
using FantasyGame.RPG;

namespace FantasyGame.Interaction
{
    public class TreasureChest : Interactable
    {
        private Transform _lid;
        private float _openAngle;
        private bool _opening;
        private float _openTimer;

        // Loot table
        private static readonly string[] POSSIBLE_LOOT = {
            "potion_small", "potion_small", "potion_small",
            "potion_large",
            "sword_iron",
            "slime_gel", "bone_fragment", "wolf_pelt"
        };

        public void Init(Transform lid)
        {
            _lid = lid;
            PromptText = "Open Chest";
            InteractRange = 2.5f;
        }

        protected override void OnInteract()
        {
            MarkUsed();
            _opening = true;
            _openTimer = 0f;

            // Give loot
            var inventory = FindAnyObjectByType<InventoryComponent>();
            if (inventory != null)
            {
                // Give 1-3 random items
                int itemCount = Random.Range(1, 4);
                for (int i = 0; i < itemCount; i++)
                {
                    string itemId = POSSIBLE_LOOT[Random.Range(0, POSSIBLE_LOOT.Length)];
                    var item = ItemDatabase.Get(itemId);
                    if (item != null)
                    {
                        inventory.Inventory.AddItem(item);
                        Debug.Log($"[TreasureChest] Found: {item.Name}");
                    }
                }
            }

            Debug.Log("[TreasureChest] Chest opened!");
        }

        protected override void Update()
        {
            base.Update();

            // Animate lid opening
            if (_opening && _lid != null)
            {
                _openTimer += Time.deltaTime;
                _openAngle = Mathf.Lerp(_openAngle, -110f, 5f * Time.deltaTime);
                _lid.localRotation = Quaternion.Euler(_openAngle, 0, 0);

                if (_openAngle < -105f)
                    _opening = false;
            }
        }
    }
}
