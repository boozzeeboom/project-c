using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectC.Core
{
    /// <summary>
    /// Орбитальная камера от третьего лица
    /// Камера ВСЕГДА сзади-сверху персонажа
    /// Мышь вращает персонажа (yaw) — камера следует
    /// </summary>
    public class ThirdPersonCamera : MonoBehaviour
    {
        [Header("Цель")]
        [Tooltip("Персонаж за которым следовать")]
        [SerializeField] private Transform target;

        [Tooltip("Персонаж (для получения yaw)")]
        [SerializeField] private Transform targetBody;

        [Header("Орбита")]
        [Tooltip("Дистанция от цели")]
        [SerializeField] private float distance = 5f;

        [Tooltip("Высота камеры относительно цели")]
        [SerializeField] private float height = 2f;

        [Header("Смещение орбиты")]
        [Tooltip("Смещение угла камеры относительно персонажа (градусы)")]
        [SerializeField] private float orbitPitch = 15f;

        // Ввод
        private InputAction _lookAction;
        private Vector2 _lookInput;

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
                Debug.LogError("[ThirdPersonCamera] Target не назначен!");
                return;
            }
            UpdateCameraPosition();
        }

        private void LateUpdate()
        {
            if (target == null) return;

            _lookInput = _lookAction.ReadValue<Vector2>();
            UpdateCameraPosition();
        }

        private void UpdateCameraPosition()
        {
            if (targetBody == null)
            {
                // Если body не назначен — используем target
                targetBody = target;
            }

            // Камера сзади персонажа (на основе yaw персонажа)
            float yaw = targetBody.eulerAngles.y * Mathf.Deg2Rad;
            float pitch = orbitPitch * Mathf.Deg2Rad;

            // Позиция камеры позади цели
            Vector3 dir = new Vector3(
                Mathf.Sin(yaw) * Mathf.Cos(pitch),
                Mathf.Sin(pitch),
                Mathf.Cos(yaw) * Mathf.Cos(pitch)
            );

            transform.position = target.position - dir * distance + Vector3.up * height;
            transform.LookAt(target.position + Vector3.up * 1.5f);
        }
    }
}
