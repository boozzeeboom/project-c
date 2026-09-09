using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Unity.Netcode;
using ProjectC.World.FloatingOrigin.Network;

namespace ProjectC.EditorTools.FloatingOrigin
{
    /// <summary>Pure metadata/codec checks; no GameObjects, network sessions, scenes or native simulation.</summary>
    public static class ValidateGlobalMotionNetworkContracts
    {
        [Serializable] public sealed class Report { public int passed; public int failed; public string[] checks; public string[] failures; }
        [MenuItem("ProjectC/World/Floating Origin/Validate Network Startup Contracts")]
        public static void Execute()
        {
            var report = Run();
            if (report.failed != 0) throw new InvalidOperationException(JsonUtility.ToJson(report, true));
            Debug.Log("[T-FO04E] Pure startup contract checks passed: " + report.passed + ". Runtime remains untested.");
        }
        public static Report Run()
        {
            if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stable Edit Mode required.");
            var passed = new List<string>(); var failed = new List<string>();
            void Check(string name, Action action) { try { action(); passed.Add(name); } catch (Exception e) { failed.Add(name + ": " + e.Message); } }
            void RejectedFlag(string name, GlobalPrefabFeatures flag, bool remove = false)
            { Check(name, () => { var p = Player(); p.features = remove ? p.features & ~flag : p.features | flag; Require(GlobalMotionNetworkContract.ValidateLayout(p) != null); }); }

            Check("Complete spatial player layout accepted", () => Require(GlobalMotionNetworkContract.ValidateLayout(Player()) == null));
            Check("Explicit nonspatial service accepted", () => Require(GlobalMotionNetworkContract.ValidateLayout(Service()) == null));
            Check("Null and zero identity rejected", () => { Require(GlobalMotionNetworkContract.ValidateLayout(null) != null); var p = Player(); p.prefabHash = 0; Require(GlobalMotionNetworkContract.ValidateLayout(p) != null); });
            RejectedFlag("Missing root rejected", GlobalPrefabFeatures.RootNetworkObject, true);
            RejectedFlag("Missing adapter rejected", GlobalPrefabFeatures.Adapter, true);
            RejectedFlag("Missing replicator rejected", GlobalPrefabFeatures.Replicator, true);
            RejectedFlag("Disabled opt-in rejected", GlobalPrefabFeatures.CoordinatesRequired, true);
            RejectedFlag("Stock initial transform rejected", GlobalPrefabFeatures.StockTransformSync);
            RejectedFlag("Stock parenting rejected", GlobalPrefabFeatures.StockParentSync);
            RejectedFlag("Competing NetworkTransform rejected", GlobalPrefabFeatures.EnabledNetworkTransform);
            RejectedFlag("Competing NetworkRigidbody rejected", GlobalPrefabFeatures.EnabledNetworkRigidbody);
            RejectedFlag("Nested NetworkObject rejected", GlobalPrefabFeatures.NestedNetworkObject);
            RejectedFlag("Missing script rejected", GlobalPrefabFeatures.MissingScript);
            RejectedFlag("Inactive behaviour object rejected", GlobalPrefabFeatures.InactiveBehaviourObject);
            RejectedFlag("Unknown feature bit rejected", (GlobalPrefabFeatures)0x80000000u);
            RejectedFlag("Missing baked attacker rejected", GlobalPrefabFeatures.PlayerAttacker, true);
            RejectedFlag("Missing baked target rejected", GlobalPrefabFeatures.PlayerTarget, true);
            Check("Unknown classification rejected", () => { var p = Player(); p.role = GlobalPrefabRole.Unclassified; Require(GlobalMotionNetworkContract.ValidateLayout(p) != null); p.role = (GlobalPrefabRole)255; Require(GlobalMotionNetworkContract.ValidateLayout(p) != null); });
            Check("Nonspatial cannot hide geometry or actor metadata", () =>
            {
                foreach (var f in new[] { GlobalPrefabFeatures.SpatialContent, GlobalPrefabFeatures.Adapter, GlobalPrefabFeatures.Player, GlobalPrefabFeatures.PlayerAttacker, GlobalPrefabFeatures.PlayerTarget })
                { var s = Service(); s.features |= f; Require(GlobalMotionNetworkContract.ValidateLayout(s) != null); }
            });
            Check("Null empty and oversized behaviour arrays rejected", () =>
            {
                foreach (var names in new[] { null, Array.Empty<string>(), Enumerable.Repeat("A", 257).ToArray() })
                { var p = Player(); p.orderedBehaviours = names; Require(GlobalMotionNetworkContract.ValidateLayout(p) != null); }
            });
            Check("Invalid behaviour identity rejected", () =>
            {
                foreach (var name in new[] { null, "", new string('x', 1025), "\uD800" })
                { var p = Player(); p.orderedBehaviours = new[] { name }; Require(GlobalMotionNetworkContract.ValidateLayout(p) != null); }
            });
            Check("Catalog ordering does not change digest", () => Require(Equal(Digest(Player(), Service()), Digest(Service(), Player()))));
            Check("Behaviour ordering changes digest", () => { var p = Player(); Array.Reverse(p.orderedBehaviours); Require(!Equal(Digest(Player()), Digest(p))); });
            Check("Hierarchy slot changes digest", () => { var p = Player(); p.orderedBehaviours[0] = "1/2|Actor|1"; Require(!Equal(Digest(Player()), Digest(p))); });
            Check("Behaviour enabled bit changes digest", () => { var p = Player(); p.orderedBehaviours[0] = "r|Actor|0"; Require(!Equal(Digest(Player()), Digest(p))); });
            Check("Service spawn flags change digest", () => { var s = Service(); s.features |= GlobalPrefabFeatures.StockTransformSync; Require(!Equal(Digest(Player(), Service()), Digest(Player(), s))); });
            Check("Selected player is part of digest", () =>
            {
                var entries = new[] { Player(10), Player(11) };
                Require(GlobalMotionNetworkContract.TryBuildDigest(entries, 10, out var a, out _));
                Require(GlobalMotionNetworkContract.TryBuildDigest(entries, 11, out var b, out _)); Require(!Equal(a, b));
            });
            Check("Duplicate prefab identity rejected", () => Require(!GlobalMotionNetworkContract.TryBuildDigest(new[] { Player(), Player() }, 10, out _, out _)));
            Check("Unknown selected player rejected", () => Require(!GlobalMotionNetworkContract.TryBuildDigest(new[] { Player() }, 500, out _, out _)));
            Check("Nonplayer cannot be selected as player", () => Require(!GlobalMotionNetworkContract.TryBuildDigest(new[] { Player(), Service() }, 20, out _, out _)));
            Check("Missing catalog rejected", () => { Require(!GlobalMotionNetworkContract.TryBuildDigest(null, 10, out _, out _)); Require(!GlobalMotionNetworkContract.TryBuildDigest(Array.Empty<GlobalPrefabLayout>(), 10, out _, out _)); });
            Check("Catalog count bounded", () => Require(!GlobalMotionNetworkContract.TryBuildDigest(new GlobalPrefabLayout[GlobalMotionNetworkContract.MaxPrefabs + 1], 10, out _, out _)));
            Check("Canonical byte budget bounded", () =>
            {
                var entries = new List<GlobalPrefabLayout>();
                for (uint i = 10; i < 28; i++) { var p = Player(i); p.orderedBehaviours = Enumerable.Repeat(new string('a', 1024), 256).ToArray(); entries.Add(p); }
                Require(!GlobalMotionNetworkContract.TryBuildDigest(entries, 10, out _, out var error) && error == "manifest_byte_budget_exceeded");
            });
            Check("Hello has fixed explicit header", () => { var h = Hello(); Require(h.Length == 72 && h[0] == 'P' && h[3] == 'O' && h[4] == 1 && h[5] == 1 && (h[6] | h[7] << 8) == GlobalMotionNetworkContract.ProtocolVersion); });
            Check("Matching hello accepted", () => Require(GlobalMotionNetworkContract.ValidateHello(Hello(), Hello(), out _)));
            Check("Caller digests cannot mutate a created hello", () => { var d = Digest(Player()); var s = Scene(); var h = GlobalMotionNetworkContract.CreateHello(d, s); d[0] ^= 255; s[0] ^= 255; Require(GlobalMotionNetworkContract.ValidateHello(h, Hello(), out _)); });
            Check("Every single-byte corruption rejected", () => { for (int i = 0; i < 72; i++) { var h = Hello(); h[i] ^= 1; Require(!GlobalMotionNetworkContract.ValidateHello(h, Hello(), out _)); } });
            Check("Every truncated length rejected", () => { for (int i = 0; i < 72; i++) Require(!GlobalMotionNetworkContract.ValidateHello(Hello().Take(i).ToArray(), Hello(), out _)); });
            Check("Trailing bytes rejected", () => Require(!GlobalMotionNetworkContract.ValidateHello(Hello().Concat(new byte[] { 1 }).ToArray(), Hello(), out _)));
            Check("Legacy empty and null hello rejected", () => { Require(!GlobalMotionNetworkContract.ValidateHello(null, Hello(), out _)); Require(!GlobalMotionNetworkContract.ValidateHello(Array.Empty<byte>(), Hello(), out _)); });
            Check("Invalid local expected contract rejected", () => { Require(!GlobalMotionNetworkContract.ValidateHello(Hello(), null, out _)); var h = Hello(); h[5] = 0; Require(!GlobalMotionNetworkContract.ValidateHello(Hello(), h, out _)); });
            Check("Distinct mismatch reasons retained", () => { var h = Hello(); h[8] ^= 1; Require(!GlobalMotionNetworkContract.ValidateHello(h, Hello(), out var e) && e == "prefab_layout_mismatch"); h = Hello(); h[40] ^= 1; Require(!GlobalMotionNetworkContract.ValidateHello(h, Hello(), out e) && e == "scene_contract_mismatch"); });
            Check("Zero digest fields rejected in inbound and expected hello", () => { var h = Hello(); Array.Clear(h, 8, 32); Require(!GlobalMotionNetworkContract.ValidateHello(h, Hello(), out _)); h = Hello(); Array.Clear(h, 40, 32); Require(!GlobalMotionNetworkContract.ValidateHello(Hello(), h, out _)); });
            Check("Invalid digest cannot produce hello", () => { foreach (var d in new[] { null, new byte[31], new byte[32], new byte[33] }) { bool threw = false; try { GlobalMotionNetworkContract.CreateHello(d, Scene()); } catch (ArgumentException) { threw = true; } Require(threw); } });
            Check("Hex roundtrip and uppercase accepted", () => { var d = Digest(Player()); var text = GlobalMotionNetworkContract.DigestHex(d); Require(GlobalMotionNetworkContract.TryParseDigest(text, out var read) && Equal(d, read)); Require(GlobalMotionNetworkContract.TryParseDigest(text.ToUpperInvariant(), out read) && Equal(d, read)); });
            Check("Malformed hex rejected", () => { foreach (var text in new[] { null, "", new string('0', 64), new string('g', 64), new string('1', 63), new string('1', 65), " " + new string('1', 63) }) Require(!GlobalMotionNetworkContract.TryParseDigest(text, out _)); });
            Check("NGO reserved protocol changes config hash without caching", () => { var a = new NetworkConfig(); var b = new NetworkConfig { ProtocolVersion = GlobalMotionNetworkContract.ProtocolVersion }; Require(a.GetConfig(false) != b.GetConfig(false)); });
            Check("Compiled bootstrap uses explicit readonly validation and peer lifecycle", () => { var t = typeof(IGlobalMotionSpawnBootstrap); Require(t.GetMethod("ValidateNetworkStart") != null && t.GetMethod("PeerConnected") != null && t.GetMethod("PeerDisconnected") != null); });
            return new Report { passed = passed.Count, failed = failed.Count, checks = passed.ToArray(), failures = failed.ToArray() };
        }
        private static GlobalPrefabLayout Player(uint hash = 10) => new GlobalPrefabLayout
        {
            prefabHash = hash, role = GlobalPrefabRole.Spatial,
            features = GlobalPrefabFeatures.RootNetworkObject | GlobalPrefabFeatures.Adapter | GlobalPrefabFeatures.Replicator | GlobalPrefabFeatures.CoordinatesRequired |
                GlobalPrefabFeatures.Player | GlobalPrefabFeatures.PlayerAttacker | GlobalPrefabFeatures.PlayerTarget,
            orderedBehaviours = new[] { "r|Actor|1", "r|Motion|1", "r|Combat|1" }
        };
        private static GlobalPrefabLayout Service() => new GlobalPrefabLayout { prefabHash = 20, role = GlobalPrefabRole.NonSpatial, features = GlobalPrefabFeatures.RootNetworkObject, orderedBehaviours = new[] { "r|Service|1" } };
        private static byte[] Digest(params GlobalPrefabLayout[] entries)
        { Require(GlobalMotionNetworkContract.TryBuildDigest(entries, 10, out var digest, out var error), error); return digest; }
        private static byte[] Scene() { var data = new byte[32]; data[0] = 42; return data; }
        private static byte[] Hello() => GlobalMotionNetworkContract.CreateHello(Digest(Player()), Scene());
        private static bool Equal(byte[] a, byte[] b) => a.SequenceEqual(b);
        private static void Require(bool value, string message = null) { if (!value) throw new InvalidOperationException(message ?? "Assertion failed."); }
    }
}
