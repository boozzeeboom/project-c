// T-NS00: NpcShipCargoManifest + NpcCargoEntryDto — v2 forward-compat hook.
// В M1 — пустая структура (capacity=0, items=null), но DTO contract уже стабилен.
// См. docs/NPC_others_peacfull/pc_ship/03_V2_ARCHITECTURE.md §6.1 и 06_OPEN_QUESTIONS.md Q10.
//
// Pattern: WarehouseEntry (Trade/Core/Warehouse.cs) — INetworkSerializable struct.

using Unity.Netcode;

namespace ProjectC.PeacefulShip.Core
{
    /// <summary>
    /// Запись груза NPC-корабля. V2 hook — в M1 не используется.
    /// </summary>
    [System.Serializable]
    public struct NpcCargoEntryDto : INetworkSerializable
    {
        public string itemId;       // "mesium_canister_v01"
        public int quantity;
        public float unitPrice;     // v2: для journal

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref itemId);
            serializer.SerializeValue(ref quantity);
            serializer.SerializeValue(ref unitPrice);
        }
    }

    /// <summary>
    /// Cargo manifest NPC-корабля. V2 hook — в M1 всегда capacity=0, items=null.
    /// При v2 (привязка к TradeWorld): заполняется через NpcShipWorld.OnNpcShipLoaded/Unloaded.
    /// </summary>
    [System.Serializable]
    public struct NpcShipCargoManifest : INetworkSerializable
    {
        public int capacitySlots;       // v2: max slots для ship class
        public float capacityWeight;    // v2: max weight kg
        public NpcCargoEntryDto[] items; // v2: null в M1

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref capacitySlots);
            serializer.SerializeValue(ref capacityWeight);

            int len = items != null ? items.Length : 0;
            serializer.SerializeValue(ref len);

            // NGO 2.x pattern: re-create array on reader side, copy on writer side.
            if (serializer.IsReader && len > 0)
                items = new NpcCargoEntryDto[len];

            for (int i = 0; i < len; i++)
            {
                var entry = (items != null && i < items.Length) ? items[i] : default;
                serializer.SerializeValue(ref entry);
                if (items != null)
                    items[i] = entry;
            }
        }
    }
}