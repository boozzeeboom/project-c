// EdgeDetectionRenderFeature.cs — URP ScriptableRendererFeature (Unity 6 / URP 17.x)
// Borderlands-style post-process edge detection via Sobel on depth + normals.
// Shader: Assets/_Project/Shaders/EdgeDetection.shader
//
// Setup: add this feature to your URP Renderer asset (Forward Renderer / Universal Renderer).

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace ProjectC.Rendering
{
    [DisallowMultipleRendererFeature("Edge Detection")]
    [SupportedOnRenderer(typeof(UniversalRendererData))]
    public sealed class EdgeDetectionRenderFeature : ScriptableRendererFeature
    {
        [Header("Edge Settings")]
        [ColorUsage(false, false)] public Color EdgeColor = new Color(0.05f, 0.05f, 0.07f, 1f);
        [Range(1, 8)] public int EdgeWidth = 2;

        [Header("Depth Edge")]
        [Range(0.1f, 8f)] public float DepthSensitivity = 2.5f;
        [Range(0f, 0.5f)] public float DepthThreshold = 0.06f;

        [Header("Normal Edge")]
        [Range(0.1f, 8f)] public float NormalSensitivity = 1.5f;
        [Range(0f, 0.5f)] public float NormalThreshold = 0.08f;

        [Header("Pencil Style")]
        [Range(0f, 0.5f)] public float JitterAmount = 0.15f;
        [Range(1f, 20f)] public float JitterScale = 8f;
        [Range(0.01f, 0.3f)] public float LineSoftness = 0.06f;

        [Header("Material")]
        [Tooltip("Optional: pre-created material. If null, auto-created from Hidden/ProjectC/EdgeDetection shader.")]
        public Material OverrideMaterial;

        private Material _material;

        public Material GetOrCreateMaterial()
        {
            if (_material != null && _material.shader != null)
                return _material;

            if (OverrideMaterial != null)
            {
                _material = OverrideMaterial;
                return _material;
            }

            Shader shader = Shader.Find("Hidden/ProjectC/EdgeDetection");
            if (shader == null)
            {
                Debug.LogError("[EdgeDetectionFeature] Shader 'Hidden/ProjectC/EdgeDetection' not found.");
                return null;
            }

            _material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            return _material;
        }

        public override void Create() { }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (renderingData.cameraData.cameraType == CameraType.Preview)
                return;

            Material mat = GetOrCreateMaterial();
            if (mat == null) return;

            // Push inspector values into material
            mat.SetColor(Shader.PropertyToID("_EdgeColor"), EdgeColor);
            mat.SetFloat(Shader.PropertyToID("_EdgeWidth"), EdgeWidth);
            mat.SetFloat(Shader.PropertyToID("_DepthSensitivity"), DepthSensitivity);
            mat.SetFloat(Shader.PropertyToID("_DepthThreshold"), DepthThreshold);
            mat.SetFloat(Shader.PropertyToID("_NormalSensitivity"), NormalSensitivity);
            mat.SetFloat(Shader.PropertyToID("_NormalThreshold"), NormalThreshold);
            mat.SetFloat(Shader.PropertyToID("_JitterAmount"), JitterAmount);
            mat.SetFloat(Shader.PropertyToID("_JitterScale"), JitterScale);
            mat.SetFloat(Shader.PropertyToID("_LineSoftness"), LineSoftness);

            var pass = new EdgeDetectionPass(mat);
            pass.renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
            renderer.EnqueuePass(pass);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _material != null)
            {
#if UNITY_EDITOR
                Object.DestroyImmediate(_material);
#else
                Object.Destroy(_material);
#endif
                _material = null;
            }
        }
    }

    /// <summary>Fullscreen edge-detection pass using RenderGraph (URP 17.x / Unity 6).</summary>
    internal sealed class EdgeDetectionPass : ScriptableRenderPass
    {
        private const string PassName = "EdgeDetection";
        private Material _material;

        private class PassData
        {
            public Material Material;
        }

        public EdgeDetectionPass(Material material)
        {
            _material = material;
            profilingSampler = new ProfilingSampler(PassName);
            requiresIntermediateTexture = false;

            // Request depth + normal textures from URP
            ConfigureInput(ScriptableRenderPassInput.Normal | ScriptableRenderPassInput.Depth);
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (_material == null) return;

            var resourceData = frameData.Get<UniversalResourceData>();
            var cameraData = frameData.Get<UniversalCameraData>();

            // We draw on top of the already-rendered color using alpha blending.
            var colorTarget = resourceData.activeColorTexture;

            using (var builder = renderGraph.AddRasterRenderPass<PassData>(PassName, out var passData, profilingSampler))
            {
                passData.Material = _material;

                // Read + Write: Read preserves existing content for blending
                builder.SetRenderAttachment(colorTarget, 0, AccessFlags.ReadWrite);

                builder.AllowPassCulling(false);

                builder.SetRenderFunc(static (PassData data, RasterGraphContext ctx) =>
                {
                    ctx.cmd.DrawProcedural(
                        Matrix4x4.identity,
                        data.Material,
                        0,
                        MeshTopology.Triangles,
                        3,
                        1
                    );
                });
            }
        }
    }
}
