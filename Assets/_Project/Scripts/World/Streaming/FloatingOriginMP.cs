using System;
using System.Collections.Generic;
using Unity.Netcode;
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

    /// <summary>
    /// Режим работы FloatingOriginMP.
    /// </summary>
    public enum OriginMode
    {
        /// <summary>Локальный сдвиг (singleplayer)</summary>
        Local,
        /// <summary>Сдвиг от сервера (multiplayer client)</summary>
        ServerSynced,
        /// <summary>Сервер инициирует сдвиг (multiplayer host/server)</summary>
        ServerAuthority
    }

    [Header("Mode")]
    [Tooltip("Режим работы: Local (singleplayer), ServerSynced (client), ServerAuthority (host/server)")]
    public OriginMode mode = OriginMode.Local;

        [Header("Position Source")]
        [Tooltip("Transform для отслеживания позиции. Null = автопоиск NetworkPlayer. КРИТИЧНО: должен быть НЕ дочерний TradeZones!")]
        public Transform positionSource;
        
        [Header("Camera Names")]
        [Tooltip("Имена камер для поиска (ThirdPersonCamera имеет приоритет над обычной Camera)")]
        public string[] cameraNames = new string[]
        {
            "ThirdPersonCamera",
            "Main Camera",
            "Camera",
            "PlayerCamera"
        };

    [Header("Threshold")]
    [Tooltip("Расстояние от origin после которого сдвигаем мир (units)")]
    public float threshold = 150000f;  // Сдвигаем когда игрок дальше 150k

    [Header("Shift Rounding")]
    [Tooltip("Округление сдвига для избежания накопления ошибок (units)")]
    public float shiftRounding = 10000f;  // Округляем до 10k для точности

        [Header("World Root Names")]
        [Tooltip("TradeZones ИСКЛЮЧЁН! TradeZones — корень сцены с камерой. Сдвигаем ТОЛЬКО WorldRoot и его children!")]
        public string[] worldRootNames = new string[]
        {
            "WorldRoot",         // Основной контейнер (СДВИГАЕТСЯ)
            "Mountains",
            "Clouds",
            "farms",
            "World",
            "ChunksContainer",
            "Platforms",
            "CloudLayer",
            "Massif",
            "Peak",
            "Farm"
            // TradeZones ИСКЛЮЧЁН — там камера, она не должна сдвигаться!
        };
        
        [Header("Exclude From WorldRoots")]
        [Tooltip("Эти объекты НИКОГДА не сдвигаются, даже если найдены в сцене.")]
        public string[] excludeFromWorldRoots = new string[]
        {
            "TradeZones",
            "TradeZone",
            "Player",
            "NetworkPlayer"
        };
        
        [Header("Exclude From Shift")]
        [Tooltip("Object names that do NOT shift (player, camera, UI). Also excludes all children of these objects.")]
        public string[] excludeFromShift = new string[]
        {
            "Player",
            "NetworkPlayer",
            "ThirdPersonCamera",
            "Main Camera",
            "Camera",
            "EventSystem",
            "NetworkManager",
            "StreamingTest",
            "NetworkManagerController"
        };

        [Header("Debug")]
        [Tooltip("Показать debug логи (ВКЛЮЧИ ДЛЯ ОТЛАДКИ!)")]
        public bool showDebugLogs = true;  // TRUE by default for debugging

        [Tooltip("Показывать debug HUD на экране")]
        public bool showDebugHUD = true;  // TRUE by default for debugging

        #endregion

        #region Private State

        private Camera _camera;
        private readonly List<Transform> _worldRoots = new List<Transform>();
        private Vector3 _totalOffset = Vector3.zero;
        private int _shiftCount = 0;
        private bool _initialized = false;
        
        /// <summary>
        /// Timestamp последнего сдвига (для защиты от спама).
        /// После ResetOrigin/ApplyWorldShift включаем cooldown чтобы LateUpdate не спамил.
        /// </summary>
        private float _lastShiftTime = -100f;
        
        /// <summary>
        /// Cooldown в секундах после сдвига — чтобы LateUpdate не добавлял новые сдвиги.
        /// </summary>
        private const float SHIFT_COOLDOWN = 0.5f;

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

        #region Position Source (Null-Safe)

        /// <summary>
        /// Получить текущую мировую позицию для проверки threshold.
        /// 
        /// ИСТОРИЯ ПРОБЛЕМ:
        /// 1. LocalClient.PlayerObject — ИНТЕРПОЛИРОВАННАЯ позиция (NGO smoothing)
        /// 2. Camera.main — камера на TradeZones (тоже сдвигается) = (0,0,0)
        /// 3. ThirdPersonCamera — близко к origin во время игры, неправильная
        /// 
        /// РЕШЕНИЕ: NetworkPlayer — правильная позиция!
        /// 
        /// Приоритет источников:
        /// 1. positionSource (явно назначенный Transform)
        /// 2. NetworkPlayer (ПРИОРИТЕТ — показывает правильную позицию!)
        /// 3. ThirdPersonCamera (fallback — может быть неправильной)
        /// 4. Camera.main (fallback — обычно (0,0,0))
        /// </summary>
        private Vector3 GetWorldPosition()
        {
            // 1. Явный источник (самый приоритетный)
            if (positionSource != null)
            {
                if (showDebugLogs && Time.frameCount % 120 == 0)
                    Debug.Log($"[FloatingOriginMP] GetWorldPosition: using positionSource={positionSource.position:F0}");
                return positionSource.position;
            }

            // 2. NetworkPlayer — ПРИОРИТЕТ! (показывает правильную позицию)
            // ВАЖНО: NetworkPlayer(Clone) рядом с origin — это НЕ настоящий игрок!
            // Настоящий игрок найден через Player tag!
            var networkPlayers = FindObjectsByType<Unity.Netcode.NetworkObject>();
            foreach (var netObj in networkPlayers)
            {
                // Ищем NetworkPlayer с IsOwner=true И позицией далеко от origin (>10000)
                // NetworkPlayer(Clone) рядом с origin (<100) — это не настоящий игрок!
                if (netObj.name.Contains("NetworkPlayer") && netObj.IsOwner)
                {
                    Vector3 pos = netObj.transform.position;
                    if (pos.magnitude > 10000) // Только если далеко от origin!
                    {
                        if (showDebugLogs)
                            Debug.Log($"[FloatingOriginMP] GetWorldPosition: using NetworkPlayer IsOwner={pos:F0}, name={netObj.name}");
                        return pos;
                    }
                    else if (showDebugLogs)
                    {
                        Debug.LogWarning($"[FloatingOriginMP] NetworkPlayer(Clone) at {pos:F0} is too close to origin - SKIPPING!");
                    }
                }
            }
            
            // 3. Объект с тегом "Player" — это настоящий игрок!
            GameObject playerByTag = GameObject.FindGameObjectWithTag("Player");
            if (playerByTag != null)
            {
                Vector3 pos = playerByTag.transform.position;
                if (showDebugLogs)
                    Debug.Log($"[FloatingOriginMP] GetWorldPosition: using Player tag={pos:F0}, name={playerByTag.name}");
                return pos;
            }
            
            // 4. ThirdPersonCamera — fallback (может быть неправильной)
            Transform thirdPersonCam = FindThirdPersonCamera();
            if (thirdPersonCam != null)
            {
                Vector3 pos = thirdPersonCam.position;
                if (showDebugLogs)
                    Debug.LogWarning($"[FloatingOriginMP] GetWorldPosition: using ThirdPersonCamera={pos:F0} (may be wrong!)");
                return pos;
            }

            // 5. Camera.main — fallback (обычно (0,0,0))
            if (Camera.main != null)
            {
                Vector3 pos = Camera.main.transform.position;
                if (showDebugLogs)
                    Debug.LogWarning($"[FloatingOriginMP] GetWorldPosition: using Camera.main={pos:F0} (WARNING: likely wrong!)");
                return pos;
            }

            // 6. Fallback - не должно происходить
            if (showDebugLogs)
            {
                Debug.LogWarning("[FloatingOriginMP] GetWorldPosition: No position source found! Using Vector3.zero");
            }
            return Vector3.zero;
        }

        #endregion

        #region Unity Lifecycle

        void Awake()
        {
            Debug.Log("[FloatingOriginMP] ============= AWOKE CALLED =============");

            // Пытаемся найти камеру на этом объекте
            _camera = GetComponent<Camera>();

            // Если камеры нет — пробуем найти Main Camera
            if (_camera == null)
            {
                _camera = Camera.main;
                if (_camera != null)
                {
                    Debug.LogWarning("[FloatingOriginMP] No Camera on this GameObject, using Camera.main");
                }
                else
                {
                    Debug.LogWarning("[FloatingOriginMP] No Camera found! Using fallback position tracking.");
                    // БУДЕМ отслеживать позицию игрока напрямую
                }
            }
            else
            {
                Debug.Log($"[FloatingOriginMP] Camera found: {_camera.name}");
            }

            FindOrCreateWorldRoots();

            Debug.Log($"[FloatingOriginMP] After FindOrCreateWorldRoots: roots={_worldRoots.Count}");

            // НЕ отключаем компонент если roots не найдены — запускаем в режиме диагностики
            _initialized = true;

            if (showDebugHUD)
            {
                Debug.Log("[FloatingOriginMP] HUD enabled");
            }

            if (_worldRoots.Count == 0)
            {
                Debug.LogError("[FloatingOriginMP] CRITICAL: No world roots found! Searching: " + string.Join(", ", worldRootNames));
            }

            Debug.Log($"[FloatingOriginMP] Initialized. threshold={threshold:N0}, roots={_worldRoots.Count}");
        }

        void LateUpdate()
        {
            // Если roots не найдены - НЕ сдвигаем!
            if (!_initialized || _worldRoots.Count == 0)
            {
                if (showDebugLogs && Time.frameCount % 60 == 0) // Каждую секунду
                {
                    Debug.LogWarning($"[FloatingOriginMP] LateUpdate skipped: initialized={_initialized}, roots={_worldRoots.Count}");
                }
                return;
            }

            // В Local режиме — работаем. В ServerSynced — пропускаем (ждём сервер).
            // ДЛЯ ОТЛАДКИ: Временно отключим ServerSynced чтобы проверить работает ли Local
            #if UNITY_EDITOR
            // РАСКОММЕНТИРУЙТЕ следющую строку для отладки Local режима:
            // if (mode == OriginMode.ServerSynced) return;
            #endif
            if (mode == OriginMode.ServerSynced) return;
            
            // ЗАЩИТА ОТ СПАМА: проверяем cooldown
            if (Time.time - _lastShiftTime < SHIFT_COOLDOWN)
            {
                return;
            }
            
            // Local и ServerAuthority режимы — вычисляем и применяем сдвиг
            Vector3 cameraWorldPos = GetWorldPosition();

            // Проверяем нужно ли сдвигать
            // ВАЖНО: threshold работает как "буфер" — мы сдвигаем когда камера уходит
            // на расстояние > threshold от мира (который уже сдвинут)
            Vector3 adjustedPos = cameraWorldPos - _totalOffset;
            float distFromOrigin = adjustedPos.magnitude;
            
            // DEBUG: Логируем каждые 60 кадров чтобы видеть что происходит
            if (showDebugLogs && Time.frameCount % 120 == 0)
            {
                Debug.Log($"[FloatingOriginMP] Debug: cameraWorldPos={cameraWorldPos:F0}, _totalOffset={_totalOffset:F0}, adjustedPos={adjustedPos:F0}, dist={distFromOrigin:F0}, threshold={threshold:F0}");
            }
            
            // threshold определяет "когда камера далеко от мира — сдвигаем мир"
            // Мы используем magnitude (общее расстояние) вместо component-wise проверки
            if (distFromOrigin > threshold)
            {
                // Вычисляем offset с округлением для избежания accumulation errors
                Vector3 offset = RoundShift(cameraWorldPos);

                // Логируем позиции WorldRoots ДО сдвига
                string rootsBefore = "";
                foreach (var root in _worldRoots)
                {
                    if (root != null) rootsBefore += $"'{root.name}'={root.position}, ";
                }

                Debug.Log($"[FloatingOriginMP] CRITICAL SHIFT: offset={offset}, cameraPos={cameraWorldPos}, roots={_worldRoots.Count}");
                Debug.Log($"[FloatingOriginMP] Roots BEFORE shift: {rootsBefore}");

                // Применяем сдвиг ко всем world roots
                ApplyShiftToAllRoots(offset);

                // ПРИМЕЧАНИЕ: Мы НЕ сдвигаем камеру/player — только мир.
                // Если камера — child Player (который тоже не сдвигается), 
                // то камера уже остаётся на месте.
                // Если камера на верхнем уровне — она тоже остаётся на месте.
                // Визуально мир "уезжает" от игрока — это ожидаемое поведение.

                // Сохраняем total offset для отладки
                _totalOffset += offset;
                _shiftCount++;

                // Запоминаем время сдвига для cooldown
                _lastShiftTime = Time.time;

                // Уведомляем подписчиков
                OnWorldShifted?.Invoke(offset);

                // ServerAuthority — рассылаем сдвиг всем клиентам
                if (mode == OriginMode.ServerAuthority && IsServer)
                {
                    BroadcastWorldShiftRpc(offset);
                }

                Debug.Log($"[FloatingOriginMP] After shift: cameraPos={_camera.transform.position}, totalOffset={_totalOffset}");
            }
        }

        #endregion
        
        #region Network RPCs (Multiplayer)

        /// <summary>
        /// RPC: Сервер → Все клиенты: сдвиг мира.
        /// Вызывается из ServerAuthority режима когда сервер сдвигает мир.
        /// </summary>
        [ClientRpc]
        private void BroadcastWorldShiftRpc(Vector3 offset, ClientRpcParams rpcParams = default)
        {
            if (mode == OriginMode.ServerSynced)
            {
                // Клиент в ServerSynced режиме — принимаем сдвиг
                ApplyWorldShift(offset);
                
                if (showDebugLogs)
                {
                    Debug.Log($"[FloatingOriginMP] Received world shift from server: offset={offset}, " +
                              $"totalOffset={_totalOffset}, shiftCount={_shiftCount}");
                }
            }
        }
        
        /// <summary>
        /// Проверить является ли текущий экземпляр сервером.
        /// </summary>
        private bool IsServer
        {
            get
            {
                return NetworkManager.Singleton != null && 
                       NetworkManager.Singleton.IsServer;
            }
        }
        
        /// <summary>
        /// Проверить является ли текущий экземпляр хостом.
        /// </summary>
        private bool IsHost
        {
            get
            {
                return NetworkManager.Singleton != null && 
                       NetworkManager.Singleton.IsHost;
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

            // ИСПРАВЛЕНО: используем GetWorldPosition() вместо _camera напрямую
            Vector3 worldPos = GetWorldPosition();
            Vector3 offset = RoundShift(worldPos);

            if (showDebugLogs)
            {
                Debug.Log($"[FloatingOriginMP] Before ResetOrigin: " +
                          $"worldPos={worldPos:F0}, totalOffset={_totalOffset:F0}, offset={offset:F0}");
            }

            ApplyShiftToAllRoots(offset);

            _totalOffset += offset;
            _shiftCount++;
            
            // Запоминаем время сдвига для cooldown
            _lastShiftTime = Time.time;

            // Уведомляем подписчиков
            OnWorldShifted?.Invoke(offset);

            // Проверяем что позиция теперь близко к origin
            Vector3 newWorldPos = GetWorldPosition();
            float distFromOrigin = newWorldPos.magnitude;

            if (showDebugLogs)
            {
                Debug.Log($"[FloatingOriginMP] After ResetOrigin: " +
                          $"newWorldPos={newWorldPos:F0}, distFromOrigin={distFromOrigin:F0}, " +
                          $"totalOffset={_totalOffset:F0}, shiftCount={_shiftCount}");
            }

            if (distFromOrigin > threshold)
            {
                Debug.LogWarning($"[FloatingOriginMP] World position still far from origin after reset! " +
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
            
            // Запоминаем время сдвига для cooldown
            _lastShiftTime = Time.time;

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
                // Пропускаем объекты из excludeFromWorldRoots
                bool isExcluded = false;
                foreach (var excludeName in excludeFromWorldRoots)
                {
                    if (rootName.Equals(excludeName, StringComparison.OrdinalIgnoreCase))
                    {
                        isExcluded = true;
                        if (showDebugLogs)
                            Debug.Log($"[FloatingOriginMP] Skipping excluded root: '{rootName}'");
                        break;
                    }
                }
                if (isExcluded) continue;
                
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

            // ИСПРАВЛЕНО: если корни не найдены — НЕ рапаренчиваем сцену.
            // CollectWorldObjects() перемещал ВСЕ сцен-объекты под "WorldRoot",
            // что ломало NetworkManager, NetworkPlayer, CharacterController и всё остальное.
            // Правильное поведение: логируем warning и отключаем компонент.
            // Для работы FloatingOriginMP добавьте в сцену GameObject'ы с именами
            // из списка worldRootNames (например, "WorldRoot"), и FloatingOriginMP найдёт их.
            if (_worldRoots.Count == 0)
            {
                Debug.LogWarning("[FloatingOriginMP] Не найдено ни одного world root по именам: " +
                    string.Join(", ", worldRootNames) + ". " +
                    "FloatingOriginMP отключён. Создайте в сцене пустой GameObject с именем 'WorldRoot' " +
                    "и поместите под него горы, облака и другие world-объекты.");
            }
        }

        /// <summary>
        /// REFACTORED (R3-003): CollectWorldObjects() удалён — больше не используется.
        /// FloatingOriginMP теперь полагается на FindOrCreateWorldRoots() который
        /// находит объекты по именам из worldRootNames[], без изменения иерархии сцены.
        /// </summary>

        /// <summary>
        /// Найти ThirdPersonCamera по именам из списка cameraNames.
        /// 
        /// ВАЖНО: Мы НЕ используем Camera.main потому что на TradeZones может быть
        /// своя камера которая ТОЖЕ сдвигается! ThirdPersonCamera — это правильная камера.
        /// </summary>
        private Transform FindThirdPersonCamera()
        {
            // Ищем по именам из списка cameraNames
            foreach (var camName in cameraNames)
            {
                GameObject camObj = GameObject.Find(camName);
                if (camObj != null)
                {
                    // Проверяем что это Transform с Camera компонентом
                    Camera cam = camObj.GetComponent<Camera>();
                    if (cam != null)
                    {
                        // Проверяем что камера НЕ является child TradeZones/WorldRoot
                        // (потому что такие камеры тоже сдвигаются!)
                        bool isUnderWorldRoot = false;
                        Transform parent = camObj.transform.parent;
                        while (parent != null)
                        {
                            foreach (var rootName in worldRootNames)
                            {
                                if (parent.name == rootName)
                                {
                                    isUnderWorldRoot = true;
                                    break;
                                }
                            }
                            if (isUnderWorldRoot) break;
                            parent = parent.parent;
                        }
                        
                        if (!isUnderWorldRoot)
                        {
                            if (showDebugLogs)
                            {
                                Debug.Log($"[FloatingOriginMP] FindThirdPersonCamera: found '{camName}' at {camObj.transform.position:F0} (NOT under world root)");
                            }
                            return camObj.transform;
                        }
                        else if (showDebugLogs)
                        {
                            Debug.Log($"[FloatingOriginMP] FindThirdPersonCamera: '{camName}' is UNDER world root - SKIPPING!");
                        }
                    }
                }
            }
            
            if (showDebugLogs)
            {
                Debug.LogWarning($"[FloatingOriginMP] FindThirdPersonCamera: No valid camera found! Tried: {string.Join(", ", cameraNames)}");
            }
            return null;
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
        /// КРИТИЧНО: Не сдвигаем TradeZones даже если он глубокий дочерний WorldRoot!
        /// </summary>
        private void ApplyShiftToAllRoots(Vector3 offset)
        {
            // СНАЧАЛА найдём ВСЕ TradeZones в сцене (где бы они ни были)
            List<Transform> allTradeZones = new List<Transform>();
            var allObjects = FindObjectsOfType<Transform>();
            foreach (var obj in allObjects)
            {
                if (obj.name == "TradeZones" || obj.name == "TradeZone")
                {
                    allTradeZones.Add(obj);
                }
            }
            
            if (showDebugLogs)
                Debug.Log($"[FloatingOriginMP] Found {allTradeZones.Count} TradeZones in scene: " + 
                          string.Join(", ", allTradeZones.ConvertAll(t => $"{t.name}@{t.position:F0}")));
            
            // Запоминаем позиции ДО сдвига
            Dictionary<Transform, Vector3> tradeZonesPositions = new Dictionary<Transform, Vector3>();
            foreach (var tz in allTradeZones)
            {
                tradeZonesPositions[tz] = tz.position;
            }
            
            // Сдвигаем world roots
            foreach (var root in _worldRoots)
            {
                if (root == null) continue;
                root.position -= offset;
            }
            
            // ВОССТАНАВЛИВАЕМ TradeZones на их оригинальные позиции
            int restored = 0;
            foreach (var tz in allTradeZones)
            {
                if (tradeZonesPositions.TryGetValue(tz, out Vector3 originalPos))
                {
                    tz.position = originalPos;
                    restored++;
                }
            }
            
            // ПРОВЕРКА: проверяем TradeZones, камеру и _camera ПОСЛЕ восстановления
            if (showDebugLogs)
            {
                Debug.Log($"[FloatingOriginMP] === POST-SHIFT CHECK ===");
                Debug.Log($"[FloatingOriginMP] TradeZones restored: {restored}/{allTradeZones.Count}");
                
                // Проверяем TradeZones
                foreach (var tz in allTradeZones)
                {
                    Debug.Log($"[FloatingOriginMP] TradeZones NOW at: {tz.position:F0}");
                    
                    // Проверяем _camera
                    if (_camera != null)
                    {
                        Debug.Log($"[FloatingOriginMP] _camera ({_camera.name}) NOW at: {_camera.transform.position:F0}");
                    }
                }
                
                // Проверяем WorldRoot
                foreach (var root in _worldRoots)
                {
                    if (root != null)
                        Debug.Log($"[FloatingOriginMP] WorldRoot NOW at: {root.position:F0}");
                }
            }
        }

        #endregion

        #region Debug HUD

        void OnGUI()
        {
            if (!showDebugHUD) return;

            // HUD в правом верхнем углу
            float boxHeight = 120 + (_worldRoots.Count * 18);
            GUILayout.BeginArea(new Rect(Screen.width - 320, 10, 310, boxHeight));
            GUILayout.BeginVertical("box");

            GUI.backgroundColor = new Color(0.1f, 0.3f, 0.6f);
            GUILayout.Label("FloatingOriginMP", GUI.skin.box);
            GUI.backgroundColor = Color.white;

            GUILayout.Label($"Offset: {_totalOffset:N0}");
            GUILayout.Label($"Shifts: {_shiftCount}");
            GUILayout.Label($"Roots: {_worldRoots.Count}");
            // ИСПРАВЛЕНО: используем GetWorldPosition() для безопасного отображения
            GUILayout.Label($"Pos: {GetWorldPosition():F0}");
            GUILayout.Label($"Init: {_initialized}");

            // Показываем warning если roots не найдены
            if (_worldRoots.Count == 0)
            {
                GUI.backgroundColor = Color.yellow;
                GUILayout.Label("WARNING: No roots found!", GUI.skin.box);
                GUI.backgroundColor = Color.white;
            }

            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        #endregion
    }
}
