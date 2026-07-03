// =====================================================================================
// ShipCargoVisual.cs — 3D визуал наполнения трюма (T-CARGO-VIS-01)
// =====================================================================================
// Клиент-сайд компонент. Подписывается на ShipTelemetryClientState.OnShipStateChanged
// и спавнит/убирает 3D ящики внутри заданного BoxCollider (grid, bottom→top).
//
// Data flow:
//   TradeWorld.OnCargoChanged → ShipController.UpdateTelemetryState()
//   → NetworkVariable<ShipTelemetryState> → NGO sync
//   → ShipTelemetryClientState.OnShipStateChanged(shipNetId)
//   → ShipCargoVisual.RefreshVisual(cargoUsed)
//
// Design: docs/Ships/cargo_system/CARGO_VIS_01_DESIGN_2026-07-02.md
// =====================================================================================

using System.Collections.Generic;
using UnityEngine;

namespace ProjectC.Ship.Cargo
{
    /// <summary>
    /// Клиент-сайд 3D визуал груза в трюме. Спавнит N ящиков внутри _spawnZone,
    /// где N = cargoUsed из ShipTelemetryState. Использует object pool для
    /// инкрементального обновления без Destroy/Instantiate на каждое изменение.
    /// </summary>
    [DefaultExecutionOrder(10)]
    public class ShipCargoVisual : MonoBehaviour
    {
        // ===========================================================
        // Inspector
        // ===========================================================

        [Header("Spawn Zone")]
        [Tooltip("BoxCollider, определяющий границы спавна ящиков. IsTrigger=true, невидим.")]
        [SerializeField] private BoxCollider _spawnZone;

        [Header("Visual Prefabs")]
        [Tooltip("Массив префабов ящиков. Выбирается случайно на каждый ящик. Минимум 1.")]
        [SerializeField] private GameObject[] _boxPrefabs;

        [Header("Box Size")]
        [Tooltip("Размер одного ящика в метрах. 0 = авто-расчёт по объёму колайдера / cargoMax.")]
        [SerializeField] private float _boxBaseSize = 0.5f;

        [Tooltip("Gap между ящиками в долях от _boxBaseSize (0..1). 0.1 = 10% зазор.")]
        [SerializeField] [Range(0f, 0.3f)] private float _boxGap = 0.1f;

        [Header("Limits")]
        [Tooltip("Максимальное количество отображаемых ящиков (perf).")]
        [SerializeField] private int _maxVisibleBoxes = 50;

        [Header("Debug")]
        [Tooltip("Включить подробные логи в консоль.")]
        [SerializeField] private bool _debugLog = false;

        [Header("Overflow")]
        [Tooltip("Показывать индикатор перегруза (красный мигающий ящик).")]
        [SerializeField] private bool _showOverflowIndicator = true;

        [Tooltip("Префаб для overflow-индикатора. null = использовать _boxPrefabs[0] с красным tint.")]
        [SerializeField] private GameObject _overflowPrefab;

        // ===========================================================
        // State
        // ===========================================================

        private ulong _shipNetId;
        private int _currentBoxCount;
        private bool _hasShipRef;
        private bool _subscribed;

        // Object pool
        private readonly List<GameObject> _spawnedBoxes = new List<GameObject>();
        private readonly Stack<GameObject> _pool = new Stack<GameObject>();

        // Overflow
        private GameObject _overflowInstance;
        private float _overflowBlinkTimer;

        // Cached
        private Transform _cachedTransform;
        private static readonly Color OverflowColor = new Color(1f, 0.15f, 0.15f, 1f);

        // ===========================================================
        // Lifecycle
        // ===========================================================

        private void Awake()
        {
            _cachedTransform = transform;
            ResolveShipNetId();
            ValidateConfig();
        }

        private void Start()
        {
            if (!_hasShipRef)
            {
                Debug.LogWarning(
                    $"[ShipCargoVisual] '{gameObject.name}': ShipRootReference not found in parents. " +
                    "Component disabled. Place this GO under a ship root with ShipRootReference.",
                    this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (!_hasShipRef) return;
            TrySubscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Update()
        {
            // Ленивая подписка: если ShipTelemetryClientState ещё не готов на момент OnEnable
            if (_hasShipRef && !_subscribed)
            {
                TrySubscribe();
            }

            // Overflow мигание
            if (_overflowInstance != null && _overflowInstance.activeSelf)
            {
                _overflowBlinkTimer += Time.deltaTime;
                float alpha = Mathf.PingPong(_overflowBlinkTimer * 4f, 1f);
                SetOverflowAlpha(alpha);
            }
        }

        private void OnDestroy()
        {
            Unsubscribe();

            // Очистка пула
            foreach (var box in _spawnedBoxes)
            {
                if (box != null) Destroy(box);
            }
            _spawnedBoxes.Clear();

            while (_pool.Count > 0)
            {
                var box = _pool.Pop();
                if (box != null) Destroy(box);
            }

            if (_overflowInstance != null)
            {
                Destroy(_overflowInstance);
                _overflowInstance = null;
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (_spawnZone == null) return;

            Gizmos.color = new Color(0.2f, 0.8f, 0.2f, 0.4f);
            Gizmos.matrix = _spawnZone.transform.localToWorldMatrix;
            Gizmos.DrawCube(_spawnZone.center, _spawnZone.size);

            Gizmos.color = new Color(0.2f, 0.8f, 0.2f, 0.8f);
            Gizmos.DrawWireCube(_spawnZone.center, _spawnZone.size);
        }
#endif

        // ===========================================================
        // Ship reference
        // ===========================================================

        private void ResolveShipNetId()
        {
            var rootRef = GetComponentInParent<ShipRootReference>();
            if (rootRef == null)
            {
                _hasShipRef = false;
                return;
            }

            var netObj = rootRef.ShipNetworkObject;
            if (netObj == null)
            {
                Debug.LogWarning(
                    $"[ShipCargoVisual] '{gameObject.name}': ShipRootReference found but " +
                    "ShipNetworkObject is null.", this);
                _hasShipRef = false;
                return;
            }

            _shipNetId = netObj.NetworkObjectId;
            _hasShipRef = true;
        }

        private void ValidateConfig()
        {
            if (_spawnZone == null)
            {
                Debug.LogWarning(
                    $"[ShipCargoVisual] '{gameObject.name}': _spawnZone (BoxCollider) not assigned. " +
                    "No boxes will spawn.", this);
            }

            if (_boxPrefabs == null || _boxPrefabs.Length == 0)
            {
                Debug.LogError(
                    $"[ShipCargoVisual] '{gameObject.name}': _boxPrefabs array is empty. " +
                    "Assign at least 1 prefab.", this);
            }
        }

        // ===========================================================
        // Subscription
        // ===========================================================

        private void TrySubscribe()
        {
            if (_subscribed) return;

            // Ленивый ре-резолв ID: на момент Awake() NGO ещё не инициализирован,
            // NetworkObjectId = 0. После StartHost() появится реальный ID.
            if (_shipNetId == 0)
            {
                ResolveShipNetId();
                if (_shipNetId == 0) return; // NGO всё ещё не готов
            }

            var clientState = Client.ShipTelemetryClientState.Instance;
            if (clientState == null) return;

            clientState.OnShipStateChanged += OnShipStateChanged;
            _subscribed = true;

            if (_debugLog)
                Debug.Log($"[ShipCargoVisual] '{name}': subscribed. shipNetId={_shipNetId}, " +
                          $"spawnZone={(_spawnZone != null)}, prefabs={(_boxPrefabs != null ? _boxPrefabs.Length : 0)}");

            var state = clientState.GetShipState(_shipNetId);
            if (state.HasValue)
            {
                int realCargoUsed = state.Value.cargoUsed;
                if (_debugLog)
                    Debug.Log($"[ShipCargoVisual] '{name}': initial cargoUsed={realCargoUsed}, cargoMax={state.Value.cargoMax}");
                RefreshVisual(realCargoUsed);
                SetOverflowVisible(realCargoUsed);
            }
            else
            {
                Debug.LogWarning($"[ShipCargoVisual] '{name}': shipNetId={_shipNetId} NOT found in telemetry cache. " +
                                 $"Tracked ships: {clientState.TrackedShipCount}");
            }
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;

            var clientState = Client.ShipTelemetryClientState.Instance;
            if (clientState != null)
            {
                clientState.OnShipStateChanged -= OnShipStateChanged;
            }
            _subscribed = false;
        }

        // ===========================================================
        // Telemetry callback
        // ===========================================================

        private void OnShipStateChanged(ulong shipNetId)
        {
            if (shipNetId != _shipNetId) return;

            var clientState = Client.ShipTelemetryClientState.Instance;
            if (clientState == null) return;

            var state = clientState.GetShipState(_shipNetId);
            if (!state.HasValue) return;

            int realCargoUsed = state.Value.cargoUsed;
            if (_debugLog)
                Debug.Log($"[ShipCargoVisual] '{name}': OnShipStateChanged cargoUsed={realCargoUsed} (current boxes={_currentBoxCount})");
            RefreshVisual(realCargoUsed);
            SetOverflowVisible(realCargoUsed);
        }

        // ===========================================================
        // Visual refresh (incremental pool)
        // ===========================================================

        /// <summary>
        /// Инкрементально обновляет количество видимых ящиков до targetCount.
        /// Лишние возвращаются в пул, недостающие спавнятся.
        /// </summary>
        public void RefreshVisual(int targetBoxCount)
        {
            int effectiveMax = Mathf.Min(_maxVisibleBoxes, GetMaxBoxesInZone());
            int clamped = Mathf.Clamp(targetBoxCount, 0, effectiveMax);

            if (clamped == _currentBoxCount) return;

            int delta = clamped - _currentBoxCount;

            if (delta > 0)
            {
                for (int i = 0; i < delta; i++)
                {
                    int index = _currentBoxCount + i;
                    SpawnBox(index, clamped);
                }
            }
            else if (delta < 0)
            {
                int removeCount = -delta;
                for (int i = 0; i < removeCount; i++)
                {
                    if (_spawnedBoxes.Count == 0) break;
                    int lastIdx = _spawnedBoxes.Count - 1;
                    ReturnToPool(_spawnedBoxes[lastIdx]);
                    _spawnedBoxes.RemoveAt(lastIdx);
                }
            }

            _currentBoxCount = clamped;
        }

        /// <summary>
        /// Показать/скрыть overflow indicator.
        /// </summary>
        public void SetOverflowVisible(int realCargoUsed)
        {
            int effectiveMax = Mathf.Min(_maxVisibleBoxes, GetMaxBoxesInZone());
            bool shouldShow = _showOverflowIndicator && realCargoUsed > effectiveMax;

            if (shouldShow && _overflowInstance == null)
            {
                SpawnOverflowIndicator();
            }

            if (_overflowInstance != null)
            {
                _overflowInstance.SetActive(shouldShow);
                if (shouldShow)
                {
                    PositionOverflowIndicator();
                }
            }
        }

        // ===========================================================
        // Spawn / Pool
        // ===========================================================

        private void SpawnBox(int index, int total)
        {
            GameObject box = GetPooledBox();
            box.transform.SetParent(_cachedTransform, false);
            box.transform.localPosition = CalculateBoxPosition(index, total);
            box.transform.localRotation = Quaternion.identity;
            box.transform.localScale = CalculateBoxScale();
            box.SetActive(true);
            _spawnedBoxes.Add(box);
        }

        private GameObject GetPooledBox()
        {
            if (_pool.Count > 0)
            {
                return _pool.Pop();
            }

            GameObject prefab = PickRandomPrefab();
            if (prefab == null)
            {
                var fallback = GameObject.CreatePrimitive(PrimitiveType.Cube);
                fallback.name = "CargoBox_Fallback";
                return fallback;
            }

            var instance = Instantiate(prefab);
            instance.name = $"CargoBox_{_spawnedBoxes.Count + _pool.Count:D3}";
            return instance;
        }

        private void ReturnToPool(GameObject box)
        {
            if (box == null) return;
            box.SetActive(false);
            box.transform.SetParent(_cachedTransform, false);
            _pool.Push(box);
        }

        private GameObject PickRandomPrefab()
        {
            if (_boxPrefabs == null || _boxPrefabs.Length == 0) return null;
            int idx = Random.Range(0, _boxPrefabs.Length);
            return _boxPrefabs[idx];
        }

        // ===========================================================
        // Grid placement
        // ===========================================================

        private Vector3 CalculateBoxPosition(int index, int total)
        {
            if (_spawnZone == null) return Vector3.zero;

            float boxSize = GetEffectiveBoxSize();

            // _spawnZone на этом же GO — используем его локальные center/size напрямую
            Vector3 zoneCenter = _spawnZone.center;
            Vector3 zoneSize = _spawnZone.size;
            Vector3 localMin = zoneCenter - zoneSize * 0.5f;

            int cols = Mathf.Max(1, Mathf.FloorToInt(zoneSize.x / boxSize));
            int rows = Mathf.Max(1, Mathf.FloorToInt(zoneSize.z / boxSize));
            int perLayer = cols * rows;

            int layer = index / perLayer;
            int remainder = index % perLayer;
            int col = remainder % cols;
            int row = remainder / cols;

            // Центрирование grid внутри зоны
            float totalWidth = cols * boxSize;
            float totalDepth = rows * boxSize;
            float offsetX = (zoneSize.x - totalWidth) * 0.5f;
            float offsetZ = (zoneSize.z - totalDepth) * 0.5f;

            float halfBox = boxSize * 0.5f;
            float x = localMin.x + offsetX + halfBox + col * boxSize;
            float y = localMin.y + halfBox + layer * boxSize;
            float z = localMin.z + offsetZ + halfBox + row * boxSize;

            return new Vector3(x, y, z);
        }

        private Vector3 CalculateBoxScale()
        {
            float s = GetEffectiveBoxSize() * (1f - _boxGap);
            return new Vector3(s, s, s);
        }

        private float GetEffectiveBoxSize()
        {
            if (_boxBaseSize > 0.001f) return _boxBaseSize;

            if (_spawnZone == null) return 0.5f;

            Vector3 zoneSize = _spawnZone.size;
            float minDim = Mathf.Min(zoneSize.x, zoneSize.y, zoneSize.z);
            float autoSize = minDim / Mathf.Max(1, Mathf.CeilToInt(Mathf.Pow(_maxVisibleBoxes, 1f / 3f)));
            return Mathf.Max(0.1f, autoSize);
        }

        /// <summary>
        /// Сколько ящиков физически помещается в _spawnZone
        /// (cols × rows × layers по XYZ).
        /// </summary>
        private int GetMaxBoxesInZone()
        {
            if (_spawnZone == null) return _maxVisibleBoxes;

            float boxSize = GetEffectiveBoxSize();
            Vector3 zoneSize = _spawnZone.size;

            int cols = Mathf.Max(1, Mathf.FloorToInt(zoneSize.x / boxSize));
            int rows = Mathf.Max(1, Mathf.FloorToInt(zoneSize.z / boxSize));
            int layers = Mathf.Max(1, Mathf.FloorToInt(zoneSize.y / boxSize));

            return cols * rows * layers;
        }

        // ===========================================================
        // Overflow
        // ===========================================================

        private void SpawnOverflowIndicator()
        {
            GameObject prefab = _overflowPrefab;
            if (prefab == null && _boxPrefabs != null && _boxPrefabs.Length > 0)
            {
                prefab = _boxPrefabs[0];
            }

            if (prefab == null)
            {
                _overflowInstance = GameObject.CreatePrimitive(PrimitiveType.Cube);
                _overflowInstance.name = "CargoOverflow";
            }
            else
            {
                _overflowInstance = Instantiate(prefab);
                _overflowInstance.name = "CargoOverflow";
            }

            _overflowInstance.transform.SetParent(_cachedTransform, false);

            var renderers = _overflowInstance.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                foreach (var mat in r.materials)
                {
                    mat.color = OverflowColor;
                }
            }

            PositionOverflowIndicator();
            _overflowInstance.SetActive(true);
        }

        private void PositionOverflowIndicator()
        {
            if (_overflowInstance == null || _spawnZone == null) return;

            Vector3 zoneCenter = _spawnZone.center;
            Vector3 zoneSize = _spawnZone.size;

            float boxSize = GetEffectiveBoxSize();
            _overflowInstance.transform.localPosition = new Vector3(
                zoneCenter.x,
                zoneCenter.y + zoneSize.y * 0.5f + boxSize * 0.6f,
                zoneCenter.z
            );

            _overflowInstance.transform.localScale = CalculateBoxScale() * 1.2f;
        }

        private void SetOverflowAlpha(float alpha)
        {
            if (_overflowInstance == null) return;

            var renderers = _overflowInstance.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                foreach (var mat in r.materials)
                {
                    Color c = mat.color;
                    c.a = alpha;
                    mat.color = c;
                }
            }
        }

        // ===========================================================
        // Public API
        // ===========================================================

        public int VisibleBoxCount => _currentBoxCount;

        public ulong ShipNetId => _shipNetId;

#if UNITY_EDITOR
        [ContextMenu("Force Refresh")]
        private void ForceRefresh()
        {
            Awake();
            if (_hasShipRef)
            {
                Unsubscribe();
                _subscribed = false;
                TrySubscribe();
            }
        }
#endif

        public void Rebuild()
        {
            while (_spawnedBoxes.Count > 0)
            {
                int lastIdx = _spawnedBoxes.Count - 1;
                ReturnToPool(_spawnedBoxes[lastIdx]);
                _spawnedBoxes.RemoveAt(lastIdx);
            }

            if (_overflowInstance != null)
            {
                _overflowInstance.SetActive(false);
            }

            _currentBoxCount = 0;

            var clientState = Client.ShipTelemetryClientState.Instance;
            if (clientState != null)
            {
                var state = clientState.GetShipState(_shipNetId);
                if (state.HasValue)
                {
                    int cargoUsed = state.Value.cargoUsed;
                    RefreshVisual(cargoUsed);
                    SetOverflowVisible(cargoUsed);
                }
            }
        }
    }
}
