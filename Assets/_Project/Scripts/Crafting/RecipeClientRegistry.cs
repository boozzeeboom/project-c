// Project C: Crafting — T-KNOWLEDGE-V2 Phase B
// RecipeClientRegistry: client-side recipeId → RecipeData lookup.
// Populated at startup from Resources/Crafting/Recipes/.
// Pattern: same as SkillsClientState R4 cache (LoadAll<SkillNodeConfig>).

using System.Collections.Generic;
using UnityEngine;

namespace ProjectC.Crafting
{
    public static class RecipeClientRegistry
    {
        private static Dictionary<int, RecipeData> _recipesById;
        private static Dictionary<RecipeData, int> _idsByRecipe;
        private static bool _loaded;

        /// <summary>Force load recipe registry (call once from client startup).</summary>
        public static void EnsureLoaded()
        {
            if (_loaded) return;
            _recipesById = new Dictionary<int, RecipeData>();
            _idsByRecipe = new Dictionary<RecipeData, int>();

            var allRecipes = Resources.LoadAll<RecipeData>("Crafting/Recipes");
            int nextId = 1;
            foreach (var r in allRecipes)
            {
                if (r == null) continue;
                int id = nextId++;
                _recipesById[id] = r;
                _idsByRecipe[r] = id;
            }
            _loaded = true;
            Debug.Log($"[RecipeClientRegistry] Loaded {_recipesById.Count} recipes from Resources/Crafting/Recipes/");
        }

        public static RecipeData GetRecipe(int recipeId)
        {
            EnsureLoaded();
            _recipesById.TryGetValue(recipeId, out var r);
            return r;
        }

        public static int GetRecipeId(RecipeData recipe)
        {
            EnsureLoaded();
            return recipe != null && _idsByRecipe.TryGetValue(recipe, out int id) ? id : -1;
        }
    }
}
