using System;

namespace ProjectC.World.FloatingOrigin.Network
{
    /// <summary>Explicit server-session issuer. Share one instance per server session; not an authentication token.</summary>
    public sealed class GlobalMotionSession
    {
        private ulong _nextSpawn;
        public ulong SessionId { get; }

        public GlobalMotionSession(ulong sessionId)
        {
            if (sessionId == 0) throw new ArgumentOutOfRangeException(nameof(sessionId));
            SessionId = sessionId;
        }

        public static GlobalMotionSession CreateRandom()
        {
            ulong id;
            do { id = BitConverter.ToUInt64(Guid.NewGuid().ToByteArray(), 0); } while (id == 0);
            return new GlobalMotionSession(id);
        }

        public MotionStreamBinding Allocate(ulong networkObjectId)
        {
            ulong generation = checked(_nextSpawn + 1);
            _nextSpawn = generation;
            return new MotionStreamBinding
            {
                SessionId = SessionId, NetworkObjectId = networkObjectId, SpawnGeneration = generation,
                AuthorityGeneration = 1, DiscontinuityGeneration = 1, Space = MotionCoordinateSpace.World
            };
        }

        public MotionStreamBinding NextAuthority(MotionStreamBinding binding)
        {
            Validate(binding);
            binding.AuthorityGeneration = checked(binding.AuthorityGeneration + 1);
            binding.DiscontinuityGeneration = 1;
            return binding;
        }

        public MotionStreamBinding NextDiscontinuity(MotionStreamBinding binding, MotionCoordinateSpace space,
            ulong parentObjectId = 0, ulong parentSpawnGeneration = 0)
        {
            Validate(binding);
            binding.DiscontinuityGeneration = checked(binding.DiscontinuityGeneration + 1);
            binding.Space = space;
            binding.ParentNetworkObjectId = parentObjectId;
            binding.ParentSpawnGeneration = parentSpawnGeneration;
            Validate(binding);
            return binding;
        }

        private void Validate(MotionStreamBinding binding)
        {
            if (!binding.IsValid || binding.SessionId != SessionId)
                throw new ArgumentException("Binding does not belong to this session.", nameof(binding));
        }
    }
}
