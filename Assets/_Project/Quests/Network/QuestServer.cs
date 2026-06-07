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

using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using ProjectC.Core;
using ProjectC.Quests.Dto;
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
        /// returns first DialogueStepDto. T-Q10 fills the snapshot construction.
        /// </summary>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void RequestTalkToNpcRpc(string npcId, RpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;
            if (!CheckRateLimit(clientId)) return;
            if (QuestWorld.Instance == null) return;
            if (debugMode) Debug.Log($"[QuestServer] RequestTalkToNpc client={clientId} npc={npcId}");
            // T-Q10: QuestWorld.StartDialog(clientId, npcId) → builds DialogueStepDto → sends to NetworkPlayer.ReceiveDialogueStepTargetRpc.
        }

        /// <summary>
        /// Player picked an option in dialog. Server validates option index, fires action,
        /// returns next DialogueStepDto (or end marker).
        /// </summary>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void RequestAdvanceDialogueRpc(string dialogTreeId, string currentNodeId, int optionIndex, string talkingToNpcId, RpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;
            if (!CheckRateLimit(clientId)) return;
            if (QuestWorld.Instance == null) return;
            if (debugMode) Debug.Log($"[QuestServer] RequestAdvanceDialogue client={clientId} tree={dialogTreeId} node={currentNodeId} option={optionIndex}");
            // T-Q10: QuestWorld.AdvanceDialogue(clientId, treeId, currentNodeId, optionIndex).
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
    }
}
