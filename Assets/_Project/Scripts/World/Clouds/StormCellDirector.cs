// StormCellDirector.cs — Phase 2.4 (T-CLOUD39)
// Manages storm cells for lightning VFX. Drives StormLightningVfx via events.
// Pushes cell data to VolumetricClouds shader for visual storm cloud rendering.
// Supports: runtime tweaking, EditorPrefs save/load, per-spawn randomization.
// Temporary data source; will be replaced by WeatherCellManager in Phase 3.3.

using System.Collections.Generic;
using UnityEngine;
using ProjectC.Core;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ProjectC.World.Clouds
{
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
        [Range(5f, 60f)] public float LightningIntervalMin = 10f;
        [Range(10f, 120f)] public float LightningIntervalMax = 30f;

        [Header("Wind")]
        [Tooltip("Множитель скорости ветра для движения ячеек.")]
        [Range(0f, 5f)] public float WindSpeedMultiplier = 1f;

        [Header("Test")]
        [Tooltip("При старте создать тестовые ячейки вокруг игрока.")]
        public bool SpawnTestCells = true;
        [Range(0, 5)] public int TestCellCount = 2;
        [Range(500f, 5000f)] public float TestSpawnDistance = 1500f;
        [Tooltip("Задержка перед спавном тестовых ячеек (0 = мгновенно).")]
        [Range(0f, 60f)] public float TestSpawnDelay = 2f;

        [Header("Debug Visuals")]
        public bool ShowDebugMarkers = true;
        public bool ShowDebugColumns = true;
        public bool ShowDebugGizmos = true;
        [SerializeField] private bool _logDebug = true;

        [Header("Debug Markers")]
        [Range(50f, 5000f)] public float MarkerWidth = 500f;
        [Range(0f, 10000f)] public float MarkerHeight = 0f;
        [Range(0f, 0.5f)] public float MarkerSizeVariation = 0.1f;
        public Color MarkerColor = new Color(1f, 0f, 1f, 0.8f);

        [Header("Storm Cloud Shader")]
        [Tooltip("Множитель плотности штормовых облаков.")]
        [Range(0.1f, 10f)] public float StormDensityMultiplier = 2.0f;
        [Tooltip("Цвет ядра шторма (плотная область).")]
        public Color StormColorDark = new Color(0.08f, 0.06f, 0.12f, 1f);
        [Tooltip("Цвет края шторма (разреженная область).")]
        public Color StormColorLight = new Color(0.25f, 0.22f, 0.35f, 1f);
        [Tooltip("Мягкость края шторма.")]
        [Range(0.01f, 0.5f)] public float StormEdgeSoftness = 0.12f;
        [Tooltip("Пик плотности по вертикали (0=дно, 0.5=центр, 1=верх). Рандомизируется при спавне.")]
        [Range(0.1f, 0.9f)] public float StormVerticalPeak = 0.5f;
        [Tooltip("Максимум ячеек передаваемых в шейдер.")]
        [Range(1, 8)] public int MaxStormCellsInShader = 8;

        [Header("Storm Noise (Organic Shape)")]
        [Tooltip("Масштаб кластеров (м). Меньше = мельче детали. 200-800м хорошо для органики.")]
        [Range(50f, 5000f)] public float StormNoiseScale = 800f;
        [Tooltip("Сила warp-деформации (0=ровный круг, 1=рваные края).")]
        [Range(0f, 1f)] public float StormNoiseStrength = 0.6f;
        [Tooltip("Октавы шума. 1 = самые органичные кластеры. 2-3 = больше деталей но слоистость.")]
        [Range(1, 3)] public int StormNoiseOctaves = 2;
        [Tooltip("Скорость эволюции шума от ветра.")]
        [Range(0f, 0.5f)] public float StormNoiseSpeed = 0.05f;
        [Tooltip("Контраст кластеров (0.1=мыльно, 0.5=резкие дольки).")]
        [Range(0.1f, 0.5f)] public float StormClusterContrast = 0.25f;

        // Shader property IDs
        private static readonly int StormCellPosId      = Shader.PropertyToID("_StormCellPos");
        private static readonly int StormCellParamsId   = Shader.PropertyToID("_StormCellParams");
        private static readonly int StormCellCountId    = Shader.PropertyToID("_StormCellCount");
        private static readonly int StormDensityMultId  = Shader.PropertyToID("_StormDensityMult");
        private static readonly int StormColorDarkId    = Shader.PropertyToID("_StormColorDark");
        private static readonly int StormColorLightId   = Shader.PropertyToID("_StormColorLight");
        private static readonly int StormEdgeSoftnessId = Shader.PropertyToID("_StormEdgeSoftness");
        private static readonly int StormVerticalPeakId = Shader.PropertyToID("_StormVerticalPeak");
        private static readonly int StormNoiseScaleId    = Shader.PropertyToID("_StormNoiseScale");
        private static readonly int StormNoiseStrengthId = Shader.PropertyToID("_StormNoiseStrength");
        private static readonly int StormNoiseOctavesId  = Shader.PropertyToID("_StormNoiseOctaves");
        private static readonly int StormNoiseSpeedId    = Shader.PropertyToID("_StormNoiseSpeed");
        private static readonly int StormClusterContrastId = Shader.PropertyToID("_StormClusterContrast");

        private static readonly Vector4[] _stormPosCache    = new Vector4[8];
        private static readonly Vector4[] _stormParamsCache = new Vector4[8];

        private readonly List<GameObject> _debugMarkers = new();

        public event System.Action<Vector3, float> OnLightningTriggered;

        private readonly List<StormCell> _cells = new();
        private float _nextPushLogTime;

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

#if UNITY_EDITOR
            // Restore saved defaults from EditorPrefs (persists after stopping Play Mode)
            LoadFromEditorPrefs();
#endif

            // Push defaults immediately — shader has values from frame 0
            PushStormCellsToShader();

            if (_logDebug)
                Debug.Log($"[StormCellDirector] Awake. MaxCells={MaxCells}, noiseStr={StormNoiseStrength}, noiseScale={StormNoiseScale}");
        }

        private void Start()
        {
            if (SpawnTestCells && TestCellCount > 0)
            {
                if (TestSpawnDelay <= 0f)
                {
                    SpawnTestCellsAroundPosition(GetPlayerPosition());
                }
                else
                {
                    StartCoroutine(SpawnTestCellsDelayed());
                }
            }
        }

        private System.Collections.IEnumerator SpawnTestCellsDelayed()
        {
            yield return new WaitForSeconds(TestSpawnDelay);

            Vector3 playerPos = GetPlayerPosition();
            if (_logDebug)
                Debug.Log($"[StormCellDirector] Spawning {TestCellCount} test cells at player: {playerPos}");

            SpawnTestCellsAroundPosition(playerPos);
        }

        private void Update()
        {
            // Always push to shader — even with 0 cells (zeroes out arrays).
            PushStormCellsToShader();

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

                cell.WorldPosition += windDir * windSpeed * WindSpeedMultiplier * dt;
                cell.TimeSinceLightning += dt;

                if (cell.TimeSinceLightning >= cell.NextLightningTime)
                {
                    OnLightningTriggered?.Invoke(cell.WorldPosition, cell.Intensity);

                    float baseInterval = Random.Range(LightningIntervalMin, LightningIntervalMax);
                    cell.NextLightningTime = Mathf.Max(5f, baseInterval / Mathf.Max(cell.Intensity, 0.1f));
                    cell.TimeSinceLightning = 0f;

                    if (_logDebug)
                        Debug.Log($"[StormCellDirector] ⚡ Lightning at {cell.WorldPosition} intensity={cell.Intensity:F2} nextIn={cell.NextLightningTime:F1}s");
                }

                _cells[i] = cell;
            }

            SyncDebugMarkers();

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

                    for (float y = CellBottomY; y <= CellTopY; y += 500f)
                        DrawDebugRing(new Vector3(center.x, y, center.z), r, Color.magenta);

                    for (int seg = 0; seg < 8; seg++)
                    {
                        float a = seg * Mathf.PI * 2f / 8f;
                        float x = Mathf.Cos(a) * r;
                        float z = Mathf.Sin(a) * r;
                        Debug.DrawLine(bot + new Vector3(x, 0, z), top + new Vector3(x, 0, z), Color.magenta, 0f, false);
                    }

                    Vector3 mid = new Vector3(center.x, (CellBottomY + CellTopY) * 0.5f, center.z);
                    Debug.DrawLine(mid + Vector3.left * 300f, mid + Vector3.right * 300f, Color.yellow, 0f, false);
                    Debug.DrawLine(mid + Vector3.forward * 300f, mid + Vector3.back * 300f, Color.yellow, 0f, false);
                    Debug.DrawLine(playerPos, mid, new Color(1f, 0.5f, 0f, 0.8f), 0f, false);
                }
            }

            GlobalStormEvents.BroadcastStormIntensity(GetAverageIntensity());
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            ClearDebugMarkers();
        }

        // ═══════════════════════════════════════════
        // Debug Markers
        // ═══════════════════════════════════════════

        private void SyncDebugMarkers()
        {
            if (!ShowDebugMarkers) { ClearDebugMarkers(); return; }

            float baseW = MarkerWidth;
            float baseH = MarkerHeight > 0f ? MarkerHeight : (CellTopY - CellBottomY);
            float midY = (CellBottomY + CellTopY) * 0.5f;

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
            foreach (var m in _debugMarkers) if (m != null) Destroy(m);
            _debugMarkers.Clear();
        }

        // ═══════════════════════════════════════════
        // Public API
        // ═══════════════════════════════════════════

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

        public IReadOnlyList<StormCell> GetCells() => _cells;

        public float GetAverageIntensity()
        {
            if (_cells.Count == 0) return 0f;
            float sum = 0f;
            for (int i = 0; i < _cells.Count; i++) sum += _cells[i].Intensity;
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
        /// Force pushes all storm parameters to shader + optionally respawns test cells.
        /// Available as context menu and inspector button.
        /// </summary>
        [ContextMenu("Force Regenerate Storm")]
        public void ForceRegenerateStorm()
        {
            PushStormCellsToShader();

            if (Application.isPlaying && _cells.Count == 0 && SpawnTestCells && TestCellCount > 0)
            {
                SpawnTestCellsAroundPosition(GetPlayerPosition());
            }

            if (_logDebug)
                Debug.Log($"[StormCellDirector] 🔄 Force regenerate: {_cells.Count} cells pushed. " +
                    $"noiseScale={StormNoiseScale} contrast={StormClusterContrast} strength={StormNoiseStrength}");
        }

        /// <summary>
        /// Packs cell data into Vector4 arrays and pushes to shader globals.
        /// Called every frame — inspector changes visible immediately in Play Mode.
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
            Shader.SetGlobalFloat(StormNoiseScaleId, StormNoiseScale);
            Shader.SetGlobalFloat(StormNoiseStrengthId, StormNoiseStrength);
            Shader.SetGlobalInt(StormNoiseOctavesId, StormNoiseOctaves);
            Shader.SetGlobalFloat(StormNoiseSpeedId, StormNoiseSpeed);
            Shader.SetGlobalFloat(StormClusterContrastId, StormClusterContrast);

            if (_logDebug && Time.time > _nextPushLogTime)
            {
                _nextPushLogTime = Time.time + 1.0f;
                Debug.Log($"[StormCellDirector] Shader push: cells={count} " +
                    $"densMult={StormDensityMultiplier} warpStr={StormNoiseStrength} " +
                    $"noiseScale={StormNoiseScale} contrast={StormClusterContrast} octaves={StormNoiseOctaves}");
            }
        }

        // ═══════════════════════════════════════════
        // EditorPrefs Save/Load (persists runtime tweaks across play sessions)
        // ═══════════════════════════════════════════

#if UNITY_EDITOR
        private const string PrefsKeyPrefix = "StormCellDirector.";

        [ContextMenu("Save Current as Defaults")]
        public void SaveToEditorPrefs()
        {
            EditorPrefs.SetFloat(PrefsKeyPrefix + "StormDensityMultiplier", StormDensityMultiplier);
            EditorPrefs.SetFloat(PrefsKeyPrefix + "StormEdgeSoftness", StormEdgeSoftness);
            EditorPrefs.SetFloat(PrefsKeyPrefix + "StormVerticalPeak", StormVerticalPeak);
            EditorPrefs.SetFloat(PrefsKeyPrefix + "StormNoiseScale", StormNoiseScale);
            EditorPrefs.SetFloat(PrefsKeyPrefix + "StormNoiseStrength", StormNoiseStrength);
            EditorPrefs.SetInt(PrefsKeyPrefix + "StormNoiseOctaves", StormNoiseOctaves);
            EditorPrefs.SetFloat(PrefsKeyPrefix + "StormNoiseSpeed", StormNoiseSpeed);
            EditorPrefs.SetFloat(PrefsKeyPrefix + "StormClusterContrast", StormClusterContrast);
            EditorPrefs.SetFloat(PrefsKeyPrefix + "CellRadius", CellRadius);
            EditorPrefs.SetFloat(PrefsKeyPrefix + "CellBottomY", CellBottomY);
            EditorPrefs.SetFloat(PrefsKeyPrefix + "CellTopY", CellTopY);
            EditorPrefs.SetInt(PrefsKeyPrefix + "TestCellCount", TestCellCount);
            EditorPrefs.SetFloat(PrefsKeyPrefix + "TestSpawnDistance", TestSpawnDistance);
            EditorPrefs.SetFloat(PrefsKeyPrefix + "TestSpawnDelay", TestSpawnDelay);

            var dark = StormColorDark; var light = StormColorLight;
            EditorPrefs.SetFloat(PrefsKeyPrefix + "StormColorDark.r", dark.r);
            EditorPrefs.SetFloat(PrefsKeyPrefix + "StormColorDark.g", dark.g);
            EditorPrefs.SetFloat(PrefsKeyPrefix + "StormColorDark.b", dark.b);
            EditorPrefs.SetFloat(PrefsKeyPrefix + "StormColorLight.r", light.r);
            EditorPrefs.SetFloat(PrefsKeyPrefix + "StormColorLight.g", light.g);
            EditorPrefs.SetFloat(PrefsKeyPrefix + "StormColorLight.b", light.b);

            Debug.Log("[StormCellDirector] 💾 Saved current values as defaults.");
        }

        public bool LoadFromEditorPrefs()
        {
            if (!EditorPrefs.HasKey(PrefsKeyPrefix + "StormDensityMultiplier"))
                return false;

            StormDensityMultiplier = EditorPrefs.GetFloat(PrefsKeyPrefix + "StormDensityMultiplier");
            StormEdgeSoftness     = EditorPrefs.GetFloat(PrefsKeyPrefix + "StormEdgeSoftness");
            StormVerticalPeak     = EditorPrefs.GetFloat(PrefsKeyPrefix + "StormVerticalPeak");
            StormNoiseScale       = EditorPrefs.GetFloat(PrefsKeyPrefix + "StormNoiseScale");
            StormNoiseStrength    = EditorPrefs.GetFloat(PrefsKeyPrefix + "StormNoiseStrength");
            StormNoiseOctaves     = EditorPrefs.GetInt(PrefsKeyPrefix + "StormNoiseOctaves");
            StormNoiseSpeed       = EditorPrefs.GetFloat(PrefsKeyPrefix + "StormNoiseSpeed");
            StormClusterContrast  = EditorPrefs.GetFloat(PrefsKeyPrefix + "StormClusterContrast");
            CellRadius            = EditorPrefs.GetFloat(PrefsKeyPrefix + "CellRadius");
            CellBottomY           = EditorPrefs.GetFloat(PrefsKeyPrefix + "CellBottomY");
            CellTopY              = EditorPrefs.GetFloat(PrefsKeyPrefix + "CellTopY");
            TestCellCount         = EditorPrefs.GetInt(PrefsKeyPrefix + "TestCellCount");
            TestSpawnDistance     = EditorPrefs.GetFloat(PrefsKeyPrefix + "TestSpawnDistance");
            TestSpawnDelay        = EditorPrefs.GetFloat(PrefsKeyPrefix + "TestSpawnDelay");

            StormColorDark = new Color(
                EditorPrefs.GetFloat(PrefsKeyPrefix + "StormColorDark.r", 0.08f),
                EditorPrefs.GetFloat(PrefsKeyPrefix + "StormColorDark.g", 0.06f),
                EditorPrefs.GetFloat(PrefsKeyPrefix + "StormColorDark.b", 0.12f), 1f);
            StormColorLight = new Color(
                EditorPrefs.GetFloat(PrefsKeyPrefix + "StormColorLight.r", 0.25f),
                EditorPrefs.GetFloat(PrefsKeyPrefix + "StormColorLight.g", 0.22f),
                EditorPrefs.GetFloat(PrefsKeyPrefix + "StormColorLight.b", 0.35f), 1f);

            Debug.Log("[StormCellDirector] 📂 Loaded saved defaults.");
            return true;
        }
#endif

        // ═══════════════════════════════════════════
        // Test Spawn
        // ═══════════════════════════════════════════

        private void SpawnTestCellsAroundPosition(Vector3 center)
        {
            // Randomize vertical peak per spawn for varied anvil shapes
            StormVerticalPeak = Random.Range(0.3f, 0.7f);

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
                Debug.Log($"[StormCellDirector] ⛈ Spawned {TestCellCount} test cells. Columns {CellBottomY}→{CellTopY}m, radius={CellRadius}m, verticalPeak={StormVerticalPeak:F2}.");
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

                Gizmos.color = new Color(1f, 0.15f, 0.7f, 0.06f);
                for (float y = CellBottomY + 200f; y < CellTopY; y += 400f)
                    Gizmos.DrawSphere(new Vector3(center.x, y, center.z), r * 0.4f);

                Gizmos.color = new Color(1f, 0.3f, 0.9f, 0.3f);
                for (float y = CellBottomY; y <= CellTopY; y += 500f)
                    DrawGizmoRing(new Vector3(center.x, y, center.z), r);

                for (int seg = 0; seg < 4; seg++)
                {
                    float a = seg * Mathf.PI * 2f / 4f;
                    float x = Mathf.Cos(a) * r, z = Mathf.Sin(a) * r;
                    Gizmos.DrawLine(bot + new Vector3(x, 0, z), top + new Vector3(x, 0, z));
                }

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
