// Assets/Scripts/Enemies/EnemyHealthBar.cs
// ==========================================
// World-space health bar rendered above enemy using OnGUI (billboard IMGUI).

using UnityEngine;

namespace FantasyGame.Enemies
{
    public class EnemyHealthBar : MonoBehaviour
    {
        private EnemyBase _enemy;
        private UnityEngine.Camera _cam;

        private const float BAR_WIDTH = 60f;
        private const float BAR_HEIGHT = 8f;
        private const float OFFSET_Y = 2.2f;     // World units above enemy
        private const float MAX_DISTANCE = 25f;   // Don't draw beyond this

        private static Texture2D _bgTex;
        private static Texture2D _hpTex;
        private static Texture2D _borderTex;

        public void Init(EnemyBase enemy)
        {
            _enemy = enemy;
        }

        private void Awake()
        {
            // Create shared textures once
            if (_bgTex == null)
            {
                _bgTex = MakeTex(new Color(0.15f, 0.15f, 0.15f, 0.8f));
                _hpTex = MakeTex(new Color(0.8f, 0.15f, 0.15f, 0.9f));
                _borderTex = MakeTex(new Color(0f, 0f, 0f, 0.9f));
            }
        }

        private static Texture2D MakeTex(Color c)
        {
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, c);
            tex.Apply();
            return tex;
        }

        private void OnGUI()
        {
            if (_enemy == null || !_enemy.IsAlive) return;

            if (_cam == null)
                _cam = UnityEngine.Camera.main;
            if (_cam == null) return;

            Vector3 worldPos = transform.position + Vector3.up * OFFSET_Y;
            float dist = Vector3.Distance(_cam.transform.position, worldPos);
            if (dist > MAX_DISTANCE) return;

            // Behind camera check
            Vector3 viewportPos = _cam.WorldToViewportPoint(worldPos);
            if (viewportPos.z < 0) return;

            Vector3 screenPos = _cam.WorldToScreenPoint(worldPos);
            float x = screenPos.x - BAR_WIDTH * 0.5f;
            float y = Screen.height - screenPos.y;

            float hpPct = (float)_enemy.CurrentHealth / _enemy.MaxHealth;

            // Scale bar by distance (smaller when further)
            float scale = Mathf.Clamp(1f - (dist / MAX_DISTANCE) * 0.5f, 0.5f, 1f);
            float w = BAR_WIDTH * scale;
            float h = BAR_HEIGHT * scale;
            float bx = screenPos.x - w * 0.5f;

            // Border
            GUI.DrawTexture(new Rect(bx - 1, y - 1, w + 2, h + 2), _borderTex);
            // Background
            GUI.DrawTexture(new Rect(bx, y, w, h), _bgTex);
            // Health fill — lerp green to red
            Color hpColor = Color.Lerp(new Color(0.8f, 0.15f, 0.15f), new Color(0.2f, 0.8f, 0.2f), hpPct);
            var hpFillTex = MakeTex(hpColor);
            GUI.DrawTexture(new Rect(bx, y, w * hpPct, h), hpFillTex);

            // Name label
            var style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.RoundToInt(10 * scale),
                normal = { textColor = Color.white }
            };
            GUI.Label(new Rect(bx - 20, y - h - 4, w + 40, h + 2), _enemy.EnemyName, style);
        }
    }
}
