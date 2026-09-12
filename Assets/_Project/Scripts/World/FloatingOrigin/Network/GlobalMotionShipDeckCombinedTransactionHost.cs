using System;
using ProjectC.AI;
using ProjectC.Ship;
using UnityEngine;

namespace ProjectC.World.FloatingOrigin.Network
{
    public sealed class GlobalMotionShipDeckCombinedSnapshot
    {
        public string TransactionId { get; }
        public ShipDeckNavFloatingOriginSnapshot DeckSnapshot { get; }
        public GlobalMotionNpcShipDeckSnapshot[] PassengerSnapshots { get; }

        public GlobalMotionShipDeckCombinedSnapshot(
            string transactionId,
            ShipDeckNavFloatingOriginSnapshot deckSnapshot,
            GlobalMotionNpcShipDeckSnapshot[] passengerSnapshots)
        {
            TransactionId = transactionId;
            DeckSnapshot = deckSnapshot;
            PassengerSnapshots = passengerSnapshots;
        }
    }

    /// <summary>
    /// T-FO06CB: explicit combined ShipDeckNav + passenger transaction bridge.
    /// It is not an adapter registration point and has no automatic discovery.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ShipDeckNav))]
    public sealed class GlobalMotionShipDeckCombinedTransactionHost : MonoBehaviour
    {
        [SerializeField] private ShipDeckNav _deckNav;
        [SerializeField] private NpcBrain[] _reviewedPassengers = Array.Empty<NpcBrain>();

        public bool IsBound => ResolveDeckNav() != null;

        public bool TryConfigureReviewedPassengers(NpcBrain[] passengers, out string error)
        {
            if (passengers == null || passengers.Length == 0)
                return Reject("passenger_reviewed_sources_required", out error);
            for (int i = 0; i < passengers.Length; i++)
            {
                if (passengers[i] == null)
                    return Reject("passenger_reviewed_source_missing:index=" + i, out error);
            }
            _reviewedPassengers = passengers;
            error = null;
            return true;
        }

        public bool TryCapture(
            string transactionId,
            ulong frameGeneration,
            out GlobalMotionShipDeckCombinedSnapshot snapshot,
            out string error)
        {
            snapshot = null;
            if (!ValidateIdentity(transactionId, frameGeneration, out error))
                return false;
            if (!ResolveDeckNav().TryCaptureFloatingOriginSnapshot(transactionId, out var deckSnapshot, out error))
                return false;

            var passengerSnapshots = new GlobalMotionNpcShipDeckSnapshot[_reviewedPassengers.Length];
            for (int i = 0; i < _reviewedPassengers.Length; i++)
            {
                if (!_reviewedPassengers[i].TryCaptureFloatingOriginShipDeckSnapshot(
                        transactionId,
                        out passengerSnapshots[i],
                        out error))
                    return false;
            }

            snapshot = new GlobalMotionShipDeckCombinedSnapshot(transactionId, deckSnapshot, passengerSnapshots);
            return true;
        }

        public bool TryRebuild(
            GlobalMotionShipDeckCombinedSnapshot snapshot,
            Vector3 navFrameOrigin,
            out string error)
        {
            if (!TryValidateSnapshot(snapshot, out error))
                return false;
            if (!ResolveDeckNav().TryRebuildFloatingOriginSnapshot(snapshot.TransactionId, navFrameOrigin, out error))
                return false;

            for (int i = 0; i < snapshot.PassengerSnapshots.Length; i++)
            {
                if (!_reviewedPassengers[i].TryRestoreFloatingOriginShipDeckSnapshot(
                        snapshot.PassengerSnapshots[i],
                        out error))
                {
                    ResolveDeckNav().TryRestoreFloatingOriginSnapshot(snapshot.DeckSnapshot, out _);
                    return false;
                }
            }

            return true;
        }

        public bool TryValidate(
            GlobalMotionShipDeckCombinedSnapshot snapshot,
            out string error)
        {
            if (!TryValidateSnapshot(snapshot, out error))
                return false;
            if (!ResolveDeckNav().IsReady)
                return Reject("ship_deck_nav_not_ready", out error);
            for (int i = 0; i < snapshot.PassengerSnapshots.Length; i++)
            {
                if (!_reviewedPassengers[i].IsExplicitShipAttachmentActive ||
                    !_reviewedPassengers[i].IsDeckNavigationActive ||
                    !_reviewedPassengers[i].IsDeckProxyOnNavMesh)
                    return Reject("passenger_state_not_restored:" + _reviewedPassengers[i].name, out error);
            }
            error = null;
            return true;
        }

        public bool TryRestore(
            GlobalMotionShipDeckCombinedSnapshot snapshot,
            out string error)
        {
            if (!TryValidateSnapshot(snapshot, out error))
                return false;
            if (!ResolveDeckNav().TryRestoreFloatingOriginSnapshot(snapshot.DeckSnapshot, out error))
                return false;

            for (int i = 0; i < snapshot.PassengerSnapshots.Length; i++)
            {
                if (!_reviewedPassengers[i].TryRestoreFloatingOriginShipDeckSnapshot(
                        snapshot.PassengerSnapshots[i],
                        out error))
                    return false;
            }

            return true;
        }

        private bool ValidateIdentity(string transactionId, ulong frameGeneration, out string error)
        {
            if (string.IsNullOrWhiteSpace(transactionId) || transactionId.Trim() != transactionId)
                return Reject("transaction_id_required", out error);
            if (frameGeneration == 0)
                return Reject("frame_generation_required", out error);
            if (ResolveDeckNav() == null)
                return Reject("ship_deck_nav_missing", out error);
            if (_reviewedPassengers == null || _reviewedPassengers.Length == 0)
                return Reject("passenger_reviewed_sources_required", out error);
            error = null;
            return true;
        }

        private bool TryValidateSnapshot(GlobalMotionShipDeckCombinedSnapshot snapshot, out string error)
        {
            error = null;
            if (snapshot == null)
                return Reject("combined_snapshot_missing", out error);
            if (string.IsNullOrWhiteSpace(snapshot.TransactionId))
                return Reject("transaction_id_required", out error);
            if (snapshot.PassengerSnapshots == null || snapshot.PassengerSnapshots.Length == 0)
                return Reject("passenger_snapshot_missing", out error);
            if (_reviewedPassengers == null || _reviewedPassengers.Length != snapshot.PassengerSnapshots.Length)
                return Reject("passenger_snapshot_count_mismatch", out error);
            for (int i = 0; i < snapshot.PassengerSnapshots.Length; i++)
            {
                if (!GlobalMotionNpcShipDeckSnapshotContract.TryValidate(snapshot.PassengerSnapshots[i], out error))
                    return false;
                if (!_reviewedPassengers[i].TryCaptureFloatingOriginShipDeckSnapshot(
                        snapshot.TransactionId,
                        out _,
                        out _))
                    return Reject("passenger_runtime_state_unavailable:index=" + i, out error);
            }
            return true;
        }

        private ShipDeckNav ResolveDeckNav()
        {
            if (_deckNav == null)
                _deckNav = GetComponent<ShipDeckNav>();
            return _deckNav;
        }

        private static bool Reject(string reason, out string error)
        {
            error = reason;
            return false;
        }
    }
}