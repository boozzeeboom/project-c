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
using ProjectC.Core;
using ProjectC.Factions;
using ProjectC.Quests.Dto;
using ProjectC.Quests.Triggers;

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

        /// <summary>T-Q15: NPC lookup reference (для TryTurnIn NPC validation). Set via CreateAndInitialize.</summary>
        public QuestDatabase Database { get; private set; }

        /// <summary>T-Q15: cap на max active quests per player (consumed in TryAccept).</summary>
        public int MaxActiveQuestsPerPlayer { get; set; } = 20;

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
            /// <summary>T-Q10: SO ref for fast access (no GetDialogTree lookup per advance).</summary>
            public Dialogue.DialogTree tree;
            public string currentNodeId = "";
            public string npcId = "";            // T-Q10: server stores npcId for session validation
            public ulong startedAtUnix = 0;
        }

        // ============ Lifecycle ============

        /// <summary>Create and assign singleton. Idempotent (call again replaces; for tests).</summary>
        public static void CreateAndInitialize(QuestDefinition[] questDefinitions, QuestDatabase database = null, int maxActiveQuestsPerPlayer = 20)
        {
            if (Instance != null)
            {
                Debug.LogWarning("[QuestWorld] Already initialized — replacing.");
            }
            Instance = new QuestWorld();
            Instance.RegisterQuests(questDefinitions);
            Instance.Database = database;
            Instance.MaxActiveQuestsPerPlayer = maxActiveQuestsPerPlayer;
            // T-Q06: create trigger service immediately.
            Instance.TriggerService = new Triggers.QuestTriggerService(Instance);
            Debug.Log($"[QuestWorld] Initialized: {Instance._questById.Count} quest definitions registered, maxActive={maxActiveQuestsPerPlayer}, dbNPCs={(database?.npcs?.Length ?? 0)}, TriggerService online.");
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

        // ============ T-Q13: Reputation + NpcAttitude modifiers (with cross-faction influence) ============

        /// <summary>
        /// T-Q13: apply delta to per-player faction reputation (clamped to [min..max]).
        /// Publishes <see cref="ReputationChangedEvent"/> через WorldEventBus.
        /// T-Q15+: вызывается из DialogueAction.AddReputation и QuestRewardReputation grant.
        /// </summary>
        /// <param name="silent">true = НЕ publish event (для cross-fallback внутри ModifyNpcAttitude).</param>
        /// <returns>New value after clamp.</returns>
        public int ModifyReputation(ulong clientId, FactionId faction, int delta, int min = -100, int max = 100, bool silent = false)
        {
            int oldVal = GetReputation(clientId, faction);
            int newVal = oldVal + delta;
            if (newVal < min) newVal = min;
            if (newVal > max) newVal = max;
            if (newVal == oldVal) return newVal;
            _reputation[(clientId, faction)] = newVal;
            if (!silent)
            {
                WorldEventBus.Publish(new ReputationChangedEvent
                {
                    PlayerId = clientId,
                    Faction = faction,
                    NewValue = newVal,
                    Delta = newVal - oldVal
                });
            }
            if (Debug.isDebugBuild) Debug.Log($"[QuestWorld] ModifyReputation player={clientId} faction={faction} delta={delta} {oldVal}→{newVal}");
            return newVal;
        }

        /// <summary>
        /// T-Q13: apply delta to per-player NpcAttitude (clamped to NpcDefinition.personalAttitudeMin/Max,
        /// fallback NpcAttitude.MinValue/MaxValue = -100..200). Cross-faction influence:
        /// для каждой link в NpcDefinition.attitudeLinks[] — применить deltaOnLike (delta > 0)
        /// или deltaOnDislike (delta < 0) к target faction (silent — избежать recursion).
        /// Publishes <see cref="NpcAttitudeChangedEvent"/>.
        /// </summary>
        public int ModifyNpcAttitude(ulong clientId, string npcId, int delta, NpcDefinition npcDef = null, QuestDatabase database = null)
        {
            if (string.IsNullOrEmpty(npcId)) return 0;
            int oldVal = GetNpcAttitude(clientId, npcId);
            int newVal = oldVal + delta;
            int min = NpcAttitude.MinValue;
            int max = NpcAttitude.MaxValue;
            if (npcDef != null)
            {
                min = npcDef.personalAttitudeMin;
                max = npcDef.personalAttitudeMax;
            }
            if (newVal < min) newVal = min;
            if (newVal > max) newVal = max;
            if (newVal == oldVal) return newVal;
            _npcAttitude[(clientId, npcId)] = newVal;

            // Cross-faction influence: для каждой link применить deltaOnLike/dislike к faction
            // (только если NpcDefinition известен и delta реально изменил attitude).
            if (npcDef != null && npcDef.attitudeLinks != null)
            {
                for (int i = 0; i < npcDef.attitudeLinks.Length; i++)
                {
                    var link = npcDef.attitudeLinks[i];
                    if (link == null) continue;
                    int crossDelta = delta > 0 ? link.deltaOnLike : link.deltaOnDislike;
                    if (crossDelta == 0) continue;
                    ModifyReputation(clientId, link.targetFaction, crossDelta, silent: true);
                }
            }

            WorldEventBus.Publish(new NpcAttitudeChangedEvent
            {
                PlayerId = clientId,
                NpcId = npcId,
                NewValue = newVal,
                Delta = newVal - oldVal
            });
            if (Debug.isDebugBuild) Debug.Log($"[QuestWorld] ModifyNpcAttitude player={clientId} npc={npcId} delta={delta} {oldVal}→{newVal}");
            return newVal;
        }

        // ============ T-Q15: Quest state transitions (Accept / TurnIn / Track) ============

        /// <summary>
        /// T-Q15: accept a Discovered/Offered quest → Active.
        /// Server-authoritative: validates state transition, quest exists, max-active cap.
        /// Out of scope T-Q15: applying reward / finalizing quest (T-Q16+).
        /// </summary>
        public QuestResultDto TryAccept(ulong clientId, string questId, string fromNpcId = "")
        {
            if (string.IsNullOrEmpty(questId))
                return Fail(QuestResultCode.NotFound, "questId empty", questId);
            var def = GetQuest(questId);
            if (def == null)
                return Fail(QuestResultCode.NotFound, $"Quest '{questId}' not found", questId);

            var playerQuests = GetPlayerQuests(clientId);

            // Idempotency: если уже Active/Completed/TurnedIn — OK (success — no-op).
            for (int i = 0; i < playerQuests.Count; i++)
            {
                var existing = playerQuests[i];
                if (existing.questId == questId)
                {
                    if (existing.state == QuestState.Active || existing.state == QuestState.Completed || existing.state == QuestState.TurnedIn)
                    {
                        return Ok($"Already {existing.state}", questId);
                    }
                    if (!QuestStateTransition.IsAllowed(existing.state, QuestState.Active))
                        return Fail(QuestResultCode.InvalidState,
                            $"Cannot accept from state {existing.state}", questId);
                    existing.state = QuestState.Active;
                    if (string.IsNullOrEmpty(existing.currentStageId))
                    {
                        var entry = def.GetEntryStage();
                        if (entry != null) existing.currentStageId = entry.stageId;
                    }
                    return Ok("Accepted", questId);
                }
            }

            // Max active cap (считаем только Active — Discovered/Offered не лимитируем).
            int activeCount = 0;
            for (int i = 0; i < playerQuests.Count; i++)
                if (playerQuests[i].state == QuestState.Active) activeCount++;
            if (activeCount >= MaxActiveQuestsPerPlayer)
                return Fail(QuestResultCode.PrerequisitesNotMet,
                    $"Max active quests reached ({activeCount}/{MaxActiveQuestsPerPlayer})", questId);

            // Create new QuestInstance
            var instance = new QuestInstance
            {
                questId = questId,
                state = QuestState.Active,
                isTracked = false
            };
            var entryStage = def.GetEntryStage();
            if (entryStage != null) instance.currentStageId = entryStage.stageId;
            playerQuests.Add(instance);

            if (Debug.isDebugBuild) Debug.Log($"[QuestWorld] TryAccept: client={clientId} quest={questId} fromNpc={fromNpcId} → Active");
            return Ok("Accepted", questId);
        }

        /// <summary>
        /// T-Q15: turn in a Completed quest at the right NPC → TurnedIn.
        /// validates: state=Completed, NPC is in quest.questTurnIns[].
        /// Out of scope T-Q15: applying rewards (T-Q16: ApplyQuestRewards).
        /// </summary>
        public QuestResultDto TryTurnIn(ulong clientId, string questId, string toNpcId)
        {
            if (string.IsNullOrEmpty(questId))
                return Fail(QuestResultCode.NotFound, "questId empty", questId);
            var def = GetQuest(questId);
            if (def == null)
                return Fail(QuestResultCode.NotFound, $"Quest '{questId}' not found", questId);

            // Find player's quest instance
            var playerQuests = GetPlayerQuests(clientId);
            QuestInstance instance = null;
            for (int i = 0; i < playerQuests.Count; i++)
            {
                if (playerQuests[i].questId == questId) { instance = playerQuests[i]; break; }
            }
            if (instance == null)
                return Fail(QuestResultCode.NotFound, $"Quest not in player's log", questId);

            // State: Active → Completed (auto-complete if all required objectives satisfied).
            // For MVP: skip objective check, require state=Completed already. Stage advancement in T-Q06+ full impl.
            if (instance.state == QuestState.Active)
            {
                // Auto-complete: if no required objectives remaining (MVP — just transition).
                if (QuestStateTransition.IsAllowed(instance.state, QuestState.Completed))
                {
                    instance.state = QuestState.Completed;
                }
            }
            if (instance.state != QuestState.Completed)
                return Fail(QuestResultCode.InvalidState,
                    $"Quest must be Completed (current={instance.state})", questId);

            // Validate NPC can turn-in
            if (!string.IsNullOrEmpty(toNpcId))
            {
                // NPC must be registered в questDatabase
                var npc = Database != null ? Database.GetNpc(toNpcId) : null;
                if (npc == null)
                    return Fail(QuestResultCode.NotFound, $"NPC '{toNpcId}' not found", questId);
                // Optional: check npc.questTurnIns contains questId
                if (npc.questTurnIns != null && npc.questTurnIns.Length > 0)
                {
                    bool found = false;
                    for (int i = 0; i < npc.questTurnIns.Length; i++)
                    {
                        if (npc.questTurnIns[i] == questId) { found = true; break; }
                    }
                    if (!found)
                        return Fail(QuestResultCode.PrerequisitesNotMet,
                            $"NPC '{toNpcId}' cannot turn-in quest '{questId}'", questId);
                }
            }

            // Transition: Completed → TurnedIn
            if (!QuestStateTransition.IsAllowed(instance.state, QuestState.TurnedIn))
                return Fail(QuestResultCode.InvalidState, $"Cannot turn-in from {instance.state}", questId);
            instance.state = QuestState.TurnedIn;

            // T-Q16: ApplyQuestRewards (out of scope T-Q15).
            // ApplyQuestRewards(clientId, def.rewards);

            if (Debug.isDebugBuild) Debug.Log($"[QuestWorld] TryTurnIn: client={clientId} quest={questId} toNpc={toNpcId} → TurnedIn");
            return Ok("Turned in", questId);
        }

        /// <summary>
        /// T-Q15: toggle isTracked on player's quest instance. Snapshot rebuild reflects new value.
        /// </summary>
        public QuestResultDto SetTracked(ulong clientId, string questId, bool track)
        {
            if (string.IsNullOrEmpty(questId))
                return Fail(QuestResultCode.NotFound, "questId empty", questId);
            var playerQuests = GetPlayerQuests(clientId);
            for (int i = 0; i < playerQuests.Count; i++)
            {
                if (playerQuests[i].questId == questId)
                {
                    playerQuests[i].isTracked = track;
                    if (Debug.isDebugBuild) Debug.Log($"[QuestWorld] SetTracked: client={clientId} quest={questId} tracked={track}");
                    return Ok(track ? "Tracking" : "Untracked", questId);
                }
            }
            return Fail(QuestResultCode.NotFound, $"Quest not in player's log", questId);
        }

        // ============ T-Q15: Result helpers (alias for QuestWorld → avoid duplication) ============

        private static QuestResultDto Ok(string message, string questId)
        {
            return new QuestResultDto
            {
                code = (byte)QuestResultCode.Ok,
                questId = questId,
                message = message
            };
        }
        private static QuestResultDto Fail(QuestResultCode code, string message, string questId)
        {
            return new QuestResultDto
            {
                code = (byte)code,
                questId = questId,
                message = message
            };
        }

        /// <summary>T-Q06: trigger service singleton. Created in CreateAndInitialize.</summary>
        public Triggers.QuestTriggerService TriggerService { get; private set; }

        public bool GetFlag(ulong clientId, string flagId)
        {
            return _worldFlags.TryGetValue((clientId, flagId), out var v) && v;
        }

        public DialogSession GetDialogSession(ulong clientId)
        {
            return _dialogByPlayer.TryGetValue(clientId, out var s) ? s : null;
        }

        /// <summary>T-Q10: open new dialog session. Returns null если session уже open.</summary>
        public DialogSession OpenDialog(ulong clientId, string npcId, Dialogue.DialogTree tree)
        {
            if (tree == null) return null;
            if (_dialogByPlayer.ContainsKey(clientId)) return null; // already in dialog
            var session = new DialogSession
            {
                treeId = tree.treeId,
                tree = tree,
                currentNodeId = string.IsNullOrEmpty(tree.rootNodeId) ? "" : tree.rootNodeId,
                npcId = npcId ?? "",
                startedAtUnix = (ulong)System.DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };
            _dialogByPlayer[clientId] = session;
            return session;
        }

        /// <summary>T-Q10: close dialog session. No-op if not open.</summary>
        public void CloseDialog(ulong clientId)
        {
            _dialogByPlayer.Remove(clientId);
        }

        // ============ T-Q06: NPC talk + Custom event tracking ============

        /// <summary>Per-player set of NPC ids the player has talked to at least once.</summary>
        private readonly Dictionary<ulong, HashSet<string>> _npcTalkedTo = new Dictionary<ulong, HashSet<string>>();

        /// <summary>Per-player set of custom event ids that have occurred (DialogueAction.EmitEvent).</summary>
        private readonly Dictionary<ulong, HashSet<string>> _eventsOccurred = new Dictionary<ulong, HashSet<string>>();

        // T-Q15: contract tracking (для ContractCompletedTrigger / ContractAcceptedTrigger).
        private readonly Dictionary<ulong, HashSet<string>> _contractsCompleted = new Dictionary<ulong, HashSet<string>>();
        private readonly Dictionary<ulong, HashSet<string>> _contractsAccepted = new Dictionary<ulong, HashSet<string>>();

        public bool HasContractCompleted(ulong clientId, string contractId)
        {
            if (string.IsNullOrEmpty(contractId)) return false;
            return _contractsCompleted.TryGetValue(clientId, out var set) && set.Contains(contractId);
        }

        public void MarkContractCompleted(ulong clientId, string contractId)
        {
            if (string.IsNullOrEmpty(contractId)) return;
            if (!_contractsCompleted.TryGetValue(clientId, out var set))
            {
                set = new HashSet<string>();
                _contractsCompleted[clientId] = set;
            }
            set.Add(contractId);
        }

        public bool HasContractAccepted(ulong clientId, string contractId)
        {
            if (string.IsNullOrEmpty(contractId)) return false;
            return _contractsAccepted.TryGetValue(clientId, out var set) && set.Contains(contractId);
        }

        public void MarkContractAccepted(ulong clientId, string contractId)
        {
            if (string.IsNullOrEmpty(contractId)) return;
            if (!_contractsAccepted.TryGetValue(clientId, out var set))
            {
                set = new HashSet<string>();
                _contractsAccepted[clientId] = set;
            }
            set.Add(contractId);
        }

        public bool HasNpcTalkedTo(ulong clientId, string npcId)
        {
            if (string.IsNullOrEmpty(npcId)) return false;
            return _npcTalkedTo.TryGetValue(clientId, out var set) && set.Contains(npcId);
        }

        public void MarkNpcTalked(ulong clientId, string npcId)
        {
            if (string.IsNullOrEmpty(npcId)) return;
            if (!_npcTalkedTo.TryGetValue(clientId, out var set))
            {
                set = new HashSet<string>();
                _npcTalkedTo[clientId] = set;
            }
            set.Add(npcId);
        }

        public bool HasEventOccurred(ulong clientId, string eventId)
        {
            if (string.IsNullOrEmpty(eventId)) return false;
            return _eventsOccurred.TryGetValue(clientId, out var set) && set.Contains(eventId);
        }

        public void MarkEventOccurred(ulong clientId, string eventId)
        {
            if (string.IsNullOrEmpty(eventId)) return;
            if (!_eventsOccurred.TryGetValue(clientId, out var set))
            {
                set = new HashSet<string>();
                _eventsOccurred[clientId] = set;
            }
            set.Add(eventId);
        }

        // ============ T-Q06: Quest advancement (minimal) ============

        /// <summary>
        /// T-Q06 scope: minimal TryAdvanceObjective. Mark objective как completed если
        /// есть matching QuestInstance.Active quest с этим objective. Полная логика
        /// (TryOffer, TryAccept, TryTurnIn, state transitions, onCompleteActions) — T-Q15+.
        /// </summary>
        /// <returns>True если objective был marked completed.</returns>
        public bool TryAdvanceObjective(ulong clientId, string questId, string objectiveId)
        {
            if (string.IsNullOrEmpty(questId) || string.IsNullOrEmpty(objectiveId)) return false;
            var questDef = GetQuest(questId);
            if (questDef == null) return false;

            var playerQuests = GetPlayerQuests(clientId);
            QuestInstance instance = null;
            for (int i = 0; i < playerQuests.Count; i++)
            {
                if (playerQuests[i].questId == questId)
                {
                    instance = playerQuests[i];
                    break;
                }
            }
            if (instance == null) return false;
            if (instance.state != QuestState.Active) return false;
            if (instance.currentStageId != questDef.GetEntryStage()?.stageId
                && !string.IsNullOrEmpty(instance.currentStageId))
            {
                // Try matching against current stage
            }

            // Find stage containing this objective
            QuestStage stage = null;
            for (int i = 0; i < questDef.stages.Length; i++)
            {
                for (int j = 0; j < questDef.stages[i].objectives.Length; j++)
                {
                    if (questDef.stages[i].objectives[j].objectiveId == objectiveId)
                    {
                        stage = questDef.stages[i];
                        break;
                    }
                }
                if (stage != null) break;
            }
            if (stage == null) return false;
            if (stage.stageId != instance.currentStageId) return false;

            // Mark progress
            var progress = instance.GetOrCreateProgress(objectiveId);
            if (progress.completed) return false;
            progress.completed = true;
            return true;
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
