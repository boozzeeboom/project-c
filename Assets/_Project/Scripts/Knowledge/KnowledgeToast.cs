// Project C: Knowledge System V3
// KnowledgeToast: UI toast «Открыто знание» при получении новых знаний.
// Подписывается на 4 события клиентских стейтов и показывает diff.
// Design: docs/Character/Knowledges/07_KNOWLEDGE_SYSTEM_V3_INTEGRATION_PLAN.md §4 V3.8

using System.Collections.Generic;
using UnityEngine;
using ProjectC.Skills;
using ProjectC.Crafting;
using ProjectC.Reputation;
using ProjectC.Quests.Dto;
using ProjectC.Factions;

namespace ProjectC.Knowledge
{
    public class KnowledgeToast : MonoBehaviour
    {
        public static KnowledgeToast Instance { get; private set; }

        [Header("Toast Settings")]
        [SerializeField] private float _toastDuration = 3f;
        [SerializeField] private int _maxToastLines = 3;

        // Previous state for diff
        private HashSet<string> _prevKnownSkills = new HashSet<string>();
        private HashSet<string> _prevKnownRecipes = new HashSet<string>();
        private HashSet<byte> _prevKnownFactions = new HashSet<byte>();
        private HashSet<string> _prevKnownNpcs = new HashSet<string>();

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
                return;
            }
        }

        private void OnEnable() { TrySubscribe(); }
        private void OnDisable() { TryUnsubscribe(); }
        private void OnDestroy() { TryUnsubscribe(); if (Instance == this) Instance = null; }

        private bool _subscribed;

        private void TrySubscribe()
        {
            if (_subscribed) return;

            var skillsState = SkillsClientState.Instance;
            if (skillsState != null)
            {
                skillsState.OnSkillsUpdated += OnSkillsUpdated;
                _prevKnownSkills = new HashSet<string>(skillsState.KnownSkillIds ?? new HashSet<string>());
            }

            var recipeState = RecipeKnowledgeClientState.Instance;
            if (recipeState != null)
            {
                recipeState.OnRecipeKnowledgeUpdated += OnRecipesUpdated;
                _prevKnownRecipes = new HashSet<string>(recipeState.KnownRecipeIds ?? new HashSet<string>());
            }

            var repState = ReputationClientState.Instance;
            if (repState != null)
            {
                repState.OnReputationUpdated += OnReputationUpdated;
                _prevKnownFactions = new HashSet<byte>(repState.KnownFactionIds ?? new HashSet<byte>());
            }

            var npcState = NpcAttitudeClientState.Instance;
            if (npcState != null)
            {
                npcState.OnNpcAttitudeUpdated += OnNpcAttitudeUpdated;
                _prevKnownNpcs = new HashSet<string>(npcState.KnownNpcIds ?? new HashSet<string>());
            }

            _subscribed = true;
        }

        private void TryUnsubscribe()
        {
            if (!_subscribed) return;
            if (SkillsClientState.Instance != null) SkillsClientState.Instance.OnSkillsUpdated -= OnSkillsUpdated;
            if (RecipeKnowledgeClientState.Instance != null) RecipeKnowledgeClientState.Instance.OnRecipeKnowledgeUpdated -= OnRecipesUpdated;
            if (ReputationClientState.Instance != null) ReputationClientState.Instance.OnReputationUpdated -= OnReputationUpdated;
            if (NpcAttitudeClientState.Instance != null) NpcAttitudeClientState.Instance.OnNpcAttitudeUpdated -= OnNpcAttitudeUpdated;
            _subscribed = false;
        }

        private void OnSkillsUpdated(HashSet<string> learnedSkills)
        {
            var state = SkillsClientState.Instance;
            if (state == null) return;
            var current = state.KnownSkillIds ?? new HashSet<string>();
            DiffAndToast("Навык", _prevKnownSkills, current, GetSkillDisplayName);
            _prevKnownSkills = new HashSet<string>(current);
        }

        private void OnRecipesUpdated(HashSet<string> knownRecipes)
        {
            var current = knownRecipes ?? new HashSet<string>();
            DiffAndToast("Рецепт", _prevKnownRecipes, current, GetRecipeDisplayName);
            _prevKnownRecipes = new HashSet<string>(current);
        }

        private void OnReputationUpdated(ReputationSnapshotDto dto)
        {
            var state = ReputationClientState.Instance;
            if (state == null) return;
            var current = state.KnownFactionIds ?? new HashSet<byte>();
            DiffAndToastByte("Фракция", _prevKnownFactions, current, GetFactionDisplayName);
            _prevKnownFactions = new HashSet<byte>(current);
        }

        private void OnNpcAttitudeUpdated(NpcAttitudeSnapshotDto dto)
        {
            var state = NpcAttitudeClientState.Instance;
            if (state == null) return;
            var current = state.KnownNpcIds ?? new HashSet<string>();
            DiffAndToast("NPC", _prevKnownNpcs, current, GetNpcDisplayName);
            _prevKnownNpcs = new HashSet<string>(current);
        }

        private void DiffAndToast(string category, HashSet<string> prev, HashSet<string> current,
            System.Func<string, string> getName)
        {
            var newItems = new List<string>();
            foreach (var id in current)
            {
                if (!prev.Contains(id))
                    newItems.Add(getName(id));
            }
            if (newItems.Count > 0) ShowToast(category, newItems);
        }

        private void DiffAndToastByte(string category, HashSet<byte> prev, HashSet<byte> current,
            System.Func<byte, string> getName)
        {
            var newItems = new List<string>();
            foreach (var id in current)
            {
                if (!prev.Contains(id))
                    newItems.Add(getName(id));
            }
            if (newItems.Count > 0) ShowToast(category, newItems);
        }

        private void ShowToast(string category, List<string> names)
        {
            int count = Mathf.Min(names.Count, _maxToastLines);
            string displayNames = string.Join(", ", names.GetRange(0, count));
            if (names.Count > _maxToastLines)
                displayNames += $" и ещё {names.Count - _maxToastLines}";

            string message = $"Открыто знание — {category}: {displayNames}";
            Debug.Log($"[KnowledgeToast] {message}");
        }

        private string GetSkillDisplayName(string skillId)
        {
            var state = SkillsClientState.Instance;
            if (state != null && state.TryGetSkillConfig(skillId, out var cfg))
                return !string.IsNullOrEmpty(cfg.displayName) ? cfg.displayName : skillId;
            return skillId;
        }

        private string GetRecipeDisplayName(string recipeId)
        {
            var recipe = RecipeClientRegistry.GetRecipe(recipeId);
            return recipe != null ? recipe.DisplayName : recipeId;
        }

        private string GetFactionDisplayName(byte factionIdByte)
        {
            var factionId = (FactionId)factionIdByte;
            var catalog = FactionCatalog.Instance;
            if (catalog != null) return catalog.GetDisplayName(factionId);
            return factionId.ToString();
        }

        private string GetNpcDisplayName(string npcId) => npcId;
    }
}