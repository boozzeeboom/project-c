using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using ProjectC.UI;

namespace ProjectC.Core
{
    /// <summary>
    /// Орбитальная камера от третьего лица
    /// Мышь вращает КАМЕРУ вокруг персонажа
    /// Камера ВСЕГДА смотрит на персонажа
    /// </summary>
    public class ThirdPersonCamera : MonoBehaviour
    {
        [Header("Цель")]
        [Tooltip("Персонаж за которым следить")]
        [SerializeField] private Transform target;

        [Header("Орбита")]
        [Tooltip("Дистанция от цели (пеший режим)")]
        [SerializeField] private float distance = 5f;

        [Tooltip("Дистанция от цели (режим корабля)")]
        [SerializeField] private float shipDistance = 18f;

        [Tooltip("Высота камеры относительно цели (пеший)")]
        [SerializeField] private float height = 2f;

        [Tooltip("Высота камеры относительно цели (корабль)")]
        [SerializeField] private float shipHeight = 6f;

        // Текущие интерполированные значения
        private float _currentDistance;
        private float _currentHeight;

        [Header("Вращение")]
        [Tooltip("Чувствительность мыши X")]
        [SerializeField] private float mouseSensitivityX = 3f;

        [Tooltip("Чувствительность мыши Y")]
        [SerializeField] private float mouseSensitivityY = 3f;

        [Tooltip("Минимальный угол обзора")]
        [SerializeField] private float minVerticalAngle = -80f;

        [Tooltip("Максимальный угол обзора")]
        [SerializeField] private float maxVerticalAngle = 80f;

        // Углы орбиты
        private float _yaw;
        private float _pitch;

        // Ввод
        private InputAction _lookAction;
        private Vector2 _lookInput;

        // Инициализация
        private bool _cameraInitialized = false;

        // REFACTORED: Cache UI references instead of FindAnyObjectByType in CreateControlHintsUI
        private ProjectC.UI.ControlHintsUI _cachedControlHintsUI;
        private Canvas _cachedCanvas;

        /// <summary>
        /// Горизонтальное направление камеры (куда бежит персонаж по W)
        /// </summary>
        public Vector3 CameraForward
        {
            get
            {
                float yawRad = _yaw * Mathf.Deg2Rad;
                return new Vector3(Mathf.Sin(yawRad), 0, Mathf.Cos(yawRad));
            }
        }

        public Vector3 CameraRight
        {
            get
            {
                float yawRad = _yaw * Mathf.Deg2Rad;
                return new Vector3(Mathf.Cos(yawRad), 0, -Mathf.Sin(yawRad));
            }
        }

        private void Awake()
        {
            // КРИТИЧНО: Far Clip Plane для бесшовного мира 350,000 units
            Camera cam = GetComponent<Camera>();
            if (cam != null)
            {
                cam.farClipPlane = 1000000f; // 1 million units - covers entire world
                cam.nearClipPlane = 0.5f; // Slightly increased to reduce z-fighting

                // ИСПРАВЛЕНО: FloatingOriginMP НЕ добавляется автоматически на камеру-ребёнка.
                // FloatingOriginMP должен быть на отдельном объекте в сцене (WorldStreamingManager).
                // Добавление FloatingOriginMP сюда вызывало CollectWorldObjects() →
                // рапаренчивание ВСЕХ объектов сцены (включая NetworkManager, игрока) → краш.
            }

            _lookAction = new InputAction("Look", binding: "<Mouse>/delta", expectedControlType: "Vector2");
        }

        private void OnEnable() => _lookAction.Enable();
        private void OnDisable() => _lookAction.Disable();

        private void OnDestroy()
        {
            // Разблокируем курсор когда камера уничтожается (игрок отключился → меню снова кликабельно)
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void Start()
        {
            if (_cameraInitialized) return; // уже инициализирован через InitializeCamera()

            if (target == null)
            {
                // Target будет назначен позже через SetTarget() + InitializeCamera()
                return;
            }

            InitializeCamera();
        }

        /// <summary>
        /// Инициализировать камеру после назначения target.
        /// Вызывается из NetworkPlayer.SpawnCamera() сразу после SetTarget().
        /// Безопасно вызывать несколько раз — повторная инициализация игнорируется.
        /// </summary>
        public void InitializeCamera()
        {
            if (_cameraInitialized) return;
            if (target == null)
            {
                Debug.LogWarning("[ThirdPersonCamera] InitializeCamera вызван до SetTarget! Камера не инициализирована.");
                return;
            }

            _yaw = 0f;
            _pitch = 15f;
            _currentDistance = distance;
            _currentHeight = height;

            // Блокируем курсор ТОЛЬКО если NetworkManager активен (игрок реально в игре).
            // Если target задан в Inspector вручную (без сети) — меню должно оставаться кликабельным.
            bool inActiveGame = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
            if (inActiveGame)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            else
            {
                // Сцена без сети или ещё не подключились — курсор свободен
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            UpdateCameraPosition();

            // Создаём UI подсказок если нет
            CreateControlHintsUI();

            _cameraInitialized = true;

            // R2-XXX: Регистрируем камеру для Billboard (имена над персонажами)
            Billboard.ActiveCamera = transform;
        }

        /// <summary>
        /// Переключить режим камеры (пеший ↔ корабль)
        /// </summary>
        public void SetShipMode(bool isShip)
        {
            float targetDistance = isShip ? shipDistance : distance;
            float targetHeight = isShip ? shipHeight : height;

            // Плавное переключение будет в LateUpdate через Lerp
            _currentDistance = targetDistance;
            _currentHeight = targetHeight;
        }

        /// <summary>
        /// Установить новую цель (например, корабль)
        /// </summary>
        public void SetTarget(Transform newTarget)
        {
            if (newTarget != null)
            {
                target = newTarget;
            }
        }

        /// <summary>
        /// COMPOSITE SHIP (Phase 1): установить target и сразу переключить режим камеры.
        /// Заменяет пару SetTarget+SetShipMode.
        /// </summary>
        public void SetTargetMode(Transform newTarget, bool isShip)
        {
            SetTarget(newTarget);
            SetShipMode(isShip);
        }

        /// <summary>
        /// Создать UI подсказок автоматически.
        /// REFACTORED: Uses cached references instead of FindAnyObjectByType.
        /// </summary>
        /// <summary>
        /// Создать UI подсказок автоматически.
        /// REFACTORED (5_3): Использует HUDManager вместо создания своего Canvas.
        /// </summary>
        private void CreateControlHintsUI()
        {
            // Check cached reference first
            if (_cachedControlHintsUI != null)
            {
                return;
            }

            // Try to find existing ControlHintsUI (only once, then cache)
            if (_cachedControlHintsUI == null)
            {
                var existingHints = FindObjectsByType<ProjectC.UI.ControlHintsUI>(FindObjectsInactive.Include);
                if (existingHints != null && existingHints.Length > 0)
                {
                    _cachedControlHintsUI = existingHints[0];
                    return;
                }
            }

            // Ensure HUDManager exists and get its Canvas
            var hudManager = ProjectC.UI.HUDManager.EnsureExists();
            _cachedCanvas = hudManager.GetOrCreateHUDCanvas();

            // Create TextMeshPro element using HUDManager
            var (textObj, textRect, tmpText) = hudManager.CreateHUDText(
                "ControlHintsText",
                null,
                fontSize: 14,
                color: Color.white,
                alignment: TextAlignmentOptions.TopLeft,
                anchoredPosition: new Vector2(20, -20),
                sizeDelta: new Vector2(300, 300)
            );

            // Create ControlHintsUI component
            GameObject hintsObj = new GameObject("ControlHintsUI");
            hintsObj.transform.SetParent(_cachedCanvas.transform);
            _cachedControlHintsUI = hintsObj.AddComponent<ProjectC.UI.ControlHintsUI>();
            _cachedControlHintsUI.hintsText = tmpText;
        }

        private void LateUpdate()
        {
            if (target == null) return;
            if (Cursor.lockState != CursorLockMode.Locked) return;

            _lookInput = _lookAction.ReadValue<Vector2>();

            _yaw += _lookInput.x * mouseSensitivityX;
            _pitch -= _lookInput.y * mouseSensitivityY;
            _pitch = Mathf.Clamp(_pitch, minVerticalAngle, maxVerticalAngle);

            UpdateCameraPosition();
        }

        private void UpdateCameraPosition()
        {
            float yawRad = _yaw * Mathf.Deg2Rad;
            float pitchRad = _pitch * Mathf.Deg2Rad;

            // Направление от цели к камере (обратное от forward камеры)
            Vector3 dir = new Vector3(
                -Mathf.Sin(yawRad) * Mathf.Cos(pitchRad),
                Mathf.Sin(pitchRad),
                -Mathf.Cos(yawRad) * Mathf.Cos(pitchRad)
            );

            transform.position = target.position + dir * _currentDistance + Vector3.up * _currentHeight;
            transform.LookAt(target.position + Vector3.up * 1.5f);
        }
    }
}
