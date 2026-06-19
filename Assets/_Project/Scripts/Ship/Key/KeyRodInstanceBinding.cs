// =====================================================================================
// KeyRodInstanceBinding.cs — явная привязка PickupItem → корабль (R2-SHIP-KEY-003, T-KEY-04)
// =====================================================================================
// Документация:
//   • docs/Ships/Key-subsystem/20_UNIQUE_KEY_INSTANCE.md §2.4, §3.4
//   • docs/Ships/Key-subsystem/23_ROADMAP.md T-KEY-04
//
// Назначение: явный компонент на [KeyRod_*] PickupItem в сцене. Заменяет отменённый
// auto-bootstrap (Q11, 2026-06-18). При Server Start создаёт KeyRodInstance
// через KeyRodInstanceWorld.CreateInstance с привязкой к заданному ShipController.
// T-KEY-PERSIST: KeyRodInstanceWorld инициализируется в InventoryServer.OnNetworkSpawn
// с репозиторием. KeyRodInstanceBinding НЕ вызывает CreateAndInitialize() повторно.
// CreateInstance() сам возвращает существующий instanceId если корабль уже привязан.
//
// Связь с PickupItem:
//   • KeyRodInstanceBinding НЕ заменяет PickupItem — они на одном GameObject
//   • KeyRodInstanceBinding отвечает за создание экземпляра ключа на сервере
//   • PickupItem отвечает за подбор предмета (add to inventory)
//   • При подборе: KeyRodInstanceBinding.TryGetInstanceId() возвращает instanceId,
//     который PickupItem (или InventoryServer) пробрасывает в слот инвентаря (T-KEY-05)
//
// Правила:
//   • MonoBehaviour (не NetworkBehaviour) — не требует NetworkObject на PickupItem
//   • Server-only (с проверкой IsServer)
//   • Использует Invoke loop для ожидания готовности ShipController (NetworkObjectId)
// =====================================================================================

using UnityEngine;
using Unity.Netcode;
using ProjectC.Items;
using ProjectC.Player;  // ShipController

namespace ProjectC.Ship.Key
{
    /// <summary>
    /// Явная привязка PickupItem → корабль. На каждый [KeyRod_*] GameObject
    /// в сцене добавляется этот компонент (рядом с PickupItem).
    /// </summary>
    [RequireComponent(typeof(PickupItem))]
    public class KeyRodInstanceBinding : MonoBehaviour
    {
        [Header("T-KEY-04: Привязка к кораблю (Q11, explicit)")]
        [Tooltip("ShipController корабля, к которому привязан этот ключ. Drag-and-drop в инспекторе.")]
        [SerializeField] private ShipController _ship;

        [Tooltip("ItemData этого ключа (тот же SO что в PickupItem.itemData).")]
        [SerializeField] private ItemData _keyItemData;

        [Header("Debug (read-only)")]
        [SerializeField] private int _debugInstanceId = 0;

        // ===========================================================
        // State
        // ===========================================================

        private int _instanceId = 0;
        private bool _registered = false;
        private int _retryCount = 0;
        private const int MAX_RETRIES = 15;  // ~15 seconds max wait

        private PickupItem _pickupItem;

        // ===========================================================
        // Lifecycle
        // ===========================================================

        private void Awake()
        {
            _pickupItem = GetComponent<PickupItem>();

            // T-KEY-09 (Шаг 3): если сервер уже готов и InventoryWorld есть — попытаться
            // зарегистрировать instance сразу. Fallback — Start → TryRegister с retries.
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer
                && ProjectC.Items.InventoryWorld.Instance != null)
            {
                TryRegister();
            }
        }

        private void Start()
        {
            // Server-only: clients don't create instances
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            {
                enabled = false;
                return;
            }

            // Начинаем цикл ожидания (ShipController и InventoryWorld могут быть не готовы).
            // Если уже зарегистрированы в Awake — TryRegister no-op.
            TryRegister();
        }

        /// <summary>Повторная попытка регистрации с задержкой (ожидание готовности
        /// ShipController.OnNetworkSpawn и InventoryWorld.CreateAndInitialize).</summary>
        private void TryRegister()
        {
            if (_registered) return;
            if (_retryCount >= MAX_RETRIES)
            {
                Debug.LogError($"[KeyRodInstanceBinding] Failed to register after {MAX_RETRIES} retries. " +
                               $"ship={( _ship != null ? _ship.name : "NULL")}, item={(_keyItemData != null ? _keyItemData.name : "NULL")}");
                return;
            }
            _retryCount++;

            // Проверка: ShipController готов (IsSpawned)
            if (_ship == null)
            {
                Debug.LogWarning($"[KeyRodInstanceBinding] GameObject={gameObject.name}: _ship is null. Retry {_retryCount}/{MAX_RETRIES}");
                Invoke(nameof(TryRegister), 1.0f);
                return;
            }
            if (!_ship.IsSpawned)
            {
                // Проверка: может быть не заспавнен (scene-placed NetworkObject timing)
                Invoke(nameof(TryRegister), 1.0f);
                return;
            }

            // T-KEY-PERSIST: KeyRodInstanceWorld инициализируется в InventoryServer.OnNetworkSpawn
            // с репозиторием. Не вызываем CreateAndInitialize() — он уже готов.
            if (!KeyRodInstanceWorld.IsInitialized)
            {
                Debug.LogWarning($"[KeyRodInstanceBinding] KeyRodInstanceWorld not ready yet. Retry {_retryCount}/{MAX_RETRIES}");
                Invoke(nameof(TryRegister), 1.0f);
                return;
            }

            // Проверка: InventoryWorld готов для резолва itemId
            if (_keyItemData == null)
            {
                Debug.LogWarning($"[KeyRodInstanceBinding] GameObject={gameObject.name}: _keyItemData is null. Cannot resolve itemId.");
                Invoke(nameof(TryRegister), 1.0f);
                return;
            }

            // Резолвим itemId (через InventoryWorld если готов, иначе прямой ID)
            int itemId = -1;
            if (InventoryWorld.Instance != null)
            {
                itemId = InventoryWorld.Instance.GetOrRegisterItemId(_keyItemData);
            }

            // Если InventoryWorld ещё не готов — ждём (но он обычно готов до ShipController)
            if (itemId <= 0)
            {
                Invoke(nameof(TryRegister), 1.0f);
                return;
            }

            // Создаём экземпляр ключа (owner = NONE = ключ в мире)
            // T-KEY-PERSIST: если instance уже есть (из persistence), CreateInstance вернёт
            // существующий instanceId (guard внутри KeyRodInstanceWorld.CreateInstance).
            ulong shipNetId = _ship.NetworkObjectId;
            _instanceId = KeyRodInstanceWorld.CreateInstance(itemId, shipNetId, KeyRodInstance.OWNER_NONE);

            if (_instanceId > 0)
            {
                _registered = true;
                _debugInstanceId = _instanceId;
                Debug.Log($"[KeyRodInstanceBinding] Registered: keyRod={_keyItemData.name}, " +
                          $"ship={_ship.name} (netId={shipNetId}), " +
                          $"itemId={itemId}, instanceId={_instanceId}");
            }
            else
            {
                Debug.LogError($"[KeyRodInstanceBinding] CreateInstance FAILED: itemId={itemId}, " +
                               $"ship={_ship.name} (netId={shipNetId}), item={_keyItemData.name}");
                // One more retry
                Invoke(nameof(TryRegister), 2.0f);
            }
        }

        // ===========================================================
        // Public API (для PickupItem / InventoryServer, T-KEY-05)
        // ===========================================================

        /// <summary>Получить instanceId этого ключа. Вызывается из PickupItem.Collect()
        /// или InventoryServer при pickup.</summary>
        /// <param name="instanceId">instanceId если > 0, иначе 0 (не зарегистрирован).</param>
        /// <returns>True если instanceId > 0 (экземпляр создан).</returns>
        public bool TryGetInstanceId(out int instanceId)
        {
            instanceId = _instanceId;
            return _instanceId > 0;
        }

        // ===========================================================
        // Editor helpers
        // ===========================================================

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_keyItemData == null && _pickupItem != null)
            {
                // Автоподстановка: если не задан — берём из PickupItem
                if (_pickupItem.itemData != null && _pickupItem.itemData.itemType == ItemType.Key)
                {
                    _keyItemData = _pickupItem.itemData;
                }
            }
        }

        private void Reset()
        {
            // При добавлении через AddComponent — подхватываем PickupItem
            _pickupItem = GetComponent<PickupItem>();
            if (_pickupItem != null) _keyItemData = _pickupItem.itemData;
        }
#endif
    }
}
