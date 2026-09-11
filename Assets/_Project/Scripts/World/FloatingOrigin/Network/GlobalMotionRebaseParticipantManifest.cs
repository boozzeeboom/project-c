using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ProjectC.World.FloatingOrigin.Network
{
    /// <summary>
    /// Immutable, runtime-independent participant manifest contract.
    /// It describes reviewed identities only; it never discovers Unity objects.
    /// </summary>
    public readonly struct GlobalMotionRebaseManifestEntry
    {
        public string ParticipantId { get; }
        public GlobalMotionRebaseParticipantKind Kind { get; }
        public string SourceIdentity { get; }
        public int Ordinal { get; }

        public GlobalMotionRebaseManifestEntry(string participantId, GlobalMotionRebaseParticipantKind kind,
            string sourceIdentity, int ordinal)
        {
            ValidateToken(participantId, nameof(participantId), 256, true);
            ValidateToken(sourceIdentity, nameof(sourceIdentity), 1024, false);
            if (!Enum.IsDefined(typeof(GlobalMotionRebaseParticipantKind), kind))
                throw new ArgumentOutOfRangeException(nameof(kind));
            if (ordinal < 0)
                throw new ArgumentOutOfRangeException(nameof(ordinal));

            ParticipantId = participantId;
            Kind = kind;
            SourceIdentity = sourceIdentity;
            Ordinal = ordinal;
        }

        private static void ValidateToken(string value, string parameterName, int maxLength, bool asciiOnly)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Trim() != value || value.Length > maxLength)
                throw new ArgumentException("Stable non-empty token required.", parameterName);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (char.IsControl(c) || (asciiOnly && (c > 0x7f || !(char.IsLetterOrDigit(c) || c == '_' || c == '/' || c == '-' || c == '.'))))
                    throw new ArgumentException("Token contains an unsupported character.", parameterName);
            }
        }
    }

    public sealed class GlobalMotionRebaseParticipantManifest
    {
        public const int Version = 1;
        public const int MaximumEntries = 4096;
        public const int RequiredShipRoots = 22;
        public const int RequiredShipDeckNav = 20;
        private const string CanonicalHeader = "projectc.global-motion-rebase-participants";

        private readonly ReadOnlyCollection<GlobalMotionRebaseManifestEntry> _entries;
        private readonly byte[] _canonicalBytes;

        public IReadOnlyList<GlobalMotionRebaseManifestEntry> Entries => _entries;
        public string Digest { get; }
        public int Count => _entries.Count;

        private GlobalMotionRebaseParticipantManifest(List<GlobalMotionRebaseManifestEntry> entries,
            byte[] canonicalBytes, string digest)
        {
            _entries = new ReadOnlyCollection<GlobalMotionRebaseManifestEntry>(entries);
            _canonicalBytes = (byte[])canonicalBytes.Clone();
            Digest = digest;
        }

        public static bool TryCreate(IEnumerable<GlobalMotionRebaseManifestEntry> source,
            out GlobalMotionRebaseParticipantManifest manifest, out string error)
        {
            manifest = null;
            error = null;
            if (source == null) return Fail("manifest_entries_required", out error);

            var entries = new List<GlobalMotionRebaseManifestEntry>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var sourceKeys = new HashSet<string>(StringComparer.Ordinal);
            try
            {
                foreach (var entry in source)
                {
                    if (entries.Count >= MaximumEntries) return Fail("manifest_entry_limit_exceeded", out error);
                    if (!ids.Add(entry.ParticipantId)) return Fail("duplicate_participant_id:" + entry.ParticipantId, out error);
                    string sourceKey = ((byte)entry.Kind).ToString(CultureInfo.InvariantCulture) + "|" + entry.SourceIdentity;
                    if (!sourceKeys.Add(sourceKey)) return Fail("duplicate_source_identity:" + entry.SourceIdentity, out error);
                    entries.Add(entry);
                }
            }
            catch (Exception exception) when (exception is ArgumentException || exception is ArgumentOutOfRangeException)
            {
                return Fail("manifest_entry_invalid:" + exception.GetType().Name, out error);
            }

            if (entries.Count == 0) return Fail("manifest_empty", out error);
            entries.Sort(CompareEntries);
            byte[] canonical = BuildCanonicalBytes(entries);
            manifest = new GlobalMotionRebaseParticipantManifest(entries, canonical, DigestHex(canonical));
            return true;
        }

        public byte[] CopyCanonicalBytes() => (byte[])_canonicalBytes.Clone();

        public bool HasDigest(string expected)
        {
            return !string.IsNullOrEmpty(expected) && string.Equals(Digest, expected, StringComparison.Ordinal);
        }

        public bool TryGet(string participantId, out GlobalMotionRebaseManifestEntry entry)
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                if (string.Equals(_entries[i].ParticipantId, participantId, StringComparison.Ordinal))
                {
                    entry = _entries[i];
                    return true;
                }
            }
            entry = default;
            return false;
        }

        /// <summary>
        /// Validates only the fixed contract coverage. Dynamic network gameplay roots remain
        /// explicit manifest entries but their count is intentionally not guessed here.
        /// </summary>
        public bool TryValidateRequiredCoverage(out string error)
        {
            error = null;
            if (!HasKind("CITY_STATIC", GlobalMotionRebaseParticipantKind.CityStatic, out error) ||
                !HasKind("WORLD_ANCHORS", GlobalMotionRebaseParticipantKind.WorldAnchor, out error) ||
                !HasKind("PLAYER_FRAME", GlobalMotionRebaseParticipantKind.PlayerFrame, out error) ||
                !HasKind("CAMERA", GlobalMotionRebaseParticipantKind.Camera, out error))
                return false;

            for (int i = 1; i <= RequiredShipRoots; i++)
            {
                string id = "SHIP_ROOT/" + i.ToString("00", CultureInfo.InvariantCulture);
                if (!HasKind(id, GlobalMotionRebaseParticipantKind.ShipRoot, out error)) return false;
            }
            for (int i = 1; i <= RequiredShipDeckNav; i++)
            {
                string id = "SHIP_DECK_NAV/" + i.ToString("00", CultureInfo.InvariantCulture);
                if (!HasKind(id, GlobalMotionRebaseParticipantKind.ShipDeckNav, out error)) return false;
            }
            return true;
        }

        private bool HasKind(string id, GlobalMotionRebaseParticipantKind expected, out string error)
        {
            error = null;
            if (!TryGet(id, out var entry))
            {
                error = "required_participant_missing:" + id;
                return false;
            }
            if (entry.Kind != expected)
            {
                error = "required_participant_kind_mismatch:" + id;
                return false;
            }
            return true;
        }

        private static int CompareEntries(GlobalMotionRebaseManifestEntry a, GlobalMotionRebaseManifestEntry b)
        {
            int result = string.CompareOrdinal(a.ParticipantId, b.ParticipantId);
            if (result != 0) return result;
            result = ((byte)a.Kind).CompareTo((byte)b.Kind);
            if (result != 0) return result;
            result = string.CompareOrdinal(a.SourceIdentity, b.SourceIdentity);
            return result != 0 ? result : a.Ordinal.CompareTo(b.Ordinal);
        }

        private static byte[] BuildCanonicalBytes(List<GlobalMotionRebaseManifestEntry> entries)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, new UTF8Encoding(false), true))
            {
                writer.Write(CanonicalHeader);
                writer.Write(Version);
                writer.Write(entries.Count);
                foreach (var entry in entries)
                {
                    writer.Write(entry.ParticipantId);
                    writer.Write((byte)entry.Kind);
                    writer.Write(entry.SourceIdentity);
                    writer.Write(entry.Ordinal);
                }
                writer.Flush();
                return stream.ToArray();
            }
        }

        private static string DigestHex(byte[] bytes)
        {
            using (var sha = SHA256.Create())
            {
                byte[] digest = sha.ComputeHash(bytes);
                var builder = new StringBuilder(64);
                foreach (byte value in digest) builder.Append(value.ToString("x2", CultureInfo.InvariantCulture));
                return builder.ToString();
            }
        }

        private static bool Fail(string reason, out string error)
        {
            error = reason;
            return false;
        }
    }
}
