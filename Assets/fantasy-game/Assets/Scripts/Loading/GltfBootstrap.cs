// Assets/Scripts/Loading/GltfBootstrap.cs
// =========================================
// Loads the rigged humanoid glTF at runtime, attaches the
// ThirdPersonController, and spawns a follow camera.
// Uses [RuntimeInitializeOnLoadMethod] so it works without
// any manual scene setup — just hit Play.
//
// Works on both Desktop (standalone) and WebGL.

using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using GLTFast;

namespace FantasyGame.Loading
{
    public class GltfBootstrap : MonoBehaviour
    {
        private const string MODEL_FILENAME = "humanoid_rigged.glb";
        private const float GROUND_SIZE = 50f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBootstrap()
        {
            if (FindAnyObjectByType<GltfBootstrap>() != null)
            {
                Debug.Log("[GltfBootstrap] Instance already in scene, skipping auto-create.");
                return;
            }

            Debug.Log("[GltfBootstrap] Auto-creating bootstrap...");
            var go = new GameObject("GameBootstrap");
            go.AddComponent<GltfBootstrap>();
        }

        private async void Start()
        {
            try
            {
                Debug.Log("[GltfBootstrap] Start() running...");
                EnsureEnvironment();
                await LoadAndSetupCharacter();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[GltfBootstrap] Exception during setup:\n{ex}");
            }
        }

        private async Task LoadAndSetupCharacter()
        {
            // --- Build the URL / path to the model ---
            // On WebGL, streamingAssetsPath is an HTTP URL.
            // On desktop, it's a local file path.
            // GLTFast.Load(string uri) handles both URL and file:// URIs.
            // Build model URL — use "/" separator (not Path.Combine which uses backslashes)
            string basePath = Application.streamingAssetsPath;
            string modelUrl;
            if (basePath.StartsWith("http", System.StringComparison.OrdinalIgnoreCase)
                || basePath.StartsWith("jar:", System.StringComparison.OrdinalIgnoreCase))
            {
                // WebGL or Android: streamingAssetsPath is already a URL
                modelUrl = basePath + "/Models/" + MODEL_FILENAME;
            }
            else
            {
                // Desktop: convert local file path to file:// URI
                string localPath = Path.Combine(basePath, "Models", MODEL_FILENAME);
                modelUrl = new System.Uri(localPath).AbsoluteUri;
            }

            Debug.Log($"[GltfBootstrap] Loading model from: {modelUrl}");

            // --- Load glTF via URL (works on all platforms including WebGL) ---
            var gltf = new GltfImport();
            bool success = await gltf.Load(modelUrl);
            Debug.Log($"[GltfBootstrap] Load result: {success}");

            if (!success)
            {
                Debug.LogError("[GltfBootstrap] Failed to load glTF.");
                return;
            }

            // --- Instantiate with GameObjectInstantiator (needed for animations) ---
            var characterRoot = new GameObject("Character");
            characterRoot.transform.position = Vector3.zero;

            var instantiator = new GameObjectInstantiator(gltf, characterRoot.transform);
            bool instantiated = await gltf.InstantiateMainSceneAsync(instantiator);

            if (!instantiated)
            {
                Debug.LogError("[GltfBootstrap] Failed to instantiate glTF scene.");
                return;
            }

            Debug.Log($"[GltfBootstrap] Model instantiated. Children: {characterRoot.transform.childCount}");

            var renderers = characterRoot.GetComponentsInChildren<Renderer>(true);
            Debug.Log($"[GltfBootstrap] Renderers found: {renderers.Length}");

            if (renderers.Length == 0)
            {
                Debug.LogWarning("[GltfBootstrap] No renderers found! Creating debug cube.");
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = "DebugCube";
                cube.transform.SetParent(characterRoot.transform);
                cube.transform.localPosition = new Vector3(0, 1f, 0);
                var cubeShader = FindAvailableShader();
                if (cubeShader != null)
                {
                    var cubeMat = new Material(cubeShader);
                    cubeMat.color = Color.red;
                    cube.GetComponent<Renderer>().material = cubeMat;
                }
            }

            // --- Attach character controller ---
            SetupCharacterController(characterRoot);

            // --- Attach animations ---
            SetupAnimation(characterRoot);

            // --- Attach camera ---
            SetupCamera(characterRoot.transform);

            Debug.Log("[GltfBootstrap] Character ready! WASD to move, mouse to look, Space to jump.");
        }

        private void SetupCharacterController(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>();
            Bounds bounds = new Bounds(root.transform.position, Vector3.zero);
            foreach (var r in renderers)
                bounds.Encapsulate(r.bounds);

            float height = bounds.size.y;
            float radius = Mathf.Max(bounds.size.x, bounds.size.z) * 0.3f;

            if (height < 0.01f) height = 1.8f;
            if (radius < 0.01f) radius = 0.3f;

            Debug.Log($"[GltfBootstrap] CharacterController: height={height:F2}, radius={radius:F2}");

            var cc = root.AddComponent<CharacterController>();
            cc.height = height;
            cc.radius = radius;
            cc.center = new Vector3(0, height * 0.5f, 0);
            cc.skinWidth = 0.01f;

            var controller = root.AddComponent<Player.ThirdPersonController>();
            controller.Init(cc, height);
        }

        private void SetupAnimation(GameObject root)
        {
            var anim = root.GetComponentInChildren<Animation>();
            if (anim == null)
            {
                Debug.LogWarning("[GltfBootstrap] No Animation component found on imported model.");
                return;
            }

            var controller = root.GetComponent<Player.ThirdPersonController>();
            if (controller == null)
            {
                Debug.LogWarning("[GltfBootstrap] No ThirdPersonController found.");
                return;
            }

            var characterAnimator = root.AddComponent<Player.CharacterAnimator>();
            characterAnimator.Init(anim, controller);

            Debug.Log($"[GltfBootstrap] Animation setup complete. Clips: {anim.GetClipCount()}");
        }

        private void SetupCamera(Transform target)
        {
            UnityEngine.Camera cam = UnityEngine.Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera");
                camGo.tag = "MainCamera";
                cam = camGo.AddComponent<UnityEngine.Camera>();
                camGo.AddComponent<AudioListener>();
            }

            var existing = cam.GetComponent<FantasyGame.Camera.ThirdPersonCamera>();
            if (existing != null) Destroy(existing);

            var follow = cam.gameObject.AddComponent<FantasyGame.Camera.ThirdPersonCamera>();
            follow.Init(target);

            Debug.Log("[GltfBootstrap] Camera attached and following character.");
        }

        private static Shader FindAvailableShader()
        {
            // Try shaders in order of preference
            string[] shaderNames = {
                "Universal Render Pipeline/Lit",
                "Universal Render Pipeline/Simple Lit",
                "Universal Render Pipeline/Unlit",
                "Standard",
                "Unlit/Color"
            };
            foreach (var name in shaderNames)
            {
                var s = Shader.Find(name);
                if (s != null)
                {
                    Debug.Log($"[GltfBootstrap] Using shader: {name}");
                    return s;
                }
            }
            Debug.LogWarning("[GltfBootstrap] No shader found!");
            return null;
        }

        private void EnsureEnvironment()
        {
            if (FindAnyObjectByType<MeshCollider>() == null
                && FindAnyObjectByType<BoxCollider>() == null)
            {
                var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
                ground.name = "Ground";
                ground.transform.localScale = Vector3.one * (GROUND_SIZE / 10f);
                ground.transform.position = Vector3.zero;

                var shader = FindAvailableShader();
                if (shader != null)
                {
                    var mat = new Material(shader);
                    mat.color = new Color(0.25f, 0.35f, 0.2f);
                    ground.GetComponent<Renderer>().material = mat;
                }

                Debug.Log("[GltfBootstrap] Ground plane created.");
            }
        }
    }
}
