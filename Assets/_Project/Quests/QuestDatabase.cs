// T-Q09: QuestDatabase ScriptableObject — central registry для всех quest-related assets.
// Заменяет T-Q05 QuestServer.questDatabase (QuestDefinition[]) на единый SO-контейнер.
// См. docs/NPC_quests/02_V2_ARCHITECTURE.md §2.3 (data model aggregation).
//
// T-Q09: ручное наполнение (создаётся [QuestDatabase] asset, поля заполняются drag-drop).
// T-Q09+ расширение: [InitializeOnLoad] editor code сканирует Assets/_Project/Quests/Data/.

using System;
using UnityEngine;
using ProjectC.Factions;
using ProjectC.Dialogue;

namespace ProjectC.Quests
{
    /// <summary>
    /// Central registry: все FactionDefinition, NpcDefinition, DialogTree, QuestDefinition
    /// используются квестовой подсистемой. QuestServer (T-Q05) ссылается на один QuestDatabase asset.
    /// </summary>
    /// <remarks>
    /// Auto-populate via T-Q09 Editor.QuestDatabaseAutoDiscover (InitializeOnLoad).
    /// Manual edit OK for new projects / when no auto-discover worked.
    /// </remarks>
    [CreateAssetMenu(fileName = "QuestDatabase", menuName = "ProjectC/Quests/Quest Database", order = 50)]
    public class QuestDatabase : ScriptableObject
    {
        [Header("Factions (from Assets/_Project/Quests/Data/Factions/)")]
        [Tooltip("Все FactionDefinition assets в проекте. Заполняется auto-discover или вручную.")]
        public FactionDefinition[] factions = Array.Empty<FactionDefinition>();

        [Header("NPCs (from Assets/_Project/Quests/Data/Npcs/)")]
        [Tooltip("Все NpcDefinition assets в проекте.")]
        public NpcDefinition[] npcs = Array.Empty<NpcDefinition>();

        [Header("Dialog Trees (from Assets/_Project/Quests/Data/Dialogs/)")]
        [Tooltip("Все DialogTree assets в проекте.")]
        public DialogTree[] dialogTrees = Array.Empty<DialogTree>();

        [Header("Quests (from Assets/_Project/Quests/Data/Quests/)")]
        [Tooltip("Все QuestDefinition assets в проекте.")]
        public QuestDefinition[] quests = Array.Empty<QuestDefinition>();

        // ============ Lookup helpers ============

        public QuestDefinition GetQuest(string questId)
        {
            if (string.IsNullOrEmpty(questId) || quests == null) return null;
            for (int i = 0; i < quests.Length; i++)
            {
                if (quests[i] != null && quests[i].questId == questId) return quests[i];
            }
            return null;
        }

        public NpcDefinition GetNpc(string npcId)
        {
            if (string.IsNullOrEmpty(npcId) || npcs == null) return null;
            for (int i = 0; i < npcs.Length; i++)
            {
                if (npcs[i] != null && npcs[i].npcId == npcId) return npcs[i];
            }
            return null;
        }

        public FactionDefinition GetFaction(FactionId id)
        {
            if (factions == null) return null;
            for (int i = 0; i < factions.Length; i++)
            {
                if (factions[i] != null && factions[i].factionId == id) return factions[i];
            }
            return null;
        }

        public DialogTree GetDialogTree(string treeId)
        {
            if (string.IsNullOrEmpty(treeId) || dialogTrees == null) return null;
            for (int i = 0; i < dialogTrees.Length; i++)
            {
                if (dialogTrees[i] != null && dialogTrees[i].treeId == treeId) return dialogTrees[i];
            }
            return null;
        }
    }
}
