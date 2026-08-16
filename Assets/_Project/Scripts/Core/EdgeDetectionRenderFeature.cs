// EdgeDetectionRenderFeature.cs — URP ScriptableRendererFeature (Unity 6 / URP 17.x)
// Borderlands-style post-process edge detection via Sobel on depth + normals.
// Distance-based thickness falloff. Adaptive color. Pencil stroke (tapered ends).
// Shader: Assets/_Project/Shaders/EdgeDetection.shader

using System.Collections.Generic;
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
        [Tooltip("Ссылка на hidden shader (обязательна для inclusion в билд). Fallback: Shader.Find.")]
        [SerializeField] private Shader _edgeDetectionShader;
        [SerializeField] private Shader _edgeDetectionMaskShader;

        [Header("Per-object Targets")]
        public bool EnablePerObjectTargets = true;

        private Material _material;
        private Material _targetMaskMaterial;

        private static readonly int UseTargetMaskId = Shader.PropertyToID("_UseEdgeTargetMask");
        private static readonly int TargetExcludeFromGlobalId = Shader.PropertyToID("_TargetExcludeFromGlobal");
        private static readonly int TargetUseSettingsId = Shader.PropertyToID("_TargetUseSettings");
        private static readonly int TargetEdgeColorId = Shader.PropertyToID("_TargetEdgeColor");
        private static readonly int TargetEdgeWidthId = Shader.PropertyToID("_TargetEdgeWidth");
        private static readonly int TargetMaxEdgeDistanceId = Shader.PropertyToID("_TargetMaxEdgeDistance");
        private static readonly int TargetDepthFalloffId = Shader.PropertyToID("_TargetDepthFalloff");
        private static readonly int TargetUseDepthEdgesId = Shader.PropertyToID("_TargetUseDepthEdges");
        private static readonly int TargetDepthSensitivityId = Shader.PropertyToID("_TargetDepthSensitivity");
        private static readonly int TargetDepthThresholdId = Shader.PropertyToID("_TargetDepthThreshold");
        private static readonly int TargetUseNormalEdgesId = Shader.PropertyToID("_TargetUseNormalEdges");
        private static readonly int TargetNormalSensitivityId = Shader.PropertyToID("_TargetNormalSensitivity");
        private static readonly int TargetNormalThresholdId = Shader.PropertyToID("_TargetNormalThreshold");
        private static readonly int TargetUseAdaptiveColorId = Shader.PropertyToID("_TargetUseAdaptiveColor");
        private static readonly int TargetAdaptiveStrengthId = Shader.PropertyToID("_TargetAdaptiveStrength");
        private static readonly int TargetUsePencilStrokeId = Shader.PropertyToID("_TargetUsePencilStroke");
        private static readonly int TargetPencilTaperId = Shader.PropertyToID("_TargetPencilTaper");
        private static readonly int TargetPencilGrainId = Shader.PropertyToID("_TargetPencilGrain");
        private static readonly int TargetLineSoftnessId = Shader.PropertyToID("_TargetLineSoftness");

        public Material GetOrCreateMaterial()
        {
            if (_material != null && _material.shader != null) return _material;
            if (OverrideMaterial != null) { _material = OverrideMaterial; return _material; }
            Shader shader = _edgeDetectionShader != null ? _edgeDetectionShader : Shader.Find("Hidden/ProjectC/EdgeDetection");
            if (shader == null) { Debug.LogError("[EdgeDetectionFeature] Shader not found."); return null; }
            _material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            return _material;
        }

        private Material GetOrCreateTargetMaskMaterial()
        {
            if (_targetMaskMaterial != null && _targetMaskMaterial.shader != null)
                return _targetMaskMaterial;

            Shader shader = _edgeDetectionMaskShader != null ? _edgeDetectionMaskShader : Shader.Find("Hidden/ProjectC/EdgeDetectionMask");
            if (shader == null)
            {
                Debug.LogError("[EdgeDetectionFeature] Target mask shader not found.");
                return null;
            }

            _targetMaskMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            return _targetMaskMaterial;
        }

        public override void Create() { }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (renderingData.cameraData.cameraType == CameraType.Preview) return;
            Material mat = GetOrCreateMaterial();
            if (mat == null) return;

            EdgeDetectionTarget target = EnablePerObjectTargets
                ? EdgeDetectionTarget.GetActiveTarget()
                : null;
            bool needsTargetMask = target != null &&
                                   (target.ExcludeFromGlobal || target.UseTargetSettings);
            Material targetMaskMaterial = needsTargetMask
                ? GetOrCreateTargetMaskMaterial()
                : null;

            ApplyProperties(mat, target, targetMaskMaterial != null);

            var pass = new EdgeDetectionPass(
                mat,
                targetMaskMaterial,
                target,
                UseAdaptiveColor);
            pass.renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
            renderer.EnqueuePass(pass);
        }

        private void ApplyProperties(Material mat, EdgeDetectionTarget target, bool targetMaskEnabled)
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

            mat.SetFloat(UseTargetMaskId, targetMaskEnabled ? 1f : 0f);
            mat.SetFloat(TargetExcludeFromGlobalId, target != null && target.ExcludeFromGlobal ? 1f : 0f);
            mat.SetFloat(TargetUseSettingsId, target != null && target.UseTargetSettings ? 1f : 0f);
            mat.SetColor(TargetEdgeColorId, target != null ? target.TargetEdgeColor : EdgeColor);
            mat.SetFloat(TargetEdgeWidthId, target != null ? target.TargetEdgeWidth : EdgeWidth);
            mat.SetFloat(TargetMaxEdgeDistanceId, target != null ? target.TargetMaxEdgeDistance : MaxEdgeDistance);
            mat.SetFloat(TargetDepthFalloffId, target != null ? target.TargetDepthFalloff : DepthFalloff);
            mat.SetFloat(TargetUseDepthEdgesId, target != null && target.TargetUseDepthEdges ? 1f : 0f);
            mat.SetFloat(TargetDepthSensitivityId, target != null ? target.TargetDepthSensitivity : DepthSensitivity);
            mat.SetFloat(TargetDepthThresholdId, target != null ? target.TargetDepthThreshold : DepthThreshold);
            mat.SetFloat(TargetUseNormalEdgesId, target != null && target.TargetUseNormalEdges ? 1f : 0f);
            mat.SetFloat(TargetNormalSensitivityId, target != null ? target.TargetNormalSensitivity : NormalSensitivity);
            mat.SetFloat(TargetNormalThresholdId, target != null ? target.TargetNormalThreshold : NormalThreshold);
            mat.SetFloat(TargetUseAdaptiveColorId, target != null && target.TargetUseAdaptiveColor ? 1f : 0f);
            mat.SetFloat(TargetAdaptiveStrengthId, target != null ? target.TargetAdaptiveStrength : AdaptiveStrength);
            mat.SetFloat(TargetUsePencilStrokeId, target != null && target.TargetUsePencilStroke ? 1f : 0f);
            mat.SetFloat(TargetPencilTaperId, target != null ? target.TargetPencilTaper : PencilTaper);
            mat.SetFloat(TargetPencilGrainId, target != null ? target.TargetPencilGrain : PencilGrain);
            mat.SetFloat(TargetLineSoftnessId, target != null ? target.TargetLineSoftness : LineSoftness);
        }

        protected override void Dispose(bool disposing)
        {
            if (!disposing) return;

#if UNITY_EDITOR
            if (_material != null) Object.DestroyImmediate(_material);
            if (_targetMaskMaterial != null) Object.DestroyImmediate(_targetMaskMaterial);
#else
            if (_material != null) Object.Destroy(_material);
            if (_targetMaskMaterial != null) Object.Destroy(_targetMaskMaterial);
#endif

            _material = null;
            _targetMaskMaterial = null;
        }
    }

    internal sealed class EdgeDetectionPass : ScriptableRenderPass
    {
        private const string PassName = "EdgeDetection";
        private const string MaskPassName = "EdgeDetectionTargetMask";

        private readonly Material _material;
        private readonly Material _targetMaskMaterial;
        private readonly EdgeDetectionTarget _target;
        private readonly bool _needsSourceTex;

        private static readonly int SourceTexId = Shader.PropertyToID("_EdgeSourceTex");
        private static readonly int TargetMaskId = Shader.PropertyToID("_EdgeTargetMask");

        private sealed class MaskPassData
        {
            public Material Material;
            public Renderer[] Renderers;
        }

        private sealed class PassData
        {
            public Material Material;
            public TextureHandle SourceTex;
            public TextureHandle TargetMask;
        }

        public EdgeDetectionPass(
            Material material,
            Material targetMaskMaterial,
            EdgeDetectionTarget target,
            bool needsSourceTex)
        {
            _material = material;
            _targetMaskMaterial = targetMaskMaterial;
            _target = target;
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

            TextureHandle targetMask = default;
            if (_target != null && _targetMaskMaterial != null)
            {
                var renderers = new List<Renderer>();
                EdgeDetectionTarget.CollectActiveRenderers(renderers);
                if (renderers.Count > 0)
                {
                    var maskDesc = colorTarget.GetDescriptor(renderGraph);
                    maskDesc.depthBufferBits = 0;
                    maskDesc.msaaSamples = (MSAASamples)1;
                    maskDesc.colorFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R8_UNorm;
                    maskDesc.name = "EdgeTargetMask";
                    maskDesc.clearBuffer = true;
                    maskDesc.clearColor = Color.clear;
                    targetMask = renderGraph.CreateTexture(maskDesc);

                    using (var maskBuilder = renderGraph.AddRasterRenderPass<MaskPassData>(
                               MaskPassName,
                               out var maskPassData,
                               new ProfilingSampler(MaskPassName)))
                    {
                        maskPassData.Material = _targetMaskMaterial;
                        maskPassData.Renderers = renderers.ToArray();
                        maskBuilder.SetRenderAttachment(targetMask, 0, AccessFlags.Write);
                        maskBuilder.AllowPassCulling(false);

                        maskBuilder.SetRenderFunc(static (MaskPassData data, RasterGraphContext ctx) =>
                        {
                            ctx.cmd.ClearRenderTarget(false, true, Color.clear);
                            if (data.Renderers == null) return;

                            for (int i = 0; i < data.Renderers.Length; i++)
                            {
                                Renderer renderer = data.Renderers[i];
                                if (renderer == null || !renderer.enabled)
                                    continue;

                                // RasterCommandBuffer uses an explicit submesh index; -1 is invalid here
                                // and produces a per-frame "submeshIndex out of range" error. Draw every
                                // material slot so multi-submesh target meshes are fully covered.
                                Material[] sharedMaterials = renderer.sharedMaterials;
                                int submeshCount = sharedMaterials != null ? sharedMaterials.Length : 0;
                                for (int submeshIndex = 0; submeshIndex < submeshCount; submeshIndex++)
                                    ctx.cmd.DrawRenderer(renderer, data.Material, submeshIndex, 0);
                            }
                        });
                    }
                }
            }

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
                passData.TargetMask = targetMask;
                builder.SetRenderAttachment(colorTarget, 0, AccessFlags.ReadWrite);
                builder.AllowPassCulling(false);
                builder.AllowGlobalStateModification(true);

                if (sourceTex.IsValid())
                    builder.UseTexture(sourceTex, AccessFlags.Read);
                if (targetMask.IsValid())
                    builder.UseTexture(targetMask, AccessFlags.Read);

                builder.SetRenderFunc(static (PassData data, RasterGraphContext ctx) =>
                {
                    if (data.SourceTex.IsValid())
                        ctx.cmd.SetGlobalTexture(SourceTexId, data.SourceTex);
                    if (data.TargetMask.IsValid())
                        ctx.cmd.SetGlobalTexture(TargetMaskId, data.TargetMask);

                    ctx.cmd.DrawProcedural(Matrix4x4.identity, data.Material, 0,
                        MeshTopology.Triangles, 3, 1);
                });
            }
        }
    }
}
