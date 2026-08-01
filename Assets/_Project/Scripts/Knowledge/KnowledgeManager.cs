// Project C: Knowledge System V3
// KnowledgeManager: единый серверный фасад для открытия знаний всех типов.
// POCO-синглтон (паттерн SkillsWorld / QuestWorld).
// Design: docs/Character/Knowledges/07_KNOWLEDGE_SYSTEM_V3_INTEGRATION_PLAN.md §4 V3.1

using System.Collections.Generic;
using UnityEngine;
using ProjectC.Skills;
using ProjectC.Crafting;
using ProjectC.Quests;
using ProjectC.Factions;

namespace ProjectC.Knowledge
{
    public class KnowledgeManager
    {
        public static KnowledgeManager Instance { get; private set; }

        public KnowledgeManager()
        {
            if (Instance != null)
            {
                Debug.LogWarning("[KnowledgeManager] Replacing existing instance.");
            }
            Instance = this;
        }

        public static void Reset()
        {
            Instance = null;
        }

        /// <summary>
        /// Открыть знание об одном ассете. Автоматически определяет тип.
        /// Возвращает true если реально открыли новое знание.
        /// </summary>
        public bool Unlock(ulong clientId, Object asset)
        {
            if (asset == null) return false;

            switch (asset)
            {
                case SkillNodeConfig skill:
                    return SkillsWorld.Instance?.UnlockSkillKnowledge(clientId, skill.skillId) ?? false;

                case RecipeData recipe:
                    if (string.IsNullOrEmpty(recipe.RecipeId))
                    {
                        Debug.LogWarning($"[KnowledgeManager] Recipe '{recipe.name}' has empty recipeId — skipping.");
                        return false;
                    }
                    return CraftingWorld.UnlockRecipeKnowledge(clientId, recipe.RecipeId);

                case NpcDefinition npc:
                {
                    bool unlocked = false;
                    var qw = QuestWorld.Instance;
                    if (qw != null)
                    {
                        qw.UnlockNpcKnowledge(clientId, npc.npcId);
                        unlocked = true;

                        // V3: консистентно с MarkNpcTalked — авто-открытие фракции NPC (Проблема 11)
                        if (npc.faction != FactionId.None)
                        {
                            qw.UnlockFactionKnowledge(clientId, npc.faction);
                        }
                    }
                    return unlocked;
                }

                case FactionDefinition factionDef:
                {
                    var qw = QuestWorld.Instance;
                    if (qw != null && factionDef.factionId != FactionId.None)
                    {
                        qw.UnlockFactionKnowledge(clientId, factionDef.factionId);
                        return true;
                    }
                    return false;
                }

                default:
                    Debug.LogWarning($"[KnowledgeManager] Unsupported asset type: {asset.GetType().Name}");
                    return false;
            }
        }

        /// <summary>
        /// Batch-открытие знаний. Возвращает количество реально открытых.
        /// После всех изменений вызывает SavePlayer + рассылает снапшоты.
        /// </summary>
        public int UnlockAll(ulong clientId, Object[] assets)
        {
            if (assets == null || assets.Length == 0) return 0;

            int unlocked = 0;
            bool anyChanged = false;

            foreach (var asset in assets)
            {
                if (asset == null) continue;
                if (Unlock(clientId, asset))
                {
                    unlocked++;
                    anyChanged = true;
                }
            }

            if (anyChanged)
            {
                // V3.1: знания сохраняются сразу
                QuestWorld.Instance?.SavePlayer(clientId);

                // V3.7: рассылаем снапшоты существующими методами
                SendSnapshots(clientId);
            }

            return unlocked;
        }

        /// <summary>
        /// V3.7: после unlock — отправить все снапшоты клиенту.
        /// Используются существующие публичные методы серверов.
        /// </summary>
        private void SendSnapshots(ulong clientId)
        {
            // Skills snapshot
            var skillsServer = SkillsServer.Instance;
            if (skillsServer != null)
                skillsServer.SendSnapshotToOwner(clientId);

            // Recipe knowledge
            var craftingServer = CraftingServer.Instance;
            if (craftingServer != null)
                craftingServer.SendRecipeKnowledgeToClient(clientId);

            // Reputation + NPC knowledge (QuestServer.BroadcastKnowledgeChange)
            var questServer = ProjectC.Quests.QuestServer.Instance;
            if (questServer != null)
                questServer.BroadcastKnowledgeChange(clientId);
        }
    }
}