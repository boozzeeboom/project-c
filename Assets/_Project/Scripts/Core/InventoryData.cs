using System;
using System.Collections.Generic;
using Unity.Netcode;

namespace ProjectC.Items
{
    /// <summary>
    /// Network-serializable структура для передачи инвентаря.
    /// Используется NetworkVariable для автоматической синхронизации между клиентами.
    /// Формат: список ID предметов для каждого типа.
    /// 
    /// R2-001: Реализует INetworkSerializable для надёжной синхронизации.
    /// </summary>
    [Serializable]
    public struct InventoryData : INetworkSerializable
    {
        // 8 списков ID для каждого типа предмета
        private List<int> _resourceIds;
        private List<int> _equipmentIds;
        private List<int> _foodIds;
        private List<int> _fuelIds;
        private List<int> _antigravIds;
        private List<int> _meziyIds;
        private List<int> _medicalIds;
        private List<int> _techIds;

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
            }
        }

        /// <summary>
        /// Получить список ID для типа предмета
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
                _ => new List<int>()
            };
        }

        /// <summary>
        /// Добавить предмет по типу и ID
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
            }
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
                return count;
            }
        }

        // ==================== INetworkSerializable ====================

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            // Serialize each list individually
            SerializeList(serializer, ref _resourceIds);
            SerializeList(serializer, ref _equipmentIds);
            SerializeList(serializer, ref _foodIds);
            SerializeList(serializer, ref _fuelIds);
            SerializeList(serializer, ref _antigravIds);
            SerializeList(serializer, ref _meziyIds);
            SerializeList(serializer, ref _medicalIds);
            SerializeList(serializer, ref _techIds);
        }

        private static void SerializeList<T>(BufferSerializer<T> serializer, ref List<int> list) where T : IReaderWriter
        {
            if (serializer.IsReader)
            {
                // Reader mode: deserialize
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
                // Writer mode: serialize
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
    }
}