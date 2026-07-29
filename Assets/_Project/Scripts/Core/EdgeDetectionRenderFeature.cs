// EdgeDetectionRenderFeature.cs — URP ScriptableRendererFeature (Unity 6 / URP 17.x)
// Borderlands-style post-process edge detection via Sobel on depth + normals.
// Distance-based thickness falloff. Adaptive color. Pencil stroke (tapered ends).
// Shader: Assets/_Project/Shaders/EdgeDetection.shader

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace ProjectC.Rendering
{
    [DisallowMultipleRendererFeature("Edge Detection")]
    [SupportedOnRenderer(typeof(UniversalRendererData))]
    public sealed class EdgeDetectionRenderFeature : ScriptableRendererFeature
    {
        [Header("Edge")]
        [ColorUsage(false, false)] public Color EdgeColor = new Color(0.02f, 0.02f, 0.04f, 1f);
        [Range(0.1f, 8f)] public float EdgeWidth = 1.5f;

        [Header("Distance Falloff")]
        [Range(1f, 500f)] public float MaxEdgeDistance = 80f;
        [Range(0f, 2f)] public float DepthFalloff = 0.8f;

        [Header("Depth Edges")]
        public bool UseDepthEdges = true;
        [Range(0.1f, 8f)] public float DepthSensitivity = 2f;
        [Range(0f, 0.5f)] public float DepthThreshold = 0.04f;

        [Header("Normal Edges")]
        public bool UseNormalEdges = true;
        [Range(0.1f, 4f)] public float NormalSensitivity = 0.8f;
        [Range(0f, 0.8f)] public float NormalThreshold = 0.25f;

        [Header("Adaptive Color")]
        public bool UseAdaptiveColor = false;
        [Range(0f, 1f)] public float AdaptiveStrength = 0.6f;

        [Header("Pencil Stroke")]
        public bool UsePencilStroke = false;
        [Range(0f, 1f)] public float PencilTaper = 0.7f;
        [Range(0f, 0.3f)] public float PencilGrain = 0.08f;

        [Header("Softness")]
        [Range(0.005f, 0.2f)] public float LineSoftness = 0.03f;

        [Header("Material")]
        public Material OverrideMaterial;

        private Material _material;

        public Material GetOrCreateMaterial()
        {
            if (_material != null && _material.shader != null) return _material;
            if (OverrideMaterial != null) { _material = OverrideMaterial; return _material; }
            Shader shader = Shader.Find("Hidden/ProjectC/EdgeDetection");
            if (shader == null) { Debug.LogError("[EdgeDetectionFeature] Shader not found."); return null; }
            _material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            return _material;
        }

        public override void Create() { }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (renderingData.cameraData.cameraType == CameraType.Preview) return;
            Material mat = GetOrCreateMaterial();
            if (mat == null) return;
            ApplyProperties(mat);

            var pass = new EdgeDetectionPass(mat, UseAdaptiveColor);
            pass.renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
            renderer.EnqueuePass(pass);
        }

        private void ApplyProperties(Material mat)
        {
            mat.SetColor("_EdgeColor", EdgeColor);
            mat.SetFloat("_EdgeWidth", EdgeWidth);
            mat.SetFloat("_MaxEdgeDistance", MaxEdgeDistance);
            mat.SetFloat("_DepthFalloff", DepthFalloff);
            mat.SetFloat("_UseDepthEdges", UseDepthEdges ? 1f : 0f);
            mat.SetFloat("_DepthSensitivity", DepthSensitivity);
            mat.SetFloat("_DepthThreshold", DepthThreshold);
            mat.SetFloat("_UseNormalEdges", UseNormalEdges ? 1f : 0f);
            mat.SetFloat("_NormalSensitivity", NormalSensitivity);
            mat.SetFloat("_NormalThreshold", NormalThreshold);
            mat.SetFloat("_UseAdaptiveColor", UseAdaptiveColor ? 1f : 0f);
            mat.SetFloat("_AdaptiveStrength", AdaptiveStrength);
            mat.SetFloat("_UsePencilStroke", UsePencilStroke ? 1f : 0f);
            mat.SetFloat("_PencilTaper", PencilTaper);
            mat.SetFloat("_PencilGrain", PencilGrain);
            mat.SetFloat("_LineSoftness", LineSoftness);
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

    internal sealed class EdgeDetectionPass : ScriptableRenderPass
    {
        private const string PassName = "EdgeDetection";
        private Material _material;
        private readonly bool _needsSourceTex;

        private static readonly int SourceTexId = Shader.PropertyToID("_EdgeSourceTex");

        private class PassData
        {
            public Material Material;
            public TextureHandle SourceTex;
        }

        public EdgeDetectionPass(Material material, bool needsSourceTex)
        {
            _material = material;
            _needsSourceTex = needsSourceTex;
            profilingSampler = new ProfilingSampler(PassName);

            var inputs = ScriptableRenderPassInput.Normal | ScriptableRenderPassInput.Depth;
            ConfigureInput(inputs);
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (_material == null) return;
            var resourceData = frameData.Get<UniversalResourceData>();
            var colorTarget = resourceData.activeColorTexture;

            TextureHandle sourceTex = default;
            if (_needsSourceTex)
            {
                var desc = colorTarget.GetDescriptor(renderGraph);
                desc.depthBufferBits = 0;
                desc.msaaSamples = (MSAASamples)1;
                desc.name = "EdgeSourceCopy";
                sourceTex = renderGraph.CreateTexture(desc);
                RenderGraphUtils.AddCopyPass(renderGraph, colorTarget, sourceTex,
                    "CopyColorForEdge", false);
            }

            using (var builder = renderGraph.AddRasterRenderPass<PassData>(
                       PassName, out var passData, profilingSampler))
            {
                passData.Material = _material;
                passData.SourceTex = sourceTex;
                builder.SetRenderAttachment(colorTarget, 0, AccessFlags.ReadWrite);
                builder.AllowPassCulling(false);
                builder.AllowGlobalStateModification(true);

                if (sourceTex.IsValid())
                    builder.UseTexture(sourceTex, AccessFlags.Read);

                builder.SetRenderFunc(static (PassData data, RasterGraphContext ctx) =>
                {
                    if (data.SourceTex.IsValid())
                        ctx.cmd.SetGlobalTexture(SourceTexId, data.SourceTex);
                    ctx.cmd.DrawProcedural(Matrix4x4.identity, data.Material, 0,
                        MeshTopology.Triangles, 3, 1);
                });
            }
        }
    }
}
