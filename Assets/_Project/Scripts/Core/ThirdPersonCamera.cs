using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectC.Core
{
    /// <summary>
    /// Камера от третьего лица — орбитальная, следует за персонажем
    /// Камера вращается МЫШЬЮ независимо
    /// Персонаж поворачивается по направлению КАМЕРЫ
    /// </summary>
    public class ThirdPersonCamera : MonoBehaviour
    {
        [Header("Цель")]
        [Tooltip("Персонаж за которым следовать")]
        [SerializeField] private Transform target;

        [Header("Орбита")]
        [Tooltip("Дистанция от цели")]
        [SerializeField] private float distance = 6f;

        [Tooltip("Высота камеры относительно цели")]
        [SerializeField] private float height = 3f;

        [Header("Вращение")]
        [Tooltip("Чувствительность мыши X")]
        [SerializeField] private float mouseSensitivityX = 4f;

        [Tooltip("Чувствительность мыши Y")]
        [SerializeField] private float mouseSensitivityY = 4f;

        [Tooltip("Минимальный угол обзора")]
        [SerializeField] private float minVerticalAngle = -10f;

        [Tooltip("Максимальный угол обзора")]
        [SerializeField] private float maxVerticalAngle = 60f;

        [Header("Сглаживание")]
        [Tooltip("Скорость следования")]
        [SerializeField] private float smoothSpeed = 15f;

        // Ввод
        private InputAction _lookAction;
        private Vector2 _lookInput;

        // Углы орбиты (yaw/pitch)
        private float _yaw;
        private float _pitch;

        // Текущая позиция камеры (для сглаживания)
        private Vector3 _smoothPosition;

        /// <summary>
        /// Направление камеры (для движения персонажа)
        /// </summary>
        public Vector3 Forward => -transform.forward;

        public Vector3 Right => transform.right;

        private void Awake()
        {
            _lookAction = new InputAction("Look", binding: "<Mouse>/delta", expectedControlType: "Vector2");
        }

        private void OnEnable()
        {
            _lookAction.Enable();
        }

        private void OnDisable()
        {
            _lookAction.Disable();
        }

        private void Start()
        {
            if (target == null)
            {
                Debug.LogError("[ThirdPersonCamera] Target не назначен!");
                return;
            }

            // Начальные углы — камера позади цели
            _yaw = 0f;
            _pitch = 15f;

            // Стартовая позиция
            _smoothPosition = CalculatePosition();
            transform.position = _smoothPosition;
            transform.LookAt(target.position);
        }

        private void LateUpdate()
        {
            if (target == null) return;

            // Ввод мыши
            _lookInput = _lookAction.ReadValue<Vector2>();

            // Обновить углы
            _yaw += _lookInput.x * mouseSensitivityX;
            _pitch -= _lookInput.y * mouseSensitivityY;
            _pitch = Mathf.Clamp(_pitch, minVerticalAngle, maxVerticalAngle);

            // Вычислить и применить позицию
            Vector3 desiredPos = CalculatePosition();
            _smoothPosition = Vector3.Lerp(_smoothPosition, desiredPos, smoothSpeed * Time.deltaTime);
            transform.position = _smoothPosition;

            // Смотреть на цель
            transform.LookAt(target.position + Vector3.up * 1.2f);
        }

        /// <summary>
        /// Вычислить позицию камеры из углов орбиты
        /// </summary>
        private Vector3 CalculatePosition()
        {
            float yawRad = _yaw * Mathf.Deg2Rad;
            float pitchRad = _pitch * Mathf.Deg2Rad;

            // Направление от цели к камере
            Vector3 dir = new Vector3(
                Mathf.Sin(yawRad) * Mathf.Cos(pitchRad),
                Mathf.Sin(pitchRad),
                Mathf.Cos(yawRad) * Mathf.Cos(pitchRad)
            );

            return target.position + dir * distance + Vector3.up * height;
        }
    }
}
