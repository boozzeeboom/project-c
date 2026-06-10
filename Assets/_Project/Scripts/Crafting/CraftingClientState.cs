// CraftingClientState.cs (T-C04 stub + partial T-C05 wire-in)
// В T-C05 будет полная версия: events, ServerTimeoutWatcher, RequestAdd/Start/Cancel/Collect.
// В T-C04 нужен минимум: RequestSubscribe() + Instance singleton, т.к. NetworkPlayer.TryInteractNearestCraftingStation его вызывает.
using UnityEngine;

namespace ProjectC.Crafting
{
    /// <summary>Client-only singleton. Auto-spawned by NetworkManagerController.CreateCraftingClientState (T-C05).</summary>
    public class CraftingClientState : MonoBehaviour
    {
        public static CraftingClientState Instance { get; private set; }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
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

        // ==========================================================
        // Client → Server: subscribe to station snapshot
        // ==========================================================
        public void RequestSubscribe(ulong stationNetId)
        {
            var server = CraftingServer.Instance;
            if (server == null) { Debug.LogWarning($"[CraftingClientState] RequestSubscribe: CraftingServer.Instance==null (server not started)"); return; }
            server.SubscribeStationRpc(stationNetId);
        }

        public void RequestUnsubscribe(ulong stationNetId)
        {
            var server = CraftingServer.Instance;
            if (server == null) return;
            server.UnsubscribeStationRpc(stationNetId);
        }

        // ==========================================================
        // NetworkPlayer.ReceiveCraftingResultTargetRpc target (T-C03 stub)
        // ==========================================================
        public void OnCraftingResultReceived(CraftingResultDto result)
        {
            // T-C05: route to events (OnCraftingProgress / OnCraftingCompleted / etc.)
            Debug.Log($"[CraftingClientState] Result: code={result.code} station={result.stationNetId} msg={result.message}");
        }

        public void OnCraftingSnapshotReceived(CraftingSnapshotDto snapshot)
        {
            // T-C05: cache + emit OnSnapshotUpdated event (T-C06: opens CraftingWindow)
            Debug.Log($"[CraftingClientState] Snapshot: station={snapshot.stationNetId} state={snapshot.jobState} owner={snapshot.ownerClientId} recipe={snapshot.activeRecipeId}");
        }
    }
}