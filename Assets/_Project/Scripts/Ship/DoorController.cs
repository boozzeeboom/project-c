using UnityEngine;
using System.Collections;
using ProjectC.Player;

namespace ProjectC.Ship
{
    /// <summary>
    /// DoorController — дверь на составном корабле.
    /// Анимация slide (сдвиг в сторону). Для MVP — локальная,
    /// без NetworkVariable. Каждый клиент проигрывает свою анимацию.
    ///
    /// E-key открывает/закрывает. Если на том же GameObject есть
    /// MetaRequirement — дверь можно запереть (требуется ключ).
    ///
    /// Иерархия:
    ///   Ship_Root
    ///   └── Door (этот компонент + BoxCollider IsTrigger)
    ///       └── Model_Door (3D модель, сдвигается анимацией)
    ///
    /// Phase 3 (Composite Ship): MVP дверь.
    /// Phase 4+: NetworkBehaviour + NetworkVariable<bool> IsOpen.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    [DisallowMultipleComponent]
    public class DoorController : MonoBehaviour
    {
        [Header("Анимация")]
        [Tooltip("Направление сдвига (локальное, относительно Model_Door)")]
        [SerializeField] private Vector3 slideDirection = Vector3.right;

        [Tooltip("Дистанция сдвига в единицах")]
        [SerializeField] private float slideDistance = 2f;

        [Tooltip("Скорость анимации (м/с)")]
        [SerializeField] private float slideSpeed = 1.5f;

        [Tooltip("Модель двери (дочерний GameObject) — анимируется сдвиг")]
        [SerializeField] private Transform doorModel;

        [Header("Состояние")]
        [SerializeField] private bool startOpen = false;

        /// <summary>
        /// Ссылка на корневой корабль (опционально).
        /// </summary>
        public ShipController ShipController { get; private set; }

        // Состояние
        private Vector3 _closedPos;
        private Vector3 _openPos;
        private bool _isOpen;
        private bool _isAnimating;
        private Coroutine _animCoroutine;

        private void Awake()
        {
            // Найти ShipController через корень (опционально — дверь может быть и не на корабле)
            var rootRef = GetComponentInParent<ShipRootReference>();
            if (rootRef != null)
                ShipController = rootRef.ShipController;

            // Если модель не назначена — используем transform этого объекта
            if (doorModel == null)
                doorModel = transform;

            // Запомнить закрытую позицию
            _closedPos = doorModel.localPosition;
            _openPos = _closedPos + slideDirection.normalized * slideDistance;
            _isOpen = startOpen;

            if (startOpen)
                doorModel.localPosition = _openPos;
        }

        /// <summary>
        /// Переключить дверь (открыть/закрыть).
        /// Вызывается из NetworkPlayer при E-взаимодействии.
        /// </summary>
        public void Toggle()
        {
            if (_isAnimating) return;

            if (_animCoroutine != null)
                StopCoroutine(_animCoroutine);

            _isOpen = !_isOpen;
            Vector3 target = _isOpen ? _openPos : _closedPos;
            _animCoroutine = StartCoroutine(AnimateSlide(target));
        }

        /// <summary>
        /// Открыть дверь принудительно.
        /// </summary>
        public void Open()
        {
            if (_isOpen || _isAnimating) return;
            Toggle();
        }

        /// <summary>
        /// Закрыть дверь принудительно.
        /// </summary>
        public void Close()
        {
            if (!_isOpen || _isAnimating) return;
            Toggle();
        }

        private IEnumerator AnimateSlide(Vector3 target)
        {
            _isAnimating = true;
            Vector3 start = doorModel.localPosition;
            float distance = Vector3.Distance(start, target);
            float duration = distance / Mathf.Max(slideSpeed, 0.01f);

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                doorModel.localPosition = Vector3.Lerp(start, target, t);
                yield return null;
            }

            doorModel.localPosition = target;
            _isAnimating = false;
        }

        public bool IsOpen => _isOpen;

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (doorModel == null) return;

            // Визуализация направления и дистанции сдвига
            Gizmos.color = Color.green;
            Vector3 from = doorModel.position;
            Vector3 to = from + transform.TransformDirection(slideDirection.normalized * slideDistance);
            Gizmos.DrawLine(from, to);
            Gizmos.DrawSphere(to, 0.15f);
        }
#endif
    }
}
