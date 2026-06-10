// BufferedIngredientDto.cs (T-C02) - ingredient laid on the station buffer (pre-craft)
using Unity.Netcode;
namespace ProjectC.Crafting
{
    public struct BufferedIngredientDto : INetworkSerializable
    {
        public int itemId;
        public int quantity;
        public byte source;        // CraftingSourceType cast -> byte
        public ulong ownerClientId;

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref itemId);
            s.SerializeValue(ref quantity);
            s.SerializeValue(ref source);
            s.SerializeValue(ref ownerClientId);
        }
    }

    public struct CommittedIngredientDto : INetworkSerializable
    {
        public int itemId;
        public int quantity;
        public ulong ownerClientId;

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref itemId);
            s.SerializeValue(ref quantity);
            s.SerializeValue(ref ownerClientId);
        }
    }
}