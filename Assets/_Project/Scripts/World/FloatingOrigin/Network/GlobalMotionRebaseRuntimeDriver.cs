using System;

namespace ProjectC.World.FloatingOrigin.Network
{
    /// <summary>
    /// T-FO06AB: explicit user-controlled runtime boundary for the next rebase slice.
    /// The driver validates the trigger, delegates closed-world preparation to the existing
    /// coordinator and emits ordered transaction evidence. Native phases remain unreachable
    /// until a concrete adapter and a proven readiness bundle are supplied.
    /// </summary>
    public interface IGlobalMotionRebaseRuntimeDriverAdapter
    {
        bool TryPrepareUserControlled(
            GlobalMotionRebaseTriggerRequest trigger,
            out GlobalMotionRebaseRequest request,
            out GlobalMotionRebaseParticipantSet participants,
            out string error);

        bool TryApply(GlobalMotionRebaseRequest request, out string error);
        bool TryRebuild(GlobalMotionRebaseRequest request, out string error);
        bool TryValidate(GlobalMotionRebaseRequest request, out string error);
        bool TryPublish(GlobalMotionRebaseRequest request, out string error);
        bool TryRestore(GlobalMotionRebaseRequest request, out string error);
    }

    /// <summary>
    /// Runtime driver shell. It accepts no automatic threshold trigger and performs no Unity state mutation itself.
    /// Readiness authorization is explicit and fail-closed.
    /// </summary>
    public sealed class GlobalMotionRebaseRuntimeDriver
    {
        private readonly GlobalMotionRebaseCoordinator _coordinator;
        private readonly IGlobalMotionRebaseRuntimeDriverAdapter _adapter;
        private GlobalMotionRebaseTriggerRequest _trigger;
        private GlobalMotionRebaseRequest _request;
        private GlobalMotionRebaseReadinessBundle _readiness;
        private GlobalMotionRebaseRuntimeInstallationIntent _installationIntent;
        private bool _readinessAuthorized;
        private bool _installationIntentAuthorized;

        public GlobalMotionRebaseTransactionPhase Phase { get; private set; } = GlobalMotionRebaseTransactionPhase.Idle;
        public Guid ActiveTransactionId => _trigger.TransactionId;
        public GlobalMotionRebaseTransactionPhase LastTerminalPhase { get; private set; } = GlobalMotionRebaseTransactionPhase.Idle;
        public bool IsReadinessAuthorized => _readinessAuthorized && _readiness.IsReady;
        public bool IsInstallationIntentAuthorized => _installationIntentAuthorized &&
            IsReadinessAuthorized && _installationIntent.IsValid &&
            string.Equals(_installationIntent.ManifestDigest, _readiness.ManifestDigest, StringComparison.Ordinal) &&
            string.Equals(_installationIntent.SessionIdentity, _readiness.SessionIdentity, StringComparison.Ordinal);
        public GlobalMotionRebaseReadinessBundle Readiness => _readiness;
        public GlobalMotionRebaseRuntimeInstallationIntent InstallationIntent => _installationIntent;

        public GlobalMotionRebaseRuntimeDriver(
            GlobalMotionRebaseCoordinator coordinator,
            IGlobalMotionRebaseRuntimeDriverAdapter adapter)
        {
            _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
            _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
        }

        public bool TryAuthorizeReadiness(GlobalMotionRebaseReadinessBundle readiness, out string error)
        {
            error = null;
            if (Phase != GlobalMotionRebaseTransactionPhase.Idle)
            {
                error = "readiness_authorization_requires_idle:phase=" + Phase;
                return false;
            }
            if (!readiness.IsReady)
            {
                error = "readiness_bundle_not_ready:" + (string.IsNullOrEmpty(readiness.FailureReason) ? "unknown" : readiness.FailureReason);
                return false;
            }

            _installationIntent = default;
            _installationIntentAuthorized = false;
            _readiness = readiness;
            _readinessAuthorized = true;
            Record("ReadinessAuthorized", "manifest=" + readiness.ManifestDigest + ";session=" + readiness.SessionIdentity + ";participants=" + readiness.ParticipantCount + ";installationInvalidated=true");
            return true;
        }

        public bool TryAuthorizeInstallationIntent(
            GlobalMotionRebaseRuntimeInstallationIntent installationIntent,
            out string error)
        {
            error = null;
            if (Phase != GlobalMotionRebaseTransactionPhase.Idle)
            {
                error = "installation_authorization_requires_idle:phase=" + Phase;
                return false;
            }
            if (!IsReadinessAuthorized)
            {
                error = "readiness_bundle_not_authorized";
                return false;
            }
            if (_installationIntentAuthorized)
            {
                error = "installation_authorization_already_granted";
                return false;
            }
            if (!installationIntent.IsValid)
            {
                error = "installation_intent_invalid";
                return false;
            }
            if (!string.Equals(installationIntent.ManifestDigest, _readiness.ManifestDigest, StringComparison.Ordinal))
            {
                error = "installation_manifest_digest_mismatch";
                return false;
            }
            if (!string.Equals(installationIntent.SessionIdentity, _readiness.SessionIdentity, StringComparison.Ordinal))
            {
                error = "installation_session_identity_mismatch";
                return false;
            }

            _installationIntent = installationIntent;
            _installationIntentAuthorized = true;
            Record("InstallationAuthorized", "installation=" + installationIntent.InstallationId + ";reason=" + installationIntent.Reason);
            return true;
        }

        public bool TryRequestUserControlled(
            ulong frameGeneration,
            string reason,
            out string error)
        {
            error = null;
            if (!IsReadinessAuthorized)
            {
                error = "readiness_bundle_not_authorized";
                Record("Rejected", error);
                return false;
            }
            if (!IsInstallationIntentAuthorized)
            {
                error = "installation_intent_not_authorized";
                Record("Rejected", error);
                return false;
            }
            if (Phase != GlobalMotionRebaseTransactionPhase.Idle)
            {
                error = "driver_not_idle:phase=" + Phase;
                return false;
            }

            _trigger = new GlobalMotionRebaseTriggerRequest(
                Guid.NewGuid(),
                GlobalMotionRebaseTriggerKind.UserControlled,
                frameGeneration,
                reason);

            if (!_trigger.TryValidate(out error))
                return Fail(GlobalMotionRebaseTransactionPhase.Faulted, "trigger_invalid:" + error, out error);

            Phase = GlobalMotionRebaseTransactionPhase.Requested;
            Record("Requested", "kind=UserControlled installation=" + _installationIntent.InstallationId + ";reason=" + reason);

            if (!_adapter.TryPrepareUserControlled(_trigger, out _request, out var participants, out error))
                return AbortWithReason("frame_prepare_refused:" + error, out error);

            if (!_request.TryValidate(out error))
                return AbortWithReason("request_invalid:" + error, out error);

            if (!Advance(GlobalMotionRebaseTransactionPhase.FramePrepared, out error))
                return false;

            if (!_coordinator.TryPrepare(_request, participants, out error))
                return AbortWithReason("coordinator_prepare_refused:" + error, out error);

            if (!_adapter.TryApply(_request, out error))
                return RollbackAfterFailure("apply_refused:" + error, out error);
            if (!Advance(GlobalMotionRebaseTransactionPhase.Applied, out error))
                return RollbackAfterFailure("apply_phase_refused:" + error, out error);

            if (!_adapter.TryRebuild(_request, out error))
                return RollbackAfterFailure("rebuild_refused:" + error, out error);
            if (!Advance(GlobalMotionRebaseTransactionPhase.PhysicsSynchronized, out error))
                return RollbackAfterFailure("physics_phase_refused:" + error, out error);

            if (!_adapter.TryValidate(_request, out error))
                return RollbackAfterFailure("validate_refused:" + error, out error);
            if (!Advance(GlobalMotionRebaseTransactionPhase.Validated, out error))
                return RollbackAfterFailure("validation_phase_refused:" + error, out error);

            if (!_adapter.TryPublish(_request, out error))
                return RollbackAfterFailure("publish_refused:" + error, out error);
            if (!Advance(GlobalMotionRebaseTransactionPhase.Published, out error))
                return RollbackAfterFailure("publish_phase_refused:" + error, out error);

            if (!_coordinator.TryCommit(out error))
                return RollbackAfterFailure("coordinator_commit_refused:" + error, out error);

            if (!Advance(GlobalMotionRebaseTransactionPhase.Completed, out error))
                return RollbackAfterFailure("completion_phase_refused:" + error, out error);

            return true;
        }

        public bool TryReset(out string error)
        {
            error = null;
            if (Phase != GlobalMotionRebaseTransactionPhase.Aborted &&
                Phase != GlobalMotionRebaseTransactionPhase.Faulted &&
                Phase != GlobalMotionRebaseTransactionPhase.RollbackCompleted)
            {
                error = "driver_reset_requires_terminal_failure:phase=" + Phase;
                return false;
            }

            _coordinator.Reset();
            _trigger = default;
            _request = default;
            _readiness = GlobalMotionRebaseReadinessBundle.Blocked("readiness_reset");
            _installationIntent = default;
            _readinessAuthorized = false;
            _installationIntentAuthorized = false;
            Phase = GlobalMotionRebaseTransactionPhase.Idle;
            LastTerminalPhase = GlobalMotionRebaseTransactionPhase.Idle;
            Record("Reset", null);
            return true;
        }

        private bool AbortWithReason(string reason, out string error)
        {
            Record("AbortRequested", reason);
            var aborted = _coordinator.TryAbort(out var abortError);
            if (!aborted)
                return Fail(GlobalMotionRebaseTransactionPhase.Faulted, reason + ";coordinator_abort=" + abortError, out error);

            return Fail(GlobalMotionRebaseTransactionPhase.Aborted, reason, out error);
        }

        private bool Advance(GlobalMotionRebaseTransactionPhase next, out string error)
        {
            if (!GlobalMotionRebaseTransactionContract.TryAdvance(Phase, next, out error))
                return Fail(GlobalMotionRebaseTransactionPhase.Faulted, "phase_transition_refused:" + error, out error);

            Phase = next;
            Record(next.ToString(), null);
            return true;
        }

        private bool Fail(GlobalMotionRebaseTransactionPhase terminal, string reason, out string error)
        {
            if (_installationIntentAuthorized)
            {
                _installationIntent = default;
                _installationIntentAuthorized = false;
                Record("InstallationInvalidated", "reason=terminal_failure:" + reason);
            }

            Phase = terminal;
            LastTerminalPhase = terminal;
            error = reason;
            Record(terminal.ToString(), "reason=" + reason);
            return false;
        }

        private void Record(string phase, string payload)
        {
            GlobalMotionRuntimeEvidenceProbe.RecordEvent("rebase", phase, payload);
        }

        private bool RollbackAfterFailure(string reason, out string error)
        {
            Record("RollbackRequested", "reason=" + reason);
            var rollbackPhaseAdvanced = Advance(GlobalMotionRebaseTransactionPhase.RollbackBegun, out var phaseError);
            var adapterRestored = _adapter.TryRestore(_request, out var adapterError);
            var coordinatorAborted = _coordinator.TryAbort(out var coordinatorError);

            if (rollbackPhaseAdvanced)
                Advance(GlobalMotionRebaseTransactionPhase.Restored, out phaseError);

            var rollbackSucceeded = rollbackPhaseAdvanced && adapterRestored && coordinatorAborted &&
                Advance(GlobalMotionRebaseTransactionPhase.RollbackCompleted, out phaseError);
            if (!rollbackSucceeded)
            {
                var details = reason + ";phase=" + (phaseError ?? "unknown") +
                    ";adapter=" + (adapterError ?? "ok") +
                    ";coordinator=" + (coordinatorError ?? "ok");
                return Fail(GlobalMotionRebaseTransactionPhase.Faulted, "rollback_failed:" + details, out error);
            }

            if (_installationIntentAuthorized)
            {
                _installationIntent = default;
                _installationIntentAuthorized = false;
                Record("InstallationInvalidated", "reason=rollback_completed:" + reason);
            }

            error = reason;
            LastTerminalPhase = GlobalMotionRebaseTransactionPhase.RollbackCompleted;
            Record("RollbackCompleted", "reason=" + reason);
            return false;
        }
    }
}
