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
        public ReputationSnapshotDto? CurrentReputation { get; private set; }
        public NpcAttitudeSnapshotDto? CurrentNpcAttitude { get; private set; }

        // ============ Last action result (для UI feedback) ============
        public QuestResultDto? LastResult { get; private set; }
        public ReputationResultDto? LastReputationResult { get; private set; }

        // ============ Events для UI ============
        public event Action<QuestSnapshotDto> OnSnapshotUpdated;
        public event Action<ReputationSnapshotDto> OnReputationUpdated;
        public event Action<NpcAttitudeSnapshotDto> OnNpcAttitudeUpdated;
        public event Action<QuestResultDto> OnQuestResult;
        public event Action<ReputationResultDto> OnReputationResult;
        /// <summary>T-Q07: EventDriven quest auto-discovered. Args: (questId, displayName).</summary>
        public event Action<string, string> OnQuestDiscovered;
        /// <summary>T-Q10: server pushed new dialog step. Args: DialogStepDto. UI: show options / close window if isEnd.</summary>
        public event Action<DialogStepDto> OnDialogStepReceived;
        /// <summary>T-Q10: server pushed dialog action result (e.g. "quest offered: find_artifact").</summary>
        public event Action<DialogActionResultDto> OnDialogActionResultReceived;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                if (dontDestroyOnLoad) DontDestroyOnLoad(gameObject);
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
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

        public void OnReputationSnapshotReceived(ReputationSnapshotDto snapshot)
        {
            CurrentReputation = snapshot;
            OnReputationUpdated?.Invoke(snapshot);
            if (Debug.isDebugBuild) Debug.Log($"[QuestClientState] OnReputationSnapshotReceived: {snapshot.entries?.Length ?? 0} factions");
        }

        public void OnNpcAttitudeSnapshotReceived(NpcAttitudeSnapshotDto snapshot)
        {
            CurrentNpcAttitude = snapshot;
            OnNpcAttitudeUpdated?.Invoke(snapshot);
            if (Debug.isDebugBuild) Debug.Log($"[QuestClientState] OnNpcAttitudeSnapshotReceived: {snapshot.entries?.Length ?? 0} NPCs");
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
