using System;
using UnityEngine;
using ProjectC.World.FloatingOrigin.Network;

namespace ProjectC.World.FloatingOrigin.Persistence
{
    public enum GlobalPlayerSpawnResolutionKind { RestoredCheckpoint, ExplicitFirstSpawn }
    public sealed class GlobalPlayerSpawnResolution
    {
        public string PlayerId { get; }
        public GlobalPlayerSpawnResolutionKind Kind { get; }
        public GlobalPlayerPositionRecord Checkpoint { get; }
        public GlobalMotionPlayerSpawnPlan Plan { get; }
        internal GlobalPlayerSpawnResolution(string id, GlobalPlayerSpawnResolutionKind kind, GlobalPlayerPositionRecord checkpoint, GlobalMotionPlayerSpawnPlan plan)
        { PlayerId = id; Kind = kind; Checkpoint = checkpoint; Plan = plan; }
    }
    /// <summary>Pure lookup + projection. Caller supplies trusted identity, a prepared frame, appearance/game rules and an OPTIONAL explicit first-spawn point.</summary>
    public static class GlobalPlayerSpawnPlanResolver
    {
        public static bool TryResolve(string playerId, ulong clientId, CheckpointStoreObservation observation, int frameId, LocalCoordinateFrame coordinates,
            Quaternion rotation, Vector3 scale, Func<GlobalMotionSnapshot, bool> ownerRules, GlobalPosition? explicitFirstSpawn, Guid reservation,
            out GlobalPlayerSpawnResolution resolution, out string error)
        {
            resolution = null; error = null;
            if (!GlobalPlayerPositionRecord.IsValidIdentity(playerId) || reservation == Guid.Empty) return PositionJsonSafety.Fail("verified_identity_and_reservation_required", out error);
            if (observation == null || (observation.Status != CheckpointStoreStatus.Ready && observation.Status != CheckpointStoreStatus.Empty) ||
                (observation.Status == CheckpointStoreStatus.Ready && observation.Snapshot == null)) return PositionJsonSafety.Fail("published_ready_or_explicit_empty_checkpoint_required", out error);
            GlobalPlayerPositionRecord checkpoint = null;
            if (observation.Snapshot != null) foreach (var record in observation.Snapshot.Players) if (string.Equals(record.PlayerId, playerId, StringComparison.Ordinal)) { checkpoint = record; break; }
            if (checkpoint != null && checkpoint.InShip) return PositionJsonSafety.Fail("ship_affinity_restore_bridge_required", out error);
            if (checkpoint == null && !explicitFirstSpawn.HasValue) return PositionJsonSafety.Fail("no_checkpoint_explicit_first_spawn_required", out error);
            var position = checkpoint != null ? checkpoint.Position : explicitFirstSpawn.Value;
            var plan = new GlobalMotionPlayerSpawnPlan(frameId, position, rotation, scale, ownerRules, reservation);
            if (!plan.TryProject(coordinates, clientId, out _)) return PositionJsonSafety.Fail("explicit_pose_rules_or_prepared_frame_projection_invalid", out error);
            resolution = new GlobalPlayerSpawnResolution(playerId, checkpoint == null ? GlobalPlayerSpawnResolutionKind.ExplicitFirstSpawn : GlobalPlayerSpawnResolutionKind.RestoredCheckpoint, checkpoint, plan);
            return true;
        }
        public static bool SamePlan(GlobalMotionPlayerSpawnPlan expected, GlobalMotionPlayerSpawnPlan candidate) => expected.ReservationId != Guid.Empty &&
            expected.ReservationId == candidate.ReservationId && expected.FrameId == candidate.FrameId && expected.Position == candidate.Position &&
            expected.Rotation.Equals(candidate.Rotation) && expected.Scale.Equals(candidate.Scale) && ReferenceEquals(expected.OwnerRules, candidate.OwnerRules);
        public static bool CanCancel(bool completed, Guid currentReservation, Guid requestedReservation) => !completed && requestedReservation != Guid.Empty && currentReservation == requestedReservation;
        public static bool WithinLease(double issued, double deadline, double now) => GlobalPosition.IsFiniteValue(issued) && GlobalPosition.IsFiniteValue(deadline) &&
            GlobalPosition.IsFiniteValue(now) && issued >= 0 && deadline > issued && deadline - issued <= 60d && now >= issued && now <= deadline;
    }
}
