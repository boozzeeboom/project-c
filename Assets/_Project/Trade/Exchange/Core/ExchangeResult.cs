namespace ProjectC.Trade.Core
{
    /// <summary>
    /// T-E02: Результат операции обмена (Pack/Unpack).
    ///
    /// НЕ является INetworkSerializable — результат возвращается серверным RPC
    /// через отдельный ExchangeResultDto в T-E03.
    ///
    /// Содержит: успех/неудача, сообщение, дельты для UI.
    /// </summary>
    public readonly struct ExchangeResult
    {
        public readonly bool IsSuccess;
        public readonly string Message;
        public readonly string WarehouseItemId;
        public readonly int WarehouseDelta;   // +/- количество на складе
        public readonly int InventoryDelta;   // +/- количество в инвентаре

        private ExchangeResult(bool success, string message,
            string warehouseItemId, int warehouseDelta, int inventoryDelta)
        {
            IsSuccess = success;
            Message = message;
            WarehouseItemId = warehouseItemId;
            WarehouseDelta = warehouseDelta;
            InventoryDelta = inventoryDelta;
        }

        public static ExchangeResult Ok(string message,
            string warehouseItemId, int warehouseDelta, int inventoryDelta)
            => new ExchangeResult(true, message,
                warehouseItemId, warehouseDelta, inventoryDelta);

        public static ExchangeResult Fail(string message,
            string warehouseItemId = null)
            => new ExchangeResult(false, message,
                warehouseItemId ?? string.Empty, 0, 0);
    }
}
