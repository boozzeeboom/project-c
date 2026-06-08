// T-Q03 (expanded from T-Q02 stub): DialogTree ScriptableObject with full graph.
// См. docs/NPC_quests/02_V2_ARCHITECTURE.md §2.3.7 + §7.3 (пример MiraDefault).
//
// Graph model: flat list of DialogueNode, edges are nested inside each node
// (per-node ownership keeps authoring local). Edges reference targetNodeId
// by string. Edges with targetNodeId == "" == EndConversation (UI reuses
// DialogueAction.EndConversation for explicit goodbye).

using System;
using UnityEngine;

namespace ProjectC.Dialogue
{
    /// <summary>
    /// Top-level dialog graph. Replaces v1 NpcData.dialogues[] array.
    /// Can be shared between multiple NPCs ("generic dockworker" pattern) or
    /// switched at runtime via <c>DialogueAction.SwitchDialogTree</c>.
    /// </summary>
    [CreateAssetMenu(fileName = "DialogTree_", menuName = "ProjectC/Dialogue/Dialog Tree", order = 120)]
    public class DialogTree : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Уникальный ID дерева (например: 'mira_default', 'mira_artifact_story')")]
        public string treeId = "";

        [Tooltip("Отображаемое имя (loc key в будущем)")]
        public string displayName = "";

        [Header("Graph")]
        [Tooltip("nodeId стартовой ноды. Должен совпадать с DialogueNode.nodeId одного из nodes[]. " +
                 "Если root не найден — UI покажет ошибку при входе в диалог.")]
        public string rootNodeId = "greeting";

        [Tooltip("Все ноды графа. Каждая нода владеет своими исходящими edges. " +
                 "Порядок в массиве — порядок отображения в QuestDatabaseWindow (T-Q09).")]
        public DialogueNode[] nodes = Array.Empty<DialogueNode>();

        [Tooltip("(Optional) CSV/JSON loc table для будущей локализации. Пока не парсится — тексты в DialogueNode.text literal.")]
        public TextAsset localizationTable;

        // ============ Runtime helpers ============

        /// <summary>
        /// Find a node by id. O(N), used by QuestServer.RequestAdvanceDialogue
        /// и GraphView в T-Q09b. Не делать dictionary-кэш — массив обычно &lt; 20 nodes.
        /// </summary>
        public DialogueNode GetNode(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId)) return null;
            for (int i = 0; i < nodes.Length; i++)
            {
                if (nodes[i] != null && nodes[i].nodeId == nodeId) return nodes[i];
            }
            return null;
        }

        /// <summary>
        /// Validate graph reachability. Returns null если все ноды достижимы из root;
        /// иначе — список unreachable nodeId. Editor-time check (T-Q09 validate button).
        /// </summary>
        public string[] GetUnreachableNodes()
        {
            if (nodes == null || nodes.Length == 0) return Array.Empty<string>();
            if (GetNode(rootNodeId) == null) return new[] { $"<root '{rootNodeId}' missing>" };

            var visited = new System.Collections.Generic.HashSet<string>();
            var queue = new System.Collections.Generic.Queue<string>();
            queue.Enqueue(rootNodeId);
            visited.Add(rootNodeId);

            while (queue.Count > 0)
            {
                var current = GetNode(queue.Dequeue());
                if (current == null) continue;
                for (int i = 0; i < current.edges.Length; i++)
                {
                    var edge = current.edges[i];
                    if (edge == null || string.IsNullOrEmpty(edge.targetNodeId)) continue;
                    if (visited.Add(edge.targetNodeId)) queue.Enqueue(edge.targetNodeId);
                }
            }

            var unreachable = new System.Collections.Generic.List<string>();
            for (int i = 0; i < nodes.Length; i++)
            {
                if (nodes[i] != null && !visited.Contains(nodes[i].nodeId))
                {
                    unreachable.Add(nodes[i].nodeId);
                }
            }
            return unreachable.ToArray();
        }
    }
}
