using UnityEngine;
using ProjectC.Player;

namespace ProjectC.Ship
{
    /// <summary>
    /// PilotSeatController — место пилота на составном корабле.
    /// Вешается на дочерний GameObject (с триггер-коллайдером) рядом со штурвалом.
    /// F-key ищет ближайший PilotSeatController, а не ShipController напрямую.
    ///
    /// Phase 1 (Composite Ship): базовая функциональность — попытка сесть / выйти.
    /// Phase 4+: multi-crew, разные типы мест (Pilot, Gunner, Engineer).
    ///
    /// Иерархия (задаётся в инспекторе):
    ///   Ship_Root (с ShipController)
    ///   └── PilotSeat (этот компонент, BoxCollider IsTrigger)
    /// </summary>
    [RequireComponent(typeof(Collider))]
    [DisallowMultipleComponent]
    public class PilotSeatController : MonoBehaviour
    {
        [Header("Ссылки")]
        [Tooltip("Маркер корня корабля. Если не задан — ищется в родителях автоматически.")]
        [SerializeField] private ShipRootReference shipRoot;

        [Header("Настройки")]
        [Tooltip("Тип места (для будущей multi-crew)")]
        [SerializeField] private PilotSeatType seatType = PilotSeatType.Pilot;

        [Tooltip("Радиус поиска игрока (для визуализации в Gizmos)")]
        [SerializeField] private float interactRadius = 2.5f;

        [Tooltip("Debug: логировать вход/выход")]
        [SerializeField] private bool verboseLog = true;

        /// <summary>
        /// Тип места пилота. Phase 1 — только Pilot.
        /// Phase 4+: Gunner (турели), Engineer (системы), Navigator (карта).
        /// </summary>
        public enum PilotSeatType
        {
            Pilot,      // Управление движением
            Gunner,     // Турели
            Engineer,   // Системы
            Navigator   // Карта
        }

        /// <summary>
        /// Ссылка на корневой ShipController. Кешируется в Awake.
        /// </summary>
        public ShipController ShipController { get; private set; }

        /// <summary>
        /// Тип места.
        /// </summary>
        public PilotSeatType SeatType => seatType;

        private void Awake()
        {
            // Кешируем корневой ShipController через ShipRootReference
            if (shipRoot == null)
            {
                shipRoot = GetComponentInParent<ShipRootReference>();
            }

            if (shipRoot != null && shipRoot.ShipController != null)
            {
                ShipController = shipRoot.ShipController;
            }
            else
            {
                // Фолбэк: ищем напрямую
                ShipController = GetComponentInParent<ShipController>();
                if (ShipController == null)
                {
                    Debug.LogWarning(
                        $"[PilotSeatController] '{gameObject.name}': не найден ShipController " +
                        "ни через ShipRootReference, ни через GetComponentInParent."
                    );
                }
            }

            // Гарантируем что коллайдер — триггер
            var col = GetComponent<Collider>();
            if (col != null && !col.isTrigger)
            {
                col.isTrigger = true;
                if (verboseLog)
                {
                    Debug.Log($"[PilotSeatController] '{gameObject.name}': принудительно IsTrigger=true");
                }
            }
        }

        /// <summary>
        /// Попытка сесть в место пилота (для будущего использования из NPC/AI).
        /// Phase 1: для игрока-посадки NetworkPlayer вызывает напрямую
        /// _currentShip.AddPilot(this), этот метод оставлен для совместимости и
        /// для будущей multi-crew логики.
        /// </summary>
        public bool TryBoard(ulong clientId)
        {
            if (ShipController == null)
            {
                Debug.LogWarning($"[PilotSeatController] '{gameObject.name}': нет ShipController, сесть нельзя.");
                return false;
            }

            if (verboseLog)
            {
                Debug.Log($"[PilotSeatController] Pilot seated: clientId={clientId}, seat='{gameObject.name}'");
            }
            return true;
        }

        /// <summary>
        /// Выход из места пилота.
        /// </summary>
        public void Exit(ulong clientId)
        {
            if (ShipController == null) return;
            ShipController.RemovePilot(clientId);
            if (verboseLog)
            {
                Debug.Log($"[PilotSeatController] Pilot exited: clientId={clientId}, seat='{gameObject.name}'");
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // Визуализация зоны взаимодействия
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, interactRadius);
        }
#endif
    }
}
