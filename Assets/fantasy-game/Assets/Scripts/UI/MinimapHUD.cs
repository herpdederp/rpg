// Assets/Scripts/UI/MinimapHUD.cs
// =================================
// IMGUI-based minimap in the top-right corner.
// Shows terrain as a colored background, player arrow, enemies (red),
// NPCs (blue), campfires (orange), and chests (yellow).

using UnityEngine;
using System.Collections.Generic;
using FantasyGame.Enemies;
using FantasyGame.Interaction;
using FantasyGame.Dungeon;

namespace FantasyGame.UI
{
    public class MinimapHUD : MonoBehaviour
    {
        private Transform _player;
        private Texture2D _whiteTex;
        private Texture2D _minimapBg;

        private const float MAP_SIZE_RATIO = 0.18f; // % of screen height
        private const float MAP_RANGE = 50f; // World units visible on minimap
        private const float MARGIN = 10f;

        // Points of interest (always shown on minimap edge if out of range)
        private static readonly Vector3 VILLAGE_POS = new Vector3(80f, 0f, 80f);
        private const float VILLAGE_DISCOVERY_RANGE = 20f; // Within this range, stop showing arrow

        // Tracking
        private float _refreshTimer;
        private const float REFRESH_INTERVAL = 0.25f;
        private List<MinimapBlip> _blips = new List<MinimapBlip>();
        private bool _villageDiscovered;

        private struct MinimapBlip
        {
            public Vector3 WorldPos;
            public Color Color;
            public float Size;
        }

        public void Init(Transform player)
        {
            _player = player;

            _whiteTex = new Texture2D(1, 1);
            _whiteTex.SetPixel(0, 0, Color.white);
            _whiteTex.Apply();

            // Generate a simple terrain-colored minimap background
            GenerateMinimapBackground();

            Debug.Log("[MinimapHUD] Initialized.");
        }

        private void GenerateMinimapBackground()
        {
            int size = 128;
            _minimapBg = new Texture2D(size, size, TextureFormat.RGBA32, false);
            _minimapBg.filterMode = FilterMode.Bilinear;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Simple radial gradient for the minimap circle
                    float dx = (x - size * 0.5f) / (size * 0.5f);
                    float dy = (y - size * 0.5f) / (size * 0.5f);
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);

                    if (dist > 1f)
                    {
                        _minimapBg.SetPixel(x, y, new Color(0, 0, 0, 0));
                    }
                    else
                    {
                        // Dark green terrain base with subtle noise
                        float noise = Mathf.PerlinNoise(x * 0.1f, y * 0.1f);
                        Color terrainColor = Color.Lerp(
                            new Color(0.12f, 0.18f, 0.08f, 0.85f),
                            new Color(0.18f, 0.25f, 0.12f, 0.85f),
                            noise
                        );
                        // Darken edges
                        terrainColor *= Mathf.Lerp(1f, 0.6f, dist * dist);
                        terrainColor.a = 0.85f;
                        _minimapBg.SetPixel(x, y, terrainColor);
                    }
                }
            }
            _minimapBg.Apply();
        }

        private void Update()
        {
            _refreshTimer -= Time.deltaTime;
            if (_refreshTimer <= 0f)
            {
                _refreshTimer = REFRESH_INTERVAL;
                RefreshBlips();
            }
        }

        private void RefreshBlips()
        {
            _blips.Clear();

            // Enemies (red dots)
            var enemies = FindObjectsByType<EnemyBase>(FindObjectsSortMode.None);
            foreach (var enemy in enemies)
            {
                if (enemy.IsAlive)
                {
                    _blips.Add(new MinimapBlip
                    {
                        WorldPos = enemy.transform.position,
                        Color = new Color(0.9f, 0.2f, 0.15f),
                        Size = 4f
                    });
                }
            }

            // NPCs (blue dots)
            var npcs = FindObjectsByType<DialogueNPC>(FindObjectsSortMode.None);
            foreach (var npc in npcs)
            {
                _blips.Add(new MinimapBlip
                {
                    WorldPos = npc.transform.position,
                    Color = new Color(0.3f, 0.6f, 1f),
                    Size = 5f
                });
            }

            // Campfires (orange dots)
            var campfires = FindObjectsByType<Campfire>(FindObjectsSortMode.None);
            foreach (var fire in campfires)
            {
                _blips.Add(new MinimapBlip
                {
                    WorldPos = fire.transform.position,
                    Color = new Color(1f, 0.6f, 0.15f),
                    Size = 5f
                });
            }

            // Treasure chests (yellow dots) — only if not used
            var chests = FindObjectsByType<TreasureChest>(FindObjectsSortMode.None);
            foreach (var chest in chests)
            {
                if (!chest.IsUsed)
                {
                    _blips.Add(new MinimapBlip
                    {
                        WorldPos = chest.transform.position,
                        Color = new Color(1f, 0.85f, 0.2f),
                        Size = 4f
                    });
                }
            }
        }

        private void OnGUI()
        {
            if (_player == null || _whiteTex == null) return;

            float scale = Screen.height / 1080f;
            float mapSize = Screen.height * MAP_SIZE_RATIO;
            float margin = MARGIN * scale;

            // Position: top-right corner
            float mapX = Screen.width - mapSize - margin;
            float mapY = margin;
            Rect mapRect = new Rect(mapX, mapY, mapSize, mapSize);

            // Draw border ring
            GUI.color = new Color(0.3f, 0.25f, 0.2f, 0.9f);
            GUI.DrawTexture(new Rect(mapX - 2, mapY - 2, mapSize + 4, mapSize + 4), _whiteTex);
            GUI.color = Color.white;

            // Draw background
            GUI.DrawTexture(mapRect, _minimapBg);

            // Calculate world-to-minimap transform
            Vector3 playerPos = _player.position;
            float playerAngle = _player.eulerAngles.y;

            // Draw blips
            foreach (var blip in _blips)
            {
                DrawBlip(mapRect, playerPos, blip.WorldPos, blip.Color, blip.Size * scale);
            }

            // Draw village marker (green house icon on minimap edge if far away)
            // Hide when in dungeon — village marker is irrelevant underground
            if (!DungeonManager.IsInDungeon)
                DrawVillageMarker(mapRect, playerPos, scale);

            // Draw player (white triangle pointing forward)
            DrawPlayerArrow(mapRect, playerAngle, scale);

            // Compass labels
            DrawCompassLabels(mapRect, playerAngle, scale);

            GUI.color = Color.white;

            // Draw directional hint to village if not discovered yet
            // Hide when in dungeon
            if (!_villageDiscovered && !DungeonManager.IsInDungeon)
            {
                DrawVillageHint(playerPos, scale);
            }
        }

        private void DrawBlip(Rect mapRect, Vector3 playerPos, Vector3 worldPos, Color color, float size)
        {
            // Offset from player
            float dx = worldPos.x - playerPos.x;
            float dz = worldPos.z - playerPos.z;

            // Distance check
            float dist = Mathf.Sqrt(dx * dx + dz * dz);
            if (dist > MAP_RANGE) return;

            // Convert to minimap coords (center = player)
            float nx = dx / MAP_RANGE * 0.5f + 0.5f;
            float ny = 0.5f - dz / MAP_RANGE * 0.5f; // Flip Z for screen coords

            // Circular boundary check
            float cdx = nx - 0.5f;
            float cdy = ny - 0.5f;
            if (cdx * cdx + cdy * cdy > 0.23f) return; // Inside circle

            float px = mapRect.x + nx * mapRect.width;
            float py = mapRect.y + ny * mapRect.height;

            GUI.color = color;
            GUI.DrawTexture(new Rect(px - size * 0.5f, py - size * 0.5f, size, size), _whiteTex);
        }

        private void DrawPlayerArrow(Rect mapRect, float angle, float scale)
        {
            float cx = mapRect.x + mapRect.width * 0.5f;
            float cy = mapRect.y + mapRect.height * 0.5f;
            float arrowSize = 6f * scale;

            // Simple diamond shape for player
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(cx - arrowSize, cy - arrowSize, arrowSize * 2f, arrowSize * 2f), _whiteTex);

            // Direction indicator — small dot in front of player
            float rad = -angle * Mathf.Deg2Rad + Mathf.PI * 0.5f;
            float dirX = Mathf.Cos(rad) * arrowSize * 2.5f;
            float dirY = -Mathf.Sin(rad) * arrowSize * 2.5f;
            float dotSize = 3f * scale;
            GUI.color = new Color(0.4f, 0.8f, 1f);
            GUI.DrawTexture(new Rect(cx + dirX - dotSize * 0.5f, cy + dirY - dotSize * 0.5f, dotSize, dotSize), _whiteTex);
        }

        private void DrawVillageMarker(Rect mapRect, Vector3 playerPos, float scale)
        {
            float dx = VILLAGE_POS.x - playerPos.x;
            float dz = VILLAGE_POS.z - playerPos.z;
            float dist = Mathf.Sqrt(dx * dx + dz * dz);

            // Check discovery
            if (dist < VILLAGE_DISCOVERY_RANGE)
            {
                _villageDiscovered = true;
            }

            Color villageColor = new Color(0.2f, 0.9f, 0.4f); // Bright green
            float markerSize = 7f * scale;

            if (dist <= MAP_RANGE)
            {
                // Inside minimap range — draw normally
                float nx = dx / MAP_RANGE * 0.5f + 0.5f;
                float ny = 0.5f - dz / MAP_RANGE * 0.5f;
                float cdx2 = nx - 0.5f;
                float cdy2 = ny - 0.5f;
                if (cdx2 * cdx2 + cdy2 * cdy2 > 0.23f) return;

                float px = mapRect.x + nx * mapRect.width;
                float py = mapRect.y + ny * mapRect.height;

                GUI.color = villageColor;
                GUI.DrawTexture(new Rect(px - markerSize * 0.5f, py - markerSize * 0.5f, markerSize, markerSize), _whiteTex);
                // Draw a slightly smaller inner dot for visibility
                GUI.color = Color.white;
                float innerSize = markerSize * 0.4f;
                GUI.DrawTexture(new Rect(px - innerSize * 0.5f, py - innerSize * 0.5f, innerSize, innerSize), _whiteTex);
            }
            else
            {
                // Outside range — clamp to minimap edge as a direction arrow
                float angle = Mathf.Atan2(dx, dz); // angle from player to village
                float edgeRadius = 0.44f; // Just inside the circle edge
                float ex = 0.5f + Mathf.Sin(angle) * edgeRadius;
                float ey = 0.5f - Mathf.Cos(angle) * edgeRadius;

                float px = mapRect.x + ex * mapRect.width;
                float py = mapRect.y + ey * mapRect.height;

                // Pulsing alpha for attention
                float pulse = 0.6f + 0.4f * Mathf.Sin(Time.time * 3f);
                GUI.color = new Color(villageColor.r, villageColor.g, villageColor.b, pulse);
                GUI.DrawTexture(new Rect(px - markerSize * 0.5f, py - markerSize * 0.5f, markerSize, markerSize), _whiteTex);
            }
        }

        private void DrawVillageHint(Vector3 playerPos, float scale)
        {
            float dx = VILLAGE_POS.x - playerPos.x;
            float dz = VILLAGE_POS.z - playerPos.z;
            float dist = Mathf.Sqrt(dx * dx + dz * dz);

            if (dist < VILLAGE_DISCOVERY_RANGE) return;

            // Screen-space directional hint at bottom center
            var hintStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(14 * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 2f);
            hintStyle.normal.textColor = new Color(0.3f, 0.9f, 0.5f, pulse * 0.8f);

            // Direction as compass text
            float angle = Mathf.Atan2(dx, dz) * Mathf.Rad2Deg;
            string direction = GetCompassDirection(angle);
            int distInt = Mathf.RoundToInt(dist);

            string hint = $"Village Outpost — {distInt}m {direction}";
            float hintW = 300f * scale;
            float hintH = 30f * scale;
            float hintX = (Screen.width - hintW) * 0.5f;
            float hintY = Screen.height - 60f * scale;

            // Shadow
            var shadowStyle = new GUIStyle(hintStyle);
            shadowStyle.normal.textColor = new Color(0, 0, 0, pulse * 0.5f);
            GUI.Label(new Rect(hintX + 1, hintY + 1, hintW, hintH), hint, shadowStyle);
            GUI.Label(new Rect(hintX, hintY, hintW, hintH), hint, hintStyle);
        }

        private string GetCompassDirection(float angle)
        {
            // angle is degrees clockwise from +Z (north)
            if (angle < 0) angle += 360f;
            if (angle >= 337.5f || angle < 22.5f) return "N";
            if (angle < 67.5f) return "NE";
            if (angle < 112.5f) return "E";
            if (angle < 157.5f) return "SE";
            if (angle < 202.5f) return "S";
            if (angle < 247.5f) return "SW";
            if (angle < 292.5f) return "W";
            return "NW";
        }

        private void DrawCompassLabels(Rect mapRect, float angle, float scale)
        {
            var compassStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(10 * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            compassStyle.normal.textColor = new Color(0.8f, 0.75f, 0.6f, 0.7f);

            float cx = mapRect.x + mapRect.width * 0.5f;
            float cy = mapRect.y + mapRect.height * 0.5f;
            float r = mapRect.width * 0.42f;

            // N label at top
            float labelW = 14f * scale;
            float labelH = 14f * scale;
            GUI.Label(new Rect(cx - labelW * 0.5f, mapRect.y + 2f * scale, labelW, labelH), "N", compassStyle);
        }
    }
}
