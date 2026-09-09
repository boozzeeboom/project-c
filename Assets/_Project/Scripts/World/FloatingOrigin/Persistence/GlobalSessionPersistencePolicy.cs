using System;
using UnityEngine;
using ProjectC.World.FloatingOrigin.Network;

namespace ProjectC.World.FloatingOrigin.Persistence
{
    /// <summary>Trusted server input, not a packet, authentication implementation or clientId-derived identity.</summary>
    public sealed class GlobalSessionPeerPlan
    {
        public string PlayerId { get; }
        public int FrameId { get; }
        public Quaternion Rotation { get; }
        public Vector3 Scale { get; }
        public Func<GlobalMotionSnapshot, bool> OwnerRules { get; }
        public GlobalPosition? FirstSpawn { get; }
        public GlobalSessionPeerPlan(string playerId, int frameId, Quaternion rotation, Vector3 scale, Func<GlobalMotionSnapshot, bool> ownerRules, GlobalPosition? firstSpawn)
        {
            if (!GlobalPlayerPositionRecord.IsValidIdentity(playerId) || frameId <= 0 || !GlobalMotionSnapshot.UnitRotation(rotation) || !GlobalMotionSnapshot.Finite(scale) ||
                scale.x <= 0 || scale.y <= 0 || scale.z <= 0 || (firstSpawn.HasValue && !firstSpawn.Value.IsFinite)) throw new ArgumentException("Explicit valid player identity and spawn policy required.");
            PlayerId = playerId; FrameId = frameId; Rotation = rotation; Scale = scale; OwnerRules = ownerRules; FirstSpawn = firstSpawn;
        }
        public bool CanServe(ulong clientId) => clientId == Unity.Netcode.NetworkManager.ServerClientId || OwnerRules != null;
    }
    public sealed class GlobalSessionTiming
    {
        public double CheckpointInterval { get; }
        public double SampleAge { get; }
        public double PlanLifetime { get; }
        public double RetryDelay { get; }
        public GlobalSessionTiming(double checkpointInterval, double sampleAge, double planLifetime, double retryDelay)
        {
            if (!GlobalPosition.IsFiniteValue(checkpointInterval) || checkpointInterval < 1 || checkpointInterval > 3600 ||
                !GlobalPosition.IsFiniteValue(sampleAge) || sampleAge <= 0 || sampleAge > GlobalPlayerCapturePolicy.MaximumSampleAge ||
                !GlobalPosition.IsFiniteValue(planLifetime) || planLifetime <= 0 || planLifetime > 60 ||
                !GlobalPosition.IsFiniteValue(retryDelay) || retryDelay < 0.1 || retryDelay > checkpointInterval) throw new ArgumentException("Explicit bounded checkpoint timings required.");
            CheckpointInterval = checkpointInterval; SampleAge = sampleAge; PlanLifetime = planLifetime; RetryDelay = retryDelay;
        }
    }
    public enum GlobalSessionPhase { Unconfigured, Configured, Prepared, Running, Stopping, Stopped, Faulted }
    public enum GlobalSessionSaveStatus { None, Applied, Deferred, NoLivePlayers, Blocked, Abandoned }
    public static class GlobalSessionPersistencePolicy
    {
        public static bool ValidateStart(GlobalMotionStartRole role, int legacyPersistenceInstances, bool hasRepository, CheckpointStoreStatus storeStatus, bool hasHostIdentity, out string error)
        {
            error = null;
            if (role != GlobalMotionStartRole.Client && role != GlobalMotionStartRole.Host && role != GlobalMotionStartRole.Server) return PositionJsonSafety.Fail("invalid_role", out error);
            if (legacyPersistenceInstances != 0) return PositionJsonSafety.Fail("legacy_position_services_must_be_absent_not_just_disabled", out error);
            if (role == GlobalMotionStartRole.Client) return true;
            if (!hasRepository || (storeStatus != CheckpointStoreStatus.Ready && storeStatus != CheckpointStoreStatus.Empty)) return PositionJsonSafety.Fail("explicit_ready_or_empty_server_repository_required", out error);
            if (role == GlobalMotionStartRole.Host && !hasHostIdentity) return PositionJsonSafety.Fail("explicit_trusted_host_identity_and_spawn_policy_required", out error);
            return true;
        }
        public static bool IsCurrentPeerReceipt(Guid expectedSession, Guid submittedSession, object currentPeer, object submittedPeer) => expectedSession != Guid.Empty &&
            expectedSession == submittedSession && currentPeer != null && ReferenceEquals(currentPeer, submittedPeer);
        public static bool MatchesRole(GlobalMotionStartRole role, bool server, bool client, bool host) => role == GlobalMotionStartRole.Host ? server && client && host :
            role == GlobalMotionStartRole.Server ? server && !client && !host : role == GlobalMotionStartRole.Client && !server && client && !host;
        public static bool MayStop(GlobalSessionSaveStatus result, bool explicitAbandon) => explicitAbandon || result == GlobalSessionSaveStatus.Applied || result == GlobalSessionSaveStatus.NoLivePlayers;
        public static bool IsSuccessfulCommit(CheckpointTransactionStatus status) => status == CheckpointTransactionStatus.Applied;
    }
    /// <summary>Pure monotonic scheduling and fault latch. No automatic quarantine, recovery or retry after a write failure.</summary>
    public sealed class GlobalCheckpointSchedule
    {
        private readonly GlobalSessionTiming _timing;
        private double _next, _last;
        public bool IsFaulted { get; private set; }
        public double NextAttempt => _next;
        public GlobalCheckpointSchedule(GlobalSessionTiming timing, double now)
        { _timing = timing ?? throw new ArgumentNullException(nameof(timing)); if (!ValidTime(now)) throw new ArgumentOutOfRangeException(nameof(now)); _last = now; _next = now + timing.CheckpointInterval; }
        public bool IsDue(double now) => Observe(now) && !IsFaulted && now >= _next;
        public void RecordApplied(double now) { if (Observe(now) && !IsFaulted) _next = now + _timing.CheckpointInterval; }
        public void RecordDeferred(double now) { if (Observe(now) && !IsFaulted) _next = now + _timing.RetryDelay; }
        public void RecordFailure() { IsFaulted = true; }
        public bool RetryExplicitly(double now) { if (!Observe(now)) return false; IsFaulted = false; _next = now; return true; }
        private bool Observe(double now) { if (!ValidTime(now) || now < _last) { IsFaulted = true; return false; } _last = now; return true; }
        private static bool ValidTime(double time) => GlobalPosition.IsFiniteValue(time) && time >= 0;
    }
}
