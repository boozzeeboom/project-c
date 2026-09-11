using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ProjectC.World.FloatingOrigin;

namespace ProjectC.World.FloatingOrigin.Network
{
    public enum GlobalMotionRebasePhase : byte
    {
        Idle,
        Requested,
        Frozen,
        Preflighted,
        Captured,
        Aborted,
        Faulted
    }

    public enum GlobalMotionRebaseParticipantKind : byte
    {
        CityStatic,
        WorldAnchor,
        ShipRoot,
        ShipDeckNav,
        PlayerFrame,
        Camera,
        NetworkGameplayRoot
    }

    public interface IGlobalMotionRebaseSnapshot
    {
        string ParticipantId { get; }
    }

    /// <summary>
    /// Explicit participant contract for the first rebase integration slice.
    /// Implementations must not mutate Unity state from preflight or capture.
    /// Apply/rebuild/validate methods are reserved for the following implementation slice.
    /// </summary>
    public interface IGlobalMotionRebaseParticipant
    {
        string ParticipantId { get; }
        GlobalMotionRebaseParticipantKind Kind { get; }
        bool IsCurrent { get; }
        bool TryPreflight(GlobalMotionRebaseRequest request, out string error);
        bool TryCapture(GlobalMotionRebaseRequest request, out IGlobalMotionRebaseSnapshot snapshot, out string error);
        bool TryApply(GlobalMotionRebaseRequest request, out string error);
        bool TryRebuild(GlobalMotionRebaseRequest request, out string error);
        bool TryValidate(GlobalMotionRebaseRequest request, out string error);
        bool TryRestore(GlobalMotionRebaseRequest request, IGlobalMotionRebaseSnapshot snapshot, out string error);
    }

    /// <summary>
    /// Global freeze/release boundary. A missing or failed gate blocks preparation.
    /// This interface intentionally contains no Transform or NetworkObject operations.
    /// </summary>
    public interface IGlobalMotionRebaseGate
    {
        bool TryFreeze(GlobalMotionRebaseRequest request, out string error);
        bool TryRelease(GlobalMotionRebaseRequest request, out string error);
    }

    public readonly struct GlobalMotionRebaseRequest
    {
        public Guid TransactionId { get; }
        public ulong FrameGeneration { get; }
        public OriginRebasePlan Plan { get; }
        public int ExpectedParticipantCount { get; }
        public string ParticipantManifestDigest { get; }

        public bool IsValid => TransactionId != Guid.Empty && FrameGeneration > 0 && Plan.IsValid &&
            ExpectedParticipantCount > 0 && !string.IsNullOrWhiteSpace(ParticipantManifestDigest);

        private GlobalMotionRebaseRequest(Guid transactionId, ulong frameGeneration, OriginRebasePlan plan,
            int expectedParticipantCount, string participantManifestDigest)
        {
            TransactionId = transactionId;
            FrameGeneration = frameGeneration;
            Plan = plan;
            ExpectedParticipantCount = expectedParticipantCount;
            ParticipantManifestDigest = participantManifestDigest;
        }

        public static GlobalMotionRebaseRequest Create(ulong frameGeneration, OriginRebasePlan plan,
            int expectedParticipantCount, string participantManifestDigest)
        {
            return new GlobalMotionRebaseRequest(Guid.NewGuid(), frameGeneration, plan,
                expectedParticipantCount, participantManifestDigest);
        }

        public bool TryValidate(out string error)
        {
            error = null;
            if (TransactionId == Guid.Empty) { error = "transaction_id_required"; return false; }
            if (FrameGeneration == 0) { error = "frame_generation_required"; return false; }
            if (!Plan.IsValid) { error = "valid_origin_rebase_plan_required"; return false; }
            if (ExpectedParticipantCount <= 0) { error = "expected_participant_count_required"; return false; }
            if (string.IsNullOrWhiteSpace(ParticipantManifestDigest)) { error = "participant_manifest_digest_required"; return false; }
            return true;
        }
    }

    /// <summary>
    /// Sealed, ordered participant set. The coordinator treats count, identity and current-state
    /// checks as a closed-world admission gate; it never discovers or adopts unknown objects.
    /// </summary>
    public sealed class GlobalMotionRebaseParticipantSet
    {
        private readonly List<IGlobalMotionRebaseParticipant> _items = new List<IGlobalMotionRebaseParticipant>();
        private readonly HashSet<string> _ids = new HashSet<string>(StringComparer.Ordinal);
        private bool _sealed;

        public IReadOnlyList<IGlobalMotionRebaseParticipant> Items => new ReadOnlyCollection<IGlobalMotionRebaseParticipant>(_items);
        public int Count => _items.Count;
        public bool IsSealed => _sealed;

        public bool TryAdd(IGlobalMotionRebaseParticipant participant, out string error)
        {
            error = null;
            if (_sealed) { error = "participant_set_already_sealed"; return false; }
            if (participant == null) { error = "participant_missing"; return false; }
            if (string.IsNullOrWhiteSpace(participant.ParticipantId)) { error = "participant_id_required"; return false; }
            if (!_ids.Add(participant.ParticipantId)) { error = "duplicate_participant_id:" + participant.ParticipantId; return false; }
            _items.Add(participant);
            return true;
        }

        public bool Seal(out string error)
        {
            error = null;
            if (_sealed) { error = "participant_set_already_sealed"; return false; }
            if (_items.Count == 0) { error = "participant_set_empty"; return false; }
            _sealed = true;
            return true;
        }

        public bool TryValidateClosedWorld(GlobalMotionRebaseRequest request, out string error)
        {
            error = null;
            if (!_sealed) { error = "participant_set_must_be_sealed"; return false; }
            if (_items.Count != request.ExpectedParticipantCount)
            {
                error = "participant_count_mismatch:expected=" + request.ExpectedParticipantCount + ";actual=" + _items.Count;
                return false;
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < _items.Count; i++)
            {
                var participant = _items[i];
                if (participant == null) { error = "participant_missing:index=" + i; return false; }
                if (string.IsNullOrWhiteSpace(participant.ParticipantId)) { error = "participant_id_required:index=" + i; return false; }
                if (!ids.Add(participant.ParticipantId)) { error = "duplicate_participant_id:" + participant.ParticipantId; return false; }
                if (!participant.IsCurrent) { error = "stale_participant:" + participant.ParticipantId; return false; }
            }
            return true;
        }
    }

    /// <summary>
    /// First implementation slice: closed-world request, freeze gate, participant preflight and
    /// immutable rollback snapshots. It deliberately stops at Captured; no Transform, Rigidbody,
    /// NavMesh, camera, network baseline or frame publication is performed here.
    /// </summary>
    public sealed class GlobalMotionRebaseCoordinator
    {
        private readonly IGlobalMotionRebaseGate _gate;
        private readonly List<IGlobalMotionRebaseSnapshot> _snapshots = new List<IGlobalMotionRebaseSnapshot>();
        private GlobalMotionRebaseParticipantSet _participants;
        private GlobalMotionRebaseRequest _request;
        private bool _frozen;

        public GlobalMotionRebasePhase Phase { get; private set; } = GlobalMotionRebasePhase.Idle;
        public Guid ActiveTransactionId => _request.TransactionId;
        public int CapturedParticipantCount => _snapshots.Count;

        public GlobalMotionRebaseCoordinator(IGlobalMotionRebaseGate gate)
        {
            _gate = gate ?? throw new ArgumentNullException(nameof(gate));
        }

        public bool TryPrepare(GlobalMotionRebaseRequest request, GlobalMotionRebaseParticipantSet participants, out string error)
        {
            error = null;
            if (Phase != GlobalMotionRebasePhase.Idle)
            {
                error = "coordinator_not_idle:phase=" + Phase;
                return false;
            }
            if (!request.TryValidate(out error)) return false;
            if (participants == null) { error = "participant_set_required"; return false; }
            if (!participants.Seal(out error)) return false;
            if (!participants.TryValidateClosedWorld(request, out error)) return false;

            _request = request;
            _participants = participants;
            _snapshots.Clear();
            Phase = GlobalMotionRebasePhase.Requested;

            if (!_gate.TryFreeze(request, out error))
                return FailBeforeCapture("freeze_gate_refused:" + error, out error);
            _frozen = true;
            Phase = GlobalMotionRebasePhase.Frozen;

            for (int i = 0; i < participants.Items.Count; i++)
            {
                var participant = participants.Items[i];
                if (!participant.TryPreflight(request, out error))
                    return FailBeforeCapture("participant_preflight_refused:" + participant.ParticipantId + ":" + error, out error);
            }
            Phase = GlobalMotionRebasePhase.Preflighted;

            for (int i = 0; i < participants.Items.Count; i++)
            {
                var participant = participants.Items[i];
                if (!participant.TryCapture(request, out var snapshot, out error))
                    return FailBeforeCapture("participant_capture_refused:" + participant.ParticipantId + ":" + error, out error);
                if (snapshot == null || !string.Equals(snapshot.ParticipantId, participant.ParticipantId, StringComparison.Ordinal))
                    return FailBeforeCapture("participant_snapshot_identity_mismatch:" + participant.ParticipantId, out error);
                _snapshots.Add(snapshot);
            }
            Phase = GlobalMotionRebasePhase.Captured;
            return true;
        }

        public bool TryAbort(out string error)
        {
            error = null;
            if (Phase == GlobalMotionRebasePhase.Idle || Phase == GlobalMotionRebasePhase.Aborted)
            {
                error = "no_active_rebase_transaction";
                return false;
            }
            return AbortInternal(out error);
        }

        public void Reset()
        {
            if (Phase != GlobalMotionRebasePhase.Aborted && Phase != GlobalMotionRebasePhase.Faulted)
                throw new InvalidOperationException("Only an aborted or faulted transaction can be reset.");
            _participants = null;
            _snapshots.Clear();
            _request = default;
            _frozen = false;
            Phase = GlobalMotionRebasePhase.Idle;
        }

        private bool FailBeforeCapture(string reason, out string error)
        {
            var abortError = AbortInternal(out var cleanupError) ? null : cleanupError;
            error = abortError == null ? reason : reason + ";cleanup=" + abortError;
            return false;
        }

        private bool AbortInternal(out string error)
        {
            var failures = new List<string>();
            if (_participants != null)
            {
                for (int i = _snapshots.Count - 1; i >= 0; i--)
                {
                    var snapshot = _snapshots[i];
                    IGlobalMotionRebaseParticipant participant = null;
                    for (int p = 0; p < _participants.Items.Count; p++)
                    {
                        if (string.Equals(_participants.Items[p].ParticipantId, snapshot.ParticipantId, StringComparison.Ordinal))
                        {
                            participant = _participants.Items[p];
                            break;
                        }
                    }
                    string restoreError = null;
                    var restored = participant != null && participant.TryRestore(_request, snapshot, out restoreError);
                    if (!restored)
                        failures.Add("restore:" + snapshot.ParticipantId + ":" + (restoreError ?? "participant_missing"));
                }
            }

            if (_frozen && !_gate.TryRelease(_request, out var releaseError))
                failures.Add("release:" + releaseError);

            _frozen = false;
            Phase = failures.Count == 0 ? GlobalMotionRebasePhase.Aborted : GlobalMotionRebasePhase.Faulted;
            error = failures.Count == 0 ? null : string.Join("|", failures);
            return failures.Count == 0;
        }
    }
}
