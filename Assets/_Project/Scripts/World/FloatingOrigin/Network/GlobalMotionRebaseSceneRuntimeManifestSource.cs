using System;
using System.Collections.Generic;
using UnityEngine;
using ProjectC.AI;
using ProjectC.Player;
using ProjectC.Ship;

namespace ProjectC.World.FloatingOrigin.Network
{
    /// <summary>
    /// Supplies admission evidence only when an owner-reviewed runtime provider can produce it.
    /// The provider must not return readiness for missing adapters, incomplete proof or rollback gaps.
    /// </summary>
    public interface IGlobalMotionRebaseRuntimeAdmissionEvidenceSource
    {
        bool TryGetAdmissionEvidence(
            out GlobalMotionRebaseParticipantAdmissionEvidence evidence,
            out string error);
    }

    /// <summary>
    /// Runtime census source for the fixed global-motion participant coverage.
    /// It discovers actual ship/deck/passenger objects, rejects count or readiness drift and then
    /// delegates the non-inventable admission evidence to a separate reviewed provider.
    /// This component is dormant until explicitly referenced by the runtime bridge.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GlobalMotionRebaseSceneRuntimeManifestSource : MonoBehaviour, IGlobalMotionRebaseLiveManifestRuntimeSource
    {
        [Header("Reviewed fixed identities")]
        [SerializeField] private string _cityStaticIdentity;
        [SerializeField] private string _worldAnchorsIdentity;
        [SerializeField] private string _playerFrameIdentity;
        [SerializeField] private string _cameraIdentity;
        [SerializeField] private MonoBehaviour _admissionEvidenceSource;

        public bool TryBuildManifest(
            out GlobalMotionRebaseParticipantManifest manifest,
            out GlobalMotionRebaseParticipantAdmissionEvidence admission,
            out string error)
        {
            manifest = null;
            admission = default;
            error = null;

            if (!ValidateIdentity(_cityStaticIdentity, "city_static_identity", out error) ||
                !ValidateIdentity(_worldAnchorsIdentity, "world_anchors_identity", out error) ||
                !ValidateIdentity(_playerFrameIdentity, "player_frame_identity", out error) ||
                !ValidateIdentity(_cameraIdentity, "camera_identity", out error))
                return false;

            if (!(_admissionEvidenceSource is IGlobalMotionRebaseRuntimeAdmissionEvidenceSource provider))
                return Reject("runtime_admission_evidence_source_missing", out error);

            ShipController[] ships = FindObjectsByType<ShipController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            ShipDeckNav[] decks = FindObjectsByType<ShipDeckNav>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            NpcBrain[] passengers = FindObjectsByType<NpcBrain>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            if (ships.Length != GlobalMotionRebaseParticipantManifest.RequiredShipRoots)
                return Reject("ship_root_count_mismatch:actual=" + ships.Length + ";expected=" + GlobalMotionRebaseParticipantManifest.RequiredShipRoots, out error);
            if (decks.Length != GlobalMotionRebaseParticipantManifest.RequiredShipDeckNav)
                return Reject("ship_deck_nav_count_mismatch:actual=" + decks.Length + ";expected=" + GlobalMotionRebaseParticipantManifest.RequiredShipDeckNav, out error);
            if (passengers.Length != GlobalMotionRebaseParticipantManifest.RequiredShipDeckNav)
                return Reject("passenger_count_mismatch:actual=" + passengers.Length + ";expected=" + GlobalMotionRebaseParticipantManifest.RequiredShipDeckNav, out error);

            var entries = new List<GlobalMotionRebaseManifestEntry>(4 + ships.Length + decks.Length)
            {
                new GlobalMotionRebaseManifestEntry("CITY_STATIC", GlobalMotionRebaseParticipantKind.CityStatic, _cityStaticIdentity, 0),
                new GlobalMotionRebaseManifestEntry("WORLD_ANCHORS", GlobalMotionRebaseParticipantKind.WorldAnchor, _worldAnchorsIdentity, 0),
                new GlobalMotionRebaseManifestEntry("PLAYER_FRAME", GlobalMotionRebaseParticipantKind.PlayerFrame, _playerFrameIdentity, 0),
                new GlobalMotionRebaseManifestEntry("CAMERA", GlobalMotionRebaseParticipantKind.Camera, _cameraIdentity, 0)
            };

            if (!AppendShips(ships, entries, out error) || !AppendDecks(decks, passengers, entries, out error))
                return false;
            if (!provider.TryGetAdmissionEvidence(out admission, out error))
                return false;
            if (!GlobalMotionRebaseParticipantManifest.TryCreate(entries, out manifest, out error))
                return false;
            if (!manifest.TryValidateRequiredCoverage(out error))
            {
                manifest = null;
                return false;
            }

            return true;
        }

        private static bool AppendShips(
            ShipController[] ships,
            List<GlobalMotionRebaseManifestEntry> entries,
            out string error)
        {
            error = null;
            Array.Sort(ships, CompareNames);
            var identities = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < ships.Length; i++)
            {
                ShipController ship = ships[i];
                if (ship == null || !identities.Add(ship.name))
                    return Reject("ship_root_identity_missing_or_duplicate", out error);
                entries.Add(new GlobalMotionRebaseManifestEntry(
                    "SHIP_ROOT/" + (i + 1).ToString("00"),
                    GlobalMotionRebaseParticipantKind.ShipRoot,
                    "ship-root:" + ship.name,
                    i + 1));
            }
            return true;
        }

        private static bool AppendDecks(
            ShipDeckNav[] decks,
            NpcBrain[] passengers,
            List<GlobalMotionRebaseManifestEntry> entries,
            out string error)
        {
            error = null;
            Array.Sort(decks, CompareNames);
            Array.Sort(passengers, CompareNames);
            var identities = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < decks.Length; i++)
            {
                ShipDeckNav deck = decks[i];
                if (deck == null || !identities.Add(deck.name))
                    return Reject("ship_deck_nav_identity_missing_or_duplicate", out error);
                if (!deck.IsRegistered || !deck.IsNavMeshInstanceValid || !deck.IsReady)
                    return Reject("ship_deck_nav_not_ready:" + deck.name, out error);
                entries.Add(new GlobalMotionRebaseManifestEntry(
                    "SHIP_DECK_NAV/" + (i + 1).ToString("00"),
                    GlobalMotionRebaseParticipantKind.ShipDeckNav,
                    "ship-deck-nav:" + deck.name,
                    i + 1));
            }

            for (int i = 0; i < passengers.Length; i++)
            {
                NpcBrain passenger = passengers[i];
                if (passenger == null || !passenger.IsExplicitShipAttachmentRequested ||
                    !passenger.IsExplicitShipAttachmentActive || !passenger.IsDeckProxyCreated ||
                    !passenger.IsDeckProxyOnNavMesh || !passenger.IsDeckNavigationActive ||
                    string.IsNullOrWhiteSpace(passenger.DeckNavName))
                    return Reject("passenger_attachment_not_ready:" + (passenger != null ? passenger.name : "<null>"), out error);
            }
            return true;
        }

        private static int CompareNames(UnityEngine.Object left, UnityEngine.Object right)
        {
            return string.CompareOrdinal(left != null ? left.name : string.Empty, right != null ? right.name : string.Empty);
        }

        private static bool ValidateIdentity(string value, string field, out string error)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Trim() != value)
                return Reject(field + "_required", out error);
            error = null;
            return true;
        }

        private static bool Reject(string reason, out string error)
        {
            error = reason;
            return false;
        }
    }
}
