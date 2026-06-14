using UnityEngine;

namespace ProjectC.UI
{
    /// <summary>
    /// Билборд — всегда смотрит на активную камеру.
    ///
    /// Использование:
    ///   1. Повесить на любой GameObject (TextMeshPro, World Space Canvas, спрайт).
    ///   2. Активная камера выставляется автоматически через
    ///      ThirdPersonCamera.InitializeCamera() → Billboard.ActiveCamera.
    ///   3. Если ActiveCamera не задан — падает на Camera.main (для редактора/тестов).
    ///
    /// Без кода: Billboard сам найдёт MainCamera если ничего не задано.
    /// С камерой от 3L: ThirdPersonCamera сам выставляет Billboard.ActiveCamera при инициализации.
    /// </summary>
    public class Billboard : MonoBehaviour
    {
        [Header("Настройки")]
        [Tooltip("Сохранять вертикальную ориентацию (не переворачивать текст вверх ногами).")]
        public bool keepVertical = true;

        // ======================================================================
        // Статика — активная камера для всех Billboard'ов в сцене.
        // Выставляется ThirdPersonCamera.InitializeCamera() или вручную.
        // ======================================================================
        public static Transform ActiveCamera { get; set; }

        private Transform _cam;

        private void Start()
        {
            ResolveCamera();
        }

        private void LateUpdate()
        {
            // Если статик обновился (другая камера стала активной) — подхватываем
            if (_cam != ActiveCamera)
                ResolveCamera();

            if (_cam == null)
                return;

            if (keepVertical)
            {
                transform.LookAt(
                    transform.position + _cam.rotation * Vector3.forward,
                    _cam.rotation * Vector3.up
                );
            }
            else
            {
                transform.LookAt(_cam);
            }
        }

        private void ResolveCamera()
        {
            if (ActiveCamera != null)
            {
                _cam = ActiveCamera;
                return;
            }

            // Fallback для редактора / тестов без ThirdPersonCamera
            var main = Camera.main;
            if (main != null)
                _cam = main.transform;
        }
    }
}
