// StormCellDirector.cs — Phase 2.4
// Manages storm cells for lightning VFX. Drives StormLightningVfx via events.
// Temporary data source; will be replaced by WeatherCellManager in Phase 3.3.
// Event signature Action<Vector3, float> stays the same for forward compatibility.

using System.Collections.Generic;
using UnityEngine;
using ProjectC.Core;

namespace ProjectC.World.Clouds
{
    /// <summary>
    /// Источник штормовых ячеек. Двигает ячейки по ветру,
    /// триггерит молнии по таймеру. Временный — в 3.3 заменится на WeatherCellManager.
    /// </summary>
    public class StormCellDirector : MonoBehaviour
    {
        public static StormCellDirector Instance { get; private set; }

        [Header("Cells")]
        [Range(1, 10)] public int MaxCells = 5;
        [Tooltip("Радиус влияния ячейки (м).")]
        [Range(500f, 50000f)] public float CellRadius = 5000f;
        [Tooltip("Нижняя граница столба (Y).")]
        [Range(100f, 2000f)] public float CellBottomY = 800f;
        [Tooltip("Верхняя граница столба (Y).")]
        [Range(2000f, 8000f)] public float CellTopY = 5000f;

        [Header("Lightning")]
        [Tooltip("Минимальный интервал между молниями (сек).")]
        [Range(5f, 60f)] public float LightningIntervalMin = 10f;
        [Tooltip("Максимальный интервал между молниями (сек).")]
        [Range(10f, 120f)] public float LightningIntervalMax = 30f;

        [Header("Wind")]
        [Tooltip("Множитель скорости ветра для движения ячеек.")]
        [Range(0f, 5f)] public float WindSpeedMultiplier = 1f;

        [Header("Test")]
        [Tooltip("При старте создать тестовые ячейки вокруг игрока.")]
        public bool SpawnTestCells = true;
        [Tooltip("Сколько тестовых ячеек создать.")]
        [Range(0, 5)] public int TestCellCount = 2;
        [Tooltip("Дистанция тестовых ячеек от игрока.")]
        [Range(500f, 5000f)] public float TestSpawnDistance = 1500f;
        [Tooltip("Задержка перед спавном тестовых ячеек (сек).")]
        [Range(1f, 60f)] public float TestSpawnDelay = 15f;

        [Header("Debug Visuals")]
        [Tooltip("Создать GameObject-маркеры (столбы) на позициях ячеек.")]
        public bool ShowDebugMarkers = true;
        [Tooltip("Рисовать столбы в Game View (Debug.DrawLine).")]
        public bool ShowDebugColumns = true;
        [Tooltip("Рисовать Gizmos в Scene View.")]
        public bool ShowDebugGizmos = true;
        [Tooltip("Логировать в консоль.")]
        [SerializeField] private bool _logDebug = true;

        [Header("Debug Markers")]
        [Tooltip("Ширина маркера по XZ (м).")]
        [Range(50f, 5000f)] public float MarkerWidth = 500f;
        [Tooltip("Высота маркера по Y (м). 0 = авто (CellTopY − CellBottomY).")]
        [Range(0f, 10000f)] public float MarkerHeight = 0f;
        [Tooltip("Вариативность размера (±% от заданного).")]
        [Range(0f, 0.5f)] public float MarkerSizeVariation = 0.1f;
        [Tooltip("Цвет маркера.")]
        public Color MarkerColor = new Color(1f, 0f, 1f, 0.8f);

        [Header("Storm Cloud Shader")]
        [Tooltip("Множитель плотности штормовых облаков в реймарче.")]
        [Range(0.1f, 10f)] public float StormDensityMultiplier = 1.5f;
        [Tooltip("Цвет ядра шторма (плотная область).")]
        public Color StormColorDark = new Color(0.08f, 0.06f, 0.12f, 1f);
        [Tooltip("Цвет края шторма (разреженная область).")]
        public Color StormColorLight = new Color(0.25f, 0.22f, 0.35f, 1f);
        [Tooltip("Мягкость края шторма (0=резкий, 0.5=размытый).")]
        [Range(0.01f, 0.5f)] public float StormEdgeSoftness = 0.12f;
        [Tooltip("Где пик плотности по вертикали (0=дно, 0.5=центр, 1=верх).")]
        [Range(0.1f, 0.9f)] public float StormVerticalPeak = 0.5f;
        [Tooltip("Максимум ячеек передаваемых в шейдер.")]
        [Range(1, 8)] public int MaxStormCellsInShader = 8;

        // Shader property IDs
        private static readonly int StormCellPosId      = Shader.PropertyToID("_StormCellPos");
        private static readonly int StormCellParamsId   = Shader.PropertyToID("_StormCellParams");
        private static readonly int StormCellCountId    = Shader.PropertyToID("_StormCellCount");
        private static readonly int StormDensityMultId  = Shader.PropertyToID("_StormDensityMult");
        private static readonly int StormColorDarkId    = Shader.PropertyToID("_StormColorDark");
        private static readonly int StormColorLightId   = Shader.PropertyToID("_StormColorLight");
        private static readonly int StormEdgeSoftnessId = Shader.PropertyToID("_StormEdgeSoftness");
        private static readonly int StormVerticalPeakId = Shader.PropertyToID("_StormVerticalPeak");

        // Pre-allocated caches for shader push
        private static readonly Vector4[] _stormPosCache    = new Vector4[8];
        private static readonly Vector4[] _stormParamsCache = new Vector4[8];

        private readonly List<GameObject> _debugMarkers = new();

        /// <summary>Событие: молния в worldPos с интенсивностью [0..1].</summary>
        public event System.Action<Vector3, float> OnLightningTriggered;

        // ── Внутренние данные ──

        private readonly List<StormCell> _cells = new();

        // ═══════════════════════════════════════════
        // Unity Lifecycle
        // ═══════════════════════════════════════════

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (_logDebug)
                Debug.Log($"[StormCellDirector] Awake. MaxCells={MaxCells}");
        }

        private void Start()
        {
            if (SpawnTestCells && TestCellCount > 0)
                StartCoroutine(SpawnTestCellsDelayed());
        }

        private System.Collections.IEnumerator SpawnTestCellsDelayed()
        {
            // Ждём пока сцена загрузится и игрок заспавнится
            yield return new WaitForSeconds(TestSpawnDelay);

            Vector3 playerPos = GetPlayerPosition();
            if (_logDebug)
                Debug.Log($"[StormCellDirector] Spawning test cells at player: {playerPos}");

            SpawnTestCellsAroundPosition(playerPos);
        }

        private void Update()
        {
            if (_cells.Count == 0) return;

            Vector3 windDir = Vector3.right;
            float windSpeed = 0f;
            if (WindManager.Instance != null)
            {
                windDir = WindManager.Instance.CurrentWindDirection.normalized;
                windSpeed = WindManager.Instance.CurrentWindSpeed;
            }

            float dt = Time.deltaTime;

            for (int i = 0; i < _cells.Count; i++)
            {
                var cell = _cells[i];

                // Движение по ветру
                cell.WorldPosition += windDir * windSpeed * WindSpeedMultiplier * dt;
                cell.TimeSinceLightning += dt;

                if (cell.TimeSinceLightning >= cell.NextLightningTime)
                {
                    // Триггер молнии
                    OnLightningTriggered?.Invoke(cell.WorldPosition, cell.Intensity);

                    // Сброс таймера с новой случайной задержкой
                    float baseInterval = Random.Range(LightningIntervalMin, LightningIntervalMax);
                    cell.NextLightningTime = Mathf.Max(5f, baseInterval / Mathf.Max(cell.Intensity, 0.1f));
                    cell.TimeSinceLightning = 0f;

                    if (_logDebug)
                        Debug.Log($"[StormCellDirector] ⚡ Lightning at {cell.WorldPosition} intensity={cell.Intensity:F2} nextIn={cell.NextLightningTime:F1}s");
                }

                _cells[i] = cell;
            }

            // ── GameObject-маркеры ──
            SyncDebugMarkers();

            // ── Game View debug: ЦИЛИНДРЫ (кольца + вертикали) ──
            if (ShowDebugColumns)
            {
                var playerPos = GetPlayerPosition();
                for (int i = 0; i < _cells.Count; i++)
                {
                    var c = _cells[i];
                    float r = c.Radius;
                    Vector3 center = c.WorldPosition;
                    Vector3 bot = new Vector3(center.x, CellBottomY, center.z);
                    Vector3 top = new Vector3(center.x, CellTopY, center.z);

                    // Кольца каждые 500м
                    for (float y = CellBottomY; y <= CellTopY; y += 500f)
                        DrawDebugRing(new Vector3(center.x, y, center.z), r, Color.magenta);

                    // Вертикальные рёбра цилиндра (8 шт)
                    for (int seg = 0; seg < 8; seg++)
                    {
                        float a = seg * Mathf.PI * 2f / 8f;
                        float x = Mathf.Cos(a) * r;
                        float z = Mathf.Sin(a) * r;
                        Debug.DrawLine(bot + new Vector3(x, 0, z), top + new Vector3(x, 0, z), Color.magenta, 0f, false);
                    }

                    // Крест в центре
                    Vector3 mid = new Vector3(center.x, (CellBottomY + CellTopY) * 0.5f, center.z);
                    Debug.DrawLine(mid + Vector3.left * 300f, mid + Vector3.right * 300f, Color.yellow, 0f, false);
                    Debug.DrawLine(mid + Vector3.forward * 300f, mid + Vector3.back * 300f, Color.yellow, 0f, false);

                    // Оранжевый луч от игрока
                    Debug.DrawLine(playerPos, mid, new Color(1f, 0.5f, 0f, 0.8f), 0f, false);
                }
            }

            // Push storm cell data to VolumetricClouds shader
            PushStormCellsToShader();

            // Broadcast global storm intensity
            GlobalStormEvents.BroadcastStormIntensity(GetAverageIntensity());
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;

            ClearDebugMarkers();
        }

        // ═══════════════════════════════════════════
        // Debug Markers
        // ═══════════════════════════════════════════

        private void SyncDebugMarkers()
        {
            if (!ShowDebugMarkers)
            {
                ClearDebugMarkers();
                return;
            }

            float baseW = MarkerWidth;
            float baseH = MarkerHeight > 0f ? MarkerHeight : (CellTopY - CellBottomY);
            float midY = (CellBottomY + CellTopY) * 0.5f;

            // Create/remove to match cell count
            while (_debugMarkers.Count < _cells.Count)
            {
                float variation = 1f + Random.Range(-MarkerSizeVariation, MarkerSizeVariation);

                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = $"StormCellMarker_{_debugMarkers.Count}";
                cube.transform.localScale = new Vector3(baseW * variation, baseH * variation, baseW * variation);
                cube.hideFlags = HideFlags.DontSave;
                Destroy(cube.GetComponent<Collider>());
                var mr = cube.GetComponent<MeshRenderer>();
                mr.material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                mr.material.color = MarkerColor;
                _debugMarkers.Add(cube);
            }

            while (_debugMarkers.Count > _cells.Count)
            {
                Destroy(_debugMarkers[_debugMarkers.Count - 1]);
                _debugMarkers.RemoveAt(_debugMarkers.Count - 1);
            }

            // Sync positions + scale (live-updates from inspector)
            for (int i = 0; i < _cells.Count; i++)
            {
                Vector3 pos = _cells[i].WorldPosition;
                var t = _debugMarkers[i].transform;
                t.position = new Vector3(pos.x, midY, pos.z);
                t.localScale = new Vector3(baseW, baseH, baseW);
            }
        }

        private void ClearDebugMarkers()
        {
            foreach (var m in _debugMarkers)
                if (m != null) Destroy(m);
            _debugMarkers.Clear();
        }

        // ═══════════════════════════════════════════
        // Public API
        // ═══════════════════════════════════════════

        /// <summary>
        /// Добавить ячейку шторма (будет использоваться WeatherCellManager в 3.3).
        /// </summary>
        public void AddCell(Vector3 worldPos, float radius, float intensity)
        {
            if (_cells.Count >= MaxCells)
            {
                Debug.LogWarning("[StormCellDirector] Max cells reached, removing oldest.");
                _cells.RemoveAt(0);
            }

            var cell = new StormCell
            {
                WorldPosition = worldPos,
                Radius = radius,
                Intensity = Mathf.Clamp01(intensity),
                TimeSinceLightning = 0f,
                NextLightningTime = Random.Range(LightningIntervalMin, LightningIntervalMax)
            };

            _cells.Add(cell);

            if (_logDebug)
                Debug.Log($"[StormCellDirector] Cell added at {worldPos} r={radius} intensity={intensity:F2}. Total={_cells.Count}");
        }

        /// <summary>Удалить ячейку по индексу.</summary>
        public void RemoveCell(int index)
        {
            if (index < 0 || index >= _cells.Count) return;
            _cells.RemoveAt(index);
        }

        private static void DrawDebugRing(Vector3 center, float radius, Color color)
        {
            const int segments = 24;
            float step = Mathf.PI * 2f / segments;
            for (int i = 0; i < segments; i++)
            {
                float a0 = i * step, a1 = (i + 1) * step;
                Vector3 p0 = center + new Vector3(Mathf.Cos(a0) * radius, 0f, Mathf.Sin(a0) * radius);
                Vector3 p1 = center + new Vector3(Mathf.Cos(a1) * radius, 0f, Mathf.Sin(a1) * radius);
                Debug.DrawLine(p0, p1, color, 0f, false);
            }
        }

        /// <summary>Read-only доступ к ячейкам (для дебага / 3.3).</summary>
        public IReadOnlyList<StormCell> GetCells() => _cells;

        /// <summary>Средняя интенсивность всех ячеек [0..1].</summary>
        public float GetAverageIntensity()
        {
            if (_cells.Count == 0) return 0f;
            float sum = 0f;
            for (int i = 0; i < _cells.Count; i++)
                sum += _cells[i].Intensity;
            return sum / _cells.Count;
        }

        // ═══════════════════════════════════════════
        // Helpers
        // ═══════════════════════════════════════════

        private static Vector3 GetPlayerPosition()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            return player != null ? player.transform.position : Vector3.zero;
        }

        /// <summary>
        /// Упаковывает данные ячеек в Vector4-массивы и пушит в глобальные uniform'ы шейдера.
        /// Вызывается каждый кадр из Update().
        /// </summary>
        private void PushStormCellsToShader()
        {
            int count = Mathf.Min(_cells.Count, MaxStormCellsInShader);

            for (int i = 0; i < 8; i++)
            {
                if (i < count)
                {
                    var c = _cells[i];
                    _stormPosCache[i]    = new Vector4(c.WorldPosition.x, c.WorldPosition.y, c.WorldPosition.z, c.Intensity);
                    _stormParamsCache[i] = new Vector4(c.Radius, CellBottomY, CellTopY, 0f);
                }
                else
                {
                    _stormPosCache[i]    = Vector4.zero;
                    _stormParamsCache[i] = Vector4.zero;
                }
            }

            Shader.SetGlobalVectorArray(StormCellPosId, _stormPosCache);
            Shader.SetGlobalVectorArray(StormCellParamsId, _stormParamsCache);
            Shader.SetGlobalInt(StormCellCountId, count);

            Shader.SetGlobalFloat(StormDensityMultId, StormDensityMultiplier);
            Shader.SetGlobalVector(StormColorDarkId, StormColorDark);
            Shader.SetGlobalVector(StormColorLightId, StormColorLight);
            Shader.SetGlobalFloat(StormEdgeSoftnessId, StormEdgeSoftness);
            Shader.SetGlobalFloat(StormVerticalPeakId, StormVerticalPeak);
        }

        // ═══════════════════════════════════════════
        // Test Spawn
        // ═══════════════════════════════════════════

        private void SpawnTestCellsAroundPosition(Vector3 center)
        {

            for (int i = 0; i < TestCellCount; i++)
            {
                float angle = (360f / TestCellCount) * i + Random.Range(-30f, 30f);
                float dist = TestSpawnDistance + Random.Range(-300f, 300f);
                float midY = (CellBottomY + CellTopY) * 0.5f;
                Vector3 pos = center + new Vector3(
                    Mathf.Cos(angle * Mathf.Deg2Rad) * dist,
                    midY - center.y + Random.Range(-200f, 200f),
                    Mathf.Sin(angle * Mathf.Deg2Rad) * dist
                );

                float intensity = Random.Range(0.5f, 1f);
                AddCell(pos, CellRadius, intensity);
            }

            if (_logDebug)
                Debug.Log($"[StormCellDirector] ⛈ Spawned {TestCellCount} test cells. Columns {CellBottomY}→{CellTopY}m, radius={CellRadius}m.");
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!ShowDebugGizmos || _cells == null) return;
            for (int i = 0; i < _cells.Count; i++)
            {
                var c = _cells[i];
                float r = c.Radius;
                Vector3 center = c.WorldPosition;
                Vector3 bot = new Vector3(center.x, CellBottomY, center.z);
                Vector3 top = new Vector3(center.x, CellTopY, center.z);
                Vector3 mid = new Vector3(center.x, (CellBottomY + CellTopY) * 0.5f, center.z);

                // Цепочка сфер по высоте
                Gizmos.color = new Color(1f, 0.15f, 0.7f, 0.06f);
                for (float y = CellBottomY + 200f; y < CellTopY; y += 400f)
                    Gizmos.DrawSphere(new Vector3(center.x, y, center.z), r * 0.4f);

                // Кольца
                Gizmos.color = new Color(1f, 0.3f, 0.9f, 0.3f);
                for (float y = CellBottomY; y <= CellTopY; y += 500f)
                    DrawGizmoRing(new Vector3(center.x, y, center.z), r);

                // Вертикали
                for (int seg = 0; seg < 4; seg++)
                {
                    float a = seg * Mathf.PI * 2f / 4f;
                    float x = Mathf.Cos(a) * r, z = Mathf.Sin(a) * r;
                    Gizmos.DrawLine(bot + new Vector3(x, 0, z), top + new Vector3(x, 0, z));
                }

                // Крест + подпись
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(mid + Vector3.left * 200f, mid + Vector3.right * 200f);
                Gizmos.DrawLine(mid + Vector3.forward * 200f, mid + Vector3.back * 200f);
                UnityEditor.Handles.Label(mid + Vector3.up * 50f,
                    $"CELL[{i}] R={r:F0} I={c.Intensity:F1} ⚡{c.NextLightningTime - c.TimeSinceLightning:F1}s");
            }
        }

        private static void DrawGizmoRing(Vector3 center, float radius)
        {
            const int seg = 32;
            float step = Mathf.PI * 2f / seg;
            for (int i = 0; i < seg; i++)
            {
                float a0 = i * step, a1 = (i + 1) * step;
                Vector3 p0 = center + new Vector3(Mathf.Cos(a0) * radius, 0f, Mathf.Sin(a0) * radius);
                Vector3 p1 = center + new Vector3(Mathf.Cos(a1) * radius, 0f, Mathf.Sin(a1) * radius);
                Gizmos.DrawLine(p0, p1);
            }
        }
#endif

        // ═══════════════════════════════════════════
        // Data
        // ═══════════════════════════════════════════

        [System.Serializable]
        public struct StormCell
        {
            public Vector3 WorldPosition;
            public float Radius;
            public float Intensity;
            public float TimeSinceLightning;
            public float NextLightningTime;
        }
    }
}
