using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using ProjectC.Items;

namespace ProjectC.Core
{
    /// <summary>
    /// Сетевая синхронизация инвентаря через NetworkVariable.
    /// Server-authoritative: клиент шлёт RPC → сервер валидирует → обновляет NetworkVariable.
    /// NetworkVariable автоматически реплицирует изменения всем клиентам.
    /// Использует строку (CSV) для хранения списка ID предметов.
    /// </summary>
    public class NetworkInventory : NetworkBehaviour
    {
        [Header("Настройки")]
        [SerializeField] private int maxSlots = 32;

        // NetworkVariable для синхронизации предметов (CSV строка ID)
        // Формат: "1,5,3,12" — ID предметов через запятую
        private NetworkVariable<string> _itemIdsString = new NetworkVariable<string>(
            "",
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
            _itemIdsString.OnValueChanged += OnItemIdsChanged;

            // Загружаем предметы из NetworkVariable в локальный кэш
            SyncLocalCache();
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();

            if (_itemIdsString != null)
            {
                _itemIdsString.OnValueChanged -= OnItemIdsChanged;
            }
        }

        /// <summary>
        /// Обработка изменения NetworkVariable
        /// </summary>
        private void OnItemIdsChanged(string previousValue, string newValue)
        {
            SyncLocalCache();
            OnInventoryChanged?.Invoke();
        }

        /// <summary>
        /// Синхронизировать локальный кэш из NetworkVariable
        /// </summary>
        private void SyncLocalCache()
        {
            _items.Clear();

            if (string.IsNullOrEmpty(_itemIdsString.Value))
                return;

            string[] ids = _itemIdsString.Value.Split(',');
            foreach (var idStr in ids)
            {
                if (int.TryParse(idStr, out int itemId))
                {
                    if (_itemDatabase.TryGetValue(itemId, out var itemData))
                    {
                        _items.Add(itemData);
                    }
                }
            }
        }

        /// <summary>
        /// Клиент запрашивает подбор предмета (server-authoritative)
        /// </summary>
        [Rpc(SendTo.Server)]
        public void PickupItemServerRpc(int itemId, Vector3 pickupPosition, RpcParams rpcParams = default)
        {
            // Валидация: проверяем дистанцию (анти-чит)
            var serverPlayer = GetComponent<NetworkPlayer>();
            if (serverPlayer == null) return;

            float dist = Vector3.Distance(serverPlayer.transform.position, pickupPosition);
            if (dist > 5f) // Максимальная дистанция подбора
            {
                Debug.LogWarning($"[NetworkInventory] Игрок {OwnerClientId} пытается подобрать предмет на расстоянии {dist}м (анти-чит)");
                return;
            }

            // Валидация: предмет существует
            if (!_itemDatabase.ContainsKey(itemId))
            {
                Debug.LogWarning($"[NetworkInventory] Предмет с ID {itemId} не найден в базе");
                return;
            }

            // Проверка места в инвентаре
            var currentIds = GetCurrentItemIds();
            if (currentIds.Count >= maxSlots)
            {
                Debug.LogWarning($"[NetworkInventory] Инвентарь игрока {OwnerClientId} полон");
                return;
            }

            // Добавляем предмет
            currentIds.Add(itemId);
            _itemIdsString.Value = string.Join(",", currentIds);

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
        /// Получить текущие ID предметов из строки
        /// </summary>
        private List<int> GetCurrentItemIds()
        {
            var ids = new List<int>();
            if (string.IsNullOrEmpty(_itemIdsString.Value))
                return ids;

            string[] parts = _itemIdsString.Value.Split(',');
            foreach (var part in parts)
            {
                if (int.TryParse(part, out int id))
                    ids.Add(id);
            }
            return ids;
        }

        /// <summary>
        /// Добавить предмет напрямую (для сервера или тестов)
        /// </summary>
        public void AddItem(int itemId)
        {
            if (!IsServer && !IsHost)
            {
                Debug.LogWarning("[NetworkInventory] AddItem можно вызывать только на сервере");
                return;
            }

            var currentIds = GetCurrentItemIds();
            if (currentIds.Count >= maxSlots)
            {
                Debug.LogWarning($"[NetworkInventory] Инвентарь полон");
                return;
            }

            currentIds.Add(itemId);
            _itemIdsString.Value = string.Join(",", currentIds);
        }

        /// <summary>
        /// Добавить несколько предметов
        /// </summary>
        public void AddMultipleItems(List<int> itemIds)
        {
            if (!IsServer && !IsHost) return;

            foreach (var itemId in itemIds)
            {
                AddItem(itemId);
            }
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
            foreach (var kvp in _itemDatabase)
            {
                if (kvp.Value == itemData) return kvp.Key;
            }
            return -1;
        }

        /// <summary>
        /// Очистить инвентарь
        /// </summary>
        public void Clear()
        {
            if (!IsServer && !IsHost) return;
            _itemIdsString.Value = "";
        }
    }
}

