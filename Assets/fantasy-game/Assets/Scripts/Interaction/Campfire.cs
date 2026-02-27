// Assets/Scripts/Interaction/Campfire.cs
// ========================================
// Rest point. Heals player to full, restores stamina.
// Reusable with cooldown. Animated flame particles.

using UnityEngine;
using FantasyGame.RPG;

namespace FantasyGame.Interaction
{
    public class Campfire : Interactable
    {
        private float _cooldownTimer;
        private const float REST_COOLDOWN = 30f;
        private bool _resting;
        private float _restTimer;
        private const float REST_DURATION = 2f;

        // Flame particles (simple animated cubes)
        private GameObject[] _flames;
        private float[] _flamePhases;

        public void Init()
        {
            PromptText = "Rest";
            InteractRange = 3f;
            CreateFlameEffect();
        }

        protected override void OnInteract()
        {
            if (_cooldownTimer > 0f)
            {
                Debug.Log($"[Campfire] Not ready yet ({_cooldownTimer:F0}s)");
                return;
            }

            var statsComp = FindAnyObjectByType<PlayerStatsComponent>();
            if (statsComp == null) return;

            _resting = true;
            _restTimer = REST_DURATION;

            // Heal to full
            statsComp.Stats.Heal(statsComp.Stats.MaxHealth);

            // Heal VFX
            if (VFX.ParticleEffectManager.Instance != null)
                VFX.ParticleEffectManager.Instance.SpawnHealEffect(transform.position + Vector3.up * 0.5f);

            Debug.Log("[Campfire] Resting... Health and stamina restored!");

            _cooldownTimer = REST_COOLDOWN;
        }

        protected override void Update()
        {
            base.Update();

            if (_cooldownTimer > 0f)
            {
                _cooldownTimer -= Time.deltaTime;
                if (_cooldownTimer <= 0f)
                    PromptText = "Rest";
                else
                    PromptText = $"Rest ({Mathf.CeilToInt(_cooldownTimer)}s)";
            }

            if (_resting)
            {
                _restTimer -= Time.deltaTime;
                if (_restTimer <= 0f)
                    _resting = false;
            }

            // Animate flames
            AnimateFlames();
        }

        private void CreateFlameEffect()
        {
            int count = 5;
            _flames = new GameObject[count];
            _flamePhases = new float[count];

            for (int i = 0; i < count; i++)
            {
                var flame = GameObject.CreatePrimitive(PrimitiveType.Cube);
                flame.name = "Flame";
                flame.transform.SetParent(transform);

                float angle = (float)i / count * Mathf.PI * 2f;
                float radius = 0.15f;
                flame.transform.localPosition = new Vector3(
                    Mathf.Cos(angle) * radius,
                    0.3f,
                    Mathf.Sin(angle) * radius
                );
                flame.transform.localScale = new Vector3(0.08f, 0.2f, 0.08f);

                var col = flame.GetComponent<Collider>();
                if (col != null) Destroy(col);

                var renderer = flame.GetComponent<Renderer>();
                if (renderer != null)
                {
                    var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color"));
                    mat.color = new Color(1f, 0.6f, 0.1f);
                    renderer.material = mat;
                }

                _flames[i] = flame;
                _flamePhases[i] = Random.value * Mathf.PI * 2f;
            }
        }

        private void AnimateFlames()
        {
            if (_flames == null) return;

            for (int i = 0; i < _flames.Length; i++)
            {
                if (_flames[i] == null) continue;

                float phase = _flamePhases[i];
                float t = Time.time * 3f + phase;

                // Flicker height
                float heightScale = 0.15f + Mathf.Sin(t * 2.3f) * 0.08f + Mathf.Sin(t * 5.7f) * 0.04f;
                _flames[i].transform.localScale = new Vector3(0.06f, heightScale, 0.06f);

                // Slight sway
                float swayX = Mathf.Sin(t * 1.7f) * 0.03f;
                float swayZ = Mathf.Cos(t * 2.1f) * 0.03f;
                float baseAngle = (float)i / _flames.Length * Mathf.PI * 2f;
                _flames[i].transform.localPosition = new Vector3(
                    Mathf.Cos(baseAngle) * 0.15f + swayX,
                    0.25f + heightScale * 0.5f,
                    Mathf.Sin(baseAngle) * 0.15f + swayZ
                );

                // Color flicker
                var renderer = _flames[i].GetComponent<Renderer>();
                if (renderer != null)
                {
                    float flicker = 0.5f + Mathf.Sin(t * 4f) * 0.3f + Mathf.Sin(t * 7f) * 0.2f;
                    renderer.material.color = Color.Lerp(
                        new Color(1f, 0.3f, 0f),
                        new Color(1f, 0.8f, 0.2f),
                        flicker
                    );
                }
            }
        }

        // Resting overlay
        protected override void OnGUI()
        {
            // Show base interact prompt
            base.OnGUI();

            if (_resting)
            {
                float scale = Screen.height / 1080f;
                float alpha = Mathf.Clamp01(_restTimer / REST_DURATION) * 0.4f;

                // Warm glow overlay
                GUI.color = new Color(1f, 0.7f, 0.3f, alpha * 0.15f);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
                GUI.color = Color.white;

                var style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = Mathf.RoundToInt(24 * scale),
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                style.normal.textColor = new Color(1f, 0.85f, 0.5f, 1f);
                GUI.Label(new Rect(0, Screen.height * 0.4f, Screen.width, 40 * scale),
                    "Resting...", style);
            }
        }
    }
}
