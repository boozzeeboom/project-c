using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using ProjectC.World.FloatingOrigin.Network;

namespace ProjectC.EditorTools.FloatingOrigin
{
    /// <summary>
    /// T-FO06N read-only manifest contract checks.
    /// No scene census, runtime discovery, asset writes, NGO objects or Play Mode.
    /// </summary>
    public static class ValidateGlobalMotionRebaseParticipantManifest
    {
        [Serializable]
        public sealed class Report
        {
            public int passed;
            public int failed;
            public string[] checks;
            public string[] failures;
        }

        [MenuItem("ProjectC/World/Floating Origin/Validate Rebase Participant Manifest")]
        public static void Execute()
        {
            var report = Run();
            if (report.failed != 0)
                throw new InvalidOperationException(JsonUtility.ToJson(report, true));

            Debug.Log($"[T-FO06N] Participant manifest: {report.passed} pure checks PASS / {report.failed} FAIL; live manifest binding remains unimplemented.");
        }

        public static Report Run()
        {
            if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stable Edit Mode required.");

            var passed = new List<string>();
            var failed = new List<string>();

            void Check(string name, Action action)
            {
                try
                {
                    action();
                    passed.Add(name);
                }
                catch (Exception exception)
                {
                    failed.Add(name + ": " + exception.GetType().Name + ": " + exception.Message);
                }
            }

            Check("Null and empty manifests fail closed", () =>
            {
                Require(!GlobalMotionRebaseParticipantManifest.TryCreate(null, out _, out var error));
                Require(error == "manifest_entries_required");
                Require(!GlobalMotionRebaseParticipantManifest.TryCreate(Array.Empty<GlobalMotionRebaseManifestEntry>(), out _, out error));
                Require(error == "manifest_empty");
            });

            Check("Entry tokens reject whitespace, control characters and unsupported IDs", () =>
            {
                Throws(() => new GlobalMotionRebaseManifestEntry("", GlobalMotionRebaseParticipantKind.Camera, "camera", 0));
                Throws(() => new GlobalMotionRebaseManifestEntry(" CAMERA", GlobalMotionRebaseParticipantKind.Camera, "camera", 0));
                Throws(() => new GlobalMotionRebaseManifestEntry("CAMERA\n", GlobalMotionRebaseParticipantKind.Camera, "camera", 0));
                Throws(() => new GlobalMotionRebaseManifestEntry("camera:owner", GlobalMotionRebaseParticipantKind.Camera, "camera", 0));
                Throws(() => new GlobalMotionRebaseManifestEntry("CAMERA", (GlobalMotionRebaseParticipantKind)99, "camera", 0));
                Throws(() => new GlobalMotionRebaseManifestEntry("CAMERA", GlobalMotionRebaseParticipantKind.Camera, "camera", -1));
            });

            Check("Duplicate participant IDs and duplicate kind/source identities reject", () =>
            {
                var duplicateId = new[]
                {
                    Entry("A", GlobalMotionRebaseParticipantKind.Camera, "camera-a", 0),
                    Entry("A", GlobalMotionRebaseParticipantKind.Camera, "camera-b", 1)
                };
                Require(!GlobalMotionRebaseParticipantManifest.TryCreate(duplicateId, out _, out var error));
                Require(error == "duplicate_participant_id:A");

                var duplicateSource = new[]
                {
                    Entry("A", GlobalMotionRebaseParticipantKind.Camera, "camera", 0),
                    Entry("B", GlobalMotionRebaseParticipantKind.Camera, "camera", 1)
                };
                Require(!GlobalMotionRebaseParticipantManifest.TryCreate(duplicateSource, out _, out error));
                Require(error == "duplicate_source_identity:camera");
            });

            Check("Canonical ordering makes digest independent of input order", () =>
            {
                var a = new[]
                {
                    Entry("B", GlobalMotionRebaseParticipantKind.WorldAnchor, "scene/b", 1),
                    Entry("A", GlobalMotionRebaseParticipantKind.CityStatic, "scene/a", 0)
                };
                var b = new[] { a[1], a[0] };
                Require(GlobalMotionRebaseParticipantManifest.TryCreate(a, out var first, out var error), error);
                Require(GlobalMotionRebaseParticipantManifest.TryCreate(b, out var second, out error), error);
                Require(first.Digest == second.Digest);
                Require(first.Entries[0].ParticipantId == "A" && first.Entries[1].ParticipantId == "B");
            });

            Check("Kind, source identity and ordinal are digest inputs", () =>
            {
                var baseline = Manifest(Entry("A", GlobalMotionRebaseParticipantKind.Camera, "camera", 0));
                var kind = Manifest(Entry("A", GlobalMotionRebaseParticipantKind.PlayerFrame, "camera", 0));
                var source = Manifest(Entry("A", GlobalMotionRebaseParticipantKind.Camera, "camera-2", 0));
                var ordinal = Manifest(Entry("A", GlobalMotionRebaseParticipantKind.Camera, "camera", 1));
                Require(baseline.Digest != kind.Digest && baseline.Digest != source.Digest && baseline.Digest != ordinal.Digest);
            });

            Check("Manifest exposes a lowercase SHA256 digest and exact identity lookup", () =>
            {
                var manifest = Manifest(Entry("CAMERA", GlobalMotionRebaseParticipantKind.Camera, "bootstrap/main-camera", 0));
                Require(manifest.Digest.Length == 64);
                for (int i = 0; i < manifest.Digest.Length; i++)
                    Require((manifest.Digest[i] >= '0' && manifest.Digest[i] <= '9') || (manifest.Digest[i] >= 'a' && manifest.Digest[i] <= 'f'));
                Require(manifest.HasDigest(manifest.Digest));
                Require(manifest.TryGet("CAMERA", out var entry) && entry.SourceIdentity == "bootstrap/main-camera");
                Require(!manifest.TryGet("camera", out _));
            });

            Check("Canonical bytes and entry collection are defensive copies", () =>
            {
                var manifest = Manifest(Entry("A", GlobalMotionRebaseParticipantKind.Camera, "camera", 0));
                byte[] bytes = manifest.CopyCanonicalBytes();
                bytes[0] ^= 255;
                Require(manifest.CopyCanonicalBytes()[0] != bytes[0]);
                Require(manifest.Entries.Count == 1);
            });

            Check("Required fixed coverage accepts 4 fixed roots, 22 ship roots and 20 deck participants", () =>
            {
                var manifest = RequiredManifest();
                Require(manifest.Count == 46);
                Require(manifest.TryValidateRequiredCoverage(out var error), error);
                Require(manifest.TryGet("SHIP_ROOT/01", out var root) && root.Kind == GlobalMotionRebaseParticipantKind.ShipRoot);
                Require(manifest.TryGet("SHIP_ROOT/22", out root) && root.Ordinal == 22);
                Require(manifest.TryGet("SHIP_DECK_NAV/20", out var nav) && nav.Kind == GlobalMotionRebaseParticipantKind.ShipDeckNav);
            });

            Check("Required coverage reports missing and kind-mismatched fixed entries", () =>
            {
                var entries = RequiredEntries();
                entries.RemoveAt(entries.Count - 1);
                var missing = Manifest(entries.ToArray());
                Require(!missing.TryValidateRequiredCoverage(out var error) && error == "required_participant_missing:SHIP_DECK_NAV/20");

                entries = RequiredEntries();
                entries.RemoveAt(0);
                entries.Add(Entry("CITY_STATIC", GlobalMotionRebaseParticipantKind.Camera, "wrong", 0));
                var wrongKind = Manifest(entries.ToArray());
                Require(!wrongKind.TryValidateRequiredCoverage(out error) && error == "required_participant_kind_mismatch:CITY_STATIC");
            });

            Check("Dynamic network gameplay roots remain explicit without guessed count", () =>
            {
                var entries = RequiredEntries();
                entries.Add(Entry("NETWORK_GAMEPLAY_ROOT/NPC_001", GlobalMotionRebaseParticipantKind.NetworkGameplayRoot, "world/NPC_001", 1));
                entries.Add(Entry("NETWORK_GAMEPLAY_ROOT/CREW_001", GlobalMotionRebaseParticipantKind.NetworkGameplayRoot, "ship/CREW_001", 2));
                var manifest = Manifest(entries.ToArray());
                Require(manifest.Count == 48);
                Require(manifest.TryValidateRequiredCoverage(out var error), error);
                Require(manifest.TryGet("NETWORK_GAMEPLAY_ROOT/NPC_001", out var npc) && npc.Kind == GlobalMotionRebaseParticipantKind.NetworkGameplayRoot);
            });

            return new Report
            {
                passed = passed.Count,
                failed = failed.Count,
                checks = passed.ToArray(),
                failures = failed.ToArray()
            };
        }

        private static GlobalMotionRebaseManifestEntry Entry(string id, GlobalMotionRebaseParticipantKind kind, string source, int ordinal)
        {
            return new GlobalMotionRebaseManifestEntry(id, kind, source, ordinal);
        }

        private static GlobalMotionRebaseParticipantManifest Manifest(params GlobalMotionRebaseManifestEntry[] entries)
        {
            Require(GlobalMotionRebaseParticipantManifest.TryCreate(entries, out var manifest, out var error), error);
            return manifest;
        }

        private static GlobalMotionRebaseParticipantManifest RequiredManifest()
        {
            return Manifest(RequiredEntries().ToArray());
        }

        private static List<GlobalMotionRebaseManifestEntry> RequiredEntries()
        {
            var entries = new List<GlobalMotionRebaseManifestEntry>
            {
                Entry("CITY_STATIC", GlobalMotionRebaseParticipantKind.CityStatic, "scene/WorldRoot_0_0/static-reviewed", 0),
                Entry("WORLD_ANCHORS", GlobalMotionRebaseParticipantKind.WorldAnchor, "scene/Respawn_Default", 0),
                Entry("PLAYER_FRAME", GlobalMotionRebaseParticipantKind.PlayerFrame, "runtime/player-frame", 0),
                Entry("CAMERA", GlobalMotionRebaseParticipantKind.Camera, "runtime/active-camera-chain", 0)
            };
            for (int i = 1; i <= GlobalMotionRebaseParticipantManifest.RequiredShipRoots; i++)
                entries.Add(Entry("SHIP_ROOT/" + i.ToString("00"), GlobalMotionRebaseParticipantKind.ShipRoot, "scene/ship-root/" + i.ToString("00"), i));
            for (int i = 1; i <= GlobalMotionRebaseParticipantManifest.RequiredShipDeckNav; i++)
                entries.Add(Entry("SHIP_DECK_NAV/" + i.ToString("00"), GlobalMotionRebaseParticipantKind.ShipDeckNav, "runtime/ship-deck-nav/" + i.ToString("00"), i));
            return entries;
        }

        private static void Throws(Action action)
        {
            bool thrown = false;
            try { action(); } catch (Exception) { thrown = true; }
            Require(thrown);
        }

        private static void Require(bool condition, string reason = null)
        {
            if (!condition)
                throw new InvalidOperationException(reason ?? "Assertion failed.");
        }
    }
}
