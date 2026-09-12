using System;

namespace ProjectC.World.FloatingOrigin.Network
{
    public enum GlobalMotionNetworkBaselineReversiblePhase : byte
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
    /// Reviewed capability boundary for a protocol-owned, server-coordinated NGO baseline transaction.
    /// This contract only validates declared boundaries; it does not inspect or mutate NGO state.
    /// </summary>
    public readonly struct GlobalMotionNetworkBaselineReversibleCapability
    {
        public string ParticipantId { get; }
        public string TransactionId { get; }
        public GlobalMotionNetworkBaselineIdentity Identity { get; }
        public ulong BindingAuthorityGeneration { get; }
        public ulong BindingDiscontinuityGeneration { get; }
        public bool ServerAuthoritative { get; }
        public bool ProtocolOwned { get; }
        public bool CaptureBoundaryDefined { get; }
        public bool ApplyBoundaryDefined { get; }
        public bool ValidateBoundaryDefined { get; }
        public bool RestoreBoundaryDefined { get; }
        public bool OwnershipRestoreDefined { get; }
        public bool LifetimeRestoreDefined { get; }
        public bool BaselineGenerationRestoreDefined { get; }

        public bool IsComplete => ServerAuthoritative && ProtocolOwned &&
            CaptureBoundaryDefined && ApplyBoundaryDefined && ValidateBoundaryDefined &&
            RestoreBoundaryDefined && OwnershipRestoreDefined && LifetimeRestoreDefined &&
            BaselineGenerationRestoreDefined;

        public GlobalMotionNetworkBaselineReversibleCapability(
            string participantId,
            string transactionId,
            GlobalMotionNetworkBaselineIdentity identity,
            ulong bindingAuthorityGeneration,
            ulong bindingDiscontinuityGeneration,
            bool serverAuthoritative,
            bool protocolOwned,
            bool captureBoundaryDefined,
            bool applyBoundaryDefined,
            bool validateBoundaryDefined,
            bool restoreBoundaryDefined,
            bool ownershipRestoreDefined,
            bool lifetimeRestoreDefined,
            bool baselineGenerationRestoreDefined)
        {
            ParticipantId = participantId;
            TransactionId = transactionId;
            Identity = identity;
            BindingAuthorityGeneration = bindingAuthorityGeneration;
            BindingDiscontinuityGeneration = bindingDiscontinuityGeneration;
            ServerAuthoritative = serverAuthoritative;
            ProtocolOwned = protocolOwned;
            CaptureBoundaryDefined = captureBoundaryDefined;
            ApplyBoundaryDefined = applyBoundaryDefined;
            ValidateBoundaryDefined = validateBoundaryDefined;
            RestoreBoundaryDefined = restoreBoundaryDefined;
            OwnershipRestoreDefined = ownershipRestoreDefined;
            LifetimeRestoreDefined = lifetimeRestoreDefined;
            BaselineGenerationRestoreDefined = baselineGenerationRestoreDefined;
        }
    }

    /// <summary>
    /// Receipt shape for a future protocol-owned baseline transaction. A receipt is valid only
    /// when the capability and exact identity/generation lineage remain unchanged.
    /// </summary>
    public readonly struct GlobalMotionNetworkBaselineReversibleReceipt
    {
        public GlobalMotionNetworkBaselineReversibleCapability Capability { get; }
        public GlobalMotionNetworkBaselineReversiblePhase Phase { get; }
        public bool CaptureCompleted { get; }
        public bool ApplyCompleted { get; }
        public bool ValidationCompleted { get; }
        public bool RestoreCompleted { get; }

        public GlobalMotionNetworkBaselineReversibleReceipt(
            GlobalMotionNetworkBaselineReversibleCapability capability,
            GlobalMotionNetworkBaselineReversiblePhase phase,
            bool captureCompleted,
            bool applyCompleted,
            bool validationCompleted,
            bool restoreCompleted)
        {
            Capability = capability;
            Phase = phase;
            CaptureCompleted = captureCompleted;
            ApplyCompleted = applyCompleted;
            ValidationCompleted = validationCompleted;
            RestoreCompleted = restoreCompleted;
        }
    }

    public static class GlobalMotionNetworkBaselineReversibleTransactionContract
    {
        public static bool TryBuildCapability(
            GlobalMotionNetworkBaselineEvidence evidence,
            bool serverAuthoritative,
            bool protocolOwned,
            bool captureBoundaryDefined,
            bool applyBoundaryDefined,
            bool validateBoundaryDefined,
            bool restoreBoundaryDefined,
            bool ownershipRestoreDefined,
            bool lifetimeRestoreDefined,
            bool baselineGenerationRestoreDefined,
            string transactionId,
            out GlobalMotionNetworkBaselineReversibleCapability capability,
            out string error)
        {
            capability = default;
            error = null;
            if (!GlobalMotionNetworkBaselineContract.TryValidate(evidence, out error)) return false;
            if (!GlobalMotionNetworkBaselineContract.CanClaimFullTransaction(evidence))
                return Reject("network_baseline_evidence_not_restorable", out error);
            if (!serverAuthoritative) return Reject("network_baseline_server_authority_missing", out error);
            if (!protocolOwned) return Reject("network_baseline_protocol_ownership_missing", out error);
            if (string.IsNullOrWhiteSpace(transactionId) || transactionId.Trim() != transactionId)
                return Reject("network_baseline_transaction_id_required", out error);
            if (!captureBoundaryDefined) return Reject("network_baseline_capture_boundary_missing", out error);
            if (!applyBoundaryDefined) return Reject("network_baseline_apply_boundary_missing", out error);
            if (!validateBoundaryDefined) return Reject("network_baseline_validate_boundary_missing", out error);
            if (!restoreBoundaryDefined) return Reject("network_baseline_restore_boundary_missing", out error);
            if (!ownershipRestoreDefined) return Reject("network_baseline_ownership_restore_missing", out error);
            if (!lifetimeRestoreDefined) return Reject("network_baseline_lifetime_restore_missing", out error);
            if (!baselineGenerationRestoreDefined) return Reject("network_baseline_generation_restore_missing", out error);

            capability = new GlobalMotionNetworkBaselineReversibleCapability(
                evidence.ParticipantId,
                transactionId,
                evidence.Identity,
                evidence.BindingAuthorityGeneration,
                evidence.BindingDiscontinuityGeneration,
                serverAuthoritative,
                protocolOwned,
                captureBoundaryDefined,
                applyBoundaryDefined,
                validateBoundaryDefined,
                restoreBoundaryDefined,
                ownershipRestoreDefined,
                lifetimeRestoreDefined,
                baselineGenerationRestoreDefined);
            return true;
        }

        public static bool TryValidateReceipt(
            GlobalMotionNetworkBaselineReversibleReceipt receipt,
            out string error)
        {
            error = null;
            if (!receipt.Capability.IsComplete) return Reject("reversible_capability_incomplete", out error);
            if (!GlobalMotionNetworkBaselineContract.CanClaimFullTransaction(new GlobalMotionNetworkBaselineEvidence(
                    receipt.Capability.ParticipantId,
                    receipt.Capability.Identity,
                    receipt.Capability.Identity.SessionId,
                    receipt.Capability.Identity.NetworkObjectId,
                    receipt.Capability.Identity.SpawnGeneration,
                    receipt.Capability.BindingAuthorityGeneration,
                    receipt.Capability.BindingDiscontinuityGeneration,
                    1, 1, 1, false, 0, false, 0, 1, 1, true, true, true, true, true, true, true, true,
                    GlobalMotionNetworkBaselineStateClassification.Restorable,
                    true, true, true, true, true)))
                return Reject("reversible_capability_evidence_invalid", out error);
            if (receipt.Phase == GlobalMotionNetworkBaselineReversiblePhase.None ||
                receipt.Phase == GlobalMotionNetworkBaselineReversiblePhase.Faulted)
                return Reject("reversible_receipt_phase_invalid", out error);
            if (receipt.Phase >= GlobalMotionNetworkBaselineReversiblePhase.Captured && !receipt.CaptureCompleted)
                return Reject("reversible_capture_receipt_missing", out error);
            if (receipt.Phase >= GlobalMotionNetworkBaselineReversiblePhase.Applied && !receipt.ApplyCompleted)
                return Reject("reversible_apply_receipt_missing", out error);
            if (receipt.Phase >= GlobalMotionNetworkBaselineReversiblePhase.Validated && !receipt.ValidationCompleted)
                return Reject("reversible_validation_receipt_missing", out error);
            if (receipt.Phase >= GlobalMotionNetworkBaselineReversiblePhase.Restored && !receipt.RestoreCompleted)
                return Reject("reversible_restore_receipt_missing", out error);
            return true;
        }

        public static bool CanAuthorizeNativeBaselineAdapter(GlobalMotionNetworkBaselineReversibleCapability capability)
        {
            return capability.IsComplete && capability.Identity.IsValid &&
                capability.BindingAuthorityGeneration != 0 && capability.BindingDiscontinuityGeneration != 0;
        }

        private static bool Reject(string reason, out string error)
        {
            error = reason;
            return false;
        }
    }
}
