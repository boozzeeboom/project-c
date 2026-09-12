using System;
using ProjectC.AI;
using ProjectC.Ship;
using UnityEngine;

namespace ProjectC.World.FloatingOrigin.Network
{
    /// <summary>
    /// T-FO06BX: observation-only ShipDeckNav transaction host.
    /// It exposes live registration and explicitly reviewed passenger readiness, but keeps
    /// synchronous rebuild, snapshot/restore and NavMesh rollback fail-closed.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ShipDeckNav))]
    public sealed class GlobalMotionShipDeckNavProtocolHost : MonoBehaviour, IGlobalMotionShipDeckNavTransactionHost
    {
        [SerializeField] private ShipDeckNav _deckNav;
        [SerializeField] private NpcBrain[] _reviewedPassengers = Array.Empty<NpcBrain>();

        public bool IsBound => ResolveDeckNav() != null;

        private void Awake()
        {
            _deckNav = ResolveDeckNav();
        }

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
            return TryValidateReviewedPassengers(out error);
        }

        public bool TryGetLifecycleEvidence(
            out GlobalMotionRebaseShipDeckNavLifecycleEvidence evidence,
            out string error)
        {
            evidence = default;
            var deckNav = ResolveDeckNav();
            if (deckNav == null)
                return Reject("ship_deck_nav_missing", out error);

            GlobalMotionShipDeckNavLifecyclePhase phase = GlobalMotionShipDeckNavLifecyclePhase.Unbound;
            if (deckNav.IsRegistered)
                phase = deckNav.IsReady
                    ? GlobalMotionShipDeckNavLifecyclePhase.Ready
                    : GlobalMotionShipDeckNavLifecyclePhase.Registered;

            bool passengersReady = TryValidateReviewedPassengers(out _);
            evidence = new GlobalMotionRebaseShipDeckNavLifecycleEvidence(
                "ship-deck-nav:" + deckNav.name,
                deckNav.NavMeshDataName,
                "observation-only",
                deckNav.IsRegistered ? 1UL : 0UL,
                deckNav.IsNavMeshInstanceValid ? 1UL : 0UL,
                passengersReady ? 1UL : 0UL,
                phase,
                false,
                false,
                false,
                false);
            error = null;
            return true;
        }

        public bool TryGetCapability(
            out GlobalMotionRebaseShipDeckNavLifecycleEvidence evidence,
            out string error)
        {
            return TryGetLifecycleEvidence(out evidence, out error);
        }

        public bool TryCapture(
            GlobalMotionRebaseRequest request,
            out UnityStateRollbackEvidence evidence,
            out string error)
        {
            evidence = default;
            if (!request.IsValid)
                return Reject("rebase_request_invalid", out error);
            if (!TryGetLifecycleEvidence(out var lifecycle, out error))
                return false;
            if (!GlobalMotionRebaseShipDeckNavLifecycleContract.TryValidateReady(lifecycle, out error))
                return false;
            return Reject("ship_deck_nav_passenger_snapshot_boundary_missing", out error);
        }

        public bool TryApply(GlobalMotionRebaseRequest request, out string error)
        {
            if (!TryValidateRequest(request, out error))
                return false;
            return Reject("ship_deck_nav_apply_not_reviewed", out error);
        }

        public bool TryRebuild(GlobalMotionRebaseRequest request, out string error)
        {
            if (!TryValidateRequest(request, out error))
                return false;
            return Reject("ship_deck_nav_synchronous_rebuild_not_implemented", out error);
        }

        public bool TryValidate(GlobalMotionRebaseRequest request, out string error)
        {
            if (!TryValidateRequest(request, out error))
                return false;
            return Reject("ship_deck_nav_restore_validation_not_proven", out error);
        }

        public bool TryRestore(
            GlobalMotionRebaseRequest request,
            UnityStateRollbackEvidence evidence,
            out string error)
        {
            if (!TryValidateRequest(request, out error))
                return false;
            return Reject("ship_deck_nav_passenger_restore_not_implemented", out error);
        }

        private bool TryValidateRequest(GlobalMotionRebaseRequest request, out string error)
        {
            if (!request.IsValid)
                return Reject("rebase_request_invalid", out error);
            if (!TryGetLifecycleEvidence(out var lifecycle, out error))
                return false;
            if (!GlobalMotionRebaseShipDeckNavLifecycleContract.TryValidateReady(lifecycle, out error))
                return false;
            return true;
        }

        private bool TryValidateReviewedPassengers(out string error)
        {
            error = null;
            if (_reviewedPassengers == null || _reviewedPassengers.Length == 0)
                return Reject("passenger_attachment_generation_required", out error);

            var deckNav = ResolveDeckNav();
            if (deckNav == null)
                return Reject("ship_deck_nav_missing", out error);

            for (int i = 0; i < _reviewedPassengers.Length; i++)
            {
                var passenger = _reviewedPassengers[i];
                if (passenger == null)
                    return Reject("passenger_reviewed_source_missing:index=" + i, out error);
                if (!passenger.IsExplicitShipAttachmentActive || !passenger.IsDeckNavigationActive ||
                    !passenger.IsDeckProxyCreated || !passenger.IsDeckProxyOnNavMesh ||
                    !string.Equals(passenger.DeckNavName, deckNav.name, StringComparison.Ordinal))
                    return Reject("passenger_deck_readiness_not_proven:" + passenger.name, out error);
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