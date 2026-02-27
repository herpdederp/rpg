// Assets/Scripts/RPG/StatPointInput.cs
// =====================================
// Handles keyboard input for spending stat points.
// 1 = Strength, 2 = Dexterity, 3 = Vitality

using UnityEngine;
using UnityEngine.InputSystem;

namespace FantasyGame.RPG
{
    public class StatPointInput : MonoBehaviour
    {
        private PlayerStats _stats;

        public void Init(PlayerStats stats)
        {
            _stats = stats;
        }

        private void Update()
        {
            if (_stats == null || _stats.StatPoints <= 0) return;

            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.digit1Key.wasPressedThisFrame)
            {
                _stats.SpendStatPoint(StatType.Strength);
                Debug.Log($"[Stats] STR -> {_stats.Strength}");
            }
            else if (keyboard.digit2Key.wasPressedThisFrame)
            {
                _stats.SpendStatPoint(StatType.Dexterity);
                Debug.Log($"[Stats] DEX -> {_stats.Dexterity}");
            }
            else if (keyboard.digit3Key.wasPressedThisFrame)
            {
                _stats.SpendStatPoint(StatType.Vitality);
                Debug.Log($"[Stats] VIT -> {_stats.Vitality}");
            }
        }
    }
}
