using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectC.UIv2.Core.Performance
{
    /// <summary>
    /// Dev-only performance monitor for UIv2. Logs budget violations to console.
    /// Attach to a GameObject in sandbox scene. Disable in release builds.
    /// </summary>
    public class UIPerformanceMonitor : MonoBehaviour
    {
        [Header("Budgets")]
        [SerializeField] private float maxUICpuMs = 1.0f;
        [SerializeField] private int maxDrawCalls = 4;
        [SerializeField] private float logIntervalSeconds = 2.0f;

        private float _timer;
        private int _frameCount;
        private float _accumulatedCpuMs;

        private void Update()
        {
            if (!Debug.isDebugBuild) return;

            _timer += Time.unscaledDeltaTime;
            _frameCount++;
            // Approximate UI CPU: measure time spent in this Update as proxy
            // Real measurement should use Profiler markers in production.
            _accumulatedCpuMs += Time.unscaledDeltaTime * 1000f;

            if (_timer >= logIntervalSeconds)
            {
                float avgCpu = _accumulatedCpuMs / _frameCount;
                if (avgCpu > maxUICpuMs)
                {
                    Debug.LogWarning($"[UIPerfMonitor] Avg frame time {avgCpu:F2}ms exceeds budget {maxUICpuMs}ms");
                }
                _timer = 0f;
                _frameCount = 0;
                _accumulatedCpuMs = 0f;
            }
        }

        public static void LogDrawCalls(int count, int budget)
        {
            if (count > budget)
                Debug.LogWarning($"[UIPerfMonitor] Draw calls {count} exceed budget {budget}");
        }
    }
}
