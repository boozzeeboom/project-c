using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using UnityEngine;

namespace ProjectC.World.FloatingOrigin.Persistence
{
    /// <summary>Complete immutable player-only snapshot. Commit identity prevents stale-write ABA after explicit recovery.</summary>
    public sealed class GlobalPlayerCheckpointSnapshot
    {
        public const int MaxPlayers = 4096;
        public string StoreId { get; }
        public long Revision { get; }
        public string CommitId { get; }
        public string ParentCommitId { get; }
        public IReadOnlyList<GlobalPlayerPositionRecord> Players { get; }
        public GlobalPlayerCheckpointSnapshot(string storeId, long revision, string commitId, string parentCommitId, IReadOnlyList<GlobalPlayerPositionRecord> players)
        {
            if (!GlobalPlayerPositionRecord.IsValidIdentity(storeId) || revision < 1 || !IsCommitId(commitId) ||
                (revision == 1 ? parentCommitId != "" : !IsCommitId(parentCommitId)) || parentCommitId == commitId || players == null || players.Count > MaxPlayers)
                throw new ArgumentException("Invalid explicit snapshot scope, lineage or collection.");
            var copy = new List<GlobalPlayerPositionRecord>(players.Count); var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var record in players)
            { if (record == null || !ids.Add(record.PlayerId)) throw new ArgumentException("Null or duplicate player checkpoint."); copy.Add(record); }
            copy.Sort((a, b) => string.CompareOrdinal(a.PlayerId, b.PlayerId));
            StoreId = storeId; Revision = revision; CommitId = commitId; ParentCommitId = parentCommitId;
            Players = new ReadOnlyCollection<GlobalPlayerPositionRecord>(copy.ToArray());
        }
        public static bool IsCommitId(string value) => value != null && value.Length == 32 && Guid.TryParseExact(value, "N", out var id) && id != Guid.Empty && id.ToString("N") == value;
    }

    /// <summary>Canonical UTF8 snapshot envelope. Player records use the frozen T-FO05A v1 codec; no legacy auto-detection.</summary>
    public static class GlobalPlayerCheckpointSnapshotCodec
    {
        public const int Version = 1;
        public const int MaxBytes = 8 * 1024 * 1024;
        public const string Format = "projectc.global-player-snapshot";
        public const string Unsupported = "snapshot_format_or_version_unsupported";
        public const string ForeignStore = "snapshot_store_mismatch";
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        [Serializable]
        private sealed class Document
        {
            public string format;
            public int schemaVersion;
            public string storeId;
            public long revision;
            public string commitId;
            public string parentCommitId;
            public string[] records;
            public string checksum;
        }
        public static bool TryEncode(GlobalPlayerCheckpointSnapshot snapshot, out byte[] bytes, out string error)
        {
            bytes = null; error = null;
            if (snapshot == null) return PositionJsonSafety.Fail("snapshot_missing", out error);
            try
            {
                var records = new string[snapshot.Players.Count];
                for (int i = 0; i < records.Length; i++) if (!GlobalPlayerPositionCodec.TryEncode(snapshot.Players[i], out records[i], out error)) return false;
                var document = new Document { format = Format, schemaVersion = Version, storeId = snapshot.StoreId, revision = snapshot.Revision,
                    commitId = snapshot.CommitId, parentCommitId = snapshot.ParentCommitId, records = records };
                document.checksum = Checksum(document);
                string json = JsonUtility.ToJson(document, false);
                if (Utf8.GetByteCount(json) > MaxBytes) return PositionJsonSafety.Fail("snapshot_size_limit", out error);
                bytes = Utf8.GetBytes(json); return true;
            }
            catch (Exception e) { return PositionJsonSafety.Fail("snapshot_encode:" + e.GetType().Name, out error); }
        }
        public static bool TryDecode(byte[] bytes, string expectedStoreId, out GlobalPlayerCheckpointSnapshot snapshot, out string error)
        {
            snapshot = null; error = null;
            if (bytes == null || bytes.Length == 0 || bytes.Length > MaxBytes) return PositionJsonSafety.Fail("snapshot_size_or_missing", out error);
            try
            {
                string json = Utf8.GetString(bytes);
                if (!PositionJsonSafety.CheckObject(json, MaxBytes, 2, 2, out error)) return false;
                var document = JsonUtility.FromJson<Document>(json);
                if (document == null || document.format != Format || document.schemaVersion != Version) return PositionJsonSafety.Fail(Unsupported, out error);
                if (document.storeId != expectedStoreId) return PositionJsonSafety.Fail(ForeignStore, out error);
                if (document.records == null || document.records.Length > GlobalPlayerCheckpointSnapshot.MaxPlayers || !PositionJsonSafety.IsDigest(document.checksum))
                    return PositionJsonSafety.Fail("snapshot_records_or_checksum_invalid", out error);
                var records = new List<GlobalPlayerPositionRecord>(document.records.Length);
                foreach (string record in document.records)
                {
                    if (!GlobalPlayerPositionCodec.TryDecode(record, out var decoded, out error)) return false;
                    records.Add(decoded);
                }
                if (Checksum(document) != document.checksum) return PositionJsonSafety.Fail("snapshot_checksum_mismatch", out error);
                var candidate = new GlobalPlayerCheckpointSnapshot(document.storeId, document.revision, document.commitId, document.parentCommitId, records);
                if (!TryEncode(candidate, out var canonical, out error) || !CheckpointBytes.Equal(bytes, canonical))
                    return PositionJsonSafety.Fail("snapshot_noncanonical_missing_duplicate_or_unsorted_fields", out error);
                snapshot = candidate; return true;
            }
            catch (Exception e) { return PositionJsonSafety.Fail("snapshot_decode:" + e.GetType().Name, out error); }
        }
        private static string Checksum(Document document)
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream, Utf8, true))
                {
                    writer.Write(document.format); writer.Write(document.schemaVersion); writer.Write(document.storeId);
                    writer.Write(document.revision); writer.Write(document.commitId); writer.Write(document.parentCommitId);
                    writer.Write(document.records.Length); foreach (string record in document.records) writer.Write(record);
                }
                return PositionJsonSafety.Digest(stream.ToArray());
            }
        }
    }
    internal static class CheckpointBytes
    {
        public static bool Equal(byte[] a, byte[] b)
        { if (a == null || b == null) return a == b; if (a.Length != b.Length) return false; for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false; return true; }
        public static string Fingerprint(params byte[][] values)
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
                    foreach (var value in values) { writer.Write(value != null); if (value != null) { writer.Write(value.Length); writer.Write(PositionJsonSafety.Digest(value)); } }
                return PositionJsonSafety.Digest(stream.ToArray());
            }
        }
    }
}
