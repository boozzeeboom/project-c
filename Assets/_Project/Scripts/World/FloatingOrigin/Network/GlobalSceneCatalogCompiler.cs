using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace ProjectC.World.FloatingOrigin.Network
{
    /// <summary>Closed declared authoring set, not proof of native world readiness or completeness of the chosen scope.</summary>
    public static class GlobalSceneCatalogCompiler
    {
        public const int MaxScenes = 256, MaxEntries = 50000, MaxBytes = 16 * 1024 * 1024;
        public static bool IsHex(string value, int length)
        {
            if (value == null || value.Length != length) return false;
            bool nonzero = false;
            foreach (char c in value) { if (!(c >= '0' && c <= '9') && !(c >= 'a' && c <= 'f')) return false; if (c != '0') nonzero = true; }
            return nonzero;
        }
        public static bool IsSourceId(string id, string guid)
        {
            if (id == null || !IsHex(guid, 32)) return false;
            var parts = id.Split(':');
            return parts.Length == 3 && parts[0] == guid && ulong.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var objectId) && objectId != 0 &&
                ulong.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var prefabId) &&
                parts[1] == objectId.ToString(CultureInfo.InvariantCulture) && parts[2] == prefabId.ToString(CultureInfo.InvariantCulture);
        }
        public static bool TryCompile(GlobalSceneCatalogData data, out GlobalScenePlan plan, out string error)
        {
            plan = null; error = null;
            try
            {
                if (data == null || data.schemaVersion != 1 || data.expectedSceneGuids == null || data.expectedSceneGuids.Length == 0 || data.expectedSceneGuids.Length > MaxScenes ||
                    data.scenes == null || data.entries == null || data.scenes.Length != data.expectedSceneGuids.Length || data.entries.Length > MaxEntries)
                    return Fail("invalid_catalog_header", out error);
                var expected = new SortedSet<string>(StringComparer.Ordinal);
                foreach (var guid in data.expectedSceneGuids) if (!IsHex(guid, 32) || !expected.Add(guid)) return Fail("invalid_or_duplicate_scene_guid", out error);
                var scenes = new SortedDictionary<string, GlobalSceneSource>(StringComparer.Ordinal);
                var observations = new SortedDictionary<string, GlobalSceneObservation>(StringComparer.Ordinal);
                var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var source in data.scenes)
                {
                    if (source == null || !expected.Contains(source.sceneGuid) || scenes.ContainsKey(source.sceneGuid) || !source.inspectedComplete || !source.saved ||
                        !ValidPath(source.assetPath) || !paths.Add(source.assetPath) || !IsHex(source.dependencyHash, 32) || source.observations == null)
                        return Fail("missing_uninspected_dirty_or_invalid_scene_source", out error);
                    scenes.Add(source.sceneGuid, source);
                    foreach (var observation in source.observations)
                    {
                        if (observation == null || !(observation.isRoot || observation.isNetworkObject) || !IsSourceId(observation.sourceId, source.sceneGuid) ||
                            observation.parentSourceId == null || observation.isRoot != (observation.parentSourceId.Length == 0) || !IsHex(observation.layoutHash, 64) ||
                            observations.ContainsKey(observation.sourceId) || observations.Count >= MaxEntries)
                            return Fail("invalid_or_duplicate_observation", out error);
                        observations.Add(observation.sourceId, observation);
                    }
                }
                if (observations.Count != data.entries.Length) return Fail("review_does_not_cover_observations", out error);
                var entries = new SortedDictionary<string, GlobalSceneEntry>(StringComparer.Ordinal);
                foreach (var entry in data.entries)
                {
                    if (entry == null || !IsSourceId(entry.sourceId, entry.sceneGuid) || !expected.Contains(entry.sceneGuid) || entries.ContainsKey(entry.sourceId) ||
                        !observations.TryGetValue(entry.sourceId, out var observation) || string.IsNullOrWhiteSpace(entry.reviewNote) || entry.reviewNote.Length > 2048 ||
                        entry.parentSourceId != observation.parentSourceId || entry.parentSourceId == entry.sourceId ||
                        entry.treatment < GlobalSceneTreatment.PreserveContent || entry.treatment > GlobalSceneTreatment.Exclude)
                        return Fail("unreviewed_duplicate_unknown_or_reparented_entry", out error);
                    if ((entry.treatment == GlobalSceneTreatment.SceneNetworkObject && !observation.isNetworkObject) ||
                        (entry.treatment == GlobalSceneTreatment.PreserveContent && observation.isNetworkObject) ||
                        ((entry.treatment == GlobalSceneTreatment.ReplaceWithNetworkPrefab) != (entry.replacementPrefabHash != 0)))
                        return Fail("treatment_conflicts_with_observed_network_identity", out error);
                    if (!ValidPose(entry)) return Fail("invalid_or_ambiguous_scene_pose", out error);
                    entries.Add(entry.sourceId, entry);
                }
                var children = new Dictionary<string, List<string>>(StringComparer.Ordinal);
                var degree = new Dictionary<string, int>(StringComparer.Ordinal);
                var available = new SortedSet<string>(StringComparer.Ordinal);
                foreach (var entry in entries.Values) { degree.Add(entry.sourceId, 0); children.Add(entry.sourceId, new List<string>()); }
                foreach (var entry in entries.Values)
                {
                    if (entry.parentSourceId.Length == 0) { if (entry.poseKind == GlobalScenePoseKind.ParentLocal) return Fail("parent_local_without_parent", out error); continue; }
                    if (!entries.TryGetValue(entry.parentSourceId, out var parent) || parent.sceneGuid != entry.sceneGuid)
                        return Fail("missing_or_cross_scene_parent", out error);
                    if ((parent.treatment == GlobalSceneTreatment.Exclude || parent.treatment == GlobalSceneTreatment.ReplaceWithNetworkPrefab) && entry.treatment != GlobalSceneTreatment.Exclude)
                        return Fail("retired_source_ancestor_requires_explicit_descendant_exclusion", out error);
                    if (entry.treatment != GlobalSceneTreatment.Exclude && entry.spatial && !parent.spatial) return Fail("spatial_child_under_unframed_parent", out error);
                    if (entry.poseKind == GlobalScenePoseKind.ParentLocal && (!parent.spatial || !Network(parent) || !Network(entry))) return Fail("parent_local_requires_spatial_network_pair", out error);
                    if (entry.poseKind == GlobalScenePoseKind.World && Network(parent)) return Fail("network_child_requires_parent_local_pose", out error);
                    children[parent.sourceId].Add(entry.sourceId); degree[entry.sourceId]++;
                }
                foreach (var pair in degree) if (pair.Value == 0) available.Add(pair.Key);
                var ordered = new List<GlobalSceneEntry>();
                while (available.Count > 0)
                {
                    string id = available.Min; available.Remove(id); ordered.Add(entries[id]);
                    foreach (var child in children[id]) if (--degree[child] == 0) available.Add(child);
                }
                if (ordered.Count != entries.Count) return Fail("authored_parent_cycle", out error);
                byte[] digest;
                using (var stream = new MemoryStream())
                using (var writer = new BinaryWriter(stream, new UTF8Encoding(false, true), true))
                {
                    writer.Write("PCFO_SCENE_CATALOG"); writer.Write(data.schemaVersion); writer.Write(expected.Count);
                    foreach (var source in scenes.Values)
                    {
                        writer.Write(source.sceneGuid); writer.Write(source.assetPath); writer.Write(source.dependencyHash); writer.Write(source.inspectedComplete); writer.Write(source.saved);
                        var list = new List<GlobalSceneObservation>(source.observations); list.Sort((a, b) => string.CompareOrdinal(a.sourceId, b.sourceId));
                        writer.Write(list.Count);
                        foreach (var observation in list) { writer.Write(observation.sourceId); writer.Write(observation.parentSourceId); writer.Write(observation.isRoot); writer.Write(observation.isNetworkObject); writer.Write(observation.layoutHash); CheckSize(stream); }
                    }
                    writer.Write(entries.Count);
                    foreach (var entry in entries.Values)
                    {
                        writer.Write(entry.sceneGuid); writer.Write(entry.sourceId); writer.Write(entry.reviewNote); writer.Write((byte)entry.treatment); writer.Write(entry.spatial);
                        writer.Write(entry.replacementPrefabHash); writer.Write((byte)entry.poseKind); writer.Write(entry.parentSourceId);
                        writer.Write(entry.worldPosition.X); writer.Write(entry.worldPosition.Y); writer.Write(entry.worldPosition.Z);
                        Vector(writer, entry.parentLocalPosition); writer.Write(entry.rotation.x); writer.Write(entry.rotation.y); writer.Write(entry.rotation.z); writer.Write(entry.rotation.w); Vector(writer, entry.scale); CheckSize(stream);
                    }
                    writer.Flush(); using (var sha = SHA256.Create()) digest = sha.ComputeHash(stream.ToArray());
                }
                plan = new GlobalScenePlan(ordered, expected, digest); return true;
            }
            catch (Exception e) { error = "catalog_compile:" + e.GetType().Name; return false; }
        }
        public static bool TryMatchDigest(GlobalScenePlan plan, string declared, out byte[] digest)
        {
            digest = null;
            if (plan == null || !GlobalMotionNetworkContract.TryParseDigest(declared, out var expected)) return false;
            var actual = plan.CopyDigest(); int diff = 0; for (int i = 0; i < actual.Length; i++) diff |= actual[i] ^ expected[i];
            if (diff != 0) return false; digest = actual; return true;
        }
        private static bool Network(GlobalSceneEntry e) => e.treatment == GlobalSceneTreatment.SceneNetworkObject || e.treatment == GlobalSceneTreatment.ReplaceWithNetworkPrefab;
        private static bool ValidPose(GlobalSceneEntry e)
        {
            if (!e.worldPosition.IsFinite || !GlobalMotionSnapshot.Finite(e.parentLocalPosition) || !GlobalMotionSnapshot.UnitRotation(e.rotation) ||
                !GlobalMotionSnapshot.Finite(e.scale) || e.scale.x <= 0 || e.scale.y <= 0 || e.scale.z <= 0) return false;
            if (e.treatment == GlobalSceneTreatment.Exclude || !e.spatial)
                return e.poseKind == GlobalScenePoseKind.None && e.worldPosition == GlobalPosition.Zero && e.parentLocalPosition.Equals(Vector3.zero) && e.rotation.Equals(Quaternion.identity) && e.scale.Equals(Vector3.one);
            if (e.poseKind == GlobalScenePoseKind.World) return e.parentLocalPosition.Equals(Vector3.zero);
            return e.poseKind == GlobalScenePoseKind.ParentLocal && e.worldPosition == GlobalPosition.Zero && e.parentSourceId.Length != 0;
        }
        private static bool ValidPath(string p) => p != null && p.StartsWith("Assets/", StringComparison.Ordinal) && p.EndsWith(".unity", StringComparison.Ordinal) && p.Length <= 1024 &&
            p.IndexOf('\\') < 0 && p.IndexOf(':') < 0 && p.IndexOf("..", StringComparison.Ordinal) < 0 && p.IndexOf("//", StringComparison.Ordinal) < 0;
        private static void Vector(BinaryWriter writer, Vector3 p) { writer.Write(p.x); writer.Write(p.y); writer.Write(p.z); }
        private static void CheckSize(MemoryStream stream) { if (stream.Length > MaxBytes) throw new InvalidOperationException("Catalog exceeds byte budget."); }
        private static bool Fail(string message, out string error) { error = message; return false; }
    }
}
