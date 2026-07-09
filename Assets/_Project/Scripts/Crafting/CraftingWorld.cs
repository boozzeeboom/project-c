// CraftingWorld.cs (T-C02) - server-only static facade. Authoritative state for ALL crafting jobs.
// Pattern: GatheringServer registry + MetaRequirementRegistry singleton registry style.
// Subscribes to CraftingTimeService.OnTick via CraftingServer OnNetworkSpawn.
//
// NOTE: CraftingStation is T-C04 (not yet created). We use a forward reference via object +
// late-binding. To avoid hard dependency, we accept UnityEngine.Component for the registry and
// resolve via GetComponentInParent in T-C04 hooks.
using System.Collections.Generic;
using UnityEngine;

namespace ProjectC.Crafting
{
    /// <summary>Server-only. Holds all recipe -> int-id mapping + all active station jobs.
    /// Created/initialized by CraftingServer.OnNetworkSpawn; Shutdown by OnNetworkDespawn.</summary>
    public static class CraftingWorld
    {
        // ----- Recipe registry (recipeData -> compact int id, like InventoryWorld._itemDatabase) -----
        private static Dictionary<int, RecipeData> _recipesById = new Dictionary<int, RecipeData>();
        private static Dictionary<RecipeData, int> _idsByRecipe = new Dictionary<RecipeData, int>();
        private static int _nextRecipeId = 1;

        // T2: Item registry удалён — используем InventoryWorld.Instance.GetOrRegisterItemId() / GetItemDefinition()
        // во избежание двойного маппинга ItemData→int.

        // ----- Station registry (stationNetId -> MonoBehaviour; cast to CraftingStation in T-C04) -----
        // Using MonoBehaviour here avoids forward dependency on T-C04. CraftingServer/T-C04 registers
        // the actual CraftingStation component; we just hold the reference.
        private static Dictionary<ulong, MonoBehaviour> _stations = new Dictionary<ulong, MonoBehaviour>();

        // ----- Job registry (stationNetId -> CraftingJob, server-only state) -----
        private static Dictionary<ulong, CraftingJob> _jobs = new Dictionary<ulong, CraftingJob>();

        public static bool IsInitialized { get; private set; }

        // ==========================================================
        // Lifecycle
        // ==========================================================
        public static void CreateAndInitialize()
        {
            if (IsInitialized) return;
            _recipesById.Clear();
            _idsByRecipe.Clear();
            _stations.Clear();
            _jobs.Clear();
            _nextRecipeId = 1;
            IsInitialized = true;
        }

        public static void Shutdown()
        {
            _recipesById.Clear();
            _idsByRecipe.Clear();
            _stations.Clear();
            _jobs.Clear();
            IsInitialized = false;
        }

        // ==========================================================
        // Recipe registry
        // ==========================================================
        /// <summary>Register a RecipeData asset. Returns compact int id (used in DTOs).</summary>
        public static int RegisterRecipe(RecipeData recipe)
        {
            if (recipe == null) return -1;
            if (_idsByRecipe.TryGetValue(recipe, out int existing)) return existing;
            int id = _nextRecipeId++;
            _idsByRecipe[recipe] = id;
            _recipesById[id] = recipe;
            return id;
        }

        public static RecipeData GetRecipe(int recipeId)
        {
            _recipesById.TryGetValue(recipeId, out var r);
            return r;
        }

        // ==========================================================
        // Station registry (T-C04 replaces MonoBehaviour with CraftingStation)
        // T2: Item registry moved to InventoryWorld — см. InventoryWorld.GetOrRegisterItemId() / GetItemDefinition()
        // ==========================================================
        public static void RegisterStation(ulong netId, MonoBehaviour station)
        {
            if (station == null) return;
            _stations[netId] = station;
            if (!_jobs.ContainsKey(netId)) _jobs[netId] = new CraftingJob { StationNetId = netId, State = CraftingJobState.Empty };
        }

        public static void UnregisterStation(ulong netId)
        {
            _stations.Remove(netId);
            _jobs.Remove(netId);
        }

        /// <summary>Returns the station as a Component. Cast to CraftingStation in T-C04 callers.</summary>
        public static MonoBehaviour GetStationRaw(ulong netId)
        {
            _stations.TryGetValue(netId, out var s);
            return s;
        }

        public static CraftingJob GetJob(ulong stationNetId)
        {
            _jobs.TryGetValue(stationNetId, out var j);
            return j;
        }

        // ==========================================================
        // Server tick (called by CraftingTimeService.OnTick)
        // ==========================================================
        public static void OnTick(float serverTime)
        {
            if (!IsInitialized) return;
            // Copy keys: jobs may be modified mid-iteration (CompleteCraft clears)
            var keys = new List<ulong>(_jobs.Keys);
            for (int i = 0; i < keys.Count; i++)
            {
                var job = _jobs[keys[i]];
                if (job == null || job.State != CraftingJobState.InProgress) continue;
                if (serverTime - job.StartTime >= job.Duration)
                {
                    if (_stations.TryGetValue(keys[i], out var st) && st != null)
                    {
                        // T1: прямой вызов вместо reflection (CraftingStation.CompleteCraft уже public)
                        var cs = st as CraftingStation;
                        if (cs != null) cs.CompleteCraft();
                    }
                }
            }
        }
    }
}