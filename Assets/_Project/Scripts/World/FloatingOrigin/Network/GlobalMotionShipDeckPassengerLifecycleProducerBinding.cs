using System;
using System.Collections.Generic;

namespace ProjectC.World.FloatingOrigin.Network
{
    /// <summary>
    /// T-FO06CT: explicit binding façade between reviewed server/gameplay fact callers and the
    /// protocol-owned lifecycle producer bridge. It accepts facts only through typed methods,
    /// keeps the active downstream receipts required by the combined ShipDeck host, and never
    /// discovers identities or derives generations from Unity/NGO state.
    /// </summary>
    public sealed class GlobalMotionShipDeckPassengerLifecycleProducerBinding : IGlobalMotionShipDeckPassengerGenerationSource
    {
        private readonly GlobalMotionShipDeckPassengerLifecycleProducerBridge _producer;
        private readonly Dictionary<string, GlobalMotionShipDeckPassengerLifecycleReceipt> _activePassengers =
            new Dictionary<string, GlobalMotionShipDeckPassengerLifecycleReceipt>(StringComparer.Ordinal);

        private ulong _shipNetworkObjectId;
        private ulong _shipLifetimeGeneration;
        private string _shipId;
        private bool _shipRegistered;
        private bool _shipInvalidated;

        public GlobalMotionShipDeckPassengerLifecycleProducerBinding(
            GlobalMotionShipDeckPassengerLifecycleProducerBridge producer)
        {
            _producer = producer ?? throw new ArgumentNullException(nameof(producer));
        }

        public bool IsShipRegistered => _shipRegistered;
        public bool IsShipInvalidated => _shipInvalidated;
        public int ActivePassengerCount => _activePassengers.Count;
        public ulong ShipLifetimeGeneration => _shipLifetimeGeneration;
        public string ShipId => _shipId;

        public bool TryRegisterShipServerAuthorized(
            string shipId,
            ulong shipNetworkObjectId,
            bool serverAuthorized,
            bool protocolOwned,
            out GlobalMotionShipDeckPassengerLifecycleReceipt receipt,
            out string error)
        {
            receipt = default;
            if (_shipInvalidated)
                return Reject("binding_ship_invalidated", out error);
            if (_shipRegistered)
                return Reject("binding_ship_already_registered", out error);
            if (!_producer.TryRegisterShipServerAuthorized(
                    shipId, shipNetworkObjectId, serverAuthorized, protocolOwned,
                    out var mapping, out error))
                return false;

            receipt = mapping.ToDownstreamReceipt();
            _shipId = receipt.ShipId;
            _shipNetworkObjectId = receipt.ShipNetworkObjectId;
            _shipLifetimeGeneration = receipt.ShipLifetimeGeneration;
            _shipRegistered = true;
            error = null;
            return true;
        }

        public bool TryAttachPassengerServerAuthorized(
            string shipId,
            ulong shipNetworkObjectId,
            ulong expectedShipLifetimeGeneration,
            string passengerId,
            ulong previousAttachmentGeneration,
            string deckNavId,
            bool serverAuthorized,
            bool protocolOwned,
            out GlobalMotionShipDeckPassengerLifecycleReceipt receipt,
            out string error)
        {
            receipt = default;
            if (!ValidateBoundShip(shipId, shipNetworkObjectId, expectedShipLifetimeGeneration, out error))
                return false;
            if (!_producer.TryAttachPassengerServerAuthorized(
                    shipId, shipNetworkObjectId, expectedShipLifetimeGeneration, passengerId,
                    previousAttachmentGeneration, deckNavId, serverAuthorized, protocolOwned,
                    out var mapping, out error))
                return false;
            receipt = mapping.ToDownstreamReceipt();
            _activePassengers[receipt.PassengerId] = receipt;
            error = null;
            return true;
        }

        public bool TryDetachPassengerServerAuthorized(
            string shipId,
            ulong shipNetworkObjectId,
            ulong expectedShipLifetimeGeneration,
            string passengerId,
            ulong attachmentGeneration,
            string deckNavId,
            bool serverAuthorized,
            bool protocolOwned,
            out GlobalMotionShipDeckPassengerLifecycleReceipt receipt,
            out string error)
        {
            receipt = default;
            if (!ValidateBoundShip(shipId, shipNetworkObjectId, expectedShipLifetimeGeneration, out error))
                return false;
            if (!_producer.TryDetachPassengerServerAuthorized(
                    shipId, shipNetworkObjectId, expectedShipLifetimeGeneration, passengerId,
                    attachmentGeneration, deckNavId, serverAuthorized, protocolOwned,
                    out var mapping, out error))
                return false;
            receipt = mapping.ToDownstreamReceipt();
            _activePassengers.Remove(receipt.PassengerId);
            error = null;
            return true;
        }

        public bool TryInvalidateShipServerAuthorized(
            string shipId,
            ulong shipNetworkObjectId,
            ulong expectedShipLifetimeGeneration,
            string invalidationReason,
            bool serverAuthorized,
            bool protocolOwned,
            out GlobalMotionShipDeckPassengerLifecycleReceipt receipt,
            out string error)
        {
            receipt = default;
            if (!ValidateBoundShip(shipId, shipNetworkObjectId, expectedShipLifetimeGeneration, out error))
                return false;
            if (!_producer.TryInvalidateShipServerAuthorized(
                    shipId, shipNetworkObjectId, expectedShipLifetimeGeneration, invalidationReason,
                    serverAuthorized, protocolOwned, out var mapping, out error))
                return false;
            receipt = mapping.ToDownstreamReceipt();
            _activePassengers.Clear();
            _shipInvalidated = true;
            error = null;
            return true;
        }

        public bool TryCreateActivePassengerBinding(
            ulong bindingGeneration,
            out GlobalMotionShipDeckPassengerLifecycleBindingReceipt binding,
            out GlobalMotionShipDeckPassengerLifecycleReceipt[] receipts,
            out string error)
        {
            binding = default;
            receipts = null;
            if (!_shipRegistered)
                return Reject("binding_ship_registration_required", out error);
            if (_shipInvalidated)
                return Reject("binding_ship_invalidated", out error);

            var ordered = new List<GlobalMotionShipDeckPassengerLifecycleReceipt>(_activePassengers.Values);
            ordered.Sort((left, right) => StringComparer.Ordinal.Compare(left.PassengerId, right.PassengerId));
            receipts = ordered.ToArray();
            if (!GlobalMotionShipDeckPassengerLifecycleSourceBindingContract.TryBindActivePassengers(
                    bindingGeneration, receipts, out binding, out error))
            {
                receipts = null;
                return false;
            }
            error = null;
            return true;
        }

        public bool TryGetGeneration(
            string passengerId,
            out GlobalMotionShipDeckPassengerGeneration generation,
            out string error)
        {
            return _producer.TryGetGeneration(passengerId, out generation, out error);
        }

        private bool ValidateBoundShip(
            string shipId,
            ulong shipNetworkObjectId,
            ulong expectedShipLifetimeGeneration,
            out string error)
        {
            error = null;
            if (!_shipRegistered)
                return Reject("binding_ship_registration_required", out error);
            if (_shipInvalidated)
                return Reject("binding_ship_invalidated", out error);
            if (!string.Equals(shipId, _shipId, StringComparison.Ordinal))
                return Reject("binding_ship_id_mismatch", out error);
            if (shipNetworkObjectId != _shipNetworkObjectId)
                return Reject("binding_ship_network_identity_mismatch", out error);
            if (expectedShipLifetimeGeneration != _shipLifetimeGeneration)
                return Reject("binding_ship_lifetime_generation_mismatch", out error);
            return true;
        }

        private static bool Reject(string reason, out string error)
        {
            error = reason;
            return false;
        }
    }
}
