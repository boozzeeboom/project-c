using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ProjectC.World.FloatingOrigin.Network
{
    public enum GlobalPrefabRole : byte { Unclassified, Spatial, NonSpatial }
    [Flags]
    public enum GlobalPrefabFeatures : uint
    {
        None = 0, RootNetworkObject = 1, Adapter = 2, Replicator = 4, CoordinatesRequired = 8,
        StockTransformSync = 16, StockParentSync = 32, EnabledNetworkTransform = 64,
        SpatialContent = 128, Player = 256, PlayerAttacker = 512, PlayerTarget = 1024,
        NestedNetworkObject = 2048, MissingScript = 4096, InactiveBehaviourObject = 8192,
        EnabledNetworkRigidbody = 16384
    }
    [Serializable]
    public sealed class GlobalPrefabLayout
    {
        public uint prefabHash;
        public GlobalPrefabRole role;
        public GlobalPrefabFeatures features;
        public string[] orderedBehaviours;
    }

    /// <summary>Compatibility metadata, NOT authentication or proof of native world readiness.</summary>
    public static class GlobalMotionNetworkContract
    {
        public const ushort ProtocolVersion = 0xF001;
        public const byte FormatVersion = 1;
        public const int HelloSize = 72;
        public const int MaxPrefabs = 2048;
        public const int MaxManifestBytes = 4 * 1024 * 1024;
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        private const GlobalPrefabFeatures RequiredSpatial = GlobalPrefabFeatures.Adapter | GlobalPrefabFeatures.Replicator | GlobalPrefabFeatures.CoordinatesRequired;

        public static string ValidateLayout(GlobalPrefabLayout entry)
        {
            if (entry == null || entry.prefabHash == 0) return "missing_prefab_hash";
            if (((uint)entry.features & ~32767u) != 0) return "unknown_layout_features";
            if ((entry.features & GlobalPrefabFeatures.RootNetworkObject) == 0) return "missing_root_network_object";
            if ((entry.features & GlobalPrefabFeatures.NestedNetworkObject) != 0) return "nested_network_object";
            if ((entry.features & GlobalPrefabFeatures.MissingScript) != 0) return "missing_script";
            if ((entry.features & GlobalPrefabFeatures.InactiveBehaviourObject) != 0) return "inactive_network_behaviour_object";
            if (entry.orderedBehaviours == null || entry.orderedBehaviours.Length == 0 || entry.orderedBehaviours.Length > 256) return "invalid_behaviour_count";
            foreach (var name in entry.orderedBehaviours)
            {
                if (string.IsNullOrEmpty(name) || name.Length > 1024) return "invalid_behaviour_identity";
                try { Utf8.GetByteCount(name); } catch (EncoderFallbackException) { return "invalid_behaviour_utf8"; }
            }
            if (entry.role == GlobalPrefabRole.Spatial)
            {
                if ((entry.features & RequiredSpatial) != RequiredSpatial) return "missing_global_motion_components_or_optin";
                if ((entry.features & GlobalPrefabFeatures.StockTransformSync) != 0) return "stock_spawn_transform_enabled";
                if ((entry.features & GlobalPrefabFeatures.StockParentSync) != 0) return "stock_parent_sync_enabled";
                if ((entry.features & (GlobalPrefabFeatures.EnabledNetworkTransform | GlobalPrefabFeatures.EnabledNetworkRigidbody)) != 0) return "competing_network_writer";
                if ((entry.features & GlobalPrefabFeatures.Player) != 0 &&
                    (entry.features & (GlobalPrefabFeatures.PlayerAttacker | GlobalPrefabFeatures.PlayerTarget)) !=
                    (GlobalPrefabFeatures.PlayerAttacker | GlobalPrefabFeatures.PlayerTarget)) return "player_combat_behaviours_not_baked";
                return null;
            }
            if (entry.role == GlobalPrefabRole.NonSpatial)
            {
                const GlobalPrefabFeatures forbidden = RequiredSpatial | GlobalPrefabFeatures.SpatialContent |
                    GlobalPrefabFeatures.EnabledNetworkTransform | GlobalPrefabFeatures.EnabledNetworkRigidbody | GlobalPrefabFeatures.Player |
                    GlobalPrefabFeatures.PlayerAttacker | GlobalPrefabFeatures.PlayerTarget;
                return (entry.features & forbidden) != 0 ? "nonspatial_classification_conflict" : null;
            }
            return "unclassified_prefab";
        }

        public static bool TryBuildDigest(IReadOnlyList<GlobalPrefabLayout> entries, uint playerPrefabHash, out byte[] digest, out string error)
        {
            digest = null; error = null;
            if (entries == null || entries.Count == 0 || entries.Count > MaxPrefabs) { error = "invalid_catalog_size"; return false; }
            var sorted = new List<GlobalPrefabLayout>(entries.Count);
            var hashes = new HashSet<uint>();
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i]; error = ValidateLayout(entry);
                if (error != null) return false;
                if (!hashes.Add(entry.prefabHash)) { error = "duplicate_prefab_hash"; return false; }
                sorted.Add(entry);
            }
            var player = sorted.Find(entry => entry.prefabHash == playerPrefabHash);
            if (player == null || player.role != GlobalPrefabRole.Spatial || (player.features & GlobalPrefabFeatures.Player) == 0)
            { error = "selected_player_not_in_spatial_catalog"; return false; }
            sorted.Sort((a, b) => a.prefabHash.CompareTo(b.prefabHash));
            using (var bytes = new MemoryStream())
            using (var writer = new BinaryWriter(bytes, Utf8, true))
            {
                writer.Write(FormatVersion); writer.Write(playerPrefabHash); writer.Write(sorted.Count);
                foreach (var entry in sorted)
                {
                    writer.Write(entry.prefabHash); writer.Write((byte)entry.role); writer.Write((uint)entry.features);
                    writer.Write(entry.orderedBehaviours.Length);
                    foreach (var name in entry.orderedBehaviours)
                    {
                        writer.Write(name);
                        if (bytes.Length > MaxManifestBytes) { error = "manifest_byte_budget_exceeded"; return false; }
                    }
                }
                writer.Flush();
                using (var sha = SHA256.Create()) digest = sha.ComputeHash(bytes.ToArray());
            }
            return true;
        }

        public static byte[] CreateHello(byte[] layoutDigest, byte[] sceneDigest)
        {
            if (!ValidDigest(layoutDigest) || !ValidDigest(sceneDigest)) throw new ArgumentException("Nonzero SHA256 digests are required.");
            var result = new byte[HelloSize];
            result[0] = (byte)'P'; result[1] = (byte)'C'; result[2] = (byte)'F'; result[3] = (byte)'O';
            result[4] = FormatVersion; result[5] = 1; // Global coordinate mode, never a legacy hello.
            result[6] = (byte)(ProtocolVersion & 255); result[7] = (byte)(ProtocolVersion >> 8);
            Buffer.BlockCopy(layoutDigest, 0, result, 8, 32); Buffer.BlockCopy(sceneDigest, 0, result, 40, 32);
            return result;
        }
        public static bool ValidateHello(byte[] payload, byte[] expected, out string error)
        {
            error = null;
            if (!HasValidHeader(expected)) { error = "invalid_local_contract"; return false; }
            if (!HasValidHeader(payload)) { error = "invalid_or_legacy_hello"; return false; }
            int layoutDifference = 0, sceneDifference = 0;
            for (int i = 8; i < 40; i++) layoutDifference |= payload[i] ^ expected[i];
            for (int i = 40; i < HelloSize; i++) sceneDifference |= payload[i] ^ expected[i];
            if (layoutDifference != 0) { error = "prefab_layout_mismatch"; return false; }
            if (sceneDifference != 0) { error = "scene_contract_mismatch"; return false; }
            return true;
        }
        private static bool HasValidHeader(byte[] data)
        {
            if (data == null || data.Length != HelloSize || data[0] != 'P' || data[1] != 'C' || data[2] != 'F' || data[3] != 'O' ||
                data[4] != FormatVersion || data[5] != 1 || (data[6] | (data[7] << 8)) != ProtocolVersion) return false;
            int a = 0, b = 0; for (int i = 0; i < 32; i++) { a |= data[8 + i]; b |= data[40 + i]; }
            return a != 0 && b != 0;
        }
        public static bool TryParseDigest(string hex, out byte[] digest)
        {
            digest = null;
            if (hex == null || hex.Length != 64) return false;
            var bytes = new byte[32];
            for (int i = 0; i < bytes.Length; i++)
            {
                int a = Hex(hex[i * 2]), b = Hex(hex[i * 2 + 1]);
                if (a < 0 || b < 0) return false;
                bytes[i] = (byte)((a << 4) | b);
            }
            if (!ValidDigest(bytes)) return false;
            digest = bytes; return true;
        }
        public static string DigestHex(byte[] digest)
        {
            if (!ValidDigest(digest)) throw new ArgumentException("Invalid digest.");
            return BitConverter.ToString(digest).Replace("-", "").ToLowerInvariant();
        }
        private static bool ValidDigest(byte[] digest)
        { if (digest == null || digest.Length != 32) return false; int value = 0; foreach (byte b in digest) value |= b; return value != 0; }
        private static int Hex(char c) => c >= '0' && c <= '9' ? c - '0' : c >= 'a' && c <= 'f' ? c - 'a' + 10 : c >= 'A' && c <= 'F' ? c - 'A' + 10 : -1;
    }
}
