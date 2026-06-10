// CraftingResultDto.cs (T-C02) - server->client ack/deny. Pattern: GatherResult T-G03
using Unity.Netcode;
namespace ProjectC.Crafting
{
    public struct CraftingResultDto : INetworkSerializable
    {
        public byte code;          // CraftingResultCode
        public ulong stationNetId;
        public string message;     // human-readable (P16d: force non-null)

        public CraftingResultCode Result => (CraftingResultCode)code;

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref code);
            s.SerializeValue(ref stationNetId);
            var m = message ?? "";
            s.SerializeValue(ref m);
            if (s.IsReader) message = m;
        }

        public static CraftingResultDto Ok(ulong netId) => new CraftingResultDto {
            code = (byte)CraftingResultCode.Ok, stationNetId = netId
        };
        public static CraftingResultDto Denied(CraftingResultCode c, string msg, ulong netId = 0) => new CraftingResultDto {
            code = (byte)c, message = msg ?? "", stationNetId = netId
        };
    }
}