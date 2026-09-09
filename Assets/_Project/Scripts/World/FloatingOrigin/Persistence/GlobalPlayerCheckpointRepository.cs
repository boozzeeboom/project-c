using System;
using System.Collections.Generic;
using System.IO;

namespace ProjectC.World.FloatingOrigin.Persistence
{
    public enum CheckpointStoreStatus { Empty, Ready, Pending, RecoveryAvailable, Blocked }
    public enum CheckpointTransactionStatus { Applied, NotApplied, Conflict, Unavailable, Indeterminate, RecoveryRequired }
    /// <summary>Repository-instance-bound observation of all three raw files. Only Ready exposes an active snapshot.</summary>
    public sealed class CheckpointStoreObservation
    {
        internal Guid Owner { get; }
        internal string Fingerprint { get; }
        public CheckpointStoreStatus Status { get; }
        public GlobalPlayerCheckpointSnapshot Snapshot { get; }
        public GlobalPlayerCheckpointSnapshot RecoveryCandidate { get; }
        public string Error { get; }
        internal CheckpointStoreObservation(Guid owner, string fingerprint, CheckpointStoreStatus status, GlobalPlayerCheckpointSnapshot snapshot, GlobalPlayerCheckpointSnapshot recovery, string error)
        { Owner = owner; Fingerprint = fingerprint; Status = status; Snapshot = snapshot; RecoveryCandidate = recovery; Error = error; }
    }
    public sealed class CheckpointTransactionResult
    {
        public CheckpointTransactionStatus Status { get; }
        public CheckpointStoreObservation Observation { get; }
        public string Error { get; }
        public string QuarantineId { get; }
        internal CheckpointTransactionResult(CheckpointTransactionStatus status, CheckpointStoreObservation observation, string error, string quarantineId = null)
        { Status = status; Observation = observation; Error = error; QuarantineId = quarantineId; }
    }

    /// <summary>
    /// Explicit player-only repository, NOT a MonoBehaviour or replacement for ShipPositions.json.
    /// All operations hold a cooperative storage lease; no default path, auto-save, automatic rollback/backup adoption.
    /// The caller supplies authorized global checkpoints and persistent identity; this class is not an auth provider.
    /// </summary>
    public sealed class GlobalPlayerCheckpointRepository
    {
        private sealed class State
        {
            public byte[] Primary, Backup, Pending;
            public GlobalPlayerCheckpointSnapshot Head, Previous;
            public CheckpointStoreStatus Status;
            public string Error;
            public string Fingerprint => CheckpointBytes.Fingerprint(Primary, Backup, Pending);
        }
        private readonly Guid _owner = Guid.NewGuid();
        private readonly object _gate = new object();
        private readonly string _storeId;
        private readonly IGlobalPlayerCheckpointStorage _storage;
        public GlobalPlayerCheckpointRepository(string storeId, IGlobalPlayerCheckpointStorage storage)
        {
            if (!GlobalPlayerPositionRecord.IsValidIdentity(storeId) || storage == null) throw new ArgumentException("Explicit store identity and storage required.");
            _storeId = storeId; _storage = storage;
        }
        public CheckpointStoreObservation Inspect()
        {
            lock (_gate)
                try { using (_storage.AcquireLease()) return Observe(ReadState()); }
                catch (Exception e) { return Unavailable("inspect:" + e.GetType().Name); }
        }
        /// <summary>Optional synchronous read-only game-state guard, checked under the lease before staging and before publication.</summary>
        public CheckpointTransactionResult TryCommit(CheckpointStoreObservation expected, IReadOnlyList<GlobalPlayerPositionRecord> allPlayers, bool authorizePlayerRemovals = false, Func<bool> publicationGuard = null)
            => Write(expected, allPlayers, false, authorizePlayerRemovals, publicationGuard);
        public CheckpointTransactionResult TryRecoverBackup(CheckpointStoreObservation expected)
            => Write(expected, null, true, false, null);

        private CheckpointTransactionResult Write(CheckpointStoreObservation expected, IReadOnlyList<GlobalPlayerPositionRecord> players, bool recovery, bool allowRemovals, Func<bool> publicationGuard)
        {
            if (!Owned(expected)) return Result(CheckpointTransactionStatus.Conflict, null, "observation_not_owned_by_this_repository");
            lock (_gate)
            {
                bool writeAttempted = false;
                try
                {
                    using (_storage.AcquireLease())
                    {
                        var before = ReadState();
                        if (before.Fingerprint != expected.Fingerprint) return Result(CheckpointTransactionStatus.Conflict, before, "store_changed_since_observation");
                        if (recovery ? before.Status != CheckpointStoreStatus.RecoveryAvailable : before.Status != CheckpointStoreStatus.Ready && before.Status != CheckpointStoreStatus.Empty)
                            return Result(CheckpointTransactionStatus.NotApplied, before, "explicit_pending_recovery_or_blocked_store");
                        var basis = recovery ? before.Previous : before.Head;
                        GlobalPlayerCheckpointSnapshot candidate;
                        try
                        {
                            if (basis != null && basis.Revision == long.MaxValue) throw new InvalidOperationException("Revision exhausted.");
                            candidate = new GlobalPlayerCheckpointSnapshot(_storeId, basis == null ? 1 : basis.Revision + 1, Guid.NewGuid().ToString("N"), basis == null ? "" : basis.CommitId,
                                recovery ? basis.Players : players);
                            if (!recovery && basis != null) ValidateReplacement(basis, candidate, allowRemovals);
                        }
                        catch (Exception e) { return Result(CheckpointTransactionStatus.NotApplied, before, "invalid_snapshot_request:" + e.Message); }
                        if (!GlobalPlayerCheckpointSnapshotCodec.TryEncode(candidate, out var bytes, out var encodeError))
                            return Result(CheckpointTransactionStatus.NotApplied, before, encodeError);
                        if (publicationGuard != null)
                        {
                            bool allowed = false; string guardError = null;
                            try { allowed = publicationGuard(); } catch (Exception e) { guardError = e.GetType().Name; }
                            var guarded = ReadState();
                            if (guarded.Fingerprint != before.Fingerprint) return Result(CheckpointTransactionStatus.Conflict, guarded, "store_changed_during_publication_guard");
                            if (!allowed) return Result(CheckpointTransactionStatus.NotApplied, before, "publication_guard_refused_before_staging:" + guardError);
                        }
                        string quarantine = recovery && before.Primary != null ? Guid.NewGuid().ToString("N") : null;
                        Exception operationError = null;
                        try
                        {
                            writeAttempted = true; _storage.WritePendingNew(bytes);
                            var staged = ReadState();
                            if (!CheckpointBytes.Equal(staged.Primary, before.Primary) || !CheckpointBytes.Equal(staged.Backup, before.Backup) || !CheckpointBytes.Equal(staged.Pending, bytes))
                                throw new IOException("State or staged bytes changed before publication.");
                            if (publicationGuard != null)
                            {
                                if (!publicationGuard()) throw new InvalidOperationException("Publication guard refused after staging.");
                                var guarded = ReadState();
                                if (!CheckpointBytes.Equal(guarded.Primary, before.Primary) || !CheckpointBytes.Equal(guarded.Backup, before.Backup) || !CheckpointBytes.Equal(guarded.Pending, bytes))
                                    throw new IOException("Store changed during final publication guard.");
                            }
                            _storage.PublishPending(before.Primary != null, quarantine);
                        }
                        catch (Exception e) { operationError = e; }
                        // A throw can happen AFTER native publication. Never claim rollback merely from an exception.
                        var after = ReadState();
                        bool backupIntact = CheckpointBytes.Equal(after.Backup, recovery ? before.Backup : before.Primary);
                        bool displacedIntact = quarantine == null || CheckpointBytes.Equal(_storage.ReadQuarantine(quarantine, GlobalPlayerCheckpointSnapshotCodec.MaxBytes), before.Primary);
                        if (after.Status == CheckpointStoreStatus.Ready && CheckpointBytes.Equal(after.Primary, bytes) && backupIntact && displacedIntact)
                            return Result(CheckpointTransactionStatus.Applied, after, operationError == null ? null : "publication_verified_after_exception:" + operationError.GetType().Name, quarantine);
                        if (CheckpointBytes.Equal(after.Primary, before.Primary) && CheckpointBytes.Equal(after.Backup, before.Backup))
                            return Result(CheckpointTransactionStatus.NotApplied, after, "publication_not_applied_pending_retained:" + (operationError?.GetType().Name ?? "verification_failed"), quarantine);
                        return Result(CheckpointTransactionStatus.RecoveryRequired, after, "publication_state_or_backup_invariant_failed:" + operationError?.GetType().Name, quarantine);
                    }
                }
                catch (Exception e)
                { return new CheckpointTransactionResult(writeAttempted ? CheckpointTransactionStatus.Indeterminate : CheckpointTransactionStatus.Unavailable, Unavailable(e.GetType().Name), e.GetType().Name); }
            }
        }
        /// <summary>Explicitly quarantine stale/partial pending bytes; never promote a pending file merely because it parses.</summary>
        public CheckpointTransactionResult TryQuarantinePending(CheckpointStoreObservation expected)
        {
            if (!Owned(expected)) return Result(CheckpointTransactionStatus.Conflict, null, "observation_not_owned_by_this_repository");
            lock (_gate)
            {
                bool attempted = false; string quarantine = Guid.NewGuid().ToString("N");
                try
                {
                    using (_storage.AcquireLease())
                    {
                        var before = ReadState();
                        if (before.Fingerprint != expected.Fingerprint) return Result(CheckpointTransactionStatus.Conflict, before, "store_changed_since_observation");
                        if (before.Pending == null) return Result(CheckpointTransactionStatus.NotApplied, before, "no_pending_file");
                        Exception error = null;
                        try { attempted = true; _storage.QuarantinePending(quarantine); } catch (Exception e) { error = e; }
                        var after = ReadState();
                        if (after.Pending == null && CheckpointBytes.Equal(after.Primary, before.Primary) && CheckpointBytes.Equal(after.Backup, before.Backup) &&
                            CheckpointBytes.Equal(_storage.ReadQuarantine(quarantine, GlobalPlayerCheckpointSnapshotCodec.MaxBytes), before.Pending))
                            return Result(CheckpointTransactionStatus.Applied, after, error == null ? null : "quarantine_verified_after_exception:" + error.GetType().Name, quarantine);
                        if (after.Fingerprint == before.Fingerprint) return Result(CheckpointTransactionStatus.NotApplied, after, "quarantine_not_applied", quarantine);
                        return Result(CheckpointTransactionStatus.RecoveryRequired, after, "quarantine_verification_failed", quarantine);
                    }
                }
                catch (Exception e) { return new CheckpointTransactionResult(attempted ? CheckpointTransactionStatus.Indeterminate : CheckpointTransactionStatus.Unavailable, Unavailable(e.GetType().Name), e.GetType().Name, quarantine); }
            }
        }
        private State ReadState()
        {
            var state = new State { Primary = _storage.Read(CheckpointSlot.Primary, GlobalPlayerCheckpointSnapshotCodec.MaxBytes),
                Backup = _storage.Read(CheckpointSlot.Backup, GlobalPlayerCheckpointSnapshotCodec.MaxBytes), Pending = _storage.Read(CheckpointSlot.Pending, GlobalPlayerCheckpointSnapshotCodec.MaxBytes) };
            string headError = null, backupError = null;
            bool headValid = state.Primary != null && GlobalPlayerCheckpointSnapshotCodec.TryDecode(state.Primary, _storeId, out state.Head, out headError);
            bool backupValid = state.Backup != null && GlobalPlayerCheckpointSnapshotCodec.TryDecode(state.Backup, _storeId, out state.Previous, out backupError);
            if (IsUnsupported(headError) || IsUnsupported(backupError)) { state.Status = CheckpointStoreStatus.Blocked; state.Error = "foreign_or_unsupported_snapshot_no_automatic_downgrade"; return state; }
            if (state.Backup != null && !backupValid) { state.Status = CheckpointStoreStatus.Blocked; state.Error = "backup_invalid:" + backupError; return state; }
            if (headValid)
            {
                bool chainValid = state.Head.Revision == 1 ? state.Backup == null : backupValid && state.Previous.Revision == state.Head.Revision - 1 && state.Previous.CommitId == state.Head.ParentCommitId;
                if (!chainValid) { state.Status = CheckpointStoreStatus.Blocked; state.Error = "backup_lineage_missing_or_inconsistent"; return state; }
                state.Status = CheckpointStoreStatus.Ready;
            }
            else if (backupValid) { state.Status = CheckpointStoreStatus.RecoveryAvailable; state.Error = "explicit_backup_recovery_required:" + headError; }
            else if (state.Primary == null) state.Status = CheckpointStoreStatus.Empty;
            else { state.Status = CheckpointStoreStatus.Blocked; state.Error = "primary_invalid_without_usable_backup:" + headError; return state; }
            if (state.Pending != null) { state.Status = CheckpointStoreStatus.Pending; state.Error = "pending_file_requires_explicit_quarantine"; }
            return state;
        }
        private static bool IsUnsupported(string error) => error == GlobalPlayerCheckpointSnapshotCodec.Unsupported || error == GlobalPlayerCheckpointSnapshotCodec.ForeignStore;
        private static void ValidateReplacement(GlobalPlayerCheckpointSnapshot previous, GlobalPlayerCheckpointSnapshot candidate, bool allowRemovals)
        {
            var records = new Dictionary<string, GlobalPlayerPositionRecord>(StringComparer.Ordinal);
            foreach (var player in candidate.Players) records.Add(player.PlayerId, player);
            foreach (var prior in previous.Players)
            {
                if (!records.TryGetValue(prior.PlayerId, out var current)) { if (!allowRemovals) throw new InvalidOperationException("Explicit player removal authorization required; do not lose offline checkpoints."); }
                else if (current.SavedAtUnix < prior.SavedAtUnix) throw new InvalidOperationException("Older player checkpoint cannot overwrite a newer one.");
            }
        }
        private bool Owned(CheckpointStoreObservation observation) => observation != null && observation.Owner == _owner && observation.Fingerprint != null;
        private CheckpointStoreObservation Observe(State state) => new CheckpointStoreObservation(_owner, state.Fingerprint, state.Status,
            state.Status == CheckpointStoreStatus.Ready ? state.Head : null, state.Status == CheckpointStoreStatus.RecoveryAvailable ? state.Previous : null, state.Error);
        private CheckpointStoreObservation Unavailable(string error) => new CheckpointStoreObservation(Guid.Empty, null, CheckpointStoreStatus.Blocked, null, null, "storage_unavailable:" + error);
        private CheckpointTransactionResult Result(CheckpointTransactionStatus status, State state, string error, string quarantine = null)
            => new CheckpointTransactionResult(status, state == null ? null : Observe(state), error, quarantine);
    }
}
