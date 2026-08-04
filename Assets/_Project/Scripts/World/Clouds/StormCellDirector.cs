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
        [Range(500f, 5000f)] public float CellRadius = 2000f;
        [Tooltip("Базовая высота ячеек (Y).")]
        [Range(500f, 3000f)] public float CellAltitude = 1500f;

        [Header("Lightning")]
        [Tooltip("Минимальный интервал между молниями (сек).")]
        [Range(5f, 60f)] public float LightningIntervalMin = 10f;
        [Tooltip("Максимальный интервал между молниями (сек).")]
        [Range(10f, 120f)] public float LightningIntervalMax = 30f;

        [Header("Test")]
        [Tooltip("При старте создать тестовые ячейки вокруг камеры.")]
        public bool SpawnTestCells = true;
        [Tooltip("Сколько тестовых ячеек создать.")]
        [Range(0, 5)] public int TestCellCount = 2;
        [Tooltip("Дистанция тестовых ячеек от камеры.")]
        [Range(500f, 5000f)] public float TestSpawnDistance = 1500f;

        [Header("Debug")]
        [SerializeField] private bool _logDebug = true;

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
                SpawnTestCellsAroundCamera();
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
                cell.WorldPosition += windDir * windSpeed * dt;
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

            // ── Game View debug: ВЕРТИКАЛЬНЫЕ СТОЛБЫ 800→5000м ──
            const float colBot = 800f, colTop = 5000f;
            var playerPos = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
            for (int i = 0; i < _cells.Count; i++)
            {
                var c = _cells[i];
                Vector3 bot = new Vector3(c.WorldPosition.x, colBot, c.WorldPosition.z);
                Vector3 top = new Vector3(c.WorldPosition.x, colTop, c.WorldPosition.z);
                Vector3 mid = new Vector3(c.WorldPosition.x, (colBot + colTop) * 0.5f, c.WorldPosition.z);

                // Столб: вертикальная линия 800→5000 (ярко-розовая)
                Debug.DrawLine(bot, top, Color.magenta, 0f, false);
                // Контрастная обводка (белая, чуть смещённая — двойная линия)
                Debug.DrawLine(bot + Vector3.right * 5f, top + Vector3.right * 5f, Color.white, 0f, false);

                // Гигантский крест в середине столба (300м)
                const float cr = 300f;
                Debug.DrawLine(mid + Vector3.left * cr, mid + Vector3.right * cr, Color.yellow, 0f, false);
                Debug.DrawLine(mid + Vector3.forward * cr, mid + Vector3.back * cr, Color.yellow, 0f, false);
                Debug.DrawLine(mid + Vector3.left * cr + Vector3.forward * cr, mid + Vector3.right * cr + Vector3.back * cr, Color.yellow, 0f, false);

                // Оранжевый луч от игрока к середине столба
                Debug.DrawLine(playerPos, mid, new Color(1f, 0.5f, 0f, 0.8f), 0f, false);
            }

            // ── Периодический лог (раз в 10 сек) ──
            if (_logDebug && Time.frameCount % 600 == 0)
            {
                var sb = new System.Text.StringBuilder($"[StormCellDirector] ⛈ {_cells.Count} cells: ");
                for (int i = 0; i < _cells.Count; i++)
                    sb.Append($" [{i}]Y={_cells[i].WorldPosition.y:F0}");
                Debug.Log(sb.ToString());
            }

            // Broadcast global storm intensity
            GlobalStormEvents.BroadcastStormIntensity(GetAverageIntensity());
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
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
        // Test Spawn
        // ═══════════════════════════════════════════

        private void SpawnTestCellsAroundCamera()
        {
            Vector3 center = Camera.main != null
                ? Camera.main.transform.position
                : Vector3.zero;

            for (int i = 0; i < TestCellCount; i++)
            {
                float angle = (360f / TestCellCount) * i + Random.Range(-30f, 30f);
                float dist = TestSpawnDistance + Random.Range(-300f, 300f);
                Vector3 pos = center + new Vector3(
                    Mathf.Cos(angle * Mathf.Deg2Rad) * dist,
                    CellAltitude - center.y + Random.Range(-200f, 200f),
                    Mathf.Sin(angle * Mathf.Deg2Rad) * dist
                );

                float intensity = Random.Range(0.5f, 1f);
                AddCell(pos, CellRadius, intensity);
            }

            if (_logDebug)
                Debug.Log($"[StormCellDirector] ⛈ Spawned {TestCellCount} test cells around camera at Y≈{CellAltitude}." +
                          $" Open SceneView, look for MAGENTA crosses at Y={CellAltitude}.");
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (_cells == null) return;
            const float colBot = 800f, colTop = 5000f;
            for (int i = 0; i < _cells.Count; i++)
            {
                var c = _cells[i];
                Vector3 bot = new Vector3(c.WorldPosition.x, colBot, c.WorldPosition.z);
                Vector3 top = new Vector3(c.WorldPosition.x, colTop, c.WorldPosition.z);

                // Столб: полупрозрачная сфера по всей высоте
                for (float y = colBot + 200f; y < colTop; y += 400f)
                {
                    Gizmos.color = new Color(1f, 0.15f, 0.7f, 0.08f);
                    Gizmos.DrawSphere(new Vector3(c.WorldPosition.x, y, c.WorldPosition.z), 180f);
                }

                // Контур столба
                Gizmos.color = new Color(1f, 0.3f, 0.9f, 0.4f);
                Gizmos.DrawLine(bot, top);

                // Крест в центре
                Vector3 mid = new Vector3(c.WorldPosition.x, (colBot + colTop) * 0.5f, c.WorldPosition.z);
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(mid + Vector3.left * 200f, mid + Vector3.right * 200f);
                Gizmos.DrawLine(mid + Vector3.forward * 200f, mid + Vector3.back * 200f);

                // Подпись
                UnityEditor.Handles.Label(mid + Vector3.up * 50f,
                    $"CELL[{i}] I={c.Intensity:F1} ⚡{c.NextLightningTime - c.TimeSinceLightning:F1}s");
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
