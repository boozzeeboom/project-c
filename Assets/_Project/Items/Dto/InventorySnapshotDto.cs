// =====================================================================================
// InventorySnapshotDto.cs — снимок инвентаря игрока (Project C: The Clouds)
// =====================================================================================
// Документация:
//   • docs/dev/INVENTORY_V2_REFACTOR.md — Phase 1 (DTO)
//
// Назначение: проекция server-state инвентаря на конкретный момент. Шлётся с сервера
// на клиент через NetworkPlayer.ReceiveInventorySnapshotTargetRpc. Клиент обновляет
// InventoryClientState.CurrentSnapshot и дёргает OnSnapshotUpdated — UI (TAB-колесо
// и P-таб CharacterWindow) подписаны на это событие.
//
// Поля:
//   • locationId — id зоны, в которой находится игрок (для cross-tab фильтрации;
//                  null если не в зоне). String, не enum — будущие локации
//                  могут иметь произвольные id (см. MarketSnapshotDto.locationId).
//   • items      — массив всех stack'ов (включая пустые слоты как quantity=0 —
//                  inventory list UI рендерит все 32 слота для предсказуемости).
//                  Может быть null (на старте, до первого snapshot'а).
//   • maxSlots   — размер инвентаря (32 по умолчанию; см. NetworkInventory.maxSlots).
//   • credits    — кэш кредитов (для P-таб "Персонаж" — кросс-доменная зависимость
//                  с MarketClientState; см. unity-v2-subsystem-migration pitfall #12).
//
// NGO 2.x serialisation gotchas (см. unity-v2-subsystem-migration §3.2):
//   • string — manual serialize с hasLoc flag (consistency: null vs empty).
//   • T[] — manual length-prefixed serialize, NOT null-safe by default.
// =====================================================================================

using System;
using Unity.Netcode;

namespace ProjectC.Items.Dto
{
    [Serializable]
    public struct InventorySnapshotDto : INetworkSerializable
    {
        public string locationId;
        public InventoryItemDto[] items;
        public int   maxSlots;
        public float credits;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            // --- locationId (string, nullable) ---
            // Пишем bool hasLoc + string. На reader: если hasLoc — читаем, иначе null.
            bool hasLoc = !string.IsNullOrEmpty(locationId);
            serializer.SerializeValue(ref hasLoc);
            if (hasLoc)
            {
                if (serializer.IsReader) locationId = string.Empty; // allocate before re-serialise
                serializer.SerializeValue(ref locationId);
            }
            else
            {
                if (serializer.IsReader) locationId = null;
            }

            // --- items (T[], length-prefixed) ---
            int len = items != null ? items.Length : 0;
            serializer.SerializeValue(ref len);
            if (serializer.IsReader)
            {
                items = len > 0 ? new InventoryItemDto[len] : null;
            }
            for (int i = 0; i < len; i++)
            {
                var x = items[i];
                x.NetworkSerialize(serializer);
                items[i] = x;
            }

            // --- maxSlots, credits ---
            serializer.SerializeValue(ref maxSlots);
            serializer.SerializeValue(ref credits);
        }
    }
}
