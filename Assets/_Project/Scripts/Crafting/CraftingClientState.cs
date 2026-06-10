// CraftingClientState.cs (T-C03 stub, full impl in T-C05)
// Stub нужен чтобы NetworkPlayer.ReceiveCraftingResultTargetRpc компилировался.
// В T-C05 добавим events + RequestSubscribe/RequestAddIngredient/RequestStart/RequestCancel/RequestCollect
// + ServerTimeoutWatcher (5s без InProgress → прервано) + авто-подписку на snapshot'ы.
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
        // NetworkPlayer.ReceiveCraftingResultTargetRpc target (T-C03 stub)
        // ==========================================================
        public void OnCraftingResultReceived(CraftingResultDto result)
        {
            // T-C05: route to events (OnCraftingProgress / OnCraftingCompleted / etc.)
            Debug.Log($"[CraftingClientState] Result: code={result.code} station={result.stationNetId} msg={result.message}");
        }

        public void OnCraftingSnapshotReceived(CraftingSnapshotDto snapshot)
        {
            // T-C05: cache + emit OnSnapshotUpdated event
            Debug.Log($"[CraftingClientState] Snapshot: station={snapshot.stationNetId} state={snapshot.jobState} owner={snapshot.ownerClientId} recipe={snapshot.activeRecipeId}");
        }
    }
}