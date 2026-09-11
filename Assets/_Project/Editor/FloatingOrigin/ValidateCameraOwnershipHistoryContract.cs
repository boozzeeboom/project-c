using System;
using System.Collections.Generic;
using UnityEditor;
using ProjectC.World.FloatingOrigin.Network;

namespace ProjectC.EditorTools.FloatingOrigin
{
    /// <summary>
    /// Pure Edit Mode validation for dormant camera ownership/history evidence.
    /// </summary>
    public static class ValidateCameraOwnershipHistoryContract
    {
        public sealed class Report
        {
            public int passed;
            public int failed;
            public readonly List<string> failures = new List<string>();
        }

        [MenuItem("ProjectC/World/Floating Origin/Validate Camera Ownership History Contract")]
        public static void Execute()
        {
            Report report = Run();
            if (report.failed != 0)
                throw new InvalidOperationException($"[T-FO06W] Camera ownership/history contract: {report.passed} PASS / {report.failed} FAIL; first={report.failures[0]}");

            UnityEngine.Debug.Log($"[T-FO06W] Camera ownership/history contract: {report.passed} pure checks PASS / {report.failed} FAIL; runtime camera ownership remains unimplemented.");
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

            const string cameraId = "camera:owner-0";
            const string ownerId = "player:0";
            const string targetId = "player-transform:0";
            const string activeCameraId = "camera:owner-0";

            CameraOwnershipHistoryEvidence Ready() => new CameraOwnershipHistoryEvidence(
                cameraId, ownerId, targetId, activeCameraId, 4, 9,
                activeCamera: true,
                targetBound: true,
                historyCaptured: true,
                collisionHistoryCaptured: true,
                historyContinuityVerified: true,
                billboardBindingVerified: true);

            Check("Complete evidence validates", () =>
            {
                CameraOwnershipHistoryEvidence evidence = Ready();
                Require(CameraOwnershipHistoryContract.TryValidate(evidence, out string error), error);
                Require(CameraOwnershipHistoryContract.IsReady(evidence), "complete_evidence_rejected");
            });

            Check("Missing camera identity fails closed", () =>
            {
                var evidence = ReadyWith(cameraIdOverride: "");
                Require(!CameraOwnershipHistoryContract.TryValidate(evidence, out string error) && error == "camera_id_required", error);
            });

            Check("Missing owner identity fails closed", () =>
            {
                var evidence = ReadyWith(ownerIdOverride: "");
                Require(!CameraOwnershipHistoryContract.TryValidate(evidence, out string error) && error == "camera_owner_id_required", error);
            });

            Check("Missing target identity fails closed", () =>
            {
                var evidence = ReadyWith(targetIdOverride: "");
                Require(!CameraOwnershipHistoryContract.TryValidate(evidence, out string error) && error == "camera_target_id_required", error);
            });

            Check("Missing active camera identity fails closed", () =>
            {
                var evidence = ReadyWith(activeCameraIdOverride: "");
                Require(!CameraOwnershipHistoryContract.TryValidate(evidence, out string error) && error == "active_camera_id_required", error);
            });

            Check("Missing binding generation fails closed", () =>
            {
                var evidence = ReadyWith(bindingGeneration: 0);
                Require(!CameraOwnershipHistoryContract.TryValidate(evidence, out string error) && error == "camera_binding_generation_required", error);
            });

            Check("Missing history generation fails closed", () =>
            {
                var evidence = ReadyWith(historyGeneration: 0);
                Require(!CameraOwnershipHistoryContract.TryValidate(evidence, out string error) && error == "camera_history_generation_required", error);
            });

            Check("Active camera mismatch fails closed", () =>
            {
                var evidence = ReadyWith(activeCameraIdOverride: "camera:other");
                Require(!CameraOwnershipHistoryContract.TryValidate(evidence, out string error) && error == "active_camera_mismatch:camera:owner-0", error);
            });

            Check("Inactive camera fails closed", () =>
            {
                var evidence = ReadyWith(activeCamera: false);
                Require(!CameraOwnershipHistoryContract.TryValidate(evidence, out string error) && error == "camera_not_active:camera:owner-0", error);
            });

            Check("Unbound target fails closed", () =>
            {
                var evidence = ReadyWith(targetBound: false);
                Require(!CameraOwnershipHistoryContract.TryValidate(evidence, out string error) && error == "camera_target_not_bound:player-transform:0", error);
            });

            Check("Missing camera history fails closed", () =>
            {
                var evidence = ReadyWith(historyCaptured: false);
                Require(!CameraOwnershipHistoryContract.TryValidate(evidence, out string error) && error == "camera_history_not_captured:camera:owner-0", error);
            });

            Check("Missing collision history fails closed", () =>
            {
                var evidence = ReadyWith(collisionHistoryCaptured: false);
                Require(!CameraOwnershipHistoryContract.TryValidate(evidence, out string error) && error == "camera_collision_history_not_captured:camera:owner-0", error);
            });

            Check("History continuity fails closed", () =>
            {
                var evidence = ReadyWith(historyContinuityVerified: false);
                Require(!CameraOwnershipHistoryContract.TryValidate(evidence, out string error) && error == "camera_history_continuity_unverified:camera:owner-0", error);
            });

            Check("Billboard binding fails closed", () =>
            {
                var evidence = ReadyWith(billboardBindingVerified: false);
                Require(!CameraOwnershipHistoryContract.TryValidate(evidence, out string error) && error == "camera_billboard_binding_unverified:camera:owner-0", error);
            });

            Check("Validation does not mutate evidence", () =>
            {
                var evidence = Ready();
                Require(CameraOwnershipHistoryContract.TryValidate(evidence, out string error), error);
                Require(evidence.CameraId == cameraId && evidence.OwnerId == ownerId && evidence.TargetId == targetId &&
                    evidence.ActiveCameraId == activeCameraId && evidence.BindingGeneration == 4 && evidence.HistoryGeneration == 9 &&
                    evidence.HistoryContinuityVerified && evidence.BillboardBindingVerified, "evidence_mutated");
            });

            return report;

            CameraOwnershipHistoryEvidence ReadyWith(
                string cameraIdOverride = null,
                string ownerIdOverride = null,
                string targetIdOverride = null,
                string activeCameraIdOverride = null,
                ulong? bindingGeneration = null,
                ulong? historyGeneration = null,
                bool? activeCamera = null,
                bool? targetBound = null,
                bool? historyCaptured = null,
                bool? collisionHistoryCaptured = null,
                bool? historyContinuityVerified = null,
                bool? billboardBindingVerified = null)
            {
                return new CameraOwnershipHistoryEvidence(
                    cameraIdOverride ?? cameraId,
                    ownerIdOverride ?? ownerId,
                    targetIdOverride ?? targetId,
                    activeCameraIdOverride ?? activeCameraId,
                    bindingGeneration ?? 4,
                    historyGeneration ?? 9,
                    activeCamera ?? true,
                    targetBound ?? true,
                    historyCaptured ?? true,
                    collisionHistoryCaptured ?? true,
                    historyContinuityVerified ?? true,
                    billboardBindingVerified ?? true);
            }
        }

        private static void Require(bool condition, string error)
        {
            if (!condition)
                throw new InvalidOperationException(error ?? "assertion_failed");
        }
    }
}