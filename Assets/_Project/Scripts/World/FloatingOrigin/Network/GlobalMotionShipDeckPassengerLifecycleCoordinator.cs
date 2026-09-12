using System;
using System.Collections.Generic;

namespace ProjectC.World.FloatingOrigin.Network
{
    /// <summary>
    /// Explicit server-authorized lifecycle fact consumed by the protocol-owned coordinator.
    /// Expected generations are lineage assertions; newly accepted generations are allocated by the coordinator.
    /// </summary>
    public readonly struct GlobalMotionShipDeckPassengerLifecycleFact
    {
        public GlobalMotionShipDeckPassengerLifecyclePhase Phase { get; }
        public string ShipId { get; }
        public ulong ShipNetworkObjectId { get; }
        public ulong ExpectedShipLifetimeGeneration { get; }
        public string PassengerId { get; }
        public ulong ExpectedAttachmentGeneration { get; }
        public string DeckNavId { get; }
        public string InvalidationReason { get; }
        public bool ServerAuthorized { get; }
        public bool ProtocolOwned { get; }

        public GlobalMotionShipDeckPassengerLifecycleFact(
            GlobalMotionShipDeckPassengerLifecyclePhase phase,
            string shipId,
            ulong shipNetworkObjectId,
            ulong expectedShipLifetimeGeneration,
            string passengerId,
            ulong expectedAttachmentGeneration,
            string deckNavId,
            string invalidationReason,
            bool serverAuthorized,
            bool protocolOwned)
        {
            Phase = phase;
            ShipId = shipId;
            ShipNetworkObjectId = shipNetworkObjectId;
            ExpectedShipLifetimeGeneration = expectedShipLifetimeGeneration;
            PassengerId = passengerId;
            ExpectedAttachmentGeneration = expectedAttachmentGeneration;
            DeckNavId = deckNavId;
            InvalidationReason = invalidationReason;
            ServerAuthorized = serverAuthorized;
            ProtocolOwned = protocolOwned;
        }

        public static GlobalMotionShipDeckPassengerLifecycleFact CreateShipRegistered(
            string shipId,
            ulong shipNetworkObjectId,
            bool serverAuthorized,
            bool protocolOwned)
        {
            return new GlobalMotionShipDeckPassengerLifecycleFact(
                GlobalMotionShipDeckPassengerLifecyclePhase.ShipRegistered,
                shipId, shipNetworkObjectId, 0, null, 0, null, null,
                serverAuthorized, protocolOwned);
        }

        public static GlobalMotionShipDeckPassengerLifecycleFact CreatePassengerAttached(
            string shipId,
            ulong shipNetworkObjectId,
            ulong expectedShipLifetimeGeneration,
            string passengerId,
            ulong previousAttachmentGeneration,
            string deckNavId,
            bool serverAuthorized,
            bool protocolOwned)
        {
            return new GlobalMotionShipDeckPassengerLifecycleFact(
                GlobalMotionShipDeckPassengerLifecyclePhase.PassengerAttached,
                shipId, shipNetworkObjectId, expectedShipLifetimeGeneration,
                passengerId, previousAttachmentGeneration, deckNavId, null,
                serverAuthorized, protocolOwned);
        }

        public static GlobalMotionShipDeckPassengerLifecycleFact CreatePassengerDetached(
            string shipId,
            ulong shipNetworkObjectId,
            ulong expectedShipLifetimeGeneration,
            string passengerId,
            ulong attachmentGeneration,
            string deckNavId,
            bool serverAuthorized,
            bool protocolOwned)
        {
            return new GlobalMotionShipDeckPassengerLifecycleFact(
                GlobalMotionShipDeckPassengerLifecyclePhase.PassengerDetached,
                shipId, shipNetworkObjectId, expectedShipLifetimeGeneration,
                passengerId, attachmentGeneration, deckNavId, null,
                serverAuthorized, protocolOwned);
        }

        public static GlobalMotionShipDeckPassengerLifecycleFact CreateShipInvalidated(
            string shipId,
            ulong shipNetworkObjectId,
            ulong expectedShipLifetimeGeneration,
            string invalidationReason,
            bool serverAuthorized,
            bool protocolOwned)
        {
            return new GlobalMotionShipDeckPassengerLifecycleFact(
                GlobalMotionShipDeckPassengerLifecyclePhase.ShipInvalidated,
                shipId, shipNetworkObjectId, expectedShipLifetimeGeneration,
                null, 0, null, invalidationReason,
                serverAuthorized, protocolOwned);
        }
    }

    /// <summary>
    /// Immutable receipt issued only after an explicit fact is accepted by the coordinator.
    /// ShipId is the stable protocol identity; NetworkObjectId is supplemental transport identity only.
    /// </summary>
    public readonly struct GlobalMotionShipDeckPassengerLifecycleCoordinatorReceipt
    {
        public string ShipId { get; }
        public ulong ShipNetworkObjectId { get; }
        public ulong ShipLifetimeGeneration { get; }
        public string PassengerId { get; }
        public ulong AttachmentGeneration { get; }
        public string DeckNavId { get; }
        public GlobalMotionShipDeckPassengerLifecyclePhase Phase { get; }
        public ulong LedgerOrdinal { get; }
        public string InvalidationReason { get; }
        public bool ServerOwned { get; }
        public bool ProtocolOwned { get; }

        public GlobalMotionShipDeckPassengerLifecycleCoordinatorReceipt(
            string shipId,
            ulong shipNetworkObjectId,
            ulong shipLifetimeGeneration,
            string passengerId,
            ulong attachmentGeneration,
            string deckNavId,
            GlobalMotionShipDeckPassengerLifecyclePhase phase,
            ulong ledgerOrdinal,
            string invalidationReason,
            bool serverOwned,
            bool protocolOwned)
        {
            ShipId = shipId;
            ShipNetworkObjectId = shipNetworkObjectId;
            ShipLifetimeGeneration = shipLifetimeGeneration;
            PassengerId = passengerId;
            AttachmentGeneration = attachmentGeneration;
            DeckNavId = deckNavId;
            Phase = phase;
            LedgerOrdinal = ledgerOrdinal;
            InvalidationReason = invalidationReason;
            ServerOwned = serverOwned;
            ProtocolOwned = protocolOwned;
        }

        public GlobalMotionShipDeckPassengerGeneration ToGeneration()
        {
            return new GlobalMotionShipDeckPassengerGeneration(
                PassengerId, ShipNetworkObjectId, ShipLifetimeGeneration,
                AttachmentGeneration, DeckNavId, ServerOwned, ProtocolOwned);
        }
    }

    /// <summary>
    /// Dormant protocol-owned server lifecycle coordinator and ledger owner for ShipDeck passengers.
    /// It accepts no Unity/NGO observations and never infers generations from object state.
    /// </summary>
    public sealed class GlobalMotionShipDeckPassengerLifecycleCoordinator : IGlobalMotionShipDeckPassengerGenerationSource
    {
        private sealed class PassengerState
        {
            public ulong AttachmentGeneration;
            public string DeckNavId;
            public bool Active;
        }

        private sealed class ShipState
        {
            public string ShipId;
            public ulong ShipNetworkObjectId;
            public ulong ShipLifetimeGeneration;
            public bool Invalidated;
            public readonly Dictionary<string, PassengerState> Passengers =
                new Dictionary<string, PassengerState>(StringComparer.Ordinal);
        }

        private sealed class ActivePassengerState
        {
            public string ShipId;
            public ulong ShipLifetimeGeneration;
            public ulong AttachmentGeneration;
        }

        private readonly Dictionary<string, ShipState> _ships =
            new Dictionary<string, ShipState>(StringComparer.Ordinal);
        private readonly Dictionary<string, ActivePassengerState> _activePassengers =
            new Dictionary<string, ActivePassengerState>(StringComparer.Ordinal);
        private ulong _nextShipLifetimeGeneration;
        private ulong _nextAttachmentGeneration;
        private ulong _nextLedgerOrdinal;

        public bool TryAccept(
            GlobalMotionShipDeckPassengerLifecycleFact fact,
            out GlobalMotionShipDeckPassengerLifecycleCoordinatorReceipt receipt,
            out string error)
        {
            receipt = default;
            if (!TryValidateFact(fact, out error))
                return false;

            if (fact.Phase == GlobalMotionShipDeckPassengerLifecyclePhase.ShipRegistered)
                return TryAcceptShipRegistered(fact, out receipt, out error);

            if (!_ships.TryGetValue(fact.ShipId, out ShipState ship))
                return Reject("ship_not_registered", out error);
            if (ship.Invalidated)
                return Reject("ship_lifecycle_invalidated", out error);
            if (fact.ShipNetworkObjectId != ship.ShipNetworkObjectId)
                return Reject("ship_network_identity_mismatch", out error);
            if (fact.ExpectedShipLifetimeGeneration != ship.ShipLifetimeGeneration)
                return Reject("stale_ship_lifetime_generation", out error);

            switch (fact.Phase)
            {
                case GlobalMotionShipDeckPassengerLifecyclePhase.PassengerAttached:
                    return TryAcceptPassengerAttached(ship, fact, out receipt, out error);
                case GlobalMotionShipDeckPassengerLifecyclePhase.PassengerDetached:
                    return TryAcceptPassengerDetached(ship, fact, out receipt, out error);
                case GlobalMotionShipDeckPassengerLifecyclePhase.ShipInvalidated:
                    return TryAcceptShipInvalidated(ship, fact, out receipt, out error);
                default:
                    return Reject("lifecycle_phase_unsupported", out error);
            }
        }

        public bool TryGetGeneration(
            string passengerId,
            out GlobalMotionShipDeckPassengerGeneration generation,
            out string error)
        {
            generation = default;
            if (!IsStableIdentity(passengerId))
                return Reject("passenger_id_required", out error);
            if (!_activePassengers.TryGetValue(passengerId, out ActivePassengerState active))
                return Reject("passenger_not_active", out error);
            if (!_ships.TryGetValue(active.ShipId, out ShipState ship) || ship.Invalidated)
                return Reject("ship_lifecycle_invalidated", out error);
            if (!ship.Passengers.TryGetValue(passengerId, out PassengerState passenger) ||
                !passenger.Active || passenger.AttachmentGeneration != active.AttachmentGeneration)
                return Reject("passenger_ledger_state_invalid", out error);

            generation = new GlobalMotionShipDeckPassengerGeneration(
                passengerId, ship.ShipNetworkObjectId, ship.ShipLifetimeGeneration,
                passenger.AttachmentGeneration, passenger.DeckNavId, true, true);
            if (!GlobalMotionShipDeckPassengerGenerationContract.TryValidate(generation, out error))
            {
                generation = default;
                return false;
            }

            error = null;
            return true;
        }

        public static bool TryValidateFact(
            GlobalMotionShipDeckPassengerLifecycleFact fact,
            out string error)
        {
            error = null;
            if (fact.Phase == GlobalMotionShipDeckPassengerLifecyclePhase.None)
                return Reject("lifecycle_phase_required", out error);
            if (!IsStableIdentity(fact.ShipId))
                return Reject("ship_id_required", out error);
            if (fact.ShipNetworkObjectId == 0)
                return Reject("ship_network_object_id_required", out error);
            if (!fact.ServerAuthorized)
                return Reject("server_authorization_required", out error);
            if (!fact.ProtocolOwned)
                return Reject("protocol_ownership_required", out error);

            if (fact.Phase == GlobalMotionShipDeckPassengerLifecyclePhase.ShipRegistered)
            {
                if (fact.ExpectedShipLifetimeGeneration != 0)
                    return Reject("ship_registration_generation_must_be_coordinator_owned", out error);
                if (!string.IsNullOrEmpty(fact.PassengerId) || fact.ExpectedAttachmentGeneration != 0 ||
                    !string.IsNullOrEmpty(fact.DeckNavId) || !string.IsNullOrEmpty(fact.InvalidationReason))
                    return Reject("ship_registration_contains_passenger_state", out error);
                return true;
            }

            if (fact.ExpectedShipLifetimeGeneration == 0)
                return Reject("expected_ship_lifetime_generation_required", out error);

            switch (fact.Phase)
            {
                case GlobalMotionShipDeckPassengerLifecyclePhase.PassengerAttached:
                    if (!IsStableIdentity(fact.PassengerId))
                        return Reject("passenger_id_required", out error);
                    if (!IsStableIdentity(fact.DeckNavId))
                        return Reject("deck_nav_id_required", out error);
                    if (!string.IsNullOrEmpty(fact.InvalidationReason))
                        return Reject("attachment_invalidation_reason_forbidden", out error);
                    return true;
                case GlobalMotionShipDeckPassengerLifecyclePhase.PassengerDetached:
                    if (!IsStableIdentity(fact.PassengerId))
                        return Reject("passenger_id_required", out error);
                    if (fact.ExpectedAttachmentGeneration == 0)
                        return Reject("expected_attachment_generation_required", out error);
                    if (!IsStableIdentity(fact.DeckNavId))
                        return Reject("deck_nav_id_required", out error);
                    if (!string.IsNullOrEmpty(fact.InvalidationReason))
                        return Reject("detach_invalidation_reason_forbidden", out error);
                    return true;
                case GlobalMotionShipDeckPassengerLifecyclePhase.ShipInvalidated:
                    if (!string.IsNullOrWhiteSpace(fact.PassengerId) || fact.ExpectedAttachmentGeneration != 0 ||
                        !string.IsNullOrWhiteSpace(fact.DeckNavId))
                        return Reject("ship_invalidation_contains_passenger_state", out error);
                    if (!IsStableIdentity(fact.InvalidationReason))
                        return Reject("invalidation_reason_required", out error);
                    return true;
                default:
                    return Reject("lifecycle_phase_unsupported", out error);
            }
        }

        public static bool TryValidateReceipt(
            GlobalMotionShipDeckPassengerLifecycleCoordinatorReceipt receipt,
            out string error)
        {
            error = null;
            if (!IsStableIdentity(receipt.ShipId))
                return Reject("ship_id_required", out error);
            if (receipt.ShipNetworkObjectId == 0)
                return Reject("ship_network_object_id_required", out error);
            if (receipt.ShipLifetimeGeneration == 0)
                return Reject("ship_lifetime_generation_required", out error);
            if (receipt.LedgerOrdinal == 0)
                return Reject("ledger_ordinal_required", out error);
            if (!receipt.ServerOwned)
                return Reject("server_owned_lifecycle_required", out error);
            if (!receipt.ProtocolOwned)
                return Reject("protocol_owned_lifecycle_required", out error);
            if (receipt.Phase == GlobalMotionShipDeckPassengerLifecyclePhase.None)
                return Reject("lifecycle_phase_required", out error);

            if (receipt.Phase == GlobalMotionShipDeckPassengerLifecyclePhase.ShipRegistered)
            {
                if (!string.IsNullOrEmpty(receipt.PassengerId) || receipt.AttachmentGeneration != 0 ||
                    !string.IsNullOrEmpty(receipt.DeckNavId) || !string.IsNullOrEmpty(receipt.InvalidationReason))
                    return Reject("ship_registration_contains_passenger_state", out error);
                return true;
            }
            if (receipt.Phase == GlobalMotionShipDeckPassengerLifecyclePhase.ShipInvalidated)
            {
                if (!string.IsNullOrEmpty(receipt.PassengerId) || receipt.AttachmentGeneration != 0 ||
                    !string.IsNullOrEmpty(receipt.DeckNavId))
                    return Reject("ship_invalidation_contains_passenger_state", out error);
                if (!IsStableIdentity(receipt.InvalidationReason))
                    return Reject("invalidation_reason_required", out error);
                return true;
            }
            if (!IsStableIdentity(receipt.PassengerId))
                return Reject("passenger_id_required", out error);
            if (receipt.AttachmentGeneration == 0)
                return Reject("attachment_generation_required", out error);
            if (!IsStableIdentity(receipt.DeckNavId))
                return Reject("deck_nav_id_required", out error);
            return true;
        }

        private bool TryAcceptShipRegistered(
            GlobalMotionShipDeckPassengerLifecycleFact fact,
            out GlobalMotionShipDeckPassengerLifecycleCoordinatorReceipt receipt,
            out string error)
        {
            receipt = default;
            if (_ships.TryGetValue(fact.ShipId, out ShipState existing) && !existing.Invalidated)
                return Reject("duplicate_ship_registration", out error);
            if (!TryNext(ref _nextShipLifetimeGeneration, out ulong generation, out error))
                return false;

            var ship = new ShipState
            {
                ShipId = fact.ShipId,
                ShipNetworkObjectId = fact.ShipNetworkObjectId,
                ShipLifetimeGeneration = generation,
                Invalidated = false
            };
            _ships[fact.ShipId] = ship;
            return TryCreateReceipt(ship, null, 0, GlobalMotionShipDeckPassengerLifecyclePhase.ShipRegistered,
                null, out receipt, out error);
        }

        private bool TryAcceptPassengerAttached(
            ShipState ship,
            GlobalMotionShipDeckPassengerLifecycleFact fact,
            out GlobalMotionShipDeckPassengerLifecycleCoordinatorReceipt receipt,
            out string error)
        {
            receipt = default;
            if (_activePassengers.ContainsKey(fact.PassengerId))
                return Reject("duplicate_active_passenger_attachment", out error);
            if (ship.Passengers.TryGetValue(fact.PassengerId, out PassengerState previous))
            {
                if (previous.Active)
                    return Reject("duplicate_active_passenger_attachment", out error);
                if (fact.ExpectedAttachmentGeneration != previous.AttachmentGeneration)
                    return Reject("stale_attachment_generation", out error);
            }
            else if (fact.ExpectedAttachmentGeneration != 0)
            {
                return Reject("attachment_lineage_mismatch", out error);
            }

            if (!TryNext(ref _nextAttachmentGeneration, out ulong generation, out error))
                return false;
            var passenger = previous ?? new PassengerState();
            passenger.AttachmentGeneration = generation;
            passenger.DeckNavId = fact.DeckNavId;
            passenger.Active = true;
            ship.Passengers[fact.PassengerId] = passenger;
            _activePassengers[fact.PassengerId] = new ActivePassengerState
            {
                ShipId = ship.ShipId,
                ShipLifetimeGeneration = ship.ShipLifetimeGeneration,
                AttachmentGeneration = generation
            };
            return TryCreateReceipt(ship, fact.PassengerId, generation,
                GlobalMotionShipDeckPassengerLifecyclePhase.PassengerAttached,
                fact.DeckNavId, out receipt, out error);
        }

        private bool TryAcceptPassengerDetached(
            ShipState ship,
            GlobalMotionShipDeckPassengerLifecycleFact fact,
            out GlobalMotionShipDeckPassengerLifecycleCoordinatorReceipt receipt,
            out string error)
        {
            receipt = default;
            if (!ship.Passengers.TryGetValue(fact.PassengerId, out PassengerState passenger) || !passenger.Active)
                return Reject("passenger_not_active", out error);
            if (passenger.AttachmentGeneration != fact.ExpectedAttachmentGeneration)
                return Reject("stale_attachment_generation", out error);
            if (!string.Equals(passenger.DeckNavId, fact.DeckNavId, StringComparison.Ordinal))
                return Reject("deck_nav_identity_mismatch", out error);

            passenger.Active = false;
            if (!_activePassengers.Remove(fact.PassengerId))
                return Reject("passenger_active_index_missing", out error);
            return TryCreateReceipt(ship, fact.PassengerId, passenger.AttachmentGeneration,
                GlobalMotionShipDeckPassengerLifecyclePhase.PassengerDetached,
                passenger.DeckNavId, out receipt, out error);
        }

        private bool TryAcceptShipInvalidated(
            ShipState ship,
            GlobalMotionShipDeckPassengerLifecycleFact fact,
            out GlobalMotionShipDeckPassengerLifecycleCoordinatorReceipt receipt,
            out string error)
        {
            receipt = default;
            ship.Invalidated = true;
            foreach (KeyValuePair<string, PassengerState> pair in ship.Passengers)
            {
                if (pair.Value.Active)
                {
                    pair.Value.Active = false;
                    _activePassengers.Remove(pair.Key);
                }
            }
            return TryCreateReceipt(ship, null, 0,
                GlobalMotionShipDeckPassengerLifecyclePhase.ShipInvalidated,
                fact.InvalidationReason, out receipt, out error);
        }

        private bool TryCreateReceipt(
            ShipState ship,
            string passengerId,
            ulong attachmentGeneration,
            GlobalMotionShipDeckPassengerLifecyclePhase phase,
            string deckNavOrReason,
            out GlobalMotionShipDeckPassengerLifecycleCoordinatorReceipt receipt,
            out string error)
        {
            receipt = default;
            if (!TryNext(ref _nextLedgerOrdinal, out ulong ordinal, out error))
                return false;

            string deckNavId = phase == GlobalMotionShipDeckPassengerLifecyclePhase.ShipInvalidated ? null : deckNavOrReason;
            string invalidationReason = phase == GlobalMotionShipDeckPassengerLifecyclePhase.ShipInvalidated ? deckNavOrReason : null;
            receipt = new GlobalMotionShipDeckPassengerLifecycleCoordinatorReceipt(
                ship.ShipId, ship.ShipNetworkObjectId, ship.ShipLifetimeGeneration,
                passengerId, attachmentGeneration, deckNavId, phase, ordinal,
                invalidationReason, true, true);
            if (!TryValidateReceipt(receipt, out error))
            {
                receipt = default;
                return false;
            }
            return true;
        }

        private static bool TryNext(ref ulong counter, out ulong next, out string error)
        {
            next = 0;
            if (counter == ulong.MaxValue)
                return Reject("generation_exhausted", out error);
            next = ++counter;
            error = null;
            return true;
        }

        private static bool IsStableIdentity(string value)
        {
            return !string.IsNullOrWhiteSpace(value) && value.Trim() == value;
        }

        private static bool Reject(string reason, out string error)
        {
            error = reason;
            return false;
        }
    }
}