// VolumetricCloudsRenderFeature.cs — CLOUD_system 3.0
// URP ScriptableRendererFeature (RenderGraph API).
// Phase 1.3: B&W density fullscreen pass
// Phase 1.4: height profile + wind (in shader density)
// Phase 1.5: colored light-march pass with Ghibli ramps
// Phase 1.6: half-res render + blue-noise dither + temporal reprojection

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
        [Range(0.1f, 10f)] public float DensityMultiplier = 3f;
        [Range(0.001f, 0.5f)] public float LightAbsorption = 0.05f;
        [Range(0.01f, 0.5f)] public float HeightEdgeSoftness = 0.15f;

        [Header("Phase 1.6: Quality")]
        public bool HalfResRender = true;
        public bool TemporalReprojection = true;
        public bool BlueNoiseDither = true;
        public Texture2D BlueNoiseTexture;

        [Header("Phase 1.5: Ghibli Ramps")]
        [ColorUsage(false, false)] public Color DayRampTop = Color.white;
        [ColorUsage(false, false)] public Color DayRampMid = new Color(0.831f, 0.902f, 0.945f);
        [ColorUsage(false, false)] public Color DayRampBot = new Color(0.663f, 0.8f, 0.89f);
        [ColorUsage(false, false)] public Color SunsetRampTop = Color.white;
        [ColorUsage(false, false)] public Color SunsetRampMid = new Color(1f, 0.714f, 0.757f);
        [ColorUsage(false, false)] public Color SunsetRampBot = new Color(0.804f, 0.361f, 0.361f);

        [Header("Material")]
        public Material OverrideMaterial;

        private Material _material;
        private RenderTexture _cloudHistoryRT;

        // Shader property IDs
        private static readonly int CloudNoise3DId     = Shader.PropertyToID("_CloudNoise3D");
        private static readonly int CloudBottomYId     = Shader.PropertyToID("_CloudBottomY");
        private static readonly int CloudTopYId        = Shader.PropertyToID("_CloudTopY");
        private static readonly int RaymarchStepsId    = Shader.PropertyToID("_RaymarchSteps");
        private static readonly int MaxRayDistanceId   = Shader.PropertyToID("_MaxRayDistance");
        private static readonly int DensityMultId      = Shader.PropertyToID("_DensityMultiplier");
        private static readonly int LightAbsorptionId  = Shader.PropertyToID("_LightAbsorption");
        private static readonly int HeightEdgeId       = Shader.PropertyToID("_HeightEdgeSoftness");
        private static readonly int WindOffsetId       = Shader.PropertyToID("_WindOffset");
        private static readonly int SunDirectionId     = Shader.PropertyToID("_SunDirection");
        private static readonly int DayRampTopId       = Shader.PropertyToID("_DayRampTop");
        private static readonly int DayRampMidId       = Shader.PropertyToID("_DayRampMid");
        private static readonly int DayRampBotId       = Shader.PropertyToID("_DayRampBot");
        private static readonly int SunsetRampTopId    = Shader.PropertyToID("_SunsetRampTop");
        private static readonly int SunsetRampMidId    = Shader.PropertyToID("_SunsetRampMid");
        private static readonly int SunsetRampBotId    = Shader.PropertyToID("_SunsetRampBot");
        internal static readonly int BlueNoiseTexId     = Shader.PropertyToID("_BlueNoiseTex");
        internal static readonly int CloudHistoryRTId   = Shader.PropertyToID("_CloudHistoryRT");
        internal static readonly int PrevViewProjId     = Shader.PropertyToID("_PrevViewProj");
        private static readonly int CloudRTId          = Shader.PropertyToID("_CloudRT");

        // Phase 1.6: temporal state
        private Matrix4x4 _prevViewProj = Matrix4x4.identity;
        private bool _prevViewProjValid;

        public Material GetOrCreateMaterial()
        {
            if (_material != null && _material.shader != null) return _material;
            if (OverrideMaterial != null) { _material = OverrideMaterial; return _material; }
            Shader shader = Shader.Find("Hidden/ProjectC/VolumetricClouds");
            if (shader == null)
            {
                Debug.LogError("[VolumetricClouds] Shader not found.");
                return null;
            }
            _material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            return _material;
        }

        public override void Create() { _prevViewProjValid = false; }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (renderingData.cameraData.cameraType == CameraType.Preview) return;
            Material mat = GetOrCreateMaterial();
            if (mat == null) return;

            ApplyProperties(mat, renderingData.cameraData.camera);
            var pass = new VolumetricCloudsPass(mat, HalfResRender, TemporalReprojection,
                _prevViewProj, _prevViewProjValid, ref _cloudHistoryRT);
            pass.renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
            renderer.EnqueuePass(pass);
        }

        private void ApplyProperties(Material mat, Camera camera)
        {
            mat.SetFloat(CloudBottomYId, CloudBottomY);
            mat.SetFloat(CloudTopYId, CloudTopY);
            mat.SetInt(RaymarchStepsId, RaymarchSteps);
            mat.SetFloat(MaxRayDistanceId, MaxRayDistance);
            mat.SetFloat(DensityMultId, DensityMultiplier);
            mat.SetFloat(LightAbsorptionId, LightAbsorption);
            mat.SetFloat(HeightEdgeId, HeightEdgeSoftness);

            if (CloudNoise3D != null)
                mat.SetTexture(CloudNoise3DId, CloudNoise3D);

            // Wind (null-guard)
            Vector3 windDir = Vector3.right;
            float windSpeed = 1f;
            if (WindManager.Instance != null)
            {
                windDir = WindManager.Instance.CurrentWindDirection.normalized;
                windSpeed = Mathf.Max(WindManager.Instance.CurrentWindSpeed, 0.1f);
            }
            Vector4 windOffset = mat.GetVector(WindOffsetId);
            windOffset += (Vector4)(windDir * windSpeed * Time.deltaTime * 0.05f);
            mat.SetVector(WindOffsetId, windOffset);

            // Sun direction
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

            // Blue noise (Phase 1.6 — shader declares _BLUE_NOISE_ON via multi_compile_local)
            if (BlueNoiseTexture != null)
                mat.SetTexture(BlueNoiseTexId, BlueNoiseTexture);
            if (BlueNoiseDither && BlueNoiseTexture != null && mat.shader != null)
            {
                var kw = new LocalKeyword(mat.shader, "_BLUE_NOISE_ON");
                mat.SetKeyword(kw, true);
            }

            // Temporal: cache prevViewProj for NEXT frame
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
                    Object.DestroyImmediate(_material);
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
        private readonly bool _halfRes;
        private readonly bool _temporal;
        private readonly Matrix4x4 _prevVp;
        private readonly bool _prevVpValid;
        private RenderTexture _historyRT;

        private class PassData
        {
            public Material Material;
            public TextureHandle ColorTarget;
            public TextureHandle CloudRT;
        }

        public VolumetricCloudsPass(Material material, bool halfRes, bool temporal,
            Matrix4x4 prevVp, bool prevVpValid, ref RenderTexture historyRT)
        {
            _material = material;
            _halfRes = halfRes;
            _temporal = temporal;
            _prevVp = prevVp;
            _prevVpValid = prevVpValid;
            _historyRT = historyRT;
            profilingSampler = new ProfilingSampler(PassName);
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (_material == null) return;

            var resourceData = frameData.Get<UniversalResourceData>();
            var colorTarget = resourceData.activeColorTexture;
            var cameraData = frameData.Get<UniversalCameraData>();
            var cam = cameraData.camera;

            // Determine resolution
            int w = _halfRes ? Mathf.Max(1, cam.pixelWidth / 2) : cam.pixelWidth;
            int h = _halfRes ? Mathf.Max(1, cam.pixelHeight / 2) : cam.pixelHeight;

            // Create half-res cloud RT
            var desc = colorTarget.GetDescriptor(renderGraph);
            desc.width = w;
            desc.height = h;
            desc.depthBufferBits = 0;
            desc.msaaSamples = MSAASamples.None;
            desc.name = "_CloudRT";
            TextureHandle cloudRT = renderGraph.CreateTexture(desc);

            // --- Pass A: Raymarch → _CloudRT (half-res) ---
            using (var builder = renderGraph.AddRasterRenderPass<PassData>(
                PassName, out var passData, profilingSampler))
            {
                passData.Material = _material;
                passData.CloudRT = cloudRT;
                passData.ColorTarget = colorTarget;

                builder.SetRenderAttachment(cloudRT, 0, AccessFlags.Write);
                builder.AllowPassCulling(false);
                builder.AllowGlobalStateModification(true);

                builder.SetRenderFunc(static (PassData data, RasterGraphContext ctx) =>
                {
                    // Pass 1: colored light-march (Phase 1.5)
                    ctx.cmd.DrawProcedural(Matrix4x4.identity, data.Material, 1,
                        MeshTopology.Triangles, 3, 1);
                });
            }

            // --- Pass B: Upsample + composite → camera color target ---
            using (var builder = renderGraph.AddRasterRenderPass<PassData>(
                "VolumetricClouds_Composite", out var compData, profilingSampler))
            {
                compData.Material = _material;
                compData.CloudRT = cloudRT;
                compData.ColorTarget = colorTarget;

                builder.SetRenderAttachment(colorTarget, 0, AccessFlags.ReadWrite);
                builder.UseTexture(cloudRT, AccessFlags.Read);
                builder.AllowPassCulling(false);
                builder.AllowGlobalStateModification(true);

                builder.SetRenderFunc((PassData data, RasterGraphContext ctx) =>
                {
                    ctx.cmd.SetGlobalTexture(VolumetricCloudsRenderFeature.CloudHistoryRTId, data.CloudRT);
                    // Pass 2: composite (Phase 1.6)
                    ctx.cmd.DrawProcedural(Matrix4x4.identity, data.Material, 2,
                        MeshTopology.Triangles, 3, 1);
                });
            }
        }
    }
}
