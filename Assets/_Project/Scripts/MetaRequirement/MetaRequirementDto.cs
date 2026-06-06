// =====================================================================================
// MetaRequirementDto.cs — INetworkSerializable DTO (Project C: The Clouds)
// =====================================================================================
// Документация:
//   • docs/MetaRequirement/00_OVERVIEW.md
//
// Назначение: компактная DTO для RPC Push к клиенту. Содержит:
//   • interactableNetworkObjectId — ключ словаряка в MetaRequirementClientState
//   • itemIds[] — массив требуемых itemId (0 если не зарезолвлен)
//   • displayName — для toast'а и UI
//   • logic (byte) — RequirementLogic enum
//   • requiredCount — для AtLeastN
//   • consumeOnUse — для будущего v2
//
// NB: используем FixedString для экономии аллокаций на каждом RPC push'е
// (как в ShipKeyBindingDto из подсистемы Ship Key).
// =====================================================================================

using Unity.Collections;
using Unity.Netcode;

namespace ProjectC.MetaRequirement
{
    public struct MetaRequirementDto : INetworkSerializable
    {
        public ulong interactableNetworkObjectId;
        public FixedString64Bytes displayName;
        public int[] itemIds;
        public byte logic;            // (byte)RequirementLogic
        public int requiredCount;     // для AtLeastN
        public bool consumeOnUse;     // для будущего

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref interactableNetworkObjectId);
            serializer.SerializeValue(ref displayName);
            serializer.SerializeValue(ref logic);
            serializer.SerializeValue(ref requiredCount);
            serializer.SerializeValue(ref consumeOnUse);

            // Serialize int[] (variable-length via magic helpers)
            int len = itemIds != null ? itemIds.Length : 0;
            serializer.SerializeValue(ref len);
            if (serializer.IsReader)
            {
                itemIds = new int[len];
            }
            for (int i = 0; i < len; i++)
            {
                serializer.SerializeValue(ref itemIds[i]);
            }
        }
    }
}
