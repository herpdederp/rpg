// Assets/Scripts/RPG/PlayerStats.cs
// ==================================
// Core RPG stat system: attributes, HP, stamina, XP, leveling.
// Purely data-driven — no MonoBehaviour. Attach via PlayerStatsComponent.

using System;
using UnityEngine;

namespace FantasyGame.RPG
{
    [Serializable]
    public class PlayerStats
    {
        // --- Base Attributes ---
        public int Strength = 5;
        public int Dexterity = 5;
        public int Vitality = 5;

        // --- Derived Stats ---
        public int MaxHealth => 50 + Vitality * 10;
        public int MaxStamina => 30 + Dexterity * 5;
        public float AttackDamage => 5f + Strength * 2f;
        public float AttackSpeed => 1f + Dexterity * 0.05f;
        public float MoveSpeedBonus => Dexterity * 0.1f;

        // --- Runtime State ---
        public int CurrentHealth { get; private set; }
        public int CurrentStamina { get; private set; }
        public int Level { get; private set; } = 1;
        public int XP { get; private set; }
        public int XPToNextLevel => 50 + (Level - 1) * 30;
        public int StatPoints { get; private set; }

        // --- Events ---
        public event Action<int, int> OnHealthChanged;    // current, max
        public event Action<int, int> OnStaminaChanged;   // current, max
        public event Action<int, int> OnXPChanged;        // current, toNext
        public event Action<int> OnLevelUp;               // newLevel
        public event Action OnDeath;

        // --- Stamina Regen ---
        private float _staminaRegenTimer;
        private const float STAMINA_REGEN_DELAY = 1.5f;
        private const float STAMINA_REGEN_RATE = 8f;

        public void Initialize()
        {
            CurrentHealth = MaxHealth;
            CurrentStamina = MaxStamina;
        }

        public void Update(float deltaTime)
        {
            // Stamina regeneration
            _staminaRegenTimer += deltaTime;
            if (_staminaRegenTimer >= STAMINA_REGEN_DELAY && CurrentStamina < MaxStamina)
            {
                CurrentStamina = Mathf.Min(MaxStamina,
                    CurrentStamina + Mathf.RoundToInt(STAMINA_REGEN_RATE * deltaTime));
                OnStaminaChanged?.Invoke(CurrentStamina, MaxStamina);
            }
        }

        public void TakeDamage(int amount)
        {
            if (CurrentHealth <= 0) return;

            CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
            OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);

            if (CurrentHealth <= 0)
                OnDeath?.Invoke();
        }

        public void Heal(int amount)
        {
            CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + amount);
            OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);
        }

        /// <summary>
        /// Try to spend stamina. Returns false if not enough.
        /// </summary>
        public bool SpendStamina(int amount)
        {
            if (CurrentStamina < amount) return false;
            CurrentStamina -= amount;
            _staminaRegenTimer = 0f; // reset regen delay
            OnStaminaChanged?.Invoke(CurrentStamina, MaxStamina);
            return true;
        }

        public void AddXP(int amount)
        {
            XP += amount;
            OnXPChanged?.Invoke(XP, XPToNextLevel);

            while (XP >= XPToNextLevel)
            {
                XP -= XPToNextLevel;
                Level++;
                StatPoints += 3;
                CurrentHealth = MaxHealth; // full heal on level up
                CurrentStamina = MaxStamina;
                OnLevelUp?.Invoke(Level);
                OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);
                OnStaminaChanged?.Invoke(CurrentStamina, MaxStamina);
                OnXPChanged?.Invoke(XP, XPToNextLevel);
                Debug.Log($"[PlayerStats] LEVEL UP! Now level {Level}. +3 stat points.");
            }
        }

        public bool SpendStatPoint(StatType stat)
        {
            if (StatPoints <= 0) return false;
            StatPoints--;

            switch (stat)
            {
                case StatType.Strength: Strength++; break;
                case StatType.Dexterity: Dexterity++; break;
                case StatType.Vitality:
                    int oldMax = MaxHealth;
                    Vitality++;
                    // Heal the HP gained from vitality increase
                    CurrentHealth += MaxHealth - oldMax;
                    OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);
                    break;
            }
            return true;
        }
    }

    public enum StatType { Strength, Dexterity, Vitality }
}
