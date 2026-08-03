// VolumetricCloudsRenderFeature.cs — CLOUD_system 3.0 TUNING
// URP ScriptableRendererFeature (RenderGraph API).
// All tuning parameters exposed in inspector.
// Shader global-only uniforms set via Shader.SetGlobal* (NOT mat.Set*)
// to avoid material-slot shadowing issues.

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;
using ProjectC.Core;
using ProjectC.World.Clouds;

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
        [Range(0.1f, 10f)] public float DensityMultiplier = 1f;
        [Range(0.001f, 0.5f)] public float LightAbsorption = 0.05f;
        [Range(0.1f, 5f)] public float Opacity = 1f;
        [Range(0.1f, 5f)] public float ColorIntensity = 1f;

        [Header("Shape")]
        [Range(0.01f, 0.5f)] public float HeightEdgeSoftness = 0.15f;
        [Tooltip("World units per 3D-noise tile (128 texels).")]
        [Range(256f, 4096f)] public float NoiseTileSize = 1024f;
        [Tooltip("World scale of the 2D coverage FBM.")]
        [Range(0.0001f, 0.01f)] public float CoverageScale = 0.0008f;
        [Tooltip("Coverage cutoff: below = clear sky hole.")]
        [Range(0.1f, 0.9f)] public float CoverageThreshold = 0.5f;

        [Header("Quality")]
        public bool HalfResRender = true;
        public bool TemporalReprojection = true;
        public bool BlueNoiseDither = true;
        public Texture2D BlueNoiseTexture;

        [Header("Wind")]
        [Range(0f, 5f)] public float WindSpeedMultiplier = 1f;

        [Header("Debug")]
        public bool DebugDensityDirect = false;

        [Header("Ghibli Ramps")]
        [ColorUsage(false, false)] public Color DayRampTop = Color.white;
        [ColorUsage(false, false)] public Color DayRampMid = new Color(0.831f, 0.902f, 0.945f);
        [ColorUsage(false, false)] public Color DayRampBot = new Color(0.663f, 0.8f, 0.89f);
        [ColorUsage(false, false)] public Color SunsetRampTop = Color.white;
        [ColorUsage(false, false)] public Color SunsetRampMid = new Color(1f, 0.714f, 0.757f);
        [ColorUsage(false, false)] public Color SunsetRampBot = new Color(0.804f, 0.361f, 0.361f);

        [Header("Phase 2.2: Local Density")]
        [Tooltip("Используется singleton LocalDensityBuffer.Instance (не требует ручного назначения).")]
        [Range(0f, 2f)] public float LocalDensityInfluence = 1f;

        [Header("Phase 2.2B: Displacement (Variant B)")]
        [Tooltip("Множитель силы displacement. Применимо только когда LocalDensityBuffer.Mode = Displacement.")]
        [Range(0f, 1000f)] public float DisplacementStrength = 300f;

        [Header("Material")]
        public Material OverrideMaterial;

        private Material _material;
        private RTHandle _cloudHistoryA, _cloudHistoryB;
        private int _historyIdx;

        // Shader property IDs
        private static readonly int CloudNoise3DId      = Shader.PropertyToID("_CloudNoise3D");
        private static readonly int CloudBottomYId      = Shader.PropertyToID("_CloudBottomY");
        private static readonly int CloudTopYId         = Shader.PropertyToID("_CloudTopY");
        private static readonly int RaymarchStepsId     = Shader.PropertyToID("_RaymarchSteps");
        private static readonly int MaxRayDistanceId    = Shader.PropertyToID("_MaxRayDistance");
        private static readonly int DensityMultId       = Shader.PropertyToID("_DensityMultiplier");
        private static readonly int LightAbsorptionId   = Shader.PropertyToID("_LightAbsorption");
        private static readonly int HeightEdgeId        = Shader.PropertyToID("_HeightEdgeSoftness");
        private static readonly int NoiseTileSizeId     = Shader.PropertyToID("_NoiseTileSize");
        private static readonly int CoverageScaleId     = Shader.PropertyToID("_CoverageScale");
        private static readonly int CoverageThresholdId = Shader.PropertyToID("_CoverageThreshold");
        private static readonly int TemporalBlendId     = Shader.PropertyToID("_TemporalBlend");
        private static readonly int WindOffsetId        = Shader.PropertyToID("_WindOffset");
        private static readonly int SunDirectionId      = Shader.PropertyToID("_SunDirection");
        private static readonly int DayRampTopId        = Shader.PropertyToID("_DayRampTop");
        private static readonly int DayRampMidId        = Shader.PropertyToID("_DayRampMid");
        private static readonly int DayRampBotId        = Shader.PropertyToID("_DayRampBot");
        private static readonly int SunsetRampTopId     = Shader.PropertyToID("_SunsetRampTop");
        private static readonly int SunsetRampMidId     = Shader.PropertyToID("_SunsetRampMid");
        private static readonly int SunsetRampBotId     = Shader.PropertyToID("_SunsetRampBot");
        internal static readonly int BlueNoiseTexId      = Shader.PropertyToID("_BlueNoiseTex");
        internal static readonly int CloudHistoryRTId    = Shader.PropertyToID("_CloudHistoryRT");
        internal static readonly int PrevViewProjId      = Shader.PropertyToID("_PrevViewProj");
        internal static readonly int CloudRTId           = Shader.PropertyToID("_CloudRT");
        internal static readonly int CloudTargetSizeId   = Shader.PropertyToID("_CloudTargetSize");
        internal static readonly int ViewToWorldId       = Shader.PropertyToID("_Cloud_ViewToWorld");
        internal static readonly int InvProjectionId     = Shader.PropertyToID("_Cloud_InvProj");
        internal static readonly int CloudOpacityId      = Shader.PropertyToID("_CloudOpacity");
        internal static readonly int CloudColorIntensityId = Shader.PropertyToID("_CloudColorIntensity");
        internal static readonly int LocalDensityRTId     = Shader.PropertyToID("_LocalDensityRT");
        internal static readonly int LocalDensityCenterId = Shader.PropertyToID("_LocalDensityCenter");
        internal static readonly int LocalDensitySizeId   = Shader.PropertyToID("_LocalDensitySize");
        internal static readonly int LocalDensityInfluenceId = Shader.PropertyToID("_LocalDensityInfluence");
        internal static readonly int LocalDisplacementRTId     = Shader.PropertyToID("_LocalDisplacementRT");
        internal static readonly int LocalDisplacementCenterId = Shader.PropertyToID("_LocalDisplacementCenter");
        internal static readonly int LocalDisplacementSizeId   = Shader.PropertyToID("_LocalDisplacementSize");
        internal static readonly int LocalDisplacementStrengthId = Shader.PropertyToID("_LocalDisplacementStrength");

        private Matrix4x4 _prevViewProj = Matrix4x4.identity;
        private bool _prevViewProjValid;
        private bool _loggedOnce;
        private bool _addOnceLogged;

        public Material GetOrCreateMaterial()
        {
            if (_material != null && _material.shader != null) return _material;
            if (OverrideMaterial != null) { _material = OverrideMaterial; return _material; }
            Shader shader = Shader.Find("Hidden/ProjectC/VolumetricClouds");
            if (shader == null) { Debug.LogError("[VolumetricClouds] Shader not found."); return null; }
            _material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            return _material;
        }

        public override void Create()
        {
            _prevViewProjValid = false;
            Debug.Log("[VolClouds] Create() called — RenderFeature initialized.");
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (!_addOnceLogged)
            {
                _addOnceLogged = true;
                Debug.Log($"[VolClouds] AddRenderPasses() called! cameraType={renderingData.cameraData.cameraType} cam={renderingData.cameraData.camera?.name} frameCount={Time.frameCount}");
            }
            if (renderingData.cameraData.cameraType == CameraType.Preview) return;
            Material mat = GetOrCreateMaterial();
            if (mat == null) return;
            ApplyProperties(mat, renderingData.cameraData.camera);

            Camera cam = renderingData.cameraData.camera;
            int hw = Mathf.Max(1, cam.pixelWidth);
            int hh = Mathf.Max(1, cam.pixelHeight);
            EnsureHistoryRT(ref _cloudHistoryA, hw, hh, "_CloudHistoryA");
            EnsureHistoryRT(ref _cloudHistoryB, hw, hh, "_CloudHistoryB");

            RTHandle readHandle = _historyIdx == 0 ? _cloudHistoryA : _cloudHistoryB;
            RTHandle writeHandle = _historyIdx == 0 ? _cloudHistoryB : _cloudHistoryA;
            _historyIdx ^= 1;

            var pass = new VolumetricCloudsPass(mat, HalfResRender, TemporalReprojection,
                DebugDensityDirect, _prevViewProj, _prevViewProjValid, readHandle, writeHandle,
                Opacity, ColorIntensity);
            pass.renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
            renderer.EnqueuePass(pass);
        }

        private static void EnsureHistoryRT(ref RTHandle handle, int w, int h, string name)
        {
            if (handle != null && handle.rt != null && handle.rt.width == w && handle.rt.height == h) return;
            handle?.Release();
            var rt = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32)
            { name = name, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            rt.Create();
            handle = RTHandles.Alloc(rt, true);
        }

        private void ApplyProperties(Material mat, Camera camera)
        {
            // Material properties (in shader Properties — mat.Set*)
            mat.SetFloat(CloudBottomYId, CloudBottomY);
            mat.SetFloat(CloudTopYId, CloudTopY);
            mat.SetInt(RaymarchStepsId, RaymarchSteps);
            mat.SetFloat(MaxRayDistanceId, MaxRayDistance);
            mat.SetFloat(HeightEdgeId, HeightEdgeSoftness);
            mat.SetFloat(CoverageScaleId, CoverageScale);
            mat.SetFloat(CoverageThresholdId, CoverageThreshold);
            mat.SetFloat(TemporalBlendId, (TemporalReprojection && _prevViewProjValid) ? 0.9f : 0f);

            // Global-only (NOT in shader Properties — Shader.SetGlobal* to avoid material-slot shadowing)
            Shader.SetGlobalFloat(NoiseTileSizeId, NoiseTileSize);
            Shader.SetGlobalFloat(DensityMultId, DensityMultiplier);
            Shader.SetGlobalFloat(LightAbsorptionId, LightAbsorption);
            Shader.SetGlobalFloat(CloudOpacityId, Opacity);
            Shader.SetGlobalFloat(CloudColorIntensityId, ColorIntensity);
            Shader.SetGlobalColor(DayRampTopId, DayRampTop);
            Shader.SetGlobalColor(DayRampMidId, DayRampMid);
            Shader.SetGlobalColor(DayRampBotId, DayRampBot);
            Shader.SetGlobalColor(SunsetRampTopId, SunsetRampTop);
            Shader.SetGlobalColor(SunsetRampMidId, SunsetRampMid);
            Shader.SetGlobalColor(SunsetRampBotId, SunsetRampBot);

            if (Time.frameCount % 120 == 0)
            {
                var ldInst = LocalDensityBuffer.Instance;
                Debug.Log($"[VolClouds] Dens={DensityMultiplier:F2} Absorb={LightAbsorption:F3} Opacity={Opacity:F2} Color={ColorIntensity:F2} CovThresh={CoverageThreshold:F2}" +
                    (ldInst != null ? $" | LocalDensity OK: RT={ldInst.GetDensityRT()!=null} size={ldInst.Resolution*ldInst.TexelSize}" : " | LocalDensity: NULL"));
            }

            if (CloudNoise3D != null)
                mat.SetTexture(CloudNoise3DId, CloudNoise3D);

            // Phase 2.2: LocalDensityBuffer → shader (via mat.Set* — now in Properties)
            var ld = LocalDensityBuffer.Instance;
            if (ld != null)
            {
                var rt = ld.GetDensityRT();
                if (rt != null)
                {
                    bool isDisp = ld.BufferMode == LocalDensityBuffer.Mode.Displacement;

                    if (isDisp)
                    {
                        mat.SetTexture(LocalDisplacementRTId, rt);
                        mat.SetVector(LocalDisplacementCenterId, ld.Center);
                        mat.SetFloat(LocalDisplacementSizeId, ld.Resolution * ld.TexelSize);
                        mat.SetFloat(LocalDisplacementStrengthId, DisplacementStrength);
                    }
                    else
                    {
                        mat.SetTexture(LocalDensityRTId, rt);
                        mat.SetVector(LocalDensityCenterId, ld.Center);
                        mat.SetFloat(LocalDensitySizeId, ld.Resolution * ld.TexelSize);
                        mat.SetFloat(LocalDensityInfluenceId, LocalDensityInfluence);
                    }

                    // Keyword via LocalKeyword (local multi_compile)
                    if (mat.shader != null)
                    {
                        var kwDisp = new LocalKeyword(mat.shader, "_LOCALDENSITY_DISPLACEMENT");
                        mat.SetKeyword(kwDisp, isDisp);
                    }

                    if (!_loggedOnce)
                    {
                        _loggedOnce = true;
                        Debug.Log($"[VolClouds] LocalDensity VIA MAT: mode={ld.BufferMode} RT={rt.name} {rt.width}x{rt.height}x{rt.volumeDepth} center={ld.Center} size={ld.Resolution * ld.TexelSize}" +
                            (isDisp ? $" dispStrength={DisplacementStrength}" : $" influence={LocalDensityInfluence}"));
                    }
                }
                else if (!_loggedOnce)
                {
                    _loggedOnce = true;
                    Debug.LogWarning("[VolClouds] LocalDensity RT is NULL!");
                }
            }
            else if (!_loggedOnce)
            {
                _loggedOnce = true;
                Debug.LogWarning("[VolClouds] LocalDensityBuffer.Instance is NULL!");
            }

            Vector3 windDir = Vector3.right; float windSpeed = 1f;
            if (WindManager.Instance != null)
            { windDir = WindManager.Instance.CurrentWindDirection.normalized; windSpeed = Mathf.Max(WindManager.Instance.CurrentWindSpeed, 0.1f); }
            float windMult = WindSpeedMultiplier * 0.05f;
            Vector4 windOffset;
            if (OverrideMaterial != null)
                windOffset = (Vector4)(windDir * windSpeed * Time.time * windMult);
            else
            { windOffset = mat.GetVector(WindOffsetId); windOffset += (Vector4)(windDir * windSpeed * Time.deltaTime * windMult); }
            mat.SetVector(WindOffsetId, windOffset);

            Vector3 sunDir = RenderSettings.sun != null ? RenderSettings.sun.transform.forward : new Vector3(0.3f, 0.7f, -0.6f).normalized;
            mat.SetVector(SunDirectionId, sunDir);

            if (BlueNoiseTexture != null) mat.SetTexture(BlueNoiseTexId, BlueNoiseTexture);
            if (mat.shader != null)
            { var kw = new LocalKeyword(mat.shader, "_BLUE_NOISE_ON"); mat.SetKeyword(kw, BlueNoiseDither && BlueNoiseTexture != null); }

            Matrix4x4 proj = GL.GetGPUProjectionMatrix(camera.projectionMatrix, false);
            Matrix4x4 view = camera.worldToCameraMatrix;
            if (_prevViewProjValid) mat.SetMatrix(PrevViewProjId, _prevViewProj);
            _prevViewProj = proj * view;
            _prevViewProjValid = true;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_material != null) { Object.DestroyImmediate(_material); _material = null; }
                if (_cloudHistoryA != null) { _cloudHistoryA.Release(); _cloudHistoryA = null; }
                if (_cloudHistoryB != null) { _cloudHistoryB.Release(); _cloudHistoryB = null; }
            }
        }
    }

    internal sealed class VolumetricCloudsPass : ScriptableRenderPass
    {
        private const string PassName = "VolumetricClouds";
        private Material _material;
        private readonly bool _halfRes, _temporal, _debugDirect;
        private readonly Matrix4x4 _prevVp;
        private readonly bool _prevVpValid;
        private RTHandle _historyRead, _historyWrite;
        private readonly float _opacity, _colorIntensity;

        private class PassData
        {
            public Material Material;
            public TextureHandle CloudRT, HistoryRT;
            public Vector4 TargetSize;
            public Matrix4x4 ViewToWorld, InvProjection;
            public float Opacity, ColorIntensity;
        }

        public VolumetricCloudsPass(Material material, bool halfRes, bool temporal,
            bool debugDirect, Matrix4x4 prevVp, bool prevVpValid,
            RTHandle historyRead, RTHandle historyWrite,
            float opacity, float colorIntensity)
        {
            _material = material; _halfRes = halfRes; _temporal = temporal;
            _debugDirect = debugDirect; _prevVp = prevVp; _prevVpValid = prevVpValid;
            _historyRead = historyRead; _historyWrite = historyWrite;
            _opacity = opacity; _colorIntensity = colorIntensity;
            profilingSampler = new ProfilingSampler(PassName);
            ConfigureInput(ScriptableRenderPassInput.Depth);
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (_material == null) return;
            var resourceData = frameData.Get<UniversalResourceData>();
            var colorTarget = resourceData.activeColorTexture;
            if (!colorTarget.IsValid()) return;
            var cameraData = frameData.Get<UniversalCameraData>();
            var cam = cameraData.camera;
            Matrix4x4 viewToWorld = cam.cameraToWorldMatrix;
            Matrix4x4 invProj = GL.GetGPUProjectionMatrix(cam.projectionMatrix, false).inverse;

            // DEBUG: Pass 0 (B&W) straight to camera
            if (_debugDirect)
            {
                int cw = Mathf.Max(1, cam.pixelWidth), ch = Mathf.Max(1, cam.pixelHeight);
                Vector4 fs = new Vector4(cw, ch, 1f / cw, 1f / ch);
                using (var builder = renderGraph.AddRasterRenderPass<PassData>("VolumetricClouds_Debug", out var d, profilingSampler))
                {
                    d.Material = _material; d.TargetSize = fs; d.ViewToWorld = viewToWorld; d.InvProjection = invProj;
                    builder.SetRenderAttachment(colorTarget, 0, AccessFlags.ReadWrite);
                    builder.AllowPassCulling(false); builder.AllowGlobalStateModification(true);
                    builder.SetRenderFunc(static (PassData p, RasterGraphContext ctx) =>
                    {
                        ctx.cmd.SetGlobalVector(VolumetricCloudsRenderFeature.CloudTargetSizeId, p.TargetSize);
                        ctx.cmd.SetGlobalMatrix(VolumetricCloudsRenderFeature.ViewToWorldId, p.ViewToWorld);
                        ctx.cmd.SetGlobalMatrix(VolumetricCloudsRenderFeature.InvProjectionId, p.InvProjection);
                        ctx.cmd.DrawProcedural(Matrix4x4.identity, p.Material, 0, MeshTopology.Triangles, 3, 1);
                    });
                }
                return;
            }

            // Pass 1 → colorTarget direct (cloudRT/history bypassed for tuning)
            int cw2 = Mathf.Max(1, cam.pixelWidth), ch2 = Mathf.Max(1, cam.pixelHeight);
            Vector4 fs2 = new Vector4(cw2, ch2, 1f / cw2, 1f / ch2);
            using (var builder = renderGraph.AddRasterRenderPass<PassData>("VolumetricClouds_Direct", out var pd, profilingSampler))
            {
                pd.Material = _material; pd.TargetSize = fs2; pd.ViewToWorld = viewToWorld; pd.InvProjection = invProj;
                pd.Opacity = _opacity; pd.ColorIntensity = _colorIntensity;
                builder.SetRenderAttachment(colorTarget, 0, AccessFlags.ReadWrite);
                builder.AllowPassCulling(false); builder.AllowGlobalStateModification(true);
                builder.SetRenderFunc(static (PassData p, RasterGraphContext ctx) =>
                {
                    ctx.cmd.SetGlobalVector(VolumetricCloudsRenderFeature.CloudTargetSizeId, p.TargetSize);
                    ctx.cmd.SetGlobalMatrix(VolumetricCloudsRenderFeature.ViewToWorldId, p.ViewToWorld);
                    ctx.cmd.SetGlobalMatrix(VolumetricCloudsRenderFeature.InvProjectionId, p.InvProjection);
                    ctx.cmd.SetGlobalFloat(VolumetricCloudsRenderFeature.CloudOpacityId, p.Opacity);
                    ctx.cmd.SetGlobalFloat(VolumetricCloudsRenderFeature.CloudColorIntensityId, p.ColorIntensity);
                    ctx.cmd.DrawProcedural(Matrix4x4.identity, p.Material, 1, MeshTopology.Triangles, 3, 1);
                });
            }
        }
    }
}
