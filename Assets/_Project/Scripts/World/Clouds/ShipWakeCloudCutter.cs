// ShipWakeCloudCutter.cs — Phase 2.2 (T-CLOUD02)
// Demo: ship cuts clouds via LocalDensityBuffer.SplatDensity.
// Uses singleton Instance (no inspector assignment needed).
// Speed measured via position delta (no hasChanged flag).
// Wake cone: series of splats BEHIND the ship along movement direction,
// radius grows with distance → clouds part in a cone behind the ship.

using UnityEngine;

namespace ProjectC.World.Clouds
{
    /// <summary>
    /// Демо-компонент: корабль «режет» локальное облачное поле.
    /// Кильватерный конус позади корабля: N сплатов вдоль -dir движения,
    /// радиус каждого следующего больше → облака расходятся за кормой конусом.
    /// Разрез визуально зарастает за 1–2 с (релаксация).
    /// </summary>
    public class ShipWakeCloudCutter : MonoBehaviour
    {
        [Header("Target")]
        [Tooltip("Transform корабля. Если null — использует свой transform.")]
        public Transform ShipTransform;

        [Header("Splat")]
        [Tooltip("Positive = mark area for cloud cutting. Buffer is max(0, val), so negative values are silently clamped to 0.")]
        [Range(10f, 200f)] public float CutRadius = 30f;
        [Range(0f, 1f)] public float CutAmount = 0.4f;
        [Range(1f, 30f)] public float MinSpeed = 5f;
        [Range(0.05f, 1f)] public float SplatInterval = 0.1f;

        [Header("Wake Cone")]
        [Tooltip("Сколько сплатов укладывается позади корабля (длина конуса).")]
        [Range(2, 16)] public int ConeSegments = 8;
        [Tooltip("Шаг между сплатами конуса, в долях CutRadius.")]
        [Range(0.3f, 3f)] public float ConeSpacing = 0.5f;
        [Tooltip("Рост радиуса на каждый сегмент конуса (в долях CutRadius).")]
        [Range(0f, 1f)] public float ConeRadiusGrowth = 0.25f;

        private Vector3 _lastPos;
        private float _timer;

        private void Start()
        {
            if (ShipTransform == null)
                ShipTransform = transform;
            _lastPos = ShipTransform.position;

            // Point LocalDensityBuffer window at the ship (not camera)
            var ld = LocalDensityBuffer.Instance;
            if (ld != null && ld.FollowTarget == null)
            {
                ld.FollowTarget = ShipTransform;
                Debug.Log($"[ShipWakeCloudCutter] LocalDensityBuffer.FollowTarget → {ShipTransform.name}");
            }
        }

        private void Update()
        {
            var ld = LocalDensityBuffer.Instance;
            if (ld == null) return;

            _timer += Time.deltaTime;
            if (_timer < SplatInterval) return;
            _timer = 0f;

            Vector3 pos = ShipTransform.position;
            Vector3 delta = pos - _lastPos;
            float speed = delta.magnitude / Mathf.Max(SplatInterval, 0.001f);
            _lastPos = pos;

            if (speed < MinSpeed) return;
            if (delta.sqrMagnitude < 0.0001f) return;

            Vector3 dir = delta.normalized;

            // Wake cone: splats BEHIND the ship (opposite to movement direction)
            int count = Mathf.Max(2, ConeSegments);
            float step = CutRadius * Mathf.Max(0.1f, ConeSpacing);
            for (int i = 1; i <= count; i++)
            {
                Vector3 p = pos - dir * (step * i);
                float r = CutRadius * (1f + ConeRadiusGrowth * (i - 1));
                ld.SplatDensity(p, r, CutAmount);
            }
        }

        // Debug: press Space to test-splat
        private void OnDrawGizmosSelected()
        {
            if (Application.isPlaying && UnityEngine.Input.GetKey(KeyCode.Space))
            {
                var ld = LocalDensityBuffer.Instance;
                if (ld != null)
                {
                    ld.SplatDensity(transform.position, CutRadius, CutAmount);
                    Debug.Log($"[ShipWakeCloudCutter] Test splat at {transform.position}, r={CutRadius}, amount={CutAmount}");
                }
            }
        }
    }
}
