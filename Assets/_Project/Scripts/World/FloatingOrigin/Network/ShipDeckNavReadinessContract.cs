using System;

namespace ProjectC.World.FloatingOrigin.Network
{
    /// <summary>
    /// Runtime-independent evidence envelope for a ship-deck NavMesh and passenger attachment gate.
    /// The contract is intentionally dormant: it does not inspect or mutate Unity runtime state.
    /// </summary>
    public readonly struct ShipDeckNavReadinessEvidence
    {
        public string ShipId { get; }
        public string ShipDeckNavId { get; }
        public string PassengerId { get; }
        public string NavMeshDataId { get; }
        public string ProxyAgentId { get; }
        public string AttachmentEvidenceId { get; }
        public bool ShipDeckNavRegistered { get; }
        public bool NavMeshDataInstanceValid { get; }
        public bool ProxyAgentCreated { get; }
        public bool ProxyAgentIsOnNavMesh { get; }
        public bool PassengerAttachmentRequested { get; }
        public bool PassengerAnchorResolved { get; }
        public bool PassengerAttachmentCompleted { get; }
        public bool PassengerProvenanceRecorded { get; }

        public ShipDeckNavReadinessEvidence(
            string shipId,
            string shipDeckNavId,
            string passengerId,
            string navMeshDataId,
            string proxyAgentId,
            string attachmentEvidenceId,
            bool shipDeckNavRegistered,
            bool navMeshDataInstanceValid,
            bool proxyAgentCreated,
            bool proxyAgentIsOnNavMesh,
            bool passengerAttachmentRequested,
            bool passengerAnchorResolved,
            bool passengerAttachmentCompleted,
            bool passengerProvenanceRecorded)
        {
            ShipId = shipId;
            ShipDeckNavId = shipDeckNavId;
            PassengerId = passengerId;
            NavMeshDataId = navMeshDataId;
            ProxyAgentId = proxyAgentId;
            AttachmentEvidenceId = attachmentEvidenceId;
            ShipDeckNavRegistered = shipDeckNavRegistered;
            NavMeshDataInstanceValid = navMeshDataInstanceValid;
            ProxyAgentCreated = proxyAgentCreated;
            ProxyAgentIsOnNavMesh = proxyAgentIsOnNavMesh;
            PassengerAttachmentRequested = passengerAttachmentRequested;
            PassengerAnchorResolved = passengerAnchorResolved;
            PassengerAttachmentCompleted = passengerAttachmentCompleted;
            PassengerProvenanceRecorded = passengerProvenanceRecorded;
        }
    }

    public static class ShipDeckNavReadinessContract
    {
        public static bool TryValidate(ShipDeckNavReadinessEvidence evidence, out string error)
        {
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(evidence.ShipId))
                return Reject("ship_id_required", out error);
            if (string.IsNullOrWhiteSpace(evidence.ShipDeckNavId))
                return Reject("ship_deck_nav_id_required", out error);
            if (string.IsNullOrWhiteSpace(evidence.PassengerId))
                return Reject("passenger_id_required", out error);
            if (!evidence.ShipDeckNavRegistered)
                return Reject("ship_deck_nav_not_registered:" + evidence.ShipDeckNavId, out error);
            if (string.IsNullOrWhiteSpace(evidence.NavMeshDataId))
                return Reject("nav_mesh_data_id_required", out error);
            if (!evidence.NavMeshDataInstanceValid)
                return Reject("nav_mesh_data_instance_invalid:" + evidence.ShipDeckNavId, out error);
            if (string.IsNullOrWhiteSpace(evidence.ProxyAgentId))
                return Reject("proxy_agent_id_required", out error);
            if (!evidence.ProxyAgentCreated)
                return Reject("proxy_agent_not_created:" + evidence.ProxyAgentId, out error);
            if (!evidence.ProxyAgentIsOnNavMesh)
                return Reject("proxy_agent_not_on_nav_mesh:" + evidence.ProxyAgentId, out error);
            if (!evidence.PassengerAttachmentRequested)
                return Reject("passenger_attachment_not_requested:" + evidence.PassengerId, out error);
            if (!evidence.PassengerAnchorResolved)
                return Reject("passenger_anchor_unresolved:" + evidence.PassengerId, out error);
            if (!evidence.PassengerAttachmentCompleted)
                return Reject("passenger_attachment_incomplete:" + evidence.PassengerId, out error);
            if (string.IsNullOrWhiteSpace(evidence.AttachmentEvidenceId))
                return Reject("attachment_evidence_id_required", out error);
            if (!evidence.PassengerProvenanceRecorded)
                return Reject("passenger_provenance_missing:" + evidence.PassengerId, out error);

            return true;
        }

        public static bool IsReady(ShipDeckNavReadinessEvidence evidence)
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