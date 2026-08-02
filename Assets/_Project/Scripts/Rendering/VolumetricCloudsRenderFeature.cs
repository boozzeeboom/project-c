// VolumetricCloudsRenderFeature.cs — Phase 1.3
// URP ScriptableRendererFeature for volumetric cloud raymarch.
// Pattern: EdgeDetectionRenderFeature.cs (RenderGraph API, AfterOpaques).
// Shader: Assets/_Project/Shaders/Clouds/VolumetricClouds.shader

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using ProjectC.Core;

namespace ProjectC.Rendering
{
    [DisallowMultipleRendererFeature("Volumetric Clouds")]
    [SupportedOnRenderer(typeof(UniversalRendererData))]
    public sealed class VolumetricCloudsRenderFeature : ScriptableRendererFeature
    {
        [Header("Cloud Layer")]
        [Range(100f, 5000f)] public float CloudBottomY = 800f;
        [Range(200f, 10000f)] public float CloudTopY = 2000f;
        public Texture3D CloudNoise3D;

        [Header("Raymarch")]
        [Range(8, 128)] public int RaymarchSteps = 48;
        [Range(500f, 20000f)] public float MaxRayDistance = 5000f;

        [Header("Density")]
        [Range(0.1f, 5f)] public float DensityMultiplier = 1f;
        [Range(0.001f, 0.5f)] public float LightAbsorption = 0.05f;
        [Range(0.01f, 0.5f)] public float HeightEdgeSoftness = 0.15f;

        [Header("Quality (Phase 1.6)")]
        public bool HalfResRender = true;
        public bool TemporalReprojection = true;
        public bool BlueNoiseDither = true;
        public Texture2D BlueNoiseTexture;

        [Header("Ghibli Ramps (Phase 1.5)")]
        [ColorUsage(false, false)] public Color DayRampTop = new Color(1f, 1f, 1f, 1f);
        [ColorUsage(false, false)] public Color DayRampMid = new Color(0.831f, 0.902f, 0.945f, 1f);
        [ColorUsage(false, false)] public Color DayRampBot = new Color(0.663f, 0.8f, 0.89f, 1f);
        [ColorUsage(false, false)] public Color SunsetRampTop = new Color(1f, 0.714f, 0.757f, 1f);
        [ColorUsage(false, false)] public Color SunsetRampMid = new Color(1f, 0.557f, 0.655f, 1f);
        [ColorUsage(false, false)] public Color SunsetRampBot = new Color(0.804f, 0.361f, 0.361f, 1f);

        [Header("Material")]
        public Material OverrideMaterial;

        private Material _material;
        private RenderTexture _cloudHistoryRT;

        private static readonly int CloudNoise3DId = Shader.PropertyToID("_CloudNoise3D");
        private static readonly int CloudBottomYId = Shader.PropertyToID("_CloudBottomY");
        private static readonly int CloudTopYId = Shader.PropertyToID("_CloudTopY");
        private static readonly int RaymarchStepsId = Shader.PropertyToID("_RaymarchSteps");
        private static readonly int MaxRayDistanceId = Shader.PropertyToID("_MaxRayDistance");
        private static readonly int DensityMultiplierId = Shader.PropertyToID("_DensityMultiplier");
        private static readonly int LightAbsorptionId = Shader.PropertyToID("_LightAbsorption");
        private static readonly int HeightEdgeSoftnessId = Shader.PropertyToID("_HeightEdgeSoftness");
        private static readonly int WindOffsetId = Shader.PropertyToID("_WindOffset");
        private static readonly int SunDirectionId = Shader.PropertyToID("_SunDirection");
        private static readonly int DayRampTopId = Shader.PropertyToID("_DayRampTop");
        private static readonly int DayRampMidId = Shader.PropertyToID("_DayRampMid");
        private static readonly int DayRampBotId = Shader.PropertyToID("_DayRampBot");
        private static readonly int SunsetRampTopId = Shader.PropertyToID("_SunsetRampTop");
        private static readonly int SunsetRampMidId = Shader.PropertyToID("_SunsetRampMid");
        private static readonly int SunsetRampBotId = Shader.PropertyToID("_SunsetRampBot");
        private static readonly int BlueNoiseTexId = Shader.PropertyToID("_BlueNoiseTex");
        internal static readonly int CloudHistoryRTId = Shader.PropertyToID("_CloudHistoryRT");
        internal static readonly int PrevViewProjId = Shader.PropertyToID("_PrevViewProj");

        private Matrix4x4 _prevViewProj = Matrix4x4.identity;
        private bool _prevViewProjValid;

        public Material GetOrCreateMaterial()
        {
            if (_material != null && _material.shader != null) return _material;
            if (OverrideMaterial != null) { _material = OverrideMaterial; return _material; }
            Shader shader = Shader.Find("Hidden/ProjectC/VolumetricClouds");
            if (shader == null)
            {
                Debug.LogError("[VolumetricClouds] Shader 'Hidden/ProjectC/VolumetricClouds' not found.");
                return null;
            }
            _material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            return _material;
        }

        public override void Create()
        {
            _prevViewProjValid = false;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (renderingData.cameraData.cameraType == CameraType.Preview) return;
            Material mat = GetOrCreateMaterial();
            if (mat == null) return;

            ApplyProperties(mat, renderingData.cameraData.camera);
            var pass = new VolumetricCloudsPass(mat);
            pass.renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
            renderer.EnqueuePass(pass);
        }

        private void ApplyProperties(Material mat, Camera camera)
        {
            mat.SetFloat(CloudBottomYId, CloudBottomY);
            mat.SetFloat(CloudTopYId, CloudTopY);
            mat.SetInt(RaymarchStepsId, RaymarchSteps);
            mat.SetFloat(MaxRayDistanceId, MaxRayDistance);
            mat.SetFloat(DensityMultiplierId, DensityMultiplier);
            mat.SetFloat(LightAbsorptionId, LightAbsorption);
            mat.SetFloat(HeightEdgeSoftnessId, HeightEdgeSoftness);

            if (CloudNoise3D != null)
                mat.SetTexture(CloudNoise3DId, CloudNoise3D);

            // Wind from WindManager
            Vector3 windDir = Vector3.right;
            if (WindManager.Instance != null)
            {
                windDir = WindManager.Instance.CurrentWindDirection.normalized;
            }
            // Accumulate wind offset over time
            Vector4 windOffset = mat.GetVector(WindOffsetId);
            windOffset += (Vector4)(windDir * Time.deltaTime * 0.1f);
            mat.SetVector(WindOffsetId, windOffset);

            // Sun direction from RenderSettings or DayNight system
            Vector3 sunDir = RenderSettings.sun != null
                ? RenderSettings.sun.transform.forward
                : new Vector3(0.3f, 0.7f, -0.6f).normalized;
            mat.SetVector(SunDirectionId, sunDir);

            // Ghibli ramps
            mat.SetColor(DayRampTopId, DayRampTop);
            mat.SetColor(DayRampMidId, DayRampMid);
            mat.SetColor(DayRampBotId, DayRampBot);
            mat.SetColor(SunsetRampTopId, SunsetRampTop);
            mat.SetColor(SunsetRampMidId, SunsetRampMid);
            mat.SetColor(SunsetRampBotId, SunsetRampBot);

            if (BlueNoiseTexture != null)
                mat.SetTexture(BlueNoiseTexId, BlueNoiseTexture);
            mat.SetKeyword(new LocalKeyword(mat.shader, "_BLUE_NOISE_ON"), BlueNoiseDither && BlueNoiseTexture != null);

            // Temporal reprojection
            mat.SetKeyword(new LocalKeyword(mat.shader, "_TEMPORAL_ON"), TemporalReprojection);
        }

        /// <summary>
        /// Called AFTER render to cache view-proj for next frame's temporal reprojection.
        /// </summary>
        internal void CacheViewProj(Camera camera)
        {
            Matrix4x4 proj = GL.GetGPUProjectionMatrix(camera.projectionMatrix, false);
            Matrix4x4 view = camera.worldToCameraMatrix;
            _prevViewProj = proj * view;
            _prevViewProjValid = true;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_material != null)
                {
#if UNITY_EDITOR
                    Object.DestroyImmediate(_material);
#else
                    Object.Destroy(_material);
#endif
                    _material = null;
                }
                if (_cloudHistoryRT != null)
                {
                    _cloudHistoryRT.Release();
                    Object.DestroyImmediate(_cloudHistoryRT);
                    _cloudHistoryRT = null;
                }
            }
        }
    }

    internal sealed class VolumetricCloudsPass : ScriptableRenderPass
    {
        private const string PassName = "VolumetricClouds";

        private Material _material;

        private class PassData
        {
            public Material Material;
            public TextureHandle ColorTarget;
        }

        public VolumetricCloudsPass(Material material)
        {
            _material = material;
            profilingSampler = new ProfilingSampler(PassName);
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (_material == null) return;

            var resourceData = frameData.Get<UniversalResourceData>();
            var colorTarget = resourceData.activeColorTexture;

            using (var builder = renderGraph.AddRasterRenderPass<PassData>(
                PassName, out var passData, profilingSampler))
            {
                passData.Material = _material;
                passData.ColorTarget = colorTarget;

                builder.SetRenderAttachment(colorTarget, 0, AccessFlags.ReadWrite);
                builder.AllowPassCulling(false);
                builder.AllowGlobalStateModification(true);

                builder.SetRenderFunc(static (PassData data, RasterGraphContext ctx) =>
                {
                    ctx.cmd.DrawProcedural(Matrix4x4.identity, data.Material, 0,
                        MeshTopology.Triangles, 3, 1);
                });
            }
        }
    }
}
