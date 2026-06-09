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

        /// <summary>T-Q18: persistence repository (optional, nullable). Set by QuestServer.OnNetworkSpawn via SetRepository.</summary>
        public ProjectC.Quests.Persistence.IQuestStateRepository Repository { get; private set; }

        /// <summary>T-Q18: assign persistence backend. Pass null для disable persistence (test mode).</summary>
        public void SetRepository(ProjectC.Quests.Persistence.IQuestStateRepository repo)
        {
            Repository = repo;
            if (Debug.isDebugBuild) Debug.Log($"[QuestWorld] Repository set: {(repo == null ? "null (disabled)" : repo.GetType().Name)}");
        }

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
            /// <summary>T-Q21: filtered visible edges (hideIfUnavailable).</summary>
            public System.Collections.Generic.List<ProjectC.Dialogue.DialogueEdge> visibleEdges;
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
            SavePlayer(clientId); // T-Q18
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
            SavePlayer(clientId); // T-Q18
            return newVal;
        }

        // ============ T-Q15: Quest state transitions (Accept / TurnIn / Track) ============

        /// <summary>
        /// T-Q15 fix: QuestServer.FireDialogAction.OfferQuest stub → real impl.
        /// Server-side offer quest to player (Discovered state). Если уже есть в log (любой state) — return Ok (idempotent).
        /// Edge action: action.stringParam = questId. НЕ auto-accept.
        /// </summary>
        public QuestResultDto TryOffer(ulong clientId, string questId)
        {
            if (string.IsNullOrEmpty(questId))
                return Fail(QuestResultCode.NotFound, "questId empty", questId);
            var def = GetQuest(questId);
            if (def == null)
                return Fail(QuestResultCode.NotFound, $"Quest '{questId}' not found", questId);

            var playerQuests = GetPlayerQuests(clientId);

            // Idempotency: если уже в log — success (no-op).
            for (int i = 0; i < playerQuests.Count; i++)
            {
                if (playerQuests[i].questId == questId)
                {
                    return Ok($"Already in log (state={playerQuests[i].state})", questId);
                }
            }

            // Create new QuestInstance in Discovered state.
            var instance = new QuestInstance
            {
                questId = questId,
                state = QuestState.Discovered,
                isTracked = false
            };
            playerQuests.Add(instance);

            if (Debug.isDebugBuild) Debug.Log($"[QuestWorld] TryOffer: client={clientId} quest={questId} → Discovered");

            // T-Q18: persist on every state change (per §H, no debounce).
            SavePlayer(clientId);

            return new QuestResultDto
            {
                code = (byte)QuestResultCode.Discovered, // signal "newly discovered" to UI
                questId = questId,
                message = "Discovered"
            };
        }

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
                        // T-Q22 fix: fire onEnterActions of entry stage on accept.
                        if (entry != null && entry.onEnterActions != null && entry.onEnterActions.Length > 0)
                        {
                            OnFireDialogActions?.Invoke(clientId, fromNpcId ?? "", entry.onEnterActions);
                        }
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

            // T-Q22 fix: fire onEnterActions of entry stage on accept.
            if (entryStage != null && entryStage.onEnterActions != null && entryStage.onEnterActions.Length > 0)
            {
                OnFireDialogActions?.Invoke(clientId, fromNpcId ?? "", entryStage.onEnterActions);
            }

            if (Debug.isDebugBuild) Debug.Log($"[QuestWorld] TryAccept: client={clientId} quest={questId} fromNpc={fromNpcId} → Active");
            SavePlayer(clientId); // T-Q18
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
            // T-Q22 fix: вместо прямого state=Completed (который пропускал fire onCompleteActions)
            // — вызываем TryAdvanceStage. Он сам проверит AreAllRequiredComplete + fire onCompleteActions
            // + если nextStageId пуст → state=Completed + ApplyQuestRewards. Также для non-final stages —
            // переведёт в следующий stage без state=Completed.
            if (instance.state == QuestState.Active)
            {
                var def2 = GetQuest(questId);
                if (def2 != null && !string.IsNullOrEmpty(instance.currentStageId))
                {
                    var curStage = def2.GetStage(instance.currentStageId);
                    if (curStage != null)
                    {
                        TryAdvanceStage(clientId, instance, def2, curStage);
                    }
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

            // T-Q18: ApplyQuestRewards (real impl) — credits + items + reputation + npcAttitude + flags.
            // def.rewards is a QuestReward (T-Q04).
            if (def.rewards != null)
            {
                ApplyQuestRewards(clientId, def.rewards, toNpcId);
            }

            if (Debug.isDebugBuild) Debug.Log($"[QuestWorld] TryTurnIn: client={clientId} quest={questId} toNpc={toNpcId} → TurnedIn");
            SavePlayer(clientId); // T-Q18
            return Ok("Turned in", questId);
        }

        /// <summary>
        /// T-Q18: apply QuestReward к player. Items → InventoryServer.AddItemDirect (ItemType.Resources, int.Parse tradeItemId).
        /// Cargo items — out of scope T-Q18 (no active ship tracking). Reputation → ModifyReputation. NPC attitude → ModifyNpcAttitude.
        /// Unlocks (DialogTree/Zone) → log only (T-Q19 cleanup will full impl).
        /// </summary>
        private void ApplyQuestRewards(ulong clientId, QuestReward reward, string turnInNpcId)
        {
            if (reward == null) return;

            // 1) Credits: repository SetCredits.
            if (reward.credits != 0)
            {
                var repo = ProjectC.Trade.Core.TradeWorld.Instance != null ? ProjectC.Trade.Core.TradeWorld.Instance.Repository : null;
                if (repo != null)
                {
                    float currentCredits = repo.GetCredits(clientId);
                    float newCredits = Mathf.Max(0f, currentCredits + reward.credits);
                    repo.SetCredits(clientId, newCredits);
                    if (Debug.isDebugBuild) Debug.Log($"[QuestWorld] ApplyQuestRewards: credits {currentCredits:F0} → {newCredits:F0} (+{reward.credits})");
                }
                else
                {
                    Debug.LogWarning("[QuestWorld] ApplyQuestRewards: TradeWorld.Repository == null, credits skipped");
                }
            }

            // 2) Items: AddItemDirect via InventoryWorld.
            if (reward.items != null)
            {
                for (int i = 0; i < reward.items.Length; i++)
                {
                    var ri = reward.items[i];
                    if (ri == null || string.IsNullOrEmpty(ri.tradeItemId) || ri.count <= 0) continue;
                    if (!int.TryParse(ri.tradeItemId, out int legacyIntId))
                    {
                        Debug.LogWarning($"[QuestWorld] ApplyQuestRewards: items[{i}] tradeItemId='{ri.tradeItemId}' не конвертируется в int (T-Q19 cleanup: TradeItemDefinition legacy mapping)");
                        continue;
                    }
                    var inv = ProjectC.Items.InventoryWorld.Instance;
                    if (inv == null)
                    {
                        Debug.LogWarning($"[QuestWorld] ApplyQuestRewards: InventoryWorld == null, items[{i}] skipped");
                        break;
                    }
                    var result = inv.AddItemDirect(clientId, legacyIntId, ProjectC.Items.ItemType.Resources);
                    if (Debug.isDebugBuild) Debug.Log($"[QuestWorld] ApplyQuestRewards: items[{i}] id={legacyIntId} x{ri.count} → code={result.code} message={result.message}");
                }
            }

            // 3) Cargo items: out of scope T-Q18 (need active ship tracking, T-Q17+ or T-Q22).
            if (reward.cargoItems != null && reward.cargoItems.Length > 0)
            {
                Debug.LogWarning($"[QuestWorld] ApplyQuestRewards: cargoItems ({reward.cargoItems.Length}) skipped — T-Q18 out of scope (active ship tracking TBD)");
            }

            // 4) Reputation deltas.
            if (reward.reputation != null)
            {
                for (int i = 0; i < reward.reputation.Length; i++)
                {
                    var rr = reward.reputation[i];
                    if (rr == null || rr.faction == FactionId.None) continue;
                    int newVal = ModifyReputation(clientId, rr.faction, rr.value, silent: true);
                    if (Debug.isDebugBuild) Debug.Log($"[QuestWorld] ApplyQuestRewards: reputation faction={rr.faction} delta={rr.value} → {newVal}");
                }
            }

            // 5) Unlocks: log only. Real impl в T-Q19 (dialog tree unlock, zone unlock, etc).
            if (reward.unlocks != null && reward.unlocks.Length > 0)
            {
                for (int i = 0; i < reward.unlocks.Length; i++)
                {
                    var ul = reward.unlocks[i];
                    if (ul == null) continue;
                    Debug.Log($"[QuestWorld] ApplyQuestRewards: unlock type={ul.unlockType} id='{ul.unlockId}' (T-Q19+ impl)");
                }
            }
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
                    SavePlayer(clientId); // T-Q18
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
            if (set.Add(contractId)) SavePlayer(clientId); // T-Q18
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
            if (set.Add(contractId)) SavePlayer(clientId); // T-Q18
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
            if (set.Add(npcId)) SavePlayer(clientId); // T-Q18
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
            if (set.Add(eventId)) SavePlayer(clientId); // T-Q18
        }

        // ============ T-Q06: Quest advancement (minimal) ============

        // ============ T-Q20: Real-time tick + stage transitions ============

        /// <summary>T-Q20: tick interval (seconds). QuestServer.Update accumulates Time.deltaTime.</summary>
        public float TickInterval = 5f;

        /// <summary>T-Q20: accumulator used by QuestServer-driven ticks.</summary>
        public float TickAccumulator = 0f;

        /// <summary>T-Q20: QuestWorld raises this when stage actions must fire (e.g. onCompleteActions,
        /// onEnterActions). QuestServer subscribes and routes to FireDialogAction.
        /// Args: (clientId, npcIdHint, actions[]). npcIdHint может быть пуст (stage transition не привязан к NPC).</summary>
        public event System.Action<ulong, string, ProjectC.Dialogue.DialogueAction[]> OnFireDialogActions;

        /// <summary>T-Q20: QuestWorld raises this когда stage transitioned. QuestServer
        /// подписывается → SendQuestSnapshotToClient + SavePlayer.
        /// Args: (clientId, questId, fromStageId, toStageId).</summary>
        public event System.Action<ulong, string, string, string> OnStageTransition;

        /// <summary>T-Q20: position lookup. Server подменяет этот делегат на NetworkPlayer.transform.position lookup.
        /// Args: clientId, returns Vector3 или Vector3.zero если не найден.</summary>
        public System.Func<ulong, Vector3> PlayerPositionProvider;

        /// <summary>T-Q20: resolve item id from objective.itemTradeItemId. Tries:
        /// 1) int.TryParse (direct id) — for legacy configs.
        /// 2) ItemData.itemName lookup в Resources/Items/ — for M13 test convenience.
        /// Returns 0 если ничего не найдено.
        /// </summary>
        public static int ResolveItemId(string itemTradeItemId)
        {
            if (string.IsNullOrEmpty(itemTradeItemId)) return 0;
            // 1. Direct int.
            if (int.TryParse(itemTradeItemId, out int direct) && direct > 0) return direct;
            // 2. Name lookup через Resources/Items/ ItemData.
            var allItems = Resources.LoadAll<ProjectC.Items.ItemData>("Items");
            if (allItems == null) return 0;
            for (int i = 0; i < allItems.Length; i++)
            {
                if (allItems[i] == null) continue;
                if (allItems[i].itemName == itemTradeItemId) return i + 1;  // registration order = id
            }
            return 0;
        }

        /// <summary>
        /// T-Q20: server-driven tick. Accumulate Time.deltaTime, evaluate all active
        /// quests for all players when interval reached.
        /// </summary>
        public void TickAll(float dt)
        {
            if (dt < 0f) return;
            TickAccumulator += dt;
            if (TickAccumulator < TickInterval) return;
            TickAccumulator = 0f;

            // Snapshot keys (нельзя менять dictionary во время итерации)
            if (_questsByPlayer == null || _questsByPlayer.Count == 0) return;
            var playerIds = new List<ulong>(_questsByPlayer.Keys);
            for (int i = 0; i < playerIds.Count; i++)
            {
                TickPlayer(playerIds[i]);
            }
        }

        /// <summary>
        /// T-Q20: tick one player. Walks all their Active quests, evaluates current stage.
        /// </summary>
        public void TickPlayer(ulong clientId)
        {
            if (!_questsByPlayer.TryGetValue(clientId, out var quests) || quests == null) return;

            // Snapshot quest list (TryAdvanceStage может менять state, но не add/remove)
            for (int i = 0; i < quests.Count; i++)
            {
                var inst = quests[i];
                if (inst == null) continue;
                if (inst.state != QuestState.Active) continue;
                if (string.IsNullOrEmpty(inst.currentStageId)) continue;

                var def = GetQuest(inst.questId);
                if (def == null) continue;

                var stage = def.GetStage(inst.currentStageId);
                if (stage == null) continue;

                EvaluateAndAdvanceStage(clientId, inst, def, stage);
            }
        }

        /// <summary>
        /// T-Q20: evaluate all required objectives в current stage. Если все satisfied
        /// → TryAdvanceStage. Returns true если что-то изменилось.
        /// </summary>
        private bool EvaluateAndAdvanceStage(ulong clientId, QuestInstance instance, QuestDefinition def, QuestStage stage)
        {
            if (stage.objectives == null || stage.objectives.Length == 0)
            {
                // No objectives → auto-advance immediately (degenerate stage)
                return TryAdvanceStage(clientId, instance, def, stage);
            }

            bool changed = false;
            for (int i = 0; i < stage.objectives.Length; i++)
            {
                var obj = stage.objectives[i];
                if (obj == null) continue;
                if (!obj.required || obj.optional) continue;

                var progress = instance.GetOrCreateProgress(obj.objectiveId);
                int prevCount = progress.currentCount;
                bool prevCompleted = progress.completed;
                if (progress.completed) continue;

                if (EvaluateObjective(clientId, instance, obj, progress))
                {
                    progress.completed = true;
                    if (Debug.isDebugBuild) Debug.Log($"[QuestWorld] Objective completed: quest={def.questId} stage={stage.stageId} obj={obj.objectiveId}");
                    changed = true;
                }
                else if (progress.currentCount != prevCount || progress.completed != prevCompleted)
                {
                    // T-Q21: progress изменился (e.g. picked up item, qty 0→1) — push snapshot чтобы UI обновил (1/3).
                    changed = true;
                }
            }

            // If all required objectives now complete → advance.
            if (instance.AreAllRequiredComplete(stage))
            {
                if (TryAdvanceStage(clientId, instance, def, stage))
                {
                    changed = true;
                }
            }

            if (changed) {
                SavePlayer(clientId);
                // T-Q21: push snapshot на client чтобы UI обновил счётчик objective (1/3, 2/3 ...).
                if (QuestServer.Instance != null) {
                    var method = typeof(QuestServer).GetMethod("SendQuestSnapshotToClient",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                        null, new System.Type[] { typeof(ulong) }, null);
                    if (method != null) method.Invoke(QuestServer.Instance, new object[] { clientId });
                }
            }
            return changed;
        }

        /// <summary>
        /// T-Q20: evaluate single objective (event-driven + tick-friendly hybrid).
        /// Updates progress.currentCount (for HaveItem, ReputationAtLeast).
        /// </summary>
        private bool EvaluateObjective(ulong clientId, QuestInstance instance, QuestObjective obj, QuestInstance.ObjectiveProgress progress)
        {
            if (obj == null) return false;
            switch (obj.objectiveType)
            {
                case QuestObjectiveType.TalkToNpc:
                    return HasNpcTalkedTo(clientId, obj.targetNpcId);

                case QuestObjectiveType.HaveItem:
                {
                    int itemId = ResolveItemId(obj.itemTradeItemId);
                    if (itemId <= 0) return false;
                    int count = ProjectC.Items.InventoryWorld.Instance != null
                        ? ProjectC.Items.InventoryWorld.Instance.CountOf(clientId, itemId)
                        : 0;
                    progress.currentCount = count;
                    return count >= obj.requiredQuantity;
                }

                case QuestObjectiveType.DeliverItem:
                {
                    // Same as HaveItem for MVP (turn-in handled в TryTurnIn).
                    int itemId = ResolveItemId(obj.itemTradeItemId);
                    if (itemId <= 0) return false;
                    int count = ProjectC.Items.InventoryWorld.Instance != null
                        ? ProjectC.Items.InventoryWorld.Instance.CountOf(clientId, itemId)
                        : 0;
                    progress.currentCount = count;
                    return count >= obj.requiredQuantity;
                }

                case QuestObjectiveType.ReachLocation:
                {
                    if (PlayerPositionProvider == null) return false;
                    var playerPos = PlayerPositionProvider(clientId);
                    if (playerPos == Vector3.zero) return false; // not on a real map
                    float dist = Vector3.Distance(playerPos, obj.targetPosition);
                    progress.currentCount = obj.targetRadius > 0f ? (int)(dist * 100f / obj.targetRadius) : 0;
                    return dist <= obj.targetRadius;
                }

                case QuestObjectiveType.ReputationAtLeast:
                {
                    int val = GetReputation(clientId, obj.targetFaction);
                    progress.currentCount = val;
                    return val >= obj.reputationValue;
                }

                case QuestObjectiveType.WaitForEvent:
                case QuestObjectiveType.EventDriven:
                    return HasEventOccurred(clientId, obj.eventId);

                case QuestObjectiveType.KillEntity:
                    // STUB: combat system не реализован. Always false.
                    if (Debug.isDebugBuild && Time.frameCount % 600 == 0)
                        Debug.Log($"[QuestWorld] Objective {obj.objectiveType} ({obj.objectiveId}) is STUB — always unsatisfied");
                    return false;

                default:
                    return false;
            }
        }

        /// <summary>
        /// T-Q20 + T-Q22: advance current stage to next. Fire onCompleteActions →
        /// transition currentStageId → nextStageId (or Completed) → fire onEnterActions.
        /// </summary>
        /// <returns>True if a transition happened.</returns>
        private bool TryAdvanceStage(ulong clientId, QuestInstance instance, QuestDefinition def, QuestStage currentStage)
        {
            if (instance == null || def == null || currentStage == null) return false;

            string fromStage = currentStage.stageId;
            string toStage = currentStage.nextStageId;

            // 1. Fire onCompleteActions of CURRENT stage (before transition).
            if (currentStage.onCompleteActions != null && currentStage.onCompleteActions.Length > 0)
            {
                OnFireDialogActions?.Invoke(clientId, "", currentStage.onCompleteActions);
                // Also: if final stage — apply def.rewards
                if (string.IsNullOrEmpty(toStage) && def.rewards != null)
                {
                    if (Debug.isDebugBuild) Debug.Log($"[QuestWorld] Stage advanced to END: {def.questId} {fromStage} → (final) → rewards");
                    ApplyQuestRewards(clientId, def.rewards, "");
                }
            }

            // 2. Transition.
            if (string.IsNullOrEmpty(toStage))
            {
                // Final stage: Active → Completed
                if (QuestStateTransition.IsAllowed(instance.state, QuestState.Completed))
                {
                    instance.state = QuestState.Completed;
                    instance.completedAtUnix = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    if (Debug.isDebugBuild) Debug.Log($"[QuestWorld] State transitioned: {def.questId} {fromStage} → Completed");
                }
                OnStageTransition?.Invoke(clientId, def.questId, fromStage, "");
                return true;
            }
            else
            {
                instance.currentStageId = toStage;
                if (Debug.isDebugBuild) Debug.Log($"[QuestWorld] Stage advanced: {def.questId} {fromStage} → {toStage}");
                OnStageTransition?.Invoke(clientId, def.questId, fromStage, toStage);

                // 3. Fire onEnterActions of NEW stage.
                var nextStage = def.GetStage(toStage);
                if (nextStage != null && nextStage.onEnterActions != null && nextStage.onEnterActions.Length > 0)
                {
                    OnFireDialogActions?.Invoke(clientId, "", nextStage.onEnterActions);
                }
                return true;
            }
        }

        // ============ T-Q06: TryAdvanceObjective (kept) ============

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

        // ============ T-Q18: Persistence ============

        /// <summary>
        /// T-Q18: build complete QuestSaveData для clientId. Aggregates quests +
        /// reputation + npcAttitude + sets (events/contracts/npcTalkedTo/worldFlags).
        /// Returns null if no data (не должен — всегда хотя бы empty lists).
        /// </summary>
        public ProjectC.Quests.Persistence.QuestSaveData BuildSaveData(ulong clientId)
        {
            var data = new ProjectC.Quests.Persistence.QuestSaveData();
            data.version = 1;

            // Quests
            if (_questsByPlayer.TryGetValue(clientId, out var quests))
            {
                for (int i = 0; i < quests.Count; i++)
                {
                    var q = quests[i];
                    var entry = new ProjectC.Quests.Persistence.QuestSaveEntry
                    {
                        questId = q.questId,
                        state = (byte)q.state,
                        currentStageId = q.currentStageId ?? "",
                        acceptedAtUnix = q.acceptedAtUnix,
                        completedAtUnix = q.completedAtUnix,
                        isTracked = q.isTracked
                    };
                    if (q.objectiveProgress != null)
                    {
                        for (int j = 0; j < q.objectiveProgress.Count; j++)
                        {
                            var op = q.objectiveProgress[j];
                            entry.objectiveProgress.Add(new ProjectC.Quests.Persistence.ObjectiveSaveEntry
                            {
                                objectiveId = op.objectiveId,
                                currentCount = op.currentCount,
                                completed = op.completed
                            });
                        }
                    }
                    data.quests.Add(entry);
                }
            }

            // Reputation: scan all keys with this clientId
            foreach (var kv in _reputation)
            {
                if (kv.Key.Item1 != clientId) continue;
                data.reputation.Add(new ProjectC.Quests.Persistence.FactionRepSaveEntry
                {
                    factionId = (int)kv.Key.Item2,
                    value = kv.Value
                });
            }

            // NPC attitude
            foreach (var kv in _npcAttitude)
            {
                if (kv.Key.Item1 != clientId) continue;
                data.npcAttitude.Add(new ProjectC.Quests.Persistence.NpcAttitudeSaveEntry
                {
                    npcId = kv.Key.Item2,
                    value = kv.Value
                });
            }

            // String sets
            AddStringSetIfPresent(data, clientId, "eventsOccurred", _eventsOccurred);
            AddStringSetIfPresent(data, clientId, "contractsCompleted", _contractsCompleted);
            AddStringSetIfPresent(data, clientId, "contractsAccepted", _contractsAccepted);
            AddStringSetIfPresent(data, clientId, "npcTalkedTo", _npcTalkedTo);

            // worldFlags: Dictionary<(ulong, string), bool> → key.Item1=clientId, key.Item2=flagName, value=bool
            // Only persist true flags (false = default).
            var flagsEntry = new ProjectC.Quests.Persistence.StringSetSaveEntry { setName = "worldFlags" };
            foreach (var kv in _worldFlags)
            {
                if (kv.Key.Item1 != clientId) continue;
                if (kv.Value) flagsEntry.values.Add(kv.Key.Item2);
            }
            if (flagsEntry.values.Count > 0) data.stringSets.Add(flagsEntry);

            return data;
        }

        private static void AddStringSetIfPresent(ProjectC.Quests.Persistence.QuestSaveData data, ulong clientId, string setName,
            Dictionary<ulong, HashSet<string>> source)
        {
            if (!source.TryGetValue(clientId, out var set) || set == null || set.Count == 0) return;
            var entry = new ProjectC.Quests.Persistence.StringSetSaveEntry { setName = setName };
            entry.values.AddRange(set);
            data.stringSets.Add(entry);
        }

        /// <summary>T-Q18: save player state via Repository. No-op если Repository == null.</summary>
        public void SavePlayer(ulong clientId)
        {
            if (Repository == null) return;
            var data = BuildSaveData(clientId);
            Repository.Save(clientId, data);
        }

        /// <summary>T-Q18: load player state from Repository → populate dictionaries. Returns true если loaded.</summary>
        public bool LoadPlayer(ulong clientId)
        {
            if (Repository == null) return false;
            var data = Repository.Load(clientId);
            if (data == null) return false;

            // Wipe existing state for this client first (avoid stale data from prior in-memory session)
            _questsByPlayer.Remove(clientId);
            // Don't wipe reputation/attitude — caller decides if they want fresh start.
            // For simplicity, also wipe:
            var repToRemove = new List<(ulong, FactionId)>();
            foreach (var kv in _reputation) if (kv.Key.Item1 == clientId) repToRemove.Add(kv.Key);
            foreach (var k in repToRemove) _reputation.Remove(k);
            var attToRemove = new List<(ulong, string)>();
            foreach (var kv in _npcAttitude) if (kv.Key.Item1 == clientId) attToRemove.Add(kv.Key);
            foreach (var k in attToRemove) _npcAttitude.Remove(k);
            _eventsOccurred.Remove(clientId);
            _contractsCompleted.Remove(clientId);
            _contractsAccepted.Remove(clientId);
            _npcTalkedTo.Remove(clientId);
            var flagToRemove = new List<(ulong, string)>();
            foreach (var kv in _worldFlags) if (kv.Key.Item1 == clientId) flagToRemove.Add(kv.Key);
            foreach (var k in flagToRemove) _worldFlags.Remove(k);

            // Apply quests
            if (data.quests != null)
            {
                var list = new List<QuestInstance>();
                for (int i = 0; i < data.quests.Count; i++)
                {
                    var e = data.quests[i];
                    var inst = new QuestInstance
                    {
                        questId = e.questId,
                        state = (QuestState)e.state,
                        currentStageId = e.currentStageId ?? "",
                        acceptedAtUnix = e.acceptedAtUnix,
                        completedAtUnix = e.completedAtUnix,
                        isTracked = e.isTracked
                    };
                    if (e.objectiveProgress != null)
                    {
                        for (int j = 0; j < e.objectiveProgress.Count; j++)
                        {
                            var op = e.objectiveProgress[j];
                            inst.objectiveProgress.Add(new QuestInstance.ObjectiveProgress
                            {
                                objectiveId = op.objectiveId,
                                currentCount = op.currentCount,
                                completed = op.completed
                            });
                        }
                    }
                    list.Add(inst);
                }
                if (list.Count > 0) _questsByPlayer[clientId] = list;
            }

            // Apply reputation
            if (data.reputation != null)
            {
                for (int i = 0; i < data.reputation.Count; i++)
                {
                    var e = data.reputation[i];
                    _reputation[(clientId, (FactionId)e.factionId)] = e.value;
                }
            }

            // Apply npcAttitude
            if (data.npcAttitude != null)
            {
                for (int i = 0; i < data.npcAttitude.Count; i++)
                {
                    var e = data.npcAttitude[i];
                    if (string.IsNullOrEmpty(e.npcId)) continue;
                    _npcAttitude[(clientId, e.npcId)] = e.value;
                }
            }

            // Apply string sets
            if (data.stringSets != null)
            {
                for (int i = 0; i < data.stringSets.Count; i++)
                {
                    var e = data.stringSets[i];
                    if (e.values == null || e.values.Count == 0) continue;
                    switch (e.setName)
                    {
                        case "eventsOccurred": _eventsOccurred[clientId] = new HashSet<string>(e.values); break;
                        case "contractsCompleted": _contractsCompleted[clientId] = new HashSet<string>(e.values); break;
                        case "contractsAccepted": _contractsAccepted[clientId] = new HashSet<string>(e.values); break;
                        case "npcTalkedTo": _npcTalkedTo[clientId] = new HashSet<string>(e.values); break;
                        case "worldFlags":
                            for (int j = 0; j < e.values.Count; j++) _worldFlags[(clientId, e.values[j])] = true;
                            break;
                    }
                }
            }

            if (Debug.isDebugBuild) Debug.Log($"[QuestWorld] LoadPlayer: client={clientId} restored {data.quests?.Count ?? 0} quests, {data.reputation?.Count ?? 0} factions, {data.npcAttitude?.Count ?? 0} npcAttitudes");
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
            // T-Q18: flush all players
            if (Repository != null)
            {
                foreach (var clientId in _questsByPlayer.Keys)
                {
                    SavePlayer(clientId);
                }
            }
            _questById.Clear();
            _questsByPlayer.Clear();
            _reputation.Clear();
            _npcAttitude.Clear();
            _worldFlags.Clear();
            _dialogByPlayer.Clear();
        }
    }
}
