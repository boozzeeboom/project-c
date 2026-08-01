// Project C: Knowledge System V2
// KnowledgeLossConfig: конфиг потери знаний при смерти игрока.
// Design: docs/Character/Knowledges/05_KNOWLEDGE_SYSTEM_V2_RESEARCH_REVIEW.md §4.3
//
// Все поля настраиваемые — никакого хардкода.
// RNG: randomSeed = 0 → без сида (System.Random default).

using System;
using UnityEngine;
using ProjectC.Factions;

namespace ProjectC.Knowledge
{
    [CreateAssetMenu(fileName = "KnowledgeLossConfig", menuName = "Project C/Knowledge/Loss Config", order = 50)]
    public class KnowledgeLossConfig : ScriptableObject
    {
        [Header("Master Switch")]
        [Tooltip("Если false — потеря знаний при смерти отключена полностью.")]
        public bool enabled = true;

        [Header("Faction Knowledge Loss")]
        [Tooltip("Минимум фракций, которые останутся известны после смерти.")]
        [Range(0, 16)]
        public int minRetainFactions = 1;

        [Tooltip("Шанс потери знания о каждой фракции (0..1).")]
        [Range(0f, 1f)]
        public float factionLossChance = 0.5f;

        [Header("NPC Knowledge Loss")]
        [Tooltip("Минимум NPC, которые останутся известны после смерти.")]
        [Range(0, 50)]
        public int minRetainNpcs = 3;

        [Tooltip("Шанс потери знания о каждом NPC (0..1).")]
        [Range(0f, 1f)]
        public float npcLossChance = 0.3f;

        [Header("Recipe Knowledge Loss")]
        [Tooltip("Шанс потери знания о каждом рецепте (0..1).")]
        [Range(0f, 1f)]
        public float recipeLossChance = 0.25f;

        [Header("Skill Knowledge Loss")]
        [Tooltip("Шанс потери ЗНАНИЯ о навыке (не самого навыка!). ADR-7: 0.0 по умолчанию.")]
        [Range(0f, 1f)]
        public float skillKnowledgeLossChance = 0.0f;

        [Header("Protected — Never Forget")]
        [Tooltip("Фракции, которые НИКОГДА не забываются (сюжетные).")]
        public FactionId[] neverForgetFactions = new[]
        {
            FactionId.Neutral,
            // FactionId.GuildOfThoughts,  // раскомментировать когда появятся в enum
            // FactionId.GuildOfCreation,
        };

        [Tooltip("NPC, которые НИКОГДА не забываются (сюжетные).")]
        public string[] neverForgetNpcs = Array.Empty<string>();

        [Header("RNG")]
        [Tooltip("Seed для детерминированных тестов. 0 = случайный (System.Random default).")]
        public int randomSeed = 0;
    }
}
