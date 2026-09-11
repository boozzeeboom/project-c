using System;
using System.Collections.Generic;
using UnityEditor;
using ProjectC.World.FloatingOrigin.Network;

namespace ProjectC.EditorTools.FloatingOrigin
{
    /// <summary>
    /// Pure Edit Mode validation for the dormant T-FO06V ShipDeckNav readiness contract.
    /// It does not inspect scenes, NavMeshData, agents, or runtime attachment state.
    /// </summary>
    public static class ValidateShipDeckNavReadinessContract
    {
        public sealed class Report
        {
            public int passed;
            public int failed;
            public readonly List<string> failures = new List<string>();
        }

        [MenuItem("ProjectC/World/Floating Origin/Validate ShipDeckNav Readiness Contract")]
        public static void Execute()
        {
            Report report = Run();
            if (report.failed != 0)
                throw new InvalidOperationException($"[T-FO06V] ShipDeckNav readiness contract: {report.passed} PASS / {report.failed} FAIL; first={report.failures[0]}");

            UnityEngine.Debug.Log($"[T-FO06V] ShipDeckNav readiness contract: {report.passed} pure checks PASS / {report.failed} FAIL; runtime registration, proxy agents and passenger attachment remain unimplemented.");
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

            const string shipId = "SHIP_ROOT/03";
            const string deckNavId = "SHIP_DECK_NAV/03";
            const string passengerId = "crew/passenger-03";
            const string navMeshDataId = "navmeshdata:ship-03";
            const string proxyAgentId = "proxy-agent:passenger-03";
            const string attachmentEvidenceId = "attachment:ship-03:passenger-03";

            ShipDeckNavReadinessEvidence Ready() => new ShipDeckNavReadinessEvidence(
                shipId,
                deckNavId,
                passengerId,
                navMeshDataId,
                proxyAgentId,
                attachmentEvidenceId,
                shipDeckNavRegistered: true,
                navMeshDataInstanceValid: true,
                proxyAgentCreated: true,
                proxyAgentIsOnNavMesh: true,
                passengerAttachmentRequested: true,
                passengerAnchorResolved: true,
                passengerAttachmentCompleted: true,
                passengerProvenanceRecorded: true);

            Check("Complete evidence validates", () =>
            {
                ShipDeckNavReadinessEvidence evidence = Ready();
                Require(ShipDeckNavReadinessContract.TryValidate(evidence, out string error), error);
                Require(ShipDeckNavReadinessContract.IsReady(evidence), "complete_evidence_rejected");
            });

            Check("Registered alone is insufficient", () =>
            {
                ShipDeckNavReadinessEvidence evidence = new ShipDeckNavReadinessEvidence(
                    shipId, deckNavId, passengerId, navMeshDataId, proxyAgentId, attachmentEvidenceId,
                    shipDeckNavRegistered: true,
                    navMeshDataInstanceValid: false,
                    proxyAgentCreated: false,
                    proxyAgentIsOnNavMesh: false,
                    passengerAttachmentRequested: true,
                    passengerAnchorResolved: true,
                    passengerAttachmentCompleted: false,
                    passengerProvenanceRecorded: false);
                Require(!ShipDeckNavReadinessContract.TryValidate(evidence, out string error) &&
                    error == "nav_mesh_data_instance_invalid:SHIP_DECK_NAV/03", error);
            });

            Check("Missing ShipDeckNav registration fails closed", () =>
            {
                var evidence = ReadyWith(shipDeckNavRegistered: false);
                Require(!ShipDeckNavReadinessContract.TryValidate(evidence, out string error) &&
                    error == "ship_deck_nav_not_registered:SHIP_DECK_NAV/03", error);
            });

            Check("Missing NavMeshData identity fails closed", () =>
            {
                var evidence = ReadyWith(navMeshDataIdOverride: "");
                Require(!ShipDeckNavReadinessContract.TryValidate(evidence, out string error) &&
                    error == "nav_mesh_data_id_required", error);
            });

            Check("Invalid NavMeshDataInstance fails closed", () =>
            {
                var evidence = ReadyWith(navMeshDataInstanceValid: false);
                Require(!ShipDeckNavReadinessContract.TryValidate(evidence, out string error) &&
                    error == "nav_mesh_data_instance_invalid:SHIP_DECK_NAV/03", error);
            });

            Check("Missing proxy agent creation fails closed", () =>
            {
                var evidence = ReadyWith(proxyAgentCreated: false);
                Require(!ShipDeckNavReadinessContract.TryValidate(evidence, out string error) &&
                    error == "proxy_agent_not_created:proxy-agent:passenger-03", error);
            });

            Check("Proxy agent off NavMesh fails closed", () =>
            {
                var evidence = ReadyWith(proxyAgentIsOnNavMesh: false);
                Require(!ShipDeckNavReadinessContract.TryValidate(evidence, out string error) &&
                    error == "proxy_agent_not_on_nav_mesh:proxy-agent:passenger-03", error);
            });

            Check("Attachment request alone is insufficient", () =>
            {
                var evidence = ReadyWith(passengerAttachmentCompleted: false, passengerProvenanceRecorded: false);
                Require(!ShipDeckNavReadinessContract.TryValidate(evidence, out string error) &&
                    error == "passenger_attachment_incomplete:crew/passenger-03", error);
            });

            Check("Missing attachment request fails closed", () =>
            {
                var evidence = ReadyWith(passengerAttachmentRequested: false);
                Require(!ShipDeckNavReadinessContract.TryValidate(evidence, out string error) &&
                    error == "passenger_attachment_not_requested:crew/passenger-03", error);
            });

            Check("Unresolved passenger anchor fails closed", () =>
            {
                var evidence = ReadyWith(passengerAnchorResolved: false);
                Require(!ShipDeckNavReadinessContract.TryValidate(evidence, out string error) &&
                    error == "passenger_anchor_unresolved:crew/passenger-03", error);
            });

            Check("Missing completed attachment fails closed", () =>
            {
                var evidence = ReadyWith(passengerAttachmentCompleted: false);
                Require(!ShipDeckNavReadinessContract.TryValidate(evidence, out string error) &&
                    error == "passenger_attachment_incomplete:crew/passenger-03", error);
            });

            Check("Missing attachment evidence identity fails closed", () =>
            {
                var evidence = ReadyWith(attachmentEvidenceIdOverride: "");
                Require(!ShipDeckNavReadinessContract.TryValidate(evidence, out string error) &&
                    error == "attachment_evidence_id_required", error);
            });

            Check("Missing passenger provenance fails closed", () =>
            {
                var evidence = ReadyWith(passengerProvenanceRecorded: false);
                Require(!ShipDeckNavReadinessContract.TryValidate(evidence, out string error) &&
                    error == "passenger_provenance_missing:crew/passenger-03", error);
            });

            Check("Failure precedence is deterministic", () =>
            {
                var evidence = new ShipDeckNavReadinessEvidence(
                    "", "", "", "", "", "",
                    shipDeckNavRegistered: false,
                    navMeshDataInstanceValid: false,
                    proxyAgentCreated: false,
                    proxyAgentIsOnNavMesh: false,
                    passengerAttachmentRequested: false,
                    passengerAnchorResolved: false,
                    passengerAttachmentCompleted: false,
                    passengerProvenanceRecorded: false);
                Require(!ShipDeckNavReadinessContract.TryValidate(evidence, out string error) &&
                    error == "ship_id_required", error);
            });

            Check("Validation does not mutate evidence", () =>
            {
                var evidence = Ready();
                Require(ShipDeckNavReadinessContract.TryValidate(evidence, out string error), error);
                Require(evidence.ShipId == shipId && evidence.ShipDeckNavId == deckNavId &&
                    evidence.PassengerId == passengerId && evidence.NavMeshDataInstanceValid &&
                    evidence.ProxyAgentIsOnNavMesh && evidence.PassengerAttachmentCompleted &&
                    evidence.PassengerProvenanceRecorded, "evidence_mutated");
            });

            return report;

            ShipDeckNavReadinessEvidence ReadyWith(
                string shipIdOverride = null,
                string deckNavIdOverride = null,
                string passengerIdOverride = null,
                string navMeshDataIdOverride = null,
                string proxyAgentIdOverride = null,
                string attachmentEvidenceIdOverride = null,
                bool? shipDeckNavRegistered = null,
                bool? navMeshDataInstanceValid = null,
                bool? proxyAgentCreated = null,
                bool? proxyAgentIsOnNavMesh = null,
                bool? passengerAttachmentRequested = null,
                bool? passengerAnchorResolved = null,
                bool? passengerAttachmentCompleted = null,
                bool? passengerProvenanceRecorded = null)
            {
                return new ShipDeckNavReadinessEvidence(
                    shipIdOverride ?? shipId,
                    deckNavIdOverride ?? deckNavId,
                    passengerIdOverride ?? passengerId,
                    navMeshDataIdOverride ?? navMeshDataId,
                    proxyAgentIdOverride ?? proxyAgentId,
                    attachmentEvidenceIdOverride ?? attachmentEvidenceId,
                    shipDeckNavRegistered ?? true,
                    navMeshDataInstanceValid ?? true,
                    proxyAgentCreated ?? true,
                    proxyAgentIsOnNavMesh ?? true,
                    passengerAttachmentRequested ?? true,
                    passengerAnchorResolved ?? true,
                    passengerAttachmentCompleted ?? true,
                    passengerProvenanceRecorded ?? true);
            }
        }

        private static void Require(bool condition, string error)
        {
            if (!condition)
                throw new InvalidOperationException(error ?? "assertion_failed");
        }
    }
}