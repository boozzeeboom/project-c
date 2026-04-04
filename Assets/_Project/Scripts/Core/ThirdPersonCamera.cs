using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

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
        [SerializeField] private float minVerticalAngle = 0f;

        [Tooltip("Максимальный угол обзора")]
        [SerializeField] private float maxVerticalAngle = 80f;

        // Углы орбиты
        private float _yaw;
        private float _pitch;

        // Ввод
        private InputAction _lookAction;
        private Vector2 _lookInput;

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
            _lookAction = new InputAction("Look", binding: "<Mouse>/delta", expectedControlType: "Vector2");
        }

        private void OnEnable() => _lookAction.Enable();
        private void OnDisable() => _lookAction.Disable();

        private void Start()
        {
            if (target == null)
            {
                // Target будет назначен позже через SetTarget()
                return;
            }

            _yaw = 0f;
            _pitch = 15f;
            _currentDistance = distance;
            _currentHeight = height;

            UpdateCameraPosition();

            // Создаём UI подсказок если нет
            CreateControlHintsUI();
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
                Debug.Log($"[ThirdPersonCamera] Target changed to {newTarget.name}");
            }
        }

        /// <summary>
        /// Создать UI подсказок автоматически
        /// </summary>
        private void CreateControlHintsUI()
        {
            var existingHints = FindAnyObjectByType<ProjectC.UI.ControlHintsUI>();
            if (existingHints != null)
            {
                Debug.Log("[ThirdPersonCamera] ControlHintsUI уже существует");
                return;
            }

            // Создаём Canvas
            var canvas = FindAnyObjectByType<Canvas>();
            if (canvas == null)
            {
                GameObject canvasObj = new GameObject("Canvas");
                canvas = canvasObj.AddComponent<Canvas>();
                canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
                canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }

            // TextMeshPro
            var textObj = new GameObject("ControlHintsText");
            textObj.transform.SetParent(canvas.transform);
            RectTransform rt = textObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(20, -20);
            rt.sizeDelta = new Vector2(300, 300);

            var tmpText = textObj.AddComponent<TextMeshProUGUI>();
            tmpText.fontSize = 14;
            tmpText.color = Color.white;
            tmpText.alignment = TextAlignmentOptions.TopLeft;

            // ControlHintsUI
            GameObject hintsObj = new GameObject("ControlHintsUI");
            hintsObj.transform.SetParent(canvas.transform);
            var controlHints = hintsObj.AddComponent<ProjectC.UI.ControlHintsUI>();
            controlHints.hintsText = tmpText;

            Debug.Log("[ThirdPersonCamera] Создан ControlHintsUI");
        }

        private void LateUpdate()
        {
            if (target == null) return;

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
