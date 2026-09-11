using System;

namespace ProjectC.World.FloatingOrigin.Network
{
    public enum GlobalMotionRebaseTransactionPhase : byte
    {
        Idle,
        Requested,
        FramePrepared,
        Applied,
        PhysicsSynchronized,
        Validated,
        Published,
        Completed,
        RollbackBegun,
        Restored,
        RollbackCompleted,
        Aborted,
        Faulted
    }

    public enum GlobalMotionRebaseTriggerKind : byte
    {
        UserControlled = 1
    }

    /// <summary>
    /// Runtime-independent boundary for the future controlled rebase transaction.
    /// It defines accepted trigger identity and phase transitions without mutating Unity state.
    /// </summary>
    public readonly struct GlobalMotionRebaseTriggerRequest
    {
        public Guid TransactionId { get; }
        public GlobalMotionRebaseTriggerKind Kind { get; }
        public ulong FrameGeneration { get; }
        public string Reason { get; }

        public GlobalMotionRebaseTriggerRequest(Guid transactionId, GlobalMotionRebaseTriggerKind kind,
            ulong frameGeneration, string reason)
        {
            TransactionId = transactionId;
            Kind = kind;
            FrameGeneration = frameGeneration;
            Reason = reason;
        }

        public bool TryValidate(out string error)
        {
            error = null;
            if (TransactionId == Guid.Empty) { error = "transaction_id_required"; return false; }
            if (Kind != GlobalMotionRebaseTriggerKind.UserControlled) { error = "user_controlled_trigger_required"; return false; }
            if (FrameGeneration == 0) { error = "frame_generation_required"; return false; }
            if (string.IsNullOrWhiteSpace(Reason) || Reason.Trim() != Reason || Reason.Length > 256)
            {
                error = "trigger_reason_required";
                return false;
            }
            return true;
        }
    }

    /// <summary>
    /// Ordered transaction contract. This is deliberately not connected to a runtime driver yet.
    /// </summary>
    public static class GlobalMotionRebaseTransactionContract
    {
        public static bool TryAdvance(GlobalMotionRebaseTransactionPhase current,
            GlobalMotionRebaseTransactionPhase next, out string error)
        {
            error = null;
            if (current == GlobalMotionRebaseTransactionPhase.Idle && next == GlobalMotionRebaseTransactionPhase.Requested) return true;
            if (current == GlobalMotionRebaseTransactionPhase.Requested && next == GlobalMotionRebaseTransactionPhase.FramePrepared) return true;
            if (current == GlobalMotionRebaseTransactionPhase.FramePrepared && next == GlobalMotionRebaseTransactionPhase.Applied) return true;
            if (current == GlobalMotionRebaseTransactionPhase.Applied && next == GlobalMotionRebaseTransactionPhase.PhysicsSynchronized) return true;
            if (current == GlobalMotionRebaseTransactionPhase.PhysicsSynchronized && next == GlobalMotionRebaseTransactionPhase.Validated) return true;
            if (current == GlobalMotionRebaseTransactionPhase.Validated && next == GlobalMotionRebaseTransactionPhase.Published) return true;
            if (current == GlobalMotionRebaseTransactionPhase.Published && next == GlobalMotionRebaseTransactionPhase.Completed) return true;

            if (CanBeginRollback(current) && next == GlobalMotionRebaseTransactionPhase.RollbackBegun) return true;
            if (current == GlobalMotionRebaseTransactionPhase.RollbackBegun && next == GlobalMotionRebaseTransactionPhase.Restored) return true;
            if (current == GlobalMotionRebaseTransactionPhase.Restored && next == GlobalMotionRebaseTransactionPhase.RollbackCompleted) return true;

            if (CanAbort(current) && next == GlobalMotionRebaseTransactionPhase.Aborted) return true;
            if (CanAbort(current) && next == GlobalMotionRebaseTransactionPhase.Faulted) return true;

            error = "invalid_rebase_phase_transition:" + current + "->" + next;
            return false;
        }

        public static bool IsTerminal(GlobalMotionRebaseTransactionPhase phase)
        {
            return phase == GlobalMotionRebaseTransactionPhase.Completed ||
                phase == GlobalMotionRebaseTransactionPhase.RollbackCompleted ||
                phase == GlobalMotionRebaseTransactionPhase.Aborted ||
                phase == GlobalMotionRebaseTransactionPhase.Faulted;
        }

        private static bool CanBeginRollback(GlobalMotionRebaseTransactionPhase phase)
        {
            return phase == GlobalMotionRebaseTransactionPhase.FramePrepared ||
                phase == GlobalMotionRebaseTransactionPhase.Applied ||
                phase == GlobalMotionRebaseTransactionPhase.PhysicsSynchronized ||
                phase == GlobalMotionRebaseTransactionPhase.Validated ||
                phase == GlobalMotionRebaseTransactionPhase.Published;
        }

        private static bool CanAbort(GlobalMotionRebaseTransactionPhase phase)
        {
            return phase == GlobalMotionRebaseTransactionPhase.Requested ||
                phase == GlobalMotionRebaseTransactionPhase.FramePrepared ||
                phase == GlobalMotionRebaseTransactionPhase.Applied ||
                phase == GlobalMotionRebaseTransactionPhase.PhysicsSynchronized ||
                phase == GlobalMotionRebaseTransactionPhase.Validated ||
                phase == GlobalMotionRebaseTransactionPhase.Published ||
                phase == GlobalMotionRebaseTransactionPhase.RollbackBegun ||
                phase == GlobalMotionRebaseTransactionPhase.Restored;
        }
    }
}