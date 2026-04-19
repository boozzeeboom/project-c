using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using ProjectC.World.Streaming;
using ProjectC.Player;

namespace ProjectC.World
{
    /// <summary>
    /// Тестовый компонент для демонстрации World Streaming системы.
    /// Используйте в Play Mode для проверки работы стриминга.
    /// 
    /// Управление (F-клавиши чтобы НЕ конфликтовать с основным управлением):
    /// - F5: Телепортироваться к следующей точке
    /// - F6: Телепортироваться к предыдущей точке
    /// - F7: Загрузить чанки вокруг текущей позиции
    /// - F8: Сбросить FloatingOrigin
    /// - F9: Toggle Grid визуализация
    /// - F10: Показать/скрыть Debug HUD
    /// </summary>
    public class StreamingTest : MonoBehaviour
    {
        [Header("Streaming System")]
        [Tooltip("WorldStreamingManager для тестирования")]
        [SerializeField] private WorldStreamingManager streamingManager;
        
        [Header("Test Settings")]
        [Tooltip("Список тестовых точек телепортации (XZ координаты)")]
        [SerializeField] private Vector2[] testPositions = new Vector2[]
        {
            new Vector2(0, 0),
            new Vector2(500, 300),
            new Vector2(-300, -500),
            new Vector2(200, -200),
            new Vector2(-400, 400),
            new Vector2(600, -100)
        };
        
        [Tooltip("Высота Y для телепортации")]
        [SerializeField] private float teleportHeight = 500f;
        
        [Tooltip("Плавное перемещение к точке")]
        [SerializeField] private bool smoothTeleport = true;
        
        [Tooltip("Скорость плавного перемещения")]
        [SerializeField] private float teleportSpeed = 100f;
        
        [Header("Multiplayer Settings")]
        [Tooltip("Использовать позицию локального игрока вместо камеры")]
        [SerializeField] private bool useLocalPlayerPosition = true;
        
        [Tooltip("Teleport игрока напрямую (вместо камеры)")]
        [SerializeField] private bool teleportPlayer = true;
        
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
        
        // Cached components
        private Camera _mainCamera;
        private Transform _trackedTransform;
        private NetworkPlayer _localPlayer;
        
        private void Start()
        {
            // Ищем камеру несколькими способами
            _mainCamera = Camera.main;
            
            if (_mainCamera == null)
            {
                // Пробуем найти камеру по тегу
                GameObject camObj = GameObject.FindGameObjectWithTag("MainCamera");
                if (camObj != null)
                    _mainCamera = camObj.GetComponent<Camera>();
            }
            
            if (_mainCamera == null)
            {
                // Берем любую Enabled камеру
                _mainCamera = FindAnyObjectByType<Camera>();
            }
            
            
            // В мультиплеере ищем локального игрока
            if (useLocalPlayerPosition)
            {
                TryFindLocalPlayer();
            }
            
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
            }
            else
            {
                Debug.LogWarning("[StreamingTest] StreamingManager not found. Streaming will be disabled.");
            }
            
            // Начинаем с первой тестовой позиции
            if (testPositions.Length > 0)
            {
                _targetPosition = new Vector3(testPositions[0].x, teleportHeight, testPositions[0].y);
            }
            
            
            // Определяем что отслеживать
            UpdateTrackedTransform();
        }
        
        /// <summary>
        /// Попытка найти локального игрока в мультиплеере.
        /// </summary>
        private void TryFindLocalPlayer()
        {
            // Проверяем NetworkManager
            if (NetworkManager.Singleton != null)
            {
                // Ищем локального игрока
                var networkObjects = FindObjectsByType<NetworkObject>();
                foreach (var netObj in networkObjects)
                {
                    if (netObj.IsOwner)
                    {
                        var player = netObj.GetComponent<NetworkPlayer>();
                        if (player != null)
                        {
                            _localPlayer = player;
                            Debug.Log($"[StreamingTest] Found local player: {netObj.name}");
                            return;
                        }
                    }
                }
            }
            
            // Local player not found (singleplayer mode)
        }
        
        /// <summary>
        /// Обновить трансформ для отслеживания.
        /// </summary>
        private void UpdateTrackedTransform()
        {
            // Приоритет: локальный игрок > камера
            if (useLocalPlayerPosition && _localPlayer != null)
            {
                _trackedTransform = _localPlayer.transform;
                Debug.Log("[StreamingTest] Tracking local player transform");
            }
            else if (_mainCamera != null)
            {
                _trackedTransform = _mainCamera.transform;
            }
            else
            {
                _trackedTransform = null;
                Debug.LogWarning("[StreamingTest] No transform to track!");
            }
        }
        
        /// <summary>
        /// Получить текущую позицию для отслеживания/стриминга.
        /// </summary>
        private Vector3 GetCurrentPosition()
        {
            if (_trackedTransform != null)
            {
                return _trackedTransform.position;
            }
            
            // Fallback на камеру
            if (_mainCamera != null)
            {
                return _mainCamera.transform.position;
            }
            
            return Vector3.zero;
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
                    
                    // Reset origin if needed
                    var fo = FindAnyObjectByType<ProjectC.World.Streaming.FloatingOriginMP>();
                    if (fo != null && direction.magnitude > 50000f)
                    {
                        fo.ResetOrigin();
                    }
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
        /// Используем F5-F10 чтобы НЕ конфликтовать с основным управлением.
        /// </summary>
        private void HandleKeyboardInput()
        {
            // F5 - следующая позиция (не конфликтует с управлением)
            if (Keyboard.current.f5Key.wasPressedThisFrame)
            {
                _currentTargetIndex = (_currentTargetIndex + 1) % testPositions.Length;
                TeleportToTestPosition(_currentTargetIndex);
            }
            
            // F6 - предыдущая позиция
            if (Keyboard.current.f6Key.wasPressedThisFrame)
            {
                _currentTargetIndex = (_currentTargetIndex - 1 + testPositions.Length) % testPositions.Length;
                TeleportToTestPosition(_currentTargetIndex);
            }
            
            // F7 - загрузить чанки вокруг текущей позиции
            if (Keyboard.current.f7Key.wasPressedThisFrame)
            {
                if (streamingManager != null)
                {
                    // Используем GetCurrentPosition() которое работает с игроком или камерой
                    Vector3 position = GetCurrentPosition();
                    
                    if (position != Vector3.zero || _trackedTransform != null)
                    {
                        streamingManager.LoadChunksAroundPlayer(position);
                        Debug.Log($"[StreamingTest] F7: Loading chunks around position {position} (tracked: {_trackedTransform?.name ?? "NULL"})");
                    }
                    else
                    {
                        Debug.LogWarning("[StreamingTest] F7: No position for chunk loading!");
                    }
                }
                else
                {
                    Debug.LogWarning("[StreamingTest] F7: StreamingManager не найден!");
                }
            }
            
            // F8 - Toggle FloatingOrigin
            if (Keyboard.current.f8Key.wasPressedThisFrame)
            {
                var fo = FindAnyObjectByType<ProjectC.World.Streaming.FloatingOriginMP>();
                if (fo != null)
                {
                    fo.ResetOrigin();
                    Debug.Log($"[StreamingTest] FloatingOrigin reset. Total offset: {fo.TotalOffset}");
                }
            }
            
            // F9 - Toggle Grid visualization
            if (Keyboard.current.f9Key.wasPressedThisFrame)
            {
#if UNITY_EDITOR
                var chunkVizType = System.Type.GetType("ProjectC.Editor.ChunkVisualizer, Assembly-CSharp");
                if (chunkVizType != null)
                {
                    var method = chunkVizType.GetMethod("ToggleChunkGrid", 
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    method?.Invoke(null, null);
                }
#endif
            }
            
            // F10 - Toggle debug HUD
            if (Keyboard.current.f10Key.wasPressedThisFrame)
            {
                _debugHUDVisible = !_debugHUDVisible;
                if (streamingManager != null)
                {
                    var field = streamingManager.GetType().GetField("showDebugHUD", 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (field != null)
                    {
                        field.SetValue(streamingManager, _debugHUDVisible);
                    }
                    Debug.Log($"[StreamingTest] Debug HUD: {(_debugHUDVisible ? "ON" : "OFF")}");
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
            
            Debug.Log($"[StreamingTest] Teleport to position {index}: ({pos2D.x}, {pos2D.y}), teleportPlayer={teleportPlayer}, player={_localPlayer?.name ?? "NULL"}");
            
            // Если включен телепорт игрока и игрок найден
            if (teleportPlayer && _localPlayer != null)
            {
                _localPlayer.transform.position = _targetPosition;
                Debug.Log($"[StreamingTest] Teleported player to {_targetPosition}");
                OnTeleportComplete();
            }
            else if (smoothTeleport && _mainCamera != null)
            {
                _isMoving = true;
                Debug.Log($"[StreamingTest] Smooth camera move to test position {index}: ({pos2D.x}, {pos2D.y})");
            }
            else if (_mainCamera != null)
            {
                // Instant teleport
                _mainCamera.transform.position = _targetPosition;
                Debug.Log($"[StreamingTest] Instant camera teleport to {_targetPosition}");
                OnTeleportComplete();
            }
            else
            {
                Debug.LogWarning($"[StreamingTest] Cannot teleport: no camera and no player!");
            }
        }
        
        /// <summary>
        /// Вызывается после завершения телепортации.
        /// </summary>
        private void OnTeleportComplete()
        {
            Debug.Log($"[StreamingTest] Teleport complete. Position: {_targetPosition}");
            
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
        
        /// <summary>
        /// Телепортироваться к позиции пика.
        /// </summary>
        public void TeleportToPeak(Vector3 peakPosition)
        {
            peakPosition.y = teleportHeight;
            _targetPosition = peakPosition;
            
            if (!smoothTeleport && _mainCamera != null)
            {
                _mainCamera.transform.position = _targetPosition;
            }
            
            _isMoving = smoothTeleport;
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
    }
}