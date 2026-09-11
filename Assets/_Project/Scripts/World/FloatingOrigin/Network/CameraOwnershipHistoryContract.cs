using System;

namespace ProjectC.World.FloatingOrigin.Network
{
    /// <summary>
    /// Runtime-independent evidence envelope for active camera ownership and history continuity.
    /// It does not inspect or mutate Unity camera state.
    /// </summary>
    public readonly struct CameraOwnershipHistoryEvidence
    {
        public string CameraId { get; }
        public string OwnerId { get; }
        public string TargetId { get; }
        public string ActiveCameraId { get; }
        public ulong BindingGeneration { get; }
        public ulong HistoryGeneration { get; }
        public bool ActiveCamera { get; }
        public bool TargetBound { get; }
        public bool HistoryCaptured { get; }
        public bool CollisionHistoryCaptured { get; }
        public bool HistoryContinuityVerified { get; }
        public bool BillboardBindingVerified { get; }

        public CameraOwnershipHistoryEvidence(
            string cameraId,
            string ownerId,
            string targetId,
            string activeCameraId,
            ulong bindingGeneration,
            ulong historyGeneration,
            bool activeCamera,
            bool targetBound,
            bool historyCaptured,
            bool collisionHistoryCaptured,
            bool historyContinuityVerified,
            bool billboardBindingVerified)
        {
            CameraId = cameraId;
            OwnerId = ownerId;
            TargetId = targetId;
            ActiveCameraId = activeCameraId;
            BindingGeneration = bindingGeneration;
            HistoryGeneration = historyGeneration;
            ActiveCamera = activeCamera;
            TargetBound = targetBound;
            HistoryCaptured = historyCaptured;
            CollisionHistoryCaptured = collisionHistoryCaptured;
            HistoryContinuityVerified = historyContinuityVerified;
            BillboardBindingVerified = billboardBindingVerified;
        }
    }

    public static class CameraOwnershipHistoryContract
    {
        public static bool TryValidate(CameraOwnershipHistoryEvidence evidence, out string error)
        {
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(evidence.CameraId))
                return Reject("camera_id_required", out error);
            if (string.IsNullOrWhiteSpace(evidence.OwnerId))
                return Reject("camera_owner_id_required", out error);
            if (string.IsNullOrWhiteSpace(evidence.TargetId))
                return Reject("camera_target_id_required", out error);
            if (string.IsNullOrWhiteSpace(evidence.ActiveCameraId))
                return Reject("active_camera_id_required", out error);
            if (evidence.BindingGeneration == 0)
                return Reject("camera_binding_generation_required", out error);
            if (evidence.HistoryGeneration == 0)
                return Reject("camera_history_generation_required", out error);
            if (!string.Equals(evidence.CameraId, evidence.ActiveCameraId, StringComparison.Ordinal))
                return Reject("active_camera_mismatch:" + evidence.CameraId, out error);
            if (!evidence.ActiveCamera)
                return Reject("camera_not_active:" + evidence.CameraId, out error);
            if (!evidence.TargetBound)
                return Reject("camera_target_not_bound:" + evidence.TargetId, out error);
            if (!evidence.HistoryCaptured)
                return Reject("camera_history_not_captured:" + evidence.CameraId, out error);
            if (!evidence.CollisionHistoryCaptured)
                return Reject("camera_collision_history_not_captured:" + evidence.CameraId, out error);
            if (!evidence.HistoryContinuityVerified)
                return Reject("camera_history_continuity_unverified:" + evidence.CameraId, out error);
            if (!evidence.BillboardBindingVerified)
                return Reject("camera_billboard_binding_unverified:" + evidence.CameraId, out error);

            return true;
        }

        public static bool IsReady(CameraOwnershipHistoryEvidence evidence)
        {
            return TryValidate(evidence, out _);
        }

        private static bool Reject(string reason, out string error)
        {
            error = reason;
            return false;
        }
    }
}