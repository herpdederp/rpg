// Assets/Scripts/RPG/PotionInput.cs
// ==================================
// Handles potion usage input. Press Q to use a small potion.

using UnityEngine;
using UnityEngine.InputSystem;

namespace FantasyGame.RPG
{
    public class PotionInput : MonoBehaviour
    {
        private Inventory _inventory;
        private PlayerStats _stats;

        public void Init(Inventory inventory, PlayerStats stats)
        {
            _inventory = inventory;
            _stats = stats;
        }

        private void Update()
        {
            if (_inventory == null || _stats == null) return;

            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.qKey.wasPressedThisFrame)
            {
                // Try large potion first, then small
                if (!_inventory.UsePotion("potion_large", _stats))
                {
                    if (!_inventory.UsePotion("potion_small", _stats))
                    {
                        Debug.Log("[PotionInput] No potions available!");
                    }
                }
            }
        }
    }
}
