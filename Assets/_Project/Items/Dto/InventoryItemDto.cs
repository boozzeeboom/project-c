// =====================================================================================
// InventoryItemDto.cs — один предмет в инвентаре (Project C: The Clouds)
// =====================================================================================
// Документация:
//   • docs/dev/INVENTORY_V2_REFACTOR.md — Phase 1 (DTO)
//
// Назначение: минимальный INetworkSerializable для одного stack'а в инвентаре.
// Передаётся внутри InventorySnapshotDto.items[]. NOT содержит ItemData (definition)
// — definition'ы — это локальный кэш (SO в Resources/Items), их не шлём по сети.
//
// Поля:
//   • itemId     — уникальный ID в ItemDatabase (см. InventoryWorld._itemDatabase)
//   • type       — (byte)ItemType, для быстрой фильтрации в UI без lookup'а definition
//   • quantity   — stack count (1..maxStack; maxStack хранится в ItemData.maxStack)
//   • slotIndex  — позиция в инвентаре [0, maxSlots); -1 означает "нет слота" (не используется
//                  в DTO, но reserved для будущих операций drop с авто-выбором слота).
//   • instanceId — (T-KEY-02, R2-SHIP-KEY-003) 0 для обычных предметов; >0 для Key-предметов
//                  с привязкой к KeyRodInstance.
// =====================================================================================

using System;
using Unity.Netcode;

namespace ProjectC.Items.Dto
{
    [Serializable]
    public struct InventoryItemDto : INetworkSerializable, IEquatable<InventoryItemDto>
    {
        public int    itemId;
        public byte   type;        // (byte)ItemType
        public int    quantity;
        public int    slotIndex;
        public int    instanceId;  // T-KEY-02: 0 = non-instance, >0 = KeyRodInstance.instanceId
        public string itemName;    // R2-fix: имя предмета прямо в DTO (не зависит от клиентского кэша)

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref itemId);
            serializer.SerializeValue(ref type);
            serializer.SerializeValue(ref quantity);
            serializer.SerializeValue(ref slotIndex);
            serializer.SerializeValue(ref instanceId);
            serializer.SerializeValue(ref itemName);
        }

        public bool Equals(InventoryItemDto other)
        {
            return itemId == other.itemId
                && type == other.type
                && quantity == other.quantity
                && slotIndex == other.slotIndex
                && instanceId == other.instanceId
                && itemName == other.itemName;
        }

        public override bool Equals(object obj) => obj is InventoryItemDto o && Equals(o);
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + itemId;
                hash = hash * 31 + type;
                hash = hash * 31 + quantity;
                hash = hash * 31 + slotIndex;
                hash = hash * 31 + instanceId;
                hash = hash * 31 + (itemName?.GetHashCode() ?? 0);
                return hash;
            }
        }
    }
}