// Assets/Scripts/World/DayNightCycle.cs
// =======================================
// Day/night cycle that rotates the directional light and changes
// ambient colors over time. Full cycle = 10 minutes by default.

using UnityEngine;

namespace FantasyGame.World
{
    public class DayNightCycle : MonoBehaviour
    {
        private Light _sun;
        private float _timeOfDay; // 0-1, 0=midnight, 0.25=sunrise, 0.5=noon, 0.75=sunset

        private const float FULL_CYCLE_MINUTES = 10f;
        private const float CYCLE_SPEED = 1f / (FULL_CYCLE_MINUTES * 60f);

        // Color presets
        private static readonly Color DAWN_COLOR = new Color(1f, 0.65f, 0.35f);
        private static readonly Color NOON_COLOR = new Color(1f, 0.95f, 0.85f);
        private static readonly Color DUSK_COLOR = new Color(1f, 0.45f, 0.25f);
        private static readonly Color NIGHT_COLOR = new Color(0.15f, 0.18f, 0.35f);

        private static readonly Color DAWN_AMBIENT = new Color(0.4f, 0.35f, 0.3f);
        private static readonly Color NOON_AMBIENT = new Color(0.55f, 0.5f, 0.45f);
        private static readonly Color DUSK_AMBIENT = new Color(0.35f, 0.25f, 0.3f);
        private static readonly Color NIGHT_AMBIENT = new Color(0.08f, 0.08f, 0.15f);

        public float TimeOfDay => _timeOfDay;
        public bool IsNight => _timeOfDay < 0.2f || _timeOfDay > 0.8f;

        public void Init()
        {
            // Start at morning
            _timeOfDay = 0.3f;

            // Find or create directional light
            _sun = FindDirectionalLight();
            if (_sun == null)
            {
                var sunGo = new GameObject("Sun");
                _sun = sunGo.AddComponent<Light>();
                _sun.type = LightType.Directional;
                _sun.shadows = LightShadows.Soft;
            }

            UpdateLighting();
            Debug.Log("[DayNightCycle] Initialized.");
        }

        private void Update()
        {
            _timeOfDay += CYCLE_SPEED * Time.deltaTime;
            if (_timeOfDay > 1f) _timeOfDay -= 1f;

            UpdateLighting();
        }

        private void UpdateLighting()
        {
            if (_sun == null) return;

            // Sun rotation: follows a circular arc
            // At timeOfDay=0.25 (dawn): sun at horizon (angle ~0)
            // At timeOfDay=0.5  (noon): sun overhead (angle ~90)
            // At timeOfDay=0.75 (dusk): sun at horizon (angle ~180)
            float sunAngle = (_timeOfDay - 0.25f) * 360f;
            _sun.transform.rotation = Quaternion.Euler(sunAngle, -30f, 0);

            // Light color and intensity based on time
            Color lightColor;
            float intensity;
            Color ambientColor;

            if (_timeOfDay < 0.2f) // Night (midnight to pre-dawn)
            {
                float t = _timeOfDay / 0.2f;
                lightColor = Color.Lerp(NIGHT_COLOR, DAWN_COLOR, t * 0.3f);
                ambientColor = Color.Lerp(NIGHT_AMBIENT, DAWN_AMBIENT, t * 0.3f);
                intensity = Mathf.Lerp(0.05f, 0.2f, t);
            }
            else if (_timeOfDay < 0.3f) // Dawn
            {
                float t = (_timeOfDay - 0.2f) / 0.1f;
                lightColor = Color.Lerp(DAWN_COLOR, NOON_COLOR, t);
                ambientColor = Color.Lerp(DAWN_AMBIENT, NOON_AMBIENT, t);
                intensity = Mathf.Lerp(0.3f, 0.8f, t);
            }
            else if (_timeOfDay < 0.7f) // Day
            {
                float t = (_timeOfDay - 0.3f) / 0.4f;
                // Peak at noon (0.5), gentle curve
                float noonFactor = 1f - Mathf.Abs(t - 0.5f) * 2f;
                lightColor = Color.Lerp(NOON_COLOR, NOON_COLOR * 0.9f, 1f - noonFactor);
                ambientColor = NOON_AMBIENT;
                intensity = Mathf.Lerp(0.7f, 1.0f, noonFactor);
            }
            else if (_timeOfDay < 0.8f) // Dusk
            {
                float t = (_timeOfDay - 0.7f) / 0.1f;
                lightColor = Color.Lerp(NOON_COLOR, DUSK_COLOR, t);
                ambientColor = Color.Lerp(NOON_AMBIENT, DUSK_AMBIENT, t);
                intensity = Mathf.Lerp(0.7f, 0.3f, t);
            }
            else // Night (dusk to midnight)
            {
                float t = (_timeOfDay - 0.8f) / 0.2f;
                lightColor = Color.Lerp(DUSK_COLOR, NIGHT_COLOR, t);
                ambientColor = Color.Lerp(DUSK_AMBIENT, NIGHT_AMBIENT, t);
                intensity = Mathf.Lerp(0.2f, 0.05f, t);
            }

            _sun.color = lightColor;
            _sun.intensity = intensity;

            // Ambient lighting
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = ambientColor;

            // Fog
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = IsNight ? 0.015f : 0.005f;
            RenderSettings.fogColor = Color.Lerp(ambientColor, lightColor, 0.3f);
        }

        private Light FindDirectionalLight()
        {
            var lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            foreach (var light in lights)
            {
                if (light.type == LightType.Directional)
                    return light;
            }
            return null;
        }
    }
}
