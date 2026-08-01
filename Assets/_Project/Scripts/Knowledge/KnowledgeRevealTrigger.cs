// Project C: Knowledge System V3
// KnowledgeRevealTrigger: server-authoritative MonoBehaviour для trigger zone.
// Дизайнер кидает ассеты в инспектор — игрок открывает их при входе в зону.
// Design: docs/Character/Knowledges/07_KNOWLEDGE_SYSTEM_V3_INTEGRATION_PLAN.md §4 V3.2

using UnityEngine;
using UnityEngine.Events;
using Unity.Netcode;
using ProjectC.Skills;
using ProjectC.Crafting;
using ProjectC.Quests;
using ProjectC.Factions;

namespace ProjectC.Knowledge
{
    [RequireComponent(typeof(Collider))]
    public class KnowledgeRevealTrigger : MonoBehaviour
    {
        [Header("Ассеты, открываемые при активации")]
        public SkillNodeConfig[] skillsToReveal;
        public RecipeData[] recipesToReveal;
        public FactionDefinition[] factionsToReveal;
        public NpcDefinition[] npcsToReveal;

        [Header("Активация")]
        [Tooltip("Сработать только один раз (рекомендуется для зон).")]
        public bool triggerOnce = true;

        [Tooltip("Теги объектов, которые считаются игроком.")]
        public string[] playerTags = { "Player" };

        [Tooltip("Событие вызывается на сервере после открытия знаний (VFX, звук).")]
        public UnityEvent onRevealed;

        private bool _triggered;

        private void Awake()
        {
            var col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            // Server-only: триггер существует на всех машинах, RPC не нужен
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;
            if (_triggered && triggerOnce) return;

            if (!MatchesPlayerTag(other)) return;

            var netObj = other.GetComponentInParent<NetworkObject>();
            if (netObj == null) return;
            ulong clientId = netObj.OwnerClientId;
            if (clientId == 0) return; // host or not a player

            var allAssets = CollectAssets();
            if (KnowledgeManager.Instance != null)
                KnowledgeManager.Instance.UnlockAll(clientId, allAssets);

            if (triggerOnce) _triggered = true;
            onRevealed?.Invoke();
        }

        private bool MatchesPlayerTag(Collider other)
        {
            if (playerTags == null || playerTags.Length == 0) return true;
            for (int i = 0; i < playerTags.Length; i++)
            {
                if (other.CompareTag(playerTags[i])) return true;
            }
            return false;
        }

        private Object[] CollectAssets()
        {
            int count = 0;
            if (skillsToReveal != null) count += skillsToReveal.Length;
            if (recipesToReveal != null) count += recipesToReveal.Length;
            if (factionsToReveal != null) count += factionsToReveal.Length;
            if (npcsToReveal != null) count += npcsToReveal.Length;

            var all = new Object[count];
            int idx = 0;

            if (skillsToReveal != null)
                for (int i = 0; i < skillsToReveal.Length; i++)
                    all[idx++] = skillsToReveal[i];
            if (recipesToReveal != null)
                for (int i = 0; i < recipesToReveal.Length; i++)
                    all[idx++] = recipesToReveal[i];
            if (factionsToReveal != null)
                for (int i = 0; i < factionsToReveal.Length; i++)
                    all[idx++] = factionsToReveal[i];
            if (npcsToReveal != null)
                for (int i = 0; i < npcsToReveal.Length; i++)
                    all[idx++] = npcsToReveal[i];

            return all;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            var col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;

            // Validate recipeIds
            if (recipesToReveal != null)
            {
                foreach (var r in recipesToReveal)
                {
                    if (r != null && string.IsNullOrEmpty(r.RecipeId))
                        Debug.LogWarning($"[KnowledgeRevealTrigger] Recipe '{r.name}' has empty recipeId — knowledge unlock will fail.", this);
                }
            }
            // Validate skillIds
            if (skillsToReveal != null)
            {
                foreach (var s in skillsToReveal)
                {
                    if (s != null && string.IsNullOrEmpty(s.skillId))
                        Debug.LogWarning($"[KnowledgeRevealTrigger] Skill '{s.name}' has empty skillId — knowledge unlock will fail.", this);
                }
            }
        }
#endif
    }
}