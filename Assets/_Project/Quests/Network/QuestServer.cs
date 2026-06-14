// T-Q05: QuestServer — server-side NetworkBehaviour hub for NPC+Quest+Reputation+Dialogue.
// См. docs/NPC_quests/02_V2_ARCHITECTURE.md §2.4, contract: ContractServer.cs (reused pattern).
//
// T-Q05 scope:
//   - NetworkBehaviour skeleton (Instance singleton, OnNetworkSpawn/Despawn).
//   - QuestDatabase SO ref (T-Q09 fills it, T-Q05 just consumes the array).
//   - All RPCs declared (RequestTalkToNpc, RequestAdvanceDialogue, RequestAcceptQuest,
//     RequestTurnInQuest, RequestTrackQuest, RequestRefreshQuests, RequestRefreshReputation,
//     RequestRefreshNpcAttitude, RequestDiscoverQuest). Stub logic — real impl in T-Q06+.
//   - Rate limiting per-client (copy-paste from ContractServer).
//   - Place in BootstrapScene.unity via MCP; ScenePlacedObjectSpawner wires NetworkObject.
//
// T-Q06+: fill in actual QuestWorld calls (TryOffer, ModifyReputation, etc.),
//          DTO serialization, client senders.
// T-Q07+: add SendTo.Owner RPCs (DONE on NetworkPlayer side via ReceiveXxxTargetRpc).

using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using ProjectC.Core;
using ProjectC.Quests.Dto;
using ProjectC.Dialogue;
using ProjectC.Items;
using NetworkPlayer = ProjectC.Player.NetworkPlayer;

namespace ProjectC.Quests
{
    /// <summary>
    /// Server hub for NPC + Quest + Reputation + Dialogue. Singleton, DontDestroyOnLoad
    /// (через NetworkManagerController.Awake bootstrap).
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class QuestServer : NetworkBehaviour
    {
        public static QuestServer Instance { get; private set; }

        [Header("Quest Database")]
        [Tooltip("T-Q09: QuestDatabase SO с auto-discovery. Содержит factions, npcs, dialog trees, quests.")]
        [SerializeField] private QuestDatabase questDatabase;

        [Header("Behavior")]
        [Tooltip("Макс активных квестов на игрока (для cap на Discover/Offer).")]
        [SerializeField] private int maxActiveQuestsPerPlayer = 20;

        [Tooltip("Debug mode: verbose logging в Console.")]
        [SerializeField] private bool debugMode = true;

        [Header("Rate Limiting")]
        [Tooltip("Макс операций в минуту на клиента (0 = без лимита).")]
        [SerializeField] private int maxOpsPerMinute = 30;

        // Per-client rate limiting
        private readonly Dictionary<ulong, List<float>> _opTimestamps = new Dictionary<ulong, List<float>>();

        // ============================================================
        // LIFECYCLE
        // ============================================================

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (Instance == null) Instance = this;

            if (!IsServer)
            {
                // Клиент: server-only functionality disabled.
                enabled = false;
                return;
            }

            // Init QuestWorld
            // T-Q09: pass QuestDatabase.quests[] to QuestWorld (QuestDefinition[]).
            if (questDatabase == null)
            {
                Debug.LogError("[QuestServer] questDatabase is not assigned! Auto-discovery not yet completed or asset missing.");
                return;
            }
            QuestWorld.CreateAndInitialize(questDatabase.quests, questDatabase, maxActiveQuestsPerPlayer);

            // T-Q18: assign JSON repository (load on client connect, save on every state change).
            QuestWorld.Instance.SetRepository(new ProjectC.Quests.Persistence.JsonQuestStateRepository());

            // T-Q06: subscribe to WorldEventBus → route to QuestTriggerService.Evaluate().
            _handleItemAdded = OnItemAdded;
            _handleItemRemoved = OnItemRemoved;
            _handleReputationChanged = OnReputationChanged;
            _handleNpcAttitudeChanged = OnNpcAttitudeChanged;
            _handleCustomEvent = OnCustomEvent;
            _handleDialogVisited = OnDialogVisited;
            _handleDayNightChanged = OnDayNightChanged;
            WorldEventBus.Subscribe(_handleItemAdded);
            WorldEventBus.Subscribe(_handleItemRemoved);
            WorldEventBus.Subscribe(_handleReputationChanged);
            WorldEventBus.Subscribe(_handleNpcAttitudeChanged);
            WorldEventBus.Subscribe(_handleCustomEvent);
            WorldEventBus.Subscribe(_handleDialogVisited);
            WorldEventBus.Subscribe(_handleDayNightChanged);

            // T-Q20: subscribe to QuestWorld events (stage transitions + actions).
            QuestWorld.Instance.OnFireDialogActions += OnWorldFireDialogActions;
            QuestWorld.Instance.OnStageTransition += OnWorldStageTransition;
            QuestWorld.Instance.PlayerPositionProvider = GetPlayerPosition;

            // T-Q13: при коннекте нового клиента — push initial reputation + npcAttitude snapshot.
            _handleClientConnected = OnClientConnectedForSnapshot;
            NetworkManager.Singleton.OnClientConnectedCallback += _handleClientConnected;

            if (debugMode)
            {
                Debug.Log($"[QuestServer] OnNetworkSpawn — IsServer=true, questDatabase={questDatabase.quests?.Length ?? 0} quests, maxActive={maxActiveQuestsPerPlayer}, maxOps/min={maxOpsPerMinute}, triggerSubs=7, tickInterval={QuestWorld.Instance.TickInterval}s");
            }
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            if (IsServer)
            {
                // T-Q06: unsubscribe from bus
                if (_handleItemAdded != null) WorldEventBus.Unsubscribe(_handleItemAdded);
                if (_handleItemRemoved != null) WorldEventBus.Unsubscribe(_handleItemRemoved);
                if (_handleReputationChanged != null) WorldEventBus.Unsubscribe(_handleReputationChanged);
                if (_handleNpcAttitudeChanged != null) WorldEventBus.Unsubscribe(_handleNpcAttitudeChanged);
                if (_handleCustomEvent != null) WorldEventBus.Unsubscribe(_handleCustomEvent);
                if (_handleDialogVisited != null) WorldEventBus.Unsubscribe(_handleDialogVisited);
                if (_handleDayNightChanged != null) WorldEventBus.Unsubscribe(_handleDayNightChanged);

                // T-Q13: unsubscribe OnClientConnected
                if (_handleClientConnected != null && NetworkManager.Singleton != null)
                {
                    NetworkManager.Singleton.OnClientConnectedCallback -= _handleClientConnected;
                    _handleClientConnected = null;
                }

                // T-Q20: unsubscribe QuestWorld events
                if (QuestWorld.Instance != null)
                {
                    QuestWorld.Instance.OnFireDialogActions -= OnWorldFireDialogActions;
                    QuestWorld.Instance.OnStageTransition -= OnWorldStageTransition;
                    QuestWorld.Instance.PlayerPositionProvider = null;
                }

                if (QuestWorld.Instance != null)
                {
                    // T-Q18: SaveAll() before shutdown.
                    QuestWorld.Instance.Shutdown();
                }

                // T-Q28: destroy runtime fallback DialogTree ScriptableObject instances.
                DestroyFallbackTrees();
            }
            if (Instance == this) Instance = null;
        }

        // ============================================================
        // T-Q28: Runtime fallback DialogTree for NPCs without defaultDialogTree.
        // Builds a minimal in-memory tree from NpcDefinition.questOffers[] +
        // questTurnIns[] + greetingText. ScriptableObject instance with
        // HideAndDontSave (Editor-friendly, no asset file pollution).
        // Cache: одна tree per npcId per server lifetime.
        // ============================================================
        private readonly System.Collections.Generic.Dictionary<string, ProjectC.Dialogue.DialogTree> _runtimeFallbackTrees
            = new System.Collections.Generic.Dictionary<string, ProjectC.Dialogue.DialogTree>();

        /// <summary>
        /// T-Q28: Get or build a runtime fallback DialogTree for the given NPC.
        /// Returns null only если у NPC нет ни questOffers, ни questTurnIns, ни greetingText
        /// (фактически dead-NPC). Caller must dispose via DestroyFallbackTree on shutdown.
        /// </summary>
        private ProjectC.Dialogue.DialogTree GetOrBuildFallbackTree(NpcDefinition npc, ulong clientId)
        {
            if (npc == null || string.IsNullOrEmpty(npc.npcId)) return null;
            string cacheKey = $"{npc.npcId}@{clientId}";  // T-Q28: per-player cache (state зависит от игрока)
            if (_runtimeFallbackTrees.TryGetValue(cacheKey, out var cached) && cached != null)
            {
                return cached;
            }
            var tree = BuildFallbackDialogTree(npc, clientId);
            if (tree != null)
            {
                _runtimeFallbackTrees[cacheKey] = tree;
            }
            return tree;
        }

        /// <summary>
        /// T-Q28: Quest availability для NPC's questOffer/questTurnIn slot'а.
        /// Используется в BuildFallbackDialogTree для фильтрации кнопок.
        /// </summary>
        private enum TQ28QuestAvailability
        {
            Available,        // можно взять/сдать
            Locked,           // prereq/rep не выполнены — показываем серым
            AlreadyActive,     // уже взят (нельзя взять второй раз)
            AlreadyCompleted,  // oneShot=true, уже сдан
            Hidden             // вообще не показываем
        }

        /// <summary>
        /// T-Q28: проверка доступности квеста для игрока.
        /// Учитывает: prerequisites (prereqQuest), state игрока, oneShot.
        /// </summary>
        private TQ28QuestAvailability CheckQuestAvailability(ulong clientId, QuestDefinition def)
        {
            if (def == null) return TQ28QuestAvailability.Hidden;
            var state = QuestWorld.Instance.GetPlayerQuestState(clientId, def.questId);
            // Если в логе — фильтруем по state
            if (state.HasValue)
            {
                switch (state.Value)
                {
                    case QuestState.Discovered:
                    case QuestState.Offered:
                    case QuestState.Active:
                        return TQ28QuestAvailability.AlreadyActive;
                    case QuestState.Completed:
                    case QuestState.TurnedIn:
                        return def.oneShot ? TQ28QuestAvailability.AlreadyCompleted : TQ28QuestAvailability.AlreadyActive;
                    case QuestState.Failed:
                        return TQ28QuestAvailability.Hidden;
                }
            }
            // Не в логе — проверяем prerequisites
            var (met, _) = QuestWorld.Instance.ArePrerequisitesMet(clientId, def);
            return met ? TQ28QuestAvailability.Available : TQ28QuestAvailability.Locked;
        }

        /// <summary>
        /// T-Q28: Build a minimal dialog graph at runtime:
        ///   greeting (root) ──► "Взять квест: {name}" edges (OfferQuest, только Available) ──► accepted (terminal)
        ///                  ├──► "🔒 {reason}" edges (greyed, для Locked) — info-only
        ///                  ├──► "Сдать квест: {name}" edges (CompleteObjective) ──► turned_in (terminal)
        ///                  └──► "До свидания" (EndConversation)
        /// Each OfferQuest/CompleteObjective edge → separate terminal node с подтверждением.
        /// </summary>
        private ProjectC.Dialogue.DialogTree BuildFallbackDialogTree(NpcDefinition npc, ulong clientId)
        {
            // Empty-NPC: no questOffers, no questTurnIns, no greeting → nothing to show.
            int offerCount = npc.questOffers != null ? npc.questOffers.Length : 0;
            int turnInCount = npc.questTurnIns != null ? npc.questTurnIns.Length : 0;
            string greeting = npc.showGreeting ? npc.greetingText : "";
            if (offerCount == 0 && turnInCount == 0 && string.IsNullOrEmpty(greeting)) return null;

            var w = QuestWorld.Instance;
            var nodes = new System.Collections.Generic.List<ProjectC.Dialogue.DialogueNode>();
            var greetingEdges = new System.Collections.Generic.List<ProjectC.Dialogue.DialogueEdge>();
            int shownOffers = 0, lockedShown = 0, hiddenOffers = 0, turnInsShown = 0, hiddenTurnIns = 0;

            // --- Per-offer branches ---
            // T-Q28: фильтрация по availability. Available → "Взять", Locked → "🔒 ...", остальное → скрыть.
            for (int i = 0; i < offerCount; i++)
            {
                string questId = npc.questOffers[i];
                if (string.IsNullOrEmpty(questId)) continue;
                var def = w?.GetQuest(questId);
                if (def == null) { hiddenOffers++; continue; }
                var avail = CheckQuestAvailability(clientId, def);
                if (avail == TQ28QuestAvailability.Hidden || avail == TQ28QuestAvailability.AlreadyCompleted) { hiddenOffers++; continue; }
                if (avail == TQ28QuestAvailability.AlreadyActive)
                {
                    // Уже взят — НЕ показываем кнопку "Взять", но questTurnIns может его покрыть (если в TurnIns).
                    hiddenOffers++;
                    continue;
                }

                string questName = ResolveQuestDisplayName(questId);
                string acceptedNodeId = $"accepted_{i}";
                nodes.Add(new ProjectC.Dialogue.DialogueNode
                {
                    nodeId = acceptedNodeId,
                    speaker = new ProjectC.Dialogue.SpeakerRef { speakerKind = ProjectC.Dialogue.SpeakerRef.Kind.Npc, refId = npc.npcId },
                    text = $"Квест «{questName}» добавлен в журнал. Возвращайся, когда справишься.",
                    portraitEmotion = "neutral",
                    edges = new[]
                    {
                        new ProjectC.Dialogue.DialogueEdge
                        {
                            label = "Понятно",
                            targetNodeId = "greeting",
                            action = new ProjectC.Dialogue.DialogueAction { type = ProjectC.Dialogue.DialogueActionType.EndConversation }
                        }
                    }
                });
                if (avail == TQ28QuestAvailability.Available)
                {
                    greetingEdges.Add(new ProjectC.Dialogue.DialogueEdge
                    {
                        label = $"Взять квест: {questName}",
                        targetNodeId = acceptedNodeId,
                        action = new ProjectC.Dialogue.DialogueAction
                        {
                            type = ProjectC.Dialogue.DialogueActionType.OfferQuest,
                            stringParam = questId
                        }
                    });
                    shownOffers++;
                }
                else // Locked
                {
                    var (_, reason) = w.ArePrerequisitesMet(clientId, def);
                    greetingEdges.Add(new ProjectC.Dialogue.DialogueEdge
                    {
                        label = $"🔒 {reason ?? "Недоступно"} ({questName})",
                        targetNodeId = "",  // no-op target, но action OfferQuest всё равно сработает
                        hideIfUnavailable = false,  // показать серым (через label)
                        action = new ProjectC.Dialogue.DialogueAction
                        {
                            type = ProjectC.Dialogue.DialogueActionType.OfferQuest,
                            stringParam = questId
                        }
                    });
                    lockedShown++;
                }
            }

            // --- Per-turnIn branches: "Сдать квест: {questName}" → CompleteObjective → turned_in_node ---
            // T-Q28: фильтруем — показываем ТОЛЬКО для квестов, которые игрок может реально сдать
            // (state=Active или Discovered, и prereq выполнены, и квест существует).
            // oneShot=true и сдан → скрыть. Не в логе → скрыть.
            for (int i = 0; i < turnInCount; i++)
            {
                string questId = npc.questTurnIns[i];
                if (string.IsNullOrEmpty(questId)) continue;
                var def = w?.GetQuest(questId);
                if (def == null) { hiddenTurnIns++; continue; }
                var st = w.GetPlayerQuestState(clientId, questId);
                if (!st.HasValue) { hiddenTurnIns++; continue; }  // не в логе
                bool canTurnIn = st.Value == QuestState.Active || st.Value == QuestState.Discovered
                                 || st.Value == QuestState.Offered || st.Value == QuestState.Completed;
                if (!canTurnIn) { hiddenTurnIns++; continue; }
                if (st.Value == QuestState.Completed && def.oneShot) { hiddenTurnIns++; continue; }  // уже сдан
                string questName = ResolveQuestDisplayName(questId);
                string turnedInNodeId = $"turned_in_{i}";
                nodes.Add(new ProjectC.Dialogue.DialogueNode
                {
                    nodeId = turnedInNodeId,
                    speaker = new ProjectC.Dialogue.SpeakerRef { speakerKind = ProjectC.Dialogue.SpeakerRef.Kind.Npc, refId = npc.npcId },
                    text = $"Квест «{questName}» засчитан. Хорошая работа.",
                    portraitEmotion = "neutral",
                    edges = new[]
                    {
                        new ProjectC.Dialogue.DialogueEdge
                        {
                            label = "Спасибо",
                            targetNodeId = "greeting",
                            action = new ProjectC.Dialogue.DialogueAction { type = ProjectC.Dialogue.DialogueActionType.EndConversation }
                        }
                    }
                });
                greetingEdges.Add(new ProjectC.Dialogue.DialogueEdge
                {
                    label = $"Сдать квест: {questName}",
                    targetNodeId = turnedInNodeId,
                    action = new ProjectC.Dialogue.DialogueAction
                    {
                        type = ProjectC.Dialogue.DialogueActionType.CompleteObjective,
                        stringParam = questId
                    }
                });
                turnInsShown++;
            }

            // --- Goodbye edge (always last) ---
            greetingEdges.Add(new ProjectC.Dialogue.DialogueEdge
            {
                label = "До свидания",
                targetNodeId = "",  // EndConversation (empty targetNodeId)
                action = new ProjectC.Dialogue.DialogueAction { type = ProjectC.Dialogue.DialogueActionType.EndConversation }
            });

            // --- Greeting root node ---
            string greetingText = !string.IsNullOrEmpty(greeting) ? greeting : $"{npc.displayName}: чем могу помочь?";
            nodes.Add(new ProjectC.Dialogue.DialogueNode
            {
                nodeId = "greeting",
                speaker = new ProjectC.Dialogue.SpeakerRef { speakerKind = ProjectC.Dialogue.SpeakerRef.Kind.Npc, refId = npc.npcId },
                text = greetingText,
                portraitEmotion = "neutral",
                edges = greetingEdges.ToArray()
            });

            // --- Create runtime ScriptableObject ---
            var tree = ScriptableObject.CreateInstance<ProjectC.Dialogue.DialogTree>();
            tree.name = $"RuntimeFallback_{npc.npcId}";
            tree.hideFlags = HideFlags.HideAndDontSave;  // Editor: no save to scene, no leak warning
            tree.treeId = $"runtime_{npc.npcId}";
            tree.displayName = $"{npc.displayName} (runtime fallback)";
            tree.rootNodeId = "greeting";
            tree.nodes = nodes.ToArray();
            if (debugMode)
            {
                Debug.Log($"[QuestServer] T-Q28: built fallback DialogTree for NPC '{npc.npcId}' clientId={clientId} " +
                          $"(offers: shown={shownOffers} locked={lockedShown} hidden={hiddenOffers} / " +
                          $"turnIns: shown={turnInsShown} hidden={hiddenTurnIns})");
            }
            return tree;
        }

        /// <summary>
        /// T-Q28: resolve quest display name from QuestDatabase. Falls back to questId если не найден.
        /// </summary>
        private string ResolveQuestDisplayName(string questId)
        {
            if (questDatabase != null)
            {
                var q = questDatabase.GetQuest(questId);
                if (q != null && !string.IsNullOrEmpty(q.displayName)) return q.displayName;
            }
            return questId;
        }

        /// <summary>T-Q28: cleanup all runtime fallback trees (called from OnNetworkDespawn).</summary>
        private void DestroyFallbackTrees()
        {
            if (_runtimeFallbackTrees.Count == 0) return;
            foreach (var kvp in _runtimeFallbackTrees)
            {
                if (kvp.Value != null) Destroy(kvp.Value);
            }
            _runtimeFallbackTrees.Clear();
        }

        // ============================================================
        // T-Q20: Periodic tick (server-only)
        // ============================================================

        private void Update()
        {
            if (!IsServer) return;
            if (QuestWorld.Instance == null) return;
            QuestWorld.Instance.TickAll(Time.deltaTime);
        }

        /// <summary>T-Q20: server-side position provider for ReachLocation objective.</summary>
        private Vector3 GetPlayerPosition(ulong clientId)
        {
            var np = FindNetworkPlayer(clientId);
            if (np == null) return Vector3.zero;
            return np.transform.position;
        }

        /// <summary>T-Q20: handler for QuestWorld.OnFireDialogActions (stage onComplete/onEnter actions).</summary>
        private void OnWorldFireDialogActions(ulong clientId, string npcIdHint, ProjectC.Dialogue.DialogueAction[] actions)
        {
            if (actions == null) return;
            for (int i = 0; i < actions.Length; i++)
            {
                if (actions[i] == null) continue;
                FireDialogAction(clientId, npcIdHint, actions[i]);
            }
        }

        /// <summary>T-Q20: handler for QuestWorld.OnStageTransition → push snapshot.</summary>
        private void OnWorldStageTransition(ulong clientId, string questId, string fromStage, string toStage)
        {
            if (QuestWorld.Instance != null) QuestWorld.Instance.SavePlayer(clientId);
            SendQuestSnapshotToClient(clientId);
        }

        // ============================================================
        // RATE LIMITING
        // ============================================================

        private bool CheckRateLimit(ulong clientId)
        {
            if (maxOpsPerMinute <= 0) return true;
            if (!_opTimestamps.TryGetValue(clientId, out var timestamps))
            {
                timestamps = new List<float>();
                _opTimestamps[clientId] = timestamps;
            }
            // Drop old (>60s ago)
            timestamps.RemoveAll(t => Time.time - t > 60f);
            if (timestamps.Count >= maxOpsPerMinute)
            {
                if (debugMode) Debug.LogWarning($"[QuestServer] Rate limit hit for client {clientId}");
                return false;
            }
            timestamps.Add(Time.time);
            return true;
        }

        // ============================================================
        // CLIENT → SERVER RPCs (T-Q05: stubs, full logic in T-Q06+)
        // ============================================================

        /// <summary>
        /// Player wants to start a dialog with NPC. Server validates (rate, dist) and
        /// returns first DialogStepDto. T-Q10 real impl.
        /// </summary>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void RequestTalkToNpcRpc(string npcId, string treeIdHint, RpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;
            if (!CheckRateLimit(clientId)) return;
            if (QuestWorld.Instance == null || questDatabase == null) return;
            if (string.IsNullOrEmpty(npcId)) return;

            if (debugMode) Debug.Log($"[QuestServer] RequestTalkToNpc client={clientId} npc={npcId} treeHint={treeIdHint}");

            // T-Q22 fix: mark NPC as talked-to for TalkToNpc objective evaluation.
            // MarkNpcTalked must happen HERE (not only in AdvanceDialogue) because
            // the player may just press E and never select a dialogue option.
            QuestWorld.Instance?.MarkNpcTalked(clientId, npcId);

            // T-Q11c-fix: если сессия уже открыта (повторный E / stale state) — закрыть и открыть заново.
            // Иначе OpenDialog возвращает null и игрок видит "failed to open session".
            if (QuestWorld.Instance.GetDialogSession(clientId) != null)
            {
                if (debugMode) Debug.Log($"[QuestServer] RequestTalkToNpc: stale session detected, closing");
                QuestWorld.Instance.CloseDialog(clientId);
            }

            // Find NpcDefinition
            var npc = questDatabase.GetNpc(npcId);
            if (npc == null)
            {
                if (debugMode) Debug.LogWarning($"[QuestServer] RequestTalkToNpc: npc '{npcId}' not found");
                return;
            }

            // Pick dialog tree: hint or default
            DialogTree tree = null;
            if (!string.IsNullOrEmpty(treeIdHint))
            {
                tree = questDatabase.GetDialogTree(treeIdHint);
            }
            if (tree == null)
            {
                tree = npc.defaultDialogTree;
            }
            if (tree == null)
            {
                // T-Q28: runtime fallback — build DialogTree from NpcDefinition.questOffers[]+
                // questTurnIns[]+greetingText. NPC can offer/turn-in квесты immediately after
                // being placed in the world, no manual DialogTree asset needed.
                tree = GetOrBuildFallbackTree(npc, clientId);
            }
            if (tree == null)
            {
                if (debugMode) Debug.LogWarning($"[QuestServer] RequestTalkToNpc: NPC '{npcId}' has no dialog tree (no offers, no turnIns, no greeting)");
                return;
            }

            // Open dialog session in QuestWorld
            var session = QuestWorld.Instance.OpenDialog(clientId, npcId, tree);
            if (session == null)
            {
                if (debugMode) Debug.LogWarning($"[QuestServer] RequestTalkToNpc: failed to open session for client {clientId}");
                return;
            }

            // Build first step
            var step = BuildDialogStep(tree, session.currentNodeId, clientId);
            SendDialogStepToClient(clientId, step);
        }

        /// <summary>
        /// Player picked an option in dialog. Server validates option index, fires action,
        /// returns next DialogStepDto (or end marker). T-Q10 real impl.
        /// </summary>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void RequestAdvanceDialogueRpc(string dialogTreeId, string currentNodeId, int optionIndex, string talkingToNpcId, RpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;
            if (!CheckRateLimit(clientId)) return;
            if (QuestWorld.Instance == null || questDatabase == null) return;
            if (debugMode) Debug.Log($"[QuestServer] RequestAdvanceDialogue client={clientId} tree={dialogTreeId} node={currentNodeId} option={optionIndex}");

            // Validate session
            var session = QuestWorld.Instance.GetDialogSession(clientId);
            if (session == null || session.npcId != talkingToNpcId || session.tree.treeId != dialogTreeId)
            {
                if (debugMode) Debug.LogWarning($"[QuestServer] RequestAdvanceDialogue: no matching session for client {clientId}");
                return;
            }

            // Find current node
            var node = session.tree.GetNode(currentNodeId);
            if (node == null)
            {
                if (debugMode) Debug.LogWarning($"[QuestServer] RequestAdvanceDialogue: node '{currentNodeId}' not found");
                return;
            }

            // Find option. T-Q21: use session.visibleEdges (filtered by hideIfUnavailable).
            if (optionIndex < 0 || session.visibleEdges == null || optionIndex >= session.visibleEdges.Count)
            {
                if (debugMode) Debug.LogWarning($"[QuestServer] RequestAdvanceDialogue: option {optionIndex} out of range (visibleEdges={session.visibleEdges?.Count ?? 0})");
                return;
            }
            var edge = session.visibleEdges[optionIndex];

            // Check conditions (server-side authoritative)
            if (!EvaluateConditions(edge, clientId))
            {
                if (debugMode) Debug.Log($"[QuestServer] RequestAdvanceDialogue: conditions not met for option {optionIndex}");
                // Send same step back (with unavailable reason)
                var sameStep = BuildDialogStep(session.tree, currentNodeId, clientId);
                SendDialogStepToClient(clientId, sameStep);
                return;
            }

            // Fire action (if any)
            if (edge.action != null)
            {
                FireDialogAction(clientId, talkingToNpcId, edge.action);
            }

            // Publish DialogVisitedEvent (для TalkedToNpcTrigger)
            WorldEventBus.Publish(new DialogVisitedEvent
            {
                PlayerId = clientId,
                TimestampUnix = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                TreeId = dialogTreeId,
                NodeId = currentNodeId,
                NpcId = talkingToNpcId
            });

            // Advance to target node
            var nextNode = session.tree.GetNode(edge.targetNodeId);
            if (nextNode == null)
            {
                // End dialog
                session.currentNodeId = "";
                QuestWorld.Instance.CloseDialog(clientId);
                SendDialogStepToClient(clientId, new DialogStepDto { treeId = dialogTreeId, nodeId = "", isEnd = true });
                return;
            }
            session.currentNodeId = nextNode.nodeId;
            // M11 fix: fire onEnterActions of the target node (reputation, attitude, complete objective, etc.)
            if (nextNode.onEnterActions != null)
            {
                for (int i = 0; i < nextNode.onEnterActions.Length; i++)
                {
                    if (nextNode.onEnterActions[i] != null)
                        FireDialogAction(clientId, talkingToNpcId, nextNode.onEnterActions[i]);
                }
            }
            var step = BuildDialogStep(session.tree, nextNode.nodeId, clientId);
            SendDialogStepToClient(clientId, step);
        }

        /// <summary>
        /// T-Q11b-fix: player closes dialog via ESC. Server-side session must be cleaned up,
        /// otherwise subsequent RequestTalkToNpc returns "failed to open session" (already-in-dialog).
        /// </summary>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void RequestEndConversationRpc(RpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;
            if (QuestWorld.Instance == null) return;
            var session = QuestWorld.Instance.GetDialogSession(clientId);
            if (session == null) return;
            if (debugMode) Debug.Log($"[QuestServer] RequestEndConversation client={clientId}");
            QuestWorld.Instance.CloseDialog(clientId);
        }

        /// <summary>
        /// Player explicitly accepts a quest (either via dialog OfferQuest action or
        /// CharacterWindow "Accept" button на Discovered квесте).
        /// </summary>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void RequestAcceptQuestRpc(string questId, string fromNpcId, RpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;
            if (!CheckRateLimit(clientId)) return;
            if (QuestWorld.Instance == null) return;
            if (debugMode) Debug.Log($"[QuestServer] RequestAcceptQuest client={clientId} quest={questId} fromNpc={fromNpcId}");

            // T-Q15: real impl — call QuestWorld.TryAccept.
            var result = QuestWorld.Instance.TryAccept(clientId, questId, fromNpcId ?? "");
            SendQuestResultToClient(clientId, result);
            if (result.code == (byte)QuestResultCode.Ok)
            {
                // Push fresh snapshot so client sees new Active quest in their log.
                SendQuestSnapshotToClient(clientId);
            }
        }

        /// <summary>
        /// Player turns in a Completed quest at the right NPC.
        /// </summary>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void RequestTurnInQuestRpc(string questId, string toNpcId, RpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;
            if (!CheckRateLimit(clientId)) return;
            if (QuestWorld.Instance == null) return;
            if (debugMode) Debug.Log($"[QuestServer] RequestTurnInQuest client={clientId} quest={questId} toNpc={toNpcId}");

            // T-Q15: real impl — call QuestWorld.TryTurnIn.
            var result = QuestWorld.Instance.TryTurnIn(clientId, questId, toNpcId ?? "");
            SendQuestResultToClient(clientId, result);
            if (result.code == (byte)QuestResultCode.Ok)
            {
                SendQuestSnapshotToClient(clientId);
            }
        }

        /// <summary>
        /// Player toggles tracker pin (HUD shows this quest in top-right).
        /// </summary>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void RequestTrackQuestRpc(string questId, bool track, RpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;
            if (!CheckRateLimit(clientId)) return;
            if (QuestWorld.Instance == null) return;
            if (debugMode) Debug.Log($"[QuestServer] RequestTrackQuest client={clientId} quest={questId} track={track}");

            // T-Q15: real impl — call QuestWorld.SetTracked.
            var result = QuestWorld.Instance.SetTracked(clientId, questId, track);
            SendQuestResultToClient(clientId, result);
            if (result.code == (byte)QuestResultCode.Ok)
            {
                SendQuestSnapshotToClient(clientId);
            }
        }

        /// <summary>
        /// Player requests full quest list snapshot (e.g. при открытии CharacterWindow).
        /// T-Q07: real impl — build DTO + send TargetRpc to client.
        /// </summary>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void RequestRefreshQuestsRpc(RpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;
            if (!CheckRateLimit(clientId)) return;
            if (QuestWorld.Instance == null) return;
            if (debugMode) Debug.Log($"[QuestServer] RequestRefreshQuests client={clientId}");
            var snapshot = BuildQuestSnapshot(clientId);
            SendQuestSnapshotToClient(clientId, snapshot);
        }

        /// <summary>
        /// Player requests full reputation snapshot.
        /// T-Q07: real impl — build DTO + send TargetRpc to client.
        /// </summary>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void RequestRefreshReputationRpc(RpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;
            if (!CheckRateLimit(clientId)) return;
            if (QuestWorld.Instance == null) return;
            if (debugMode) Debug.Log($"[QuestServer] RequestRefreshReputation client={clientId}");
            var snapshot = BuildReputationSnapshot(clientId);
            SendReputationSnapshotToClient(clientId, snapshot);
        }

        /// <summary>
        /// Player requests full NpcAttitude snapshot (per NPC relationship values).
        /// T-Q07: real impl — build DTO + send TargetRpc to client.
        /// </summary>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void RequestRefreshNpcAttitudeRpc(RpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;
            if (!CheckRateLimit(clientId)) return;
            if (QuestWorld.Instance == null) return;
            if (debugMode) Debug.Log($"[QuestServer] RequestRefreshNpcAttitude client={clientId}");
            var snapshot = BuildNpcAttitudeSnapshot(clientId);
            SendNpcAttitudeSnapshotToClient(clientId, snapshot);
        }

        /// <summary>
        /// Server tells client about a newly Discovered quest (triggered by EventDriven objective).
        /// S→C notification. Not a client request.
        /// </summary>
        [Rpc(SendTo.Server)]
        public void NotifyQuestDiscoveredRpc(string questId, RpcParams rpcParams = default)
        {
            // Used by server-pushed notifications (EventDriven). T-Q06/T-Q11.
            if (debugMode) Debug.Log($"[QuestServer] NotifyQuestDiscovered (server-internal call)");
        }

        // ============================================================
        // T-Q06: WorldEventBus subscribers → QuestTriggerService.Evaluate
        // ============================================================

        // Cached delegate fields (so Unsubscribe can find same instance).
        private System.Action<ItemAddedEvent> _handleItemAdded;
        private System.Action<ItemRemovedEvent> _handleItemRemoved;
        private System.Action<ReputationChangedEvent> _handleReputationChanged;
        private System.Action<NpcAttitudeChangedEvent> _handleNpcAttitudeChanged;
        private System.Action<CustomEvent> _handleCustomEvent;
        private System.Action<DialogVisitedEvent> _handleDialogVisited;
        private System.Action<DayNightPhaseChangedEvent> _handleDayNightChanged;
        private System.Action<ulong> _handleClientConnected; // T-Q13

        private void OnItemAdded(ItemAddedEvent ev)
        {
            if (QuestWorld.Instance == null || QuestWorld.Instance.TriggerService == null) return;
            // Hint format: "HaveItem:<itemId>". TriggerService filters by prefix.
            int advances = QuestWorld.Instance.TriggerService.Evaluate(ev.PlayerId, $"HaveItem:{ev.ItemId}");
            if (debugMode && advances > 0) Debug.Log($"[QuestServer] OnItemAdded player={ev.PlayerId} itemId={ev.ItemId} → {advances} objective(s) advanced");
        }

        private void OnItemRemoved(ItemRemovedEvent ev)
        {
            if (QuestWorld.Instance == null || QuestWorld.Instance.TriggerService == null) return;
            int advances = QuestWorld.Instance.TriggerService.Evaluate(ev.PlayerId, $"HaveItem:{ev.ItemId}");
            if (debugMode && advances > 0) Debug.Log($"[QuestServer] OnItemRemoved player={ev.PlayerId} itemId={ev.ItemId} → {advances} objective(s) advanced");
        }

        private void OnReputationChanged(ReputationChangedEvent ev)
        {
            if (QuestWorld.Instance == null || QuestWorld.Instance.TriggerService == null) return;
            int advances = QuestWorld.Instance.TriggerService.Evaluate(ev.PlayerId, $"ReputationAtLeast:{ev.Faction}");
            if (debugMode && advances > 0) Debug.Log($"[QuestServer] OnReputationChanged player={ev.PlayerId} faction={ev.Faction} → {advances} objective(s) advanced");
        }

        private void OnNpcAttitudeChanged(NpcAttitudeChangedEvent ev)
        {
            if (QuestWorld.Instance == null || QuestWorld.Instance.TriggerService == null) return;
            int advances = QuestWorld.Instance.TriggerService.Evaluate(ev.PlayerId, $"NpcAttitudeAtLeast:{ev.NpcId}");
            if (debugMode && advances > 0) Debug.Log($"[QuestServer] OnNpcAttitudeChanged player={ev.PlayerId} npc={ev.NpcId} → {advances} objective(s) advanced");
        }

        private void OnCustomEvent(CustomEvent ev)
        {
            if (QuestWorld.Instance == null || QuestWorld.Instance.TriggerService == null) return;
            // Mark event occurred (for IsSatisfied check)
            QuestWorld.Instance.MarkEventOccurred(ev.PlayerId, ev.EventId);
            int advances = QuestWorld.Instance.TriggerService.Evaluate(ev.PlayerId, $"Event:{ev.EventId}");
            if (debugMode && advances > 0) Debug.Log($"[QuestServer] OnCustomEvent player={ev.PlayerId} eventId={ev.EventId} → {advances} objective(s) advanced");
        }

        private void OnDialogVisited(DialogVisitedEvent ev)
        {
            if (QuestWorld.Instance == null || QuestWorld.Instance.TriggerService == null) return;
            if (!string.IsNullOrEmpty(ev.NpcId))
            {
                QuestWorld.Instance.MarkNpcTalked(ev.PlayerId, ev.NpcId);
            }
            int advances = QuestWorld.Instance.TriggerService.Evaluate(ev.PlayerId, $"TalkedToNpc:{ev.NpcId}");
            if (debugMode && advances > 0) Debug.Log($"[QuestServer] OnDialogVisited player={ev.PlayerId} npc={ev.NpcId} → {advances} objective(s) advanced");
        }

        private void OnDayNightChanged(DayNightPhaseChangedEvent ev)
        {
            // PlayerId=0 global event. Loop all connected clients (T-Q15: more efficient via per-player iteration).
            if (QuestWorld.Instance == null || QuestWorld.Instance.TriggerService == null) return;
            if (NetworkManager == null) return;
            foreach (var clientId in NetworkManager.ConnectedClientsIds)
            {
                QuestWorld.Instance.TriggerService.Evaluate(clientId, $"DayNightPhase:{ev.NewPhaseName}");
            }
        }

        // ============================================================
        // T-Q07: Snapshot builders + TargetRpc senders
        // ============================================================

        private QuestSnapshotDto BuildQuestSnapshot(ulong clientId)
        {
            var w = QuestWorld.Instance;
            if (w == null) return new QuestSnapshotDto { quests = null, newlyDiscoveredQuestIds = null };
            var playerQuests = w.GetPlayerQuests(clientId);
            int count = playerQuests.Count;
            var arr = new QuestProgressDto[count];
            for (int i = 0; i < count; i++)
            {
                var inst = playerQuests[i];
                var def = w.GetQuest(inst.questId);
                // Build objective progress
                int objCount = inst.objectiveProgress != null ? inst.objectiveProgress.Count : 0;
                var objArr = new ObjectiveProgressDto[objCount];
                for (int j = 0; j < objCount; j++)
                {
                    var op = inst.objectiveProgress[j];
                    string desc = "";
                    int reqQty = 1;  // T-Q21: default 1 (TalkToNpc, DeliverItem, etc.)
                    // Look up description AND requiredQuantity from current stage's objective
                    if (def != null && !string.IsNullOrEmpty(inst.currentStageId))
                    {
                        var st = def.GetStage(inst.currentStageId);
                        if (st != null)
                        {
                            for (int k = 0; k < st.objectives.Length; k++)
                            {
                                if (st.objectives[k] != null && st.objectives[k].objectiveId == op.objectiveId)
                                {
                                    desc = st.objectives[k].description;
                                    reqQty = st.objectives[k].requiredQuantity;
                                    break;
                                }
                            }
                        }
                    }
                    objArr[j] = new ObjectiveProgressDto
                    {
                        objectiveId = op.objectiveId,
                        description = desc,
                        completed = op.completed,
                        currentValue = op.currentCount,
                        requiredQuantity = reqQty
                    };
                    if (Debug.isDebugBuild) Debug.Log($"[QuestServer] BuildQuestSnapshot: quest={inst.questId} obj={op.objectiveId} desc='{desc}' reqQty={reqQty} curVal={op.currentCount} completed={op.completed}");
                }
                arr[i] = new QuestProgressDto
                {
                    questId = inst.questId,
                    displayName = def != null ? def.displayName : "",
                    state = (byte)inst.state,
                    currentStageId = inst.currentStageId ?? "",
                    isTracked = inst.isTracked,
                    objectives = objArr
                };
            }
            return new QuestSnapshotDto
            {
                quests = arr,
                newlyDiscoveredQuestIds = null  // T-Q11: populate on EventDriven
            };
        }

        private ReputationSnapshotDto BuildReputationSnapshot(ulong clientId)
        {
            var w = QuestWorld.Instance;
            if (w == null) return new ReputationSnapshotDto { entries = null };
            var arr = new System.Collections.Generic.List<ReputationEntryDto>();
            // Iterate all FactionId values (0..11 per T-Q01)
            foreach (ProjectC.Factions.FactionId fid in System.Enum.GetValues(typeof(ProjectC.Factions.FactionId)))
            {
                if (fid == ProjectC.Factions.FactionId.None) continue;
                int v = w.GetReputation(clientId, fid);
                arr.Add(new ReputationEntryDto { faction = (byte)fid, value = v });
            }
            return new ReputationSnapshotDto { entries = arr.ToArray() };
        }

        private NpcAttitudeSnapshotDto BuildNpcAttitudeSnapshot(ulong clientId)
        {
            var w = QuestWorld.Instance;
            if (w == null) return new NpcAttitudeSnapshotDto { entries = null };
            // T-Q07: iterate questDatabase.questOffers[] to discover known NPC ids.
            // T-Q15: track all NpcDefinitions globally in QuestWorld, not just quest givers.
            var knownNpcIds = new System.Collections.Generic.HashSet<string>();
            foreach (var def in w.GetAllQuests())
            {
                if (def == null) continue;
                // Walk all stages for TalkToNpc objectives (NpcId) — defensive
                for (int s = 0; s < def.stages.Length; s++)
                {
                    for (int o = 0; o < def.stages[s].objectives.Length; o++)
                    {
                        var obj = def.stages[s].objectives[o];
                        if (obj != null && !string.IsNullOrEmpty(obj.targetNpcId))
                        {
                            knownNpcIds.Add(obj.targetNpcId);
                        }
                    }
                }
            }
            var arr = new NpcAttitudeEntryDto[knownNpcIds.Count];
            int idx = 0;
            foreach (var npcId in knownNpcIds)
            {
                arr[idx++] = new NpcAttitudeEntryDto { npcId = npcId, value = w.GetNpcAttitude(clientId, npcId) };
            }
            return new NpcAttitudeSnapshotDto { entries = arr };
        }

        // -------- Senders --------

        private void SendQuestSnapshotToClient(ulong clientId, QuestSnapshotDto snapshot)
        {
            var netPlayer = FindNetworkPlayer(clientId);
            if (netPlayer == null)
            {
                if (debugMode) Debug.LogWarning($"[QuestServer] SendQuestSnapshotToClient: no NetworkPlayer for client {clientId}");
                return;
            }
            netPlayer.ReceiveQuestSnapshotTargetRpc(snapshot);
            if (debugMode) Debug.Log($"[QuestServer] SendQuestSnapshotToClient: client={clientId} quests={snapshot.quests?.Length ?? 0}");
        }

        // T-Q15: overload — build snapshot inline (используется после Accept/TurnIn/Track).
        private void SendQuestSnapshotToClient(ulong clientId)
        {
            SendQuestSnapshotToClient(clientId, BuildQuestSnapshot(clientId));
        }

        // T-Q15: send QuestResult (Ok/Fail with code+message) to client.
        private void SendQuestResultToClient(ulong clientId, QuestResultDto result)
        {
            var netPlayer = FindNetworkPlayer(clientId);
            if (netPlayer == null) return;
            netPlayer.ReceiveQuestResultTargetRpc(result);
        }

        private void SendReputationSnapshotToClient(ulong clientId, ReputationSnapshotDto snapshot)
        {
            var netPlayer = FindNetworkPlayer(clientId);
            if (netPlayer == null) return;
            netPlayer.ReceiveReputationSnapshotTargetRpc(snapshot);
        }

        private void SendNpcAttitudeSnapshotToClient(ulong clientId, NpcAttitudeSnapshotDto snapshot)
        {
            var netPlayer = FindNetworkPlayer(clientId);
            if (netPlayer == null) return;
            netPlayer.ReceiveNpcAttitudeSnapshotTargetRpc(snapshot);
        }

        // ============================================================
        // T-Q13: Broadcast helpers (после server-side Modify* через dialogue/reward/quest)
        // + OnClientConnected snapshot push.
        // ============================================================

        /// <summary>T-Q13: вызывается из любого server-side кода после изменения reputation. Push snapshot клиенту.</summary>
        public void BroadcastReputationChange(ulong clientId)
        {
            if (!IsServer) return;
            var snapshot = BuildReputationSnapshot(clientId);
            SendReputationSnapshotToClient(clientId, snapshot);
            if (debugMode) Debug.Log($"[QuestServer] BroadcastReputationChange: client={clientId} factions={snapshot.entries?.Length ?? 0}");
        }

        /// <summary>T-Q13: вызывается из любого server-side кода после изменения npcAttitude. Push snapshot клиенту.</summary>
        public void BroadcastNpcAttitudeChange(ulong clientId)
        {
            if (!IsServer) return;
            var snapshot = BuildNpcAttitudeSnapshot(clientId);
            SendNpcAttitudeSnapshotToClient(clientId, snapshot);
            if (debugMode) Debug.Log($"[QuestServer] BroadcastNpcAttitudeChange: client={clientId} npcs={snapshot.entries?.Length ?? 0}");
        }

        /// <summary>T-Q13: push и reputation, и npcAttitude snapshot'ы. Удобно для cross-fallback (attitude → faction тоже).</summary>
        public void BroadcastBothChange(ulong clientId)
        {
            BroadcastReputationChange(clientId);
            BroadcastNpcAttitudeChange(clientId);
        }

        /// <summary>T-Q13: NetworkManager.OnClientConnectedCallback handler. Push initial snapshots новому клиенту.
        /// Player object spawn'ится раньше client connect callback, поэтому netPlayer должен быть жив.</summary>
        private void OnClientConnectedForSnapshot(ulong clientId)
        {
            if (!IsServer) return;
            if (debugMode) Debug.Log($"[QuestServer] OnClientConnectedForSnapshot: client={clientId}");

            // T-Q18: load player state from disk → populate QuestWorld dictionaries.
            // No-op если нет save (new player) или Repository == null.
            if (QuestWorld.Instance != null)
            {
                QuestWorld.Instance.LoadPlayer(clientId);
            }

            // Небольшая задержка не нужна — SendTo.Owner RPC дождётся готовности client'а.
            BroadcastBothChange(clientId);
            // T-Q15 fix: push initial quest snapshot (rep+attitude уже отправлено выше).
            // Без этого CharacterWindow → таб КВЕСТЫ пустой при P до любого Accept/TurnIn.
            SendQuestSnapshotToClient(clientId);
        }

        /// <summary>Find NetworkPlayer for clientId (server-side). null если player object не spawned.</summary>
        private NetworkPlayer FindNetworkPlayer(ulong clientId)
        {
            if (NetworkManager == null) return null;
            if (!NetworkManager.ConnectedClients.TryGetValue(clientId, out var cc)) return null;
            return cc.PlayerObject != null ? cc.PlayerObject.GetComponent<NetworkPlayer>() : null;
        }

        // ============================================================
        // T-Q10: Dialog step builder + condition/action evaluators
        // ============================================================

        private DialogStepDto BuildDialogStep(DialogTree tree, string nodeId, ulong clientId)
        {
            try
            {
            if (tree == null || string.IsNullOrEmpty(nodeId))
            {
                return new DialogStepDto { treeId = tree != null ? tree.treeId : "", nodeId = "", isEnd = true };
            }
            var node = tree.GetNode(nodeId);
            if (node == null)
            {
                return new DialogStepDto { treeId = tree.treeId, nodeId = "", isEnd = true };
            }

            // Build options: each edge → option (label, available, reason).
            // T-Q21: filter out edges where hideIfUnavailable && !available — они невидимы для клиента.
            // Store visibleEdges in DialogSession for RequestAdvanceDialogue index mapping.
            var edges = node.edges ?? Array.Empty<DialogueEdge>();
            var visibleEdges = new System.Collections.Generic.List<DialogueEdge>();
            var visibleOptions = new System.Collections.Generic.List<DialogOptionDto>();
            for (int i = 0; i < edges.Length; i++)
            {
                var edge = edges[i];
                if (edge == null) continue;
                bool available = EvaluateConditions(edge, clientId);
                if (!available && edge.hideIfUnavailable)
                {
                    // Skip completely — invisible option.
                    continue;
                }
                visibleOptions.Add(new DialogOptionDto
                {
                    index = i,
                    label = edge.label ?? "",
                    available = available,
                    unavailableReason = available ? "" : "Условие не выполнено"
                });
                visibleEdges.Add(edge);
            }
            var options = visibleOptions.ToArray();
            // Store for RequestAdvanceDialogue index mapping
            var w = ProjectC.Quests.QuestWorld.Instance;
            if (w != null)
            {
                var session = w.GetDialogSession(clientId);
                if (session != null) session.visibleEdges = visibleEdges;
            }

            // Resolve speaker
            string speakerNpcId = "";
            string speakerText = "";
            if (node.speaker != null && node.speaker.speakerKind == SpeakerRef.Kind.Npc && !string.IsNullOrEmpty(node.speaker.refId))
            {
                speakerNpcId = node.speaker.refId;
            }
            speakerText = node.text ?? "";

            return new DialogStepDto
            {
                treeId = tree.treeId,
                nodeId = node.nodeId,
                speakerNpcId = speakerNpcId,
                speakerText = speakerText,
                options = options,
                isEnd = false
            };
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[QuestServer] BuildDialogStep NRE/CSE: tree={tree?.treeId ?? "NULL"} nodeId='{nodeId}' clientId={clientId}: {e.Message}\n{e.StackTrace}");
                return new DialogStepDto { treeId = tree != null ? tree.treeId : "", nodeId = "", isEnd = true };
            }
        }

        /// <summary>Evaluate all conditions (atomic AND) for edge. T-Q06: TriggerId-based convention.</summary>
        private bool EvaluateConditions(DialogueEdge edge, ulong clientId)
        {
            if (edge == null) return true;
            // Single condition (legacy)
            if (edge.condition != null)
            {
                if (!EvaluateSingleCondition(edge.condition, clientId)) return false;
            }
            // Multiple conditions (AND)
            if (edge.conditions != null)
            {
                for (int i = 0; i < edge.conditions.Length; i++)
                {
                    if (edge.conditions[i] == null) continue;
                    if (!EvaluateSingleCondition(edge.conditions[i], clientId)) return false;
                }
            }
            return true;
        }

        private bool EvaluateSingleCondition(DialogueCondition c, ulong clientId)
        {
            if (c == null) return true;
            var w = QuestWorld.Instance;
            if (w == null) return true;
            switch (c.type)
            {
                case DialogueConditionType.HasItem:
                {
                    int itemId = 0;
                    if (!string.IsNullOrEmpty(c.stringParam)) int.TryParse(c.stringParam, out itemId);
                    return InventoryWorld.Instance != null
                        && InventoryWorld.Instance.CountOf(clientId, itemId) >= c.intParam;
                }
                case DialogueConditionType.QuestStateEquals:
                {
                    // Real impl: check quest state by questId and questStateParam.
                    var quests = w.GetPlayerQuests(clientId);
                    if (quests == null || string.IsNullOrEmpty(c.stringParam)) return true; // unknown questId → allow (stub)
                    for (int i = 0; i < quests.Count; i++)
                    {
                        if (quests[i].questId == c.stringParam)
                            return (byte)quests[i].state == (byte)c.questStateParam;
                    }
                    return false; // quest not in player log → not in this state → edge hidden via hideIfUnavailable
                }
                case DialogueConditionType.ReputationAtLeast:
                    return w.GetReputation(clientId, c.factionParam) >= c.intParam;
                case DialogueConditionType.FlagIsSet:
                    return w.GetFlag(clientId, c.stringParam);
                case DialogueConditionType.TimeOfDayIn:
                {
                    var ctrl = DayNightController.Instance;
                    if (ctrl == null) ctrl = UnityEngine.Object.FindObjectOfType<DayNightController>();
                    return ctrl != null && ctrl.CurrentPhase != null && ctrl.CurrentPhase.phaseName == c.stringParam;
                }
                default:
                    return true; // unknown / unimplemented → allow (T-Q10 stub)
            }
        }

        /// <summary>Fire dialog action (server-side). T-Q10: minimal — OfferQuest emits event, others stub.</summary>
        private void FireDialogAction(ulong clientId, string npcId, DialogueAction action)
        {
            if (action == null) return;
            var w = QuestWorld.Instance;
            if (w == null) return;
            switch (action.type)
            {
                case DialogueActionType.OfferQuest:
                    {
                        // T-Q15: real impl — QuestWorld.TryOffer (Discovered → Offered).
                        var qwOffer = QuestWorld.Instance;
                        if (qwOffer == null) break;
                        var resultOffer = qwOffer.TryOffer(clientId, action.stringParam);
                        if (debugMode) Debug.Log($"[QuestServer] FireDialogAction: OfferQuest {action.stringParam} to client {clientId} → code={resultOffer.code} msg={resultOffer.message}");
                        if (resultOffer.code == (byte)QuestResultCode.Ok)
                        {
                            SendQuestSnapshotToClient(clientId);
                        }
                        SendDialogActionResultToClient(clientId, new DialogActionResultDto
                        {
                            actionType = (byte)action.type,
                            success = resultOffer.code == (byte)QuestResultCode.Ok,
                            resultData = resultOffer.message
                        });
                    }
                    break;
                case DialogueActionType.AcceptQuest:
                    {
                        // M11: auto-accept quest — TryOffer + TryAccept (no manual P accept needed).
                        if (string.IsNullOrEmpty(action.stringParam))
                        {
                            SendDialogActionResultToClient(clientId, new DialogActionResultDto { actionType = (byte)action.type, success = false, resultData = "missing questId" });
                            break;
                        }
                        var qwAccept = QuestWorld.Instance;
                        if (qwAccept == null)
                        {
                            SendDialogActionResultToClient(clientId, new DialogActionResultDto { actionType = (byte)action.type, success = false, resultData = "QuestWorld null" });
                            break;
                        }
                        qwAccept.TryOffer(clientId, action.stringParam);
                        var acceptResult = qwAccept.TryAccept(clientId, action.stringParam, "");
                        if (debugMode) Debug.Log($"[QuestServer] FireDialogAction: AcceptQuest {action.stringParam} → code={acceptResult.code} {acceptResult.message}");
                        SendQuestSnapshotToClient(clientId);
                        SendDialogActionResultToClient(clientId, new DialogActionResultDto
                        {
                            actionType = (byte)action.type,
                            success = acceptResult.code == (byte)QuestResultCode.Ok,
                            resultData = acceptResult.message
                        });
                    }
                    break;
                case DialogueActionType.OpenMarket:
                    {
                        // T-Q17: OpenMarket — close dialog + call MarketInteractor.TryOpenMarket.
                        // Edge action: stringParam = zoneId (опционально, hint). Если пусто — local player zone.
                        // TryOpenMarket — client-side, request ContractServer.RequestListRpc RPC.
                        if (debugMode) Debug.Log($"[QuestServer] FireDialogAction: OpenMarket zone='{action.stringParam}'");
                        SendDialogActionResultToClient(clientId, new DialogActionResultDto
                        {
                            actionType = (byte)action.type,
                            success = true,
                            resultData = action.stringParam
                        });
                    }
                    break;
                case DialogueActionType.OpenService:
                    {
                        // T-Q17: OpenService — stub (ServiceUI не существует; только PriceFormula helpers).
                        // Edge action: stringParam = serviceId (опционально).
                        if (debugMode) Debug.Log($"[QuestServer] FireDialogAction: OpenService serviceId='{action.stringParam}' (T-Q17 stub — ServiceUI TBD)");
                        SendDialogActionResultToClient(clientId, new DialogActionResultDto
                        {
                            actionType = (byte)action.type,
                            success = true,
                            resultData = action.stringParam
                        });
                    }
                    break;
                case DialogueActionType.CompleteObjective:
                    {
                        // M11 demo: CompleteObjective → server-side mark objective complete (T-Q15 stub real impl).
                        // stringParam = questId, stageIdParam = objectiveId (quest stage id).
                        // Если quest помечен TurnInPending (active goal) → trigger QuestWorld.TryTurnIn.
                        if (string.IsNullOrEmpty(action.stringParam))
                        {
                            Debug.LogWarning("[QuestServer] FireDialogAction: CompleteObjective skipped — questId empty");
                            SendDialogActionResultToClient(clientId, new DialogActionResultDto
                            {
                                actionType = (byte)action.type,
                                success = false,
                                resultData = "missing questId"
                            });
                            break;
                        }
                        // M11 demo: CompleteObjective action → TryTurnIn (если quest active/Completed).
                        // Stage check убран — достаточно наличия quest в player log.
                        var sw = ProjectC.Quests.QuestWorld.Instance;
                        bool turnOk = false;
                        if (sw != null)
                        {
                            var playerQuests = sw.GetPlayerQuests(clientId);
                            bool questFound = false;
                            if (playerQuests != null)
                            {
                                foreach (var p in playerQuests)
                                {
                                    if (p.questId == action.stringParam) { questFound = true; break; }
                                }
                            }
                            if (questFound)
                            {
                                if (debugMode) Debug.Log($"[QuestServer] FireDialogAction: CompleteObjective quest={action.stringParam} → TryTurnIn");
                                var turnRes = sw.TryTurnIn(clientId, action.stringParam, string.Empty);
                                turnOk = turnRes.code == (byte)QuestResultCode.Ok;
                                if (turnOk)
                                {
                                    // Push snapshot so client sees quest in TurnedIn
                                    SendQuestSnapshotToClient(clientId);
                                }
                            }
                            else if (debugMode)
                            {
                                Debug.Log($"[QuestServer] FireDialogAction: CompleteObjective quest={action.stringParam} — not in player log, stub");
                            }
                        }
                        SendDialogActionResultToClient(clientId, new DialogActionResultDto
                        {
                            actionType = (byte)action.type,
                            success = turnOk,
                            resultData = action.stringParam
                        });
                    }
                    break;
                case DialogueActionType.DiscoverQuest:
                case DialogueActionType.SetFlag:
                case DialogueActionType.SwitchDialogTree:
                case DialogueActionType.EndConversation:
                    {
                        // T-Q15: stubs — SetFlag/SwitchDialogTree/EndConversation/DiscoverQuest — handled elsewhere or out of scope.
                        if (debugMode) Debug.Log($"[QuestServer] FireDialogAction: {action.type} (T-Q15 stub — T-Q18+ fill)");
                        SendDialogActionResultToClient(clientId, new DialogActionResultDto
                        {
                            actionType = (byte)action.type,
                            success = true
                        });
                    }
                    break;
                case DialogueActionType.GiveItem:
                case DialogueActionType.TakeItem:
                    {
                        // T-Q15: route to InventoryServer. ItemType сериализуется как byte в DTO
                        // (для будущего GiveCargoItem будет ItemType.Cargo).
                        // T-Q27: prefer action.itemId (explicit) over action.stringParam (legacy name).
                        var itemType = action.itemType;
                        // Default to Resources если явно None (== 0 enum default).
                        if ((int)itemType == 0) itemType = ProjectC.Items.ItemType.Resources;
                        bool ok;
                        if (action.type == DialogueActionType.GiveItem)
                        {
                            // T-Q27: resolve itemId — prefer action.itemId, fallback на stringParam parse, fallback на name lookup.
                            int itemId = action.itemId;
                            if (itemId == 0 && !string.IsNullOrEmpty(action.stringParam))
                            {
                                if (!int.TryParse(action.stringParam, out itemId))
                                {
                                    // Legacy: stringParam = item name → lookup via ItemRegistry.
                                    itemId = ProjectC.Quests.QuestWorld.ResolveItemId(action.stringParam);
                                }
                            }
                            if (itemId <= 0)
                            {
                                Debug.LogWarning($"[QuestServer] FireDialogAction: GiveItem skipped — invalid itemId='{action.stringParam}' (itemId field={action.itemId})");
                                SendDialogActionResultToClient(clientId, new DialogActionResultDto
                                {
                                    actionType = (byte)action.type,
                                    success = false,
                                    resultData = $"invalid itemId='{action.stringParam}'"
                                });
                                break;
                            }
                            // AddItemDirect: server-add to inventory.
                            if (ProjectC.Items.InventoryWorld.Instance != null)
                            {
                                var r = ProjectC.Items.InventoryWorld.Instance.AddItemDirect(clientId, itemId, itemType);
                                ok = r.IsSuccess;
                                if (debugMode) Debug.Log($"[QuestServer] FireDialogAction: GiveItem id={itemId} x{action.intParam} → {r.code} ({r.message})");
                            }
                            else { ok = false; }
                        }
                        else
                        {
                            // T-Q27: resolve itemId — same as GiveItem (prefer action.itemId, fallback parse).
                            int itemId = action.itemId;
                            if (itemId == 0 && !string.IsNullOrEmpty(action.stringParam))
                            {
                                if (!int.TryParse(action.stringParam, out itemId))
                                {
                                    itemId = ProjectC.Quests.QuestWorld.ResolveItemId(action.stringParam);
                                }
                            }
                            if (itemId <= 0)
                            {
                                Debug.LogWarning($"[QuestServer] FireDialogAction: TakeItem skipped — invalid itemId='{action.stringParam}' (itemId field={action.itemId})");
                                SendDialogActionResultToClient(clientId, new DialogActionResultDto
                                {
                                    actionType = (byte)action.type,
                                    success = false,
                                    resultData = $"invalid itemId='{action.stringParam}'"
                                });
                                break;
                            }
                            // TryRemove (T-Q14): server-remove from inventory.
                            if (ProjectC.Items.Network.InventoryServer.Instance != null)
                            {
                                ok = ProjectC.Items.Network.InventoryServer.Instance.TryRemove(clientId, itemId, itemType, action.intParam);
                                if (debugMode) Debug.Log($"[QuestServer] FireDialogAction: TakeItem id={itemId} x{action.intParam} → {ok}");
                            }
                            else { ok = false; }
                        }
                        SendDialogActionResultToClient(clientId, new DialogActionResultDto
                        {
                            actionType = (byte)action.type,
                            success = ok,
                            resultData = action.stringParam
                        });
                    }
                    break;
                case DialogueActionType.GiveCredits:
                    {
                        // T-Q16: server-side modify credits via TradeWorld.Repository.
                        if (action.intParam == 0)
                        {
                            // No-op (можно использовать как тестовая action).
                            if (debugMode) Debug.Log($"[QuestServer] FireDialogAction: GiveCredits 0 (no-op)");
                            SendDialogActionResultToClient(clientId, new DialogActionResultDto
                            {
                                actionType = (byte)action.type,
                                success = true,
                                resultData = "0"
                            });
                            break;
                        }
                        var repo = ProjectC.Trade.Core.TradeWorld.Instance != null ? ProjectC.Trade.Core.TradeWorld.Instance.Repository : null;
                        if (repo == null)
                        {
                            Debug.LogWarning("[QuestServer] FireDialogAction: GiveCredits — TradeWorld.Repository == null");
                            SendDialogActionResultToClient(clientId, new DialogActionResultDto
                            {
                                actionType = (byte)action.type,
                                success = false,
                                resultData = "no repository"
                            });
                            break;
                        }
                        float currentCredits = repo.GetCredits(clientId);
                        float newCredits = Mathf.Max(0f, currentCredits + action.intParam);
                        repo.SetCredits(clientId, newCredits);
                        if (debugMode) Debug.Log($"[QuestServer] FireDialogAction: GiveCredits delta={action.intParam} {currentCredits:F0}→{newCredits:F0}");
                        // T-Q16 fix: push fresh contract snapshot so ContractClientState.credits updates UI.
                        if (ProjectC.Trade.Network.ContractServer.Instance != null)
                            ProjectC.Trade.Network.ContractServer.Instance.PushPlayerSnapshot(clientId);
                        // M11 fix: push inventory snapshot with updated credits (InventorySnapshotDto.credits now reads from Repository).
                        if (ProjectC.Items.Network.InventoryServer.Instance != null)
                            ProjectC.Items.Network.InventoryServer.Instance.PushSnapshot(clientId);
                        SendDialogActionResultToClient(clientId, new DialogActionResultDto
                        {
                            actionType = (byte)action.type,
                            success = true,
                            resultData = action.intParam.ToString(),
                            intParam = action.intParam  // T-Q25
                        });
                    }
                    break;
                case DialogueActionType.AddReputation:
                    {
                        // T-Q16: server-side modify reputation via QuestWorld (T-Q13).
                        if (QuestWorld.Instance == null) break;
                        int delta = action.intParam;
                        var faction = action.factionParam;
                        if (faction == ProjectC.Factions.FactionId.None)
                        {
                            Debug.LogWarning($"[QuestServer] FireDialogAction: AddReputation skipped — faction=None");
                            SendDialogActionResultToClient(clientId, new DialogActionResultDto
                            {
                                actionType = (byte)action.type,
                                success = false,
                                resultData = "faction=None"
                            });
                            break;
                        }
                        int newValue = QuestWorld.Instance.ModifyReputation(clientId, faction, delta);
                        if (debugMode) Debug.Log($"[QuestServer] FireDialogAction: AddReputation faction={faction} delta={delta} newValue={newValue}");
                        // M11 fix: push reputation snapshot to client so CharacterWindow updates.
                        BroadcastReputationChange(clientId);
                        SendDialogActionResultToClient(clientId, new DialogActionResultDto
                        {
                            actionType = (byte)action.type,
                            success = true,
                            resultData = $"{faction}:{newValue}",
                            intParam = delta  // T-Q25
                        });
                    }
                    break;
                case DialogueActionType.AddNpcAttitude:
                    {
                        // T-Q16: server-side modify npc attitude via QuestWorld (T-Q13).
                        if (QuestWorld.Instance == null) break;
                        string targetNpcId = action.stringParam;
                        if (string.IsNullOrEmpty(targetNpcId))
                        {
                            Debug.LogWarning($"[QuestServer] FireDialogAction: AddNpcAttitude skipped — npcId empty");
                            SendDialogActionResultToClient(clientId, new DialogActionResultDto
                            {
                                actionType = (byte)action.type,
                                success = false,
                                resultData = "npcId empty"
                            });
                            break;
                        }
                        int delta = action.intParam;
                        int newAttitude = QuestWorld.Instance.ModifyNpcAttitude(clientId, targetNpcId, delta);
                        if (debugMode) Debug.Log($"[QuestServer] FireDialogAction: AddNpcAttitude npc={targetNpcId} delta={delta} newValue={newAttitude}");
                        // M11 fix: push npc attitude snapshot so dialog badge updates.
                        BroadcastNpcAttitudeChange(clientId);
                        // ModifyNpcAttitude already publishes NpcAttitudeChangedEvent + broadcast + cross-faction.
                        SendDialogActionResultToClient(clientId, new DialogActionResultDto
                        {
                            actionType = (byte)action.type,
                            success = true,
                            resultData = $"{targetNpcId}:{newAttitude}",
                            intParam = delta  // T-Q25
                        });
                    }
                    break;
                case DialogueActionType.GiveCargoItem:
                case DialogueActionType.TakeCargoItem:
                case DialogueActionType.FailQuest:
                    if (debugMode) Debug.Log($"[QuestServer] FireDialogAction: {action.type} (T-Q10 stub — T-Q15/T-Q16 fill)");
                    SendDialogActionResultToClient(clientId, new DialogActionResultDto
                    {
                        actionType = (byte)action.type,
                        success = true
                    });
                    break;
            }
        }

        private void SendDialogStepToClient(ulong clientId, DialogStepDto step)
        {
            var netPlayer = FindNetworkPlayer(clientId);
            if (netPlayer == null)
            {
                if (debugMode) Debug.LogWarning($"[QuestServer] SendDialogStepToClient: no NetworkPlayer for client {clientId}");
                return;
            }
            netPlayer.ReceiveDialogStepTargetRpc(step);
        }
        private void SendDialogActionResultToClient(ulong clientId, DialogActionResultDto result)
        {
            var netPlayer = FindNetworkPlayer(clientId);
            if (netPlayer == null) return;
            netPlayer.ReceiveDialogActionResultTargetRpc(result);
        }
    }
}
