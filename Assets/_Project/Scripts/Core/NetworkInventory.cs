using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using ProjectC.Items;
using ProjectC.Player;

namespace ProjectC.Core
{
    /// <summary>
    /// Сетевая синхронизация инвентаря через NetworkVariable.
    /// R2-001: Использует InventoryData (INetworkSerializable) для надёжной синхронизации.
    /// Server-authoritative: клиент шлёт RPC → сервер валидирует → обновляет NetworkVariable.
    /// NetworkVariable автоматически реплицирует изменения всем клиентам.
    /// </summary>
    public class NetworkInventory : NetworkBehaviour
    {
        [Header("Настройки")]
        [SerializeField] private int maxSlots = 32;

        // R2-001: NetworkVariable с InventoryData (INetworkSerializable)
        private NetworkVariable<InventoryData> _inventoryData = new NetworkVariable<InventoryData>(
            new InventoryData(true),
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        // Кэш для преобразования ID → ItemData (локальный словарь)
        private static Dictionary<int, ItemData> _itemDatabase = new Dictionary<int, ItemData>();

        // Событие изменения инвентаря (для UI)
        public System.Action OnInventoryChanged;

        // Текущие предметы (локальный кэш)
        private List<ItemData> _items = new List<ItemData>();

        /// <summary>
        /// Текущие предметы в инвентаре
        /// </summary>
        public List<ItemData> Items => _items;

        /// <summary>
        /// Количество предметов
        /// </summary>
        public int Count => _items.Count;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            // Подписываемся на изменения
            _inventoryData.OnValueChanged += OnInventoryDataChanged;

            // Загружаем предметы из NetworkVariable в локальный кэш
            SyncLocalCache();
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();

            if (_inventoryData != null)
            {
                _inventoryData.OnValueChanged -= OnInventoryDataChanged;
            }
        }

        /// <summary>
        /// R2-001: Обработка изменения NetworkVariable через InventoryData
        /// </summary>
        private void OnInventoryDataChanged(InventoryData previousValue, InventoryData newValue)
        {
            SyncLocalCache();
            OnInventoryChanged?.Invoke();
        }

        /// <summary>
        /// R2-001: Синхронизировать локальный кэш из InventoryData
        /// </summary>
        private void SyncLocalCache()
        {
            _items.Clear();

            var data = _inventoryData.Value;
            if (data.TotalCount == 0)
                return;

            // Проходим по всем типам предметов
            foreach (ItemType type in System.Enum.GetValues(typeof(ItemType)))
            {
                var ids = data.GetIdsForType(type);
                if (ids == null) continue;

                foreach (int itemId in ids)
                {
                    if (_itemDatabase.TryGetValue(itemId, out var itemData))
                    {
                        _items.Add(itemData);
                    }
                }
            }
        }

        /// <summary>
        /// R2-001: Клиент запрашивает подбор предмета (server-authoritative)
        /// </summary>
        [Rpc(SendTo.Server)]
        public void PickupItemServerRpc(int itemId, ItemType itemType, Vector3 pickupPosition, RpcParams rpcParams = default)
        {
            Debug.Log($"[NetworkInventory] ServerRpc: itemId={itemId}, type={itemType}, from client {OwnerClientId}");

            // Валидация: проверяем дистанцию (анти-чит)
            var serverPlayer = GetComponent<NetworkPlayer>();
            if (serverPlayer == null)
            {
                Debug.LogWarning("[NetworkInventory] NetworkPlayer не найден!");
                return;
            }

            float dist = Vector3.Distance(serverPlayer.transform.position, pickupPosition);
            Debug.Log($"[NetworkInventory] Дистанция: {dist:F1}м (порог: 5м)");
            if (dist > 5f)
            {
                Debug.LogWarning($"[NetworkInventory] Игрок {OwnerClientId} пытается подобрать предмет на расстоянии {dist}м (анти-чит)");
                return;
            }

            // Валидация: предмет существует
            if (!_itemDatabase.ContainsKey(itemId))
            {
                Debug.LogWarning($"[NetworkInventory] Предмет с ID {itemId} не найден на сервере.");
                return;
            }

            // R2-001: Проверка места в инвентаре через InventoryData
            var data = _inventoryData.Value;
            if (data.TotalCount >= maxSlots)
            {
                Debug.LogWarning($"[NetworkInventory] Инвентарь игрока {OwnerClientId} полон ({data.TotalCount}/{maxSlots})");
                return;
            }

            // R2-001: Добавляем предмет через InventoryData
            data.AddItem(itemType, itemId);
            _inventoryData.Value = data;
            Debug.Log($"[NetworkInventory] Предмет ID={itemId} добавлен! Всего: {data.TotalCount}");

            // Уведомляем клиента об успехе
            PickupResultClientRpc(itemId, true);
        }

        /// <summary>
        /// Результат подбора предмета (клиенту)
        /// </summary>
        [Rpc(SendTo.Owner)]
        private void PickupResultClientRpc(int itemId, bool success, RpcParams rpcParams = default)
        {
            if (!success) return;
            // Синхронизация уже произошла через NetworkVariable
        }

        /// <summary>
        /// R2-001: Добавить предмет напрямую (для сервера или тестов)
        /// </summary>
        public void AddItem(int itemId, ItemType itemType)
        {
            if (!IsServer && !IsHost)
            {
                Debug.LogWarning("[NetworkInventory] AddItem можно вызывать только на сервере");
                return;
            }

            var data = _inventoryData.Value;
            if (data.TotalCount >= maxSlots)
            {
                Debug.LogWarning($"[NetworkInventory] Инвентарь полон");
                return;
            }

            data.AddItem(itemType, itemId);
            _inventoryData.Value = data;
        }

        /// <summary>
        /// R2-001: Добавить несколько предметов
        /// </summary>
        public void AddMultipleItems(List<(int itemId, ItemType itemType)> items)
        {
            if (!IsServer && !IsHost) return;

            var data = _inventoryData.Value;
            foreach (var (itemId, itemType) in items)
            {
                if (data.TotalCount >= maxSlots) break;
                data.AddItem(itemType, itemId);
            }
            _inventoryData.Value = data;
        }

        /// <summary>
        /// Зарегистрировать предмет в базе данных (вызывать при старте)
        /// </summary>
        public static void RegisterItem(int itemId, ItemData itemData)
        {
            if (itemData != null)
            {
                _itemDatabase[itemId] = itemData;
            }
        }

        /// <summary>
        /// Получить ID предмета по ItemData
        /// </summary>
        public static int GetItemId(ItemData itemData)
        {
            if (itemData == null) return -1;

            // Сначала ищем по ссылке
            foreach (var kvp in _itemDatabase)
            {
                if (kvp.Value == itemData) return kvp.Key;
            }

            // Если не нашли — регистрируем автоматически
            int newId = _itemDatabase.Count + 1;
            RegisterItem(newId, itemData);
            Debug.Log($"[NetworkInventory] Авто-регистрация: ID {newId} - {itemData.itemName}");
            return newId;
        }

        /// <summary>
        /// R2-001: Очистить инвентарь
        /// </summary>
        public void Clear()
        {
            if (!IsServer && !IsHost) return;
            _inventoryData.Value = new InventoryData(true);
        }
    }
}

