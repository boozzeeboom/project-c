// ShipWakeCloudCutter.cs — Phase 2.2
// Demo: ship cuts clouds via LocalDensityBuffer.SplatDensity.
// Uses singleton Instance (no inspector assignment needed).
// Speed measured via position delta (no hasChanged flag).

using UnityEngine;

namespace ProjectC.World.Clouds
{
    /// <summary>
    /// Демо-компонент: корабль «режет» локальное облачное поле.
    /// SplatDensity с отрицательным amount при движении.
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

        private Vector3 _lastPos;
        private float _timer;

        private void Start()
        {
            if (ShipTransform == null)
                ShipTransform = transform;
            _lastPos = ShipTransform.position;
        }

        private void Update()
        {
            var ld = LocalDensityBuffer.Instance;
            if (ld == null) return;

            _timer += Time.deltaTime;
            if (_timer < SplatInterval) return;
            _timer = 0f;

            Vector3 pos = ShipTransform.position;
            float dist = Vector3.Distance(pos, _lastPos);
            float speed = dist / Mathf.Max(SplatInterval, 0.001f);
            _lastPos = pos;

            if (speed < MinSpeed) return;

            ld.SplatDensity(pos, CutRadius, CutAmount);
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
