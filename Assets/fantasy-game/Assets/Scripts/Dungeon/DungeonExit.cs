// Assets/Scripts/Dungeon/DungeonExit.cs
// ========================================
// Interactable exit portal inside the dungeon. Press E to teleport
// back to the entrance surface. Grants dungeon_exit_token for quest.

using UnityEngine;
using FantasyGame.RPG;

namespace FantasyGame.Dungeon
{
    public class DungeonExit : Interaction.Interactable
    {
        public void Init()
        {
            PromptText = "Leave Dungeon";
            InteractRange = 3f;
        }

        protected override void OnInteract()
        {
            if (!DungeonManager.IsInDungeon) return;

            // Grant dungeon completion token
            var inventory = FindAnyObjectByType<InventoryComponent>();
            if (inventory != null)
            {
                var token = ItemDatabase.Get("dungeon_exit_token");
                if (token != null && !inventory.Inventory.HasItem("dungeon_exit_token"))
                {
                    inventory.Inventory.AddItem(token);
                    Debug.Log("[DungeonExit] Granted dungeon_exit_token.");
                }
            }

            MarkUsed();
            DungeonManager.Instance.ExitDungeon();
        }
    }
}
