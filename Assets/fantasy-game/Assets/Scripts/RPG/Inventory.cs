// Assets/Scripts/RPG/Inventory.cs
// ================================
// Simple slot-based inventory with item types and equip support.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace FantasyGame.RPG
{
    [Serializable]
    public class ItemData
    {
        public string Id;
        public string Name;
        public ItemType Type;
        public int Value;               // damage for weapons, heal amount for potions, sell value for misc
        public string Description;
        public Color DisplayColor;      // for UI rendering

        public ItemData(string id, string name, ItemType type, int value, string desc, Color color)
        {
            Id = id;
            Name = name;
            Type = type;
            Value = value;
            Description = desc;
            DisplayColor = color;
        }
    }

    public enum ItemType
    {
        Weapon,
        Potion,
        Material,
        Quest
    }

    [Serializable]
    public class InventorySlot
    {
        public ItemData Item;
        public int Count;

        public InventorySlot(ItemData item, int count)
        {
            Item = item;
            Count = count;
        }
    }

    public class Inventory
    {
        public const int MAX_SLOTS = 20;

        public List<InventorySlot> Slots { get; private set; } = new List<InventorySlot>();
        public ItemData EquippedWeapon { get; private set; }

        public event Action OnInventoryChanged;
        public event Action<ItemData> OnItemEquipped;

        /// <summary>
        /// Add item to inventory. Stacks if same item exists. Returns false if full.
        /// </summary>
        public bool AddItem(ItemData item, int count = 1)
        {
            // Try to stack with existing
            foreach (var slot in Slots)
            {
                if (slot.Item.Id == item.Id)
                {
                    slot.Count += count;
                    OnInventoryChanged?.Invoke();
                    Debug.Log($"[Inventory] Stacked +{count} {item.Name} (total: {slot.Count})");
                    return true;
                }
            }

            // Add to new slot
            if (Slots.Count >= MAX_SLOTS)
            {
                Debug.Log("[Inventory] Inventory full!");
                return false;
            }

            Slots.Add(new InventorySlot(item, count));
            OnInventoryChanged?.Invoke();
            Debug.Log($"[Inventory] Added {item.Name} x{count}");
            return true;
        }

        /// <summary>
        /// Remove item from inventory. Returns false if not enough.
        /// </summary>
        public bool RemoveItem(string itemId, int count = 1)
        {
            for (int i = 0; i < Slots.Count; i++)
            {
                if (Slots[i].Item.Id == itemId)
                {
                    if (Slots[i].Count < count) return false;
                    Slots[i].Count -= count;
                    if (Slots[i].Count <= 0)
                        Slots.RemoveAt(i);
                    OnInventoryChanged?.Invoke();
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Equip a weapon from inventory.
        /// </summary>
        public bool EquipWeapon(string itemId)
        {
            foreach (var slot in Slots)
            {
                if (slot.Item.Id == itemId && slot.Item.Type == ItemType.Weapon)
                {
                    EquippedWeapon = slot.Item;
                    OnItemEquipped?.Invoke(slot.Item);
                    Debug.Log($"[Inventory] Equipped: {slot.Item.Name}");
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Use a potion (heal). Returns false if none available.
        /// </summary>
        public bool UsePotion(string itemId, PlayerStats stats)
        {
            foreach (var slot in Slots)
            {
                if (slot.Item.Id == itemId && slot.Item.Type == ItemType.Potion)
                {
                    stats.Heal(slot.Item.Value);
                    RemoveItem(itemId);
                    Debug.Log($"[Inventory] Used {slot.Item.Name}, healed {slot.Item.Value} HP");
                    return true;
                }
            }
            return false;
        }

        public bool HasItem(string itemId)
        {
            foreach (var slot in Slots)
                if (slot.Item.Id == itemId) return true;
            return false;
        }

        public int GetItemCount(string itemId)
        {
            foreach (var slot in Slots)
                if (slot.Item.Id == itemId) return slot.Count;
            return 0;
        }
    }

    /// <summary>
    /// MonoBehaviour wrapper for inventory.
    /// </summary>
    public class InventoryComponent : MonoBehaviour
    {
        public Inventory Inventory { get; private set; } = new Inventory();

        public void Init()
        {
            // Give player a starting sword
            var starterSword = ItemDatabase.Get("sword_wooden");
            if (starterSword != null)
            {
                Inventory.AddItem(starterSword);
                Inventory.EquipWeapon(starterSword.Id);
            }

            // A couple of starter potions
            var potion = ItemDatabase.Get("potion_small");
            if (potion != null)
                Inventory.AddItem(potion, 3);
        }
    }

    /// <summary>
    /// Simple static item database. All items defined in code.
    /// </summary>
    public static class ItemDatabase
    {
        private static Dictionary<string, ItemData> _items;

        private static void InitIfNeeded()
        {
            if (_items != null) return;
            _items = new Dictionary<string, ItemData>();

            Register(new ItemData("sword_wooden", "Wooden Sword", ItemType.Weapon, 8,
                "A simple wooden training sword.", new Color(0.6f, 0.4f, 0.2f)));
            Register(new ItemData("sword_iron", "Iron Sword", ItemType.Weapon, 15,
                "A sturdy iron blade.", new Color(0.7f, 0.7f, 0.75f)));
            Register(new ItemData("sword_magic", "Enchanted Blade", ItemType.Weapon, 25,
                "A blade humming with magical energy.", new Color(0.4f, 0.6f, 1.0f)));

            Register(new ItemData("potion_small", "Small Potion", ItemType.Potion, 30,
                "Restores 30 HP.", new Color(0.9f, 0.2f, 0.3f)));
            Register(new ItemData("potion_large", "Large Potion", ItemType.Potion, 75,
                "Restores 75 HP.", new Color(0.9f, 0.1f, 0.2f)));

            Register(new ItemData("slime_gel", "Slime Gel", ItemType.Material, 5,
                "Gooey residue from a slime.", new Color(0.3f, 0.8f, 0.3f)));
            Register(new ItemData("bone_fragment", "Bone Fragment", ItemType.Material, 8,
                "A piece of ancient bone.", new Color(0.9f, 0.9f, 0.8f)));
            Register(new ItemData("wolf_pelt", "Wolf Pelt", ItemType.Material, 12,
                "Thick fur from a wild wolf.", new Color(0.5f, 0.45f, 0.4f)));
        }

        public static void Register(ItemData item) { InitIfNeeded(); _items[item.Id] = item; }
        public static ItemData Get(string id) { InitIfNeeded(); return _items.TryGetValue(id, out var item) ? item : null; }
    }
}
