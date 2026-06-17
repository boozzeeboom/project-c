using UnityEngine;
using Unity.Netcode;
using ProjectC.Player;

namespace ProjectC.Ship
{
    /// <summary>
    /// ShipRootReference — маркер-ссылка на корневой GameObject корабля.
    /// Вешается на любую часть составного корабля (PilotSeat, Door, ModuleSlot).
    /// Даёт быстрый путь к ShipController / Rigidbody / NetworkObject.
    ///
    /// Паттерн: вместо GetComponentInParent<ShipController>() любая часть
    /// корабля может найти корень через GetComponentInParent<ShipRootReference>().
    ///
    /// Phase 0 (Composite Ship): базовый маркер. Расширение в Phase 4+ (multi-crew).
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public class ShipRootReference : MonoBehaviour
    {
        [Header("Cached References (read-only)")]
        [Tooltip("ShipController на корне корабля")]
        [SerializeField] private ShipController _shipController;

        [Tooltip("Rigidbody на корне корабля")]
        [SerializeField] private Rigidbody _rigidbody;

        [Tooltip("NetworkObject на корне корабля")]
        [SerializeField] private NetworkObject _networkObject;

        [Tooltip("Корневой Transform корабля (transform.root)")]
        [SerializeField] private Transform _root;

        /// <summary>
        /// ShipController на корне корабля. Может быть null если скрипт
        /// не висит на корне (например, на отдельном объекте без ShipController).
        /// </summary>
        public ShipController ShipController => _shipController;

        /// <summary>
        /// Корневой Rigidbody корабля.
        /// </summary>
        public Rigidbody ShipRigidbody => _rigidbody;

        /// <summary>
        /// Корневой NetworkObject корабля.
        /// </summary>
        public NetworkObject ShipNetworkObject => _networkObject;

        /// <summary>
        /// Корневой Transform корабля.
        /// </summary>
        public Transform ShipRoot => _root;

        private void Awake()
        {
            ResolveReferences();
        }

        /// <summary>
        /// (Пере)инициализировать ссылки на корневой корабль.
        /// Вызывается из Awake автоматически; можно дёрнуть вручную
        /// если компонент добавляется динамически.
        /// </summary>
        public void ResolveReferences()
        {
            _root = transform.root;

            if (_root == null)
            {
                Debug.LogWarning($"[ShipRootReference] '{gameObject.name}' не имеет корня (не в иерархии).");
                return;
            }

            _shipController = _root.GetComponent<ShipController>();
            _rigidbody = _root.GetComponent<Rigidbody>();
            _networkObject = _root.GetComponent<NetworkObject>();

            if (_shipController == null)
            {
                Debug.LogWarning(
                    $"[ShipRootReference] '{gameObject.name}': на корне '{_root.name}' " +
                    "не найден ShipController. Убедись, что это действительно часть корабля."
                );
            }
        }
    }
}
