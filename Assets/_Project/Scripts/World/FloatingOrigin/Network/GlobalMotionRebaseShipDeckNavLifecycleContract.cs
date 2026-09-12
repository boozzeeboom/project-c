using System;

namespace ProjectC.World.FloatingOrigin.Network
{
    public enum GlobalMotionShipDeckNavLifecyclePhase : byte
    {
        Unbound = 0,
        RegistrationRequested = 1,
        Registered = 2,
        Ready = 3,
        SnapshotCaptured = 4,
        RebuildRequested = 5,
        Rebuilt = 6,
        Validated = 7,
        Restored = 8
    }

    /// <summary>
    /// Runtime-independent contract for a synchronous ShipDeckNav transaction seam.
    /// It records required lifecycle lineage but does not register NavMesh data or mutate agents.
    /// </summary>
    public readonly struct GlobalMotionRebaseShipDeckNavLifecycleEvidence
    {
        public string ShipDeckNavId { get; }
        public string NavMeshDataId { get; }
        public string TransactionId { get; }
        public ulong RegistrationGeneration { get; }
        public ulong NavMeshInstanceGeneration { get; }
        public ulong PassengerAttachmentGeneration { get; }
        public GlobalMotionShipDeckNavLifecyclePhase Phase { get; }
        public bool SynchronousRebuildSupported { get; }
        public bool SynchronousRestoreSupported { get; }
        public bool PassengerStateCaptured { get; }
        public bool PassengerStateRestored { get; }

        public GlobalMotionRebaseShipDeckNavLifecycleEvidence(
            string shipDeckNavId,
            string navMeshDataId,
            string transactionId,
            ulong registrationGeneration,
            ulong navMeshInstanceGeneration,
            ulong passengerAttachmentGeneration,
            GlobalMotionShipDeckNavLifecyclePhase phase,
            bool synchronousRebuildSupported,
            bool synchronousRestoreSupported,
            bool passengerStateCaptured,
            bool passengerStateRestored)
        {
            ShipDeckNavId = shipDeckNavId;
            NavMeshDataId = navMeshDataId;
            TransactionId = transactionId;
            RegistrationGeneration = registrationGeneration;
            NavMeshInstanceGeneration = navMeshInstanceGeneration;
            PassengerAttachmentGeneration = passengerAttachmentGeneration;
            Phase = phase;
            SynchronousRebuildSupported = synchronousRebuildSupported;
            SynchronousRestoreSupported = synchronousRestoreSupported;
            PassengerStateCaptured = passengerStateCaptured;
            PassengerStateRestored = passengerStateRestored;
        }
    }

    public static class GlobalMotionRebaseShipDeckNavLifecycleContract
    {
        public static bool TryValidateReady(
            GlobalMotionRebaseShipDeckNavLifecycleEvidence evidence,
            out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(evidence.ShipDeckNavId))
                return Reject("ship_deck_nav_id_required", out error);
            if (string.IsNullOrWhiteSpace(evidence.NavMeshDataId))
                return Reject("nav_mesh_data_id_required", out error);
            if (string.IsNullOrWhiteSpace(evidence.TransactionId))
                return Reject("transaction_id_required", out error);
            if (evidence.RegistrationGeneration == 0)
                return Reject("registration_generation_required", out error);
            if (evidence.NavMeshInstanceGeneration == 0)
                return Reject("nav_mesh_instance_generation_required", out error);
            if (evidence.PassengerAttachmentGeneration == 0)
                return Reject("passenger_attachment_generation_required", out error);
            if (evidence.Phase < GlobalMotionShipDeckNavLifecyclePhase.Ready)
                return Reject("ship_deck_nav_not_ready", out error);
            if (!evidence.SynchronousRebuildSupported)
                return Reject("synchronous_rebuild_not_supported", out error);
            if (!evidence.SynchronousRestoreSupported)
                return Reject("synchronous_restore_not_supported", out error);
            return true;
        }

        public static bool TryValidateCaptured(
            GlobalMotionRebaseShipDeckNavLifecycleEvidence evidence,
            out string error)
        {
            if (!TryValidateReady(evidence, out error))
                return false;
            if (evidence.Phase < GlobalMotionShipDeckNavLifecyclePhase.SnapshotCaptured)
                return Reject("ship_deck_nav_snapshot_not_captured", out error);
            if (!evidence.PassengerStateCaptured)
                return Reject("passenger_state_not_captured", out error);
            return true;
        }

        public static bool TryValidateRestored(
            GlobalMotionRebaseShipDeckNavLifecycleEvidence evidence,
            out string error)
        {
            if (!TryValidateCaptured(evidence, out error))
                return false;
            if (evidence.Phase != GlobalMotionShipDeckNavLifecyclePhase.Restored)
                return Reject("ship_deck_nav_restore_phase_invalid", out error);
            if (!evidence.PassengerStateRestored)
                return Reject("passenger_state_not_restored", out error);
            return true;
        }

        private static bool Reject(string reason, out string error)
        {
            error = reason;
            return false;
        }
    }
}
