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
        
        /// <summary>
        /// Ссылка на единственный экземпляр (синглтон).
        /// </summary>
        private static FloatingOriginMP _instance;
        public static FloatingOriginMP Instance => _instance;

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
        
        /// <summary>
        /// ITERATION 3.1 FIX: Кэшированная позиция игрока для предотвращения oscillation.
        /// Вместо FindObjectsByType используем кэшированную позицию которую обновляет NetworkPlayer.
        /// </summary>
        private Vector3 _cachedPlayerPosition = Vector3.zero;
        private bool _hasCachedPlayerPosition = false;
        private Transform _cachedPlayerTransform;

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
        
        /// <summary>
        /// ПРИНУДИТЕЛЬНО обнулить totalOffset (для ResetOrigin после телепорта).
        /// </summary>
        public void ResetOffset()
        {
            Debug.Log($"[FloatingOriginMP] ResetOffset: was totalOffset={_totalOffset}");
            _totalOffset = Vector3.zero;
            Debug.Log($"[FloatingOriginMP] ResetOffset: now totalOffset={_totalOffset}");
        }

        #endregion

        #region Zone Responsibility Methods (FIX I1-002)

        /// <summary>
        /// FIX (I1-002): Определяет должен ли использоваться FloatingOrigin.
        /// 
        /// ЗОНЫ ОТВЕТСТВЕННОСТИ:
        /// - < threshold * 0.5: ChunkLoader управляет (local coordinates)
        /// - > threshold: FloatingOrigin управляет (world shift)
        /// 
        /// Используется другими системами (например, ChunkLoader) для определения
        /// когда нужно передать управление FloatingOriginMP.
        /// </summary>
        public bool ShouldUseFloatingOrigin()
        {
            if (positionSource == null)
            {
                // Fallback: ищем любую позицию игрока
                Vector3 pos = GetWorldPosition();
                return pos.magnitude > threshold;
            }
            
            return positionSource.position.magnitude > threshold;
        }

        /// <summary>
        /// FIX (I1-003): Проверяет что positionSource теперь близко к origin
        /// после сдвига мира. Используется для определения что сдвиг сработал корректно.
        /// </summary>
        public bool IsNearOrigin()
        {
            Vector3 pos = GetWorldPosition();
            return pos.magnitude < threshold * 0.5f;
        }

        #endregion

        #region Synchronization Events (FIX I1-003)

        /// <summary>
        /// FIX (I1-003): Событие вызывается КОГДА Floating Origin НАЧИНАЕТ сдвиг мира.
        /// Подписчики (например, ChunkLoader) могут приостановить свою работу во время сдвига.
        /// </summary>
        public System.Action<Vector3> OnFloatingOriginTriggered;

        /// <summary>
        /// FIX (I1-003): Событие вызывается КОГДА Floating Origin ЗАКАНЧИВАЕТ сдвиг мира.
        /// После этого подписчики (например, ChunkLoader) могут продолжить работу.
        /// </summary>
        public System.Action OnFloatingOriginCleared;

        /// <summary>
        /// FIX (I1-003): Проверяет активно ли сейчас Floating Origin (мир сдвинут).
        /// </summary>
        public bool IsFloatingOriginActive => _totalOffset.magnitude > 100f;

        #endregion

        #region Position Caching (ITERATION 3.1 FIX)
        
        /// <summary>
        /// ITERATION 3.1 FIX: Обновить кэшированную позицию игрока.
        /// Вызывается из NetworkPlayer.UpdatePlayerChunkTracker() на СЕРВЕРЕ.
        /// Это заменяет ненадёжный FindObjectsByType который мог выбрать неправильный объект.
        /// 
        /// ПРИМЕЧАНИЕ: Этот метод должен вызываться каждый кадр или с нужным интервалом.
        /// </summary>
        /// <param name="playerPosition">Текущая позиция игрока (transform.position)</param>
        /// <param name="playerTransform">Transform игрока (для валидации)</param>
        public void UpdateCachedPlayerPosition(Vector3 playerPosition, Transform playerTransform)
        {
            _cachedPlayerPosition = playerPosition;
            _cachedPlayerTransform = playerTransform;
            _hasCachedPlayerPosition = true;
        }

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
        /// 
        /// ITERATION 3 FIX v2: Сделан public для использования в NetworkPlayer.UpdatePlayerChunkTracker()
        /// </summary>
        public Vector3 GetWorldPosition()
        {
            // ITERATION 3.5 FIX: В ServerAuthority режиме НЕ используем кэш — он устаревает после сдвига
            // positionSource уже содержит правильную позицию без необходимости кэширования
            if (mode != OriginMode.ServerAuthority)
            {
                // 0. ITERATION 3.1 FIX: Используем кэшированную позицию если доступна
                // Это самый надёжный источник — передаётся напрямую из NetworkPlayer
                if (_hasCachedPlayerPosition && _cachedPlayerTransform != null)
                {
                    // Проверяем что Transform всё ещё валиден
                    if (_cachedPlayerTransform != null)
                    {
                        return _cachedPlayerPosition;
                    }
                    else
                    {
                        _hasCachedPlayerPosition = false;
                    }
                }
            }

            // 1. Явный источник (самый приоритетный)
            // FIX (I1-001 REVISED): Проверка близости к origin
            // Если positionSource близко к origin (< threshold * 0.5), 
            // значит он уже локальный и НЕ включает _totalOffset
            // 
            // ITERATION 3.6 FIX: В режиме ServerAuthority positionSource.position УЖЕ
            // смещён (через ApplyServerShift), поэтому используем positionSource.position
            // напрямую как "истинную" позицию без вычитания _totalOffset.
            // В режиме Local/ServerSynced вычитаем _totalOffset.
            if (positionSource != null)
            {
                if (mode == OriginMode.ServerAuthority)
                {
                    // ServerAuthority: positionSource уже смещён, используем напрямую
                    if (showDebugLogs && Time.frameCount % 600 == 0)
                        Debug.Log($"[FloatingOriginMP] GetWorldPosition: ServerAuthority, using positionSource.position={positionSource.position:F0}");
                    return positionSource.position;
                }
                
                // Local/ServerSynced: positionSource может содержать накопленное смещение
                float distToOrigin = positionSource.position.magnitude;
                if (distToOrigin < threshold * 0.5f)
                {
                    // Близко к origin — используем позицию напрямую
                    // QUIET: только раз в 10 секунд при showDebugLogs
                    if (showDebugLogs && Time.frameCount % 600 == 0)
                        Debug.Log($"[FloatingOriginMP] GetWorldPosition: close to origin ({distToOrigin:F0}), using raw position");
                    return positionSource.position;
                }
                
                // Далеко от origin — вычитаем накопленный offset
                Vector3 truePos = positionSource.position - _totalOffset;
                if (showDebugLogs && Time.frameCount % 600 == 0)
                    Debug.Log($"[FloatingOriginMP] GetWorldPosition: far from origin ({distToOrigin:F0}), truePos={truePos:F0}");
                return truePos;
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
                    
                    // ДОПОЛНИТЕЛЬНАЯ ПРОВЕРКА: если позиция > 500000 — проверяем тег!
                    // Если это НАСТОЯЩИЙ игрок (тег "Player") — используем его позицию!
                    // Только для объектов БЕЗ тега "Player" пропускаем большие позиции
                    if (pos.magnitude > 500000 && !netObj.CompareTag("Player"))
                    {
                        Debug.LogWarning($"[FloatingOriginMP] GetWorldPosition: Object at {pos:F0} (>{500000}) is too far - SKIPPING! This looks like WorldRoot!");
                        continue;
                    }
                    
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
            // СИНГЛТОН: уничтожаем дубликаты
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning($"[FloatingOriginMP] DUPLICATE INSTANCE on '{gameObject.name}'! Destroying. Existing instance on '{_instance.gameObject.name}'");
                Destroy(this);
                return;
            }
            _instance = this;
            
            Debug.Log("[FloatingOriginMP] ============= AWOKE CALLED =============");
            
            // Проверяем: если мы на TradeZones — логируем!
            Transform parent = transform.parent;
            if (parent != null)
            {
                Debug.Log($"[FloatingOriginMP] Parent of FloatingOriginMP: '{parent.name}'");
            }
            else
            {
                Debug.Log("[FloatingOriginMP] FloatingOriginMP has NO parent (root level)");
            }

            // Пытаемся найти камеру на этом объекте
            _camera = GetComponent<Camera>();
            
            // Если камеры нет — пробуем найти Main Camera
            if (_camera == null)
            {
                _camera = Camera.main;
                if (_camera != null)
                {
                    Debug.LogWarning($"[FloatingOriginMP] No Camera on this GameObject, using Camera.main ({_camera.name})");
                    Debug.LogWarning($"[FloatingOriginMP] WARNING: Camera.main is under parent: {_camera.transform.parent?.name ?? "NONE"}");
                }
                else
                {
                    Debug.LogWarning("[FloatingOriginMP] No Camera found! Using fallback position tracking.");
                    // БУДЕМ отслеживать позицию игрока напрямую
                }
            }
            else
            {
                Debug.Log($"[FloatingOriginMP] Camera found: {_camera.name} at {_camera.transform.position:F0}");
                Debug.Log($"[FloatingOriginMP] Camera parent: {_camera.transform.parent?.name ?? "NONE"}");
            }

            FindOrCreateWorldRoots();

            Debug.Log($"[FloatingOriginMP] After FindOrCreateWorldRoots: roots={_worldRoots.Count}");
            
            // Проверяем TradeZones позицию
            GameObject tz = GameObject.Find("TradeZones");
            if (tz != null)
            {
                Debug.Log($"[FloatingOriginMP] TradeZones at: {tz.transform.position:F0}");
            }

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
            // ITERATION 3.7 FIX: Полностью пропускаем LateUpdate в ServerAuthority режиме!
            // В ServerAuthority управление сдвигом осуществляется из:
            // 1. PlayerChunkTracker.FixedUpdate() — вызывает OnWorldShifted event
            // 2. FloatingOriginMP.RequestWorldShiftRpc() — серверный RPC
            // LateUpdate НЕ должен проверять threshold и вызывать ApplyServerShift(),
            // потому что positionSource (камера на TradeZones) НЕ сдвигается и остаётся
            // на (150000, 500, 150000), что вызывает бесконечный цикл сдвигов!
            if (mode == OriginMode.ServerAuthority)
            {
                return;
            }
            
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
            
            // Вычисляем позицию для проверки threshold
            Vector3 cameraWorldPos = GetWorldPosition();
            float distFromOrigin = cameraWorldPos.magnitude;
            
            // DEBUG: Логируем только при реальном сдвиге или раз в 10 секунд
            if (showDebugLogs && Time.frameCount % 600 == 0)
            {
                Debug.Log($"[FloatingOriginMP] Debug: mode={mode}, cameraWorldPos={cameraWorldPos:F0}, dist={distFromOrigin:F0}, threshold={threshold:F0}");
            }
            
            // Проверяем нужно ли сдвигать
            if (distFromOrigin > threshold)
            {
                if (mode == OriginMode.ServerAuthority)
                {
                    // ServerAuthority: отправляем RPC серверу (сервер применит сдвиг и разошлёт всем)
                    if (IsServer)
                    {
                        // Мы на сервере — применяем сдвиг напрямую
                        ApplyServerShift(cameraWorldPos);
                    }
                    else
                    {
                        // Мы на клиенте — отправляем запрос серверу
                        if (showDebugLogs)
                        {
                            Debug.Log($"[FloatingOriginMP] LateUpdate: CLIENT sending RequestWorldShiftRpc, cameraWorldPos={cameraWorldPos:F0}");
                        }
                        RequestWorldShiftRpc(cameraWorldPos);
                    }
                    
                    // Cooldown чтобы не спамить RPC
                    _lastShiftTime = Time.time;
                }
                else if (mode == OriginMode.Local)
                {
                    // Local: применяем сдвиг локально
                    ApplyLocalShift(cameraWorldPos);
                }
                // ServerSynced: ждём сдвига от сервера через RPC
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
            // ЗАЩИТА ОТ LOOP: если это ServerAuthority — НЕ принимаем свой же RPC
            // RPC приходит обратно на сервер, и ApplyWorldShift вызовет OnWorldShifted снова
            if (mode == OriginMode.ServerAuthority)
            {
                Debug.Log($"[FloatingOriginMP] BroadcastWorldShiftRpc: ServerAuthority mode - IGNORING RPC (would cause loop!)");
                return;
            }
            
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

        /// <summary>
        /// RPC: Клиент → Сервер: запрос на сдвиг мира.
        /// Сервер проверяет threshold, применяет сдвиг и рассылает всем клиентам.
        /// </summary>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void RequestWorldShiftRpc(Vector3 cameraPos, ServerRpcParams rpcParams = default)
        {
            // Только сервер может обрабатывать запрос
            if (!IsServer)
            {
                Debug.LogWarning("[FloatingOriginMP] RequestWorldShiftRpc: called on client - ignoring!");
                return;
            }
            
            // Проверяем cooldown
            if (Time.time - _lastShiftTime < SHIFT_COOLDOWN)
            {
                Debug.Log($"[FloatingOriginMP] RequestWorldShiftRpc: cooldown active, ignoring");
                return;
            }
            
            // Проверяем threshold
            float dist = cameraPos.magnitude;
            if (dist <= threshold)
            {
                Debug.Log($"[FloatingOriginMP] RequestWorldShiftRpc: dist={dist:F0} <= threshold={threshold:F0}, ignoring");
                return;
            }
            
            // Вычисляем offset с округлением
            Vector3 offset = RoundShift(cameraPos);
            
            if (showDebugLogs)
            {
                Debug.Log($"[FloatingOriginMP] RequestWorldShiftRpc: SERVER processing - cameraPos={cameraPos:F0}, offset={offset:F0}");
            }
            
            // Применяем сдвиг на сервере
            ApplyShiftToAllRoots(offset);
            
            // Обновляем total offset
            _totalOffset += offset;
            _shiftCount++;
            
            // Запоминаем время сдвига для cooldown
            _lastShiftTime = Time.time;
            
            // Уведомляем подписчиков на сервере
            OnWorldShifted?.Invoke(offset);
            
            // Рассылаем сдвиг всем клиентам (включая отправителя)
            BroadcastWorldShiftRpc(offset);
            
            if (showDebugLogs)
            {
                Debug.Log($"[FloatingOriginMP] RequestWorldShiftRpc: BroadcastWorldShiftRpc sent to all clients");
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

            // ITERATION 3.5 FIX: Сбросить кэш позиции
            _hasCachedPlayerPosition = false;
            _cachedPlayerPosition = Vector3.zero;
            _cachedPlayerTransform = null;

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

            // ITERATION 3.5 FIX: Сбросить кэш позиции после сдвига мира
            // Кэш устаревает потому что мир сдвинулся, а позиция игрока в кэше старая
            _hasCachedPlayerPosition = false;
            _cachedPlayerPosition = Vector3.zero;
            _cachedPlayerTransform = null;

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

        #region Shift Application Methods

        /// <summary>
        /// Применить сдвиг на СЕРВЕРЕ (ServerAuthority режим).
        /// Вызывается из LateUpdate когда IsServer = true.
        /// </summary>
        private void ApplyServerShift(Vector3 cameraWorldPos)
        {
            // ITERATION 3.5 FIX: Сбросить кэш позиции
            _hasCachedPlayerPosition = false;
            _cachedPlayerPosition = Vector3.zero;
            _cachedPlayerTransform = null;

            // Вычисляем offset с округлением для избежания accumulation errors
            Vector3 offset = RoundShift(cameraWorldPos);

            if (showDebugLogs)
            {
                // Логируем позиции WorldRoots ДО сдвига
                string rootsBefore = "";
                foreach (var root in _worldRoots)
                {
                    if (root != null) rootsBefore += $"'{root.name}'={root.position}, ";
                }
                Debug.Log($"[FloatingOriginMP] SERVER SHIFT: offset={offset}, cameraPos={cameraWorldPos}");
                Debug.Log($"[FloatingOriginMP] Roots BEFORE shift: {rootsBefore}");
            }

            // Применяем сдвиг ко всем world roots
            ApplyShiftToAllRoots(offset);

            // Сохраняем total offset
            _totalOffset += offset;
            _shiftCount++;

            // Запоминаем время сдвига для cooldown
            _lastShiftTime = Time.time;

            // Уведомляем подписчиков
            OnWorldShifted?.Invoke(offset);

            // Рассылаем сдвиг всем клиентам
            BroadcastWorldShiftRpc(offset);

            if (showDebugLogs)
            {
                Debug.Log($"[FloatingOriginMP] SERVER SHIFT complete: totalOffset={_totalOffset}");
            }
        }

        /// <summary>
        /// Применить сдвиг ЛОКАЛЬНО (Local режим).
        /// Вызывается из LateUpdate.
        /// </summary>
        private void ApplyLocalShift(Vector3 cameraWorldPos)
        {
            // ITERATION 3.5 FIX: Сбросить кэш позиции
            _hasCachedPlayerPosition = false;
            _cachedPlayerPosition = Vector3.zero;
            _cachedPlayerTransform = null;

            // Вычисляем offset с округлением для избежания accumulation errors
            Vector3 offset = RoundShift(cameraWorldPos);

            if (showDebugLogs)
            {
                // Логируем позиции WorldRoots ДО сдвига
                string rootsBefore = "";
                foreach (var root in _worldRoots)
                {
                    if (root != null) rootsBefore += $"'{root.name}'={root.position}, ";
                }
                Debug.Log($"[FloatingOriginMP] LOCAL SHIFT: offset={offset}, cameraPos={cameraWorldPos}");
                Debug.Log($"[FloatingOriginMP] Roots BEFORE shift: {rootsBefore}");
            }

            // Применяем сдвиг ко всем world roots
            ApplyShiftToAllRoots(offset);

            // Сохраняем total offset
            _totalOffset += offset;
            _shiftCount++;

            // Запоминаем время сдвига для cooldown
            _lastShiftTime = Time.time;

            // Уведомляем подписчиков
            OnWorldShifted?.Invoke(offset);

            if (showDebugLogs)
            {
                Debug.Log($"[FloatingOriginMP] LOCAL SHIFT complete: totalOffset={_totalOffset}");
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
        /// Проверить, является ли transform дочерним WorldRoot.
        /// Если да — его позиция уже включает сдвиг и не подходит для определения позиции игрока.
        /// </summary>
        private bool IsUnderWorldRoot(Transform transform)
        {
            if (transform == null) return false;
            
            Transform parent = transform.parent;
            while (parent != null)
            {
                foreach (var rootName in worldRootNames)
                {
                    if (parent.name == rootName)
                    {
                        return true;
                    }
                }
                parent = parent.parent;
            }
            return false;
        }

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
            var allObjects = FindObjectsByType<Transform>();
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

        // QUIET: счётчик для ограничения логов в OnGUI
        private int _lastGuiFrame = -1;
        
        void OnGUI()
        {
            if (!showDebugHUD) return;
            
            // Ограничиваем логирование до 1 раза в 30 кадров
            if (Time.frameCount - _lastGuiFrame < 30) return;
            _lastGuiFrame = Time.frameCount;

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
