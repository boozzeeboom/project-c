// =====================================================================================
// GatheringClientState.cs — клиентская проекция (T-G03 STUB, полная версия в T-G04)
// =====================================================================================
// Документация:
//   • docs/Mining/10_DESIGN.md §1.4
//   • docs/Mining/ROADMAP.md T-G04
//
// T-G03 создаёт STUB-версию: только singleton + RequestStartGather + OnGatherResultReceived.
// Полная версия (T-G04) добавит:
//   - Events: OnGatherProgress / OnGatherCompleted / OnGatherInterrupted / OnGatherDenied
//   - Queue + timeout
//   - Auto-spawn через NetworkManagerController
//   - UI subscription для GatheringToastController
// =====================================================================================

using UnityEngine;

namespace ProjectC.ResourceNode
{
    public class GatheringClientState : MonoBehaviour
    {
        public static GatheringClientState Instance { get; private set; }

        [SerializeField] private bool _dontDestroyOnLoad = true;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                if (_dontDestroyOnLoad) DontDestroyOnLoad(gameObject);
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

        /// <summary>Клиент → сервер: запросить старт сбора на указанном ноде.</summary>
        public void RequestStartGather(ulong nodeNetId)
        {
            if (GatheringServer.Instance == null)
            {
                Debug.LogWarning("[GatheringClientState] RequestStartGather: GatheringServer.Instance==null. " +
                                 "(сервер ещё не стартовал?)");
                return;
            }
            GatheringServer.Instance.RequestStartGatherRpc(nodeNetId);
        }

        /// <summary>Клиент → сервер: отменить активный сбор.</summary>
        public void RequestCancelGather()
        {
            if (GatheringServer.Instance == null) return;
            GatheringServer.Instance.RequestCancelGatherRpc();
        }

        /// <summary>RPC-получатель: сервер пушит результат тика (InProgress/Completed/Interrupted/Denied).
        /// T-G04 добавит события + queue + таймаут. Сейчас — логируем.</summary>
        public void OnGatherResultReceived(GatherResult result)
        {
            switch (result.Result)
            {
                case GatherResultCode.InProgress:
                    Debug.Log($"[GatheringClientState] Tick: progress={result.progress:F2}");
                    break;
                case GatherResultCode.Completed:
                    Debug.Log($"[GatheringClientState] Completed: {result.itemName} × {result.quantity}, depleted={result.isDepleted}");
                    break;
                case GatherResultCode.Interrupted:
                    Debug.Log($"[GatheringClientState] Interrupted: {result.reason}");
                    break;
                case GatherResultCode.Denied:
                    Debug.LogWarning($"[GatheringClientState] Denied: {result.reason}");
                    break;
                case GatherResultCode.Cancelled:
                    Debug.Log("[GatheringClientState] Cancelled");
                    break;
            }
        }
    }
}
