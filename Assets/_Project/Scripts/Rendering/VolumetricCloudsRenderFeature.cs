// VolumetricCloudsRenderFeature.cs — CLOUD_system 3.0
// URP ScriptableRendererFeature (RenderGraph API).
// Phase 1.3: B&W density fullscreen pass
// Phase 1.4: height profile + wind + procedural coverage
// Phase 1.5: colored light-march pass with Ghibli ramps
// Phase 1.6: half-res render + blue-noise dither + temporal reprojection (MRT history)
//
// FIXES 2026-08-02 (post-implementation debug):
//  - _PrevViewProj теперь реально передаётся в материал (предыдущий кадр, ДО обновления)
//  - _CloudHistoryRT (persistent, full-res) импортируется в RenderGraph и используется
//    как MRT-таргет 1 в композите — настоящий temporal ping-pong
//  - Pass 1: _CloudTargetSize (полуразрешение) → корректные UV реймарча
//  - Добавлены NoiseTileSize / CoverageScale / CoverageThreshold / _TemporalBlend

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
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
        [Range(0.1f, 10f)] public float DensityMultiplier = 1f;
        [Range(0.001f, 0.5f)] public float LightAbsorption = 0.05f;
        [Range(0.01f, 0.5f)] public float HeightEdgeSoftness = 0.15f;

        [Header("Noise / Coverage")]
        [Tooltip("World units per 3D-noise tile (128 texels). ~1024 → cloud masses 64–256 m.")]
        [Range(256f, 4096f)] public float NoiseTileSize = 1024f;
        [Tooltip("World scale of the 2D coverage FBM (1/x = feature size in m).")]
        [Range(0.0001f, 0.01f)] public float CoverageScale = 0.0008f;
        [Tooltip("Coverage cutoff: below → clear sky hole. Lower = more clouds.")]
        [Range(0.1f, 0.9f)] public float CoverageThreshold = 0.5f;

        [Header("Phase 1.6: Quality")]
        public bool HalfResRender = true;
        public bool TemporalReprojection = true;
        public bool BlueNoiseDither = true;
        public Texture2D BlueNoiseTexture;

        [Header("Wind")]
        [Range(0f, 5f)] public float WindSpeedMultiplier = 1f;

        [Header("Debug")]
        [Tooltip("Draw Pass 0 (B&W density) directly to the camera color, skipping MRT/temporal/history. Binary test.")]
        public bool DebugDensityDirect = false;

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

        // Phase 1.6: ping-pong history RTs — composite (Pass B) READS one, history
        // pass (Pass C) WRITES the other; they swap each frame. Two textures avoid
        // the RenderGraph "read + write the same texture" conflict (see pitfall #3).
        private RTHandle _cloudHistoryA;
        private RTHandle _cloudHistoryB;
        private int _historyIdx;

        // Shader property IDs
        private static readonly int CloudNoise3DId     = Shader.PropertyToID("_CloudNoise3D");
        private static readonly int CloudBottomYId     = Shader.PropertyToID("_CloudBottomY");
        private static readonly int CloudTopYId        = Shader.PropertyToID("_CloudTopY");
        private static readonly int RaymarchStepsId    = Shader.PropertyToID("_RaymarchSteps");
        private static readonly int MaxRayDistanceId   = Shader.PropertyToID("_MaxRayDistance");
        private static readonly int DensityMultId      = Shader.PropertyToID("_DensityMultiplier");
        private static readonly int LightAbsorptionId  = Shader.PropertyToID("_LightAbsorption");
        private static readonly int HeightEdgeId       = Shader.PropertyToID("_HeightEdgeSoftness");
        private static readonly int NoiseTileSizeId    = Shader.PropertyToID("_NoiseTileSize");
        private static readonly int CoverageScaleId    = Shader.PropertyToID("_CoverageScale");
        private static readonly int CoverageThresholdId = Shader.PropertyToID("_CoverageThreshold");
        private static readonly int TemporalBlendId    = Shader.PropertyToID("_TemporalBlend");
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
        internal static readonly int CloudRTId          = Shader.PropertyToID("_CloudRT");
        internal static readonly int CloudTargetSizeId  = Shader.PropertyToID("_CloudTargetSize");
        internal static readonly int ViewToWorldId      = Shader.PropertyToID("_Cloud_ViewToWorld");
        internal static readonly int InvProjectionId    = Shader.PropertyToID("_Cloud_InvProj");

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

            // Ensure persistent ping-pong history RTs (full-res)
            Camera cam = renderingData.cameraData.camera;
            int hw = Mathf.Max(1, cam.pixelWidth);
            int hh = Mathf.Max(1, cam.pixelHeight);
            EnsureHistoryRT(ref _cloudHistoryA, hw, hh, "_CloudHistoryA");
            EnsureHistoryRT(ref _cloudHistoryB, hw, hh, "_CloudHistoryB");

            // Swap: Pass B reads the PREVIOUS frame's written RT, Pass C writes the other.
            RTHandle readHandle = _historyIdx == 0 ? _cloudHistoryA : _cloudHistoryB;
            RTHandle writeHandle = _historyIdx == 0 ? _cloudHistoryB : _cloudHistoryA;
            _historyIdx ^= 1;

            var pass = new VolumetricCloudsPass(mat, HalfResRender, TemporalReprojection,
                DebugDensityDirect, _prevViewProj, _prevViewProjValid, readHandle, writeHandle);
            // IMPORTANT: render AFTER the skybox (AfterRenderingOpaques=300 < BeforeRenderingSkybox=350 —
            // the skybox would overwrite the clouds with the star dome). Clouds sit between sky and transparents.
            pass.renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
            renderer.EnqueuePass(pass);
        }

        private static void EnsureHistoryRT(ref RTHandle handle, int w, int h, string name)
        {
            if (handle != null && handle.rt != null && handle.rt.width == w && handle.rt.height == h)
                return;
            handle?.Release();
            var rt = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            rt.Create();
            handle = RTHandles.Alloc(rt, true); // transferOwnership → Release() frees RT
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
            mat.SetFloat(NoiseTileSizeId, NoiseTileSize);
            mat.SetFloat(CoverageScaleId, CoverageScale);
            mat.SetFloat(CoverageThresholdId, CoverageThreshold);
            // First frame (no valid _PrevViewProj yet) → _TemporalBlend=0 so the
            // composite writes `current` un-attenuated (0.9×history=0 would make it faint).
            mat.SetFloat(TemporalBlendId,
                (TemporalReprojection && _prevViewProjValid) ? 0.9f : 0f);

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
            float windMult = WindSpeedMultiplier * 0.05f;
            Vector4 windOffset;
            if (OverrideMaterial != null)
            {
                // Shared asset — never mutate it; derive deterministically from time
                windOffset = (Vector4)(windDir * windSpeed * Time.time * windMult);
            }
            else
            {
                windOffset = mat.GetVector(WindOffsetId);
                windOffset += (Vector4)(windDir * windSpeed * Time.deltaTime * windMult);
            }
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
            if (mat.shader != null)
            {
                var kw = new LocalKeyword(mat.shader, "_BLUE_NOISE_ON");
                mat.SetKeyword(kw, BlueNoiseDither && BlueNoiseTexture != null);
            }

            // Temporal: set PREVIOUS frame VP into the material, then store current for next frame
            Matrix4x4 proj = GL.GetGPUProjectionMatrix(camera.projectionMatrix, false);
            Matrix4x4 view = camera.worldToCameraMatrix;
            if (_prevViewProjValid)
                mat.SetMatrix(PrevViewProjId, _prevViewProj);
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
                if (_cloudHistoryA != null)
                {
                    _cloudHistoryA.Release();
                    _cloudHistoryA = null;
                }
                if (_cloudHistoryB != null)
                {
                    _cloudHistoryB.Release();
                    _cloudHistoryB = null;
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
        private readonly bool _debugDirect;
        private readonly Matrix4x4 _prevVp;
        private readonly bool _prevVpValid;
        private RTHandle _historyRead;
        private RTHandle _historyWrite;

        private class PassData
        {
            public Material Material;
            public TextureHandle CloudRT;
            public TextureHandle HistoryRT;
            public Vector4 TargetSize;
            public Matrix4x4 ViewToWorld;
            public Matrix4x4 InvProjection;
        }

        public VolumetricCloudsPass(Material material, bool halfRes, bool temporal,
            bool debugDirect, Matrix4x4 prevVp, bool prevVpValid,
            RTHandle historyRead, RTHandle historyWrite)
        {
            _material = material;
            _halfRes = halfRes;
            _temporal = temporal;
            _debugDirect = debugDirect;
            _prevVp = prevVp;
            _prevVpValid = prevVpValid;
            _historyRead = historyRead;
            _historyWrite = historyWrite;
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

            // Camera matrices for world-space ray reconstruction (URP RenderGraph does NOT
            // set UNITY_MATRIX_I_P / UNITY_MATRIX_I_V automatically in custom passes).
            Matrix4x4 viewToWorld = cam.cameraToWorldMatrix;
            Matrix4x4 invProj = GL.GetGPUProjectionMatrix(cam.projectionMatrix, false).inverse;

            // Determine resolution
            int w = _halfRes ? Mathf.Max(1, cam.pixelWidth / 2) : cam.pixelWidth;
            int h = _halfRes ? Mathf.Max(1, cam.pixelHeight / 2) : cam.pixelHeight;

            // Create half-res cloud RT (transient)
            var desc = colorTarget.GetDescriptor(renderGraph);
            desc.width = w;
            desc.height = h;
            desc.depthBufferBits = 0;
            desc.msaaSamples = MSAASamples.None;
            desc.name = "_CloudRT";
            TextureHandle cloudRT = renderGraph.CreateTexture(desc);

            // Import ping-pong history RTs (full-res):
            // _historyRead  = the RT written LAST frame → sampled by composite (Pass B)
            // _historyWrite = the OTHER RT → written this frame by history pass (Pass C)
            // (Ping-pong: never read+write the same texture across the frame.)
            TextureHandle historyRead = default;
            TextureHandle historyWrite = default;
            if (_historyRead != null && _historyRead.rt != null)
                historyRead = renderGraph.ImportTexture(_historyRead);
            if (_historyWrite != null && _historyWrite.rt != null)
                historyWrite = renderGraph.ImportTexture(_historyWrite);

            Vector4 targetSize = new Vector4(w, h, 1f / Mathf.Max(w, 1), 1f / Mathf.Max(h, 1));

            // --- DEBUG: Pass 0 (B&W density) straight to camera color — no MRT/temporal/history ---
            if (_debugDirect)
            {
                int cw = Mathf.Max(1, cam.pixelWidth);
                int ch = Mathf.Max(1, cam.pixelHeight);
                Vector4 fullSize = new Vector4(cw, ch, 1f / cw, 1f / ch);
                using (var builder = renderGraph.AddRasterRenderPass<PassData>(
                    "VolumetricClouds_DebugDirect", out var dbgData, profilingSampler))
                {
                    dbgData.Material = _material;
                    dbgData.TargetSize = fullSize;
                    dbgData.ViewToWorld = viewToWorld;
                    dbgData.InvProjection = invProj;
                    builder.SetRenderAttachment(colorTarget, 0, AccessFlags.ReadWrite);
                    builder.AllowPassCulling(false);
                    builder.AllowGlobalStateModification(true);
                    builder.SetRenderFunc(static (PassData data, RasterGraphContext ctx) =>
                    {
                        ctx.cmd.SetGlobalVector(VolumetricCloudsRenderFeature.CloudTargetSizeId, data.TargetSize);
                        ctx.cmd.SetGlobalMatrix(VolumetricCloudsRenderFeature.ViewToWorldId, data.ViewToWorld);
                        ctx.cmd.SetGlobalMatrix(VolumetricCloudsRenderFeature.InvProjectionId, data.InvProjection);
                        ctx.cmd.DrawProcedural(Matrix4x4.identity, data.Material, 0,
                            MeshTopology.Triangles, 3, 1);
                    });
                }
                return;
            }

            // --- Pass A: Raymarch → _CloudRT (half-res) ---
            using (var builder = renderGraph.AddRasterRenderPass<PassData>(
                PassName, out var passData, profilingSampler))
            {
                passData.Material = _material;
                passData.CloudRT = cloudRT;
                passData.TargetSize = targetSize;
                passData.ViewToWorld = viewToWorld;
                passData.InvProjection = invProj;

                builder.SetRenderAttachment(cloudRT, 0, AccessFlags.Write);
                builder.AllowPassCulling(false);
                builder.AllowGlobalStateModification(true);

                builder.SetRenderFunc(static (PassData data, RasterGraphContext ctx) =>
                {
                    ctx.cmd.SetGlobalVector(VolumetricCloudsRenderFeature.CloudTargetSizeId, data.TargetSize);
                    ctx.cmd.SetGlobalMatrix(VolumetricCloudsRenderFeature.ViewToWorldId, data.ViewToWorld);
                    ctx.cmd.SetGlobalMatrix(VolumetricCloudsRenderFeature.InvProjectionId, data.InvProjection);
                    // Pass 1: colored light-march (Phase 1.5)
                    ctx.cmd.DrawProcedural(Matrix4x4.identity, data.Material, 1,
                        MeshTopology.Triangles, 3, 1);
                });
            }

            // --- Pass B: Upsample + temporal composite → colorTarget (SINGLE target) ---
            // Same structure as the proven EdgeDetection pass: one attachment, DrawProcedural,
            // blend state from the shader (Pass 2 = SrcAlpha OneMinusSrcAlpha).
            using (var builder = renderGraph.AddRasterRenderPass<PassData>(
                "VolumetricClouds_Composite", out var compData, profilingSampler))
            {
                compData.Material = _material;
                compData.CloudRT = cloudRT;
                compData.HistoryRT = historyRead;
                compData.ViewToWorld = viewToWorld;
                compData.InvProjection = invProj;

                builder.SetRenderAttachment(colorTarget, 0, AccessFlags.ReadWrite);
                builder.UseTexture(cloudRT, AccessFlags.Read);
                if (historyRead.IsValid())
                    builder.UseTexture(historyRead, AccessFlags.Read);
                builder.AllowPassCulling(false);
                builder.AllowGlobalStateModification(true);

                builder.SetRenderFunc(static (PassData data, RasterGraphContext ctx) =>
                {
                    ctx.cmd.SetGlobalTexture(VolumetricCloudsRenderFeature.CloudRTId, data.CloudRT);
                    if (data.HistoryRT.IsValid())
                        ctx.cmd.SetGlobalTexture(VolumetricCloudsRenderFeature.CloudHistoryRTId, data.HistoryRT);
                    ctx.cmd.SetGlobalMatrix(VolumetricCloudsRenderFeature.ViewToWorldId, data.ViewToWorld);
                    ctx.cmd.SetGlobalMatrix(VolumetricCloudsRenderFeature.InvProjectionId, data.InvProjection);
                    // Pass 2: composite — blends result onto the camera color (SrcAlpha)
                    ctx.cmd.DrawProcedural(Matrix4x4.identity, data.Material, 2,
                        MeshTopology.Triangles, 3, 1);
                });
            }

            // --- Pass C: raw composite result → ping-pong history RT (single target) ---
            // Pass 3 = same composite fragment, Blend One Zero. Reads the OPPOSITE history
            // texture (historyRead) while writing historyWrite — legal in RenderGraph.
            if (historyWrite.IsValid())
            {
                using (var builder = renderGraph.AddRasterRenderPass<PassData>(
                    "VolumetricClouds_History", out var histData, profilingSampler))
                {
                    histData.Material = _material;
                    histData.CloudRT = cloudRT;
                    histData.HistoryRT = historyRead;
                    histData.ViewToWorld = viewToWorld;
                    histData.InvProjection = invProj;

                    builder.SetRenderAttachment(historyWrite, 0, AccessFlags.Write);
                    builder.UseTexture(cloudRT, AccessFlags.Read);
                    if (historyRead.IsValid())
                        builder.UseTexture(historyRead, AccessFlags.Read);
                    builder.AllowPassCulling(false);
                    builder.AllowGlobalStateModification(true);

                    builder.SetRenderFunc(static (PassData data, RasterGraphContext ctx) =>
                    {
                        ctx.cmd.SetGlobalTexture(VolumetricCloudsRenderFeature.CloudRTId, data.CloudRT);
                        if (data.HistoryRT.IsValid())
                            ctx.cmd.SetGlobalTexture(VolumetricCloudsRenderFeature.CloudHistoryRTId, data.HistoryRT);
                        ctx.cmd.SetGlobalMatrix(VolumetricCloudsRenderFeature.ViewToWorldId, data.ViewToWorld);
                        ctx.cmd.SetGlobalMatrix(VolumetricCloudsRenderFeature.InvProjectionId, data.InvProjection);
                        // Pass 3: raw result into history (One Zero — RT not cleared by RenderGraph)
                        ctx.cmd.DrawProcedural(Matrix4x4.identity, data.Material, 3,
                            MeshTopology.Triangles, 3, 1);
                    });
                }
            }
        }
    }
}
