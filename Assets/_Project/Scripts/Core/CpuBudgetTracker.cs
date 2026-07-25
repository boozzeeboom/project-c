// ProjectC: CPU Budget Tracker — T-PERF-08
// Design: docs/world/admin_tool/perfomance/PERFORMANCE_MONITORING_RESEARCH.md §4.4
// Monitors CPU time per category using ProfilerRecorder, warns on budget exceed.
// Only active in DEVELOPMENT_BUILD to avoid overhead in release.
using UnityEngine;
using Unity.Profiling;

namespace ProjectC.Core
{
    /// <summary>
    /// Per-frame CPU budget monitor. Reads ProfilerRecorder counters and logs warnings
    /// when any category exceeds its budget. Zero allocation in release builds.
    /// </summary>
    public class CpuBudgetTracker : MonoBehaviour
    {
        [System.Serializable]
        public struct BudgetEntry
        {
            public string Name;
            public float BudgetMs60fps;   // 16.6ms frame budget
            public float BudgetMs30fps;   // 33.3ms fallback budget
        }

        [Header("Frame Budgets (ms)")]
        [SerializeField] private BudgetEntry[] _budgets = new[]
        {
            new BudgetEntry { Name = "Scripts",  BudgetMs60fps = 8.0f,  BudgetMs30fps = 20.0f },
            new BudgetEntry { Name = "Render",   BudgetMs60fps = 5.0f,  BudgetMs30fps = 10.0f },
            new BudgetEntry { Name = "Physics",  BudgetMs60fps = 3.0f,  BudgetMs30fps = 5.0f },
            new BudgetEntry { Name = "Network",  BudgetMs60fps = 1.0f,  BudgetMs30fps = 2.0f },
        };

        [Header("Warning Thresholds")]
        [SerializeField] private float _consecutiveWarnFrames = 3;

        private float[] _exceedCounters;
        private float _nextCheckTime;

        // Profiler recorders for built-in main thread timings
        private ProfilerRecorder _scriptsRecorder;
        private ProfilerRecorder _renderRecorder;
        private ProfilerRecorder _physicsRecorder;

        private void OnEnable()
        {
#if DEVELOPMENT_BUILD
            _exceedCounters = new float[_budgets.Length];

            // Start recorders for "Main Thread" time in each category
            _scriptsRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Main Thread", 15);
            _renderRecorder  = ProfilerRecorder.StartNew(ProfilerCategory.Render,  "Main Thread", 15);
            _physicsRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Physics, "Main Thread", 15);
#endif
        }

        private void OnDisable()
        {
#if DEVELOPMENT_BUILD
            _scriptsRecorder.Dispose();
            _renderRecorder.Dispose();
            _physicsRecorder.Dispose();
#endif
        }

#if DEVELOPMENT_BUILD
        private void Update()
        {
            if (Time.unscaledTime < _nextCheckTime) return;
            _nextCheckTime = Time.unscaledTime + 1f; // Check once per second

            CheckBudget(0, _scriptsRecorder);
            CheckBudget(1, _renderRecorder);
            CheckBudget(2, _physicsRecorder);
        }

        private void CheckBudget(int idx, ProfilerRecorder recorder)
        {
            if (idx >= _budgets.Length || !recorder.Valid) return;
            var budget = _budgets[idx];

            // LastValue is in nanoseconds — convert to ms
            float ms = recorder.LastValueAsDouble > 0
                ? (float)(recorder.LastValueAsDouble * 1e-6)
                : 0f;

            if (ms > budget.BudgetMs60fps)
            {
                _exceedCounters[idx]++;
                if (_exceedCounters[idx] >= _consecutiveWarnFrames)
                {
                    Debug.LogWarning(
                        $"[PERF] {budget.Name}: {ms:F1}ms > budget {budget.BudgetMs60fps}ms " +
                        $"(x{_exceedCounters[idx]:F0} consecutive)");
                    _exceedCounters[idx] = 0f; // Reset to avoid spam
                }
            }
            else
            {
                _exceedCounters[idx] = 0f;
            }
        }
#endif
    }
}
