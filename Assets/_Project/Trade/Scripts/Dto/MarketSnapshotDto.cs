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

        // FIX (2026-06-04): Груз ВЫБРАННОГО корабля. Раньше cargo не входил в snapshot
        // (комментарий "слишком жирно слать"), и клиент видел cargo только из
        // updatedCargoSnapshot TradeResultDto. Результат: при открытии рынка / смене
        // корабля UI показывал СТАРЫЙ cargo из локального _cargoCache — игрок не знал,
        // что в трюме уже лежат предметы с прошлой сессии. Жал LOAD qty=1, а на сервере
        // cargo было уже 1 → получилось 2. UNLOAD 2 → на склад вернулось 2 шт. при том,
        // что куплено было 5: "эксплойт +1 бесплатный товар". Серверная логика
        // TradeWorld была корректной — баг был в UI-проекции.
        // Решение: сервер знает выбранный клиентом корабль (SetSelectedShipRpc) и
        // включает его cargo в snapshot. null/empty = трюм пуст.
        //
        // FIX (2026-06-05): Поле `cargo` теперь избыточно — см. `shipCargos` ниже.
        // Сервер по-прежнему заполняет `cargo` cargo выбранного корабля (для
        // обратной совместимости и как single-source для UI), а `shipCargos`
        // даёт клиенту per-ship кэш, чтобы при переключении корабля в ship-selector
        // UI мгновенно показывал cargo нового корабля (без ожидания следующего
        // snapshot). Подробнее — см. FIXES_HISTORY.md 2026-06-05.
        public WarehouseEntryDto[] cargo;

        // FIX (2026-06-05): cargo ДЛЯ ВСЕХ кораблей в зоне, keyed by shipNetworkObjectId.
        // Содержит по одной записи на каждый ShipSummaryDto в `nearbyShips`.
        // Серверный источник истины (TradeWorld._cargoCache[shipId]) — каждый корабль
        // это «инвентарь», груз не теряется при переключении. Клиент кэширует в
        // MarketClientState.CurrentShipCargos и переключает мгновенно.
        // null/пустой массив = в зоне 0 кораблей.
        public ShipCargoDto[] shipCargos;

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
            SerializeArray<T, WarehouseEntryDto>(ref cargo, serializer);
            SerializeArray<T, ShipCargoDto>(ref shipCargos, serializer);
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

    /// <summary>
    /// FIX (2026-06-05): Cargo одного корабля из <see cref="MarketSnapshotDto.shipCargos"/>.
    /// Сервер шлёт по одной записи на каждый nearby ship. Клиент собирает в
    /// <c>MarketClientState.CurrentShipCargos</c> (Dictionary&lt;shipId, cargo[]&gt;)
    /// и переключает мгновенно в ship-selector — без ожидания следующего snapshot.
    ///
    /// Архитектурно: каждый корабль = "инвентарь" с устойчивым состоянием на
    /// сервере (TradeWorld._cargoCache[shipId] + IPlayerDataRepository.SetCargo).
    /// Другие игроки (когда будет добавлена ownership-логика) увидят cargo,
    /// оставленное предыдущим владельцем. См. docs/Markets/FIXES_HISTORY.md 2026-06-05.
    /// </summary>
    public struct ShipCargoDto : INetworkSerializable
    {
        public ulong shipNetworkObjectId;
        public WarehouseEntryDto[] cargo;  // null/empty = трюм пуст

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref shipNetworkObjectId);
            int len = cargo?.Length ?? 0;
            serializer.SerializeValue(ref len);
            if (serializer.IsReader)
            {
                cargo = len > 0 ? new WarehouseEntryDto[len] : null;
            }
            for (int i = 0; i < len; i++)
            {
                var item = cargo[i];
                item.NetworkSerialize(serializer);
                cargo[i] = item;
            }
        }
    }
}
