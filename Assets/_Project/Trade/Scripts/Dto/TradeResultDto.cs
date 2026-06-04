using Unity.Netcode;

namespace ProjectC.Trade.Dto
{
    /// <summary>
    /// Результат одной торговой операции (buy / sell / load / unload).
    /// Сервер шлёт клиенту после обработки RPC.
    /// </summary>
    public struct TradeResultDto : INetworkSerializable
    {
        public TradeResultCode code;
        public TradeOp op;                // какая операция была
        public string locationId;
        public string itemId;
        public int quantity;

        // Обновлённое состояние игрока (кэш клиента экономит round-trip)
        public float newCredits;
        public int newStock;              // availableStock на рынке после операции (0 если не применимо)
        public WarehouseEntryDto[] updatedWarehouseSnapshot;  // новый склад игрока (только changed)
        public WarehouseEntryDto[] updatedCargoSnapshot;      // новый груз корабля (только если был shipId)
        public ulong shipNetworkObjectId;  // 0 = не применимо

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            byte codeB = (byte)code;
            byte opB = (byte)op;
            serializer.SerializeValue(ref codeB);
            serializer.SerializeValue(ref opB);
            if (serializer.IsReader)
            {
                code = (TradeResultCode)codeB;
                op = (TradeOp)opB;
            }
            serializer.SerializeValue(ref locationId);
            serializer.SerializeValue(ref itemId);
            serializer.SerializeValue(ref quantity);
            serializer.SerializeValue(ref newCredits);
            serializer.SerializeValue(ref newStock);
            serializer.SerializeValue(ref shipNetworkObjectId);

            SerializeArray<T, WarehouseEntryDto>(ref updatedWarehouseSnapshot, serializer);
            SerializeArray<T, WarehouseEntryDto>(ref updatedCargoSnapshot, serializer);
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

        public bool IsSuccess => code == TradeResultCode.Ok;
    }

    public enum TradeOp : byte
    {
        Buy = 0,
        Sell = 1,
        LoadToShip = 2,
        UnloadFromShip = 3,
    }
}
