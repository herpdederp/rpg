// Assets/Scripts/RPG/PlayerStatsComponent.cs
// ============================================
// MonoBehaviour wrapper for PlayerStats. Attach to the player GameObject.

using UnityEngine;

namespace FantasyGame.RPG
{
    public class PlayerStatsComponent : MonoBehaviour
    {
        public PlayerStats Stats { get; private set; } = new PlayerStats();

        public void Init()
        {
            Stats.Initialize();
        }

        private void Update()
        {
            Stats.Update(Time.deltaTime);
        }
    }
}
