using System;
using System.Collections.Generic;
using UnityEditor;
using ProjectC.World.FloatingOrigin.Network;

namespace ProjectC.EditorTools.FloatingOrigin
{
    /// <summary>
    /// Pure validation for the T-FO06BC live manifest bridge contracts.
    /// It validates manifest publication/evidence semantics without installing the bridge or changing scenes.
    /// </summary>
    public static class ValidateGlobalMotionRebaseLiveManifestRuntimeBridge
    {
        public sealed class Report
        {
            public int passed;
            public int failed;
            public readonly List<string> failures = new List<string>();
        }

        [MenuItem("ProjectC/World/Floating Origin/Validate Live Manifest Runtime Bridge")]
        public static void Execute()
        {
            Report report = Run();
            if (report.failed != 0)
                throw new InvalidOperationException($"[T-FO06BC] Live manifest bridge: {report.passed} PASS / {report.failed} FAIL; first={report.failures[0]}");

            UnityEngine.Debug.Log($"[T-FO06BC] Live manifest bridge: {report.passed} pure checks PASS / {report.failed} FAIL; runtime installation remains disabled.");
        }

        public static Report Run()
        {
            var report = new Report();
            void Check(string name, Action action)
            {
                try
                {
                    action();
                    report.passed++;
                }
                catch (Exception exception)
                {
                    report.failed++;
                    report.failures.Add(name + ": " + exception.Message);
                }
            }

            Check("Required manifest coverage creates deterministically", () =>
            {
                Require(GlobalMotionRebaseParticipantManifest.TryCreate(
                    BuildEntries(), out var manifest, out string error), error);
                Require(manifest.TryValidateRequiredCoverage(out error), error);
                Require(manifest.Count == 46, "unexpected_manifest_count=" + manifest.Count);
            });

            Check("Ready admission can issue a receipt", () =>
            {
                var manifest = CreateManifest();
                Require(GlobalMotionRebaseLiveManifestReceiptSource.TryIssue(
                    manifest, ReadyAdmission(), "ProjectC/Server", 101, 1, out var receipt, out string error), error);
                Require(receipt.IsValid, "receipt_invalid");
                Require(receipt.EntryCount == manifest.Count, "receipt_count_mismatch");
            });

            Check("Receipt validates against its manifest", () =>
            {
                var manifest = CreateManifest();
                Require(GlobalMotionRebaseLiveManifestReceiptSource.TryIssue(
                    manifest, ReadyAdmission(), "ProjectC/Server", 101, 1, out var receipt, out string error), error);
                Require(GlobalMotionRebaseLiveManifestReceiptSource.TryValidate(
                    manifest, receipt, "ProjectC/Server", out error), error);
            });

            Check("Session evidence requires an observed peer", () =>
            {
                var manifest = CreateManifest();
                Require(GlobalMotionRebaseLiveManifestReceiptSource.TryIssue(
                    manifest, ReadyAdmission(), "ProjectC/Server", 101, 1, out var receipt, out string error), error);
                var evidence = new GlobalMotionRebaseLiveManifestSessionEvidence(
                    receipt, "ProjectC/Server/session/101", true, true, true, true, true, 0);
                Require(!GlobalMotionRebaseLiveManifestSessionEvidenceGate.TryValidate(
                    manifest, evidence, "ProjectC/Server/session/101", "ProjectC/Server", out error) &&
                    error == "manifest_observed_peer_count_required", error);
            });

            Check("Complete session evidence validates", () =>
            {
                var manifest = CreateManifest();
                Require(GlobalMotionRebaseLiveManifestReceiptSource.TryIssue(
                    manifest, ReadyAdmission(), "ProjectC/Server", 101, 1, out var receipt, out string error), error);
                var evidence = new GlobalMotionRebaseLiveManifestSessionEvidence(
                    receipt, "ProjectC/Server/session/101", true, true, true, true, true, 1);
                Require(GlobalMotionRebaseLiveManifestSessionEvidenceGate.TryValidate(
                    manifest, evidence, "ProjectC/Server/session/101", "ProjectC/Server", out error), error);
            });

            Check("Digest drift fails closed", () =>
            {
                var manifest = CreateManifest();
                Require(GlobalMotionRebaseLiveManifestReceiptSource.TryIssue(
                    manifest, ReadyAdmission(), "ProjectC/Server", 101, 1, out var receipt, out string error), error);
                var drifted = new GlobalMotionRebaseLiveManifestReceipt(
                    "deadbeef", receipt.PublisherIdentity, receipt.SessionGeneration,
                    receipt.PublicationGeneration, receipt.EntryCount);
                Require(!GlobalMotionRebaseLiveManifestReceiptSource.TryValidate(
                    manifest, drifted, "ProjectC/Server", out error) &&
                    error == "manifest_digest_mismatch", error);
            });

            return report;
        }

        private static GlobalMotionRebaseParticipantManifest CreateManifest()
        {
            Require(GlobalMotionRebaseParticipantManifest.TryCreate(
                BuildEntries(), out var manifest, out string error), error);
            return manifest;
        }

        private static GlobalMotionRebaseParticipantAdmissionEvidence ReadyAdmission()
        {
            return new GlobalMotionRebaseParticipantAdmissionEvidence(true, true, true, true, true, true);
        }

        private static List<GlobalMotionRebaseManifestEntry> BuildEntries()
        {
            var entries = new List<GlobalMotionRebaseManifestEntry>
            {
                new GlobalMotionRebaseManifestEntry("CITY_STATIC", GlobalMotionRebaseParticipantKind.CityStatic, "scene:WorldScene_0_0", 0),
                new GlobalMotionRebaseManifestEntry("WORLD_ANCHORS", GlobalMotionRebaseParticipantKind.WorldAnchor, "scene:WorldScene_0_0/anchors", 0),
                new GlobalMotionRebaseManifestEntry("PLAYER_FRAME", GlobalMotionRebaseParticipantKind.PlayerFrame, "network:player-frame", 0),
                new GlobalMotionRebaseManifestEntry("CAMERA", GlobalMotionRebaseParticipantKind.Camera, "camera:ThirdPersonCamera_0", 0)
            };
            for (int i = 1; i <= GlobalMotionRebaseParticipantManifest.RequiredShipRoots; i++)
                entries.Add(new GlobalMotionRebaseManifestEntry(
                    "SHIP_ROOT/" + i.ToString("00"), GlobalMotionRebaseParticipantKind.ShipRoot, "ship-root:" + i, i));
            for (int i = 1; i <= GlobalMotionRebaseParticipantManifest.RequiredShipDeckNav; i++)
                entries.Add(new GlobalMotionRebaseManifestEntry(
                    "SHIP_DECK_NAV/" + i.ToString("00"), GlobalMotionRebaseParticipantKind.ShipDeckNav, "ship-deck-nav:" + i, i));
            return entries;
        }

        private static void Require(bool condition, string error)
        {
            if (!condition) throw new InvalidOperationException(error ?? "check_failed");
        }
    }
}
