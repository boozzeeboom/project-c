// CraftingWorld.cs (T-C02) - server-only static facade. Authoritative state for ALL crafting jobs.
// Pattern: GatheringServer registry + MetaRequirementRegistry singleton registry style.
// Subscribes to CraftingTimeService.OnTick via CraftingServer OnNetworkSpawn.
//
// NOTE: CraftingStation is T-C04 (not yet created). We use a forward reference via object +
// late-binding. To avoid hard dependency, we accept UnityEngine.Component for the registry and
// resolve via GetComponentInParent in T-C04 hooks.
using System.Collections.Generic;
using UnityEngine;
using ProjectC.Core;

namespace ProjectC.Crafting
{
    /// <summary>Server-only. Holds all recipe -> string-id mapping + all active station jobs.
    /// Created/initialized by CraftingServer.OnNetworkSpawn; Shutdown by OnNetworkDespawn.</summary>
    public static class CraftingWorld
    {
        // ----- Recipe registry (recipeId (string) -> recipe, V3 stable key) -----
        private static Dictionary<string, RecipeData> _recipesById = new Dictionary<string, RecipeData>();
        private static Dictionary<RecipeData, string> _idsByRecipe = new Dictionary<RecipeData, string>();

        // T2: Item registry удалён — используем InventoryWorld.Instance.GetOrRegisterItemId() / GetItemDefinition()
        // во избежание двойного маппинга ItemData→int.

        // ----- Station registry (stationNetId -> MonoBehaviour; cast to CraftingStation in T-C04) -----
        // Using MonoBehaviour here avoids forward dependency on T-C04. CraftingServer/T-C04 registers
        // the actual CraftingStation component; we just hold the reference.
        private static Dictionary<ulong, MonoBehaviour> _stations = new Dictionary<ulong, MonoBehaviour>();

        // ----- Job registry (stationNetId -> CraftingJob, server-only state) -----
        private static Dictionary<ulong, CraftingJob> _jobs = new Dictionary<ulong, CraftingJob>();

        // ----- T-KNOWLEDGE-V3: recipe knowledge (per-player) — string key -----
        private static Dictionary<ulong, HashSet<string>> _knownRecipes = new Dictionary<ulong, HashSet<string>>();

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
            _knownRecipes.Clear();
            IsInitialized = true;
        }

        public static void Shutdown()
        {
            _recipesById.Clear();
            _idsByRecipe.Clear();
            _stations.Clear();
            _jobs.Clear();
            _knownRecipes.Clear();
            IsInitialized = false;
        }

        // ==========================================================
        // Recipe registry (V3: string recipeId)
        // ==========================================================
        /// <summary>Register a RecipeData asset. Returns stable string recipeId.</summary>
        public static string RegisterRecipe(RecipeData recipe)
        {
            if (recipe == null) return null;
            if (string.IsNullOrEmpty(recipe.RecipeId))
            {
                Debug.LogError($"[CraftingWorld] RegisterRecipe: recipe '{recipe.name}' has empty recipeId — skipping.");
                return null;
            }
            if (_idsByRecipe.TryGetValue(recipe, out string existing)) return existing;
            string id = recipe.RecipeId;
            _idsByRecipe[recipe] = id;
            _recipesById[id] = recipe;
            return id;
        }

        public static RecipeData GetRecipe(string recipeId)
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
                        if (cs != null)
                        {
                            cs.CompleteCraft();

                            // L1: публикуем WorldEvent для StatsServer (XP за крафт)
                            var recipe = GetRecipe(job.RecipeId);
                            int totalQty = 0;
                            if (recipe != null && recipe.Outputs != null)
                            {
                                foreach (var o in recipe.Outputs)
                                    if (o.item != null) totalQty += o.quantity;
                            }
                            WorldEventBus.Publish(new CraftingCompletedEvent
                            {
                                PlayerId = job.OwnerClientId,
                                StationNetId = job.StationNetId,
                                RecipeId = job.RecipeId ?? "",
                                ResultItemName = job.ResultItemName ?? "",
                                Quantity = totalQty,
                                TimestampUnix = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                            });
                        }
                    }
                }
            }
        }

        // ==========================================================
        // T-KNOWLEDGE-V3: Recipe knowledge (per-player) — string key
        // ==========================================================

        private static HashSet<string> GetKnownRecipeSet(ulong clientId)
        {
            if (!_knownRecipes.TryGetValue(clientId, out var set))
            {
                set = new HashSet<string>();
                _knownRecipes[clientId] = set;
            }
            return set;
        }

        public static bool IsRecipeKnown(ulong clientId, string recipeId)
        {
            if (string.IsNullOrEmpty(recipeId)) return false;
            return GetKnownRecipeSet(clientId).Contains(recipeId);
        }

        public static bool UnlockRecipeKnowledge(ulong clientId, string recipeId)
        {
            if (string.IsNullOrEmpty(recipeId)) return false;
            var set = GetKnownRecipeSet(clientId);
            if (set.Add(recipeId))
            {
                if (Debug.isDebugBuild)
                    Debug.Log($"[CraftingWorld] Recipe knowledge unlocked: player={clientId} recipeId={recipeId}");
                return true;
            }
            return false;
        }

        public static HashSet<string> GetKnownRecipeIds(ulong clientId)
            => GetKnownRecipeSet(clientId);

        /// <summary>
        /// Apply death recipe loss: удаляет recipeId из known set если Random < lossChance.
        /// Возвращает количество потерянных рецептов.
        /// </summary>
        public static int ApplyDeathRecipeLoss(ulong clientId, float lossChance, System.Random rng)
        {
            var set = GetKnownRecipeSet(clientId);
            if (set.Count == 0) return 0;

            var toRemove = new List<string>();
            foreach (var id in set)
            {
                if (rng.NextDouble() < lossChance)
                    toRemove.Add(id);
            }
            foreach (var id in toRemove)
                set.Remove(id);

            if (toRemove.Count > 0)
                Debug.Log($"[CraftingWorld] Death recipe loss: player={clientId} lost={toRemove.Count} remaining={set.Count}");
            return toRemove.Count;
        }

        /// <summary>Build knownRecipeIds list for persistence (called by QuestWorld.BuildSaveData).</summary>
        public static List<string> BuildRecipeKnowledgeSave(ulong clientId)
        {
            var set = GetKnownRecipeSet(clientId);
            return new List<string>(set);
        }

        /// <summary>Load knownRecipeIds from persistence (called by QuestWorld.LoadPlayer).</summary>
        public static void LoadRecipeKnowledge(ulong clientId, List<string> knownRecipeIds)
        {
            var set = GetKnownRecipeSet(clientId);
            set.Clear();
            if (knownRecipeIds != null)
            {
                foreach (var id in knownRecipeIds)
                {
                    if (!string.IsNullOrEmpty(id))
                        set.Add(id);
                }
            }
            if (Debug.isDebugBuild)
                Debug.Log($"[CraftingWorld] LoadRecipeKnowledge: player={clientId} count={set.Count}");
        }
    }
}