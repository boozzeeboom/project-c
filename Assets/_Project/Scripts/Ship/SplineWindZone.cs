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

        [Tooltip("Развернуть направление на 180° (поток в обратную сторону сплайна).")]
        public bool reverseDirection = false;

        [Header("Центрирование (удержание в трубе)")]
        [Tooltip("Сила притяжения к центру сплайна. 0 = без центрирования, 1 = слабо, 10 = жёсткая труба.")]
        [Min(0f)]
        public float centeringStrength = 3f;

        [Header("Производительность")]
        [Tooltip("Интервал обновления кэша ShipController (сек).")]
        [Min(0.5f)]
        [SerializeField] private float _shipCacheRefreshInterval = 2f;

        [Tooltip("Шаг детекции: каждый N-й FixedUpdate (1 = каждый, 5 = ~10 Гц).")]
        [Min(1)]
        [SerializeField] private int _detectionStep = 5;

        [Header("Gizmos")]
        [Tooltip("Точек на сегмент сплайна для визуализации коридора.")]
        [Min(4)]
        [SerializeField] private int _gizmoSamplesPerSegment = 12;

        // ============================================================
        // State
        // ============================================================

        private SplineContainer _splineContainer;

        // Кэш: корабль → его сплайн-параметры (один GetNearestPoint на цикл детекции)
        private readonly Dictionary<ShipController, ShipSplineEntry> _shipEntries = new();

        // Массив всех ShipController — обновляется редко
        private ShipController[] _cachedShips = System.Array.Empty<ShipController>();
        private float _nextCacheRefresh;

        // Счётчик кадров для троттлинга
        private int _frameCounter;

        // ============================================================
        // Structs
        // ============================================================

        private struct ShipSplineEntry
        {
            public float splineT;          // параметр на сплайне
            public float distance;         // расстояние до сплайна
            public Vector3 direction;      // направление ветра вдоль сплайна (мировое)
            public Vector3 nearestPoint;   // ближайшая точка на сплайне (мировая)
        }

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

            // Троттлинг: детекция раз в _detectionStep FixedUpdate
            _frameCounter++;
            if (_frameCounter < _detectionStep)
            {
                // Между циклами детекции — всё равно применяем силу
                // по последним известным сплайн-параметрам
                ApplyWindToShipsCached();
                return;
            }

            _frameCounter = 0;

            RefreshShipCache();
            DetectShipsAndCacheSplineData();
            ApplyWindToShipsCached();
        }

        private void OnDisable()
        {
            _shipEntries.Clear();
        }

        // ============================================================
        // Cache
        // ============================================================

        private void RefreshShipCache()
        {
            if (Time.time < _nextCacheRefresh)
                return;

            _nextCacheRefresh = Time.time + _shipCacheRefreshInterval;
            _cachedShips = FindObjectsByType<ShipController>(FindObjectsSortMode.None);

            // Прогрев: при первом заполнении кэша сразу делаем детекцию
            _frameCounter = _detectionStep;
        }

        // ============================================================
        // Detection (один GetNearestPoint на корабль)
        // ============================================================

        private void DetectShipsAndCacheSplineData()
        {
            _shipEntries.Clear();

            if (_cachedShips == null || _cachedShips.Length == 0)
                return;

            var spline = _splineContainer.Spline;
            if (spline == null)
                return;

            Transform splineTransform = _splineContainer.transform;

            foreach (var ship in _cachedShips)
            {
                if (ship == null)
                    continue;

                Vector3 worldPos = ship.transform.position;
                float3 localPos = splineTransform.InverseTransformPoint(worldPos);

                // ЕДИНСТВЕННЫЙ вызов GetNearestPoint на корабль за цикл
                float distance = SplineUtility.GetNearestPoint(
                    spline,
                    localPos,
                    out float3 nearestLocal,
                    out float t
                );

                if (distance > corridorRadius)
                    continue;

                // Ближайшая точка на сплайне в мировых координатах
                Vector3 nearestWorld = splineTransform.TransformPoint(nearestLocal);

                // Определяем направление
                Vector3 direction;
                if (directionMode == SplineWindDirectionMode.AlongSpline)
                {
                    float3 localTangent = SplineUtility.EvaluateTangent(spline, t);
                    direction = splineTransform.TransformDirection(localTangent).normalized;
                }
                else
                {
                    direction = windData.windDirection.normalized;
                }

                if (reverseDirection)
                    direction = -direction;

                _shipEntries[ship] = new ShipSplineEntry
                {
                    splineT = t,
                    distance = distance,
                    direction = direction,
                    nearestPoint = nearestWorld
                };
            }
        }

        // ============================================================
        // Force Application (reuse cached spline data — zero extra lookups)
        // ============================================================

        private void ApplyWindToShipsCached()
        {
            if (_shipEntries.Count == 0)
                return;

            float forceMagnitude = ComputeForceMagnitude(Vector3.zero);

            foreach (var kv in _shipEntries)
            {
                ShipController ship = kv.Key;
                if (ship == null)
                    continue;

                ShipSplineEntry entry = kv.Value;

                // Shear: пересчитываем magnitude с учётом высоты (дёшево)
                if (windData.profile == WindProfile.Shear)
                {
                    forceMagnitude = windData.windForce + ship.transform.position.y * windData.shearGradient;
                }

                Vector3 force = entry.direction * forceMagnitude;

                // Центрирующая сила: тянет корабль к центру сплайна
                // Квадратичная кривая: 0 в центре, максимум на границе коридора
                if (centeringStrength > 0f && entry.distance > 0.01f)
                {
                    float t = entry.distance / corridorRadius;       // 0..1
                    float strength = centeringStrength * t * t;       // квадратичный рост к краю
                    Vector3 toCenter = (entry.nearestPoint - ship.transform.position).normalized;
                    force += toCenter * (strength * windData.windForce);
                }

                if (force.sqrMagnitude > 0.001f)
                {
                    ship.ApplyExternalForce(force);
                }
            }
        }

        private float ComputeForceMagnitude(Vector3 worldPosition)
        {
            switch (windData.profile)
            {
                case WindProfile.Constant:
                    return windData.windForce;

                case WindProfile.Gust:
                {
                    float gustFactor = Mathf.Sin(Time.time * (2f * Mathf.PI) / windData.gustInterval);
                    float variation = gustFactor * windData.windVariation;
                    return windData.windForce * (1f + variation);
                }

                case WindProfile.Shear:
                    return windData.windForce + worldPosition.y * windData.shearGradient;

                default:
                    return windData.windForce;
            }
        }

        // ============================================================
        // Public API
        // ============================================================

        /// <summary>
        /// Рассчитать силу ветра в заданной мировой позиции (для внешних запросов).
        /// Делает отдельный GetNearestPoint — не для частого вызова.
        /// </summary>
        public Vector3 GetWindForceAtPosition(Vector3 worldPosition)
        {
            if (windData == null)
                return Vector3.zero;

            Vector3 direction = GetWindDirection(worldPosition);
            float magnitude = ComputeForceMagnitude(worldPosition);
            return direction * magnitude;
        }

        /// <summary>
        /// Получить направление ветра в заданной точке (для внешних запросов).
        /// </summary>
        public Vector3 GetWindDirection(Vector3 worldPosition)
        {
            if (directionMode == SplineWindDirectionMode.AlongSpline)
            {
                var spline = _splineContainer.Spline;
                if (spline == null)
                    return Vector3.forward;

                float3 localPos = _splineContainer.transform.InverseTransformPoint(worldPosition);
                SplineUtility.GetNearestPoint(spline, localPos, out float3 _, out float t);
                float3 localTangent = SplineUtility.EvaluateTangent(spline, t);
                Vector3 dir = _splineContainer.transform.TransformDirection(localTangent).normalized;
                return reverseDirection ? -dir : dir;
            }

            Vector3 customDir = windData.windDirection.normalized;
            return reverseDirection ? -customDir : customDir;
        }

        /// <summary>Количество кораблей в зоне (для дебага).</summary>
        public int ShipCount => _shipEntries.Count;

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

            // Коридор: кольца вдоль сплайна
            for (int i = 0; i < totalSamples; i++)
            {
                float tNorm = i * step;
                float splineT = SplineUtility.ConvertIndexUnit(spline, tNorm, PathIndexUnit.Normalized);

                float3 localPos = SplineUtility.EvaluatePosition(spline, splineT);
                Vector3 worldPos = _splineContainer.transform.TransformPoint(localPos);

                float3 localTangent = SplineUtility.EvaluateTangent(spline, splineT);
                Vector3 worldDir = _splineContainer.transform.TransformDirection(localTangent).normalized;

                Color ringColor = new(windColor.r, windColor.g, windColor.b, 0.08f);
                DrawGizmoCircle(worldPos, worldDir, r, ringColor);

                Color outlineColor = new(windColor.r, windColor.g, windColor.b, 0.35f);
                DrawGizmoCircle(worldPos, worldDir, r, outlineColor);
            }

            // Стрелки направления
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
