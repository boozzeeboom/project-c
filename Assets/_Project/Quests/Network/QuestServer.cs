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
            QuestWorld.CreateAndInitialize(questDatabase.quests);

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

            if (debugMode)
            {
                Debug.Log($"[QuestServer] OnNetworkSpawn — IsServer=true, questDatabase={questDatabase.quests?.Length ?? 0} quests, maxActive={maxActiveQuestsPerPlayer}, maxOps/min={maxOpsPerMinute}, triggerSubs=7");
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

                if (QuestWorld.Instance != null)
                {
                    // T-Q18: SaveAll() before shutdown.
                    QuestWorld.Instance.Shutdown();
                }
            }
            if (Instance == this) Instance = null;
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
                if (debugMode) Debug.LogWarning($"[QuestServer] RequestTalkToNpc: NPC '{npcId}' has no dialog tree");
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

            // Find option
            if (optionIndex < 0 || optionIndex >= node.edges.Length)
            {
                if (debugMode) Debug.LogWarning($"[QuestServer] RequestAdvanceDialogue: option {optionIndex} out of range");
                return;
            }
            var edge = node.edges[optionIndex];

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
            // T-Q15: QuestWorld.TryAccept(clientId, questId, fromNpcId).
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
            // T-Q16: QuestWorld.TryTurnIn(clientId, questId, toNpcId).
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
            // T-Q12: QuestWorld.SetTracked(clientId, questId, track).
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
                    // Look up description from current stage's objective
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
                        currentValue = op.currentCount
                    };
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

            // Build options: each edge → option (label, available, reason)
            var edges = node.edges ?? Array.Empty<DialogueEdge>();
            var options = new DialogOptionDto[edges.Length];
            for (int i = 0; i < edges.Length; i++)
            {
                var edge = edges[i];
                if (edge == null) { options[i] = new DialogOptionDto { index = i, label = $"[edge {i} null]", available = false, unavailableReason = "edge null" }; continue; }
                bool available = EvaluateConditions(edge, clientId);
                options[i] = new DialogOptionDto
                {
                    index = i,
                    label = edge.label ?? "",
                    available = available,
                    unavailableReason = available ? "" : "Условие не выполнено"
                };
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
                    return InventoryWorld.Instance != null
                        && InventoryWorld.Instance.CountOf(clientId, 0) >= c.intParam; // T-Q15: lookup by stringParam → ItemDataId
                case DialogueConditionType.QuestStateEquals:
                    return true; // T-Q15: full quest state lookup
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
                    // T-Q15: QuestWorld.TryOffer(clientId, action.stringParam).
                    // T-Q10: log + send DialogActionResultDto to client.
                    if (debugMode) Debug.Log($"[QuestServer] FireDialogAction: OfferQuest {action.stringParam} to client {clientId}");
                    SendDialogActionResultToClient(clientId, new DialogActionResultDto
                    {
                        actionType = (byte)action.type,
                        success = true,
                        resultData = action.stringParam
                    });
                    break;
                case DialogueActionType.CompleteObjective:
                case DialogueActionType.DiscoverQuest:
                case DialogueActionType.GiveItem:
                case DialogueActionType.TakeItem:
                case DialogueActionType.GiveCredits:
                case DialogueActionType.AddReputation:
                case DialogueActionType.AddNpcAttitude:
                case DialogueActionType.OpenMarket:
                case DialogueActionType.OpenService:
                case DialogueActionType.SetFlag:
                case DialogueActionType.SwitchDialogTree:
                case DialogueActionType.EndConversation:
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
