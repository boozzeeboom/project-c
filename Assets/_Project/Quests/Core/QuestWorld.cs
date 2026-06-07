// T-Q05: QuestWorld — server-side POCO singleton. Source of truth for all
// per-player quest state. См. docs/NPC_quests/02_V2_ARCHITECTURE.md §2.4.
//
// Initialized by QuestServer.OnNetworkSpawn (server-only). On clients this
// type is not used — clients consume DTOs via QuestClientState (T-Q07).
//
// T-Q05 scope: skeleton (init, getters, registry by questId). Full logic
// (TryOffer/TryAdvanceObjective/ModifyReputation/Save/Load) lands in T-Q06+.

using System;
using System.Collections.Generic;
using UnityEngine;
using ProjectC.Factions;

namespace ProjectC.Quests
{
    /// <summary>
    /// Server-side quest/reputation/attitude state. Singleton — one per server
    /// process. NOT replicated (clients receive DTOs).
    /// </summary>
    public class QuestWorld
    {
        public static QuestWorld Instance { get; private set; }

        /// <summary>
        /// Static quest definitions. Set by QuestServer.OnNetworkSpawn via
        /// questDatabase.allQuests field (a new SO registry, T-Q09).
        /// T-Q05: list populated manually by tests; full integration in T-Q09.
        /// </summary>
        private readonly Dictionary<string, QuestDefinition> _questById = new Dictionary<string, QuestDefinition>();

        /// <summary>Per-player quest instances (key: clientId).</summary>
        private readonly Dictionary<ulong, List<QuestInstance>> _questsByPlayer = new Dictionary<ulong, List<QuestInstance>>();

        /// <summary>Per-player faction reputation (key: (clientId, FactionId)).</summary>
        private readonly Dictionary<(ulong, FactionId), int> _reputation = new Dictionary<(ulong, FactionId), int>();

        /// <summary>Per-player NPC attitude (key: (clientId, npcId)).</summary>
        private readonly Dictionary<(ulong, string), int> _npcAttitude = new Dictionary<(ulong, string), int>();

        /// <summary>Per-player world flags (key: (clientId, flagId)).</summary>
        private readonly Dictionary<(ulong, string), bool> _worldFlags = new Dictionary<(ulong, string), bool>();

        /// <summary>Per-player active dialog session (key: clientId). Содержит treeId+currentNodeId для resume.</summary>
        private readonly Dictionary<ulong, DialogSession> _dialogByPlayer = new Dictionary<ulong, DialogSession>();

        /// <summary>Active dialog session state (in-memory only, не persisted).</summary>
        public class DialogSession
        {
            public string treeId = "";
            public string currentNodeId = "";
            public string talkingToNpcId = "";
            public ulong startedAtUnix = 0;
        }

        // ============ Lifecycle ============

        /// <summary>Create and assign singleton. Idempotent (call again replaces; for tests).</summary>
        public static void CreateAndInitialize(QuestDefinition[] questDatabase)
        {
            if (Instance != null)
            {
                Debug.LogWarning("[QuestWorld] Already initialized — replacing.");
            }
            Instance = new QuestWorld();
            Instance.RegisterQuests(questDatabase);
            Debug.Log($"[QuestWorld] Initialized: {Instance._questById.Count} quest definitions registered.");
        }

        /// <summary>Reset singleton. Editor/test only.</summary>
        public static void DestroyInstance()
        {
            Instance = null;
        }

        private void RegisterQuests(QuestDefinition[] quests)
        {
            if (quests == null) return;
            for (int i = 0; i < quests.Length; i++)
            {
                var q = quests[i];
                if (q == null || string.IsNullOrEmpty(q.questId)) continue;
                _questById[q.questId] = q;
            }
        }

        // ============ Quest registry accessors ============

        public QuestDefinition GetQuest(string questId)
        {
            if (string.IsNullOrEmpty(questId)) return null;
            return _questById.TryGetValue(questId, out var q) ? q : null;
        }

        public QuestDefinition[] GetAllQuests()
        {
            var result = new QuestDefinition[_questById.Count];
            int i = 0;
            foreach (var q in _questById.Values) result[i++] = q;
            return result;
        }

        public int QuestCount => _questById.Count;

        // ============ Per-player state accessors (T-Q06+ fills real logic) ============

        public List<QuestInstance> GetPlayerQuests(ulong clientId)
        {
            if (!_questsByPlayer.TryGetValue(clientId, out var list))
            {
                list = new List<QuestInstance>();
                _questsByPlayer[clientId] = list;
            }
            return list;
        }

        public int GetReputation(ulong clientId, FactionId faction)
        {
            return _reputation.TryGetValue((clientId, faction), out var v) ? v : 0;
        }

        public int GetNpcAttitude(ulong clientId, string npcId)
        {
            return _npcAttitude.TryGetValue((clientId, npcId), out var v) ? v : 0;
        }

        public bool GetFlag(ulong clientId, string flagId)
        {
            return _worldFlags.TryGetValue((clientId, flagId), out var v) && v;
        }

        public DialogSession GetDialogSession(ulong clientId)
        {
            return _dialogByPlayer.TryGetValue(clientId, out var s) ? s : null;
        }

        // ============ Shutdown ============

        /// <summary>
        /// Cleanup on QuestServer.OnNetworkDespawn. T-Q05: clears singleton.
        /// T-Q18: flush SaveAll() first.
        /// </summary>
        public void Shutdown()
        {
            Debug.Log("[QuestWorld] Shutdown.");
            _questById.Clear();
            _questsByPlayer.Clear();
            _reputation.Clear();
            _npcAttitude.Clear();
            _worldFlags.Clear();
            _dialogByPlayer.Clear();
        }
    }
}
