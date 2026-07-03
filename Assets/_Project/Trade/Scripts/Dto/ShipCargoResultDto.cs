// =====================================================================================
// ShipCargoResultDto.cs — DTO результата cargo-операции (T-CARGO-UI-02)
// =====================================================================================
// Назначение: INetworkSerializable struct, шлётся сервером клиенту в ответ на
// RequestStoreToCargoRpc / RequestRetrieveFromCargoRpc.
//
// Паттерн: ExchangeResultDto (Trade/Scripts/Dto/).
// =====================================================================================

using Unity.Netcode;

namespace ProjectC.Trade.Dto
{
    public struct ShipCargoResultDto : INetworkSerializable
    {
        public bool success;
        public string message;
        public byte op;              // 0=StoreToCargo, 1=RetrieveFromCargo
        public int cargoDelta;       // +/- сколько изменилось в трюме
        public int inventoryDelta;   // +/- сколько изменилось в инвентаре

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref success);
            SerializeString(serializer, ref message);
            serializer.SerializeValue(ref op);
            serializer.SerializeValue(ref cargoDelta);
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
