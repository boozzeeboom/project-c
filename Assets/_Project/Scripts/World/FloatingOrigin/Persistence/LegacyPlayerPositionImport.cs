using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using UnityEngine;
using ProjectC.Core.ShipPosition;

namespace ProjectC.World.FloatingOrigin.Persistence
{
    public enum LegacyPlayerCoordinates { Unspecified = 0, AbsoluteWorldFloat = 1, KnownLocalFrame = 2 }

    /// <summary>Trusted review for ONE record in the exact supplied legacy text snapshot. Never inferred from clientId.</summary>
    public sealed class LegacyPlayerPositionBinding
    {
        public ulong LegacyClientId { get; }
        public long ExpectedSavedAtUnix { get; }
        public string PlayerId { get; }
        public LegacyPlayerCoordinates Coordinates { get; }
        public LocalCoordinateFrame? SourceFrame { get; }
        public LegacyPlayerPositionBinding(ulong legacyClientId, long expectedSavedAtUnix, string playerId, LegacyPlayerCoordinates coordinates, LocalCoordinateFrame? sourceFrame = null)
        { LegacyClientId = legacyClientId; ExpectedSavedAtUnix = expectedSavedAtUnix; PlayerId = playerId; Coordinates = coordinates; SourceFrame = sourceFrame; }
    }

    /// <summary>
    /// Read-only ALL-player draft; does not migrate ships or replace ShipPositions.json.
    /// OriginalJson preserves supplied text (not original file encoding/BOM/bytes; this is NOT a disk backup).
    /// </summary>
    public sealed class LegacyPlayerPositionDraft
    {
        public string OriginalJson { get; }
        public string SourceTextSha256 { get; }
        public int UnconvertedShipCount { get; }
        public IReadOnlyList<GlobalPlayerPositionRecord> Players { get; }
        internal LegacyPlayerPositionDraft(string json, string digest, int ships, List<GlobalPlayerPositionRecord> players)
        { OriginalJson = json; SourceTextSha256 = digest; UnconvertedShipCount = ships; Players = new ReadOnlyCollection<GlobalPlayerPositionRecord>(players.ToArray()); }
    }

    /// <summary>
    /// Explicit read-only import of the CURRENT compact JsonUtility ShipPositionListWrapper layout.
    /// Older/pretty/reordered/extended/partial formats fail closed, never become a zero/default checkpoint.
    /// Coordinate provenance and persistent identity must be reviewed externally against the exact text digest.
    /// No Transform reads, file IO, save writes, ship conversion or runtime network/frame selection.
    /// </summary>
    public static class LegacyPlayerPositionImport
    {
        public const int MaxJsonCharacters = 1024 * 1024;
        public const int MaxRecords = 4096;
        public static bool TryComputeSourceTextDigest(string json, out string digest, out string error)
        {
            digest = null; error = null;
            if (string.IsNullOrEmpty(json) || json.Length > MaxJsonCharacters) return PositionJsonSafety.Fail("legacy_text_missing_or_too_large", out error);
            try { digest = PositionJsonSafety.Digest(new UTF8Encoding(false, true).GetBytes(json)); return true; }
            catch (ArgumentException) { return PositionJsonSafety.Fail("legacy_text_invalid_unicode", out error); }
        }
        public static bool TryCreateDraft(string legacyJson, string expectedSourceTextSha256, IReadOnlyList<LegacyPlayerPositionBinding> bindings,
            out LegacyPlayerPositionDraft draft, out string error)
        {
            draft = null; error = null;
            if (!PositionJsonSafety.IsDigest(expectedSourceTextSha256) || bindings == null || bindings.Count > MaxRecords)
                return PositionJsonSafety.Fail("explicit_snapshot_digest_and_bindings_required", out error);
            if (!PositionJsonSafety.CheckObject(legacyJson, MaxJsonCharacters, 8, 2 * MaxRecords + 3, out error) ||
                !TryComputeSourceTextDigest(legacyJson, out string actualDigest, out error)) return false;
            if (!string.Equals(actualDigest, expectedSourceTextSha256, StringComparison.Ordinal)) return PositionJsonSafety.Fail("legacy_snapshot_changed_since_review", out error);
            try
            {
                var source = JsonUtility.FromJson<ShipPositionListWrapper>(legacyJson);
                if (source == null || source.ships == null || source.players == null || source.players.Count > MaxRecords || source.ships.Count > MaxRecords || source.players.Count != bindings.Count)
                    return PositionJsonSafety.Fail("legacy_wrapper_or_complete_binding_count_invalid", out error);
                // Retain the whole original text; reject any layout for which JsonUtility would silently omit/default data.
                if (JsonUtility.ToJson(source, false) != PositionJsonSafety.TrimOuterWhitespace(legacyJson))
                    return PositionJsonSafety.Fail("unsupported_legacy_layout_requires_separate_reader", out error);
                var map = new Dictionary<ulong, LegacyPlayerPositionBinding>(); var stableIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (var binding in bindings)
                {
                    if (binding == null || !GlobalPlayerPositionRecord.IsValidIdentity(binding.PlayerId) || map.ContainsKey(binding.LegacyClientId) || !stableIds.Add(binding.PlayerId))
                        return PositionJsonSafety.Fail("missing_duplicate_or_invalid_identity_binding", out error);
                    if (binding.Coordinates == LegacyPlayerCoordinates.AbsoluteWorldFloat)
                    {
                        if (binding.SourceFrame.HasValue) return PositionJsonSafety.Fail("absolute_legacy_point_must_not_have_frame", out error);
                    }
                    else if (binding.Coordinates == LegacyPlayerCoordinates.KnownLocalFrame)
                    {
                        if (!binding.SourceFrame.HasValue || !binding.SourceFrame.Value.IsValid) return PositionJsonSafety.Fail("known_valid_source_frame_required", out error);
                    }
                    else return PositionJsonSafety.Fail("legacy_coordinate_provenance_not_reviewed", out error);
                    map.Add(binding.LegacyClientId, binding);
                }
                var seen = new HashSet<ulong>(); var records = new List<GlobalPlayerPositionRecord>(source.players.Count);
                foreach (var player in source.players)
                {
                    if (player == null || !seen.Add(player.clientId) || !map.TryGetValue(player.clientId, out var binding) || player.savedAtUnix != binding.ExpectedSavedAtUnix)
                        return PositionJsonSafety.Fail("legacy_player_unmatched_duplicate_or_timestamp_changed", out error);
                    var stored = new Vector3(player.px, player.py, player.pz);
                    GlobalPosition position = binding.Coordinates == LegacyPlayerCoordinates.AbsoluteWorldFloat
                        ? GlobalPosition.FromLegacyAbsolute(stored) : binding.SourceFrame.Value.ToGlobal(stored);
                    // Already-lost float precision is not restored. Do not add scene-size offsets or guess zero origins.
                    records.Add(new GlobalPlayerPositionRecord(binding.PlayerId, position, player.inShip, player.shipPersistentId, player.savedAtUnix));
                }
                draft = new LegacyPlayerPositionDraft(legacyJson, actualDigest, source.ships.Count, records); return true;
            }
            catch (Exception e) { return PositionJsonSafety.Fail("legacy_import:" + e.GetType().Name, out error); }
        }
    }
}
