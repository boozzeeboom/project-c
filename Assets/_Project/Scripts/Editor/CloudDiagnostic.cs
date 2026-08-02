// CloudDiagnostic.cs — Runtime diagnostic for VolumetricCloudsRenderFeature
// Checks: shader, CloudNoise3D texture, rendering state

using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class CloudDiagnostic
{
    [MenuItem("Tools/Cloud/Diagnostic - Quick Check")]
    public static void Execute()
    {
        Debug.Log("=== Cloud Diagnostic ===");

        // 1. Find shader
        var shader = Shader.Find("Hidden/ProjectC/VolumetricClouds");
        if (shader == null)
        {
            Debug.LogError("[DIAG] Shader 'Hidden/ProjectC/VolumetricClouds' NOT FOUND!");
            return;
        }
        Debug.Log($"[DIAG] Shader found: {shader.name}, passCount={shader.passCount}");

        // 2. Create test material
        var mat = new Material(shader);
        Debug.Log($"[DIAG] Material created, passCount={mat.passCount}");

        // 3. Check CloudNoise3D
        var noiseAsset = AssetDatabase.LoadAssetAtPath<Texture3D>("Assets/_Project/Data/Clouds/CloudNoise3D.asset");
        if (noiseAsset == null)
        {
            Debug.LogError("[DIAG] CloudNoise3D.asset NOT FOUND!");
        }
        else
        {
            Debug.Log($"[DIAG] CloudNoise3D: {noiseAsset.width}x{noiseAsset.height}x{noiseAsset.depth}, " +
                $"format={noiseAsset.format}, mipmaps={noiseAsset.mipmapCount}, " +
                $"isReadable={noiseAsset.isReadable}");

            // Check if data is valid (sample a few pixels if readable)
            if (noiseAsset.isReadable)
            {
                var px = noiseAsset.GetPixel(0, 0, 0, 0);
                Debug.Log($"[DIAG] CloudNoise3D[0,0,0] = RGBA({px.r:F4}, {px.g:F4}, {px.b:F4}, {px.a:F4})");
                var mid = noiseAsset.GetPixel(64, 64, 64, 0);
                Debug.Log($"[DIAG] CloudNoise3D[64,64,64] = RGBA({mid.r:F4}, {mid.g:F4}, {mid.b:F4}, {mid.a:F4})");
            }
        }

        // 4. Check BlueNoise (from RenderFeature, via reflection)
        var pipeline = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
        if (pipeline == null)
        {
            Debug.LogError("[DIAG] No URP pipeline found!");
        }
        else
        {
            Debug.Log($"[DIAG] URP Pipeline: {pipeline.name}");

            // Try to access renderer features via SerializedObject (rendererDataList elements are ScriptableObjects)
            var rendererDataList = pipeline.rendererDataList;
            if (rendererDataList.Length == 0)
            {
                Debug.LogWarning("[DIAG] rendererDataList is empty");
            }
            else
            {
                var rendererData = rendererDataList[0];
                var rso = new SerializedObject(rendererData);
                var featuresProp = rso.FindProperty("m_RendererFeatures");
                if (featuresProp != null && featuresProp.isArray)
                {
                    Debug.Log($"[DIAG] Renderer features count: {featuresProp.arraySize}");
                    for (int i = 0; i < featuresProp.arraySize; i++)
                    {
                        var elem = featuresProp.GetArrayElementAtIndex(i);
                        Debug.Log($"[DIAG]   Feature [{i}]: {elem.objectReferenceValue?.GetType().Name ?? "null"}");
                    }
                }
            }
        }

        // 5. Check Sun
        var sun = RenderSettings.sun;
        if (sun == null)
            Debug.LogWarning("[DIAG] RenderSettings.sun is NULL");
        else
            Debug.Log($"[DIAG] Sun: {sun.name}, forward={sun.transform.forward}");

        Object.DestroyImmediate(mat);
        Debug.Log("=== Diagnostic Complete ===");
    }
}
