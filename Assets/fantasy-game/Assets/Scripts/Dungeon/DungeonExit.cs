// Assets/Scripts/Dungeon/DungeonExit.cs
// ========================================
// Interactable exit portal inside the dungeon. Press E to teleport
// back to the entrance surface and destroy the dungeon interior.

using UnityEngine;

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
            DungeonManager.Instance.ExitDungeon();
        }
    }
}
