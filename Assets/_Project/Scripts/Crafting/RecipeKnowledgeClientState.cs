// Project C: Crafting — T-KNOWLEDGE-V2
// RecipeKnowledgeClientState: client-side projection of known recipe IDs.
// Auto-spawned singleton (pattern: SkillsClientState, CraftingClientState).
// Design: docs/Character/Knowledges/05_KNOWLEDGE_SYSTEM_V2_RESEARCH_REVIEW.md §4.5

using System;
using System.Collections.Generic;
using UnityEngine;
using ProjectC.Crafting.Dto;

namespace ProjectC.Crafting
{
    public class RecipeKnowledgeClientState : MonoBehaviour
    {
        public static RecipeKnowledgeClientState Instance { get; private set; }

        [SerializeField] private bool _dontDestroyOnLoad = true;

        // Кеш известных recipe IDs (int, из CraftingWorld.RegisterRecipe)
        public HashSet<int> KnownRecipeIds { get; private set; } = new HashSet<int>();

        // Событие: сервер прислал обновлённый список известных рецептов
        public event Action<HashSet<int>> OnRecipeKnowledgeUpdated;

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

        /// <summary>
        /// Server -> client handler. Вызывается из NetworkPlayer.ReceiveRecipeKnowledgeTargetRpc.
        /// </summary>
        public void OnRecipeKnowledgeReceived(RecipeKnowledgeDto dto)
        {
            KnownRecipeIds = dto.knownRecipeIds != null
                ? new HashSet<int>(dto.knownRecipeIds)
                : new HashSet<int>();
            OnRecipeKnowledgeUpdated?.Invoke(KnownRecipeIds);
            if (Debug.isDebugBuild)
            {
                Debug.Log($"[RecipeKnowledgeClientState] OnRecipeKnowledgeReceived: {KnownRecipeIds.Count} recipes known");
            }
        }

        public void ClearState()
        {
            KnownRecipeIds.Clear();
        }
    }
}
