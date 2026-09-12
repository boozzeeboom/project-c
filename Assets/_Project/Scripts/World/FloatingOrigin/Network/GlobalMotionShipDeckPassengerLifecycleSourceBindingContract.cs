using System;
using System.Collections.Generic;

namespace ProjectC.World.FloatingOrigin.Network
{
    /// <summary>
    /// Immutable preflight result for one explicit reviewed active-passenger binding.
    /// It contains no runtime object references and does not discover passengers.
    /// </summary>
    public readonly struct GlobalMotionShipDeckPassengerLifecycleBindingReceipt
    {
        public ulong ShipNetworkObjectId { get; }
        public ulong ShipSpawnGeneration { get; }
        public ulong BindingGeneration { get; }
        public int PassengerCount { get; }
        public bool ServerOwned { get; }
        public bool ProtocolOwned { get; }

        public GlobalMotionShipDeckPassengerLifecycleBindingReceipt(
            ulong shipNetworkObjectId,
            ulong shipSpawnGeneration,
            ulong bindingGeneration,
            int passengerCount,
            bool serverOwned,
            bool protocolOwned)
        {
            ShipNetworkObjectId = shipNetworkObjectId;
            ShipSpawnGeneration = shipSpawnGeneration;
            BindingGeneration = bindingGeneration;
            PassengerCount = passengerCount;
            ServerOwned = serverOwned;
            ProtocolOwned = protocolOwned;
        }
    }

    public static class GlobalMotionShipDeckPassengerLifecycleSourceBindingContract
    {
        public static bool TryBindActivePassengers(
            ulong bindingGeneration,
            GlobalMotionShipDeckPassengerLifecycleReceipt[] receipts,
            out GlobalMotionShipDeckPassengerLifecycleBindingReceipt binding,
            out string error)
        {
            binding = default;
            if (bindingGeneration == 0)
                return Reject("binding_generation_required", out error);
            if (receipts == null || receipts.Length == 0)
                return Reject("active_passenger_receipts_required", out error);

            ulong shipNetworkObjectId = 0;
            ulong shipSpawnGeneration = 0;
            var passengerIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < receipts.Length; i++)
            {
                GlobalMotionShipDeckPassengerLifecycleReceipt receipt = receipts[i];
                if (!GlobalMotionShipDeckPassengerLifecycleProducerContract.TryValidate(receipt, out error))
                    return false;
                if (receipt.Phase != GlobalMotionShipDeckPassengerLifecyclePhase.PassengerAttached)
                    return Reject("active_passenger_receipt_required:index=" + i, out error);
                if (i == 0)
                {
                    shipNetworkObjectId = receipt.ShipNetworkObjectId;
                    shipSpawnGeneration = receipt.ShipSpawnGeneration;
                }
                else if (receipt.ShipNetworkObjectId != shipNetworkObjectId ||
                         receipt.ShipSpawnGeneration != shipSpawnGeneration)
                {
                    return Reject("ship_lifetime_mismatch:index=" + i, out error);
                }
                if (!passengerIds.Add(receipt.PassengerId))
                    return Reject("duplicate_passenger_id:index=" + i, out error);
            }

            binding = new GlobalMotionShipDeckPassengerLifecycleBindingReceipt(
                shipNetworkObjectId,
                shipSpawnGeneration,
                bindingGeneration,
                receipts.Length,
                true,
                true);
            error = null;
            return true;
        }

        public static bool TryValidate(
            GlobalMotionShipDeckPassengerLifecycleBindingReceipt binding,
            out string error)
        {
            error = null;
            if (binding.ShipNetworkObjectId == 0)
                return Reject("ship_network_object_id_required", out error);
            if (binding.ShipSpawnGeneration == 0)
                return Reject("ship_spawn_generation_required", out error);
            if (binding.BindingGeneration == 0)
                return Reject("binding_generation_required", out error);
            if (binding.PassengerCount <= 0)
                return Reject("passenger_count_required", out error);
            if (!binding.ServerOwned)
                return Reject("server_owned_binding_required", out error);
            if (!binding.ProtocolOwned)
                return Reject("protocol_owned_binding_required", out error);
            return true;
        }

        private static bool Reject(string reason, out string error)
        {
            error = reason;
            return false;
        }
    }
}
