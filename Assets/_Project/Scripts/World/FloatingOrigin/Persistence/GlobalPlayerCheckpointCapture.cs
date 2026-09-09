using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;

namespace ProjectC.World.FloatingOrigin.Persistence
{
    /// <summary>Single-use prepared operation. Only Players can become durable records; Source evidence is memory-only.</summary>
    public sealed class GlobalPlayerCheckpointCaptureBatch
    {
        internal Guid Owner { get; }
        internal bool Consumed { get; set; }
        internal CheckpointStoreObservation Storage { get; }
        internal double MaximumAge { get; }
        public GlobalPlayerCaptureSnapshot Source { get; }
        public IReadOnlyList<GlobalPlayerPositionRecord> Players { get; }
        internal GlobalPlayerCheckpointCaptureBatch(Guid owner, CheckpointStoreObservation storage, GlobalPlayerCaptureSnapshot source, double maximumAge, List<GlobalPlayerPositionRecord> players)
        { Owner = owner; Storage = storage; Source = source; MaximumAge = maximumAge; Players = new ReadOnlyCollection<GlobalPlayerPositionRecord>(players.ToArray()); }
    }
    /// <summary>
    /// Explicit synchronous coordinator: accepted capture -> merge offline records -> B CAS with live pre-publication guard.
    /// No auto-save loop, native disk defaults, identity inference, teleport/restore or scene activation.
    /// Use on the source-owning Unity thread; native disk latency and later async scheduling remain acceptance work.
    /// </summary>
    public sealed class GlobalPlayerCheckpointCapture
    {
        private readonly IGlobalPlayerCheckpointSource _source;
        private readonly GlobalPlayerCheckpointRepository _repository;
        private readonly Guid _owner = Guid.NewGuid();
        private readonly int _threadId = Thread.CurrentThread.ManagedThreadId;
        private bool _busy;
        public GlobalPlayerCheckpointCapture(IGlobalPlayerCheckpointSource source, GlobalPlayerCheckpointRepository repository)
        { _source = source ?? throw new ArgumentNullException(nameof(source)); _repository = repository ?? throw new ArgumentNullException(nameof(repository)); }
        public bool TryPrepare(CheckpointStoreObservation storage, long checkpointAtUnix, double maximumSampleAge, out GlobalPlayerCheckpointCaptureBatch batch, out string error)
        {
            batch = null; error = null;
            if (_busy || Thread.CurrentThread.ManagedThreadId != _threadId) return PositionJsonSafety.Fail("capture_coordinator_reentrant_or_wrong_thread", out error);
            _busy = true;
            try
            {
                if (storage == null || (storage.Status != CheckpointStoreStatus.Ready && storage.Status != CheckpointStoreStatus.Empty) ||
                    (storage.Status == CheckpointStoreStatus.Ready && storage.Snapshot == null))
                    return PositionJsonSafety.Fail("explicit_ready_or_empty_storage_observation_required", out error);
                if (checkpointAtUnix < 0 || checkpointAtUnix > GlobalPlayerPositionRecord.MaxSavedAtUnix) return PositionJsonSafety.Fail("trusted_checkpoint_timestamp_invalid", out error);
                if (!_source.TryCapture(maximumSampleAge, out var captured, out error) || !GlobalPlayerCapturePolicy.Validate(captured, maximumSampleAge, out error)) return false;
                var merged = new Dictionary<string, GlobalPlayerPositionRecord>(StringComparer.Ordinal);
                if (storage.Snapshot != null) foreach (var record in storage.Snapshot.Players) merged.Add(record.PlayerId, record);
                foreach (var entry in captured.Entries)
                {
                    if (merged.TryGetValue(entry.PlayerId, out var previous) && previous.SavedAtUnix > checkpointAtUnix)
                        return PositionJsonSafety.Fail("capture_timestamp_older_than_saved_player", out error);
                    merged[entry.PlayerId] = new GlobalPlayerPositionRecord(entry.PlayerId, entry.Sample.WorldPosition, false, "", checkpointAtUnix);
                }
                if (merged.Count > GlobalPlayerCheckpointSnapshot.MaxPlayers) return PositionJsonSafety.Fail("merged_checkpoint_count_limit", out error);
                if (!_source.IsCurrent(captured, maximumSampleAge, out error)) return false;
                var players = new List<GlobalPlayerPositionRecord>(merged.Values); players.Sort((a, b) => string.CompareOrdinal(a.PlayerId, b.PlayerId));
                batch = new GlobalPlayerCheckpointCaptureBatch(_owner, storage, captured, maximumSampleAge, players); return true;
            }
            catch (Exception e) { return PositionJsonSafety.Fail("prepare_checkpoint_capture:" + e.GetType().Name, out error); }
            finally { _busy = false; }
        }
        public CheckpointTransactionResult TryCommit(GlobalPlayerCheckpointCaptureBatch batch)
        {
            if (_busy || Thread.CurrentThread.ManagedThreadId != _threadId || batch == null || batch.Owner != _owner || batch.Consumed)
                return Rejected(batch, "capture_batch_foreign_consumed_reentrant_or_wrong_thread");
            batch.Consumed = true; _busy = true;
            bool repositoryEntered = false;
            try
            {
                if (!_source.IsCurrent(batch.Source, batch.MaximumAge, out var error)) return Rejected(batch, "capture_changed_before_commit:" + error);
                // B validates this again under the storage lease before staging AND just before native publication.
                repositoryEntered = true;
                return _repository.TryCommit(batch.Storage, batch.Players, false, () => _source.IsCurrent(batch.Source, batch.MaximumAge, out _));
            }
            catch (Exception e)
            {
                // Do not downgrade an unexpected escaped storage exception to a claim that nothing was published.
                return repositoryEntered ? new CheckpointTransactionResult(CheckpointTransactionStatus.Indeterminate, null, "capture_storage_exception:" + e.GetType().Name)
                    : Rejected(batch, "capture_commit:" + e.GetType().Name);
            }
            finally { _busy = false; }
        }
        private static CheckpointTransactionResult Rejected(GlobalPlayerCheckpointCaptureBatch batch, string error)
            => new CheckpointTransactionResult(CheckpointTransactionStatus.NotApplied, batch?.Storage, error);
    }
}
