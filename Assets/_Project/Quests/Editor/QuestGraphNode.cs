// T-U02: QuestGraphNode — Node subclass with SO binding.
// Replaces plain GraphView.Node usage in QuestNodeGraphView.
// Each node carries:
//   - OwnerAsset: the ScriptableObject this node represents data from
//   - SourcePath: path within the SO (e.g. "stages[0].objectives[1]")
//   - SourceData: the actual POCO (QuestStage, QuestObjective, DialogueNode)
//   - NodeKind: semantic type for layout and styling

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace ProjectC.Quests.Editor
{
    /// <summary>Semantic kind of a graph node — drives auto-layout and styling.</summary>
    public enum QuestNodeKind
    {
        QuestRoot,
        Stage,
        Objective,
        Reward,
        Dialog,
        Condition
    }

    /// <summary>
    /// GraphView Node with back-reference to the ScriptableObject it represents.
    /// Enables incremental mutations: add/delete a single node without full rebuild.
    /// </summary>
    public class QuestGraphNode : Node
    {
        /// <summary>The ScriptableObject that owns the data this node displays.</summary>
        public ScriptableObject OwnerAsset;

        /// <summary>
        /// Dotted path into OwnerAsset, e.g. "stages[0]", "stages[0].objectives[1]".
        /// Used to locate the data for delete operations.
        /// </summary>
        public string SourcePath;

        /// <summary>
        /// Direct reference to the data object (QuestStage, QuestObjective, etc.).
        /// Avoids re-parsing SourcePath on every interaction.
        /// </summary>
        public object SourceData;

        /// <summary>Semantic kind for layout and styling decisions.</summary>
        public QuestNodeKind NodeKind;

        /// <summary>
        /// Unique key for position persistence. Set during BuildGraph.
        /// </summary>
        public string PersistKey;
    }
}
#endif
