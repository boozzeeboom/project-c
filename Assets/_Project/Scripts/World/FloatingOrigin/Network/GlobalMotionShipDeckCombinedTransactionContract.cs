using System;

namespace ProjectC.World.FloatingOrigin.Network
{
    public enum GlobalMotionShipDeckCombinedTransactionPhase : byte
    {
        None = 0,
        CaptureRequested = 1,
        Captured = 2,
        RebuildRequested = 3,
        Rebuilt = 4,
        Validated = 5,
        Restored = 6,
        RollbackBegun = 7,
        RollbackCompleted = 8,
        Faulted = 9
    }

    /// <summary>
    /// Immutable result for one combined ShipDeckNav/passenger operation.
    /// It records rollback attempts without claiming runtime admission readiness.
    /// </summary>
    public readonly struct GlobalMotionShipDeckCombinedTransactionResult
    {
        public string TransactionId { get; }
        public ulong FrameGeneration { get; }
        public ulong PassengerBindingGeneration { get; }
        public GlobalMotionShipDeckCombinedTransactionPhase Phase { get; }
        public bool CaptureSucceeded { get; }
        public bool RebuildSucceeded { get; }
        public bool ValidationSucceeded { get; }
        public bool RestoreAttempted { get; }
        public bool RestoreSucceeded { get; }
        public bool DeckRollbackAttempted { get; }
        public bool DeckRollbackSucceeded { get; }
        public bool PassengerRollbackAttempted { get; }
        public bool PassengerRollbackSucceeded { get; }
        public string Error { get; }

        public GlobalMotionShipDeckCombinedTransactionResult(
            string transactionId,
            ulong frameGeneration,
            ulong passengerBindingGeneration,
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
            TransactionId = transactionId;
            FrameGeneration = frameGeneration;
            PassengerBindingGeneration = passengerBindingGeneration;
            Phase = phase;
            CaptureSucceeded = captureSucceeded;
            RebuildSucceeded = rebuildSucceeded;
            ValidationSucceeded = validationSucceeded;
            RestoreAttempted = restoreAttempted;
            RestoreSucceeded = restoreSucceeded;
            DeckRollbackAttempted = deckRollbackAttempted;
            DeckRollbackSucceeded = deckRollbackSucceeded;
            PassengerRollbackAttempted = passengerRollbackAttempted;
            PassengerRollbackSucceeded = passengerRollbackSucceeded;
            Error = error;
        }

        public bool IsSuccessful =>
            Phase == GlobalMotionShipDeckCombinedTransactionPhase.Validated ||
            Phase == GlobalMotionShipDeckCombinedTransactionPhase.Restored;

        public bool IsRollbackComplete =>
            Phase == GlobalMotionShipDeckCombinedTransactionPhase.RollbackCompleted &&
            DeckRollbackAttempted && DeckRollbackSucceeded &&
            PassengerRollbackAttempted && PassengerRollbackSucceeded;
    }

    public static class GlobalMotionShipDeckCombinedTransactionContract
    {
        public static bool TryValidate(
            GlobalMotionShipDeckCombinedTransactionResult result,
            out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(result.TransactionId) || result.TransactionId.Trim() != result.TransactionId)
                return Reject("transaction_id_required", out error);
            if (result.FrameGeneration == 0)
                return Reject("frame_generation_required", out error);
            if (result.PassengerBindingGeneration == 0)
                return Reject("passenger_binding_generation_required", out error);
            if (result.Phase == GlobalMotionShipDeckCombinedTransactionPhase.None)
                return Reject("transaction_phase_required", out error);
            if (result.Phase == GlobalMotionShipDeckCombinedTransactionPhase.RollbackCompleted && !result.IsRollbackComplete)
                return Reject("rollback_completion_incomplete", out error);
            return true;
        }

        private static bool Reject(string reason, out string error)
        {
            error = reason;
            return false;
        }
    }
}
