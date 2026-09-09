using System;
using Unity.Netcode;

namespace ProjectC.World.FloatingOrigin.Network
{
    public enum GlobalMotionAuthority : byte { Server, Owner }

    /// <summary>Reliable lifecycle/keyframe envelope. Default is a legal unconfigured initial-sync state.</summary>
    [Serializable]
    public struct GlobalMotionControl : INetworkSerializable
    {
        public bool HasStream;
        public bool IsActive;
        public ulong Revision;
        public GlobalMotionAuthority Authority;
        public ulong PublisherClientId;
        public GlobalMotionSnapshot Baseline;

        public bool IsValid => !HasStream || (Revision != 0 &&
            (Authority == GlobalMotionAuthority.Server || Authority == GlobalMotionAuthority.Owner) && Baseline.TryValidate(out _));

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            if (serializer.IsWriter && !IsValid) throw new InvalidOperationException("Invalid motion control.");
            ushort version = GlobalMotionSnapshot.CurrentVersion;
            serializer.SerializeValue(ref version);
            if (version != GlobalMotionSnapshot.CurrentVersion) throw new InvalidOperationException("Unsupported motion control version.");
            var value = this;
            serializer.SerializeValue(ref value.HasStream);
            if (value.HasStream)
            {
                byte authority = (byte)value.Authority;
                serializer.SerializeValue(ref value.IsActive);
                serializer.SerializeValue(ref value.Revision);
                serializer.SerializeValue(ref authority);
                serializer.SerializeValue(ref value.PublisherClientId);
                value.Authority = (GlobalMotionAuthority)authority;
                value.Baseline.NetworkSerialize(serializer);
            }
            else value = default;
            if (serializer.IsReader)
            {
                if (!value.IsValid) throw new InvalidOperationException("Invalid received motion control.");
                this = value;
            }
        }
    }

    /// <summary>Authenticated reliable-control ordering and unreliable receive buffer, independent of MonoBehaviour.</summary>
    public sealed class GlobalMotionControlReceiver
    {
        private readonly GlobalMotionBuffer _buffer = new GlobalMotionBuffer();
        public GlobalMotionControl Control { get; private set; }
        public int BufferedCount => _buffer.Count;

        public bool TryApply(GlobalMotionControl incoming, ulong sender, ulong serverId, ulong objectId)
        {
            if (sender != serverId || !incoming.HasStream || !incoming.IsValid ||
                incoming.Baseline.Binding.NetworkObjectId != objectId ||
                (incoming.Authority == GlobalMotionAuthority.Server && incoming.PublisherClientId != serverId)) return false;
            var previous = Control;
            if (previous.HasStream)
            {
                var a = previous.Baseline.Binding;
                var b = incoming.Baseline.Binding;
                if (incoming.Revision <= previous.Revision || a.SessionId != b.SessionId ||
                    a.NetworkObjectId != b.NetworkObjectId || a.SpawnGeneration != b.SpawnGeneration) return false;
                bool advanced = b.AuthorityGeneration > a.AuthorityGeneration ||
                    (b.AuthorityGeneration == a.AuthorityGeneration && b.DiscontinuityGeneration > a.DiscontinuityGeneration);
                if (a != b && !advanced) return false;
                if ((previous.PublisherClientId != incoming.PublisherClientId || previous.Authority != incoming.Authority) &&
                    b.AuthorityGeneration <= a.AuthorityGeneration) return false;
                if (!previous.IsActive && incoming.IsActive && !advanced) return false;
            }
            if (!incoming.IsActive) _buffer.EndStream();
            else if (!_buffer.BeginStream(incoming.Baseline.Binding, incoming.Baseline, out MotionRejectReason reason))
            {
                // Reliable keyframe may lag a newer unreliable sample. Keep that newer pose.
                if (reason != MotionRejectReason.OldSequence || !previous.IsActive ||
                    previous.Baseline.Binding != incoming.Baseline.Binding) return false;
                incoming.Baseline = previous.Baseline;
            }
            Control = incoming;
            return true;
        }

        public bool TryAdd(GlobalMotionSnapshot snapshot, ulong sender, ulong serverId)
        {
            if (sender != serverId || !Control.HasStream || !Control.IsActive || !_buffer.TryAdd(snapshot, out _)) return false;
            var updated = Control;
            updated.Baseline = snapshot;
            Control = updated;
            return true;
        }

        public bool TrySample(double time, out GlobalMotionPose pose) => _buffer.TrySample(time, out pose);
        public void Reset() { _buffer.EndStream(); Control = default; }
    }
}
