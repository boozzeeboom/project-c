using System;
using ProjectC.AI;
using ProjectC.Ship;
using UnityEngine;

namespace ProjectC.World.FloatingOrigin.Network
{
    public sealed class GlobalMotionShipDeckCombinedSnapshot
    {
        public string TransactionId { get; }
        public ulong FrameGeneration { get; }
        public ulong PassengerBindingGeneration { get; }
        public ShipDeckNavFloatingOriginSnapshot DeckSnapshot { get; }
        public GlobalMotionNpcShipDeckSnapshot[] PassengerSnapshots { get; }

        public GlobalMotionShipDeckCombinedSnapshot(
            string transactionId,
            ulong frameGeneration,
            ulong passengerBindingGeneration,
            ShipDeckNavFloatingOriginSnapshot deckSnapshot,
            GlobalMotionNpcShipDeckSnapshot[] passengerSnapshots)
        {
            TransactionId = transactionId;
            FrameGeneration = frameGeneration;
            PassengerBindingGeneration = passengerBindingGeneration;
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
        private ulong _reviewedPassengerBindingGeneration;

        public bool IsBound => ResolveDeckNav() != null;
        public ulong ReviewedPassengerBindingGeneration => _reviewedPassengerBindingGeneration;

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
            _reviewedPassengerBindingGeneration = _reviewedPassengerBindingGeneration == ulong.MaxValue
                ? 1UL
                : _reviewedPassengerBindingGeneration + 1UL;
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

            snapshot = new GlobalMotionShipDeckCombinedSnapshot(
                transactionId,
                frameGeneration,
                _reviewedPassengerBindingGeneration,
                deckSnapshot,
                passengerSnapshots);
            return true;
        }

        public bool TryRebuild(
            GlobalMotionShipDeckCombinedSnapshot snapshot,
            Vector3 navFrameOrigin,
            out string error)
        {
            return TryRebuild(snapshot, navFrameOrigin, out _, out error);
        }

        public bool TryRebuild(
            GlobalMotionShipDeckCombinedSnapshot snapshot,
            Vector3 navFrameOrigin,
            out GlobalMotionShipDeckCombinedTransactionResult result,
            out string error)
        {
            result = default;
            if (!TryValidateSnapshot(snapshot, out error))
            {
                result = CreateResult(snapshot, GlobalMotionShipDeckCombinedTransactionPhase.Faulted,
                    false, false, false, false, false, false, false, false, false, error);
                return false;
            }

            if (!ResolveDeckNav().TryRebuildFloatingOriginSnapshot(snapshot.TransactionId, navFrameOrigin, out error))
            {
                result = CreateResult(snapshot, GlobalMotionShipDeckCombinedTransactionPhase.Faulted,
                    true, false, false, false, false, false, false, false, false, error);
                return false;
            }

            for (int i = 0; i < snapshot.PassengerSnapshots.Length; i++)
            {
                if (_reviewedPassengers[i].TryRestoreFloatingOriginShipDeckSnapshot(
                        snapshot.PassengerSnapshots[i],
                        out error))
                    continue;

                bool deckRollbackSucceeded = ResolveDeckNav().TryRestoreFloatingOriginSnapshot(
                    snapshot.DeckSnapshot,
                    out _);
                bool passengerRollbackSucceeded = true;
                for (int rollbackIndex = i; rollbackIndex >= 0; rollbackIndex--)
                {
                    if (!_reviewedPassengers[rollbackIndex].TryRestoreFloatingOriginShipDeckSnapshot(
                            snapshot.PassengerSnapshots[rollbackIndex],
                            out _))
                        passengerRollbackSucceeded = false;
                }

                string restoreError = error;
                bool rollbackComplete = deckRollbackSucceeded && passengerRollbackSucceeded;
                error = restoreError + ";rollback=" + (rollbackComplete ? "complete" : "incomplete");
                result = CreateResult(
                    snapshot,
                    rollbackComplete
                        ? GlobalMotionShipDeckCombinedTransactionPhase.RollbackCompleted
                        : GlobalMotionShipDeckCombinedTransactionPhase.Faulted,
                    true,
                    false,
                    false,
                    true,
                    rollbackComplete,
                    true,
                    deckRollbackSucceeded,
                    true,
                    passengerRollbackSucceeded,
                    error);
                return false;
            }

            error = null;
            result = CreateResult(snapshot, GlobalMotionShipDeckCombinedTransactionPhase.Rebuilt,
                true, true, false, false, false, false, false, false, false, null);
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
            if (string.IsNullOrWhiteSpace(snapshot.TransactionId) || snapshot.TransactionId.Trim() != snapshot.TransactionId)
                return Reject("transaction_id_required", out error);
            if (snapshot.FrameGeneration == 0)
                return Reject("frame_generation_required", out error);
            if (snapshot.PassengerBindingGeneration == 0 ||
                snapshot.PassengerBindingGeneration != _reviewedPassengerBindingGeneration)
                return Reject("passenger_binding_generation_mismatch", out error);
            if (!string.Equals(snapshot.DeckSnapshot.TransactionId, snapshot.TransactionId, StringComparison.Ordinal))
                return Reject("deck_snapshot_transaction_mismatch", out error);
            if (snapshot.PassengerSnapshots == null || snapshot.PassengerSnapshots.Length == 0)
                return Reject("passenger_snapshot_missing", out error);
            if (_reviewedPassengers == null || _reviewedPassengers.Length != snapshot.PassengerSnapshots.Length)
                return Reject("passenger_snapshot_count_mismatch", out error);
            for (int i = 0; i < snapshot.PassengerSnapshots.Length; i++)
            {
                if (!GlobalMotionNpcShipDeckSnapshotContract.TryValidate(snapshot.PassengerSnapshots[i], out error))
                    return false;
                if (!string.Equals(snapshot.PassengerSnapshots[i].TransactionId, snapshot.TransactionId, StringComparison.Ordinal))
                    return Reject("passenger_snapshot_transaction_mismatch:index=" + i, out error);
                if (!string.Equals(snapshot.PassengerSnapshots[i].PassengerId, _reviewedPassengers[i].name, StringComparison.Ordinal))
                    return Reject("passenger_snapshot_identity_mismatch:index=" + i, out error);
                if (!string.Equals(snapshot.PassengerSnapshots[i].DeckNavId, ResolveDeckNav().name, StringComparison.Ordinal))
                    return Reject("passenger_snapshot_deck_mismatch:index=" + i, out error);
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

        private static GlobalMotionShipDeckCombinedTransactionResult CreateResult(
            GlobalMotionShipDeckCombinedSnapshot snapshot,
            GlobalMotionShipDeckCombinedTransactionPhase phase,
            bool captureSucceeded,
            bool rebuildSucceeded,
            bool validationSucceeded,
            bool restoreAttempted,
            bool restoreSucceeded,
            bool deckRollbackAttempted,
            bool deckRollbackSucceeded,
            bool passengerRollbackAttempted,
            bool passengerRollbackSucceeded,
            string error)
        {
            return new GlobalMotionShipDeckCombinedTransactionResult(
                snapshot != null ? snapshot.TransactionId : string.Empty,
                snapshot != null ? snapshot.FrameGeneration : 0UL,
                snapshot != null ? snapshot.PassengerBindingGeneration : 0UL,
                phase,
                captureSucceeded,
                rebuildSucceeded,
                validationSucceeded,
                restoreAttempted,
                restoreSucceeded,
                deckRollbackAttempted,
                deckRollbackSucceeded,
                passengerRollbackAttempted,
                passengerRollbackSucceeded,
                error);
        }

        private static bool Reject(string reason, out string error)
        {
            error = reason;
            return false;
        }
    }
}