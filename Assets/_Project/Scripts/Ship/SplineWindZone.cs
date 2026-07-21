using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;
using System.Collections.Generic;
using ProjectC.Player;

namespace ProjectC.Ship
{
    /// <summary>
    /// Режим направления ветра в сплайновой зоне.
    /// </summary>
    public enum SplineWindDirectionMode
    {
        /// <summary>Ветер дует по касательной к сплайну (естественный «поток» вдоль коридора).</summary>
        AlongSpline,

        /// <summary>Направление из WindZoneData.windDirection (как у обычных WindZone).</summary>
        Custom
    }

    /// <summary>
    /// Сплайновая зона ветра — ветровой «коридор» вдоль SplineContainer.
    /// Работает ПАРАЛЛЕЛЬНО с существующими WindZone (триггерными), не трогает WindManager.
    ///
    /// Обнаружение кораблей: distance-based (кратчайшее расстояние до сплайна ≤ corridorRadius).
    /// Сила применяется напрямую через ShipController.ApplyExternalForce().
    ///
    /// Размещается в рабочих игровых сценах (не в BootstrapScene).
    /// Требует SplineContainer на том же GameObject.
    /// </summary>
    [RequireComponent(typeof(SplineContainer))]
    public class SplineWindZone : MonoBehaviour
    {
        // ============================================================
        // Inspector
        // ============================================================

        [Header("Данные Зоны")]
        [Tooltip("ScriptableObject с параметрами ветра (тот же WindZoneData, что у обычных WindZone).")]
        public WindZoneData windData;

        [Header("Сплайновый Коридор")]
        [Tooltip("Радиус коридора вокруг сплайна (м). Корабли на расстоянии ≤ radius получают силу ветра.")]
        [Min(1f)]
        public float corridorRadius = 50f;

        [Tooltip("Как определяется направление ветра.")]
        public SplineWindDirectionMode directionMode = SplineWindDirectionMode.AlongSpline;

        [Header("Производительность")]
        [Tooltip("Интервал обновления кэша ShipController (сек).")]
        [Min(0.1f)]
        [SerializeField] private float _shipCacheRefreshInterval = 1f;

        [Header("Gizmos")]
        [Tooltip("Точек на сегмент сплайна для визуализации коридора.")]
        [Min(4)]
        [SerializeField] private int _gizmoSamplesPerSegment = 20;

        // ============================================================
        // State
        // ============================================================

        private SplineContainer _splineContainer;
        private HashSet<ShipController> _shipsInZone = new();
        private ShipController[] _cachedShips = System.Array.Empty<ShipController>();
        private float _nextCacheRefresh;

        // ============================================================
        // Unity Lifecycle
        // ============================================================

        private void Awake()
        {
            _splineContainer = GetComponent<SplineContainer>();
        }

        private void FixedUpdate()
        {
            if (windData == null)
                return;

            RefreshShipCache();
            DetectShips();
            ApplyWindToShips();
        }

        // ============================================================
        // Detection
        // ============================================================

        private void RefreshShipCache()
        {
            if (Time.time < _nextCacheRefresh)
                return;

            _nextCacheRefresh = Time.time + _shipCacheRefreshInterval;
            _cachedShips = FindObjectsByType<ShipController>(FindObjectsSortMode.None);
        }

        private void DetectShips()
        {
            _shipsInZone.Clear();

            if (_cachedShips == null || _cachedShips.Length == 0)
                return;

            var spline = _splineContainer.Spline;
            if (spline == null)
                return;

            foreach (var ship in _cachedShips)
            {
                if (ship == null)
                    continue;

                // Переводим мировую позицию корабля в локальное пространство сплайна
                Vector3 worldPos = ship.transform.position;
                float3 localPos = _splineContainer.transform.InverseTransformPoint(worldPos);

                float distance = SplineUtility.GetNearestPoint(
                    spline,
                    localPos,
                    out float3 _,
                    out float _
                );

                if (distance <= corridorRadius)
                {
                    _shipsInZone.Add(ship);
                }
            }
        }

        // ============================================================
        // Wind Force
        // ============================================================

        private void ApplyWindToShips()
        {
            foreach (var ship in _shipsInZone)
            {
                if (ship == null)
                    continue;

                Vector3 force = GetWindForceAtPosition(ship.transform.position);
                if (force.sqrMagnitude > 0.001f)
                {
                    // ApplyExternalForce уже проверяет IsServer внутри — безопасно.
                    ship.ApplyExternalForce(force);
                }
            }
        }

        /// <summary>
        /// Рассчитать силу ветра в заданной мировой позиции.
        /// Возвращает Vector3 силы (ньютоны), готовый для AddForce.
        /// </summary>
        public Vector3 GetWindForceAtPosition(Vector3 worldPosition)
        {
            if (windData == null)
                return Vector3.zero;

            Vector3 direction = GetWindDirection(worldPosition);
            float forceMagnitude;

            switch (windData.profile)
            {
                case WindProfile.Constant:
                    forceMagnitude = windData.windForce;
                    break;

                case WindProfile.Gust:
                {
                    float gustFactor = Mathf.Sin(Time.time * (2f * Mathf.PI) / windData.gustInterval);
                    float variation = gustFactor * windData.windVariation;
                    forceMagnitude = windData.windForce * (1f + variation);
                    break;
                }

                case WindProfile.Shear:
                {
                    float shearBoost = worldPosition.y * windData.shearGradient;
                    forceMagnitude = windData.windForce + shearBoost;
                    break;
                }

                default:
                    forceMagnitude = windData.windForce;
                    break;
            }

            return direction * forceMagnitude;
        }

        /// <summary>
        /// Получить направление ветра в заданной точке.
        /// </summary>
        public Vector3 GetWindDirection(Vector3 worldPosition)
        {
            if (directionMode == SplineWindDirectionMode.AlongSpline)
            {
                var spline = _splineContainer.Spline;
                if (spline == null)
                    return Vector3.forward;

                float3 localPos = _splineContainer.transform.InverseTransformPoint(worldPosition);

                SplineUtility.GetNearestPoint(
                    spline,
                    localPos,
                    out float3 _,
                    out float t
                );

                // Касательная в локальном пространстве → мировое направление
                float3 localTangent = SplineUtility.EvaluateTangent(spline, t);
                return _splineContainer.transform.TransformDirection(localTangent).normalized;
            }

            // Custom mode — используем направление из WindZoneData
            return windData.windDirection.normalized;
        }

        // ============================================================
        // Public API
        // ============================================================

        /// <summary>Количество кораблей в зоне (для дебага).</summary>
        public int ShipCount => _shipsInZone.Count;

        /// <summary>Живой список кораблей в зоне (read-only для внешнего использования).</summary>
        public IReadOnlyCollection<ShipController> ShipsInZone => _shipsInZone;

        // ============================================================
        // Gizmos
        // ============================================================

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (_splineContainer == null)
                _splineContainer = GetComponent<SplineContainer>();
            if (_splineContainer == null || _splineContainer.Spline == null)
                return;

            var spline = _splineContainer.Spline;

            // Цвет по силе: синий → красный
            Color windColor;
            if (windData == null)
            {
                windColor = Color.gray;
            }
            else
            {
                float t = Mathf.InverseLerp(0f, 200f, windData.windForce);
                windColor = Color.Lerp(Color.blue, Color.red, t);
            }

            float r = corridorRadius;
            int knotCount = spline.Count;
            if (knotCount < 2)
                return;

            int totalSamples = (knotCount - 1) * _gizmoSamplesPerSegment + 1;
            float step = 1f / (totalSamples - 1);

            // Рисуем коридор как серию окружностей вдоль сплайна
            for (int i = 0; i < totalSamples; i++)
            {
                float tNorm = i * step;
                float splineT = SplineUtility.ConvertIndexUnit(spline, tNorm, PathIndexUnit.Normalized);

                float3 localPos = SplineUtility.EvaluatePosition(spline, splineT);
                Vector3 worldPos = _splineContainer.transform.TransformPoint(localPos);

                float3 localTangent = SplineUtility.EvaluateTangent(spline, splineT);
                Vector3 worldDir = _splineContainer.transform.TransformDirection(localTangent).normalized;

                // Полупрозрачная окружность (диск)
                Color ringColor = new(windColor.r, windColor.g, windColor.b, 0.08f);
                DrawGizmoCircle(worldPos, worldDir, r, ringColor);

                // Контур
                Color outlineColor = new(windColor.r, windColor.g, windColor.b, 0.35f);
                DrawGizmoCircle(worldPos, worldDir, r, outlineColor);
            }

            // Стрелки направления (каждые N колец)
            int arrowInterval = Mathf.Max(1, _gizmoSamplesPerSegment / 2);
            for (int i = 0; i < totalSamples; i += arrowInterval)
            {
                float tNorm = i * step;
                float splineT = SplineUtility.ConvertIndexUnit(spline, tNorm, PathIndexUnit.Normalized);

                float3 localPos = SplineUtility.EvaluatePosition(spline, splineT);
                Vector3 worldPos = _splineContainer.transform.TransformPoint(localPos);

                float3 localTangent = SplineUtility.EvaluateTangent(spline, splineT);
                Vector3 worldDir = _splineContainer.transform.TransformDirection(localTangent).normalized;

                float arrowLen = Mathf.Clamp((windData != null ? windData.windForce : 20f) * 0.3f, 2f, 15f);
                Vector3 arrowStart = worldPos;
                Vector3 arrowEnd = arrowStart + worldDir * arrowLen;

                Gizmos.color = windColor;
                Gizmos.DrawLine(arrowStart, arrowEnd);

                // Наконечник
                Vector3 right = Vector3.Cross(Vector3.up, worldDir).normalized;
                if (right.sqrMagnitude < 0.01f)
                    right = Vector3.Cross(Vector3.right, worldDir).normalized;
                Vector3 up = Vector3.Cross(worldDir, right).normalized;

                float headSize = arrowLen * 0.35f;
                Gizmos.DrawLine(arrowEnd, arrowEnd - worldDir * headSize + right * headSize * 0.5f);
                Gizmos.DrawLine(arrowEnd, arrowEnd - worldDir * headSize - right * headSize * 0.5f);
                Gizmos.DrawLine(arrowEnd, arrowEnd - worldDir * headSize + up * headSize * 0.5f);
                Gizmos.DrawLine(arrowEnd, arrowEnd - worldDir * headSize - up * headSize * 0.5f);
            }

            // Подпись
            if (windData != null)
            {
                float3 firstPos = SplineUtility.EvaluatePosition(spline,
                    SplineUtility.ConvertIndexUnit(spline, 0f, PathIndexUnit.Normalized));
                Vector3 labelPos = _splineContainer.transform.TransformPoint(firstPos);
                UnityEditor.Handles.Label(
                    labelPos + Vector3.up * (r + 2f),
                    $"{windData.displayName}  [{windData.profile}]  ({windData.windForce:F0}N)"
                );
            }
        }

        private static void DrawGizmoCircle(Vector3 center, Vector3 normal, float radius, Color color)
        {
            Gizmos.color = color;

            Vector3 right, up;
            if (Mathf.Abs(Vector3.Dot(normal, Vector3.up)) > 0.999f)
            {
                right = Vector3.right;
                up = Vector3.forward;
            }
            else
            {
                right = Vector3.Cross(Vector3.up, normal).normalized;
                up = Vector3.Cross(normal, right).normalized;
            }

            int segments = 32;
            float angleStep = 360f / segments;

            Vector3 prevPoint = center + right * radius;
            for (int i = 1; i <= segments; i++)
            {
                float angle = i * angleStep * Mathf.Deg2Rad;
                Vector3 point = center + (right * Mathf.Cos(angle) + up * Mathf.Sin(angle)) * radius;
                Gizmos.DrawLine(prevPoint, point);
                prevPoint = point;
            }
        }
#endif
    }
}
