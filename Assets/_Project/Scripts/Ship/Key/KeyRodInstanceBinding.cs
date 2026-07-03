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
        }

        private void Start()
        {
            // Server-only: clients don't create instances
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            {
                enabled = false;
                return;
            }

            // T-KEY-09 (Шаг 3): единственная попытка регистрации в Start.
            // Все зависимости (KeyRodInstanceWorld, InventoryWorld, ShipController.IsSpawned)
            // гарантированно готовы — scene-placed NetworkObject спавнится после BootstrapScene.
            // Если регистрация не удалась — silent fail (instanceId=0 при pickup).
            TryRegister();
        }

                /// <summary>Однократная регистрация instance через KeyRodInstanceWorld.
        /// Если не удалось — silent fail, pickup будет с instanceId=0.</summary>
        private void TryRegister()
        {
            if (_registered) return;

            // Проверки с retry (макс 15 попыток, ~15 сек)
            if (_ship == null || !_ship.IsSpawned || !KeyRodInstanceWorld.IsInitialized || _keyItemData == null)
            {
                if (_retryCount < MAX_RETRIES)
                {
                    _retryCount++;
                    Debug.Log($"[KeyRodInstanceBinding] Retry {_retryCount}/{MAX_RETRIES}: " +
                              $"ship={(_ship != null ? _ship.name : "NULL")}, " +
                              $"spawned={(_ship != null && _ship.IsSpawned)}, " +
                              $"krwInit={KeyRodInstanceWorld.IsInitialized}, " +
                              $"itemData={(_keyItemData != null ? _keyItemData.name : "NULL")}");
                    Invoke(nameof(TryRegister), 1.0f);
                }
                else
                {
                    Debug.LogError($"[KeyRodInstanceBinding] Failed to register after {MAX_RETRIES} retries. " +
                                   $"ship={(_ship != null ? _ship.name : "NULL")}, " +
                                   $"item={(_keyItemData != null ? _keyItemData.name : "NULL")}. " +
                                   $"Pickup will have instanceId=0.");
                }
                return;
            }

            // ItemId
            int itemId = InventoryWorld.Instance != null
                ? InventoryWorld.Instance.GetOrRegisterItemId(_keyItemData) : -1;
            if (itemId <= 0) return;

            // Создаём экземпляр ключа (owner = NONE = ключ в мире)
            // T-KEY-PERSIST: если instance уже есть (из persistence), CreateInstance вернёт существующий id
            _instanceId = KeyRodInstanceWorld.CreateInstance(itemId, _ship.NetworkObjectId,
                KeyRodInstance.OWNER_NONE);
            if (_instanceId > 0)
            {
                _registered = true;
                Debug.Log($"[KeyRodInstanceBinding] Registered: gameObject={gameObject.name}, " +
                          $"instanceId={_instanceId}, ship={_ship.name}({_ship.NetworkObjectId})");
            }
        }

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
