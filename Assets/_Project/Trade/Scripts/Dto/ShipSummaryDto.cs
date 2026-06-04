using Unity.Netcode;

namespace ProjectC.Trade.Dto
{
    /// <summary>
    /// Краткая информация о корабле в зоне рынка — для multi-ship selection.
    /// </summary>
    public struct ShipSummaryDto : INetworkSerializable
    {
        public ulong shipNetworkObjectId;
        public string displayName;        // "Корабль #3" или имя из префаба
        public string shipClassName;      // "Light" / "Medium" / "HeavyI" / "HeavyII"
        public float currentWeight;
        public float maxWeight;
        public float currentVolume;
        public float maxVolume;
        public int currentSlots;
        public int maxSlots;
        public int uniqueItemCount;       // для UI: «N типов в трюме»

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref shipNetworkObjectId);
            serializer.SerializeValue(ref displayName);
            serializer.SerializeValue(ref shipClassName);
            serializer.SerializeValue(ref currentWeight);
            serializer.SerializeValue(ref maxWeight);
            serializer.SerializeValue(ref currentVolume);
            serializer.SerializeValue(ref maxVolume);
            serializer.SerializeValue(ref currentSlots);
            serializer.SerializeValue(ref maxSlots);
            serializer.SerializeValue(ref uniqueItemCount);
        }
    }
}
