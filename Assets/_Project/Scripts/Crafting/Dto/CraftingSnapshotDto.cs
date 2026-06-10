// CraftingSnapshotDto.cs (T-C02) — replicated state of one station for one subscriber
// Pattern: full snapshot on subscribe, then CraftingResultDto deltas
// Fix T-C07: added `progress` field server-computed (not client-calcd, avoids clock drift)
using Unity.Netcode;
namespace ProjectC.Crafting
{
    public struct CraftingSnapshotDto : INetworkSerializable
    {
        public ulong stationNetId;
        public byte jobState;          // CraftingJobState
        public ulong ownerClientId;    // 0 = no owner
        public int activeRecipeId;     // -1 = none
        public float startTime;        // server time (NetworkManager.ServerTime.Time)
        public float duration;         // seconds
        public float progress;         // T-C07: server-computed 0..1, client uses directly (fixes clock drift)
        public BufferedIngredientDto[] buffer;
        public CommittedIngredientDto[] committed;
        public string resultItemName;  // populated on Completed (P16d)

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref stationNetId);
            s.SerializeValue(ref jobState);
            s.SerializeValue(ref ownerClientId);
            s.SerializeValue(ref activeRecipeId);
            s.SerializeValue(ref startTime);
            s.SerializeValue(ref duration);
            s.SerializeValue(ref progress);

            int bLen = buffer != null ? buffer.Length : 0;
            s.SerializeValue(ref bLen);
            if (s.IsReader) buffer = new BufferedIngredientDto[bLen];
            for (int i = 0; i < bLen; i++) { var e = buffer[i]; s.SerializeValue(ref e); buffer[i] = e; }

            int cLen = committed != null ? committed.Length : 0;
            s.SerializeValue(ref cLen);
            if (s.IsReader) committed = new CommittedIngredientDto[cLen];
            for (int i = 0; i < cLen; i++) { var e = committed[i]; s.SerializeValue(ref e); committed[i] = e; }

            var nm = resultItemName ?? "";
            s.SerializeValue(ref nm);
            if (s.IsReader) resultItemName = nm;
        }
    }
}