using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;
using ProjectC.UI;
using ProjectC.World.Core;

namespace ProjectC.Core
{
    /// <summary>
    /// Камера для мира Project C
    /// Следует за игроком и поддерживает режимы полёта/хождения
    /// </summary>
    public class WorldCamera : MonoBehaviour
    {
        [Header("Настройки камеры")]
        [Tooltip("Цель для слежения (игрок)")]
        [SerializeField] private Transform target;

        [Tooltip("Дистанция до цели")]
        [SerializeField] private float distance = 50f;

        [Tooltip("Высота камеры")]
        [SerializeField] private float height = 20f;

        [Header("Стартовая позиция")]
        [Tooltip("Начальная высота камеры при старте (уровень облаков)")]
        [SerializeField] private float startHeight = 500f;

        [Tooltip("Автоматически лететь к первому пику при старте")]
        [SerializeField] private bool flyToFirstPeakOnStart = true;

        [Header("Настройки полёта")]
        [Tooltip("Скорость движения камеры в режиме полёта")]
        [SerializeField] private float flySpeed = 100f;

        [Tooltip("Ускорение в режиме полёта")]
        [SerializeField] private float flyAcceleration = 10f;

        [Tooltip("Максимальная скорость")]
        [SerializeField] private float maxSpeed = 500f;

        [Header("Настройки вращения")]
        [Tooltip("Чувствительность мыши по горизонтали")]
        [SerializeField] private float mouseSensitivityX = 3f;

        [Tooltip("Чувствительность мыши по вертикали")]
        [SerializeField] private float mouseSensitivityY = 3f;

        [Tooltip("Минимальный угол обзора по вертикали")]
        [SerializeField] private float minVerticalAngle = -89f;

        [Tooltip("Максимальный угол обзора по вертикали")]
        [SerializeField] private float maxVerticalAngle = 89f;

        [Header("Быстрое перемещение")]
        [Tooltip("Скорость телепортации к пику")]
        [SerializeField] private float teleportSpeed = 200f;

        private float currentX = 0f;
        private float currentY = 0f;
        private Vector3 currentVelocity = Vector3.zero;
        private bool isFlying = true;
        private bool isTeleporting = false;
        private Vector3 teleportTarget = Vector3.zero;
        private WorldGenerator worldGenerator;
        private FloatingOrigin floatingOrigin;

        // REFACTORED: Cache UI and world references instead of FindObjectsByType
        private ProjectC.UI.ControlHintsUI _cachedControlHintsUI;
        private Canvas _cachedCanvas;
        private Transform _cachedWorldRoot;

        // Input System — программно созданные действия
        private InputAction _moveAction;
        private InputAction _lookAction;
        private InputAction _scrollAction;
        private InputAction _toggleFlyAction;
        private InputAction _nextPeakAction;
        private InputAction _previousPeakAction;
        private InputAction _randomPeakAction;
        private InputAction _returnToHeightAction;
        private InputAction _boostAction;

        private Vector2 _moveInput;
        private Vector2 _lookInput;
        private float _scrollInput;

        private void Awake()
        {
            // КРИТИЧНО: Far Clip Plane для бесшовного мира 350,000 units
            Camera cam = GetComponent<Camera>();
            if (cam != null)
            {
                cam.farClipPlane = 1000000f; // 1 million units - covers entire world
                cam.nearClipPlane = 0.5f; // Slightly increased to reduce z-fighting

                // АВТОМАТИЧЕСКИ добавляем FloatingOrigin если его нет
                floatingOrigin = GetComponent<FloatingOrigin>();
                if (floatingOrigin == null)
                {
                    floatingOrigin = gameObject.AddComponent<FloatingOrigin>();
                }

                // Автоматически находим worldRoot при старте
                floatingOrigin.worldRoot = FindWorldRoot();
                floatingOrigin.threshold = 100000f;
                floatingOrigin.showDebugLogs = true; // Включено для отладки телепортации

                Debug.Log($"[WorldCamera] FloatingOrigin initialized. worldRoot={floatingOrigin.worldRoot?.name ?? "NULL"}");
            }

            // Принудительно устанавливаем высоту облаков, если значение слишком большое
            if (startHeight > 1000f)
            {
                startHeight = 500f;
            }

            // Создаём Input Actions программно
            _moveAction = new InputAction("Move", binding: "<Keyboard>/w,<Keyboard>/s,<Keyboard>/a,<Keyboard>/d", expectedControlType: "Vector2");
            _moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");

            _lookAction = new InputAction("Look", binding: "<Mouse>/delta", expectedControlType: "Vector2");
            _scrollAction = new InputAction("Scroll", binding: "<Mouse>/scroll/y", expectedControlType: "Float");

            _toggleFlyAction = new InputAction("ToggleFlyMode", binding: "<Keyboard>/v", expectedControlType: "Button");
            _nextPeakAction = new InputAction("NextPeak", expectedControlType: "Button");
            _nextPeakAction.AddBinding("<Keyboard>/n");
            _nextPeakAction.AddBinding("<Keyboard>/pageUp");

            _previousPeakAction = new InputAction("PreviousPeak", expectedControlType: "Button");
            _previousPeakAction.AddBinding("<Keyboard>/b");
            _previousPeakAction.AddBinding("<Keyboard>/pageDown");

            _randomPeakAction = new InputAction("RandomPeak", binding: "<Keyboard>/r", expectedControlType: "Button");
            _returnToHeightAction = new InputAction("ReturnToHeight", binding: "<Keyboard>/h", expectedControlType: "Button");
            _boostAction = new InputAction("Boost", binding: "<Keyboard>/leftShift", expectedControlType: "Button");

            // Подписка на события
            _toggleFlyAction.performed += ctx => ToggleFlyMode();
            _nextPeakAction.performed += ctx => TeleportToNextPeak();
            _previousPeakAction.performed += ctx => TeleportToPreviousPeak();
            _randomPeakAction.performed += ctx => TeleportToRandomPeak();
            _returnToHeightAction.performed += ctx => ReturnToCloudHeight();
            // Boost проверяем каждый кадр через ReadValue — надёжнее чем performed/canceled
        }

        private void OnEnable()
        {
            // Если есть target (персонаж), отключаем WASD и scroll — камера только вращается мышью
            bool hasTarget = target != null;

            if (hasTarget)
            {
                _moveAction.Disable();
                _scrollAction.Disable();
            }
            else
            {
                _moveAction.Enable();
                _scrollAction.Enable();
            }

            _lookAction.Enable(); // Мышь всегда работает для вращения
            _toggleFlyAction.Enable();
            _nextPeakAction.Enable();
            _previousPeakAction.Enable();
            _randomPeakAction.Enable();
            _returnToHeightAction.Enable();
            _boostAction.Enable();
        }

        private void OnDisable()
        {
            _moveAction.Disable();
            _lookAction.Disable();
            _scrollAction.Disable();
            _toggleFlyAction.Disable();
            _nextPeakAction.Disable();
            _previousPeakAction.Disable();
            _randomPeakAction.Disable();
            _returnToHeightAction.Disable();
            _boostAction.Disable();
        }

        private void Start()
        {
            // ИСПРАВЛЕНО: НЕ блокируем курсор при старте — это ломало NetworkUI меню.
            // Курсор блокируется только когда игрок в игре (ThirdPersonCamera.InitializeCamera()).
            // WorldCamera получает управление мышью через ПКМ (HandleRotation при зажатой ПКМ).
            // Стартовое состояние: курсор свободен для UI.
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // Инициализируем углы
            Vector3 angles = transform.eulerAngles;
            currentX = angles.y;
            currentY = angles.x;

            // Находим WorldGenerator для получения списка пиков
            worldGenerator = FindAnyObjectByType<WorldGenerator>();

            // Если есть target (персонаж), не телепортируемся к пику
            if (target != null)
            {
                // Камера следует за персонажем, старт с начальной позиции рядом
                transform.position = target.position - transform.forward * distance + Vector3.up * height;
            }
            else
            {
                // Принудительно устанавливаем стартовую высоту
                transform.position = new Vector3(0, startHeight, 0);

                // Телепорт к первому пику если нет персонажа
                if (flyToFirstPeakOnStart && worldGenerator != null)
                {
                    var peaks = worldGenerator.GetAllPeaks();
                    if (peaks.Count > 0)
                    {
                        TeleportToPeak(0);
                    }
                }
            }

            // Создаём UI подсказок автоматически, если нет на сцене
            CreateControlHintsUI();
        }

        /// <summary>
        /// Найти корневой объект мира для FloatingOrigin.
        /// Ищет "Mountains" или любой объект с множеством детей.
        /// REFACTORED: Caches result to avoid FindObjectsByType on subsequent calls.
        /// </summary>
        private Transform FindWorldRoot()
        {
            // Return cached if already found
            if (_cachedWorldRoot != null)
            {
                return _cachedWorldRoot;
            }

            // 1. Пробуем найти "Mountains"
            GameObject mountains = GameObject.Find("Mountains");
            if (mountains != null && mountains.transform.childCount > 0)
            {
                Debug.Log($"[WorldCamera] Found Mountains root with {mountains.transform.childCount} children");
                _cachedWorldRoot = mountains.transform;
                return _cachedWorldRoot;
            }

            // 2. Ищем любой объект с большим количеством детей (only once, then cache)
            GameObject[] allObjects = FindObjectsByType<GameObject>(FindObjectsInactive.Include);
            Transform bestRoot = null;
            int maxChildren = 0;

            foreach (var obj in allObjects)
            {
                if (obj.transform.childCount > maxChildren && obj.transform.parent == null)
                {
                    maxChildren = obj.transform.childCount;
                    bestRoot = obj.transform;
                }
            }

            if (bestRoot != null && maxChildren > 5)
            {
                Debug.Log($"[WorldCamera] Using {bestRoot.name} as world root ({maxChildren} children)");
                _cachedWorldRoot = bestRoot;
                return _cachedWorldRoot;
            }

            Debug.LogWarning("[WorldCamera] Cannot find world root! FloatingOrigin may not work properly.");
            return null;
        }

        /// <summary>
        /// Создать UI подсказок автоматически.
        /// REFACTORED: Uses cached references instead of FindAnyObjectByType.
        /// </summary>
        private void CreateControlHintsUI()
        {
            // Check cached reference first
            if (_cachedControlHintsUI != null)
            {
                return;
            }

            // Try to find existing UI elements (only once, then cache)
            if (_cachedControlHintsUI == null)
            {
                var existingHints = FindObjectsByType<ControlHintsUI>(FindObjectsInactive.Include);
                if (existingHints != null && existingHints.Length > 0)
                {
                    _cachedControlHintsUI = existingHints[0];
                    return;
                }
            }

            // Find or create Canvas (only once)
            if (_cachedCanvas == null)
            {
                var existingCanvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include);
                if (existingCanvases != null && existingCanvases.Length > 0)
                {
                    _cachedCanvas = existingCanvases[0];
                }
                else
                {
                    GameObject canvasObj = new GameObject("Canvas");
                    _cachedCanvas = canvasObj.AddComponent<Canvas>();
                    canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
                    canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();
                    _cachedCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                }
            }

            // TextMeshPro
            var textObj = new GameObject("ControlHintsText");
            textObj.transform.SetParent(_cachedCanvas.transform);
            RectTransform rectTransform = textObj.AddComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0, 1);
            rectTransform.anchorMax = new Vector2(0, 1);
            rectTransform.pivot = new Vector2(0, 1);
            rectTransform.anchoredPosition = new Vector2(20, -20);
            rectTransform.sizeDelta = new Vector2(300, 400);

            var tmpText = textObj.AddComponent<TextMeshProUGUI>();
            tmpText.fontSize = 14;
            tmpText.color = Color.white;
            tmpText.alignment = TextAlignmentOptions.TopLeft;

            // ControlHintsUI
            GameObject hintsObj = new GameObject("ControlHintsUI");
            hintsObj.transform.SetParent(_cachedCanvas.transform);
            _cachedControlHintsUI = hintsObj.AddComponent<ControlHintsUI>();
            _cachedControlHintsUI.hintsText = tmpText;
        }

        private void LateUpdate()
        {
            // Если есть цель (персонаж) — камера следует + вращается мышью
            if (target != null)
            {
                // Обработка телепортации
                if (isTeleporting)
                {
                    HandleTeleport();
                    return;
                }

                // Считываем мышь для вращения вокруг персонажа
                _lookInput = _lookAction.ReadValue<Vector2>();
                HandleRotation();
                HandleFollowTarget();
                return;
            }

            // Режим свободного полёта — камера управляется сама
            _moveInput = _moveAction.ReadValue<Vector2>();
            _lookInput = _lookAction.ReadValue<Vector2>();
            _scrollInput = _scrollAction.ReadValue<float>();

            // Обработка телепортации
            if (isTeleporting)
            {
                HandleTeleport();
                return;
            }

            HandleRotation();
            HandleMovement();
        }

        /// <summary>
        /// Обработка телепортации к пику
        /// </summary>
        private void HandleTeleport()
        {
            transform.position = Vector3.MoveTowards(transform.position, teleportTarget, teleportSpeed * Time.deltaTime);

            if (Vector3.Distance(transform.position, teleportTarget) < 1f)
            {
                isTeleporting = false;
            }
        }

        /// <summary>
        /// Возврат на высоту облаков
        /// </summary>
        private void ReturnToCloudHeight()
        {
            transform.position = new Vector3(
                transform.position.x,
                500f,
                transform.position.z
            );
        }

        /// <summary>
        /// Следование за целью (персонажем) — камера от третьего лица
        /// </summary>
        private void HandleFollowTarget()
        {
            if (target == null) return;

            // Вычисляем позицию камеры на основе углов орбиты
            float yaw = currentX;
            float pitch = currentY;

            // Направление от цели к камере
            Vector3 direction = new Vector3(
                Mathf.Sin(yaw * Mathf.Deg2Rad) * Mathf.Cos(pitch * Mathf.Deg2Rad),
                Mathf.Sin(pitch * Mathf.Deg2Rad),
                Mathf.Cos(yaw * Mathf.Deg2Rad) * Mathf.Cos(pitch * Mathf.Deg2Rad)
            );

            Vector3 desiredPosition = target.position - direction.normalized * distance;

            // Плавное следование
            transform.position = Vector3.SmoothDamp(
                transform.position,
                desiredPosition,
                ref currentVelocity,
                0.15f
            );

            // Камера смотрит на цель
            transform.LookAt(target.position + Vector3.up * 1.5f);
        }

        /// <summary>
        /// Следование за целью
        /// </summary>
        private void FollowTarget()
        {
            if (target == null) return;

            Vector3 targetPosition = target.position + Vector3.up * height;

            // Плавное следование
            transform.position = Vector3.SmoothDamp(
                transform.position,
                targetPosition - transform.forward * distance,
                ref currentVelocity,
                0.3f
            );
        }

        /// <summary>
        /// Обработка вращения камеры
        /// </summary>
        private void HandleRotation()
        {
            currentX += _lookInput.x * mouseSensitivityX;
            currentY -= _lookInput.y * mouseSensitivityY;
            currentY = Mathf.Clamp(currentY, minVerticalAngle, maxVerticalAngle);

            // В режиме следования за персонажем — не вращаем саму камеру, 
            // а обновляем углы орбиты (HandleFollowTarget использует их)
            if (target == null)
            {
                transform.eulerAngles = new Vector3(currentY, currentX, 0);
            }
        }

        /// <summary>
        /// Обработка движения камеры (режим полёта)
        /// </summary>
        private void HandleMovement()
        {
            // Не двигаем камеру если следуем за персонажем — персонаж двигается сам
            if (target != null) return;
            if (!isFlying) return;

            // Boost проверяем каждый кадр — надёжнее
            bool boosting = _boostAction.ReadValue<float>() > 0.5f;
            float currentFlySpeed = boosting ? maxSpeed : flySpeed;

            // Движение вперёд/назад + стрейф
            Vector3 moveDirection = transform.right * _moveInput.x + transform.forward * _moveInput.y;

            // Ускорение
            currentVelocity = Vector3.Lerp(
                currentVelocity,
                moveDirection * currentFlySpeed,
                flyAcceleration * Time.deltaTime
            );

            transform.position += currentVelocity * Time.deltaTime;

            // Высота (вверх/вниз)
            if (_scrollInput != 0)
            {
                transform.position += Vector3.up * _scrollInput * 10f;
            }
        }

        /// <summary>
        /// Установить цель для камеры
        /// </summary>
        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }

        /// <summary>
        /// Переключить режим полёта
        /// </summary>
        public void ToggleFlyMode()
        {
            isFlying = !isFlying;
        }

        /// <summary>
        /// Установить режим полёта
        /// </summary>
        public void SetFlyMode(bool flying)
        {
            isFlying = flying;
        }

        /// <summary>
        /// Телепортация к пику по индексу
        /// КРИТИЧНО: Правильный порядок для больших миров:
        /// 1. Телепортировать камеру к пику (в мировые координаты)
        /// 2. Сдвинуть ВЕСЬ мир так чтобы камера оказалась рядом с origin
        /// Это предотвращает floating point precision проблемы.
        /// </summary>
        public void TeleportToPeak(int peakIndex)
        {
            if (worldGenerator == null)
            {
                Debug.LogWarning("[WorldCamera] WorldGenerator не найден!");
                return;
            }

            var peaks = worldGenerator.GetAllPeaks();
            if (peakIndex >= 0 && peakIndex < peaks.Count)
            {
                var peak = peaks[peakIndex];

                // Шаг 1: Телепортировать камеру к пику (в абсолютные мировые координаты)
                Vector3 targetWorldPos = peak.position + Vector3.up * (peak.height * 0.5f);
                transform.position = targetWorldPos;
                teleportTarget = targetWorldPos;
                isTeleporting = false;

                // Шаг 2: Сдвинуть ВЕСЬ мир так чтобы камера оказалась рядом с origin
                // FloatingOrigin возьмёт текущую позицию камеры и сдвинет мир
                if (floatingOrigin != null)
                {
                    floatingOrigin.ResetOrigin();
                }

                // Шаг 3: Логирование для отладки
                if (floatingOrigin != null && floatingOrigin.showDebugLogs)
                {
                    float distFromOrigin = transform.position.magnitude;
                    Debug.Log($"[WorldCamera] Teleported to {peak.name}. " +
                              $"cameraPos={transform.position:F0}, distFromOrigin={distFromOrigin:F0}");

                    if (distFromOrigin > 100000f)
                    {
                        Debug.LogWarning($"[WorldCamera] Camera is still far from origin! " +
                                        $"FloatingOrigin may not be working correctly. " +
                                        $"worldRoot={floatingOrigin.worldRoot?.name ?? "NULL"}");
                    }
                }
            }
        }

        /// <summary>
        /// Телепорт к следующему пику
        /// </summary>
        private int currentPeakIndex = -1;

        public void TeleportToNextPeak()
        {
            if (worldGenerator == null) return;

            var peaks = worldGenerator.GetAllPeaks();
            if (peaks.Count == 0) return;

            currentPeakIndex = (currentPeakIndex + 1) % peaks.Count;
            TeleportToPeak(currentPeakIndex);
        }

        /// <summary>
        /// Телепорт к предыдущему пику
        /// </summary>
        public void TeleportToPreviousPeak()
        {
            if (worldGenerator == null) return;

            var peaks = worldGenerator.GetAllPeaks();
            if (peaks.Count == 0) return;

            currentPeakIndex = (currentPeakIndex - 1 + peaks.Count) % peaks.Count;
            TeleportToPeak(currentPeakIndex);
        }

        /// <summary>
        /// Телепорт к случайному пику
        /// </summary>
        public void TeleportToRandomPeak()
        {
            if (worldGenerator == null) return;

            var peaks = worldGenerator.GetAllPeaks();
            if (peaks.Count == 0) return;

            currentPeakIndex = Random.Range(0, peaks.Count);
            TeleportToPeak(currentPeakIndex);
        }
    }
}
