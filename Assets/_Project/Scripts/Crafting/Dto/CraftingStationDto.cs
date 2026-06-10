// =====================================================================================
// CraftingStationDto.cs - staticheskaya info o stancii dlya ClientState snapshot
// (Project C: The Clouds, T-C02)
// Pattern: Read-only metadata. Sent once on subscribe, then snapshot deltas.
// =====================================================================================
using Unity.Netcode;
using Unity.Collections;

namespace ProjectC.Crafting
{
    public struct CraftingStationDto : INetworkSerializable
    {
        public ulong stationNetId;
        public string displayName;
        public byte stationType;        // StationType cast -> byte
        public int[] allowedRecipeIds;  // [RecipeData id mapping]

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref stationNetId);
            var name = displayName ?? "";
            s.SerializeValue(ref name);
            if (s.IsReader) displayName = name;
            s.SerializeValue(ref stationType);
            // int[] serialization
            int len = allowedRecipeIds != null ? allowedRecipeIds.Length : 0;
            s.SerializeValue(ref len);
            if (s.IsReader) allowedRecipeIds = new int[len];
            for (int i = 0; i < len; i++)
            {
                s.SerializeValue(ref allowedRecipeIds[i]);
            }
        }
    }
}