using ProjectC.Ship;
using UnityEngine;
using UnityEngine.AI;
using Unity.Netcode;

namespace ProjectC.World.FloatingOrigin.Network
{
    /// <summary>
    /// Explicit server-owned snapshot of one NpcBrain ship-deck attachment.
    /// It is transaction-scoped and intentionally not Unity-serialized.
    /// </summary>
    public readonly struct GlobalMotionNpcShipDeckSnapshot
    {
        public string TransactionId { get; }
        public string PassengerId { get; }
        public string DeckNavId { get; }
        public NetworkObject ShipNetworkObject { get; }
        public ShipDeckNav DeckNav { get; }
        public bool AttachmentRequested { get; }
        public bool AttachmentActive { get; }
        public bool ParentToShip { get; }
        public bool DeckNavigationActive { get; }
        public bool AgentUpdatePosition { get; }
        public bool AgentUpdateRotation { get; }
        public bool ProxyCreated { get; }
        public bool ProxyOnNavMesh { get; }
        public bool ProxyHasPath { get; }
        public bool ProxyIsStopped { get; }
        public Vector3 WorldPosition { get; }
        public Quaternion WorldRotation { get; }
        public Vector3 LocalPosition { get; }
        public Quaternion LocalRotation { get; }
        public Vector3 ProxyPosition { get; }
        public Vector3 ProxyDestination { get; }

        public GlobalMotionNpcShipDeckSnapshot(
            string transactionId,
            string passengerId,
            string deckNavId,
            NetworkObject shipNetworkObject,
            ShipDeckNav deckNav,
            bool attachmentRequested,
            bool attachmentActive,
            bool parentToShip,
            bool deckNavigationActive,
            bool agentUpdatePosition,
            bool agentUpdateRotation,
            bool proxyCreated,
            bool proxyOnNavMesh,
            bool proxyHasPath,
            bool proxyIsStopped,
            Vector3 worldPosition,
            Quaternion worldRotation,
            Vector3 localPosition,
            Quaternion localRotation,
            Vector3 proxyPosition,
            Vector3 proxyDestination)
        {
            TransactionId = transactionId;
            PassengerId = passengerId;
            DeckNavId = deckNavId;
            ShipNetworkObject = shipNetworkObject;
            DeckNav = deckNav;
            AttachmentRequested = attachmentRequested;
            AttachmentActive = attachmentActive;
            ParentToShip = parentToShip;
            DeckNavigationActive = deckNavigationActive;
            AgentUpdatePosition = agentUpdatePosition;
            AgentUpdateRotation = agentUpdateRotation;
            ProxyCreated = proxyCreated;
            ProxyOnNavMesh = proxyOnNavMesh;
            ProxyHasPath = proxyHasPath;
            ProxyIsStopped = proxyIsStopped;
            WorldPosition = worldPosition;
            WorldRotation = worldRotation;
            LocalPosition = localPosition;
            LocalRotation = localRotation;
            ProxyPosition = proxyPosition;
            ProxyDestination = proxyDestination;
        }
    }

    public static class GlobalMotionNpcShipDeckSnapshotContract
    {
        public static bool TryValidate(
            GlobalMotionNpcShipDeckSnapshot snapshot,
            out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(snapshot.TransactionId) || snapshot.TransactionId.Trim() != snapshot.TransactionId)
                return Reject("transaction_id_required", out error);
            if (string.IsNullOrWhiteSpace(snapshot.PassengerId))
                return Reject("passenger_id_required", out error);
            if (string.IsNullOrWhiteSpace(snapshot.DeckNavId))
                return Reject("deck_nav_id_required", out error);
            if (snapshot.ShipNetworkObject == null)
                return Reject("ship_network_object_required", out error);
            if (snapshot.DeckNav == null)
                return Reject("deck_nav_required", out error);
            if (!snapshot.AttachmentRequested || !snapshot.AttachmentActive)
                return Reject("ship_attachment_not_active", out error);
            if (!snapshot.DeckNavigationActive)
                return Reject("deck_navigation_not_active", out error);
            if (!snapshot.ProxyCreated || !snapshot.ProxyOnNavMesh)
                return Reject("deck_proxy_not_ready", out error);
            return true;
        }

        private static bool Reject(string reason, out string error)
        {
            error = reason;
            return false;
        }
    }
}