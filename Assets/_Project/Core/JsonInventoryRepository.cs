// T-X0: IInventoryRepository + JsonInventoryRepository.
// См. docs/NPC_quests/09_OPEN_QUESTIONS.md §D1 (inventory persistence — fix BEFORE quest rewards).
//
// T-X0 scope: per-client JSON file в Application.persistentDataPath/inventory_<clientId>.json.
// Save on every AddItemDirect / TryRemove (T-Q14). Load on player connect.
//
// Реюз Repository pattern из Trade (ProjectC.Trade.Repository.IPlayerDataRepository):
// но Trade.PlayerPrefsRepository — единый монолитный store. Здесь — отдельный per-client
// JSON file, потому что inventory data небольшой (~1-5 KB), не хочется весь PlayerPrefs
// перезаписывать на каждое изменение.

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using ProjectC.Items;

namespace ProjectC.Core
{
    /// <summary>
    /// Repository для character inventory persistence. Один файл на clientId.
    /// </summary>
    public interface IInventoryRepository
    {
        /// <summary>Load persisted inventory for clientId. Returns default (empty) InventoryData if file not found.</summary>
        InventoryData Load(ulong clientId);

        /// <summary>Save inventory for clientId. Fire-and-forget (не debounce, per §H).</summary>
        void Save(ulong clientId, InventoryData inventory);

        /// <summary>Delete saved file (e.g. на admin reset).</summary>
        void Delete(ulong clientId);
    }

    /// <summary>
    /// Plain DTO для JSON serialization. JsonUtility не сериализует private fields
    /// InventoryData, поэтому создаём посредника с public lists.
    /// </summary>
    [Serializable]
    public class InventorySaveData
    {
        public ulong clientId;
        public long savedAtUnix;

        // 8 list'ов, parallel to InventoryData._xxxIds
        public List<int> resourceIds = new List<int>();
        public List<int> equipmentIds = new List<int>();
        public List<int> foodIds = new List<int>();
        public List<int> fuelIds = new List<int>();
        public List<int> antigravIds = new List<int>();
        public List<int> meziyIds = new List<int>();
        public List<int> medicalIds = new List<int>();
        public List<int> techIds = new List<int>();
    }

    /// <summary>
    /// Default impl: пишет JSON файлы в Application.persistentDataPath/inventory_<clientId>.json.
    /// File-locking per clientId (lock объект) — для thread-safety на случай concurrent saves.
    /// </summary>
    public class JsonInventoryRepository : IInventoryRepository
    {
        private readonly object _ioLock = new object();

        private string GetPath(ulong clientId)
        {
            return Path.Combine(Application.persistentDataPath, $"inventory_{clientId}.json");
        }

        public InventoryData Load(ulong clientId)
        {
            var path = GetPath(clientId);
            lock (_ioLock)
            {
                if (!File.Exists(path)) return new InventoryData(initialize: true);
                try
                {
                    var json = File.ReadAllText(path);
                    var dto = JsonUtility.FromJson<InventorySaveData>(json);
                    if (dto == null) return new InventoryData(initialize: true);
                    return ConvertToInventoryData(dto);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[JsonInventoryRepository] Load failed for client {clientId}: {ex.Message}. Returning empty inventory.");
                    return new InventoryData(initialize: true);
                }
            }
        }

        public void Save(ulong clientId, InventoryData inventory)
        {
            var path = GetPath(clientId);
            var dto = ConvertToSaveData(clientId, inventory);
            lock (_ioLock)
            {
                try
                {
                    var json = JsonUtility.ToJson(dto, prettyPrint: false);
                    File.WriteAllText(path, json);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[JsonInventoryRepository] Save failed for client {clientId}: {ex.Message}");
                }
            }
        }

        public void Delete(ulong clientId)
        {
            var path = GetPath(clientId);
            lock (_ioLock)
            {
                if (File.Exists(path))
                {
                    try { File.Delete(path); }
                    catch (Exception ex) { Debug.LogError($"[JsonInventoryRepository] Delete failed: {ex.Message}"); }
                }
            }
        }

        // ============ Converters ============

        private static InventorySaveData ConvertToSaveData(ulong clientId, InventoryData inv)
        {
            var dto = new InventorySaveData
            {
                clientId = clientId,
                savedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };
            // InventoryData exposes GetIdsForType(ItemType). For each ItemType, copy list.
            foreach (ItemType t in System.Enum.GetValues(typeof(ItemType)))
            {
                var ids = inv.GetIdsForType(t);
                if (ids == null) continue;
                switch (t)
                {
                    case ItemType.Resources: dto.resourceIds.AddRange(ids); break;
                    case ItemType.Equipment: dto.equipmentIds.AddRange(ids); break;
                    case ItemType.Food: dto.foodIds.AddRange(ids); break;
                    case ItemType.Fuel: dto.fuelIds.AddRange(ids); break;
                    case ItemType.Antigrav: dto.antigravIds.AddRange(ids); break;
                    case ItemType.Meziy: dto.meziyIds.AddRange(ids); break;
                    case ItemType.Medical: dto.medicalIds.AddRange(ids); break;
                    case ItemType.Tech: dto.techIds.AddRange(ids); break;
                }
            }
            return dto;
        }

        private static InventoryData ConvertToInventoryData(InventorySaveData dto)
        {
            var inv = new InventoryData(initialize: true);
            if (dto.resourceIds != null) foreach (var id in dto.resourceIds) inv.AddItem(ItemType.Resources, id);
            if (dto.equipmentIds != null) foreach (var id in dto.equipmentIds) inv.AddItem(ItemType.Equipment, id);
            if (dto.foodIds != null) foreach (var id in dto.foodIds) inv.AddItem(ItemType.Food, id);
            if (dto.fuelIds != null) foreach (var id in dto.fuelIds) inv.AddItem(ItemType.Fuel, id);
            if (dto.antigravIds != null) foreach (var id in dto.antigravIds) inv.AddItem(ItemType.Antigrav, id);
            if (dto.meziyIds != null) foreach (var id in dto.meziyIds) inv.AddItem(ItemType.Meziy, id);
            if (dto.medicalIds != null) foreach (var id in dto.medicalIds) inv.AddItem(ItemType.Medical, id);
            if (dto.techIds != null) foreach (var id in dto.techIds) inv.AddItem(ItemType.Tech, id);
            return inv;
        }
    }
}
