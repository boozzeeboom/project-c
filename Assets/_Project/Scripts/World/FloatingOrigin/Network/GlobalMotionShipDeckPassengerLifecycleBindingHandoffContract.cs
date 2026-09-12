using System;

namespace ProjectC.World.FloatingOrigin.Network
{
    public static class GlobalMotionShipDeckPassengerLifecycleBindingHandoffContract
    {
        public static bool TryValidate(
            GlobalMotionShipDeckPassengerLifecycleBindingReceipt binding,
            int reviewedPassengerCount,
            ulong reviewedBindingGeneration,
            out string error)
        {
            if (!GlobalMotionShipDeckPassengerLifecycleSourceBindingContract.TryValidate(binding, out error))
                return false;
            if (reviewedPassengerCount <= 0)
                return Reject("reviewed_passenger_count_required", out error);
            if (binding.PassengerCount != reviewedPassengerCount)
                return Reject("reviewed_passenger_count_mismatch", out error);
            if (reviewedBindingGeneration == 0)
                return Reject("reviewed_binding_generation_required", out error);
            if (binding.BindingGeneration != reviewedBindingGeneration)
                return Reject("reviewed_binding_generation_mismatch", out error);
            error = null;
            return true;
        }

        private static bool Reject(string reason, out string error)
        {
            error = reason;
            return false;
        }
    }
}
