using System;
using Unity.Netcode;

namespace ProjectC.World.FloatingOrigin.Network
{
    public enum MotionCoordinateSpace : byte
    {
        World = 0,
        ParentLocal = 1
    }

    public enum MotionRejectReason
    {
        None,
        InvalidBinding,
        UnsupportedVersion,
        InvalidSnapshot,
        NotBound,
        WrongBinding,
        StaleBinding,
        OldSequence,
        TimeRegression
    }

    /// <summary>
    /// T-FO04A. Identity of a single continuous motion stream. Generations are issued by
    /// a trusted lifecycle layer, NEVER adopted from an arriving movement packet.
    /// Object ID zero is legal; session and lifetime generations must be nonzero.
    /// </summary>
    [Serializable]
    public struct MotionStreamBinding : IEquatable<MotionStreamBinding>, INetworkSerializable
    {
        public ulong SessionId;
        public ulong NetworkObjectId;
        public ulong SpawnGeneration;
        public ulong AuthorityGeneration;
        public ulong DiscontinuityGeneration;
        public MotionCoordinateSpace Space;
        public ulong ParentNetworkObjectId;
        public ulong ParentSpawnGeneration;

        public bool IsValid
        {
            get
            {
                if (SessionId == 0 || SpawnGeneration == 0 || AuthorityGeneration == 0 || DiscontinuityGeneration == 0)
                    return false;
                if (Space == MotionCoordinateSpace.World)
                    return ParentNetworkObjectId == 0 && ParentSpawnGeneration == 0;
                return Space == MotionCoordinateSpace.ParentLocal && ParentSpawnGeneration != 0 &&
                    ParentNetworkObjectId != NetworkObjectId;
            }
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            if (serializer.IsWriter && !IsValid)
                throw new InvalidOperationException("Invalid motion stream binding.");
            var value = this;
            byte space = (byte)value.Space;
            serializer.SerializeValue(ref value.SessionId);
            serializer.SerializeValue(ref value.NetworkObjectId);
            serializer.SerializeValue(ref value.SpawnGeneration);
            serializer.SerializeValue(ref value.AuthorityGeneration);
            serializer.SerializeValue(ref value.DiscontinuityGeneration);
            serializer.SerializeValue(ref space);
            serializer.SerializeValue(ref value.ParentNetworkObjectId);
            serializer.SerializeValue(ref value.ParentSpawnGeneration);
            if (serializer.IsReader)
            {
                value.Space = (MotionCoordinateSpace)space;
                if (!value.IsValid) throw new InvalidOperationException("Invalid received motion stream binding.");
                this = value;
            }
        }

        public bool Equals(MotionStreamBinding other)
        {
            return SessionId == other.SessionId && NetworkObjectId == other.NetworkObjectId &&
                SpawnGeneration == other.SpawnGeneration && AuthorityGeneration == other.AuthorityGeneration &&
                DiscontinuityGeneration == other.DiscontinuityGeneration && Space == other.Space &&
                ParentNetworkObjectId == other.ParentNetworkObjectId && ParentSpawnGeneration == other.ParentSpawnGeneration;
        }

        public override bool Equals(object obj) => obj is MotionStreamBinding other && Equals(other);
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = SessionId.GetHashCode();
                hash = hash * 397 ^ NetworkObjectId.GetHashCode();
                hash = hash * 397 ^ SpawnGeneration.GetHashCode();
                hash = hash * 397 ^ AuthorityGeneration.GetHashCode();
                hash = hash * 397 ^ DiscontinuityGeneration.GetHashCode();
                hash = hash * 397 ^ (int)Space;
                hash = hash * 397 ^ ParentNetworkObjectId.GetHashCode();
                return hash * 397 ^ ParentSpawnGeneration.GetHashCode();
            }
        }

        public static bool operator ==(MotionStreamBinding a, MotionStreamBinding b) => a.Equals(b);
        public static bool operator !=(MotionStreamBinding a, MotionStreamBinding b) => !a.Equals(b);
    }
}
