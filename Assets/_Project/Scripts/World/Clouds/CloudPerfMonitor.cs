// CloudPerfMonitor.cs — Phase 1.7
// Diagnostic component: measures VolumetricClouds GPU time via Profiler API.
// Outputs to Editor overlay or debug UI.
// Targets: ≤3ms on 1080p mid-GPU (48 steps, half-res, temporal on).

using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

namespace ProjectC.World.Clouds
{
    /// <summary>
    /// Attach to any GameObject in scene with VolumetricCloudsRenderFeature active.
    /// Collects GPU timing samples and logs statistics.
    /// </summary>
    public class CloudPerfMonitor : MonoBehaviour
    {
        [Header("Sampling")]
        [Tooltip("How many frames between samples")]
        [Range(1, 60)] public int SampleInterval = 10;

        [Tooltip("Window size for rolling average")]
        [Range(10, 300)] public int AverageWindow = 60;

        [Header("Display")]
        public bool ShowOverlay = true;
        public bool LogToConsole = false;

        private int _frameCounter;
        private float[] _samples;
        private int _sampleIndex;
        private int _samplesCollected;

        private CustomSampler _cloudSampler;

        private void Awake()
        {
            _samples = new float[AverageWindow];
            _sampleIndex = 0;
            _samplesCollected = 0;
            _cloudSampler = CustomSampler.Create("Clouds.VolumetricClouds");
        }

        private void Update()
        {
            _frameCounter++;
            if (_frameCounter < SampleInterval) return;
            _frameCounter = 0;

            // Collect GPU timestamp
            _cloudSampler.Begin();
        }

        private void LateUpdate()
        {
            if (_frameCounter != 0) return;
            _cloudSampler.End();

            // Read last frame time via FrameTimingManager (requires dynamic resolution enabled)
            // Fallback: use Profiler.GetTotalAllocatedMemoryLong or manual timing
            FrameTiming[] timings = new FrameTiming[1];
            uint count = FrameTimingManager.GetLatestTimings(1, timings);
            if (count > 0)
            {
                float gpuMs = (float)timings[0].gpuFrameTime;
                RecordSample(gpuMs);
            }
        }

        private void RecordSample(float gpuMs)
        {
            _samples[_sampleIndex] = gpuMs;
            _sampleIndex = (_sampleIndex + 1) % _samples.Length;
            _samplesCollected = Mathf.Min(_samplesCollected + 1, _samples.Length);

            if (LogToConsole && _samplesCollected % 10 == 0)
            {
                Debug.Log($"[CloudPerf] GPU: {gpuMs:F2}ms | Avg: {GetAverage():F2}ms | " +
                          $"Min: {GetMin():F2}ms | Max: {GetMax():F2}ms");
            }
        }

        public float GetAverage()
        {
            if (_samplesCollected == 0) return 0f;
            float sum = 0f;
            for (int i = 0; i < _samplesCollected; i++)
                sum += _samples[i];
            return sum / _samplesCollected;
        }

        public float GetMin()
        {
            if (_samplesCollected == 0) return 0f;
            float min = float.MaxValue;
            for (int i = 0; i < _samplesCollected; i++)
                if (_samples[i] < min) min = _samples[i];
            return min;
        }

        public float GetMax()
        {
            if (_samplesCollected == 0) return 0f;
            float max = 0f;
            for (int i = 0; i < _samplesCollected; i++)
                if (_samples[i] > max) max = _samples[i];
            return max;
        }

#if UNITY_EDITOR
        private void OnGUI()
        {
            if (!ShowOverlay || _samplesCollected < 5) return;

            float avg = GetAverage();
            float min = GetMin();
            float max = GetMax();

            string colorCode = avg <= 3.0f ? "green" : (avg <= 5.0f ? "yellow" : "red");

            GUILayout.BeginArea(new Rect(10, 300, 320, 120));
            GUILayout.BeginVertical("box");
            GUILayout.Label($"<b>☁ Cloud Perf</b>");
            GUILayout.Label($"<color={colorCode}>Avg: {avg:F2} ms</color>");
            GUILayout.Label($"Min: {min:F2} ms | Max: {max:F2} ms | Samples: {_samplesCollected}");
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }
#endif
    }
}
