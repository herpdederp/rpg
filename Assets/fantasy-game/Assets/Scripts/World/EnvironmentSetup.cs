using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FantasyGame.World
{
    public class EnvironmentSetup : MonoBehaviour
    {
        private Light _sunLight;
        private GameObject _skyDome;

        public Light SunLight => _sunLight;

        public void Init()
        {
            SetupDirectionalLight();
            SetupFog();
            SetupPostProcessing();
            SetupSkyDome();
        }

        private void SetupDirectionalLight()
        {
            // Remove any existing lights
            foreach (var existingLight in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                Destroy(existingLight.gameObject);
            }

            var sunGo = new GameObject("Sun");
            sunGo.transform.rotation = Quaternion.Euler(45f, -30f, 0f);

            _sunLight = sunGo.AddComponent<Light>();
            _sunLight.type = LightType.Directional;
            _sunLight.color = new Color(1.0f, 0.95f, 0.82f); // Warm sunlight
            _sunLight.intensity = 1.2f;
            _sunLight.shadows = LightShadows.Soft;
            _sunLight.shadowStrength = 0.7f;
            _sunLight.shadowBias = 0.05f;
            _sunLight.shadowNormalBias = 0.4f;

            // URP additional light data (bias is already set via Light component above)
            sunGo.AddComponent<UniversalAdditionalLightData>();
        }

        private void SetupFog()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 80f;
            RenderSettings.fogEndDistance = 200f;
            RenderSettings.fogColor = new Color(0.72f, 0.62f, 0.50f); // Warm amber fog

            // Ambient light - warm fill
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.35f, 0.30f, 0.25f);
        }

        private void SetupPostProcessing()
        {
            // Create a global volume for post-processing
            var volumeGo = new GameObject("PostProcessVolume");
            var volume = volumeGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10;

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();

            // Tonemapping - ACES for cinematic look
            var tonemap = profile.Add<Tonemapping>();
            tonemap.mode.Override(TonemappingMode.ACES);

            // Bloom - subtle glow
            var bloom = profile.Add<Bloom>();
            bloom.threshold.Override(0.9f);
            bloom.intensity.Override(0.3f);
            bloom.scatter.Override(0.7f);

            // Vignette - darken edges for painterly framing
            var vignette = profile.Add<Vignette>();
            vignette.intensity.Override(0.25f);
            vignette.smoothness.Override(0.4f);

            // Color Adjustments - warm the overall palette
            var colorAdj = profile.Add<ColorAdjustments>();
            colorAdj.colorFilter.Override(new Color(1.0f, 0.97f, 0.92f)); // Slight warm filter
            colorAdj.saturation.Override(10f); // Slightly more vivid
            colorAdj.contrast.Override(10f);   // Slightly punchier

            volume.profile = profile;
        }

        private void SetupSkyDome()
        {
            // Create an inverted sphere for the sky
            _skyDome = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _skyDome.name = "SkyDome";
            _skyDome.transform.localScale = Vector3.one * 500f;

            // Remove collider (sky shouldn't block physics)
            var collider = _skyDome.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            // Apply sky shader
            var shader = Shader.Find("FantasyGame/PainterlySky");
            if (shader == null)
            {
                // Fallback: use unlit with sky color
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            var mat = new Material(shader);
            mat.SetColor("_ZenithColor", new Color(0.25f, 0.40f, 0.70f));  // Deep sky blue
            mat.SetColor("_HorizonColor", new Color(0.85f, 0.70f, 0.50f)); // Warm peach
            mat.SetColor("_NadirColor", new Color(0.15f, 0.18f, 0.30f));   // Dark blue-grey

            // Render on the inside of the sphere
            mat.SetFloat("_Cull", 1); // Front face culling = render back faces
            mat.renderQueue = (int)RenderQueue.Background;

            _skyDome.GetComponent<Renderer>().material = mat;

            // Attach to camera so it follows
            var cam = UnityEngine.Camera.main;
            if (cam != null)
            {
                _skyDome.transform.SetParent(cam.transform);
                _skyDome.transform.localPosition = Vector3.zero;
            }
        }

        private void LateUpdate()
        {
            // Keep sky dome centered on camera
            if (_skyDome != null && UnityEngine.Camera.main != null)
            {
                _skyDome.transform.position = UnityEngine.Camera.main.transform.position;
            }
        }
    }
}
