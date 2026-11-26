// T-Q07: QuestClientState — клиентский singleton для quest/reputation/attitude state.
// Pattern: ContractClientState (Trade), MarketClientState.
// См. docs/NPC_quests/02_V2_ARCHITECTURE.md §2.5.

using System;
using UnityEngine;
using ProjectC.Quests.Dto;

namespace ProjectC.Quests.Client
{
    /// <summary>
    /// Client-side projection of server quest state. Один инстанс на клиентский процесс.
    /// Получает snapshot'ы и result'ы от QuestServer через NetworkPlayer.ReceiveXxxTargetRpc.
    /// UI читает только из этого класса (single source of truth на клиенте).
    /// </summary>
    /// <remarks>
    /// Auto-spawn: T-Q11 (пока — создаётся вручную через GameObject в BootstrapScene или first RPC arrival).
    /// Server push: ReceiveXxxTargetRpc → OnXxxReceived → update state + fire event.
    /// </remarks>
    public class QuestClientState : MonoBehaviour
    {
        public static QuestClientState Instance { get; private set; }

        [Header("Lifecycle")]
        [SerializeField] private bool dontDestroyOnLoad = true;

        // ============ Quest state ============
        public QuestSnapshotDto? CurrentSnapshot { get; private set; }
        public MerchantShopSnapshotDto? CurrentMerchantShop { get; private set; }
        public BlacksmithShopSnapshotDto? CurrentBlacksmithShop { get; private set; }

        // ============ Last action result (для UI feedback) ============
        public QuestResultDto? LastResult { get; private set; }
        public ReputationResultDto? LastReputationResult { get; private set; }
        public MerchantTradeResultDto? LastMerchantTradeResult { get; private set; }
        public BlacksmithUpgradeResultDto? LastBlacksmithUpgradeResult { get; private set; }

        // ============ Events для UI ============
        public event Action<QuestSnapshotDto> OnSnapshotUpdated;
        public event Action<QuestResultDto> OnQuestResult;
        public event Action<ReputationResultDto> OnReputationResult;
        /// <summary>T-Q07: EventDriven quest auto-discovered. Args: (questId, displayName).</summary>
        public event Action<string, string> OnQuestDiscovered;
        /// <summary>T-Q10: server pushed new dialog step. Args: DialogStepDto. UI: show options / close window if isEnd.</summary>
        public event Action<DialogStepDto> OnDialogStepReceived;
        /// <summary>T-Q10: server pushed dialog action result (e.g. "quest offered: find_artifact").</summary>
        public event Action<DialogActionResultDto> OnDialogActionResultReceived;
        /// <summary>T-MERCH01: server pushed merchant shop snapshot for the right panel of DialogWindow.</summary>
        public event Action<MerchantShopSnapshotDto> OnMerchantShopReceived;
        /// <summary>T-MERCH01: server pushed merchant trade buy/sell result.</summary>
        public event Action<MerchantTradeResultDto> OnMerchantTradeResultReceived;
        /// <summary>T-SMITH01: server pushed blacksmith shop snapshot for DialogWindow.</summary>
        public event Action<BlacksmithShopSnapshotDto> OnBlacksmithShopReceived;
        /// <summary>T-SMITH01: server pushed blacksmith upgrade result.</summary>
        public event Action<BlacksmithUpgradeResultDto> OnBlacksmithUpgradeResultReceived;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                if (dontDestroyOnLoad) DontDestroyOnLoad(gameObject);
                Debug.Log($"[QuestClientState] Awake: Instance set, GO={gameObject.name}, scene={gameObject.scene.name}, dddl={dontDestroyOnLoad}");
            }
            else if (Instance != this)
            {
                Debug.LogWarning($"[QuestClientState] Awake: DUPLICATE found, destroying new instance. OldInstanceGO={Instance.gameObject.name}, NewInstanceGO={gameObject.name}");
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            Debug.Log($"[QuestClientState] OnDestroy: GO={gameObject.name}, isInstance={Instance == this}");
            if (Instance == this) Instance = null;
        }

        // ============================================================
        // Server → Client handlers (called from NetworkPlayer.ReceiveXxxTargetRpc)
        // ============================================================

        public void OnQuestSnapshotReceived(QuestSnapshotDto snapshot)
        {
            CurrentSnapshot = snapshot;
            OnSnapshotUpdated?.Invoke(snapshot);
            if (Debug.isDebugBuild) Debug.Log($"[QuestClientState] OnQuestSnapshotReceived: {snapshot.quests?.Length ?? 0} quests");
        }

        public void OnQuestResultReceived(QuestResultDto result)
        {
            LastResult = result;
            OnQuestResult?.Invoke(result);
            if (Debug.isDebugBuild) Debug.Log($"[QuestClientState] OnQuestResultReceived: code={result.code}");
        }

        public void OnReputationResultReceived(ReputationResultDto result)
        {
            LastReputationResult = result;
            OnReputationResult?.Invoke(result);
            if (Debug.isDebugBuild) Debug.Log($"[QuestClientState] OnReputationResultReceived: code={result.code}");
        }

        /// <summary>Public raiser for OnQuestDiscovered (T-Q07: server-push). Вызывается из NetworkPlayer.ReceiveQuestDiscoveredTargetRpc.</summary>
        public void RaiseOnQuestDiscovered(string questId, string displayName)
        {
            OnQuestDiscovered?.Invoke(questId, displayName);
            if (Debug.isDebugBuild) Debug.Log($"[QuestClientState] RaiseOnQuestDiscovered: {questId} '{displayName}'");
        }

        /// <summary>Public raiser for OnDialogStepReceived (T-Q10). Вызывается из NetworkPlayer.ReceiveDialogStepTargetRpc.</summary>
        public void RaiseOnDialogStepReceived(DialogStepDto step)
        {
            OnDialogStepReceived?.Invoke(step);
            if (Debug.isDebugBuild) Debug.Log($"[QuestClientState] RaiseOnDialogStepReceived: tree={step.treeId} node={step.nodeId} isEnd={step.isEnd}");
        }

        /// <summary>Public raiser for OnDialogActionResultReceived (T-Q10). Вызывается из NetworkPlayer.ReceiveDialogActionResultTargetRpc.</summary>
        public void RaiseOnDialogActionResultReceived(DialogActionResultDto result)
        {
        OnDialogActionResultReceived?.Invoke(result);
        if (Debug.isDebugBuild) Debug.Log($"[QuestClientState] RaiseOnDialogActionResultReceived: type={result.actionType} success={result.success}");
        }

        /// <summary>T-MERCH01: Server → client handler для снимка магазина NPC.</summary>
        public void RaiseOnMerchantShopReceived(MerchantShopSnapshotDto snapshot)
        {
            CurrentMerchantShop = snapshot;
            OnMerchantShopReceived?.Invoke(snapshot);
            if (Debug.isDebugBuild)
                Debug.Log($"[QuestClientState] RaiseOnMerchantShopReceived: npc={snapshot.npcId} items={snapshot.shopItems?.Length ?? 0} sellable={snapshot.playerSellableItems?.Length ?? 0}");
        }

        /// <summary>T-MERCH01: Server → client handler для результата покупки/продажи у NPC.</summary>
        public void RaiseOnMerchantTradeResultReceived(MerchantTradeResultDto result)
        {
            LastMerchantTradeResult = result;
            OnMerchantTradeResultReceived?.Invoke(result);
            if (Debug.isDebugBuild)
                Debug.Log($"[QuestClientState] RaiseOnMerchantTradeResultReceived: npc={result.npcId} isBuy={result.isBuy} code={result.code} msg='{result.message}'");
        }

        // ============================================================
        // T-MERCH01: client-side merchant forwarders (DialogWindow → QuestServer)
        // ============================================================

        public void RequestMerchantShop(string npcId)
        {
            if (string.IsNullOrEmpty(npcId) || QuestServer.Instance == null) return;
            QuestServer.Instance.RequestMerchantShopRpc(npcId);
        }

        public void RequestMerchantBuy(string npcId, int itemId, int quantity)
        {
            if (string.IsNullOrEmpty(npcId) || itemId <= 0 || quantity <= 0 || QuestServer.Instance == null) return;
            QuestServer.Instance.RequestMerchantBuyRpc(npcId, itemId, quantity);
        }

        public void RequestMerchantSell(string npcId, int itemId, int quantity)
        {
            if (string.IsNullOrEmpty(npcId) || itemId <= 0 || quantity <= 0 || QuestServer.Instance == null) return;
            QuestServer.Instance.RequestMerchantSellRpc(npcId, itemId, quantity);
        }

        /// <summary>T-SMITH01: Server → client handler для снимка кузницы NPC.</summary>
        public void RaiseOnBlacksmithShopReceived(BlacksmithShopSnapshotDto snapshot)
        {
            CurrentBlacksmithShop = snapshot;
            OnBlacksmithShopReceived?.Invoke(snapshot);
            if (Debug.isDebugBuild)
                Debug.Log($"[QuestClientState] RaiseOnBlacksmithShopReceived: npc={snapshot.npcId} items={snapshot.playerItems?.Length ?? 0}");
        }

        /// <summary>T-SMITH01: Server → client handler для результата улучшения у кузнеца.</summary>
        public void RaiseOnBlacksmithUpgradeResultReceived(BlacksmithUpgradeResultDto result)
        {
            LastBlacksmithUpgradeResult = result;
            OnBlacksmithUpgradeResultReceived?.Invoke(result);
            if (Debug.isDebugBuild)
                Debug.Log($"[QuestClientState] RaiseOnBlacksmithUpgradeResultReceived: npc={result.npcId} code={result.code} msg='{result.message}'");
        }

        public void RequestBlacksmithShop(string npcId)
        {
            if (string.IsNullOrEmpty(npcId) || QuestServer.Instance == null) return;
            QuestServer.Instance.RequestBlacksmithShopRpc(npcId);
        }

        public void RequestBlacksmithUpgrade(string npcId, int targetItemId, BlacksmithUpgradeTier tier, BlacksmithPaymentMode paymentMode)
        {
            if (string.IsNullOrEmpty(npcId) || targetItemId <= 0 || QuestServer.Instance == null) return;
            QuestServer.Instance.RequestBlacksmithUpgradeRpc(npcId, targetItemId, (byte)tier, (byte)paymentMode);
        }

        // ============================================================
        // T-Q11: client-side request forwarders (UI → server RPC)
        // ============================================================

        /// <summary>
        /// T-Q11: игрок нажал "Принять" на discovered-квесте в CharacterWindow.
        /// Forward в <see cref="QuestServer.RequestAcceptQuestRpc"/>.
        /// Серверная сторона (T-Q15: QuestWorld.TryAccept) ещё не реализована —
        /// RPC дойдёт, rate-limit пройдёт, но state не сменится. UI работает.
        /// </summary>
        /// <param name="questId">questId из QuestSnapshotDto.quests[i].questId</param>
        /// <param name="fromNpcId">NPC, у которого игрок принял квест (пустая строка для EventDriven квестов, у которых нет "offering NPC")</param>
        public void RequestAcceptQuest(string questId, string fromNpcId)
        {
        if (string.IsNullOrEmpty(questId))
        {
        Debug.LogWarning("[QuestClientState] RequestAcceptQuest: questId is empty");
        return;
        }
        var server = QuestServer.Instance;
        if (server == null)
        {
        Debug.LogWarning("[QuestClientState] RequestAcceptQuest: QuestServer.Instance == null (host not started?)");
        return;
        }
        server.RequestAcceptQuestRpc(questId, fromNpcId ?? "");
        if (Debug.isDebugBuild) Debug.Log($"[QuestClientState] RequestAcceptQuest: quest={questId} fromNpc={fromNpcId}");
        }

        // ============================================================
        // T-Q11a: auto-spawn on scene load (host + client)
        // ============================================================

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoSpawn()
        {
            // T-Q11b-fix: если instance уже существует (scene-placed в BootstrapScene), не спавним новый.
            if (Instance != null) return;

            // Дополнительная проверка — найдём root [QuestClientState] GameObject в scene.
            var existingRoot = GameObject.Find("[QuestClientState]");
            if (existingRoot != null && existingRoot.GetComponent<QuestClientState>() != null)
            {
                return; // уже есть в scene
            }

            var go = new GameObject("[QuestClientState]");
            go.AddComponent<QuestClientState>();
            UnityEngine.Object.DontDestroyOnLoad(go);
            // Auto-attach DialogWindow sibling
            if (go.GetComponent<UI.DialogWindow>() == null)
            {
                go.AddComponent<UI.DialogWindow>();
            }
            if (Debug.isDebugBuild) Debug.Log("[QuestClientState] Auto-spawned on scene load");
        }
    }
}
