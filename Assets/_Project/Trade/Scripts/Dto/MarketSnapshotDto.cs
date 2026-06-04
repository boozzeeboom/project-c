using System.Collections.Generic;
using Unity.Netcode;

namespace ProjectC.Trade.Dto
{
    /// <summary>
    /// Снепшот состояния рынка, который сервер шлёт клиенту при подписке
    /// на локацию и после каждого тика.
    ///
    /// Передаётся через NetworkVariable или RPC. NGO 2.x сериализует
    /// struct'ы напрямую, без рефлексии.
    /// </summary>
    public struct MarketSnapshotDto : INetworkSerializable
    {
        public string locationId;
        public string displayName;

        // Рынок
        public ItemPriceDto[] items;          // null/empty = пустой рынок
        public int marketVersion;             // для delta-sync

        // Склад игрока на этой локации
        public WarehouseEntryDto[] warehouse;
        public float credits;
        public float warehouseMaxWeight;
        public float warehouseMaxVolume;
        public int warehouseMaxTypes;

        // Корабли в зоне (для multi-ship)
        public ShipSummaryDto[] nearbyShips;

        // Time info
        public float marketTimeMultiplier;
        public float secondsUntilNextTick;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref locationId);
            serializer.SerializeValue(ref displayName);
            serializer.SerializeValue(ref marketVersion);
            serializer.SerializeValue(ref credits);
            serializer.SerializeValue(ref warehouseMaxWeight);
            serializer.SerializeValue(ref warehouseMaxVolume);
            serializer.SerializeValue(ref warehouseMaxTypes);
            serializer.SerializeValue(ref marketTimeMultiplier);
            serializer.SerializeValue(ref secondsUntilNextTick);

            // массивы
            SerializeArray<T, ItemPriceDto>(ref items, serializer);
            SerializeArray<T, WarehouseEntryDto>(ref warehouse, serializer);
            SerializeArray<T, ShipSummaryDto>(ref nearbyShips, serializer);
        }

        private static void SerializeArray<T, TItem>(ref TItem[] arr, BufferSerializer<T> s)
            where T : IReaderWriter where TItem : INetworkSerializable, new()
        {
            int len = arr?.Length ?? 0;
            s.SerializeValue(ref len);
            if (s.IsReader)
            {
                arr = len > 0 ? new TItem[len] : null;
            }
            for (int i = 0; i < len; i++)
            {
                var item = arr[i];
                item.NetworkSerialize(s);
                arr[i] = item;
            }
        }
    }

    public struct ItemPriceDto : INetworkSerializable
    {
        public string itemId;
        public string displayName;
        public float currentPrice;
        public int availableStock;
        public int version;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref itemId);
            serializer.SerializeValue(ref displayName);
            serializer.SerializeValue(ref currentPrice);
            serializer.SerializeValue(ref availableStock);
            serializer.SerializeValue(ref version);
        }
    }

    public struct WarehouseEntryDto : INetworkSerializable
    {
        public string itemId;
        public string displayName;
        public int quantity;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref itemId);
            serializer.SerializeValue(ref displayName);
            serializer.SerializeValue(ref quantity);
        }
    }
}
