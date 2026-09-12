using System;

namespace ProjectC.World.FloatingOrigin.Network
{
    public enum GlobalMotionNetworkBaselineProtocolLedgerPhase : byte
    {
        None = 0,
        CaptureRequested = 1,
        Captured = 2,
        ApplyRequested = 3,
        Applied = 4,
        ValidateRequested = 5,
        Validated = 6,
        RestoreRequested = 7,
        Restored = 8,
        Completed = 9,
        Faulted = 10
    }

    /// <summary>
    /// Immutable server-owned receipt for one NetworkBaseline transaction ledger step.
    /// This is a protocol contract only; it does not inspect or mutate NGO state.
    /// </summary>
    public readonly struct GlobalMotionNetworkBaselineProtocolLedgerReceipt
    {
        public string TransactionId { get; }
        public string ParticipantId { get; }
        public GlobalMotionNetworkBaselineIdentity Identity { get; }
        public ulong BindingAuthorityGeneration { get; }
        public ulong BindingDiscontinuityGeneration { get; }
        public ulong ControlRevision { get; }
        public uint BaselineSequence { get; }
        public GlobalMotionNetworkBaselineProtocolLedgerPhase Phase { get; }
        public int Ordinal { get; }
        public bool ServerOwned { get; }
        public bool ProtocolOwned { get; }
        public bool CaptureReceipt { get; }
        public bool ApplyReceipt { get; }
        public bool ValidateReceipt { get; }
        public bool RestoreReceipt { get; }
        public bool OwnershipRestored { get; }
        public bool LifetimeRestored { get; }
        public bool BaselineGenerationRestored { get; }

        public GlobalMotionNetworkBaselineProtocolLedgerReceipt(
            string transactionId,
            string participantId,
            GlobalMotionNetworkBaselineIdentity identity,
            ulong bindingAuthorityGeneration,
            ulong bindingDiscontinuityGeneration,
            ulong controlRevision,
            uint baselineSequence,
            GlobalMotionNetworkBaselineProtocolLedgerPhase phase,
            int ordinal,
            bool serverOwned,
            bool protocolOwned,
            bool captureReceipt,
            bool applyReceipt,
            bool validateReceipt,
            bool restoreReceipt,
            bool ownershipRestored,
            bool lifetimeRestored,
            bool baselineGenerationRestored)
        {
            TransactionId = transactionId;
            ParticipantId = participantId;
            Identity = identity;
            BindingAuthorityGeneration = bindingAuthorityGeneration;
            BindingDiscontinuityGeneration = bindingDiscontinuityGeneration;
            ControlRevision = controlRevision;
            BaselineSequence = baselineSequence;
            Phase = phase;
            Ordinal = ordinal;
            ServerOwned = serverOwned;
            ProtocolOwned = protocolOwned;
            CaptureReceipt = captureReceipt;
            ApplyReceipt = applyReceipt;
            ValidateReceipt = validateReceipt;
            RestoreReceipt = restoreReceipt;
            OwnershipRestored = ownershipRestored;
            LifetimeRestored = lifetimeRestored;
            BaselineGenerationRestored = baselineGenerationRestored;
        }
    }

    /// <summary>
    /// T-FO06BQ: pure boundary for a reviewed server-owned NGO transaction ledger.
    /// It defines receipt identity and phase ordering without implementing the NGO host.
    /// </summary>
    public static class GlobalMotionNetworkBaselineProtocolLedgerContract
    {
        public static bool TryBegin(
            GlobalMotionNetworkBaselineReversibleCapability capability,
            GlobalMotionRebaseRequest request,
            out GlobalMotionNetworkBaselineProtocolLedgerReceipt receipt,
            out string error)
        {
            receipt = default;
            error = null;
            if (!request.TryValidate(out error)) return false;
            if (!capability.IsComplete) return Reject("reversible_capability_incomplete", out error);
            if (!capability.Identity.IsValid) return Reject("network_baseline_identity_invalid", out error);
            if (!capability.ServerAuthoritative) return Reject("server_authority_required", out error);
            if (!capability.ProtocolOwned) return Reject("protocol_ownership_required", out error);
            if (!string.Equals(capability.TransactionId, request.TransactionId.ToString("N"), StringComparison.Ordinal))
                return Reject("transaction_identity_mismatch", out error);
            if (capability.BindingAuthorityGeneration == 0 || capability.BindingDiscontinuityGeneration == 0)
                return Reject("binding_generation_required", out error);

            receipt = Create(capability, GlobalMotionNetworkBaselineProtocolLedgerPhase.CaptureRequested, 0,
                false, false, false, false, false, false, false);
            return true;
        }

        public static bool TryAdvance(
            GlobalMotionNetworkBaselineProtocolLedgerReceipt current,
            GlobalMotionNetworkBaselineProtocolLedgerPhase nextPhase,
            bool captureReceipt,
            bool applyReceipt,
            bool validateReceipt,
            bool restoreReceipt,
            bool ownershipRestored,
            bool lifetimeRestored,
            bool baselineGenerationRestored,
            out GlobalMotionNetworkBaselineProtocolLedgerReceipt next,
            out string error)
        {
            next = default;
            if (!TryValidate(current, out error)) return false;
            if (!IsNext(current.Phase, nextPhase)) return Reject("ledger_phase_order_invalid", out error);
            if (nextPhase == GlobalMotionNetworkBaselineProtocolLedgerPhase.Faulted)
                return Reject("use_try_fault_for_faulted_phase", out error);
            if (!current.ServerOwned || !current.ProtocolOwned)
                return Reject("ledger_ownership_invalid", out error);
            if (captureReceipt && !current.CaptureReceipt && nextPhase < GlobalMotionNetworkBaselineProtocolLedgerPhase.Captured)
                return Reject("capture_receipt_phase_invalid", out error);
            if (applyReceipt && !captureReceipt && !current.CaptureReceipt)
                return Reject("apply_receipt_requires_capture", out error);
            if (validateReceipt && !applyReceipt && !current.ApplyReceipt)
                return Reject("validate_receipt_requires_apply", out error);
            if (restoreReceipt && !validateReceipt && !current.ValidateReceipt)
                return Reject("restore_receipt_requires_validate", out error);
            if (nextPhase >= GlobalMotionNetworkBaselineProtocolLedgerPhase.Captured && !captureReceipt && !current.CaptureReceipt)
                return Reject("capture_receipt_required", out error);
            if (nextPhase >= GlobalMotionNetworkBaselineProtocolLedgerPhase.Applied && !applyReceipt && !current.ApplyReceipt)
                return Reject("apply_receipt_required", out error);
            if (nextPhase >= GlobalMotionNetworkBaselineProtocolLedgerPhase.Validated && !validateReceipt && !current.ValidateReceipt)
                return Reject("validate_receipt_required", out error);
            if (nextPhase >= GlobalMotionNetworkBaselineProtocolLedgerPhase.Restored &&
                ((!restoreReceipt && !current.RestoreReceipt) || (!ownershipRestored && !current.OwnershipRestored) ||
                 (!lifetimeRestored && !current.LifetimeRestored) || (!baselineGenerationRestored && !current.BaselineGenerationRestored)))
                return Reject("complete_restore_receipt_required", out error);

            bool capture = current.CaptureReceipt || captureReceipt;
            bool apply = current.ApplyReceipt || applyReceipt;
            bool validate = current.ValidateReceipt || validateReceipt;
            bool restore = current.RestoreReceipt || restoreReceipt;
            bool ownership = current.OwnershipRestored || ownershipRestored;
            bool lifetime = current.LifetimeRestored || lifetimeRestored;
            bool generation = current.BaselineGenerationRestored || baselineGenerationRestored;
            if (nextPhase == GlobalMotionNetworkBaselineProtocolLedgerPhase.Completed &&
                (!capture || !apply || !validate || !restore || !ownership || !lifetime || !generation))
                return Reject("ledger_completion_incomplete", out error);

            next = new GlobalMotionNetworkBaselineProtocolLedgerReceipt(
                current.TransactionId, current.ParticipantId, current.Identity,
                current.BindingAuthorityGeneration, current.BindingDiscontinuityGeneration,
                current.ControlRevision, current.BaselineSequence, nextPhase, current.Ordinal + 1,
                current.ServerOwned, current.ProtocolOwned, capture, apply, validate, restore,
                ownership, lifetime, generation);
            return true;
        }

        public static bool TryFault(
            GlobalMotionNetworkBaselineProtocolLedgerReceipt current,
            out GlobalMotionNetworkBaselineProtocolLedgerReceipt faulted,
            out string error)
        {
            faulted = default;
            if (!TryValidate(current, out error)) return false;
            faulted = new GlobalMotionNetworkBaselineProtocolLedgerReceipt(
                current.TransactionId, current.ParticipantId, current.Identity,
                current.BindingAuthorityGeneration, current.BindingDiscontinuityGeneration,
                current.ControlRevision, current.BaselineSequence,
                GlobalMotionNetworkBaselineProtocolLedgerPhase.Faulted, current.Ordinal + 1,
                current.ServerOwned, current.ProtocolOwned, current.CaptureReceipt, current.ApplyReceipt,
                current.ValidateReceipt, current.RestoreReceipt, current.OwnershipRestored,
                current.LifetimeRestored, current.BaselineGenerationRestored);
            return true;
        }

        public static bool TryValidate(GlobalMotionNetworkBaselineProtocolLedgerReceipt receipt, out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(receipt.TransactionId)) return Reject("transaction_id_required", out error);
            if (string.IsNullOrWhiteSpace(receipt.ParticipantId)) return Reject("participant_id_required", out error);
            if (!receipt.Identity.IsValid) return Reject("network_baseline_identity_invalid", out error);
            if (receipt.BindingAuthorityGeneration == 0 || receipt.BindingDiscontinuityGeneration == 0)
                return Reject("binding_generation_required", out error);
            if (receipt.ControlRevision == 0) return Reject("control_revision_required", out error);
            if (receipt.Ordinal < 0) return Reject("ledger_ordinal_invalid", out error);
            if (!receipt.ServerOwned) return Reject("server_authority_required", out error);
            if (!receipt.ProtocolOwned) return Reject("protocol_ownership_required", out error);
            if (receipt.Phase == GlobalMotionNetworkBaselineProtocolLedgerPhase.None)
                return Reject("ledger_phase_required", out error);
            if (receipt.Phase == GlobalMotionNetworkBaselineProtocolLedgerPhase.Faulted)
                return true;

            if (receipt.Phase >= GlobalMotionNetworkBaselineProtocolLedgerPhase.Captured && !receipt.CaptureReceipt)
                return Reject("capture_receipt_missing", out error);
            if (receipt.Phase >= GlobalMotionNetworkBaselineProtocolLedgerPhase.Applied && !receipt.ApplyReceipt)
                return Reject("apply_receipt_missing", out error);
            if (receipt.Phase >= GlobalMotionNetworkBaselineProtocolLedgerPhase.Validated && !receipt.ValidateReceipt)
                return Reject("validate_receipt_missing", out error);
            if (receipt.Phase >= GlobalMotionNetworkBaselineProtocolLedgerPhase.Restored &&
                (!receipt.RestoreReceipt || !receipt.OwnershipRestored || !receipt.LifetimeRestored || !receipt.BaselineGenerationRestored))
                return Reject("restore_receipt_incomplete", out error);
            if (receipt.Phase == GlobalMotionNetworkBaselineProtocolLedgerPhase.Completed &&
                (!receipt.CaptureReceipt || !receipt.ApplyReceipt || !receipt.ValidateReceipt || !receipt.RestoreReceipt))
                return Reject("completed_ledger_incomplete", out error);
            return true;
        }

        private static bool IsNext(GlobalMotionNetworkBaselineProtocolLedgerPhase current,
            GlobalMotionNetworkBaselineProtocolLedgerPhase next)
        {
            return (current == GlobalMotionNetworkBaselineProtocolLedgerPhase.CaptureRequested && next == GlobalMotionNetworkBaselineProtocolLedgerPhase.Captured) ||
                (current == GlobalMotionNetworkBaselineProtocolLedgerPhase.Captured && next == GlobalMotionNetworkBaselineProtocolLedgerPhase.ApplyRequested) ||
                (current == GlobalMotionNetworkBaselineProtocolLedgerPhase.ApplyRequested && next == GlobalMotionNetworkBaselineProtocolLedgerPhase.Applied) ||
                (current == GlobalMotionNetworkBaselineProtocolLedgerPhase.Applied && next == GlobalMotionNetworkBaselineProtocolLedgerPhase.ValidateRequested) ||
                (current == GlobalMotionNetworkBaselineProtocolLedgerPhase.ValidateRequested && next == GlobalMotionNetworkBaselineProtocolLedgerPhase.Validated) ||
                (current == GlobalMotionNetworkBaselineProtocolLedgerPhase.Validated && next == GlobalMotionNetworkBaselineProtocolLedgerPhase.RestoreRequested) ||
                (current == GlobalMotionNetworkBaselineProtocolLedgerPhase.RestoreRequested && next == GlobalMotionNetworkBaselineProtocolLedgerPhase.Restored) ||
                (current == GlobalMotionNetworkBaselineProtocolLedgerPhase.Restored && next == GlobalMotionNetworkBaselineProtocolLedgerPhase.Completed);
        }

        private static GlobalMotionNetworkBaselineProtocolLedgerReceipt Create(
            GlobalMotionNetworkBaselineReversibleCapability capability,
            GlobalMotionNetworkBaselineProtocolLedgerPhase phase,
            int ordinal,
            bool capture,
            bool apply,
            bool validate,
            bool restore,
            bool ownership,
            bool lifetime,
            bool generation)
        {
            return new GlobalMotionNetworkBaselineProtocolLedgerReceipt(
                capability.TransactionId, capability.ParticipantId, capability.Identity,
                capability.BindingAuthorityGeneration, capability.BindingDiscontinuityGeneration,
                1, 0, phase, ordinal, capability.ServerAuthoritative, capability.ProtocolOwned,
                capture, apply, validate, restore, ownership, lifetime, generation);
        }

        private static bool Reject(string reason, out string error)
        {
            error = reason;
            return false;
        }
    }
}
