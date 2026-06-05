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
// =====================================================================================

using System;
using Unity.Netcode;

namespace ProjectC.Items.Dto
{
    [Serializable]
    public struct InventoryItemDto : INetworkSerializable, IEquatable<InventoryItemDto>
    {
        public int  itemId;
        public byte type;        // (byte)ItemType
        public int  quantity;
        public int  slotIndex;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref itemId);
            serializer.SerializeValue(ref type);
            serializer.SerializeValue(ref quantity);
            serializer.SerializeValue(ref slotIndex);
        }

        public bool Equals(InventoryItemDto other)
        {
            return itemId == other.itemId
                && type == other.type
                && quantity == other.quantity
                && slotIndex == other.slotIndex;
        }

        public override bool Equals(object obj) => obj is InventoryItemDto o && Equals(o);
        public override int GetHashCode() => HashCode.Combine(itemId, type, quantity, slotIndex);
    }
}
