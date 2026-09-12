using System;

namespace ProjectC.World.FloatingOrigin.Network
{
    public enum GlobalMotionShipDeckPassengerLedgerPhase : byte
    {
        None = 0,
        CaptureRequested = 1,
        Captured = 2,
        RebuildRequested = 3,
        Rebuilt = 4,
        ValidateRequested = 5,
        Validated = 6,
        RestoreRequested = 7,
        Restored = 8,
        RollbackRequested = 9,
        RollbackCompleted = 10,
        Completed = 11,
        Faulted = 12
    }

    /// <summary>
    /// Immutable protocol-owned receipt for one reviewed passenger/deck lifecycle transaction.
    /// It does not inspect NGO or mutate NpcBrain/ShipDeckNav state.
    /// </summary>
    public readonly struct GlobalMotionShipDeckPassengerProtocolLedgerReceipt
    {
        public string TransactionId { get; }
        public GlobalMotionShipDeckPassengerGeneration Generation { get; }
        public ulong FrameGeneration { get; }
        public GlobalMotionShipDeckPassengerLedgerPhase Phase { get; }
        public int Ordinal { get; }
        public bool ServerOwned { get; }
        public bool ProtocolOwned { get; }
        public bool CaptureReceipt { get; }
        public bool RebuildReceipt { get; }
        public bool ValidateReceipt { get; }
        public bool RestoreReceipt { get; }
        public bool RollbackReceipt { get; }

        public GlobalMotionShipDeckPassengerProtocolLedgerReceipt(
            string transactionId,
            GlobalMotionShipDeckPassengerGeneration generation,
            ulong frameGeneration,
            GlobalMotionShipDeckPassengerLedgerPhase phase,
            int ordinal,
            bool serverOwned,
            bool protocolOwned,
            bool captureReceipt,
            bool rebuildReceipt,
            bool validateReceipt,
            bool restoreReceipt,
            bool rollbackReceipt)
        {
            TransactionId = transactionId;
            Generation = generation;
            FrameGeneration = frameGeneration;
            Phase = phase;
            Ordinal = ordinal;
            ServerOwned = serverOwned;
            ProtocolOwned = protocolOwned;
            CaptureReceipt = captureReceipt;
            RebuildReceipt = rebuildReceipt;
            ValidateReceipt = validateReceipt;
            RestoreReceipt = restoreReceipt;
            RollbackReceipt = rollbackReceipt;
        }
    }

    public static class GlobalMotionShipDeckPassengerProtocolLedgerContract
    {
        public static bool TryBegin(
            string transactionId,
            ulong frameGeneration,
            GlobalMotionShipDeckPassengerGeneration generation,
            out GlobalMotionShipDeckPassengerProtocolLedgerReceipt receipt,
            out string error)
        {
            receipt = default;
            error = null;
            if (string.IsNullOrWhiteSpace(transactionId) || transactionId.Trim() != transactionId)
                return Reject("transaction_id_required", out error);
            if (frameGeneration == 0)
                return Reject("frame_generation_required", out error);
            if (!GlobalMotionShipDeckPassengerGenerationContract.TryValidate(generation, out error))
                return false;

            receipt = Create(transactionId, frameGeneration, generation,
                GlobalMotionShipDeckPassengerLedgerPhase.CaptureRequested, 0,
                false, false, false, false, false);
            return true;
        }

        public static bool TryAdvance(
            GlobalMotionShipDeckPassengerProtocolLedgerReceipt current,
            GlobalMotionShipDeckPassengerLedgerPhase nextPhase,
            bool captureReceipt,
            bool rebuildReceipt,
            bool validateReceipt,
            bool restoreReceipt,
            bool rollbackReceipt,
            out GlobalMotionShipDeckPassengerProtocolLedgerReceipt next,
            out string error)
        {
            next = default;
            if (!TryValidate(current, out error))
                return false;
            if (!IsNext(current.Phase, nextPhase))
                return Reject("ledger_phase_order_invalid", out error);
            if (nextPhase == GlobalMotionShipDeckPassengerLedgerPhase.Faulted)
                return Reject("use_try_fault_for_faulted_phase", out error);

            bool capture = current.CaptureReceipt || captureReceipt;
            bool rebuild = current.RebuildReceipt || rebuildReceipt;
            bool validate = current.ValidateReceipt || validateReceipt;
            bool restore = current.RestoreReceipt || restoreReceipt;
            bool rollback = current.RollbackReceipt || rollbackReceipt;
            bool rollbackPath = nextPhase == GlobalMotionShipDeckPassengerLedgerPhase.RollbackRequested ||
                nextPhase == GlobalMotionShipDeckPassengerLedgerPhase.RollbackCompleted;

            if (!rollbackPath && nextPhase >= GlobalMotionShipDeckPassengerLedgerPhase.Captured && !capture)
                return Reject("capture_receipt_required", out error);
            if (!rollbackPath && nextPhase >= GlobalMotionShipDeckPassengerLedgerPhase.Rebuilt && !rebuild)
                return Reject("rebuild_receipt_required", out error);
            if (!rollbackPath && nextPhase >= GlobalMotionShipDeckPassengerLedgerPhase.Validated && !validate)
                return Reject("validate_receipt_required", out error);
            if (!rollbackPath && nextPhase >= GlobalMotionShipDeckPassengerLedgerPhase.Restored && !restore)
                return Reject("restore_receipt_required", out error);
            if (nextPhase == GlobalMotionShipDeckPassengerLedgerPhase.RollbackCompleted && !rollback)
                return Reject("rollback_receipt_required", out error);
            if (nextPhase == GlobalMotionShipDeckPassengerLedgerPhase.Completed &&
                (!capture || !rebuild || !validate || !restore))
                return Reject("ledger_completion_incomplete", out error);

            next = Create(current.TransactionId, current.FrameGeneration, current.Generation,
                nextPhase, current.Ordinal + 1, capture, rebuild, validate, restore, rollback);
            return true;
        }

        public static bool TryFault(
            GlobalMotionShipDeckPassengerProtocolLedgerReceipt current,
            out GlobalMotionShipDeckPassengerProtocolLedgerReceipt faulted,
            out string error)
        {
            faulted = default;
            if (!TryValidate(current, out error))
                return false;
            faulted = Create(current.TransactionId, current.FrameGeneration, current.Generation,
                GlobalMotionShipDeckPassengerLedgerPhase.Faulted, current.Ordinal + 1,
                current.CaptureReceipt, current.RebuildReceipt, current.ValidateReceipt,
                current.RestoreReceipt, current.RollbackReceipt);
            return true;
        }

        public static bool TryValidate(
            GlobalMotionShipDeckPassengerProtocolLedgerReceipt receipt,
            out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(receipt.TransactionId))
                return Reject("transaction_id_required", out error);
            if (receipt.FrameGeneration == 0)
                return Reject("frame_generation_required", out error);
            if (!GlobalMotionShipDeckPassengerGenerationContract.TryValidate(receipt.Generation, out error))
                return false;
            if (receipt.Ordinal < 0)
                return Reject("ledger_ordinal_invalid", out error);
            if (!receipt.ServerOwned)
                return Reject("server_authority_required", out error);
            if (!receipt.ProtocolOwned)
                return Reject("protocol_ownership_required", out error);
            if (receipt.Phase == GlobalMotionShipDeckPassengerLedgerPhase.None)
                return Reject("ledger_phase_required", out error);
            if (receipt.Phase == GlobalMotionShipDeckPassengerLedgerPhase.Faulted)
                return true;
            bool rollbackPath = receipt.Phase == GlobalMotionShipDeckPassengerLedgerPhase.RollbackRequested ||
                receipt.Phase == GlobalMotionShipDeckPassengerLedgerPhase.RollbackCompleted;
            if (!rollbackPath && receipt.Phase >= GlobalMotionShipDeckPassengerLedgerPhase.Captured && !receipt.CaptureReceipt)
                return Reject("capture_receipt_missing", out error);
            if (!rollbackPath && receipt.Phase >= GlobalMotionShipDeckPassengerLedgerPhase.Rebuilt && !receipt.RebuildReceipt)
                return Reject("rebuild_receipt_missing", out error);
            if (!rollbackPath && receipt.Phase >= GlobalMotionShipDeckPassengerLedgerPhase.Validated && !receipt.ValidateReceipt)
                return Reject("validate_receipt_missing", out error);
            if (!rollbackPath && receipt.Phase >= GlobalMotionShipDeckPassengerLedgerPhase.Restored && !receipt.RestoreReceipt)
                return Reject("restore_receipt_missing", out error);
            return true;
        }

        private static bool IsNext(
            GlobalMotionShipDeckPassengerLedgerPhase current,
            GlobalMotionShipDeckPassengerLedgerPhase next)
        {
            return (current == GlobalMotionShipDeckPassengerLedgerPhase.CaptureRequested && next == GlobalMotionShipDeckPassengerLedgerPhase.Captured) ||
                (current == GlobalMotionShipDeckPassengerLedgerPhase.Captured && next == GlobalMotionShipDeckPassengerLedgerPhase.RebuildRequested) ||
                (current == GlobalMotionShipDeckPassengerLedgerPhase.RebuildRequested && next == GlobalMotionShipDeckPassengerLedgerPhase.Rebuilt) ||
                (current == GlobalMotionShipDeckPassengerLedgerPhase.Rebuilt && next == GlobalMotionShipDeckPassengerLedgerPhase.ValidateRequested) ||
                (current == GlobalMotionShipDeckPassengerLedgerPhase.ValidateRequested && next == GlobalMotionShipDeckPassengerLedgerPhase.Validated) ||
                (current == GlobalMotionShipDeckPassengerLedgerPhase.Validated && next == GlobalMotionShipDeckPassengerLedgerPhase.RestoreRequested) ||
                (current == GlobalMotionShipDeckPassengerLedgerPhase.RestoreRequested && next == GlobalMotionShipDeckPassengerLedgerPhase.Restored) ||
                (current == GlobalMotionShipDeckPassengerLedgerPhase.Restored && next == GlobalMotionShipDeckPassengerLedgerPhase.Completed) ||
                (current != GlobalMotionShipDeckPassengerLedgerPhase.Completed && current != GlobalMotionShipDeckPassengerLedgerPhase.Faulted &&
                    next == GlobalMotionShipDeckPassengerLedgerPhase.RollbackRequested) ||
                (current == GlobalMotionShipDeckPassengerLedgerPhase.RollbackRequested && next == GlobalMotionShipDeckPassengerLedgerPhase.RollbackCompleted);
        }

        private static GlobalMotionShipDeckPassengerProtocolLedgerReceipt Create(
            string transactionId,
            ulong frameGeneration,
            GlobalMotionShipDeckPassengerGeneration generation,
            GlobalMotionShipDeckPassengerLedgerPhase phase,
            int ordinal,
            bool capture,
            bool rebuild,
            bool validate,
            bool restore,
            bool rollback)
        {
            return new GlobalMotionShipDeckPassengerProtocolLedgerReceipt(
                transactionId, generation, frameGeneration, phase, ordinal,
                true, true, capture, rebuild, validate, restore, rollback);
        }

        private static bool Reject(string reason, out string error)
        {
            error = reason;
            return false;
        }
    }
}
