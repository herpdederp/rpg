// Assets/Scripts/Enemies/LootPickup.cs
// ======================================
// Dropped loot item. Player walks into it to pick up.

using UnityEngine;
using FantasyGame.RPG;

namespace FantasyGame.Enemies
{
    public class LootPickup : MonoBehaviour
    {
        private ItemData _item;
        private int _count;
        private float _lifetime = 30f; // Despawn after 30 seconds

        public void Init(ItemData item, int count)
        {
            _item = item;
            _count = count;
            gameObject.tag = "Untagged";
            gameObject.layer = 0;
        }

        private void Update()
        {
            _lifetime -= Time.deltaTime;
            if (_lifetime <= 0)
            {
                Debug.Log($"[LootPickup] {_item.Name} despawned.");
                Destroy(gameObject);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            // Check if it's the player
            var inventory = other.GetComponentInParent<InventoryComponent>();
            if (inventory == null) return;

            bool added = inventory.Inventory.AddItem(_item, _count);
            if (added)
            {
                if (Audio.SoundManager.Instance != null)
                    Audio.SoundManager.Instance.PlayItemPickup();
                if (VFX.ParticleEffectManager.Instance != null)
                    VFX.ParticleEffectManager.Instance.SpawnLootSparkle(transform.position);

                Debug.Log($"[LootPickup] Player picked up {_item.Name} x{_count}");
                Destroy(gameObject);
            }
            else
            {
                Debug.Log("[LootPickup] Inventory full — can't pick up!");
            }
        }
    }
}
