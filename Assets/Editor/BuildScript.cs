using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class BuildScript
{
    private const string BUILD_PATH = "Builds/WebGL";

    // GLTFast shader graph GUIDs that must survive build stripping
    private static readonly string[] GltfastShaderGuids = {
        "b9d29dfa1474148e792ac720cbd45122", // glTF-pbrMetallicRoughness
        "9a07dad0f3c4e43ff8312e3b5fa42300", // glTF-pbrSpecularGlossiness
        "c87047c884d9843f5b0f4cce282aa760", // glTF-unlit
    };

    [MenuItem("Build/Build WebGL")]
    public static void BuildWebGL()
    {
        string[] scenes = EditorBuildSettings.scenes
            .Where(s => s.enabled)
            .Select(s => s.path)
            .ToArray();

        if (scenes.Length == 0)
        {
            Debug.LogError("[BuildScript] No scenes in Build Settings!");
            EditorApplication.Exit(1);
            return;
        }

        // Force-include GLTFast shaders by adding them to preloaded assets
        EnsureGltfastShadersPreloaded();

        // WebGL player settings
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.WebGL.dataCaching = true;
        PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
        PlayerSettings.WebGL.template = "APPLICATION:Default";

        var buildOptions = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = BUILD_PATH,
            target = BuildTarget.WebGL,
            options = BuildOptions.None
        };

        Debug.Log($"[BuildScript] Building WebGL with {scenes.Length} scene(s) to {BUILD_PATH}...");

        BuildReport report = BuildPipeline.BuildPlayer(buildOptions);
        BuildSummary summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"[BuildScript] Build SUCCEEDED — {summary.totalSize / (1024 * 1024f):F1} MB, took {summary.totalTime}");
            EditorApplication.Exit(0);
        }
        else
        {
            Debug.LogError($"[BuildScript] Build FAILED: {summary.result}");
            foreach (var step in report.steps)
            {
                foreach (var message in step.messages)
                {
                    if (message.type == LogType.Error)
                        Debug.LogError($"  [{step.name}] {message.content}");
                }
            }
            EditorApplication.Exit(1);
        }
    }

    private static void EnsureGltfastShadersPreloaded()
    {
        var preloaded = new List<Object>(PlayerSettings.GetPreloadedAssets());

        foreach (var guid in GltfastShaderGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogWarning($"[BuildScript] Could not find asset for GUID: {guid}");
                continue;
            }

            var asset = AssetDatabase.LoadAssetAtPath<Shader>(path);
            if (asset == null)
            {
                Debug.LogWarning($"[BuildScript] Could not load shader at: {path}");
                continue;
            }

            if (!preloaded.Contains(asset))
            {
                preloaded.Add(asset);
                Debug.Log($"[BuildScript] Added preloaded shader: {path}");
            }
        }

        PlayerSettings.SetPreloadedAssets(preloaded.ToArray());
    }
}
