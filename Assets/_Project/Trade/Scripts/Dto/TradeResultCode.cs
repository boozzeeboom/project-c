namespace ProjectC.Trade
{
    /// <summary>
    /// Код результата торговой операции. Передаётся клиенту через DTO,
    /// на клиенте маппится в локализованное сообщение.
    /// </summary>
    public enum TradeResultCode
    {
        Ok = 0,

        // Общие
        InvalidArgs = 1,
        InternalError = 2,
        NotInZone = 3,
        RateLimited = 4,

        // Рынок
        MarketNotFound = 10,
        ItemNotInMarket = 11,
        InsufficientStock = 12,
        ItemBuyDisabled = 13,
        ItemSellDisabled = 14,
        PriceInvalid = 15,
        FactionRestricted = 16,

        // Склад
        ItemNotInWarehouse = 20,
        WarehouseFullWeight = 21,
        WarehouseFullVolume = 22,
        WarehouseFullTypes = 23,

        // Корабль
        ShipNotFound = 30,
        ShipNotInZone = 31,
        ItemNotInCargo = 32,
        CargoFullWeight = 33,
        CargoFullVolume = 34,
        CargoFullSlots = 35,

        // Безопасность
        NotOwner = 36,

        // Финансы
        InsufficientCredits = 40,
    }
}
