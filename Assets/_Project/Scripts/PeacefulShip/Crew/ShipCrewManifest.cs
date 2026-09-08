using System;
using System.Collections.Generic;
using UnityEngine;
using ProjectC.Quests;

namespace ProjectC.PeacefulShip.Crew
{
    public enum ShipCrewRole
    {
        Pilot = 0,
        Captain = 1,
        Engineer = 2,
        Gunner = 3,
        Navigator = 4,
        Passenger = 5
    }

    public enum ShipCrewRespawnPolicy
    {
        Never = 0,
        OnShipSpawn = 1,
        AfterDelay = 2
    }

    [Serializable]
    public sealed class ShipCrewMemberEntry
    {
        [Tooltip("Stable member key inside this manifest. It is not the NPC identity ID.")]
        public string memberId = "";

        [Tooltip("Canonical NPC identity asset.")]
        public NpcDefinition npcDefinition;

        [Tooltip("Exact NetworkObject prefab used for this crew member.")]
        public GameObject prefab;

        [Tooltip("Gameplay role used by seat and activity bindings.")]
        public ShipCrewRole role = ShipCrewRole.Pilot;

        [Tooltip("Required members are validation errors when their identity or prefab is missing.")]
        public bool required = true;

        [Tooltip("MVP policy. Never means the fixed crew is not replaced during the current runtime after death.")]
        public ShipCrewRespawnPolicy respawnPolicy = ShipCrewRespawnPolicy.Never;

        [Tooltip("Stable ShipRoot-local spawn anchor ID. Resolved by ShipCrewSpawner in a later stage.")]
        public string spawnAnchorId = "";

        [Tooltip("Stable seat/stand anchor ID. Resolved by PilotSeatController integration in a later stage.")]
        public string seatAnchorId = "";

        [Tooltip("Stable ShipRoot-local activity anchor IDs used by the crew activity adapter.")]
        public string[] activityAnchorIds = Array.Empty<string>();
    }

    [CreateAssetMenu(fileName = "ShipCrewManifest_", menuName = "ProjectC/Peaceful Ship/Crew Manifest", order = 120)]
    public sealed class ShipCrewManifest : ScriptableObject
    {
        [Header("Ship Identity")]
        [Tooltip("Stable ship assignment key. This is not the NetworkObject ID.")]
        public string shipId = "";

        [Header("Fixed Crew")]
        public ShipCrewMemberEntry[] members = Array.Empty<ShipCrewMemberEntry>();

        public bool TryGetMember(string memberId, out ShipCrewMemberEntry member)
        {
            if (!string.IsNullOrEmpty(memberId) && members != null)
            {
                foreach (var candidate in members)
                {
                    if (candidate != null && string.Equals(candidate.memberId, memberId, StringComparison.Ordinal))
                    {
                        member = candidate;
                        return true;
                    }
                }
            }

            member = null;
            return false;
        }

        public bool TryGetRole(ShipCrewRole role, out ShipCrewMemberEntry member)
        {
            if (members != null)
            {
                foreach (var candidate in members)
                {
                    if (candidate != null && candidate.role == role)
                    {
                        member = candidate;
                        return true;
                    }
                }
            }

            member = null;
            return false;
        }

        private void OnValidate()
        {
            if (members == null)
                return;

            var memberIds = new HashSet<string>(StringComparer.Ordinal);
            var npcIds = new HashSet<string>(StringComparer.Ordinal);
            var roles = new HashSet<ShipCrewRole>();

            foreach (var member in members)
            {
                if (member == null)
                {
                    Debug.LogError($"[{nameof(ShipCrewManifest)}:{name}] contains a null crew entry.", this);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(member.memberId))
                    Debug.LogError($"[{nameof(ShipCrewManifest)}:{name}] has a crew entry without memberId.", this);
                else if (!memberIds.Add(member.memberId))
                    Debug.LogError($"[{nameof(ShipCrewManifest)}:{name}] duplicate memberId '{member.memberId}'.", this);

                if (member.npcDefinition == null)
                {
                    if (member.required)
                        Debug.LogError($"[{nameof(ShipCrewManifest)}:{name}] required member '{member.memberId}' has no NpcDefinition.", this);
                }
                else if (!npcIds.Add(member.npcDefinition.npcId))
                {
                    Debug.LogError($"[{nameof(ShipCrewManifest)}:{name}] duplicate npcId '{member.npcDefinition.npcId}'.", this);
                }

                if (member.prefab == null)
                {
                    if (member.required)
                        Debug.LogError($"[{nameof(ShipCrewManifest)}:{name}] required member '{member.memberId}' has no prefab.", this);
                }
                else
                {
                    var controller = member.prefab.GetComponent<NpcController>();
                    if (controller == null)
                    {
                        Debug.LogError($"[{nameof(ShipCrewManifest)}:{name}] prefab '{member.prefab.name}' for '{member.memberId}' has no NpcController.", this);
                    }
                    else if (member.npcDefinition != null && controller.Definition != member.npcDefinition)
                    {
                        Debug.LogError($"[{nameof(ShipCrewManifest)}:{name}] prefab '{member.prefab.name}' definition does not match entry '{member.memberId}'.", this);
                    }
                }

                if (!roles.Add(member.role))
                    Debug.LogWarning($"[{nameof(ShipCrewManifest)}:{name}] role '{member.role}' is assigned to more than one crew member.", this);
            }
        }
    }
}
