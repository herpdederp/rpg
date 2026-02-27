// Assets/Scripts/UI/GameHUD.cs
// ============================
// Runtime-created HUD with health bar, stamina bar, XP bar, and level display.
// Built entirely from code using Unity's IMGUI (OnGUI) for zero asset dependencies.

using UnityEngine;
using FantasyGame.RPG;
using FantasyGame.Player;
using FantasyGame.Combat;

namespace FantasyGame.UI
{
    public class GameHUD : MonoBehaviour
    {
        private PlayerStats _stats;
        private Texture2D _whiteTex;
        private GUIStyle _labelStyle;
        private GUIStyle _levelStyle;
        private bool _stylesInitialized;

        // Damage flash
        private float _damageFlashTimer;
        private const float DAMAGE_FLASH_DURATION = 0.3f;

        // Level up flash
        private float _levelUpFlashTimer;
        private const float LEVEL_UP_FLASH_DURATION = 1.5f;

        // Death screen
        private bool _isDead;
        private float _deathTimer;
        private float _deathFadeAlpha;
        private const float RESPAWN_DELAY = 3f;

        public void Init(PlayerStats stats)
        {
            _stats = stats;

            // Create 1x1 white texture for drawing colored rects
            _whiteTex = new Texture2D(1, 1);
            _whiteTex.SetPixel(0, 0, Color.white);
            _whiteTex.Apply();

            // Subscribe to events
            _stats.OnHealthChanged += (cur, max) =>
            {
                if (cur < max) _damageFlashTimer = DAMAGE_FLASH_DURATION;
            };
            _stats.OnLevelUp += (lvl) =>
            {
                _levelUpFlashTimer = LEVEL_UP_FLASH_DURATION;
            };
            _stats.OnDeath += OnPlayerDeath;
        }

        private void OnPlayerDeath()
        {
            if (_isDead) return;
            _isDead = true;
            _deathTimer = RESPAWN_DELAY;
            _deathFadeAlpha = 0f;

            // Disable player controls
            var controller = FindAnyObjectByType<ThirdPersonController>();
            if (controller != null) controller.enabled = false;
            var combat = FindAnyObjectByType<MeleeCombat>();
            if (combat != null) combat.enabled = false;

            Debug.Log("[GameHUD] Player died! Respawning in 3 seconds...");
        }

        private void Respawn()
        {
            _isDead = false;

            // Heal to full
            _stats.Heal(_stats.MaxHealth);

            // Teleport back to spawn area
            var controller = FindAnyObjectByType<ThirdPersonController>();
            if (controller != null)
            {
                var cc = controller.GetComponent<CharacterController>();
                if (cc != null)
                {
                    cc.enabled = false;
                    // Find terrain at spawn
                    Vector3 spawnPos = new Vector3(0, 50f, 0);
                    if (Physics.Raycast(new Vector3(0, 200f, 0), Vector3.down, out RaycastHit hit, 400f))
                    {
                        spawnPos = hit.point + Vector3.up * 0.5f;
                    }
                    controller.transform.position = spawnPos;
                    cc.enabled = true;
                    cc.Move(Vector3.down * 0.1f);
                }
                controller.enabled = true;
                controller.ResetAfterRespawn();
            }

            var combat = FindAnyObjectByType<MeleeCombat>();
            if (combat != null) combat.enabled = true;

            Debug.Log("[GameHUD] Player respawned!");
        }

        private void Update()
        {
            if (_damageFlashTimer > 0f)
                _damageFlashTimer -= Time.deltaTime;
            if (_levelUpFlashTimer > 0f)
                _levelUpFlashTimer -= Time.deltaTime;

            if (_isDead)
            {
                _deathTimer -= Time.deltaTime;
                _deathFadeAlpha = Mathf.Min(1f, _deathFadeAlpha + Time.deltaTime * 1.5f);

                if (_deathTimer <= 0f)
                {
                    Respawn();
                }
            }
        }

        private void InitStyles()
        {
            if (_stylesInitialized) return;
            _stylesInitialized = true;

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            _labelStyle.normal.textColor = Color.white;

            _levelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _levelStyle.normal.textColor = new Color(1f, 0.85f, 0.4f);
        }

        private void OnGUI()
        {
            if (_stats == null) return;
            InitStyles();

            float scale = Screen.height / 1080f;
            float barWidth = 250f * scale;
            float barHeight = 20f * scale;
            float padding = 15f * scale;
            float x = padding;
            float y = padding;

            // --- Level circle ---
            float circleSize = 50f * scale;
            DrawCircle(x, y, circleSize, new Color(0.2f, 0.15f, 0.1f, 0.9f));
            GUI.Label(new Rect(x, y, circleSize, circleSize),
                _stats.Level.ToString(), _levelStyle);
            x += circleSize + padding * 0.5f;

            // --- Health Bar ---
            float healthPct = (float)_stats.CurrentHealth / _stats.MaxHealth;
            Color healthColor = Color.Lerp(new Color(0.8f, 0.1f, 0.1f), new Color(0.2f, 0.8f, 0.2f), healthPct);
            DrawBar(x, y, barWidth, barHeight, healthPct, healthColor,
                new Color(0.15f, 0.05f, 0.05f, 0.8f));
            GUI.Label(new Rect(x + 5f * scale, y, barWidth, barHeight),
                $"HP {_stats.CurrentHealth}/{_stats.MaxHealth}", _labelStyle);
            y += barHeight + 4f * scale;

            // --- Stamina Bar ---
            float staminaPct = (float)_stats.CurrentStamina / _stats.MaxStamina;
            DrawBar(x, y, barWidth, barHeight * 0.7f, staminaPct,
                new Color(0.3f, 0.7f, 0.2f),
                new Color(0.05f, 0.15f, 0.05f, 0.8f));
            y += barHeight * 0.7f + 4f * scale;

            // --- XP Bar ---
            float xpPct = (float)_stats.XP / _stats.XPToNextLevel;
            DrawBar(x, y, barWidth, barHeight * 0.5f, xpPct,
                new Color(0.4f, 0.6f, 1.0f),
                new Color(0.05f, 0.05f, 0.2f, 0.8f));

            // --- Stat points indicator ---
            if (_stats.StatPoints > 0)
            {
                y += barHeight * 0.5f + 8f * scale;
                _labelStyle.normal.textColor = new Color(1f, 0.85f, 0.4f);
                GUI.Label(new Rect(x, y, barWidth, barHeight),
                    $"+{_stats.StatPoints} stat points (1=STR, 2=DEX, 3=VIT)", _labelStyle);
                _labelStyle.normal.textColor = Color.white;
            }

            // --- Damage flash (red vignette) ---
            if (_damageFlashTimer > 0f)
            {
                float alpha = (_damageFlashTimer / DAMAGE_FLASH_DURATION) * 0.3f;
                DrawRect(0, 0, Screen.width, Screen.height, new Color(1f, 0f, 0f, alpha));
            }

            // --- Level up flash ---
            if (_levelUpFlashTimer > 0f)
            {
                float alpha = (_levelUpFlashTimer / LEVEL_UP_FLASH_DURATION) * 0.5f;
                DrawRect(0, 0, Screen.width, Screen.height, new Color(1f, 0.85f, 0.3f, alpha * 0.15f));

                var centerStyle = new GUIStyle(_levelStyle)
                {
                    fontSize = Mathf.RoundToInt(36 * scale)
                };
                centerStyle.normal.textColor = new Color(1f, 0.85f, 0.3f, alpha * 2f);
                GUI.Label(new Rect(0, Screen.height * 0.3f, Screen.width, 50f * scale),
                    $"LEVEL {_stats.Level}!", centerStyle);
            }

            // --- Crosshair ---
            if (!_isDead)
            {
                float chSize = 4f * scale;
                DrawRect(Screen.width * 0.5f - chSize * 0.5f, Screen.height * 0.5f - chSize * 0.5f,
                    chSize, chSize, new Color(1f, 1f, 1f, 0.5f));
            }

            // --- Death screen ---
            if (_isDead)
            {
                // Dark overlay
                DrawRect(0, 0, Screen.width, Screen.height, new Color(0.05f, 0f, 0f, _deathFadeAlpha * 0.75f));

                // "YOU DIED" text
                var deathStyle = new GUIStyle(_levelStyle)
                {
                    fontSize = Mathf.RoundToInt(52 * scale)
                };
                deathStyle.normal.textColor = new Color(0.8f, 0.15f, 0.1f, _deathFadeAlpha);
                GUI.Label(new Rect(0, Screen.height * 0.35f, Screen.width, 70f * scale),
                    "YOU DIED", deathStyle);

                // Respawn countdown
                if (_deathTimer > 0f)
                {
                    var countStyle = new GUIStyle(_labelStyle)
                    {
                        fontSize = Mathf.RoundToInt(18 * scale),
                        alignment = TextAnchor.MiddleCenter
                    };
                    countStyle.normal.textColor = new Color(0.8f, 0.7f, 0.6f, _deathFadeAlpha);
                    GUI.Label(new Rect(0, Screen.height * 0.48f, Screen.width, 30f * scale),
                        $"Respawning in {Mathf.CeilToInt(_deathTimer)}...", countStyle);
                }
            }
        }

        private void DrawBar(float x, float y, float w, float h, float fill, Color fillColor, Color bgColor)
        {
            // Background
            DrawRect(x, y, w, h, bgColor);
            // Fill
            DrawRect(x + 1, y + 1, (w - 2) * Mathf.Clamp01(fill), h - 2, fillColor);
            // Border
            DrawRect(x, y, w, 1, new Color(0, 0, 0, 0.5f));                // top
            DrawRect(x, y + h - 1, w, 1, new Color(0, 0, 0, 0.5f));       // bottom
            DrawRect(x, y, 1, h, new Color(0, 0, 0, 0.5f));                // left
            DrawRect(x + w - 1, y, 1, h, new Color(0, 0, 0, 0.5f));       // right
        }

        private void DrawRect(float x, float y, float w, float h, Color color)
        {
            GUI.color = color;
            GUI.DrawTexture(new Rect(x, y, w, h), _whiteTex);
            GUI.color = Color.white;
        }

        private void DrawCircle(float x, float y, float size, Color color)
        {
            // Approximate circle with filled rect + rounded feel via multiple rects
            DrawRect(x + size * 0.15f, y, size * 0.7f, size, color);
            DrawRect(x, y + size * 0.15f, size, size * 0.7f, color);
            DrawRect(x + size * 0.07f, y + size * 0.07f, size * 0.86f, size * 0.86f, color);
        }
    }
}
