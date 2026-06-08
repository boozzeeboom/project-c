// T-Q15: ContractMetaBridge — server-side singleton. Подписывается на ContractAcceptedEvent /
// ContractCompletedEvent / ContractFailedEvent через WorldEventBus, помечает соответствующие
// markers в QuestWorld, и вызывает QuestTriggerService.Evaluate(...) для продвижения quest
// objectives вроде "доставить cargo в порт X".
//
// См. docs/NPC_quests/08_ROADMAP.md T-Q15, 09_OPEN_QUESTIONS.md §A2.
//
// Pattern: scene-placed в BootstrapScene (DontDestroyOnLoad, как ReputationClientState).

using System;
using UnityEngine;
using ProjectC.Core;
using ProjectC.Quests;

namespace ProjectC.Quests.Bridges
{
    /// <summary>
    /// T-Q15: server-side мост между Trade (ContractServer) и Quest (QuestWorld/Triggers).
    /// On contract accept/complete/fail → mark в QuestWorld + evaluate triggers.
    /// </summary>
    public class ContractMetaBridge : MonoBehaviour
    {
        public static ContractMetaBridge Instance { get; private set; }

        // Cached delegates (для корректного Unsubscribe).
        private Action<ContractAcceptedEvent> _onAccepted;
        private Action<ContractCompletedEvent> _onCompleted;
        private Action<ContractFailedEvent> _onFailed;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            if (Instance != this) { Destroy(this); return; }

            // T-Q15: scene-placed в BootstrapScene, должен переживать scene loads
            // (когда ClientSceneLoader подгружает WorldScene_0_0 additive, bootstrap scene не unload'ится,
            // но defensive DontDestroyOnLoad страхует от будущих сценариев unload).
            // Вызываем ТОЛЬКО в Play Mode (DontDestroyOnLoad в editor mode throws).
            if (Application.isPlaying) DontDestroyOnLoad(gameObject);

            _onAccepted = HandleContractAccepted;
            _onCompleted = HandleContractCompleted;
            _onFailed = HandleContractFailed;
        }

        private void OnEnable()
        {
            WorldEventBus.Subscribe(_onAccepted);
            WorldEventBus.Subscribe(_onCompleted);
            WorldEventBus.Subscribe(_onFailed);
            Debug.Log("[ContractMetaBridge] Subscribed to 3 contract events.");
        }

        private void OnDisable()
        {
            WorldEventBus.Unsubscribe(_onAccepted);
            WorldEventBus.Unsubscribe(_onCompleted);
            WorldEventBus.Unsubscribe(_onFailed);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ============ Event handlers ============

        private void HandleContractAccepted(ContractAcceptedEvent ev)
        {
            if (QuestWorld.Instance == null) return;
            QuestWorld.Instance.MarkContractAccepted(ev.PlayerId, ev.ContractId);
            int advances = QuestWorld.Instance.TriggerService?.Evaluate(ev.PlayerId, $"ContractAccepted:{ev.ContractId}") ?? 0;
            if (advances > 0 || Debug.isDebugBuild)
                Debug.Log($"[ContractMetaBridge] OnContractAccepted: client={ev.PlayerId} contract={ev.ContractId} fromNpc={ev.FromNpcId} → {advances} objective(s) advanced");
        }

        private void HandleContractCompleted(ContractCompletedEvent ev)
        {
            if (QuestWorld.Instance == null) return;
            QuestWorld.Instance.MarkContractCompleted(ev.PlayerId, ev.ContractId);
            int advances = QuestWorld.Instance.TriggerService?.Evaluate(ev.PlayerId, $"ContractCompleted:{ev.ContractId}") ?? 0;
            if (advances > 0 || Debug.isDebugBuild)
                Debug.Log($"[ContractMetaBridge] OnContractCompleted: client={ev.PlayerId} contract={ev.ContractId} wasReceipt={ev.WasReceipt} → {advances} objective(s) advanced");
        }

        private void HandleContractFailed(ContractFailedEvent ev)
        {
            if (QuestWorld.Instance == null) return;
            // T-Q15: failed contracts do not auto-advance objectives (quest designer decides).
            // Но event полезен для future penalty tracking / quest prerequisite "contract B completed within last 24h".
            if (Debug.isDebugBuild)
                Debug.Log($"[ContractMetaBridge] OnContractFailed: client={ev.PlayerId} contract={ev.ContractId} debtIncurred={ev.DebtIncurred} (no auto-advance)");
        }
    }
}
