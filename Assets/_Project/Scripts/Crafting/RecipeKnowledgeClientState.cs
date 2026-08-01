// Project C: Crafting — T-KNOWLEDGE-V3
// RecipeKnowledgeClientState: client-side projection of known recipe IDs (string keys).
// Auto-spawned singleton (pattern: SkillsClientState, CraftingClientState).
// Design: docs/Character/Knowledges/07_KNOWLEDGE_SYSTEM_V3_INTEGRATION_PLAN.md

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

        // Кеш известных recipe IDs (string, V3 stable key)
        public HashSet<string> KnownRecipeIds { get; private set; } = new HashSet<string>();

        // Событие: сервер прислал обновлённый список известных рецептов
        public event Action<HashSet<string>> OnRecipeKnowledgeUpdated;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                if (_dontDestroyOnLoad) DontDestroyOnLoad(gameObject);
                RecipeClientRegistry.EnsureLoaded(); // T-KNOWLEDGE-V3: preload recipe registry
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
                ? new HashSet<string>(dto.knownRecipeIds)
                : new HashSet<string>();
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