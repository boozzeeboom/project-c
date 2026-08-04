// StormLightningVfx.cs — Phase 2.4 (переписан)
// Subscribes to StormCellDirector.OnLightningTriggered and plays VFX Graph lightning bolt.
// Decoupled from old StormController; forward-compatible with WeatherCellManager (3.3).
// Null-guard: VisualEffect may be absent in test scenes.

using UnityEngine;
using UnityEngine.VFX;

namespace ProjectC.World.Clouds
{
    /// <summary>
    /// Подписывается на StormCellDirector.OnLightningTriggered и запускает
    /// VFX Graph молнию (LightningBolt.vfx) с параметрами позиции и интенсивности.
    /// Слой 2 архитектуры штормов 3.0: трансляция события → параметры VFX.
    /// </summary>
    public class StormLightningVfx : MonoBehaviour
    {
        [Header("VFX")]
        [Tooltip("VisualEffect компонент с LightningBolt.vfx.")]
        public VisualEffect Vfx;

        [Header("Director")]
        [Tooltip("StormCellDirector (источник событий). Если null — ищет через Instance.")]
        public StormCellDirector Director;

        [Header("Lightning Shape")]
        [Tooltip("Высота верхней точки молнии над центром ячейки.")]
        [Range(100f, 500f)] public float BoltTopOffset = 300f;
        [Tooltip("Высота нижней точки молнии под центром ячейки.")]
        [Range(0f, 200f)] public float BoltBottomOffset = 50f;
        [Tooltip("Длительность видимости молнии (сек).")]
        [Range(0.05f, 1f)] public float BoltDuration = 0.3f;

        // VFX property IDs
        private static readonly int StartPosId   = Shader.PropertyToID("StartPos");
        private static readonly int EndPosId     = Shader.PropertyToID("EndPos");
        private static readonly int SeedId       = Shader.PropertyToID("Seed");
        private static readonly int IntensityId  = Shader.PropertyToID("Intensity");

        private void Start()
        {
            if (Vfx == null)
                Vfx = GetComponent<VisualEffect>();

            if (Vfx != null)
                Vfx.Stop();

            if (Director == null)
            {
                Director = StormCellDirector.Instance;
                if (Director == null)
                    Debug.LogWarning("[StormLightningVfx] StormCellDirector.Instance is null. Lightning won't trigger.");
            }
        }

        private void OnEnable()
        {
            // Resolve director reference in OnEnable (may not be ready in Start)
            if (Director == null)
                Director = StormCellDirector.Instance;

            if (Director != null)
                Director.OnLightningTriggered += HandleLightning;
        }

        private void OnDisable()
        {
            if (Director != null)
                Director.OnLightningTriggered -= HandleLightning;
        }

        private void HandleLightning(Vector3 worldPos, float intensity)
        {
            if (Vfx == null) return;

            // Silently skip if param not yet added to VFX Graph
            if (Vfx.HasVector3(StartPosId)) Vfx.SetVector3(StartPosId, worldPos + Vector3.up * BoltTopOffset);
            if (Vfx.HasVector3(EndPosId))   Vfx.SetVector3(EndPosId, worldPos - Vector3.up * BoltBottomOffset);
            if (Vfx.HasFloat(SeedId))       Vfx.SetFloat(SeedId, Random.value);
            if (Vfx.HasFloat(IntensityId))  Vfx.SetFloat(IntensityId, Mathf.Clamp01(intensity));
            Vfx.Play();

            // Auto-stop after duration
            StartCoroutine(StopAfterDelay());
        }

        private System.Collections.IEnumerator StopAfterDelay()
        {
            yield return new WaitForSeconds(BoltDuration);
            if (Vfx != null) Vfx.Stop();
        }
    }
}
