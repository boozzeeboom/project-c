using System;
using System.Collections.Generic;

namespace ProjectC.World.FloatingOrigin.Network
{
    /// <summary>
    /// Lossless, typed mapping of one coordinator receipt at the downstream lifecycle boundary.
    /// Every value is copied from coordinator provenance; no value is inferred from Unity/NGO state.
    /// </summary>
    public readonly struct GlobalMotionShipDeckPassengerLifecycleCoordinatorBindingMapping
    {
        public string ShipId { get; }
        public ulong ShipNetworkObjectId { get; }
        public ulong ShipLifetimeGeneration { get; }
        public string PassengerId { get; }
        public ulong PassengerAttachmentGeneration { get; }
        public string DeckNavId { get; }
        public ulong CoordinatorLedgerOrdinal { get; }
        public bool ServerOwned { get; }
        public bool ProtocolOwned { get; }
        public GlobalMotionShipDeckPassengerLifecyclePhase LifecycleEventKind { get; }
        public string InvalidationReason { get; }

        public GlobalMotionShipDeckPassengerLifecycleCoordinatorBindingMapping(
            string shipId,
            ulong shipNetworkObjectId,
            ulong shipLifetimeGeneration,
            string passengerId,
            ulong passengerAttachmentGeneration,
            string deckNavId,
            ulong coordinatorLedgerOrdinal,
            bool serverOwned,
            bool protocolOwned,
            GlobalMotionShipDeckPassengerLifecyclePhase lifecycleEventKind,
            string invalidationReason)
        {
            ShipId = shipId;
            ShipNetworkObjectId = shipNetworkObjectId;
            ShipLifetimeGeneration = shipLifetimeGeneration;
            PassengerId = passengerId;
            PassengerAttachmentGeneration = passengerAttachmentGeneration;
            DeckNavId = deckNavId;
            CoordinatorLedgerOrdinal = coordinatorLedgerOrdinal;
            ServerOwned = serverOwned;
            ProtocolOwned = protocolOwned;
            LifecycleEventKind = lifecycleEventKind;
            InvalidationReason = invalidationReason;
        }

        public GlobalMotionShipDeckPassengerLifecycleReceipt ToDownstreamReceipt()
        {
            return new GlobalMotionShipDeckPassengerLifecycleReceipt(
                ShipId,
                ShipNetworkObjectId,
                ShipLifetimeGeneration,
                PassengerId,
                PassengerAttachmentGeneration,
                DeckNavId,
                LifecycleEventKind,
                CoordinatorLedgerOrdinal,
                InvalidationReason,
                ServerOwned,
                ProtocolOwned);
        }
    }

    /// <summary>
    /// Explicit dormant handoff envelope. It retains every mapped coordinator event and its
    /// converted downstream receipt; it does not bind a host or call any runtime producer.
    /// </summary>
    public readonly struct GlobalMotionShipDeckPassengerLifecycleCoordinatorBindingHandoff
    {
        public GlobalMotionShipDeckPassengerLifecycleCoordinatorBindingMapping[] Mappings { get; }
        public GlobalMotionShipDeckPassengerLifecycleReceipt[] DownstreamReceipts { get; }
        public int Count => Mappings == null ? 0 : Mappings.Length;

        public GlobalMotionShipDeckPassengerLifecycleCoordinatorBindingHandoff(
            GlobalMotionShipDeckPassengerLifecycleCoordinatorBindingMapping[] mappings,
            GlobalMotionShipDeckPassengerLifecycleReceipt[] downstreamReceipts)
        {
            Mappings = mappings;
            DownstreamReceipts = downstreamReceipts;
        }
    }

    public static class GlobalMotionShipDeckPassengerLifecycleCoordinatorBindingHandoffContract
    {
        public static bool TryMap(
            GlobalMotionShipDeckPassengerLifecycleCoordinatorReceipt coordinatorReceipt,
            out GlobalMotionShipDeckPassengerLifecycleCoordinatorBindingMapping mapping,
            out string error)
        {
            mapping = default;
            if (!GlobalMotionShipDeckPassengerLifecycleCoordinator.TryValidateReceipt(coordinatorReceipt, out error))
                return false;

            mapping = new GlobalMotionShipDeckPassengerLifecycleCoordinatorBindingMapping(
                coordinatorReceipt.ShipId,
                coordinatorReceipt.ShipNetworkObjectId,
                coordinatorReceipt.ShipLifetimeGeneration,
                coordinatorReceipt.PassengerId,
                coordinatorReceipt.AttachmentGeneration,
                coordinatorReceipt.DeckNavId,
                coordinatorReceipt.LedgerOrdinal,
                coordinatorReceipt.ServerOwned,
                coordinatorReceipt.ProtocolOwned,
                coordinatorReceipt.Phase,
                coordinatorReceipt.InvalidationReason);
            return TryValidateMapping(mapping, out error);
        }

        public static bool TryCreateHandoff(
            GlobalMotionShipDeckPassengerLifecycleCoordinatorReceipt[] coordinatorReceipts,
            out GlobalMotionShipDeckPassengerLifecycleCoordinatorBindingHandoff handoff,
            out string error)
        {
            handoff = default;
            if (coordinatorReceipts == null || coordinatorReceipts.Length == 0)
                return Reject("coordinator_receipts_required", out error);

            var mappings = new GlobalMotionShipDeckPassengerLifecycleCoordinatorBindingMapping[coordinatorReceipts.Length];
            var downstreamReceipts = new GlobalMotionShipDeckPassengerLifecycleReceipt[coordinatorReceipts.Length];
            for (int i = 0; i < coordinatorReceipts.Length; i++)
            {
                if (!TryMap(coordinatorReceipts[i], out mappings[i], out error))
                    return false;
                downstreamReceipts[i] = mappings[i].ToDownstreamReceipt();
            }

            if (!TryValidateSequence(mappings, out error))
                return false;
            handoff = new GlobalMotionShipDeckPassengerLifecycleCoordinatorBindingHandoff(
                mappings, downstreamReceipts);
            error = null;
            return true;
        }

        public static bool TryValidateMapping(
            GlobalMotionShipDeckPassengerLifecycleCoordinatorBindingMapping mapping,
            out string error)
        {
            error = null;
            if (!IsStableIdentity(mapping.ShipId))
                return Reject("ship_id_required", out error);
            if (mapping.ShipNetworkObjectId == 0)
                return Reject("ship_network_object_id_required", out error);
            if (mapping.ShipLifetimeGeneration == 0)
                return Reject("ship_lifetime_generation_required", out error);
            if (mapping.CoordinatorLedgerOrdinal == 0)
                return Reject("coordinator_ledger_ordinal_required", out error);
            if (!mapping.ServerOwned)
                return Reject("server_owned_mapping_required", out error);
            if (!mapping.ProtocolOwned)
                return Reject("protocol_owned_mapping_required", out error);
            if (mapping.LifecycleEventKind == GlobalMotionShipDeckPassengerLifecyclePhase.None)
                return Reject("lifecycle_event_kind_required", out error);

            switch (mapping.LifecycleEventKind)
            {
                case GlobalMotionShipDeckPassengerLifecyclePhase.ShipRegistered:
                    if (!string.IsNullOrEmpty(mapping.PassengerId) ||
                        mapping.PassengerAttachmentGeneration != 0 ||
                        !string.IsNullOrEmpty(mapping.DeckNavId) ||
                        !string.IsNullOrEmpty(mapping.InvalidationReason))
                        return Reject("ship_registration_mapping_incomplete", out error);
                    return true;
                case GlobalMotionShipDeckPassengerLifecyclePhase.PassengerAttached:
                case GlobalMotionShipDeckPassengerLifecyclePhase.PassengerDetached:
                    if (!IsStableIdentity(mapping.PassengerId))
                        return Reject("passenger_id_required", out error);
                    if (mapping.PassengerAttachmentGeneration == 0)
                        return Reject("passenger_attachment_generation_required", out error);
                    if (!IsStableIdentity(mapping.DeckNavId))
                        return Reject("deck_nav_id_required", out error);
                    if (!string.IsNullOrEmpty(mapping.InvalidationReason))
                        return Reject("passenger_mapping_invalidation_reason_forbidden", out error);
                    return true;
                case GlobalMotionShipDeckPassengerLifecyclePhase.ShipInvalidated:
                    if (!string.IsNullOrEmpty(mapping.PassengerId) ||
                        mapping.PassengerAttachmentGeneration != 0 ||
                        !string.IsNullOrEmpty(mapping.DeckNavId))
                        return Reject("ship_invalidation_mapping_contains_passenger_state", out error);
                    if (!IsStableIdentity(mapping.InvalidationReason))
                        return Reject("invalidation_reason_required", out error);
                    return true;
                default:
                    return Reject("lifecycle_event_kind_unsupported", out error);
            }
        }

        public static bool TryValidateHandoff(
            GlobalMotionShipDeckPassengerLifecycleCoordinatorBindingHandoff handoff,
            out string error)
        {
            error = null;
            if (handoff.Mappings == null || handoff.Mappings.Length == 0)
                return Reject("coordinator_mappings_required", out error);
            if (handoff.DownstreamReceipts == null ||
                handoff.DownstreamReceipts.Length != handoff.Mappings.Length)
                return Reject("downstream_receipt_count_mismatch", out error);
            for (int i = 0; i < handoff.Mappings.Length; i++)
            {
                if (!TryValidateMapping(handoff.Mappings[i], out error))
                    return false;
                if (!TryValidateDownstreamReceipt(handoff.DownstreamReceipts[i], handoff.Mappings[i], out error))
                    return false;
            }
            return TryValidateSequence(handoff.Mappings, out error);
        }

        private static bool TryValidateSequence(
            GlobalMotionShipDeckPassengerLifecycleCoordinatorBindingMapping[] mappings,
            out string error)
        {
            error = null;
            if (mappings == null || mappings.Length == 0)
                return Reject("coordinator_mappings_required", out error);
            if (mappings[0].LifecycleEventKind != GlobalMotionShipDeckPassengerLifecyclePhase.ShipRegistered)
                return Reject("lifecycle_registration_required_first", out error);

            string shipId = mappings[0].ShipId;
            ulong networkObjectId = mappings[0].ShipNetworkObjectId;
            ulong shipLifetimeGeneration = mappings[0].ShipLifetimeGeneration;
            ulong previousOrdinal = 0;
            bool invalidated = false;
            var activePassengers = new HashSet<string>(StringComparer.Ordinal);
            var lastAttachmentGeneration = new Dictionary<string, ulong>(StringComparer.Ordinal);

            for (int i = 0; i < mappings.Length; i++)
            {
                GlobalMotionShipDeckPassengerLifecycleCoordinatorBindingMapping mapping = mappings[i];
                if (!string.Equals(mapping.ShipId, shipId, StringComparison.Ordinal) ||
                    mapping.ShipNetworkObjectId != networkObjectId ||
                    mapping.ShipLifetimeGeneration != shipLifetimeGeneration)
                    return Reject("coordinator_ship_lifetime_mismatch:index=" + i, out error);
                if (mapping.CoordinatorLedgerOrdinal != previousOrdinal + 1UL)
                    return Reject("coordinator_ledger_ordinal_not_contiguous:index=" + i, out error);
                previousOrdinal = mapping.CoordinatorLedgerOrdinal;

                if (i > 0 && mapping.LifecycleEventKind == GlobalMotionShipDeckPassengerLifecyclePhase.ShipRegistered)
                    return Reject("duplicate_ship_registration_event:index=" + i, out error);
                if (invalidated)
                    return Reject("post_invalidation_event:index=" + i, out error);

                switch (mapping.LifecycleEventKind)
                {
                    case GlobalMotionShipDeckPassengerLifecyclePhase.ShipRegistered:
                        if (i != 0)
                            return Reject("lifecycle_registration_order_invalid:index=" + i, out error);
                        break;
                    case GlobalMotionShipDeckPassengerLifecyclePhase.PassengerAttached:
                        if (!activePassengers.Add(mapping.PassengerId))
                            return Reject("duplicate_active_passenger_attachment:index=" + i, out error);
                        if (lastAttachmentGeneration.TryGetValue(mapping.PassengerId, out ulong previousAttachment) &&
                            mapping.PassengerAttachmentGeneration <= previousAttachment)
                            return Reject("attachment_generation_not_monotonic:index=" + i, out error);
                        lastAttachmentGeneration[mapping.PassengerId] = mapping.PassengerAttachmentGeneration;
                        break;
                    case GlobalMotionShipDeckPassengerLifecyclePhase.PassengerDetached:
                        if (!activePassengers.Remove(mapping.PassengerId))
                            return Reject("passenger_detach_order_invalid:index=" + i, out error);
                        if (!lastAttachmentGeneration.TryGetValue(mapping.PassengerId, out ulong activeAttachment) ||
                            activeAttachment != mapping.PassengerAttachmentGeneration)
                            return Reject("passenger_detach_generation_mismatch:index=" + i, out error);
                        break;
                    case GlobalMotionShipDeckPassengerLifecyclePhase.ShipInvalidated:
                        invalidated = true;
                        activePassengers.Clear();
                        break;
                    default:
                        return Reject("lifecycle_event_kind_unsupported", out error);
                }
            }
            return true;
        }

        private static bool TryValidateDownstreamReceipt(
            GlobalMotionShipDeckPassengerLifecycleReceipt downstream,
            GlobalMotionShipDeckPassengerLifecycleCoordinatorBindingMapping mapping,
            out string error)
        {
            error = null;
            if (!GlobalMotionShipDeckPassengerLifecycleProducerContract.TryValidate(downstream, out error))
                return false;
            if (!string.Equals(downstream.ShipId, mapping.ShipId, StringComparison.Ordinal) ||
                downstream.ShipNetworkObjectId != mapping.ShipNetworkObjectId ||
                downstream.ShipLifetimeGeneration != mapping.ShipLifetimeGeneration ||
                downstream.ShipSpawnGeneration != mapping.ShipLifetimeGeneration ||
                !string.Equals(downstream.PassengerId, mapping.PassengerId, StringComparison.Ordinal) ||
                downstream.AttachmentGeneration != mapping.PassengerAttachmentGeneration ||
                !string.Equals(downstream.DeckNavId, mapping.DeckNavId, StringComparison.Ordinal) ||
                downstream.CoordinatorLedgerOrdinal != mapping.CoordinatorLedgerOrdinal ||
                downstream.Phase != mapping.LifecycleEventKind ||
                !string.Equals(downstream.InvalidationReason, mapping.InvalidationReason, StringComparison.Ordinal) ||
                downstream.ServerOwned != mapping.ServerOwned ||
                downstream.ProtocolOwned != mapping.ProtocolOwned)
                return Reject("downstream_provenance_mismatch", out error);
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
