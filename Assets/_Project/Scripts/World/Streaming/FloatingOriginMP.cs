using System.Collections.Generic;
using UnityEngine;

namespace ProjectC.World.Streaming
{
    /// <summary>
    /// Мультиплеер-совместимый Floating Origin — предотвращает floating point jitter при больших координатах.
    ///
    /// ПРОБЛЕМА:
    /// - При координатах >100,000 units float теряет точность
    /// - Вершины мешей начинают дрожать (jitter)
    /// - Камера трясётся при движении
    ///
    /// РЕШЕНИЕ:
    /// - Когда камера уходит дальше чем threshold от origin,
    ///   сдвигаем ВСЕ world objects обратно к (0,0,0)
    /// - Камера всегда близко к origin → float точность сохраняется
    ///
    /// ОТЛИЧИЯ ОТ СТАРОГО FloatingOrigin.cs:
    /// - Сдвигает ВСЕ world roots (Mountains, Clouds, Farms, TradeZones, и т.д.), а не только "Mountains"
    /// - Подготовка к мультиплееру: ApplyWorldShift() + OnWorldShifted event
    /// - Округление сдвига до 10,000 units для избежания накопления ошибок
    ///
    /// ИСПОЛЬЗОВАНИЕ:
    /// 1. Повесить на Camera (или root object сцены)
    /// 2. Авто-найдёт все world roots по именам (Mountains, Clouds, Farms, TradeZones, World)
    /// 3. Настроить threshold (по умолчанию 100,000 units)
    ///
    /// ВАЖНО:
    /// - НЕ двигает саму камеру — двигает мир
    /// - Физика может сломаться если есть Rigidbody — нужен отдельный handling
    /// - Audio sources с 3D spatialization могут щёлкать
    /// - Для мультиплеера: сервер должен рассылать ApplyWorldShift() через RPC
    /// </summary>
    public class FloatingOriginMP : MonoBehaviour
    {
        #region Events

        /// <summary>
        /// Событие вызывается после сдвига мира. Подписчики (NetworkTransform correction,
        /// physics sync, и т.д.) могут использовать offset для коррекции своих систем.
        /// </summary>
        public static System.Action<Vector3> OnWorldShifted;

        #endregion

        #region Configuration

        [Header("Threshold")]
        [Tooltip("Расстояние от origin после которого сдвигаем мир (units)")]
        public float threshold = 100000f;

        [Header("Shift Rounding")]
        [Tooltip("Округление сдвига для избежания накопления ошибок (units)")]
        public float shiftRounding = 10000f;

        [Header("World Root Names")]
        [Tooltip("Имена world root объектов для автопоиска. Пусто = искать все.")]
        public string[] worldRootNames = new string[]
        {
            "Mountains",
            "Clouds",
            "Farms",
            "TradeZones",
            "World",
            "WorldRoot"
        };

        [Header("Debug")]
        [Tooltip("Показать debug логи")]
        public bool showDebugLogs = false;

        [Tooltip("Показывать debug HUD на экране")]
        public bool showDebugHUD = false;

        #endregion

        #region Private State

        private Camera _camera;
        private readonly List<Transform> _worldRoots = new List<Transform>();
        private Vector3 _totalOffset = Vector3.zero;
        private int _shiftCount = 0;
        private bool _initialized = false;

        #endregion

        #region Properties

        /// <summary>
        /// Текущий суммарный offset мира от начала координат.
        /// </summary>
        public Vector3 TotalOffset => _totalOffset;

        /// <summary>
        /// Количество выполненных сдвигов.
        /// </summary>
        public int ShiftCount => _shiftCount;

        /// <summary>
        /// Список найденных world root трансформов.
        /// </summary>
        public IReadOnlyList<Transform> WorldRoots => _worldRoots.AsReadOnly();

        #endregion

        #region Unity Lifecycle

        void Awake()
        {
            _camera = GetComponent<Camera>();
            if (_camera == null)
            {
                Debug.LogError("[FloatingOriginMP] Camera not found on this GameObject! Этот компонент должен быть на объекте с Camera.");
                enabled = false;
                return;
            }

            FindOrCreateWorldRoots();

            if (_worldRoots.Count == 0)
            {
                Debug.LogError("[FloatingOriginMP] Не найдено ни одного world root! Floating origin не будет работать.");
                enabled = false;
                return;
            }

            _initialized = true;

            if (showDebugLogs)
            {
                Debug.Log($"[FloatingOriginMP] Initialized. threshold={threshold:N0}, shiftRounding={shiftRounding:N0}, roots found: {_worldRoots.Count}");
                foreach (var root in _worldRoots)
                {
                    Debug.Log($"[FloatingOriginMP]   - World root: '{root.name}'");
                }
            }
        }

        void LateUpdate()
        {
            if (!_initialized || _worldRoots.Count == 0) return;

            Vector3 cameraWorldPos = _camera.transform.position;

            // Проверяем нужно ли сдвигать
            if (Mathf.Abs(cameraWorldPos.x) > threshold ||
                Mathf.Abs(cameraWorldPos.y) > threshold ||
                Mathf.Abs(cameraWorldPos.z) > threshold)
            {
                // Вычисляем offset с округлением для избежания accumulation errors
                Vector3 offset = RoundShift(cameraWorldPos);

                // Применяем сдвиг ко всем world roots
                ApplyShiftToAllRoots(offset);

                // Сохраняем total offset для отладки
                _totalOffset += offset;
                _shiftCount++;

                // Уведомляем подписчиков
                OnWorldShifted?.Invoke(offset);

                if (showDebugLogs)
                {
                    Debug.Log($"[FloatingOriginMP] Shifted world by offset={offset}, " +
                              $"cameraPos={cameraWorldPos} → newCameraPos={_camera.transform.position}, " +
                              $"totalOffset={_totalOffset}, shiftCount={_shiftCount}");
                }
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// Ручной сброс origin (например, при телепортации игрока).
        /// КРИТИЧНО: Вызывать ДО телепортации камеры, а не ПОСЛЕ.
        /// Это сдвигает мир так чтобы камера осталась близко к (0,0,0).
        /// </summary>
        public void ResetOrigin()
        {
            if (!_initialized || _worldRoots.Count == 0)
            {
                Debug.LogWarning("[FloatingOriginMP] ResetOrigin called but component is not initialized!");
                return;
            }

            Vector3 cameraPos = _camera.transform.position;
            Vector3 offset = RoundShift(cameraPos);

            if (showDebugLogs)
            {
                Debug.Log($"[FloatingOriginMP] Before ResetOrigin: " +
                          $"cameraPos={cameraPos:F0}, totalOffset={_totalOffset:F0}, offset={offset:F0}");
            }

            ApplyShiftToAllRoots(offset);

            _totalOffset += offset;
            _shiftCount++;

            // Уведомляем подписчиков
            OnWorldShifted?.Invoke(offset);

            // Проверяем что камера теперь близко к origin
            Vector3 newCameraPos = _camera.transform.position;
            float distFromOrigin = newCameraPos.magnitude;

            if (showDebugLogs)
            {
                Debug.Log($"[FloatingOriginMP] After ResetOrigin: " +
                          $"newCameraPos={newCameraPos:F0}, distFromOrigin={distFromOrigin:F0}, " +
                          $"totalOffset={_totalOffset:F0}, shiftCount={_shiftCount}");
            }

            if (distFromOrigin > threshold)
            {
                Debug.LogWarning($"[FloatingOriginMP] Camera still far from origin after reset! " +
                                 $"distFromOrigin={distFromOrigin:F0} > threshold={threshold:F0}. " +
                                 $"This may cause rendering issues.");
            }
        }

        /// <summary>
        /// Применить сдвиг мира от сервера (для мультиплеера).
        /// Вызывается через RPC когда сервер решает что мир нужно сдвинуть.
        ///
        /// ВАЖНО: Этот метод НЕ проверяет threshold — он применяет offset напрямую.
        /// Сервер должен сам решить когда вызывать этот метод.
        /// </summary>
        /// <param name="offset">Offset для применения ко всем world objects.</param>
        public void ApplyWorldShift(Vector3 offset)
        {
            if (!_initialized || _worldRoots.Count == 0)
            {
                Debug.LogWarning("[FloatingOriginMP] ApplyWorldShift called but component is not initialized!");
                return;
            }

            if (offset == Vector3.zero)
            {
                return;
            }

            ApplyShiftToAllRoots(offset);

            _totalOffset += offset;
            _shiftCount++;

            // Уведомляем подписчиков
            OnWorldShifted?.Invoke(offset);

            if (showDebugLogs)
            {
                Debug.Log($"[FloatingOriginMP] ApplyWorldShift (from server): offset={offset}, " +
                          $"totalOffset={_totalOffset}, shiftCount={_shiftCount}");
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Найти все world root объекты по именам или создать единый "WorldRoot".
        /// </summary>
        private void FindOrCreateWorldRoots()
        {
            _worldRoots.Clear();

            // Ищем по каждому имени
            foreach (var rootName in worldRootNames)
            {
                GameObject rootObj = GameObject.Find(rootName);
                if (rootObj != null)
                {
                    _worldRoots.Add(rootObj.transform);
                    if (showDebugLogs)
                    {
                        Debug.Log($"[FloatingOriginMP] Found world root: '{rootName}'");
                    }
                }
            }

            // Если ничего не нашли — создаём "WorldRoot" и перемещаем туда все world objects
            if (_worldRoots.Count == 0)
            {
                Debug.LogWarning("[FloatingOriginMP] No world roots found by names. Creating 'WorldRoot' and collecting world objects...");

                GameObject worldRootObj = new GameObject("WorldRoot");
                Transform worldRoot = worldRootObj.transform;
                _worldRoots.Add(worldRoot);

                // Собираем все объекты которые похожи на world objects
                CollectWorldObjects(worldRoot);
            }
        }

        /// <summary>
        /// Собрать world objects (горы, облака, фермы, и т.д.) и переместить под worldRoot.
        /// </summary>
        private void CollectWorldObjects(Transform worldRoot)
        {
            // Получаем все root objects в сцене
            var sceneRoots = gameObject.scene.GetRootGameObjects();

            int movedCount = 0;

            foreach (var rootObj in sceneRoots)
            {
                // Пропускаем камеру и сам WorldRoot
                if (rootObj == gameObject || rootObj == worldRoot.gameObject) continue;

                // Пропускаем UI canvas и другие не-world объекты
                if (rootObj.CompareTag("UICanvas") || rootObj.CompareTag("EventSystem")) continue;

                // Пропускаем объекты с Camera компонентом
                if (rootObj.GetComponent<Camera>() != null) continue;

                // Перемещаем под worldRoot
                rootObj.transform.SetParent(worldRoot, true);
                movedCount++;

                if (showDebugLogs)
                {
                    Debug.Log($"[FloatingOriginMP] Moved '{rootObj.name}' under WorldRoot");
                }
            }

            Debug.Log($"[FloatingOriginMP] Collected {movedCount} world objects under 'WorldRoot'");
        }

        /// <summary>
        /// Округлить offset для избежания накопления ошибок floating point.
        /// </summary>
        private Vector3 RoundShift(Vector3 position)
        {
            return new Vector3(
                Mathf.Round(position.x / shiftRounding) * shiftRounding,
                Mathf.Round(position.y / shiftRounding) * shiftRounding,
                Mathf.Round(position.z / shiftRounding) * shiftRounding
            );
        }

        /// <summary>
        /// Применить сдвиг ко всем world roots.
        /// </summary>
        private void ApplyShiftToAllRoots(Vector3 offset)
        {
            foreach (var root in _worldRoots)
            {
                if (root == null) continue;
                root.position -= offset;
            }
        }

        #endregion

        #region Debug HUD

#if UNITY_EDITOR
        void OnGUI()
        {
            if (!showDebugHUD) return;

            GUILayout.BeginArea(new Rect(10, 10, 350, 150));
            GUILayout.BeginVertical("box");

            GUILayout.Label("<b>FloatingOriginMP</b>", UnityEditor.EditorStyles.boldLabel);
            GUILayout.Label($"Total Offset: {_totalOffset:F0}");
            GUILayout.Label($"Shift Count: {_shiftCount}");
            GUILayout.Label($"World Roots: {_worldRoots.Count}");

            if (_worldRoots.Count > 0)
            {
                GUILayout.Label("Roots:", UnityEditor.EditorStyles.boldLabel);
                foreach (var root in _worldRoots)
                {
                    if (root != null)
                    {
                        GUILayout.Label($"  - {root.name}: {root.position:F0}");
                    }
                }
            }

            GUILayout.Label($"Camera Pos: {_camera.transform.position:F0}");
            GUILayout.Label($"Threshold: {threshold:N0}");

            GUILayout.EndVertical();
            GUILayout.EndArea();
        }
#endif

        #endregion
    }
}
