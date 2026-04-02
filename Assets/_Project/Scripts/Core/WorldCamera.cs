using UnityEngine;

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
        [SerializeField] private float distance = 10f;
        
        [Tooltip("Высота камеры")]
        [SerializeField] private float height = 5f;

        [Header("Настройки полёта")]
        [Tooltip("Скорость движения камеры в режиме полёта")]
        [SerializeField] private float flySpeed = 20f;
        
        [Tooltip("Ускорение в режиме полёта")]
        [SerializeField] private float flyAcceleration = 5f;

        [Header("Настройки вращения")]
        [Tooltip("Чувствительность мыши по горизонтали")]
        [SerializeField] private float mouseSensitivityX = 2f;
        
        [Tooltip("Чувствительность мыши по вертикали")]
        [SerializeField] private float mouseSensitivityY = 2f;
        
        [Tooltip("Минимальный угол обзора по вертикали")]
        [SerializeField] private float minVerticalAngle = -80f;
        
        [Tooltip("Максимальный угол обзора по вертикали")]
        [SerializeField] private float maxVerticalAngle = 80f;

        private float currentX = 0f;
        private float currentY = 0f;
        private Vector3 currentVelocity = Vector3.zero;
        private bool isFlying = true;

        private void Start()
        {
            // Скрываем курсор
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            // Инициализируем углы
            Vector3 angles = transform.eulerAngles;
            currentX = angles.y;
            currentY = angles.x;
        }

        private void LateUpdate()
        {
            if (target != null)
            {
                FollowTarget();
            }
            
            HandleRotation();
            HandleMovement();
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
            currentX += Input.GetAxis("Mouse X") * mouseSensitivityX;
            currentY -= Input.GetAxis("Mouse Y") * mouseSensitivityY;
            currentY = Mathf.Clamp(currentY, minVerticalAngle, maxVerticalAngle);

            transform.eulerAngles = new Vector3(currentY, currentX, 0);
        }

        /// <summary>
        /// Обработка движения камеры (режим полёта)
        /// </summary>
        private void HandleMovement()
        {
            if (!isFlying) return;

            float h = Input.GetAxis("Horizontal");
            float v = Input.GetAxis("Vertical");
            
            // Движение вперёд/назад + стрейф
            Vector3 moveDirection = transform.right * h + transform.forward * v;
            
            // Ускорение
            currentVelocity = Vector3.Lerp(
                currentVelocity,
                moveDirection * flySpeed,
                flyAcceleration * Time.deltaTime
            );

            transform.position += currentVelocity * Time.deltaTime;

            // Высота (вверх/вниз)
            float altitude = Input.GetAxis("Mouse ScrollWheel");
            if (altitude != 0)
            {
                transform.position += Vector3.up * altitude * 10f;
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
            Debug.Log($"[WorldCamera] Fly mode: {isFlying}");
        }

        /// <summary>
        /// Установить режим полёта
        /// </summary>
        public void SetFlyMode(bool flying)
        {
            isFlying = flying;
        }
    }
}
