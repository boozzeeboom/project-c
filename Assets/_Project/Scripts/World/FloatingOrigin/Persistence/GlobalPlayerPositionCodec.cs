using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace ProjectC.World.FloatingOrigin.Persistence
{
    /// <summary>
    /// Fileless v1 checkpoint codec. Compact canonical JSON is part of the format (not arbitrary JSON import).
    /// Coordinates are invariant round-trip DOUBLE strings, never serialized through float. SHA256 detects damage,
    /// not malicious authors: authorization/account mapping belong to the server provider. No fallback to legacy.
    /// </summary>
    public static class GlobalPlayerPositionCodec
    {
        public const int SchemaVersion = 1;
        public const int MaxJsonCharacters = 8192;
        public const string Format = "projectc.global-player-position";
        public const string CoordinateSpace = "global-double";

        [Serializable]
        private sealed class Document
        {
            public string format;
            public int schemaVersion;
            public string coordinateSpace;
            public string playerId;
            public string x, y, z;
            public bool inShip;
            public string shipPersistentId;
            public long savedAtUnix;
            public string checksum;
        }
        public static bool TryEncode(GlobalPlayerPositionRecord record, out string json, out string error)
        {
            json = null; error = null;
            if (record == null) return PositionJsonSafety.Fail("checkpoint_missing", out error);
            try
            {
                var document = new Document { format = Format, schemaVersion = SchemaVersion, coordinateSpace = CoordinateSpace,
                    playerId = record.PlayerId, x = Coordinate(record.Position.X), y = Coordinate(record.Position.Y), z = Coordinate(record.Position.Z),
                    inShip = record.InShip, shipPersistentId = record.ShipPersistentId, savedAtUnix = record.SavedAtUnix, checksum = Checksum(record) };
                string encoded = JsonUtility.ToJson(document, false);
                if (encoded.Length > MaxJsonCharacters) return PositionJsonSafety.Fail("checkpoint_too_large", out error);
                json = encoded; return true;
            }
            catch (Exception e) { return PositionJsonSafety.Fail("checkpoint_encode:" + e.GetType().Name, out error); }
        }
        public static bool TryDecode(string json, out GlobalPlayerPositionRecord record, out string error)
        {
            record = null; error = null;
            if (!PositionJsonSafety.CheckObject(json, MaxJsonCharacters, 2, 1, out error)) return false;
            try
            {
                var document = JsonUtility.FromJson<Document>(json);
                if (document == null || document.format != Format || document.schemaVersion != SchemaVersion || document.coordinateSpace != CoordinateSpace)
                    return PositionJsonSafety.Fail("unsupported_checkpoint_format_version_or_space", out error);
                // JsonUtility alone ignores unknown fields and supplies defaults for missing ones. Require exact known shape.
                if (JsonUtility.ToJson(document, false) != PositionJsonSafety.TrimOuterWhitespace(json))
                    return PositionJsonSafety.Fail("noncanonical_unknown_duplicate_or_missing_fields", out error);
                if (!TryCoordinate(document.x, out double x) || !TryCoordinate(document.y, out double y) || !TryCoordinate(document.z, out double z))
                    return PositionJsonSafety.Fail("invalid_global_double_coordinate", out error);
                var candidate = new GlobalPlayerPositionRecord(document.playerId, new GlobalPosition(x, y, z), document.inShip, document.shipPersistentId, document.savedAtUnix);
                if (!PositionJsonSafety.IsDigest(document.checksum) || !string.Equals(Checksum(candidate), document.checksum, StringComparison.Ordinal))
                    return PositionJsonSafety.Fail("checkpoint_checksum_mismatch", out error);
                record = candidate; return true;
            }
            catch (Exception e) { return PositionJsonSafety.Fail("checkpoint_decode:" + e.GetType().Name, out error); }
        }
        private static string Coordinate(double value) => (value == 0d ? 0d : value).ToString("R", CultureInfo.InvariantCulture);
        private static bool TryCoordinate(string text, out double value)
        {
            return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && !double.IsNaN(value) && !double.IsInfinity(value) && Coordinate(value) == text;
        }
        private static string Checksum(GlobalPlayerPositionRecord record)
        {
            // Length-prefixed strings + fixed-width scalar data; little-endian BinaryWriter. No locale/JSON hash ambiguity.
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream, new UTF8Encoding(false, true), true))
                {
                    writer.Write(Format); writer.Write(SchemaVersion); writer.Write(CoordinateSpace); writer.Write(record.PlayerId);
                    writer.Write(record.Position.X); writer.Write(record.Position.Y); writer.Write(record.Position.Z);
                    writer.Write(record.InShip); writer.Write(record.ShipPersistentId); writer.Write(record.SavedAtUnix);
                }
                return PositionJsonSafety.Digest(stream.ToArray());
            }
        }
    }

    /// <summary>Bound inputs before invoking the native JSON parser; semantic/shape checks remain the caller's job.</summary>
    internal static class PositionJsonSafety
    {
        public static string TrimOuterWhitespace(string value) => value.Trim(' ', '\t', '\r', '\n');
        public static bool CheckObject(string json, int maxCharacters, int maxDepth, int maxContainers, out string error)
        {
            error = null;
            if (string.IsNullOrEmpty(json) || json.Length > maxCharacters) return Fail("json_missing_or_size_limit", out error);
            var stack = new char[maxDepth]; int depth = 0, containers = 0; bool quoted = false, escaped = false, started = false, finished = false;
            foreach (char c in json)
            {
                if (quoted)
                {
                    if (c < 32) return Fail("json_unescaped_control_character", out error);
                    if (escaped) { escaped = false; continue; }
                    if (c == '\\') { escaped = true; continue; }
                    if (c == '"') quoted = false;
                    continue;
                }
                if (c == ' ' || c == '\t' || c == '\r' || c == '\n') continue;
                if (finished || (!started && c != '{')) return Fail("json_single_object_required", out error);
                if (c == '"') { quoted = true; continue; }
                if (c == '{' || c == '[')
                {
                    if (depth >= maxDepth || ++containers > maxContainers) return Fail("json_structure_limit", out error);
                    stack[depth++] = c; started = true;
                }
                else if (c == '}' || c == ']')
                {
                    if (depth == 0 || stack[--depth] != (c == '}' ? '{' : '[')) return Fail("json_unbalanced_structure", out error);
                    if (depth == 0) finished = true;
                }
                else if (c < 32) return Fail("json_control_character", out error);
            }
            if (!finished || depth != 0 || quoted || escaped) return Fail("json_incomplete_structure", out error);
            return true;
        }
        public static bool IsDigest(string text)
        {
            if (text == null || text.Length != 64) return false;
            foreach (char c in text) if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'))) return false;
            return true;
        }
        public static string Digest(byte[] bytes)
        {
            using (var sha = SHA256.Create())
            {
                byte[] digest = sha.ComputeHash(bytes); var result = new StringBuilder(64);
                foreach (byte b in digest) result.Append(b.ToString("x2", CultureInfo.InvariantCulture));
                return result.ToString();
            }
        }
        public static bool Fail(string reason, out string error) { error = reason; return false; }
    }
}
