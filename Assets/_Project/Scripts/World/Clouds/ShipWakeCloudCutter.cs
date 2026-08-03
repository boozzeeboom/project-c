// ShipWakeCloudCutter.cs — Phase 2.2
// Demo: ship cuts clouds via LocalDensityBuffer.SplatDensity.
// Throttled to ~10 splats/sec to avoid compute queue overflow.

using UnityEngine;

namespace ProjectC.World.Clouds
{
    /// <summary>
    /// Демо-компонент: корабль «режет» локальное облачное поле,
    /// вызывая SplatDensity с отрицательным amount при движении.
    /// Разрез визуально зарастает за 1–2 с (релаксация LocalDensityBuffer).
    /// </summary>
    public class ShipWakeCloudCutter : MonoBehaviour
    {
        [Header("Target")]
        [Tooltip("Transform корабля (или любого движущегося объекта). Если null — использует свой transform.")]
        public Transform ShipTransform;

        [Header("Splat")]
        [Range(10f, 200f)] public float CutRadius = 30f;
        [Range(-1f, 0f)] public float CutAmount = -0.4f;
        [Range(1f, 30f)] public float MinSpeed = 5f;
        [Range(0.05f, 1f)] public float SplatInterval = 0.1f;

        [Header("References")]
        public LocalDensityBuffer LocalDensity;

        private float _timer;

        private void Start()
        {
            if (LocalDensity == null)
                LocalDensity = LocalDensityBuffer.Instance;

            if (ShipTransform == null)
                ShipTransform = transform;
        }

        private void Update()
        {
            if (LocalDensity == null) return;

            _timer += Time.deltaTime;
            if (_timer < SplatInterval) return;
            _timer = 0f;

            float speed = ShipTransform.hasChanged
                ? (ShipTransform.position - _lastPos).magnitude / Mathf.Max(SplatInterval, 0.001f)
                : 0f;

            if (speed < MinSpeed) return;

            Vector3 pos = ShipTransform.position;
            LocalDensity.SplatDensity(pos, CutRadius, CutAmount);
        }

        private Vector3 _lastPos;
        private void LateUpdate()
        {
            _lastPos = ShipTransform.position;
        }
    }
}
