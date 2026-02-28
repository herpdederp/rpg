// Assets/Scripts/Dungeon/DungeonEntrance.cs
// =============================================
// Interactable cave entrance on the overworld. Press E to generate
// and enter the dungeon interior.

using UnityEngine;

namespace FantasyGame.Dungeon
{
    public class DungeonEntrance : Interaction.Interactable
    {
        public void Init()
        {
            PromptText = "Enter Dungeon";
            InteractRange = 3.5f;
        }

        protected override void OnInteract()
        {
            if (DungeonManager.IsInDungeon) return;
            DungeonManager.Instance.EnterDungeon();
        }
    }
}
