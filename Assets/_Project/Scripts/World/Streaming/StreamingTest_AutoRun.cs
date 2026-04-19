using Unity.Netcode;
using UnityEngine;
using ProjectC.World.Streaming;

namespace ProjectC.World
{
    /// <summary>
    /// Автоматически запускаемый тестовый компонент для World Streaming системы.
    /// Используется в Play Mode для проверки работы стриминга.
    /// 
    /// ВАЖНО: Этот компонент автоматически добавляется на камеру при старте.
    /// Не нужно добавлять его вручную!
    /// 
    /// Управление:
    /// - F5: Телепортироваться к следующей точке
    /// - F6: Телепортироваться к предыдущей точке
    /// - F7: Загрузить чанки вокруг текущей позиции
    /// - F8: Сбросить FloatingOrigin
    /// - F9: Toggle Grid визуализация (ЧЕРЕЗ МЕНЮ Tools)
    /// - F10: Показать/скрыть Debug HUD
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class StreamingTest_AutoRun : MonoBehaviour
    {
        [Header("Streaming System")]
        [Tooltip("WorldStreamingManager для тестирования")]
        [SerializeField] private WorldStreamingManager streamingManager;
        
        [Header("Test Settings")]
        [Tooltip("Test teleport positions (XZ coordinates) - including large coordinates for artifact testing")]
        [SerializeField] private Vector2[] testPositions = new Vector2[]
        {
            // Small positions (near origin)
            new Vector2(0, 0),
            new Vector2(500, 300),
            new Vector2(-300, -500),
            // Large positions (>100k - triggers FloatingOrigin shift)
            new Vector2(150000, 150000),
            new Vector2(-150000, 150000),
            new Vector2(150000, -150000),
            new Vector2(-150000, -150000),
            // Very large positions (>200k)
            new Vector2(250000, 250000),
            new Vector2(-250000, -250000)
        };
        
        [Tooltip("Высота Y для телепортации")]
        [SerializeField] private float teleportHeight = 500f;
        
        [Tooltip("Плавное перемещение к точке")]
        [SerializeField] private bool smoothTeleport = true;
        
        [Tooltip("Скорость плавного перемещения")]
        [SerializeField] private float teleportSpeed = 100f;
        
        [Header("Debug Visualization")]
        [Tooltip("Показывать текущий чанк игрока")]
        #pragma warning disable 0414
        [SerializeField] private bool showCurrentChunk = true;
        
        [Tooltip("Цвет индикатора текущего чанка")]
        [SerializeField] private Color currentChunkColor = Color.yellow;
        
        [Tooltip("Размер индикатора")]
        [SerializeField] private float indicatorSize = 50f;
        #pragma warning restore 0414
        
        // Internal state
        private int _currentTargetIndex = 0;
        private Vector3 _targetPosition;
        private bool _isMoving = false;
        private bool _debugHUDVisible = false;
        private Camera _mainCamera;
        
        // Singleton для доступа из других компонентов
        public static StreamingTest_AutoRun Instance { get; private set; }
        
        private void Awake()
        {
            Debug.Log("[StreamingTest_AutoRun] ✅ Awake called - компонент работает!");
            Instance = this;
        }
        
        private void Start()
        {
            _mainCamera = GetComponent<Camera>();
            
            Debug.Log($"[StreamingTest_AutoRun] ✅ Start() called!");
            Debug.Log($"[StreamingTest_AutoRun] Camera: {(_mainCamera != null ? _mainCamera.name : "NULL")}");
            
            // Auto-find streaming manager
            if (streamingManager == null)
            {
                streamingManager = WorldStreamingManager.Instance;
                if (streamingManager == null)
                {
                    streamingManager = FindAnyObjectByType<WorldStreamingManager>();
                }
            }
            
            if (streamingManager != null)
            {
                Debug.Log("[StreamingTest_AutoRun] ✅ StreamingManager found. Streaming system is ready.");
            }
            else
            {
                Debug.LogWarning("[StreamingTest_AutoRun] ⚠️ StreamingManager not found. Streaming will be disabled.");
            }
            
            // Начинаем с первой тестовой позиции
            if (testPositions.Length > 0)
            {
                _targetPosition = new Vector3(testPositions[0].x, teleportHeight, testPositions[0].y);
            }
            
            Debug.Log("[StreamingTest_AutoRun] 🎮 Управление: F5=след.точка, F6=пред.точка, F7=загрузить чанки, F8=сбросить FloatingOrigin, F9=ChunkGrid(меню), F10=HUD");
        }
        
        private void Update()
        {
            // Keyboard controls for testing
            HandleKeyboardInput();
            
            // Smooth movement to target
            if (_isMoving && smoothTeleport && _mainCamera != null)
            {
                Vector3 currentPos = _mainCamera.transform.position;
                Vector3 direction = _targetPosition - currentPos;
                
                if (direction.magnitude > 5f)
                {
                    _mainCamera.transform.position = Vector3.MoveTowards(
                        currentPos,
                        _targetPosition,
                        teleportSpeed * Time.deltaTime
                    );
                    
                    // НЕ вызываем ResetOrigin() здесь — это вызовет множественные сдвиги!
                    // ResetOrigin вызывается в OnTeleportComplete() -> TeleportToPeak()
                }
                else
                {
                    _isMoving = false;
                    OnTeleportComplete();
                }
            }
        }
        
        /// <summary>
        /// Обработка клавиатурного ввода для тестирования.
        /// Использует паттерн из других файлов проекта (MeziyStatusHUD, ShipDebugHUD).
        /// </summary>
        private void HandleKeyboardInput()
        {
            bool f5Pressed = false;
            bool f6Pressed = false;
            bool f7Pressed = false;
            bool f8Pressed = false;
            bool f9Pressed = false;
            bool f10Pressed = false;
            
            #if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Keyboard.current != null)
            {
                f5Pressed = UnityEngine.InputSystem.Keyboard.current.f5Key.wasPressedThisFrame;
                f6Pressed = UnityEngine.InputSystem.Keyboard.current.f6Key.wasPressedThisFrame;
                f7Pressed = UnityEngine.InputSystem.Keyboard.current.f7Key.wasPressedThisFrame;
                f8Pressed = UnityEngine.InputSystem.Keyboard.current.f8Key.wasPressedThisFrame;
                f9Pressed = UnityEngine.InputSystem.Keyboard.current.f9Key.wasPressedThisFrame;
                f10Pressed = UnityEngine.InputSystem.Keyboard.current.f10Key.wasPressedThisFrame;
            }
            #else
            f5Pressed = Input.GetKeyDown(KeyCode.F5);
            f6Pressed = Input.GetKeyDown(KeyCode.F6);
            f7Pressed = Input.GetKeyDown(KeyCode.F7);
            f8Pressed = Input.GetKeyDown(KeyCode.F8);
            f9Pressed = Input.GetKeyDown(KeyCode.F9);
            f10Pressed = Input.GetKeyDown(KeyCode.F10);
            #endif
            
            // F5 - следующая позиция
            if (f5Pressed)
            {
                Debug.Log("[StreamingTest_AutoRun] 🎮 F5 нажата - следующая позиция");
                _currentTargetIndex = (_currentTargetIndex + 1) % testPositions.Length;
                TeleportToTestPosition(_currentTargetIndex);
            }
            
            // F6 - предыдущая позиция
            if (f6Pressed)
            {
                Debug.Log("[StreamingTest_AutoRun] 🎮 F6 нажата - предыдущая позиция");
                _currentTargetIndex = (_currentTargetIndex - 1 + testPositions.Length) % testPositions.Length;
                TeleportToTestPosition(_currentTargetIndex);
            }
            
            // F7 - загрузить чанки вокруг позиции ИГРОКА (не камеры!)
            if (f7Pressed)
            {
                Debug.Log("[StreamingTest_AutoRun] 🎮 F7 нажата - загрузить чанки вокруг игрока");
                
                // I5-001 FIX: Используем позицию игрока, а не камеры
                Vector3 playerPosition = GetPlayerPosition();
                
                if (streamingManager != null)
                {
                    streamingManager.LoadChunksAroundPlayer(playerPosition);
                    Debug.Log($"[StreamingTest_AutoRun] ✅ Чанки загружены вокруг игрока: {playerPosition}");
                }
                else
                {
                    Debug.LogWarning("[StreamingTest_AutoRun] ⚠️ StreamingManager не найден!");
                }
            }
            
            // F8 - Toggle FloatingOrigin
            if (f8Pressed)
            {
                Debug.Log("[StreamingTest_AutoRun] 🎮 F8 нажата - сбросить FloatingOrigin");
                var fo = FindAnyObjectByType<FloatingOriginMP>();
                if (fo != null)
                {
                    fo.ResetOrigin();
                    Debug.Log($"[StreamingTest_AutoRun] ✅ FloatingOrigin reset. Total offset: {fo.TotalOffset}");
                }
            }
            
            // F9 - Toggle Grid visualization (через Editor Window)
            if (f9Pressed)
            {
                Debug.Log("[StreamingTest_AutoRun] 🎮 F9 нажата - Toggle Chunk Grid");
#if UNITY_EDITOR
                // Используем reflection для вызова Editor класса
                var chunkVizType = System.Type.GetType("ProjectC.Editor.ChunkVisualizer, Assembly-CSharp");
                if (chunkVizType != null)
                {
                    var method = chunkVizType.GetMethod("ToggleChunkGrid", 
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    if (method != null)
                    {
                        method.Invoke(null, null);
                        Debug.Log("[StreamingTest_AutoRun] ✅ Chunk Grid toggled!");
                    }
                }
                else
                {
                    Debug.LogWarning("[StreamingTest_AutoRun] ⚠️ ChunkVisualizer не найден в runtime. Используйте: Tools → Project C → World → Toggle Chunk Grid");
                }
#endif
            }
            
            // F10 - Toggle debug HUD
            if (f10Pressed)
            {
                Debug.Log("[StreamingTest_AutoRun] 🎮 F10 нажата - toggle Debug HUD");
                _debugHUDVisible = !_debugHUDVisible;
                if (streamingManager != null)
                {
                    var field = streamingManager.GetType().GetField("showDebugHUD", 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (field != null)
                    {
                        field.SetValue(streamingManager, _debugHUDVisible);
                    }
                    Debug.Log($"[StreamingTest_AutoRun] ✅ Debug HUD: {(_debugHUDVisible ? "ON" : "OFF")}");
                }
            }
        }
        
        /// <summary>
        /// Телепортироваться к тестовой позиции.
        /// </summary>
        private void TeleportToTestPosition(int index)
        {
            if (index < 0 || index >= testPositions.Length) return;
            
            Vector2 pos2D = testPositions[index];
            _targetPosition = new Vector3(pos2D.x, teleportHeight, pos2D.y);
            
            Debug.Log($"[StreamingTest_AutoRun] 📍 Телепортация к точке {index}: ({pos2D.x}, {pos2D.y})");

            // Ищем персонажа для телепортации
            // ПРИМЕЧАНИЕ: ResetOrigin вызывается в TeleportToPeak() — не нужно вызывать здесь!
            // Вызов ResetOrigin до телепортации (когда камера на 0) не имеет смысла
            // и может нарушить cooldown.
            
            // Ищем персонажа для телепортации
            var networkObjects = FindObjectsByType<Unity.Netcode.NetworkObject>();
            foreach (var netObj in networkObjects)
            {
                if (netObj.IsOwner)
                {
                    var player = netObj.GetComponent<UnityEngine.Transform>();
                    if (player != null)
                    {
                        player.position = _targetPosition;
                        Debug.Log($"[StreamingTest_AutoRun] ✅ Телепортирован игрок к {_targetPosition}");
                    }
                }
            }
            
            // Также телепортируем камеру для плавности
            if (_mainCamera != null)
            {
                _mainCamera.transform.position = _targetPosition;
            }
            
            OnTeleportComplete();
        }
        
        /// <summary>
        /// Вызывается после завершения телепортации.
        /// </summary>
        private void OnTeleportComplete()
        {
            Debug.Log($"[StreamingTest_AutoRun] ✅ Телепортация завершена. Позиция: {_targetPosition}");
            
            if (streamingManager != null)
            {
                streamingManager.TeleportToPeak(_targetPosition);
            }
        }
        
        /// <summary>
        /// Телепортироваться к произвольным координатам.
        /// </summary>
        public void TeleportToCoordinates(float x, float z)
        {
            Vector3 pos = new Vector3(x, teleportHeight, z);
            
            if (_mainCamera != null)
            {
                if (smoothTeleport)
                {
                    _targetPosition = pos;
                    _isMoving = true;
                }
                else
                {
                    _mainCamera.transform.position = pos;
                    OnTeleportComplete();
                }
            }
        }
        
#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!showCurrentChunk || _mainCamera == null) return;
            
            // Показываем текущий чанк игрока
            Gizmos.color = currentChunkColor;
            
            Vector3 currentPos = _mainCamera.transform.position;
            int chunkX = Mathf.FloorToInt(currentPos.x / 2000f);
            int chunkZ = Mathf.FloorToInt(currentPos.z / 2000f);
            
            float minX = chunkX * 2000f;
            float minZ = chunkZ * 2000f;
            
            Vector3 center = new Vector3(minX + 1000f, currentPos.y, minZ + 1000f);
            Vector3 size = new Vector3(2000f, indicatorSize, 2000f);
            
            Gizmos.DrawWireCube(center, size);
            
            // Label
            Gizmos.color = Color.white;
            Gizmos.DrawIcon(center, "d_BuildSettings.PlayerSettings", true);
        }
#endif
        
        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
        
        /// <summary>
        /// I5-001 FIX: Получить позицию локального игрока.
        /// Использует приоритет:
        /// 1. NetworkPlayer с IsOwner
        /// 2. Object с тегом "Player"
        /// 3. Camera (fallback)
        /// </summary>
        private Vector3 GetPlayerPosition()
        {
            // Priority 1: NetworkPlayer с IsOwner
            var networkObjects = FindObjectsByType<Unity.Netcode.NetworkObject>();
            foreach (var netObj in networkObjects)
            {
                if (netObj.IsOwner && netObj.name.Contains("NetworkPlayer"))
                {
                    Debug.Log($"[StreamingTest_AutoRun] Using NetworkPlayer IsOwner: {netObj.transform.position}");
                    return netObj.transform.position;
                }
            }
            
            // Priority 2: Object с тегом "Player"
            GameObject playerByTag = GameObject.FindGameObjectWithTag("Player");
            if (playerByTag != null)
            {
                Debug.Log($"[StreamingTest_AutoRun] Using Player tag: {playerByTag.transform.position}");
                return playerByTag.transform.position;
            }
            
            // Priority 3: Camera fallback
            if (_mainCamera != null)
            {
                Debug.LogWarning($"[StreamingTest_AutoRun] WARNING: Using Camera fallback: {_mainCamera.transform.position}");
                return _mainCamera.transform.position;
            }
            
            return Vector3.zero;
        }
    }
}
