using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;
using System.Collections.Generic;
using ProjectC.Player;
using ProjectC.Ship;

namespace ProjectC.Core
{
    /// <summary>
    /// Central wind manager — single source of truth for wind direction/speed.
    /// Receives wind updates from server via ServerWeatherController.
    /// All cloud systems (NearCloudRenderer, DistantCloudManager, StormController) read from here.
    ///
    /// Также управляет SplineWindZone — централизованный round-robin процессинг
    /// вместо FixedUpdate на каждой зоне.
    /// </summary>
    public class WindManager : MonoBehaviour
    {
        public static WindManager Instance { get; private set; }

        [Header("Current Wind State")]
        public Vector3 CurrentWindDirection = Vector3.right;
        public float CurrentWindSpeed = 0f;

        [Header("Interpolation")]
        [SerializeField] private float _interpolationSpeed = 0.5f;

        [Header("Debug")]
        [SerializeField] private bool _logWindChanges = true;

        [Header("Влияние на геймплей (множители)")]
        [Tooltip("Глобальный множитель силы ветра, действующей на корабли (ShipController). 1 = как задано на корабле, 0 = ветер не влияет на корабли.")]
        [SerializeField] private float _shipWindMultiplier = 1f;
        [Tooltip("Глобальный множитель сноса ветром для персонажей (NetworkPlayer). 1 = базовый снос, 0 = персонажей ветром не сносит.")]
        [SerializeField] private float _characterWindMultiplier = 1f;

        /// <summary>Глобальный множитель влияния ветра на корабли (настраивается в инспекторе WindManager).</summary>
        public float ShipWindMultiplier => _shipWindMultiplier;
        /// <summary>Глобальный множитель влияния ветра на персонажей (настраивается в инспекторе WindManager).</summary>
        public float CharacterWindMultiplier => _characterWindMultiplier;

        // ============================================================
        // Spline Wind Zone Processing
        // ============================================================

        [Header("Сплайновые Ветровые Коридоры")]
        [Tooltip("Сколько зон детектить за один FixedUpdate (1 = round-robin).")]
        [Min(1)]
        [SerializeField] private int _splineZonesPerFrame = 1;

        [Tooltip("Шаг детекции: зона пересчитывается раз в N FixedUpdate (5 = ~10 Гц).")]
        [Min(1)]
        [SerializeField] private int _splineDetectionStep = 5;

        // Состояние каждой зоны (кэш кораблей + счётчик троттлинга)
        private readonly Dictionary<SplineWindZone, ZoneRuntimeState> _zoneStates = new();

        // Снапшот кораблей (переиспользуемый массив)
        private ShipController[] _shipSnapshot = System.Array.Empty<ShipController>();

        // Round-robin индекс
        private int _nextZoneIndex;

        private class ZoneRuntimeState
        {
            public Dictionary<ShipController, SplineWindZone.ShipSplineEntry> entries;
            public int frameCounter;       // счётчик для троттлинга детекции
        }



        // ============================================================
        // Interpolation State
        // ============================================================

        private Vector3 _targetDirection;
        private float _targetSpeed;
        private Vector3 _lastLoggedDirection;
        private float _lastLoggedSpeed;

        public event System.Action<Vector3, float> OnWindUpdated;

        private Vector3 _lastDirBeforeEvent = Vector3.right;
        private float _lastSpeedBeforeEvent = 0f;

        // ============================================================
        // Unity Lifecycle
        // ============================================================

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            _targetDirection = CurrentWindDirection;
            _targetSpeed = CurrentWindSpeed;
            _lastLoggedDirection = CurrentWindDirection;
            _lastLoggedSpeed = CurrentWindSpeed;
        }

        private void Update()
        {
            using var _ = ProjectCPerfCounters.WindUpdate.Auto();
            if (!float.IsNaN(_targetSpeed) && !float.IsInfinity(_targetSpeed))
            {
                CurrentWindSpeed = Mathf.Lerp(CurrentWindSpeed, _targetSpeed, _interpolationSpeed * Time.deltaTime);
            }

            if (!float.IsNaN(_targetDirection.x) && !float.IsInfinity(_targetDirection.x))
            {
                CurrentWindDirection = Vector3.Lerp(CurrentWindDirection, _targetDirection, _interpolationSpeed * Time.deltaTime);
            }

            if (CurrentWindSpeed < 1f)
                CurrentWindSpeed = 1f;

            if (_logWindChanges &&
                (CurrentWindDirection != _lastLoggedDirection || Mathf.Abs(CurrentWindSpeed - _lastLoggedSpeed) > 0.5f))
            {
                Debug.Log($"[WindManager] Wind: dir={CurrentWindDirection.normalized}, speed={CurrentWindSpeed:F1}");
                _lastLoggedDirection = CurrentWindDirection;
                _lastLoggedSpeed = CurrentWindSpeed;
            }

            if (OnWindUpdated != null &&
                (CurrentWindDirection != _lastDirBeforeEvent || Mathf.Abs(CurrentWindSpeed - _lastSpeedBeforeEvent) > 0.1f))
            {
                _lastDirBeforeEvent = CurrentWindDirection;
                _lastSpeedBeforeEvent = CurrentWindSpeed;
                OnWindUpdated(CurrentWindDirection, CurrentWindSpeed);
            }
        }

        private void FixedUpdate()
        {
            ProcessSplineWindZones();
        }

        // ============================================================
        // Public API
        // ============================================================

        /// <summary>
        /// Called by ServerWeatherController via ClientRpc
        /// </summary>
        public void ApplyWindUpdate(Vector3 direction, float speed)
        {
            if (float.IsNaN(speed) || float.IsInfinity(speed))
            {
                Debug.LogWarning("[WindManager] Rejected NaN/Infinity wind speed from server");
                return;
            }

            speed = Mathf.Clamp(speed, 0f, 100f);

            _targetDirection = direction.normalized;
            _targetSpeed = speed;
        }

        // ============================================================
        // Spline Wind Zone: Centralised Round-Robin Processing
        // ============================================================

        private void ProcessSplineWindZones()
        {
            var zones = SplineWindZone.AllZones;
            int zoneCount = zones.Count;
            if (zoneCount == 0)
                return;

            var ships = SplineWindZone.AllShips;
            int shipCount = ships.Count;
            if (shipCount == 0)
            {
                foreach (var kv in _zoneStates)
                    kv.Value.entries.Clear();
                return;
            }

            // Всегда применяем силы из кэша — дёшево
            ApplyAllCachedForces(zones);

            // Round-robin + per-zone throttling: детекция раз в _splineDetectionStep
            int processed = 0;
            int attempts = 0;
            while (processed < _splineZonesPerFrame && attempts < zoneCount)
            {
                attempts++;
                _nextZoneIndex = (_nextZoneIndex + 1) % zoneCount;
                var zone = zones[_nextZoneIndex];
                if (zone == null || zone.windData == null || zone.SplineContainer == null || zone.SplineContainer.Spline == null)
                    continue;

                // Per-zone throttling
                if (!_zoneStates.TryGetValue(zone, out var state))
                {
                    state = new ZoneRuntimeState
                    {
                        entries = new Dictionary<ShipController, SplineWindZone.ShipSplineEntry>(),
                        frameCounter = 0
                    };
                    _zoneStates[zone] = state;
                }

                state.frameCounter++;
                if (state.frameCounter < _splineDetectionStep)
                    continue;  // skip — ещё не пора детектить

                state.frameCounter = 0;

                // Снапшот кораблей только когда нужна детекция
                if (_shipSnapshot.Length < shipCount)
                    _shipSnapshot = new ShipController[shipCount];
                lock (ships)
                {
                    ships.CopyTo(_shipSnapshot);
                }

                DetectShipsInZone(zone, shipCount, ref state);
                processed++;
            }
        }


        private void DetectShipsInZone(SplineWindZone zone, int shipCount, ref ZoneRuntimeState state)
        {
            state.entries.Clear();


            var spline = zone.SplineContainer.Spline;
            Transform splineTransform = zone.SplineContainer.transform;
            float radius = zone.corridorRadius;
            var windData = zone.windData;
            var directionMode = zone.directionMode;
            bool reverse = zone.reverseDirection;

            for (int i = 0; i < shipCount; i++)
            {
                var ship = _shipSnapshot[i];
                if (ship == null)
                    continue;

                Vector3 worldPos = ship.transform.position;
                float3 localPos = splineTransform.InverseTransformPoint(worldPos);

                float distance = SplineUtility.GetNearestPoint(
                    spline,
                    localPos,
                    out float3 nearestLocal,
                    out float t
                );

                if (distance > radius)
                    continue;

                Vector3 nearestWorld = splineTransform.TransformPoint(nearestLocal);

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

                if (reverse)
                    direction = -direction;

                state.entries[ship] = new SplineWindZone.ShipSplineEntry
                {
                    splineT = t,
                    distance = distance,
                    direction = direction,
                    nearestPoint = nearestWorld
                };

                // HUD: имя зоны
                SplineWindZone.SetZoneDisplayName(ship, windData.displayName);
            }
        }

        private void ApplyAllCachedForces(System.Collections.Generic.List<SplineWindZone> zones)
        {
            foreach (var zone in zones)
            {
                if (zone == null || zone.windData == null)
                    continue;

                if (!_zoneStates.TryGetValue(zone, out var state) || state.entries.Count == 0)
                    continue;

                var windData = zone.windData;
                float forceMagnitude = zone.ComputeForceMagnitude(Vector3.zero);
                float radius = zone.corridorRadius;
                float centering = zone.centeringStrength;

                foreach (var kv in state.entries)
                {
                    var ship = kv.Key;
                    if (ship == null)
                        continue;

                    var entry = kv.Value;

                    // Shear: пересчёт с высотой
                    float magnitude = forceMagnitude;
                    if (windData.profile == WindProfile.Shear)
                    {
                        magnitude = windData.windForce + ship.transform.position.y * windData.shearGradient;
                    }

                    Vector3 force = entry.direction * magnitude;

                    // Центрирующая сила
                    if (centering > 0f && entry.distance > 0.01f)
                    {
                        float edgeT = entry.distance / radius;
                        float strength = centering * edgeT * edgeT;
                        Vector3 toCenter = (entry.nearestPoint - ship.transform.position).normalized;
                        force += toCenter * (strength * windData.windForce);
                    }

                    if (force.sqrMagnitude > 0.001f)
                    {
                        ship.ApplyExternalForce(force);
                    }
                }
            }
        }
    }
}
