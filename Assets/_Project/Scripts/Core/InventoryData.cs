using System;
using System.Collections.Generic;
using Unity.Netcode;

namespace ProjectC.Items
{
    /// <summary>
    /// (T-KEY-02, R2-SHIP-KEY-003) Слот на один предмет с возможностью
    /// хранения instanceId. Для обычных предметов (Resources/Food/...)
    /// используется только itemId (instanceId = 0). Для Key-предметов
    /// instanceId > 0 привязывает к KeyRodInstance.
    /// </summary>
    [Serializable]
    public struct InventorySlot : INetworkSerializable
    {
        public int itemId;
        public int instanceId;  // 0 = non-instance item (обычный предмет)

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref itemId);
            serializer.SerializeValue(ref instanceId);
        }
    }

    /// <summary>
    /// Network-serializable структура для передачи инвентаря.
    /// Используется NetworkVariable для автоматической синхронизации между клиентами.
    /// Формат: список ID предметов для каждого типа.
    /// 
    /// R2-001: Реализует INetworkSerializable для надёжной синхронизации.
    /// T-KEY-02: добавлена поддержка ItemType.Key с InventorySlot (instance-id слой).
    /// </summary>
    [Serializable]
    public struct InventoryData : INetworkSerializable
    {
        // 8 списков ID для каждого типа предмета (ItemType.Resources..Tech, 0..7)
        private List<int> _resourceIds;
        private List<int> _equipmentIds;
        private List<int> _foodIds;
        private List<int> _fuelIds;
        private List<int> _antigravIds;
        private List<int> _meziyIds;
        private List<int> _medicalIds;
        private List<int> _techIds;

        // T-KEY-02: for ItemType.Key (=8) — parallel lists
        // _keyIds: List<int> itemIds (для backward compat с HasItem/HasAllItems/CountOf)
        // _keySlots: List<InventorySlot> full data (itemId + instanceId)
        private List<int> _keyIds;
        private List<InventorySlot> _keySlots;

        public InventoryData(bool initialize = true)
        {
            if (initialize)
            {
                _resourceIds = new List<int>();
                _equipmentIds = new List<int>();
                _foodIds = new List<int>();
                _fuelIds = new List<int>();
                _antigravIds = new List<int>();
                _meziyIds = new List<int>();
                _medicalIds = new List<int>();
                _techIds = new List<int>();
                _keyIds = new List<int>();
                _keySlots = new List<InventorySlot>();
            }
            else
            {
                _resourceIds = null;
                _equipmentIds = null;
                _foodIds = null;
                _fuelIds = null;
                _antigravIds = null;
                _meziyIds = null;
                _medicalIds = null;
                _techIds = null;
                _keyIds = null;
                _keySlots = null;
            }
        }

        /// <summary>
        /// Получить список ID для типа предмета.
        /// Для ItemType.Key возвращает itemIds из _keyIds (parallel к _keySlots).
        /// Для всех остальных — существующий List&lt;int&gt;.
        /// </summary>
        public List<int> GetIdsForType(ItemType type)
        {
            return type switch
            {
                ItemType.Resources => _resourceIds,
                ItemType.Equipment => _equipmentIds,
                ItemType.Food => _foodIds,
                ItemType.Fuel => _fuelIds,
                ItemType.Antigrav => _antigravIds,
                ItemType.Meziy => _meziyIds,
                ItemType.Medical => _medicalIds,
                ItemType.Tech => _techIds,
                ItemType.Key => _keyIds,
                _ => new List<int>()
            };
        }

        /// <summary>
        /// Добавить предмет по типу и ID. Для ItemType.Key использует instanceId=0
        /// (не-instance). Для создания Key с instanceId используйте AddKeyItem.
        /// </summary>
        public void AddItem(ItemType type, int itemId)
        {
            switch (type)
            {
                case ItemType.Resources: _resourceIds ??= new List<int>(); _resourceIds.Add(itemId); break;
                case ItemType.Equipment: _equipmentIds ??= new List<int>(); _equipmentIds.Add(itemId); break;
                case ItemType.Food: _foodIds ??= new List<int>(); _foodIds.Add(itemId); break;
                case ItemType.Fuel: _fuelIds ??= new List<int>(); _fuelIds.Add(itemId); break;
                case ItemType.Antigrav: _antigravIds ??= new List<int>(); _antigravIds.Add(itemId); break;
                case ItemType.Meziy: _meziyIds ??= new List<int>(); _meziyIds.Add(itemId); break;
                case ItemType.Medical: _medicalIds ??= new List<int>(); _medicalIds.Add(itemId); break;
                case ItemType.Tech: _techIds ??= new List<int>(); _techIds.Add(itemId); break;
                case ItemType.Key:
                    if (_keyIds == null) _keyIds = new List<int>();
                    if (_keySlots == null) _keySlots = new List<InventorySlot>();
                    _keyIds.Add(itemId);
                    _keySlots.Add(new InventorySlot { itemId = itemId, instanceId = 0 });
                    break;
            }
        }

        // ============================================================
        // T-KEY-02: Key-specific API (instance-id слой)
        // ============================================================

        /// <summary>Добавить Key с instanceId. Поддерживает обе параллельные структуры.</summary>
        public void AddKeyItem(int itemId, int instanceId)
        {
            if (_keyIds == null) _keyIds = new List<int>();
            if (_keySlots == null) _keySlots = new List<InventorySlot>();
            _keyIds.Add(itemId);
            _keySlots.Add(new InventorySlot { itemId = itemId, instanceId = instanceId });
        }

        /// <summary>Получить InventorySlot для Key по индексу. index: позиция в _keySlots.</summary>
        public InventorySlot GetKeySlotAt(int index)
        {
            if (_keySlots == null || index < 0 || index >= _keySlots.Count)
                return new InventorySlot { itemId = -1, instanceId = 0 };
            return _keySlots[index];
        }

        /// <summary>Количество Key-слотов.</summary>
        public int KeySlotCount => _keySlots?.Count ?? 0;

        /// <summary>Удалить Key slot по индексу (из обеих параллельных структур).</summary>
        public void RemoveKeySlotAt(int index)
        {
            if (_keySlots == null || _keyIds == null) return;
            if (index < 0 || index >= _keySlots.Count) return;
            _keySlots.RemoveAt(index);
            _keyIds.RemoveAt(index);
        }

        /// <summary>Удалить один Key slot с указанным instanceId. Возвращает true если найден.</summary>
        public bool RemoveKeyByInstanceId(int instanceId)
        {
            if (_keySlots == null) return false;
            for (int i = 0; i < _keySlots.Count; i++)
            {
                if (_keySlots[i].instanceId == instanceId)
                {
                    RemoveKeySlotAt(i);
                    return true;
                }
            }
            return false;
        }

        /// <summary>Найти instanceId Key слотов для указанного itemId.</summary>
        public List<int> GetKeyInstanceIdsForItem(int itemId)
        {
            var result = new List<int>();
            if (_keySlots == null) return result;
            for (int i = 0; i < _keySlots.Count; i++)
            {
                if (_keySlots[i].itemId == itemId)
                    result.Add(_keySlots[i].instanceId);
            }
            return result;
        }

        /// <summary>True если есть Key slot с указанным instanceId.</summary>
        public bool HasKeyInstance(int instanceId)
        {
            if (_keySlots == null) return false;
            for (int i = 0; i < _keySlots.Count; i++)
            {
                if (_keySlots[i].instanceId == instanceId) return true;
            }
            return false;
        }

        /// <summary>T-KEY-07: установить instanceId в последний Key-слот (после pickup).
        /// Вызывается из InventoryServer.RequestPickupRpc после AddItem + TransferInstance.</summary>
        public void SetLastKeySlotInstanceId(int instanceId)
        {
            if (instanceId <= 0) return;
            if (_keySlots == null || _keySlots.Count == 0) return;
            var slot = _keySlots[_keySlots.Count - 1];
            if (slot.instanceId != instanceId)
            {
                slot.instanceId = instanceId;
                _keySlots[_keySlots.Count - 1] = slot;
            }
        }

        /// <summary>Получить instanceId для всех Key-слотов (для persistence).</summary>
        public List<int> GetKeyInstanceIds()
        {
            var result = new List<int>();
            if (_keySlots == null) return result;
            for (int i = 0; i < _keySlots.Count; i++)
                result.Add(_keySlots[i].instanceId);
            return result;
        }

        /// <summary>
        /// Общее количество предметов
        /// </summary>
        public int TotalCount
        {
            get
            {
                int count = 0;
                if (_resourceIds != null) count += _resourceIds.Count;
                if (_equipmentIds != null) count += _equipmentIds.Count;
                if (_foodIds != null) count += _foodIds.Count;
                if (_fuelIds != null) count += _fuelIds.Count;
                if (_antigravIds != null) count += _antigravIds.Count;
                if (_meziyIds != null) count += _meziyIds.Count;
                if (_medicalIds != null) count += _medicalIds.Count;
                if (_techIds != null) count += _techIds.Count;
                if (_keySlots != null) count += _keySlots.Count;  // T-KEY-02
                return count;
            }
        }

        // ==================== INetworkSerializable ====================

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            // Serialize each list individually (Resources..Tech are backward compat)
            SerializeIntList(serializer, ref _resourceIds);
            SerializeIntList(serializer, ref _equipmentIds);
            SerializeIntList(serializer, ref _foodIds);
            SerializeIntList(serializer, ref _fuelIds);
            SerializeIntList(serializer, ref _antigravIds);
            SerializeIntList(serializer, ref _meziyIds);
            SerializeIntList(serializer, ref _medicalIds);
            SerializeIntList(serializer, ref _techIds);
            // T-KEY-02: Key slots (InventorySlot[])
            SerializeSlotList(serializer, ref _keySlots);
        }

        private static void SerializeIntList<T>(BufferSerializer<T> serializer, ref List<int> list) where T : IReaderWriter
        {
            if (serializer.IsReader)
            {
                int count = 0;
                serializer.SerializeValue(ref count);
                if (count > 0)
                {
                    list = new List<int>(count);
                    for (int i = 0; i < count; i++)
                    {
                        int id = 0;
                        serializer.SerializeValue(ref id);
                        list.Add(id);
                    }
                }
                else
                {
                    list = new List<int>();
                }
            }
            else
            {
                int count = list != null ? list.Count : 0;
                serializer.SerializeValue(ref count);
                if (count > 0 && list != null)
                {
                    for (int i = 0; i < count; i++)
                    {
                        int id = list[i];
                        serializer.SerializeValue(ref id);
                    }
                }
            }
        }

        /// <summary>T-KEY-02: serialization for InventorySlot list (Key items).</summary>
        private static void SerializeSlotList<T>(BufferSerializer<T> serializer, ref List<InventorySlot> list) where T : IReaderWriter
        {
            if (serializer.IsReader)
            {
                int count = 0;
                serializer.SerializeValue(ref count);
                if (count > 0)
                {
                    list = new List<InventorySlot>(count);
                    for (int i = 0; i < count; i++)
                    {
                        var slot = new InventorySlot();
                        slot.NetworkSerialize(serializer);
                        list.Add(slot);
                    }
                }
                else
                {
                    list = new List<InventorySlot>();
                }
            }
            else
            {
                int count = list != null ? list.Count : 0;
                serializer.SerializeValue(ref count);
                if (count > 0 && list != null)
                {
                    for (int i = 0; i < count; i++)
                    {
                        var slot = list[i];
                        slot.NetworkSerialize(serializer);
                    }
                }
            }
        }
    }
}