// =====================================================================================
// ShipCargoConsole.cs — interactable компонент на дочернем объекте корабля (T-CARGO-UI-02)
// =====================================================================================
// Назначение: вешается на 3D-объект внутри корабля (например ShipRoot/CargoConsole).
// При входе игрока в триггер регистрируется в InteractableManager. По F открывает
// ShipCargoConsoleWindow для обмена inventory ↔ ship cargo.
//
// Паттерн: CraftingStation (IInteractable + trigger registration), но без NetworkBehaviour
// (корабль уже NetworkObject, дочерний объект не нуждается в отдельной сети).
// =====================================================================================

using ProjectC.Core;
using ProjectC.Player;
using UnityEngine;

namespace ProjectC.Ship.Cargo
{
    [DisallowMultipleComponent]
    public class ShipCargoConsole : MonoBehaviour, IInteractable
    {
        [Header("Interaction")]
        [Tooltip("Радиус взаимодействия (IInteractable).")]
        [Range(0.5f, 5f)]
        [SerializeField] private float _interactionRadius = 3f;

        [Header("Display")]
        [Tooltip("Имя, показываемое игроку при приближении.")]
        [SerializeField] private string _displayName = "Грузовой отсек";

        // Кэш
        private ShipController _ship;
        private SphereCollider _trigger;

        // ============================================================
        // IInteractable
        // ============================================================
        public string InstanceId =>
            _ship != null ? $"{_ship.NetworkObjectId}_cargo" : gameObject.name + "_cargo";

        public string DisplayName => _displayName;
        public float InteractionRadius => _interactionRadius;
        public Vector3 Position => transform.position;

        /// <summary>Родительский ShipController этого корабля.</summary>
        public ShipController Ship
        {
            get
            {
                if (_ship == null)
                    _ship = GetComponentInParent<ShipController>(true);
                return _ship;
            }
        }

        // ============================================================
        // Unity Lifecycle
        // ============================================================

        private void Awake()
        {
            // Убедимся что есть триггер-коллайдер
            _trigger = GetComponent<SphereCollider>();
            if (_trigger == null)
            {
                _trigger = gameObject.AddComponent<SphereCollider>();
                _trigger.isTrigger = true;
                _trigger.radius = _interactionRadius;
            }
            else
            {
                _trigger.isTrigger = true;
                if (_trigger.radius < 0.1f) _trigger.radius = _interactionRadius;
            }
        }

        private void Start()
        {
            if (Ship == null)
            {
                Debug.LogWarning($"[ShipCargoConsole] '{gameObject.name}': не найден ShipController в родителях. " +
                                 "Компонент должен быть на дочернем объекте корабля.", this);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player") || other.GetComponent<CharacterController>() != null)
            {
                InteractableManager.RegisterShipCargoConsole(this);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("Player") || other.GetComponent<CharacterController>() != null)
            {
                InteractableManager.UnregisterShipCargoConsole(this);
            }
        }

        private void OnDisable()
        {
            InteractableManager.UnregisterShipCargoConsole(this);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_trigger == null) _trigger = GetComponent<SphereCollider>();
            if (_trigger != null)
            {
                _trigger.isTrigger = true;
                _trigger.radius = _interactionRadius;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.4f);
            Gizmos.DrawSphere(transform.position, _interactionRadius);
        }
#endif
    }
}
