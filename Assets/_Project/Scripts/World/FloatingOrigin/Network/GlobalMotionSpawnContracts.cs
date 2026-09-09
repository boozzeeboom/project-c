using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectC.World.FloatingOrigin.Network
{
    /// <summary>Origin-independent factory input, separate from the subsequent authoritative motion baseline.</summary>
    [Serializable]
    public struct GlobalMotionSpawnSeed : INetworkSerializable
    {
        public const ushort CurrentVersion = 1;
        public ushort Version;
        public ulong SessionId, ObjectId, SpawnGeneration, OwnerId;
        public GlobalPosition Position;
        public Quaternion Rotation;
        public Vector3 Scale;
        public bool IsValid => Version == CurrentVersion && SessionId != 0 && SpawnGeneration != 0 && Position.IsFinite &&
            GlobalMotionSnapshot.UnitRotation(Rotation) && GlobalMotionSnapshot.Finite(Scale) && Scale.x > 0 && Scale.y > 0 && Scale.z > 0;
        public bool Matches(MotionStreamBinding binding, ulong owner) => IsValid && binding.IsValid && binding.SessionId == SessionId &&
            binding.NetworkObjectId == ObjectId && binding.SpawnGeneration == SpawnGeneration && OwnerId == owner;
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            if (serializer.IsWriter && !IsValid) throw new InvalidOperationException("Invalid global spawn seed.");
            var value = this;
            serializer.SerializeValue(ref value.Version);
            if (value.Version != CurrentVersion) throw new InvalidOperationException("Unsupported global spawn seed.");
            serializer.SerializeValue(ref value.SessionId); serializer.SerializeValue(ref value.ObjectId);
            serializer.SerializeValue(ref value.SpawnGeneration); serializer.SerializeValue(ref value.OwnerId);
            value.Position.NetworkSerialize(serializer);
            serializer.SerializeValue(ref value.Rotation); serializer.SerializeValue(ref value.Scale);
            if (!value.IsValid) throw new InvalidOperationException("Invalid global spawn seed.");
            if (serializer.IsReader) this = value;
        }
    }

    /// <summary>Already-prepared live scene and immutable coordinates. Never serialized across peers.</summary>
    public sealed class GlobalMotionSpawnFrame
    {
        public int Id { get; }
        public LocalCoordinateFrame Coordinates { get; }
        public UnityEngine.SceneManagement.Scene Scene { get; }
        public PhysicsScene Physics => Scene.GetPhysicsScene();
        public bool IsValid => Id > 0 && Coordinates.IsValid && Scene.IsValid() && Scene.isLoaded && Physics.IsValid();
        public GlobalMotionSpawnFrame(int id, LocalCoordinateFrame coordinates, UnityEngine.SceneManagement.Scene scene) { Id = id; Coordinates = coordinates; Scene = scene; }
    }

    public readonly struct GlobalMotionPlayerSpawnPlan
    {
        public int FrameId { get; }
        public GlobalPosition Position { get; }
        public Quaternion Rotation { get; }
        public Vector3 Scale { get; }
        public Func<GlobalMotionSnapshot, bool> OwnerRules { get; }
        public Guid ReservationId { get; } // Server-local only; never added to the wire seed or save format.
        public GlobalMotionPlayerSpawnPlan(int frameId, GlobalPosition position, Quaternion rotation, Vector3 scale, Func<GlobalMotionSnapshot, bool> ownerRules, Guid reservationId = default)
        { FrameId = frameId; Position = position; Rotation = rotation; Scale = scale; OwnerRules = ownerRules; ReservationId = reservationId; }
        public bool TryProject(LocalCoordinateFrame frame, ulong clientId, out Vector3 local)
        {
            local = default;
            return FrameId > 0 && (clientId == NetworkManager.ServerClientId || OwnerRules != null) &&
                GlobalMotionSnapshot.UnitRotation(Rotation) && GlobalMotionSnapshot.Finite(Scale) && Scale.x > 0 && Scale.y > 0 && Scale.z > 0 &&
                frame.TryToLocal(Position, out local);
        }
    }

    /// <summary>Trusted content/persistence/AOI source. Every method is read-only; false plan = not ready, no default spawn.</summary>
    public interface IGlobalMotionPlayerSpawnSource
    {
        bool ValidatePreparedContent(GlobalMotionStartRole role, GlobalMotionNetworkProfile profile, out string error);
        IReadOnlyList<GlobalMotionSpawnFrame> PreparedFrames { get; }
        bool TryGetPlayerPlan(ulong approvedClientId, out GlobalMotionPlayerSpawnPlan plan);
        bool TryGetReplicaFrame(GlobalMotionSpawnSeed seed, out int localFrameId);
    }
    /// <summary>Optional leased-source lifecycle. Validation is read-only; confirm/cancel are explicit server factory operations.</summary>
    public interface IGlobalMotionPlayerSpawnPlanGuard
    {
        bool ValidatePlayerPlan(ulong clientId, GlobalMotionPlayerSpawnPlan plan, out string error);
        bool ConfirmPlayerSpawn(ulong clientId, GlobalMotionPlayerSpawnPlan plan, NetworkObject player, out string error);
        void CancelPlayerPlan(ulong clientId, Guid reservationId);
        void ReleaseDisconnectedPlayer(ulong clientId);
    }
    public static class GlobalMotionSpawnPlanGuards
    {
        public static bool Validate(IGlobalMotionPlayerSpawnSource source, ulong clientId, GlobalMotionPlayerSpawnPlan plan, out string error)
        {
            error = null;
            if (source == null) { error = "spawn_source_missing"; return false; }
            var guard = source as IGlobalMotionPlayerSpawnPlanGuard;
            if (guard == null) { if (plan.ReservationId == Guid.Empty) return true; error = "reserved_plan_requires_source_guard"; return false; }
            try { return guard.ValidatePlayerPlan(clientId, plan, out error); }
            catch (Exception e) { error = "spawn_plan_guard:" + e.GetType().Name; return false; }
        }
        public static bool Confirm(IGlobalMotionPlayerSpawnSource source, ulong clientId, GlobalMotionPlayerSpawnPlan plan, NetworkObject player, out string error)
        {
            error = null;
            if (source == null) { error = "spawn_source_missing"; return false; }
            var guard = source as IGlobalMotionPlayerSpawnPlanGuard;
            if (guard == null) { if (plan.ReservationId == Guid.Empty) return true; error = "reserved_plan_requires_source_guard"; return false; }
            try { return guard.ConfirmPlayerSpawn(clientId, plan, player, out error); }
            catch (Exception e) { error = "spawn_plan_confirmation:" + e.GetType().Name; return false; }
        }
        public static void Cancel(IGlobalMotionPlayerSpawnSource source, ulong clientId, GlobalMotionPlayerSpawnPlan plan)
        {
            if (!(source is IGlobalMotionPlayerSpawnPlanGuard guard)) return;
            try { guard.CancelPlayerPlan(clientId, plan.ReservationId); }
            catch (Exception e) { Debug.LogException(e); }
        }
    }
    public interface IGlobalMotionSpawnBootstrapLifecycle
    {
        void InstallNetworkStart(NetworkManager manager, GlobalMotionStartRole role, GlobalMotionNetworkProfile profile);
        void ReleaseNetworkStart(NetworkManager manager);
    }

    /// <summary>Initial-only readiness gate. Teleport/parent handoff within a lifetime does not re-arm initial spawn.</summary>
    public sealed class GlobalMotionSpawnLatch
    {
        public MotionStreamBinding Applied { get; private set; }
        public bool Released { get; private set; }
        public void Reset() { Applied = default; Released = false; }
        public void Record(MotionStreamBinding binding)
        {
            if (!binding.IsValid) throw new ArgumentException("Invalid binding.");
            if (Applied.SessionId != binding.SessionId || Applied.NetworkObjectId != binding.NetworkObjectId || Applied.SpawnGeneration != binding.SpawnGeneration) Released = false;
            Applied = binding;
        }
        public bool TryRelease(MotionStreamBinding binding)
        { if (!binding.IsValid || binding != Applied) return false; Released = true; return true; }
    }

    public readonly struct GlobalMotionSpawnTicket : IEquatable<GlobalMotionSpawnTicket>
    {
        public ulong Epoch { get; }
        public ulong ClientId { get; }
        public ulong Serial { get; }
        public bool IsValid => Epoch != 0 && Serial != 0;
        public GlobalMotionSpawnTicket(ulong epoch, ulong clientId, ulong serial) { Epoch = epoch; ClientId = clientId; Serial = serial; }
        public bool Equals(GlobalMotionSpawnTicket other) => Epoch == other.Epoch && ClientId == other.ClientId && Serial == other.Serial;
        public override bool Equals(object other) => other is GlobalMotionSpawnTicket ticket && Equals(ticket);
        public override int GetHashCode() => Epoch.GetHashCode() ^ ClientId.GetHashCode() ^ Serial.GetHashCode();
    }
    public sealed class GlobalMotionSpawnQueue
    {
        private readonly Dictionary<ulong, GlobalMotionSpawnTicket> _tickets = new Dictionary<ulong, GlobalMotionSpawnTicket>();
        private ulong _epoch, _serial;
        public int Count => _tickets.Count;
        public int Capacity { get; }
        public GlobalMotionSpawnQueue(int capacity = 256) { if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity)); Capacity = capacity; }
        public void Begin() { _epoch = checked(_epoch + 1); _tickets.Clear(); }
        public void Clear() => _tickets.Clear();
        public bool TryAdd(ulong clientId, out GlobalMotionSpawnTicket ticket)
        {
            ticket = default;
            if (_epoch == 0 || _tickets.ContainsKey(clientId) || _tickets.Count >= Capacity) return false;
            ticket = new GlobalMotionSpawnTicket(_epoch, clientId, checked(++_serial)); _tickets.Add(clientId, ticket); return true;
        }
        public bool IsCurrent(GlobalMotionSpawnTicket ticket) => ticket.IsValid && _tickets.TryGetValue(ticket.ClientId, out var current) && current.Equals(ticket);
        public bool Complete(GlobalMotionSpawnTicket ticket) { if (!IsCurrent(ticket)) return false; return _tickets.Remove(ticket.ClientId); }
        public void Cancel(ulong clientId) => _tickets.Remove(clientId);
        public void CopyTo(List<GlobalMotionSpawnTicket> result) { result.Clear(); result.AddRange(_tickets.Values); }
    }
}
