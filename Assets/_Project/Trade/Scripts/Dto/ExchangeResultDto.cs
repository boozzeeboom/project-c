using Unity.Netcode;

namespace ProjectC.Trade.Dto
{
    /// <summary>
    /// T-E03: Результат операции обмена (Pack/Unpack), шлётся сервером клиенту.
    ///
    /// Аналог TradeResultDto для рынка. INetworkSerializable — NGO 2.x.
    ///
    /// Поля:
    ///   success     — true = операция выполнена
    ///   message     — строка для UI (локализованная, на русском)
    ///   op          — 0 = Pack, 1 = Unpack (byte)
    ///   warehouseItemId — itemId склада, который изменился (для UI обновления)
    ///   warehouseDelta  — +/- сколько коробок изменилось на складе
    ///   inventoryDelta  — +/- сколько предметов изменилось в инвентаре
    /// </summary>
    public struct ExchangeResultDto : INetworkSerializable
    {
        public bool success;
        public string message;
        public byte op;              // 0=Pack, 1=Unpack
        public string warehouseItemId;
        public int warehouseDelta;
        public int inventoryDelta;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref success);
            SerializeString(serializer, ref message);
            serializer.SerializeValue(ref op);
            SerializeString(serializer, ref warehouseItemId);
            serializer.SerializeValue(ref warehouseDelta);
            serializer.SerializeValue(ref inventoryDelta);
        }

        private static void SerializeString<T>(BufferSerializer<T> s, ref string val)
            where T : IReaderWriter
        {
            bool hasValue = !string.IsNullOrEmpty(val);
            s.SerializeValue(ref hasValue);
            if (hasValue)
            {
                if (s.IsReader) val = string.Empty;
                s.SerializeValue(ref val);
            }
            else
            {
                if (s.IsReader) val = null;
            }
        }
    }
}
