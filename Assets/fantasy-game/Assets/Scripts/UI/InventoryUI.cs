// Assets/Scripts/UI/InventoryUI.cs
// =================================
// Full inventory panel toggled with Tab.
// Grid of 20 slots (4×5), equipped weapon display, item tooltips,
// click-to-equip/use/drop, and pickup toast notifications.
// Built entirely with IMGUI — zero asset dependencies.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using FantasyGame.RPG;
using FantasyGame.Enemies;

namespace FantasyGame.UI
{
    public class InventoryUI : MonoBehaviour
    {
        public static bool IsOpen { get; private set; }

        private const int COLS = 4;
        private const int ROWS = 5;

        private Inventory _inventory;
        private PlayerStats _stats;
        private Transform _player;
        private Texture2D _whiteTex;

        // Tooltip state
        private InventorySlot _hoveredSlot;

        // Pickup toast
        private struct PickupToast
        {
            public string Text;
            public float Timer;
        }
        private List<PickupToast> _toasts = new List<PickupToast>();
        private const float TOAST_DURATION = 2.5f;

        public void Init(Inventory inventory, PlayerStats stats, Transform player)
        {
            _inventory = inventory;
            _stats = stats;
            _player = player;

            _whiteTex = new Texture2D(1, 1);
            _whiteTex.SetPixel(0, 0, Color.white);
            _whiteTex.Apply();

            // Subscribe to inventory changes for pickup toasts
            _inventory.OnInventoryChanged += OnInventoryChanged;

            Debug.Log("[InventoryUI] Initialized. Press Tab to open inventory.");
        }

        // Track last known counts for toast detection
        private Dictionary<string, int> _lastCounts = new Dictionary<string, int>();
        private bool _suppressToast;

        private void OnInventoryChanged()
        {
            if (_suppressToast) return;

            // Compare current vs last counts to detect additions
            foreach (var slot in _inventory.Slots)
            {
                int oldCount = _lastCounts.ContainsKey(slot.Item.Id) ? _lastCounts[slot.Item.Id] : 0;
                int diff = slot.Count - oldCount;
                if (diff > 0)
                {
                    _toasts.Add(new PickupToast
                    {
                        Text = $"+ {slot.Item.Name} x{diff}",
                        Timer = TOAST_DURATION
                    });
                }
            }

            SnapshotCounts();
        }

        private void SnapshotCounts()
        {
            _lastCounts.Clear();
            foreach (var slot in _inventory.Slots)
                _lastCounts[slot.Item.Id] = slot.Count;
        }

        private void Update()
        {
            if (_inventory == null) return;

            // Toggle on Tab
            var kb = Keyboard.current;
            if (kb != null && kb.tabKey.wasPressedThisFrame)
            {
                Toggle();
            }

            // ESC closes if open
            if (IsOpen && kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                Close();
            }

            // Update toasts
            for (int i = _toasts.Count - 1; i >= 0; i--)
            {
                var t = _toasts[i];
                t.Timer -= Time.deltaTime;
                _toasts[i] = t;
                if (t.Timer <= 0f)
                    _toasts.RemoveAt(i);
            }
        }

        private void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        private void Open()
        {
            if (PauseMenu.IsPaused) return;
            IsOpen = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            SnapshotCounts(); // snapshot now so we don't toast existing items
        }

        private void Close()
        {
            IsOpen = false;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void OnGUI()
        {
            if (_inventory == null) return;

            // Always draw toasts (even when inventory closed)
            DrawToasts();

            if (!IsOpen) return;

            float scale = Screen.height / 1080f;

            // Dark overlay
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _whiteTex);
            GUI.color = Color.white;

            // Panel dimensions
            float slotSize = 70f * scale;
            float slotGap = 8f * scale;
            float padding = 20f * scale;
            float headerH = 80f * scale;
            float panelW = padding * 2 + COLS * slotSize + (COLS - 1) * slotGap;
            float panelH = padding * 2 + headerH + ROWS * slotSize + (ROWS - 1) * slotGap + 40f * scale;
            float panelX = (Screen.width - panelW) * 0.5f;
            float panelY = (Screen.height - panelH) * 0.5f;

            // Panel background
            DrawRect(panelX, panelY, panelW, panelH, new Color(0.08f, 0.06f, 0.12f, 0.95f));
            DrawBorder(panelX, panelY, panelW, panelH, 2f, new Color(0.5f, 0.4f, 0.3f, 0.8f));

            // Title
            var titleStyle = MakeStyle(Mathf.RoundToInt(28 * scale), FontStyle.Bold, TextAnchor.MiddleCenter,
                new Color(1f, 0.85f, 0.5f));
            GUI.Label(new Rect(panelX, panelY + 10f * scale, panelW, 36f * scale), "INVENTORY", titleStyle);

            // Equipped weapon info
            DrawEquippedWeapon(panelX + padding, panelY + 42f * scale, panelW - padding * 2, 34f * scale, scale);

            // Slot grid
            float gridStartX = panelX + padding;
            float gridStartY = panelY + headerH + padding * 0.5f;
            _hoveredSlot = null;

            for (int row = 0; row < ROWS; row++)
            {
                for (int col = 0; col < COLS; col++)
                {
                    int idx = row * COLS + col;
                    float sx = gridStartX + col * (slotSize + slotGap);
                    float sy = gridStartY + row * (slotSize + slotGap);

                    InventorySlot slot = idx < _inventory.Slots.Count ? _inventory.Slots[idx] : null;
                    DrawSlot(sx, sy, slotSize, slot, scale);
                }
            }

            // Tooltip (drawn last so it's on top)
            if (_hoveredSlot != null)
            {
                DrawTooltip(scale);
            }

            // Close hint
            var hintStyle = MakeStyle(Mathf.RoundToInt(12 * scale), FontStyle.Normal, TextAnchor.MiddleCenter,
                new Color(0.5f, 0.45f, 0.4f));
            GUI.Label(new Rect(panelX, panelY + panelH - 28f * scale, panelW, 20f * scale),
                "[Tab] Close    [Left-Click] Equip/Use    [Right-Click] Drop", hintStyle);
        }

        private void DrawEquippedWeapon(float x, float y, float w, float h, float scale)
        {
            var labelStyle = MakeStyle(Mathf.RoundToInt(13 * scale), FontStyle.Normal, TextAnchor.MiddleLeft,
                new Color(0.7f, 0.65f, 0.55f));
            var weaponStyle = MakeStyle(Mathf.RoundToInt(15 * scale), FontStyle.Bold, TextAnchor.MiddleLeft,
                new Color(1f, 0.9f, 0.6f));

            GUI.Label(new Rect(x, y, 80f * scale, h), "Equipped:", labelStyle);

            var weapon = _inventory.EquippedWeapon;
            if (weapon != null)
            {
                // Weapon color swatch
                float swatchSize = 14f * scale;
                float swatchX = x + 80f * scale;
                float swatchY = y + (h - swatchSize) * 0.5f;
                DrawRect(swatchX, swatchY, swatchSize, swatchSize, weapon.DisplayColor);
                DrawBorder(swatchX, swatchY, swatchSize, swatchSize, 1f, new Color(0, 0, 0, 0.5f));

                GUI.Label(new Rect(swatchX + swatchSize + 6f * scale, y, w - 100f * scale, h),
                    $"{weapon.Name}  (DMG: {weapon.Value})", weaponStyle);
            }
            else
            {
                weaponStyle.normal.textColor = new Color(0.5f, 0.45f, 0.4f);
                GUI.Label(new Rect(x + 80f * scale, y, w - 80f * scale, h), "None", weaponStyle);
            }
        }

        private void DrawSlot(float x, float y, float size, InventorySlot slot, float scale)
        {
            Rect slotRect = new Rect(x, y, size, size);
            bool hover = slotRect.Contains(Event.current.mousePosition);

            // Background
            Color bgColor;
            if (slot != null)
            {
                // Tinted based on item type
                Color tint = slot.Item.DisplayColor * 0.3f;
                tint.a = 0.7f;
                bgColor = hover ? Color.Lerp(tint, Color.white, 0.15f) : tint;
            }
            else
            {
                bgColor = hover ? new Color(0.18f, 0.15f, 0.22f, 0.6f) : new Color(0.12f, 0.1f, 0.16f, 0.5f);
            }
            DrawRect(x, y, size, size, bgColor);

            // Border — highlight if equipped weapon
            bool isEquipped = slot != null && _inventory.EquippedWeapon != null
                && slot.Item.Id == _inventory.EquippedWeapon.Id;
            Color borderColor = isEquipped
                ? new Color(1f, 0.85f, 0.3f, 0.9f)
                : hover
                    ? new Color(0.7f, 0.6f, 0.4f, 0.7f)
                    : new Color(0.3f, 0.25f, 0.2f, 0.4f);
            DrawBorder(x, y, size, size, isEquipped ? 2f : 1f, borderColor);

            if (slot != null)
            {
                // Item icon — colored square with letter
                float iconPad = 8f * scale;
                float iconSize = size - iconPad * 2;
                DrawRect(x + iconPad, y + iconPad, iconSize, iconSize, slot.Item.DisplayColor * 0.6f);

                // Type icon letter
                string letter = GetTypeIcon(slot.Item.Type);
                var iconStyle = MakeStyle(Mathf.RoundToInt(24 * scale), FontStyle.Bold, TextAnchor.MiddleCenter,
                    new Color(1f, 1f, 1f, 0.85f));
                GUI.Label(new Rect(x + iconPad, y + iconPad, iconSize, iconSize), letter, iconStyle);

                // Item name (small, under icon)
                var nameStyle = MakeStyle(Mathf.RoundToInt(9 * scale), FontStyle.Normal, TextAnchor.LowerCenter,
                    new Color(0.9f, 0.85f, 0.75f));
                GUI.Label(new Rect(x + 2f, y + 2f, size - 4f, size - 4f), slot.Item.Name, nameStyle);

                // Stack count
                if (slot.Count > 1)
                {
                    var countStyle = MakeStyle(Mathf.RoundToInt(12 * scale), FontStyle.Bold, TextAnchor.LowerRight,
                        new Color(1f, 1f, 1f, 0.9f));
                    // Shadow
                    var shadowStyle = new GUIStyle(countStyle);
                    shadowStyle.normal.textColor = new Color(0, 0, 0, 0.8f);
                    GUI.Label(new Rect(x + 1f, y + 1f, size - 3f, size - 3f), slot.Count.ToString(), shadowStyle);
                    GUI.Label(new Rect(x, y, size - 4f, size - 4f), slot.Count.ToString(), countStyle);
                }

                // Equipped indicator
                if (isEquipped)
                {
                    var eqStyle = MakeStyle(Mathf.RoundToInt(9 * scale), FontStyle.Bold, TextAnchor.UpperRight,
                        new Color(1f, 0.85f, 0.3f));
                    GUI.Label(new Rect(x, y + 2f, size - 4f, 16f * scale), "E", eqStyle);
                }

                // Hover — set for tooltip
                if (hover)
                    _hoveredSlot = slot;

                // Click handling
                if (hover && Event.current.type == EventType.MouseDown)
                {
                    if (Event.current.button == 0) // Left click
                    {
                        HandleLeftClick(slot);
                        Event.current.Use();
                    }
                    else if (Event.current.button == 1) // Right click
                    {
                        HandleRightClick(slot);
                        Event.current.Use();
                    }
                }
            }
        }

        private void HandleLeftClick(InventorySlot slot)
        {
            _suppressToast = true;

            if (slot.Item.Type == ItemType.Weapon)
            {
                _inventory.EquipWeapon(slot.Item.Id);
                Debug.Log($"[InventoryUI] Equipped {slot.Item.Name}");
            }
            else if (slot.Item.Type == ItemType.Potion)
            {
                if (_stats.CurrentHealth < _stats.MaxHealth)
                {
                    _inventory.UsePotion(slot.Item.Id, _stats);
                    Debug.Log($"[InventoryUI] Used {slot.Item.Name}");
                }
            }

            _suppressToast = false;
            SnapshotCounts();
        }

        private void HandleRightClick(InventorySlot slot)
        {
            _suppressToast = true;

            // Drop one item on the ground
            var item = slot.Item;
            bool removed = _inventory.RemoveItem(item.Id, 1);
            if (removed && _player != null)
            {
                SpawnDroppedItem(item, 1);
                Debug.Log($"[InventoryUI] Dropped {item.Name}");
            }

            _suppressToast = false;
            SnapshotCounts();
        }

        private void SpawnDroppedItem(ItemData item, int count)
        {
            // Spawn a loot pickup in front of the player (same pattern as EnemyBase)
            Vector3 dropPos = _player.position + _player.forward * 1.5f + Vector3.up * 0.5f;

            var lootGo = new GameObject($"Loot_{item.Name}");
            lootGo.transform.position = dropPos;

            // Visual: small colored sphere
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.transform.SetParent(lootGo.transform);
            sphere.transform.localScale = Vector3.one * 0.3f;
            sphere.transform.localPosition = Vector3.zero;

            var renderer = sphere.GetComponent<Renderer>();
            if (renderer != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit")
                    ?? Shader.Find("Unlit/Color"));
                mat.color = item.DisplayColor;
                renderer.material = mat;
            }

            // Remove sphere's collider, add trigger collider on parent
            var sphereCol = sphere.GetComponent<Collider>();
            if (sphereCol != null) Object.Destroy(sphereCol);

            var trigger = lootGo.AddComponent<SphereCollider>();
            trigger.radius = 1.0f;
            trigger.isTrigger = true;

            var pickup = lootGo.AddComponent<LootPickup>();
            pickup.Init(item, count);

            // Bob animation
            lootGo.AddComponent<LootBob>();
        }

        private void DrawTooltip(float scale)
        {
            if (_hoveredSlot == null) return;
            var item = _hoveredSlot.Item;

            Vector2 mouse = Event.current.mousePosition;
            float ttW = 220f * scale;
            float ttH = 100f * scale;

            // Keep tooltip on screen
            float ttX = mouse.x + 16f * scale;
            float ttY = mouse.y + 16f * scale;
            if (ttX + ttW > Screen.width) ttX = mouse.x - ttW - 8f * scale;
            if (ttY + ttH > Screen.height) ttY = mouse.y - ttH - 8f * scale;

            // Background
            DrawRect(ttX, ttY, ttW, ttH, new Color(0.05f, 0.04f, 0.08f, 0.95f));
            DrawBorder(ttX, ttY, ttW, ttH, 1f, new Color(0.6f, 0.5f, 0.3f, 0.8f));

            float pad = 8f * scale;
            float curY = ttY + pad;

            // Name
            var nameStyle = MakeStyle(Mathf.RoundToInt(15 * scale), FontStyle.Bold, TextAnchor.UpperLeft,
                item.DisplayColor);
            GUI.Label(new Rect(ttX + pad, curY, ttW - pad * 2, 20f * scale), item.Name, nameStyle);
            curY += 20f * scale;

            // Type + value
            var typeStyle = MakeStyle(Mathf.RoundToInt(11 * scale), FontStyle.Normal, TextAnchor.UpperLeft,
                new Color(0.7f, 0.65f, 0.55f));
            string valueTxt = item.Type switch
            {
                ItemType.Weapon => $"Weapon  \u2022  DMG: {item.Value}",
                ItemType.Potion => $"Potion  \u2022  Heals: {item.Value} HP",
                ItemType.Material => $"Material  \u2022  Value: {item.Value}",
                ItemType.Quest => "Quest Item",
                _ => item.Type.ToString()
            };
            GUI.Label(new Rect(ttX + pad, curY, ttW - pad * 2, 16f * scale), valueTxt, typeStyle);
            curY += 18f * scale;

            // Count
            var countStyle = MakeStyle(Mathf.RoundToInt(11 * scale), FontStyle.Normal, TextAnchor.UpperLeft,
                new Color(0.6f, 0.55f, 0.5f));
            GUI.Label(new Rect(ttX + pad, curY, ttW - pad * 2, 16f * scale),
                $"Qty: {_hoveredSlot.Count}", countStyle);
            curY += 18f * scale;

            // Description
            var descStyle = MakeStyle(Mathf.RoundToInt(11 * scale), FontStyle.Italic, TextAnchor.UpperLeft,
                new Color(0.75f, 0.72f, 0.65f));
            descStyle.wordWrap = true;
            GUI.Label(new Rect(ttX + pad, curY, ttW - pad * 2, ttH - (curY - ttY) - pad), item.Description, descStyle);
        }

        private void DrawToasts()
        {
            if (_toasts.Count == 0) return;

            float scale = Screen.height / 1080f;
            float startY = Screen.height - 120f * scale;

            for (int i = 0; i < _toasts.Count; i++)
            {
                var toast = _toasts[i];
                float alpha = Mathf.Clamp01(toast.Timer / 0.5f); // fade out in last 0.5s
                float slideUp = (1f - Mathf.Clamp01(toast.Timer / TOAST_DURATION)) * 20f * scale;

                var style = MakeStyle(Mathf.RoundToInt(16 * scale), FontStyle.Bold, TextAnchor.MiddleCenter,
                    new Color(0.4f, 1f, 0.4f, alpha));

                float y = startY - i * 24f * scale - slideUp;

                // Shadow
                var shadowStyle = new GUIStyle(style);
                shadowStyle.normal.textColor = new Color(0, 0, 0, alpha * 0.7f);
                GUI.Label(new Rect(1f, y + 1f, Screen.width, 24f * scale), toast.Text, shadowStyle);
                GUI.Label(new Rect(0, y, Screen.width, 24f * scale), toast.Text, style);
            }
        }

        // --- Helpers ---

        private string GetTypeIcon(ItemType type)
        {
            return type switch
            {
                ItemType.Weapon => "W",
                ItemType.Potion => "P",
                ItemType.Material => "M",
                ItemType.Quest => "Q",
                _ => "?"
            };
        }

        private GUIStyle MakeStyle(int fontSize, FontStyle fontStyle, TextAnchor alignment, Color color)
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize,
                fontStyle = fontStyle,
                alignment = alignment
            };
            style.normal.textColor = color;
            return style;
        }

        private void DrawRect(float x, float y, float w, float h, Color color)
        {
            GUI.color = color;
            GUI.DrawTexture(new Rect(x, y, w, h), _whiteTex);
            GUI.color = Color.white;
        }

        private void DrawBorder(float x, float y, float w, float h, float thickness, Color color)
        {
            GUI.color = color;
            GUI.DrawTexture(new Rect(x, y, w, thickness), _whiteTex);
            GUI.DrawTexture(new Rect(x, y + h - thickness, w, thickness), _whiteTex);
            GUI.DrawTexture(new Rect(x, y, thickness, h), _whiteTex);
            GUI.DrawTexture(new Rect(x + w - thickness, y, thickness, h), _whiteTex);
            GUI.color = Color.white;
        }

        private void OnDestroy()
        {
            if (IsOpen) Close();
        }
    }
}
