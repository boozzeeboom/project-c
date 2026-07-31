// QuestGraphModel — pure C# adapter over NpcDefinition + DialogTree + QuestDefinition.
// Does NOT store data. All connections ARE the object-reference fields in the three SOs.
// Read: BuildGraph() projects SO state into nodes/edges. Write: SetConnection() modifies SO fields.
//
// Architecture: ARCHITECTURE_PLAN.md

#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ProjectC.Dialogue;
using ProjectC.Factions;

namespace ProjectC.Quests.Editor
{
    // ═══════════════════════════════════════════
    // Port Semantics
    // ═══════════════════════════════════════════

    public enum PortSemantic
    {
        // Outputs
        NpcOffersQuest,
        NpcDefaultDialog,
        DialogEdgeAction,   // userData = edge index (int)
        StageNext,
        ObjectiveTarget,

        // Inputs
        QuestOfferedBy,
        DialogIn,
        StageIn,
        NpcTargetedBy,
    }

    // ═══════════════════════════════════════════
    // Node Info (value types — keys for edges)
    // ═══════════════════════════════════════════

    public class NpcNodeInfo
    {
        public NpcDefinition npc;
        public override int GetHashCode() => npc?.GetHashCode() ?? 0;
        public override bool Equals(object o) => o is NpcNodeInfo n && n.npc == npc;
    }

    public class DialogNodeInfo
    {
        public DialogTree tree;
        public int nodeIndex;
        public DialogueNode Node => tree?.nodes != null && nodeIndex >= 0 && nodeIndex < tree.nodes.Length
            ? tree.nodes[nodeIndex] : null;
        public override int GetHashCode() => (tree, nodeIndex).GetHashCode();
        public override bool Equals(object o) => o is DialogNodeInfo d && d.tree == tree && d.nodeIndex == nodeIndex;
    }

    public class QuestNodeInfo
    {
        public QuestDefinition quest;
        public override int GetHashCode() => quest?.GetHashCode() ?? 0;
        public override bool Equals(object o) => o is QuestNodeInfo q && q.quest == quest;
    }

    public class StageNodeInfo
    {
        public QuestDefinition quest;
        public int stageIndex;
        public QuestStage Stage => quest?.stages != null && stageIndex >= 0 && stageIndex < quest.stages.Length
            ? quest.stages[stageIndex] : null;
        public override int GetHashCode() => (quest, stageIndex).GetHashCode();
        public override bool Equals(object o) => o is StageNodeInfo s && s.quest == quest && s.stageIndex == stageIndex;
    }

    public class ObjectiveNodeInfo
    {
        public QuestDefinition quest;
        public int stageIndex;
        public int objIndex;
        public QuestObjective Obj => quest?.stages != null && stageIndex >= 0 && stageIndex < quest.stages.Length
            ? (quest.stages[stageIndex]?.objectives != null && objIndex >= 0 && objIndex < quest.stages[stageIndex].objectives.Length
                ? quest.stages[stageIndex].objectives[objIndex] : null) : null;
        public override int GetHashCode() => (quest, stageIndex, objIndex).GetHashCode();
        public override bool Equals(object o) => o is ObjectiveNodeInfo ob && ob.quest == quest && ob.stageIndex == stageIndex && ob.objIndex == objIndex;
    }

    public class RewardNodeInfo
    {
        public QuestDefinition quest;
        public override int GetHashCode() => quest?.GetHashCode() ?? 0;
        public override bool Equals(object o) => o is RewardNodeInfo r && r.quest == quest;
    }

    // ═══════════════════════════════════════════
    // Edge Info
    // ═══════════════════════════════════════════

    public class EdgeInfo
    {
        public object fromNode;
        public PortSemantic fromPort;
        public object toNode;
        public PortSemantic toPort;
        public int extraIndex = -1; // edge index for DialogEdgeAction
    }

    // ═══════════════════════════════════════════
    // Model
    // ═══════════════════════════════════════════

    public class QuestGraphModel
    {
        private readonly List<NpcDefinition> _npcs = new();
        private readonly List<DialogTree> _dialogs = new();
        private readonly List<QuestDefinition> _quests = new();

        // Built node/edge lists
        public List<NpcNodeInfo> NpcNodes { get; } = new();
        public List<DialogNodeInfo> DialogNodes { get; } = new();
        public List<QuestNodeInfo> QuestNodes { get; } = new();
        public List<StageNodeInfo> StageNodes { get; } = new();
        public List<ObjectiveNodeInfo> ObjectiveNodes { get; } = new();
        public List<RewardNodeInfo> RewardNodes { get; } = new();
        public List<EdgeInfo> Edges { get; } = new();

        public int TotalNodeCount => NpcNodes.Count + DialogNodes.Count + QuestNodes.Count
            + StageNodes.Count + ObjectiveNodes.Count + RewardNodes.Count;

        // ── Registration ──

        public void AddNpc(NpcDefinition npc)
        {
            if (npc == null || _npcs.Contains(npc)) return;
            _npcs.Add(npc);

            if (npc.defaultDialogTree != null && !_dialogs.Contains(npc.defaultDialogTree))
                _dialogs.Add(npc.defaultDialogTree);

            // Also load dialogs referenced in SwitchDialogTree actions
            if (npc.defaultDialogTree?.nodes != null)
                foreach (var n in npc.defaultDialogTree.nodes)
                    if (n?.edges != null)
                        foreach (var e in n.edges)
                            if (e?.action?.type == DialogueActionType.SwitchDialogTree && e.action.dialogTreeRef != null && !_dialogs.Contains(e.action.dialogTreeRef))
                                _dialogs.Add(e.action.dialogTreeRef);

            if (npc.questOfferRefs != null)
                foreach (var q in npc.questOfferRefs)
                    if (q != null && !_quests.Contains(q)) _quests.Add(q);
            if (npc.questTurnInRefs != null)
                foreach (var q in npc.questTurnInRefs)
                    if (q != null && !_quests.Contains(q)) _quests.Add(q);
        }

        public void AddQuest(QuestDefinition q)
        {
            if (q == null || _quests.Contains(q)) return;
            _quests.Add(q);

            // Auto-load NPCs referenced by objectives
            if (q.stages != null)
                foreach (var s in q.stages)
                    if (s?.objectives != null)
                        foreach (var o in s.objectives)
                            if (o?.targetNpc != null && !_npcs.Contains(o.targetNpc))
                                _npcs.Add(o.targetNpc);
        }

        public void AddDialogTree(DialogTree t)
        {
            if (t == null || _dialogs.Contains(t)) return;
            _dialogs.Add(t);
        }

        public void Clear()
        {
            _npcs.Clear(); _dialogs.Clear(); _quests.Clear();
            NpcNodes.Clear(); DialogNodes.Clear(); QuestNodes.Clear();
            StageNodes.Clear(); ObjectiveNodes.Clear(); RewardNodes.Clear();
            Edges.Clear();
        }

        // ── Build Graph ──

        public void BuildGraph()
        {
            NpcNodes.Clear(); DialogNodes.Clear(); QuestNodes.Clear();
            StageNodes.Clear(); ObjectiveNodes.Clear(); RewardNodes.Clear();
            Edges.Clear();

            // 1. Build all nodes
            foreach (var npc in _npcs) NpcNodes.Add(new NpcNodeInfo { npc = npc });

            foreach (var tree in _dialogs)
            {
                if (tree.nodes == null) continue;
                for (int i = 0; i < tree.nodes.Length; i++)
                    if (tree.nodes[i] != null)
                        DialogNodes.Add(new DialogNodeInfo { tree = tree, nodeIndex = i });
            }

            foreach (var quest in _quests)
            {
                QuestNodes.Add(new QuestNodeInfo { quest = quest });
                if (quest.stages != null)
                {
                    for (int si = 0; si < quest.stages.Length; si++)
                    {
                        if (quest.stages[si] == null) continue;
                        StageNodes.Add(new StageNodeInfo { quest = quest, stageIndex = si });
                        var stage = quest.stages[si];
                        if (stage.objectives != null)
                            for (int oi = 0; oi < stage.objectives.Length; oi++)
                                if (stage.objectives[oi] != null)
                                    ObjectiveNodes.Add(new ObjectiveNodeInfo { quest = quest, stageIndex = si, objIndex = oi });
                    }
                }
                if (HasReward(quest.rewards))
                    RewardNodes.Add(new RewardNodeInfo { quest = quest });
            }

            // 2. Build edges
            BuildNpcEdges();
            BuildDialogEdges();
            BuildQuestEdges();
        }

        private void BuildNpcEdges()
        {
            foreach (var npc in _npcs)
            {
                var npcNode = NpcNodes.First(n => n.npc == npc);

                // NPC → DialogTree root
                if (npc.defaultDialogTree != null)
                {
                    var root = DialogNodes.FirstOrDefault(d =>
                        d.tree == npc.defaultDialogTree &&
                        d.Node != null &&
                        d.Node.nodeId == npc.defaultDialogTree.rootNodeId);
                    if (root?.tree != null)
                        Edges.Add(new EdgeInfo { fromNode = npcNode, fromPort = PortSemantic.NpcDefaultDialog,
                            toNode = root, toPort = PortSemantic.DialogIn });
                }

                // NPC → Quest offers
                if (npc.questOfferRefs != null)
                    foreach (var q in npc.questOfferRefs)
                    {
                        if (q == null) continue;
                        var qn = QuestNodes.FirstOrDefault(n => n.quest == q);
                        if (qn?.quest != null)
                            Edges.Add(new EdgeInfo { fromNode = npcNode, fromPort = PortSemantic.NpcOffersQuest,
                                toNode = qn, toPort = PortSemantic.QuestOfferedBy });
                    }
            }
        }

        private void BuildDialogEdges()
        {
            foreach (var dn in DialogNodes)
            {
                var node = dn.Node;
                if (node?.edges == null) continue;

                for (int ei = 0; ei < node.edges.Length; ei++)
                {
                    var edge = node.edges[ei];
                    if (edge == null) continue;

                    // Internal edge: DialogNode → DialogNode (same tree)
                    if (!string.IsNullOrEmpty(edge.targetNodeId))
                    {
                        var target = DialogNodes.FirstOrDefault(d =>
                            d.tree == dn.tree && d.Node != null && d.Node.nodeId == edge.targetNodeId);
                        if (target?.tree != null)
                            Edges.Add(new EdgeInfo { fromNode = dn, fromPort = PortSemantic.DialogEdgeAction,
                                toNode = target, toPort = PortSemantic.DialogIn, extraIndex = ei });
                    }

                    // Edge action: OfferQuest
                    if (edge.action != null && edge.action.type == DialogueActionType.OfferQuest && edge.action.questRef != null)
                    {
                        // Connect to the first stage of that quest
                        var firstStage = StageNodes.FirstOrDefault(s => s.quest == edge.action.questRef && s.stageIndex == 0);
                        if (firstStage?.quest != null)
                            Edges.Add(new EdgeInfo { fromNode = dn, fromPort = PortSemantic.DialogEdgeAction,
                                toNode = firstStage, toPort = PortSemantic.StageIn, extraIndex = ei });
                    }

                    // Edge action: SwitchDialogTree
                    if (edge.action != null && edge.action.type == DialogueActionType.SwitchDialogTree && edge.action.dialogTreeRef != null)
                    {
                        var targetTree = edge.action.dialogTreeRef;
                        var targetRoot = DialogNodes.FirstOrDefault(d =>
                            d.tree == targetTree && d.Node != null && d.Node.nodeId == targetTree.rootNodeId);
                        if (targetRoot?.tree != null)
                            Edges.Add(new EdgeInfo { fromNode = dn, fromPort = PortSemantic.DialogEdgeAction,
                                toNode = targetRoot, toPort = PortSemantic.DialogIn, extraIndex = ei });
                    }
                }
            }
        }

        private void BuildQuestEdges()
        {
            foreach (var quest in _quests)
            {
                if (quest.stages == null) continue;

                // Stage → next stage
                for (int si = 0; si < quest.stages.Length; si++)
                {
                    var stage = quest.stages[si];
                    if (stage == null || string.IsNullOrEmpty(stage.nextStageId)) continue;
                    var thisStage = StageNodes.FirstOrDefault(s => s.quest == quest && s.stageIndex == si);
                    var nextStage = StageNodes.FirstOrDefault(s => s.quest == quest && s.Stage != null && s.Stage.stageId == stage.nextStageId);
                    if (thisStage?.quest != null && nextStage?.quest != null)
                        Edges.Add(new EdgeInfo { fromNode = thisStage, fromPort = PortSemantic.StageNext,
                            toNode = nextStage, toPort = PortSemantic.StageIn });
                }

                // Stage 0 ← Quest root
                if (quest.stages.Length > 0 && quest.stages[0] != null)
                {
                    var qNode = QuestNodes.First(n => n.quest == quest);
                    var s0 = StageNodes.FirstOrDefault(s => s.quest == quest && s.stageIndex == 0);
                    if (qNode?.quest != null && s0?.quest != null)
                        Edges.Add(new EdgeInfo { fromNode = qNode, fromPort = PortSemantic.StageNext,
                            toNode = s0, toPort = PortSemantic.StageIn });
                }

                // Last stage → Reward
                if (RewardNodes.Any(r => r.quest == quest))
                {
                    int lastIdx = quest.stages.Length - 1;
                    var lastStage = StageNodes.FirstOrDefault(s => s.quest == quest && s.stageIndex == lastIdx);
                    var reward = RewardNodes.First(r => r.quest == quest);
                    if (lastStage?.quest != null && reward?.quest != null)
                        Edges.Add(new EdgeInfo { fromNode = lastStage, fromPort = PortSemantic.StageNext,
                            toNode = reward, toPort = PortSemantic.StageIn });
                }

                // Objective → Target NPC
                foreach (var on in ObjectiveNodes)
                {
                    var obj = on.Obj;
                    if (obj?.targetNpc == null) continue;
                    var npcNode = NpcNodes.FirstOrDefault(n => n.npc == obj.targetNpc);
                    if (npcNode?.npc != null)
                        Edges.Add(new EdgeInfo { fromNode = on, fromPort = PortSemantic.ObjectiveTarget,
                            toNode = npcNode, toPort = PortSemantic.NpcTargetedBy });
                }
            }
        }

        // ── Write-through connection ──

        /// <summary>
        /// Sets a connection between two ports by modifying the underlying SO fields.
        /// Returns true on success, false if the connection is not supported.
        /// </summary>
        public bool SetConnection(object fromNode, PortSemantic fromPort, object toNode, PortSemantic toPort, int edgeIndex = -1)
        {
            // ── DialogEdgeAction → (Quest/Stage) = OfferQuest ──
            if (fromPort == PortSemantic.DialogEdgeAction && toPort == PortSemantic.StageIn && fromNode is DialogNodeInfo dni)
            {
                var edge = GetEdge(dni, edgeIndex);
                if (edge == null) return false;

                QuestDefinition quest = null;
                if (toNode is StageNodeInfo sni) quest = sni.quest;
                else if (toNode is QuestNodeInfo qni) quest = qni.quest;
                if (quest == null) return false;

                edge.action = new DialogueAction { type = DialogueActionType.OfferQuest, questRef = quest, stringParam = quest.questId };
                EditorUtility.SetDirty(dni.tree);
                return true;
            }

            // ── DialogEdgeAction → DialogIn (same tree) = internal link ──
            if (fromPort == PortSemantic.DialogEdgeAction && toPort == PortSemantic.DialogIn &&
                fromNode is DialogNodeInfo fromDni && toNode is DialogNodeInfo toDni && fromDni.tree == toDni.tree)
            {
                var edge = GetEdge(fromDni, edgeIndex);
                if (edge == null) return false;
                edge.targetNodeId = toDni.Node?.nodeId ?? "";
                EditorUtility.SetDirty(fromDni.tree);
                return true;
            }

            // ── DialogEdgeAction → DialogIn (other tree) = SwitchDialogTree ──
            if (fromPort == PortSemantic.DialogEdgeAction && toPort == PortSemantic.DialogIn &&
                fromNode is DialogNodeInfo fromDni2 && toNode is DialogNodeInfo toDni2 && fromDni2.tree != toDni2.tree)
            {
                var edge = GetEdge(fromDni2, edgeIndex);
                if (edge == null) return false;
                edge.action = new DialogueAction { type = DialogueActionType.SwitchDialogTree, dialogTreeRef = toDni2.tree };
                edge.targetNodeId = ""; // Switch overrides target
                EditorUtility.SetDirty(fromDni2.tree);
                return true;
            }

            // ── ObjectiveTarget → NpcTargetedBy ──
            if (fromPort == PortSemantic.ObjectiveTarget && toPort == PortSemantic.NpcTargetedBy &&
                fromNode is ObjectiveNodeInfo oni && toNode is NpcNodeInfo nni)
            {
                var obj = oni.Obj;
                if (obj == null) return false;
                obj.targetNpc = nni.npc;
                obj.targetNpcId = nni.npc.npcId;
                obj.objectiveType = QuestObjectiveType.TalkToNpc;
                EditorUtility.SetDirty(oni.quest);
                return true;
            }

            // ── NpcOffersQuest → QuestOfferedBy ──
            if (fromPort == PortSemantic.NpcOffersQuest && toPort == PortSemantic.QuestOfferedBy &&
                fromNode is NpcNodeInfo nni2 && toNode is QuestNodeInfo qni2)
            {
                var list = nni2.npc.questOfferRefs?.ToList() ?? new List<QuestDefinition>();
                if (!list.Contains(qni2.quest)) list.Add(qni2.quest);
                nni2.npc.questOfferRefs = list.ToArray();
                EditorUtility.SetDirty(nni2.npc);
                return true;
            }

            // ── StageNext → StageIn ──
            if (fromPort == PortSemantic.StageNext && toPort == PortSemantic.StageIn &&
                fromNode is StageNodeInfo fromSni && toNode is StageNodeInfo toSni)
            {
                fromSni.Stage.nextStageId = toSni.Stage?.stageId ?? "";
                EditorUtility.SetDirty(fromSni.quest);
                return true;
            }

            return false;
        }

        /// <summary>Remove a connection between two ports.</summary>
        public bool RemoveConnection(object fromNode, PortSemantic fromPort, object toNode, PortSemantic toPort, int edgeIndex = -1)
        {
            if (fromPort == PortSemantic.DialogEdgeAction && fromNode is DialogNodeInfo dni)
            {
                var edge = GetEdge(dni, edgeIndex);
                if (edge == null) return false;

                if (toPort == PortSemantic.StageIn || toPort == PortSemantic.QuestOfferedBy)
                {
                    // Clear OfferQuest action
                    edge.action = null;
                    EditorUtility.SetDirty(dni.tree);
                    return true;
                }
                if (toPort == PortSemantic.DialogIn && toNode is DialogNodeInfo toDni && dni.tree != toDni.tree)
                {
                    // Clear SwitchDialogTree action
                    edge.action = null;
                    EditorUtility.SetDirty(dni.tree);
                    return true;
                }
            }

            if (fromPort == PortSemantic.ObjectiveTarget && fromNode is ObjectiveNodeInfo oni && toPort == PortSemantic.NpcTargetedBy)
            {
                var obj = oni.Obj;
                if (obj == null) return false;
                obj.targetNpc = null;
                obj.targetNpcId = "";
                EditorUtility.SetDirty(oni.quest);
                return true;
            }

            if (fromPort == PortSemantic.NpcOffersQuest && fromNode is NpcNodeInfo nni && toPort == PortSemantic.QuestOfferedBy && toNode is QuestNodeInfo qni)
            {
                var list = nni.npc.questOfferRefs?.ToList() ?? new List<QuestDefinition>();
                list.Remove(qni.quest);
                nni.npc.questOfferRefs = list.ToArray();
                EditorUtility.SetDirty(nni.npc);
                return true;
            }

            return false;
        }

        // ── Helpers ──

        private static DialogueEdge GetEdge(DialogNodeInfo dni, int edgeIndex)
        {
            var node = dni.Node;
            if (node?.edges == null || edgeIndex < 0 || edgeIndex >= node.edges.Length) return null;
            return node.edges[edgeIndex];
        }

        private static bool HasReward(QuestReward r) =>
            r != null && (r.credits > 0 || (r.items != null && r.items.Length > 0)
                || (r.cargoItems != null && r.cargoItems.Length > 0)
                || (r.reputation != null && r.reputation.Length > 0));
    }
}
#endif
