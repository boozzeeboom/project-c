// DistantFocusRenderFeature.cs — DF-001 rev.7: far-field фокус своим проходом.
// Архитектура скопирована с EdgeDetectionRenderFeature: ConfigureInput(Depth),
// копия цвета через AddCopyPass, запись назад, фулскрин-треугольник.
// Все пороги/сила — поля фичи (Inspector), хардкода нет. Динамика взгляда
// (_FarFocusLock) приходит глобалом из FarFocusController.

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace ProjectC.Rendering
{
    /// <summary>Самодиагностика прохода: ForceBlur доказывает что проход бежит, ShowDepth — что глубина живая.</summary>
    public enum FarFocusDebugView
    {
        Off = 0,
        ForceBlur = 1,
        ShowDepth = 2
    }

    [DisallowMultipleRendererFeature("Distant Focus (far-field)")]
    [SupportedOnRenderer(typeof(UniversalRendererData))]
    public sealed class DistantFocusRenderFeature : ScriptableRendererFeature
    {
        [Header("Состояние")]
        [Tooltip("Выкл = проход не ставится, эффекта нет.")]
        public bool Active = true;

        [Header("Диагностика")]
        [Tooltip("ForceBlur = мылит весь кадр (проверка что проход бежит). ShowDepth = полосы глубины.")]
        public FarFocusDebugView DebugView = FarFocusDebugView.Off;

        [Header("Дальняя зона (размыта, пока не смотрим)")]
        [Tooltip("Глубина (м), с которой начинается дальнее размытие. Настраивается под горы/здания.")]
        [Min(1f)] public float FarStart = 800f;
        [Tooltip("Глубина (м) полного дальнего размытия.")]
        [Min(2f)] public float FarEnd = 4000f;
        [Tooltip("Сила дальнего размытия.")]
        [Range(0f, 2f)] public float FarStrength = 1f;

        [Header("Ближняя зона (чуть мылится при локе вдаль)")]
        [Tooltip("Всё ближе — всегда резкое (персонаж, зум корабля).")]
        [Min(1f)] public float CharMax = 40f;
        [Tooltip("Сила среднего плана при локе взгляда вдаль.")]
        [Range(0f, 1f)] public float NearStrength = 0.35f;

        [Header("Ядро")]
        [Tooltip("Макс. радиус блюра, px.")]
        [Range(1f, 32f)] public float MaxRadius = 12f;

        [Header("Material")]
        public Material OverrideMaterial;
        [Tooltip("Ссылка на hidden shader (обязательна для inclusion в билд). Fallback: Shader.Find.")]
        [SerializeField] private Shader _distantFocusShader;

        private Material _material;

        private static readonly int SourceTexId = Shader.PropertyToID("_FarFocusSource");
        private static readonly int TexelId = Shader.PropertyToID("_FarFocusTexel");

        public Material GetOrCreateMaterial()
        {
            if (_material != null && _material.shader != null) return _material;
            if (OverrideMaterial != null) { _material = OverrideMaterial; return _material; }
            Shader shader = _distantFocusShader != null ? _distantFocusShader : Shader.Find("Hidden/ProjectC/DistantFocusFar");
            if (shader == null) { Debug.LogError("[DistantFocusFeature] Shader not found."); return null; }
            _material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            return _material;
        }

        public override void Create() { }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (!Active) return;
            if (renderingData.cameraData.cameraType == CameraType.Preview) return;
            Material mat = GetOrCreateMaterial();
            if (mat == null) return;

            mat.SetFloat("_FarStart", FarStart);
            mat.SetFloat("_FarEnd", FarEnd);
            mat.SetFloat("_FarStrength", FarStrength);
            mat.SetFloat("_NearStrength", NearStrength);
            mat.SetFloat("_MaxRadius", MaxRadius);
            mat.SetFloat("_CharMax", CharMax);
            mat.SetFloat("_DebugView", (float)DebugView);

            var pass = new DistantFocusPass(mat);
            pass.renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
            renderer.EnqueuePass(pass);
        }

        protected override void Dispose(bool disposing)
        {
            if (!disposing) return;

#if UNITY_EDITOR
            if (_material != null) Object.DestroyImmediate(_material);
#else
            if (_material != null) Object.Destroy(_material);
#endif

            _material = null;
        }
    }

    internal sealed class DistantFocusPass : ScriptableRenderPass
    {
        private const string PassName = "DistantFocusFar";
        private const string CopyName = "CopyColorForFarFocus";

        private readonly Material _material;

        private static readonly int SourceTexId = Shader.PropertyToID("_FarFocusSource");
        private static readonly int TexelId = Shader.PropertyToID("_FarFocusTexel");

        private sealed class PassData
        {
            public Material Material;
            public TextureHandle SourceTex;
            public Vector4 Texel; // w,h,1/w,1/h цели
        }

        public DistantFocusPass(Material material)
        {
            _material = material;
            profilingSampler = new ProfilingSampler(PassName);
            ConfigureInput(ScriptableRenderPassInput.Depth);
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (_material == null) return;
            var resourceData = frameData.Get<UniversalResourceData>();
            var colorTarget = resourceData.activeColorTexture;

            // Копия цвета: читаем соседей из неё, пишем результат в цель (без feedback).
            var desc = colorTarget.GetDescriptor(renderGraph);
            desc.depthBufferBits = 0;
            desc.msaaSamples = (MSAASamples)1;
            desc.name = "FarFocusSourceCopy";
            TextureHandle sourceTex = renderGraph.CreateTexture(desc);
            RenderGraphUtils.AddCopyPass(renderGraph, colorTarget, sourceTex, CopyName, false);

            var targetDesc = colorTarget.GetDescriptor(renderGraph);
            var texel = new Vector4(targetDesc.width, targetDesc.height,
                1f / Mathf.Max(1, targetDesc.width), 1f / Mathf.Max(1, targetDesc.height));

            using (var builder = renderGraph.AddRasterRenderPass<PassData>(
                       PassName, out var passData, profilingSampler))
            {
                passData.Material = _material;
                passData.SourceTex = sourceTex;
                passData.Texel = texel;
                builder.SetRenderAttachment(colorTarget, 0, AccessFlags.ReadWrite);
                builder.AllowPassCulling(false);
                builder.AllowGlobalStateModification(true);
                builder.UseTexture(sourceTex, AccessFlags.Read);

                builder.SetRenderFunc(static (PassData data, RasterGraphContext ctx) =>
                {
                    ctx.cmd.SetGlobalTexture(SourceTexId, data.SourceTex);
                    ctx.cmd.SetGlobalVector(TexelId, data.Texel);
                    ctx.cmd.DrawProcedural(Matrix4x4.identity, data.Material, 0,
                        MeshTopology.Triangles, 3, 1);
                });
            }
        }
    }
}
