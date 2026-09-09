using System;
using System.IO;
using System.Threading;

namespace ProjectC.World.FloatingOrigin.Persistence
{
    public enum CheckpointSlot { Primary, Backup, Pending }
    /// <summary>
    /// Synchronous exclusive-lease storage. Reads return owned byte copies or null ONLY for a missing file.
    /// Publication must replace primary with pending and preserve displaced primary in backup/quarantine.
    /// No copy-overwrite or delete-before-move fallback is permitted. Exceptions can mean uncertain publication.
    /// </summary>
    public interface IGlobalPlayerCheckpointStorage
    {
        IDisposable AcquireLease();
        byte[] Read(CheckpointSlot slot, int maximumBytes);
        void WritePendingNew(byte[] bytes);
        void PublishPending(bool replacePrimary, string quarantineId);
        void QuarantinePending(string quarantineId);
        byte[] ReadQuarantine(string quarantineId, int maximumBytes);
    }

    /// <summary>
    /// Actual disk adapter, dormant until explicitly constructed/called. Uses a dedicated explicit local directory,
    /// fixed non-legacy filenames, cooperative cross-process FileShare.None lock and same-directory Replace/Move.
    /// Requires a filesystem supporting File.Replace; Flush(true) is not a portable directory-fsync guarantee.
    /// Native OS, IL2CPP and sudden-power-loss behavior need separate acceptance; no automatic user-save path.
    /// </summary>
    public sealed class DirectoryGlobalPlayerCheckpointStorage : IGlobalPlayerCheckpointStorage
    {
        private const string Prefix = "global-player-checkpoints.v1";
        private readonly string _directory;
        private readonly object _gate = new object();
        private FileStream _lease;
        private int _ownerThread;
        public DirectoryGlobalPlayerCheckpointStorage(string absoluteDirectory)
        {
            if (string.IsNullOrWhiteSpace(absoluteDirectory) || !Path.IsPathRooted(absoluteDirectory) || absoluteDirectory.StartsWith("\\\\", StringComparison.Ordinal) || absoluteDirectory.StartsWith("//", StringComparison.Ordinal))
                throw new ArgumentException("Explicit canonical local absolute directory required.", nameof(absoluteDirectory));
            string full = Path.GetFullPath(absoluteDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string supplied = absoluteDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var comparison = Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (!string.Equals(full, supplied, comparison) || string.IsNullOrEmpty(full) || full == Path.GetPathRoot(full)?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
                throw new ArgumentException("Non-root canonical directory required.", nameof(absoluteDirectory));
            _directory = full;
        }
        public IDisposable AcquireLease()
        {
            lock (_gate)
            {
                if (_lease != null) throw new IOException("Checkpoint storage lease already held.");
                CheckAncestors(); Directory.CreateDirectory(_directory); CheckAncestors();
                string path = Path.Combine(_directory, Prefix + ".lock"); CheckLeaf(path);
                var stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                _lease = stream; _ownerThread = Thread.CurrentThread.ManagedThreadId;
                return new Lease(this, stream);
            }
        }
        public byte[] Read(CheckpointSlot slot, int maximumBytes) { RequireLease(); return ReadPath(SlotPath(slot), maximumBytes); }
        public byte[] ReadQuarantine(string quarantineId, int maximumBytes) { RequireLease(); return ReadPath(QuarantinePath(quarantineId), maximumBytes); }
        public void WritePendingNew(byte[] bytes)
        {
            RequireLease(); if (bytes == null || bytes.Length == 0 || bytes.Length > GlobalPlayerCheckpointSnapshotCodec.MaxBytes) throw new ArgumentException("Invalid staged bytes.");
            string path = SlotPath(CheckpointSlot.Pending); CheckLeaf(path);
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
        }
        public void PublishPending(bool replacePrimary, string quarantineId)
        {
            RequireLease(); string source = SlotPath(CheckpointSlot.Pending), primary = SlotPath(CheckpointSlot.Primary);
            CheckLeaf(source); CheckLeaf(primary);
            if (replacePrimary)
            {
                string displaced = quarantineId == null ? SlotPath(CheckpointSlot.Backup) : QuarantinePath(quarantineId);
                CheckLeaf(displaced);
                if (quarantineId != null && ExistsStrict(displaced)) throw new IOException("Quarantine must never be overwritten.");
                File.Replace(source, primary, displaced, false);
            }
            else File.Move(source, primary);
        }
        public void QuarantinePending(string quarantineId)
        {
            RequireLease(); string source = SlotPath(CheckpointSlot.Pending), destination = QuarantinePath(quarantineId);
            CheckLeaf(source); CheckLeaf(destination);
            File.Move(source, destination); // No overwrite; retain incomplete/unknown pending data for explicit diagnosis.
        }
        private byte[] ReadPath(string path, int maximumBytes)
        {
            if (maximumBytes < 0 || maximumBytes > GlobalPlayerCheckpointSnapshotCodec.MaxBytes) throw new ArgumentOutOfRangeException(nameof(maximumBytes));
            CheckLeaf(path);
            try
            {
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    if (stream.Length > maximumBytes) throw new InvalidDataException("Checkpoint file exceeds size limit.");
                    var bytes = new byte[(int)stream.Length]; int offset = 0;
                    while (offset < bytes.Length) { int read = stream.Read(bytes, offset, bytes.Length - offset); if (read == 0) throw new EndOfStreamException(); offset += read; }
                    if (stream.ReadByte() != -1) throw new IOException("File length changed during read.");
                    return bytes;
                }
            }
            catch (FileNotFoundException) { return null; } // Permission/IO/DirectoryNotFound errors are NOT an empty store.
        }
        private string SlotPath(CheckpointSlot slot)
        {
            switch (slot)
            {
                case CheckpointSlot.Primary: return Path.Combine(_directory, Prefix + ".primary.json");
                case CheckpointSlot.Backup: return Path.Combine(_directory, Prefix + ".backup.json");
                case CheckpointSlot.Pending: return Path.Combine(_directory, Prefix + ".pending.json");
                default: throw new ArgumentOutOfRangeException(nameof(slot));
            }
        }
        private string QuarantinePath(string id)
        { if (!GlobalPlayerCheckpointSnapshot.IsCommitId(id)) throw new ArgumentException("Explicit quarantine transaction ID required."); return Path.Combine(_directory, Prefix + ".quarantine-" + id + ".json"); }
        private void RequireLease()
        { if (_lease == null || _ownerThread != Thread.CurrentThread.ManagedThreadId) throw new InvalidOperationException("Current thread must own the storage lease."); CheckAncestors(); }
        private void CheckAncestors()
        {
            for (var directory = new DirectoryInfo(_directory); directory != null; directory = directory.Parent)
                if (directory.Exists && (directory.Attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("Reparse-point store directory is not supported.");
        }
        private static void CheckLeaf(string path)
        {
            try { var attributes = File.GetAttributes(path); if ((attributes & (FileAttributes.ReparsePoint | FileAttributes.Directory)) != 0) throw new IOException("Unexpected directory/reparse checkpoint path."); }
            catch (FileNotFoundException) { }
        }
        private static bool ExistsStrict(string path)
        { try { File.GetAttributes(path); return true; } catch (FileNotFoundException) { return false; } }
        private sealed class Lease : IDisposable
        {
            private DirectoryGlobalPlayerCheckpointStorage _owner;
            private readonly FileStream _stream;
            public Lease(DirectoryGlobalPlayerCheckpointStorage owner, FileStream stream) { _owner = owner; _stream = stream; }
            public void Dispose()
            {
                var owner = _owner; if (owner == null) return;
                lock (owner._gate)
                {
                    if (owner._lease != _stream || owner._ownerThread != Thread.CurrentThread.ManagedThreadId) throw new InvalidOperationException("Foreign lease release.");
                    try { _stream.Dispose(); } finally { owner._lease = null; owner._ownerThread = 0; _owner = null; }
                }
            }
        }
    }
}
