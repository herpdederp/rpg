// Assets/Scripts/UI/PauseMenu.cs
// ================================
// ESC to pause/resume. Shows controls help, resume, and quit buttons.
// Built entirely with IMGUI — zero asset dependencies.

using UnityEngine;
using UnityEngine.InputSystem;

namespace FantasyGame.UI
{
    public class PauseMenu : MonoBehaviour
    {
        public static bool IsPaused { get; private set; }

        private Texture2D _whiteTex;
        private bool _showControls;

        public void Init()
        {
            _whiteTex = new Texture2D(1, 1);
            _whiteTex.SetPixel(0, 0, Color.white);
            _whiteTex.Apply();

            Debug.Log("[PauseMenu] Initialized. Press ESC to pause.");
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                // Don't open pause menu if inventory is open (ESC closes inventory first)
                if (InventoryUI.IsOpen) return;

                if (_showControls)
                {
                    _showControls = false;
                }
                else
                {
                    TogglePause();
                }
            }
        }

        private void TogglePause()
        {
            IsPaused = !IsPaused;

            if (IsPaused)
            {
                Time.timeScale = 0f;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Time.timeScale = 1f;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                _showControls = false;
            }
        }

        private void OnGUI()
        {
            if (!IsPaused) return;

            float scale = Screen.height / 1080f;

            // Dark overlay
            GUI.color = new Color(0f, 0f, 0f, 0.7f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _whiteTex);
            GUI.color = Color.white;

            if (_showControls)
            {
                DrawControlsScreen(scale);
            }
            else
            {
                DrawMainMenu(scale);
            }
        }

        private void DrawMainMenu(float scale)
        {
            float boxW = 350f * scale;
            float boxH = 340f * scale;
            float boxX = (Screen.width - boxW) * 0.5f;
            float boxY = (Screen.height - boxH) * 0.5f;

            // Background panel
            GUI.color = new Color(0.1f, 0.08f, 0.14f, 0.95f);
            GUI.DrawTexture(new Rect(boxX, boxY, boxW, boxH), _whiteTex);
            // Border
            GUI.color = new Color(0.5f, 0.4f, 0.3f, 0.8f);
            DrawBorder(boxX, boxY, boxW, boxH, 2f);
            GUI.color = Color.white;

            // Title
            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(32 * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            titleStyle.normal.textColor = new Color(1f, 0.85f, 0.5f);
            GUI.Label(new Rect(boxX, boxY + 20f * scale, boxW, 50f * scale), "PAUSED", titleStyle);

            // Buttons
            float btnW = 220f * scale;
            float btnH = 45f * scale;
            float btnX = boxX + (boxW - btnW) * 0.5f;
            float btnStartY = boxY + 90f * scale;
            float btnGap = 60f * scale;

            if (DrawButton(btnX, btnStartY, btnW, btnH, "Resume", scale))
            {
                TogglePause();
            }

            if (DrawButton(btnX, btnStartY + btnGap, btnW, btnH, "Controls", scale))
            {
                _showControls = true;
            }

            if (DrawButton(btnX, btnStartY + btnGap * 2, btnW, btnH, "Quit", scale))
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            }

            // Version
            var versionStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(11 * scale),
                alignment = TextAnchor.MiddleCenter
            };
            versionStyle.normal.textColor = new Color(0.5f, 0.45f, 0.4f);
            GUI.Label(new Rect(boxX, boxY + boxH - 30f * scale, boxW, 20f * scale),
                "Fantasy Sandbox v0.5", versionStyle);
        }

        private void DrawControlsScreen(float scale)
        {
            float boxW = 450f * scale;
            float boxH = 500f * scale;
            float boxX = (Screen.width - boxW) * 0.5f;
            float boxY = (Screen.height - boxH) * 0.5f;

            // Background
            GUI.color = new Color(0.1f, 0.08f, 0.14f, 0.95f);
            GUI.DrawTexture(new Rect(boxX, boxY, boxW, boxH), _whiteTex);
            GUI.color = new Color(0.5f, 0.4f, 0.3f, 0.8f);
            DrawBorder(boxX, boxY, boxW, boxH, 2f);
            GUI.color = Color.white;

            // Title
            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(26 * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            titleStyle.normal.textColor = new Color(1f, 0.85f, 0.5f);
            GUI.Label(new Rect(boxX, boxY + 15f * scale, boxW, 40f * scale), "CONTROLS", titleStyle);

            // Controls list
            var keyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(15 * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            keyStyle.normal.textColor = new Color(0.9f, 0.8f, 0.5f);

            var descStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(14 * scale),
                alignment = TextAnchor.MiddleLeft
            };
            descStyle.normal.textColor = new Color(0.85f, 0.82f, 0.75f);

            string[,] controls = {
                { "W A S D", "Move" },
                { "Left Shift", "Sprint" },
                { "Space", "Jump" },
                { "Mouse", "Look Around" },
                { "Left Click", "Attack" },
                { "E", "Interact / Talk" },
                { "Q", "Use Potion" },
                { "1 / 2 / 3", "Spend Stat Points (STR/DEX/VIT)" },
                { "Tab", "Toggle Inventory" },
                { "ESC", "Pause Menu" },
            };

            float lineH = 32f * scale;
            float keyW = 140f * scale;
            float startY = boxY + 70f * scale;
            float padX = 30f * scale;

            for (int i = 0; i < controls.GetLength(0); i++)
            {
                float y = startY + i * lineH;
                GUI.Label(new Rect(boxX + padX, y, keyW, lineH), controls[i, 0], keyStyle);
                GUI.Label(new Rect(boxX + padX + keyW + 10f * scale, y, boxW - keyW - padX * 2, lineH),
                    controls[i, 1], descStyle);

                // Separator line
                if (i < controls.GetLength(0) - 1)
                {
                    GUI.color = new Color(0.3f, 0.25f, 0.2f, 0.3f);
                    GUI.DrawTexture(new Rect(boxX + padX, y + lineH - 1, boxW - padX * 2, 1), _whiteTex);
                    GUI.color = Color.white;
                }
            }

            // Back button
            float btnW = 180f * scale;
            float btnH = 40f * scale;
            if (DrawButton((Screen.width - btnW) * 0.5f, boxY + boxH - 60f * scale, btnW, btnH, "Back", scale))
            {
                _showControls = false;
            }
        }

        private bool DrawButton(float x, float y, float w, float h, string text, float scale)
        {
            Rect btnRect = new Rect(x, y, w, h);
            bool hover = btnRect.Contains(Event.current.mousePosition);

            // Button background
            Color bgColor = hover
                ? new Color(0.35f, 0.28f, 0.2f, 0.9f)
                : new Color(0.2f, 0.16f, 0.12f, 0.9f);
            GUI.color = bgColor;
            GUI.DrawTexture(btnRect, _whiteTex);

            // Button border
            Color borderColor = hover
                ? new Color(0.8f, 0.65f, 0.3f, 0.9f)
                : new Color(0.5f, 0.4f, 0.3f, 0.6f);
            GUI.color = borderColor;
            DrawBorder(x, y, w, h, 1f);
            GUI.color = Color.white;

            // Button text
            var btnStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(18 * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            btnStyle.normal.textColor = hover
                ? new Color(1f, 0.9f, 0.6f)
                : new Color(0.85f, 0.8f, 0.7f);
            GUI.Label(btnRect, text, btnStyle);

            // Click detection
            if (hover && Event.current.type == EventType.MouseDown && Event.current.button == 0)
            {
                Event.current.Use();
                return true;
            }
            return false;
        }

        private void DrawBorder(float x, float y, float w, float h, float thickness)
        {
            GUI.DrawTexture(new Rect(x, y, w, thickness), _whiteTex);
            GUI.DrawTexture(new Rect(x, y + h - thickness, w, thickness), _whiteTex);
            GUI.DrawTexture(new Rect(x, y, thickness, h), _whiteTex);
            GUI.DrawTexture(new Rect(x + w - thickness, y, thickness, h), _whiteTex);
        }

        private void OnDestroy()
        {
            // Ensure time scale is restored if destroyed while paused
            if (IsPaused)
            {
                Time.timeScale = 1f;
                IsPaused = false;
            }
        }
    }
}
