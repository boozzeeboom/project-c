// Project C: Knowledge System V2
// FactionDefinition: ScriptableObject с данными отображения одной фракции.
// Замена хардкода FindFactionFallback в CharacterWindow.cs (сейчас знает только 5 из 16).
// Design: docs/Character/Knowledges/05_KNOWLEDGE_SYSTEM_V2_RESEARCH_REVIEW.md §4.6

using UnityEngine;
using ProjectC.Factions;

namespace ProjectC.Knowledge
{
    [CreateAssetMenu(fileName = "FactionDef_", menuName = "Project C/Knowledge/Faction Definition", order = 51)]
    public class FactionDefinition : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("FactionId из enum (ProjectC.Factions.FactionId).")]
        public FactionId factionId = FactionId.None;

        [Tooltip("Отображаемое имя фракции (RU).")]
        public string displayName = "";

        [Tooltip("Отображаемое имя фракции (EN) — fallback если displayName пуст.")]
        public string displayNameEn = "";

        [Tooltip("Цвет фракции для UI (репутация, бейджи).")]
        public Color color = Color.white;

        [Tooltip("Порядок сортировки в списке (меньше = выше).")]
        public int sortOrder = 0;

        [Header("Lore")]
        [TextArea(3, 6)]
        [Tooltip("Описание фракции (показывается в деталях вкладки «Знания»).")]
        public string loreDescription = "";

        /// <summary>Resolved display name (RU → EN fallback → FactionId.ToString).</summary>
        public string ResolvedDisplayName =>
            !string.IsNullOrEmpty(displayName) ? displayName :
            !string.IsNullOrEmpty(displayNameEn) ? displayNameEn :
            factionId.ToString();
    }
}
