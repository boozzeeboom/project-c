// Project C: Crafting — T-KNOWLEDGE-V3
// RecipeClientRegistry: client-side recipeId (string) → RecipeData lookup.
// Populated at startup from Resources/Crafting/Recipes/.
// Pattern: same as SkillsClientState R4 cache (LoadAll<SkillNodeConfig>).

using System.Collections.Generic;
using UnityEngine;

namespace ProjectC.Crafting
{
    public static class RecipeClientRegistry
    {
        private static Dictionary<string, RecipeData> _recipesById;
        private static bool _loaded;

        /// <summary>Force load recipe registry (call once from client startup).</summary>
        public static void EnsureLoaded()
        {
            if (_loaded) return;
            _recipesById = new Dictionary<string, RecipeData>();

            var allRecipes = Resources.LoadAll<RecipeData>("Crafting/Recipes");
            foreach (var r in allRecipes)
            {
                if (r == null) continue;
                if (string.IsNullOrEmpty(r.RecipeId))
                {
                    Debug.LogWarning($"[RecipeClientRegistry] Recipe '{r.name}' has empty recipeId — skipping.");
                    continue;
                }
                if (_recipesById.ContainsKey(r.RecipeId))
                {
                    Debug.LogWarning($"[RecipeClientRegistry] Duplicate recipeId '{r.RecipeId}' — skipping '{r.name}'.");
                    continue;
                }
                _recipesById[r.RecipeId] = r;
            }
            _loaded = true;
            Debug.Log($"[RecipeClientRegistry] Loaded {_recipesById.Count} recipes from Resources/Crafting/Recipes/");
        }

        public static RecipeData GetRecipe(string recipeId)
        {
            EnsureLoaded();
            _recipesById.TryGetValue(recipeId, out var r);
            return r;
        }

        public static string GetRecipeId(RecipeData recipe)
        {
            if (recipe == null) return null;
            if (!string.IsNullOrEmpty(recipe.RecipeId)) return recipe.RecipeId;
            return null;
        }

        /// <summary>Try to find a recipe string id from a legacy int id (backward compat for loading old saves).</summary>
        public static string TryResolveLegacyId(int legacyIntId)
        {
            EnsureLoaded();
            // Legacy int ids were 1-based sequential; try to find by index
            int idx = 0;
            foreach (var kv in _recipesById)
            {
                idx++;
                if (idx == legacyIntId) return kv.Key;
            }
            return null;
        }
    }
}