using UnityEngine;
using Unity.Netcode;
using ProjectC.Quests;
using ProjectC.Player;

namespace ProjectC.Quests.Testing
{
    /// <summary>
    /// M13 test: trigger zone that auto-discovers a quest to a player on enter.
    /// Production code would gate this by prerequisites; this is for M13 verify only.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class M13QuestTriggerZone : MonoBehaviour
    {
        [Tooltip("Quest ID to auto-offer when player enters (Discovered state).")]
        public string questId = "collect_copper_ore";

        [Tooltip("If true, only fire once per client. Set false for replayable tests.")]
        public bool oneShot = true;

        private readonly System.Collections.Generic.HashSet<ulong> _alreadyTriggered = new System.Collections.Generic.HashSet<ulong>();

        private void Reset()
        {
            var col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            // M13 test: react to NetworkPlayer (server или client view оба)
            var netPlayer = other.GetComponent<NetworkPlayer>();
            if (netPlayer == null) return;
            ulong clientId = netPlayer.OwnerClientId;

            if (oneShot && _alreadyTriggered.Contains(clientId)) return;

            // Server-only quest mutation
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer) {
                Debug.LogWarning($"[M13QuestTriggerZone] {name}: client {clientId} entered, but not server. NetworkManager={nm}");
                return;
            }
            var w = QuestWorld.Instance;
            if (w == null) {
                Debug.LogWarning($"[M13QuestTriggerZone] {name}: QuestWorld.Instance == null");
                return;
            }

            // Idempotency
            var existing = w.GetPlayerQuests(clientId);
            for (int i = 0; i < existing.Count; i++)
            {
                if (existing[i].questId == questId) { _alreadyTriggered.Add(clientId); return; }
            }

            var newInst = new QuestInstance
            {
                questId = questId,
                state = QuestState.Discovered,
                isTracked = false
            };
            existing.Add(newInst);
            w.SavePlayer(clientId);
            _alreadyTriggered.Add(clientId);

            // Send snapshot to client
            var qserver = QuestServer.Instance;
            if (qserver != null) {
                // TriggerZone needs to notify client. QuestServer.SendQuestSnapshotToClient is private.
                // Use reflection: just call the private method.
                var method = typeof(QuestServer).GetMethod("SendQuestSnapshotToClient",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                    null, new System.Type[] { typeof(ulong) }, null);
                if (method != null) {
                    method.Invoke(qserver, new object[] { clientId });
                }
            }

            Debug.Log($"[M13QuestTriggerZone] Auto-discovered quest '{questId}' for client {clientId} on enter trigger '{name}'");
        }
    }
}
