// QuestGraphModel v5.2 — pure C# adapter. No ObjectiveNodeInfo (objectives live inside StageNode).
// Stage→NPC edge: StageNode has "→ Target NPC" port based on its first TalkToNpc/DeliverItem objective.

#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using ProjectC.Dialogue;

namespace ProjectC.Quests.Editor
{
    public enum PortSemantic
    {
        NpcOffersQuest, NpcDefaultDialog, DialogEdgeAction, StageNext, StageTargetNpc,
        QuestOfferedBy, DialogIn, StageIn, NpcTargetedBy,
    }

    public class NpcNodeInfo    { public NpcDefinition npc; public override int GetHashCode() => npc?.GetHashCode() ?? 0; public override bool Equals(object o) => o is NpcNodeInfo n && n.npc == npc; }
    public class DialogNodeInfo { public DialogTree tree; public int nodeIndex; public DialogueNode Node => tree?.nodes != null && nodeIndex >= 0 && nodeIndex < tree.nodes.Length ? tree.nodes[nodeIndex] : null; public override int GetHashCode() => (tree, nodeIndex).GetHashCode(); public override bool Equals(object o) => o is DialogNodeInfo d && d.tree == tree && d.nodeIndex == nodeIndex; }
    public class QuestNodeInfo  { public QuestDefinition quest; public override int GetHashCode() => quest?.GetHashCode() ?? 0; public override bool Equals(object o) => o is QuestNodeInfo q && q.quest == quest; }
    public class StageNodeInfo  { public QuestDefinition quest; public int stageIndex; public QuestStage Stage => quest?.stages != null && stageIndex >= 0 && stageIndex < quest.stages.Length ? quest.stages[stageIndex] : null; public override int GetHashCode() => (quest, stageIndex).GetHashCode(); public override bool Equals(object o) => o is StageNodeInfo s && s.quest == quest && s.stageIndex == stageIndex; }
    public class RewardNodeInfo { public QuestDefinition quest; public override int GetHashCode() => quest?.GetHashCode() ?? 0; public override bool Equals(object o) => o is RewardNodeInfo r && r.quest == quest; }

    public class EdgeInfo
    {
        public object fromNode, toNode;
        public PortSemantic fromPort, toPort;
        public int extraIndex = -1;
    }

    public class QuestGraphModel
    {
        private readonly List<NpcDefinition> _npcs = new();
        private readonly List<DialogTree> _dialogs = new();
        private readonly List<QuestDefinition> _quests = new();

        public List<NpcNodeInfo> NpcNodes { get; } = new();
        public List<DialogNodeInfo> DialogNodes { get; } = new();
        public List<QuestNodeInfo> QuestNodes { get; } = new();
        public List<StageNodeInfo> StageNodes { get; } = new();
        public List<RewardNodeInfo> RewardNodes { get; } = new();
        public List<EdgeInfo> Edges { get; } = new();

        public int TotalNodeCount => NpcNodes.Count + DialogNodes.Count + QuestNodes.Count + StageNodes.Count + RewardNodes.Count;

        public void AddNpc(NpcDefinition npc)
        {
            if (npc == null || _npcs.Contains(npc)) return; _npcs.Add(npc);
            if (npc.defaultDialogTree != null && !_dialogs.Contains(npc.defaultDialogTree)) _dialogs.Add(npc.defaultDialogTree);
            if (npc.defaultDialogTree?.nodes != null)
                foreach (var n in npc.defaultDialogTree.nodes)
                    if (n?.edges != null) foreach (var e in n.edges)
                        if (e?.action?.type == DialogueActionType.SwitchDialogTree && e.action.dialogTreeRef != null && !_dialogs.Contains(e.action.dialogTreeRef))
                            _dialogs.Add(e.action.dialogTreeRef);
            if (npc.questOfferRefs != null) foreach (var q in npc.questOfferRefs) if (q != null && !_quests.Contains(q)) _quests.Add(q);
            if (npc.questTurnInRefs != null) foreach (var q in npc.questTurnInRefs) if (q != null && !_quests.Contains(q)) _quests.Add(q);
        }

        public void AddQuest(QuestDefinition q)
        {
            if (q == null || _quests.Contains(q)) return; _quests.Add(q);
            if (q.stages != null) foreach (var s in q.stages)
                if (s?.objectives != null) foreach (var o in s.objectives)
                    if (o?.targetNpc != null && !_npcs.Contains(o.targetNpc)) _npcs.Add(o.targetNpc);
        }

        public void AddDialogTree(DialogTree t) { if (t != null && !_dialogs.Contains(t)) _dialogs.Add(t); }

        public void Clear() { _npcs.Clear(); _dialogs.Clear(); _quests.Clear(); NpcNodes.Clear(); DialogNodes.Clear(); QuestNodes.Clear(); StageNodes.Clear(); RewardNodes.Clear(); Edges.Clear(); }

        public void BuildGraph()
        {
            NpcNodes.Clear(); DialogNodes.Clear(); QuestNodes.Clear(); StageNodes.Clear(); RewardNodes.Clear(); Edges.Clear();
            foreach (var npc in _npcs) NpcNodes.Add(new NpcNodeInfo { npc = npc });
            foreach (var tree in _dialogs)
            { if (tree.nodes == null) continue; for (int i = 0; i < tree.nodes.Length; i++) if (tree.nodes[i] != null) DialogNodes.Add(new DialogNodeInfo { tree = tree, nodeIndex = i }); }
            foreach (var quest in _quests)
            {
                QuestNodes.Add(new QuestNodeInfo { quest = quest });
                if (quest.stages != null)
                    for (int si = 0; si < quest.stages.Length; si++)
                        if (quest.stages[si] != null) StageNodes.Add(new StageNodeInfo { quest = quest, stageIndex = si });
                if (HasReward(quest.rewards)) RewardNodes.Add(new RewardNodeInfo { quest = quest });
            }
            BuildNpcEdges(); BuildDialogEdges(); BuildQuestEdges();
        }

        private void BuildNpcEdges()
        {
            foreach (var npc in _npcs)
            {
                var npcNode = NpcNodes.First(n => n.npc == npc);
                if (npc.defaultDialogTree != null)
                {
                    var root = DialogNodes.FirstOrDefault(d => d.tree == npc.defaultDialogTree && d.Node != null && d.Node.nodeId == npc.defaultDialogTree.rootNodeId);
                    if (root?.tree != null) Edges.Add(new EdgeInfo { fromNode = npcNode, fromPort = PortSemantic.NpcDefaultDialog, toNode = root, toPort = PortSemantic.DialogIn });
                }
                if (npc.questOfferRefs != null) foreach (var q in npc.questOfferRefs)
                {
                    if (q == null) continue;
                    var qn = QuestNodes.FirstOrDefault(n => n.quest == q);
                    if (qn?.quest != null) Edges.Add(new EdgeInfo { fromNode = npcNode, fromPort = PortSemantic.NpcOffersQuest, toNode = qn, toPort = PortSemantic.QuestOfferedBy });
                }
            }
        }

        private void BuildDialogEdges()
        {
            foreach (var dn in DialogNodes)
            {
                var node = dn.Node; if (node?.edges == null) continue;
                for (int ei = 0; ei < node.edges.Length; ei++)
                {
                    var edge = node.edges[ei]; if (edge == null) continue;
                    if (!string.IsNullOrEmpty(edge.targetNodeId))
                    {
                        var target = DialogNodes.FirstOrDefault(d => d.tree == dn.tree && d.Node != null && d.Node.nodeId == edge.targetNodeId);
                        if (target?.tree != null) Edges.Add(new EdgeInfo { fromNode = dn, fromPort = PortSemantic.DialogEdgeAction, toNode = target, toPort = PortSemantic.DialogIn, extraIndex = ei });
                    }
                    if (edge.action != null && edge.action.type == DialogueActionType.OfferQuest && edge.action.questRef != null)
                    {
                        var firstStage = StageNodes.FirstOrDefault(s => s.quest == edge.action.questRef && s.stageIndex == 0);
                        if (firstStage?.quest != null) Edges.Add(new EdgeInfo { fromNode = dn, fromPort = PortSemantic.DialogEdgeAction, toNode = firstStage, toPort = PortSemantic.StageIn, extraIndex = ei });
                    }
                    if (edge.action != null && edge.action.type == DialogueActionType.SwitchDialogTree && edge.action.dialogTreeRef != null)
                    {
                        var targetTree = edge.action.dialogTreeRef;
                        var targetRoot = DialogNodes.FirstOrDefault(d => d.tree == targetTree && d.Node != null && d.Node.nodeId == targetTree.rootNodeId);
                        if (targetRoot?.tree != null) Edges.Add(new EdgeInfo { fromNode = dn, fromPort = PortSemantic.DialogEdgeAction, toNode = targetRoot, toPort = PortSemantic.DialogIn, extraIndex = ei });
                    }
                }
            }
        }

        private void BuildQuestEdges()
        {
            foreach (var quest in _quests)
            {
                if (quest.stages == null || quest.stages.Length == 0) continue;
                // Quest root → Stage 0
                var qNode = QuestNodes.First(n => n.quest == quest);
                var s0 = StageNodes.FirstOrDefault(s => s.quest == quest && s.stageIndex == 0);
                if (qNode?.quest != null && s0?.quest != null)
                    Edges.Add(new EdgeInfo { fromNode = qNode, fromPort = PortSemantic.StageNext, toNode = s0, toPort = PortSemantic.StageIn });

                // Stage → next stage (via stage.nextStageId)
                for (int si = 0; si < quest.stages.Length; si++)
                {
                    var stage = quest.stages[si]; if (stage == null) continue;
                    var thisStage = StageNodes.FirstOrDefault(s => s.quest == quest && s.stageIndex == si);
                    if (!string.IsNullOrEmpty(stage.nextStageId))
                    {
                        var nextStage = StageNodes.FirstOrDefault(s => s.quest == quest && s.Stage != null && s.Stage.stageId == stage.nextStageId);
                        if (thisStage?.quest != null && nextStage?.quest != null)
                            Edges.Add(new EdgeInfo { fromNode = thisStage, fromPort = PortSemantic.StageNext, toNode = nextStage, toPort = PortSemantic.StageIn });
                    }
                    // Stage → NPC (first TalkToNpc/DeliverItem objective)
                    if (stage.objectives != null && thisStage?.quest != null)
                    {
                        var talkObj = stage.objectives.FirstOrDefault(o => o != null && o.targetNpc != null && (o.objectiveType == QuestObjectiveType.TalkToNpc || o.objectiveType == QuestObjectiveType.DeliverItem));
                        if (talkObj != null)
                        {
                            var npcNode = NpcNodes.FirstOrDefault(n => n.npc == talkObj.targetNpc);
                            if (npcNode?.npc != null)
                                Edges.Add(new EdgeInfo { fromNode = thisStage, fromPort = PortSemantic.StageTargetNpc, toNode = npcNode, toPort = PortSemantic.NpcTargetedBy });
                        }
                    }
                }

                // Last stage → Reward
                if (RewardNodes.Any(r => r.quest == quest))
                {
                    int lastIdx = quest.stages.Length - 1;
                    var lastStage = StageNodes.FirstOrDefault(s => s.quest == quest && s.stageIndex == lastIdx);
                    var reward = RewardNodes.First(r => r.quest == quest);
                    if (lastStage?.quest != null && reward?.quest != null)
                        Edges.Add(new EdgeInfo { fromNode = lastStage, fromPort = PortSemantic.StageNext, toNode = reward, toPort = PortSemantic.StageIn });
                }
            }
        }

        // ── Write-through ──

        public bool SetConnection(object fromNode, PortSemantic fromPort, object toNode, PortSemantic toPort, int edgeIndex = -1)
        {
            if (fromPort == PortSemantic.DialogEdgeAction && toPort == PortSemantic.StageIn && fromNode is DialogNodeInfo dni)
            {
                var edge = GetEdge(dni, edgeIndex); if (edge == null) return false;
                QuestDefinition quest = (toNode as StageNodeInfo)?.quest ?? (toNode as QuestNodeInfo)?.quest;
                if (quest == null) return false;
                edge.action = new DialogueAction { type = DialogueActionType.OfferQuest, questRef = quest, stringParam = quest.questId };
                EditorUtility.SetDirty(dni.tree); return true;
            }
            if (fromPort == PortSemantic.DialogEdgeAction && toPort == PortSemantic.DialogIn && fromNode is DialogNodeInfo fromDni && toNode is DialogNodeInfo toDni)
            {
                var edge = GetEdge(fromDni, edgeIndex); if (edge == null) return false;
                if (fromDni.tree == toDni.tree) { edge.targetNodeId = toDni.Node?.nodeId ?? ""; EditorUtility.SetDirty(fromDni.tree); return true; }
                else { edge.action = new DialogueAction { type = DialogueActionType.SwitchDialogTree, dialogTreeRef = toDni.tree }; edge.targetNodeId = ""; EditorUtility.SetDirty(fromDni.tree); return true; }
            }
            if (fromPort == PortSemantic.StageTargetNpc && toPort == PortSemantic.NpcTargetedBy && fromNode is StageNodeInfo sni && toNode is NpcNodeInfo nni)
            {
                var stage = sni.Stage; if (stage?.objectives == null) return false;
                var obj = stage.objectives.FirstOrDefault(o => o != null && (o.objectiveType == QuestObjectiveType.TalkToNpc || o.objectiveType == QuestObjectiveType.DeliverItem));
                if (obj == null) { obj = new QuestObjective { objectiveId = "talk_to", objectiveType = QuestObjectiveType.TalkToNpc }; var list = stage.objectives.ToList(); list.Add(obj); stage.objectives = list.ToArray(); }
                obj.targetNpc = nni.npc; obj.targetNpcId = nni.npc.npcId; obj.objectiveType = QuestObjectiveType.TalkToNpc;
                EditorUtility.SetDirty(sni.quest); return true;
            }
            if (fromPort == PortSemantic.NpcOffersQuest && toPort == PortSemantic.QuestOfferedBy && fromNode is NpcNodeInfo nni2 && toNode is QuestNodeInfo qni2)
            {
                var list = nni2.npc.questOfferRefs?.ToList() ?? new List<QuestDefinition>(); if (!list.Contains(qni2.quest)) list.Add(qni2.quest);
                nni2.npc.questOfferRefs = list.ToArray(); EditorUtility.SetDirty(nni2.npc); return true;
            }
            if (fromPort == PortSemantic.StageNext && toPort == PortSemantic.StageIn && fromNode is StageNodeInfo fromSni && toNode is StageNodeInfo toSni)
            {
                fromSni.Stage.nextStageId = toSni.Stage?.stageId ?? ""; EditorUtility.SetDirty(fromSni.quest); return true;
            }
            return false;
        }

        public bool RemoveConnection(object fromNode, PortSemantic fromPort, object toNode, PortSemantic toPort, int edgeIndex = -1)
        {
            if (fromPort == PortSemantic.DialogEdgeAction && fromNode is DialogNodeInfo dni)
            {
                var edge = GetEdge(dni, edgeIndex); if (edge == null) return false;
                edge.action = null; EditorUtility.SetDirty(dni.tree); return true;
            }
            if (fromPort == PortSemantic.StageTargetNpc && fromNode is StageNodeInfo sni && toPort == PortSemantic.NpcTargetedBy)
            {
                var stage = sni.Stage; if (stage?.objectives == null) return false;
                var obj = stage.objectives.FirstOrDefault(o => o != null && o.targetNpc != null); if (obj == null) return false;
                obj.targetNpc = null; obj.targetNpcId = ""; EditorUtility.SetDirty(sni.quest); return true;
            }
            if (fromPort == PortSemantic.NpcOffersQuest && fromNode is NpcNodeInfo nni && toNode is QuestNodeInfo qni)
            {
                var list = nni.npc.questOfferRefs?.ToList() ?? new List<QuestDefinition>(); list.Remove(qni.quest);
                nni.npc.questOfferRefs = list.ToArray(); EditorUtility.SetDirty(nni.npc); return true;
            }
            return false;
        }

        // ── Stage CRUD (called from UI) ──

        public StageNodeInfo AddStage(QuestDefinition quest)
        {
            var list = quest.stages?.ToList() ?? new List<QuestStage>();
            var s = new QuestStage { stageId = $"stage_{list.Count}", description = "" };
            // Chain: previous last stage → new stage
            if (list.Count > 0 && list[list.Count - 1] != null)
                list[list.Count - 1].nextStageId = s.stageId;
            list.Add(s); quest.stages = list.ToArray(); EditorUtility.SetDirty(quest);
            return new StageNodeInfo { quest = quest, stageIndex = list.Count - 1 };
        }


        public void DeleteStage(StageNodeInfo sni)
        {
            var list = sni.quest.stages.ToList();
            if (sni.stageIndex < 0 || sni.stageIndex >= list.Count) return;
            list.RemoveAt(sni.stageIndex); sni.quest.stages = list.ToArray(); EditorUtility.SetDirty(sni.quest);
        }

        public void AddObjective(StageNodeInfo sni)
        {
            var stage = sni.Stage; if (stage == null) return;
            var list = stage.objectives?.ToList() ?? new List<QuestObjective>();
            list.Add(new QuestObjective { objectiveId = $"obj_{list.Count}", objectiveType = QuestObjectiveType.HaveItem, requiredQuantity = 1 });
            stage.objectives = list.ToArray(); EditorUtility.SetDirty(sni.quest);
        }

        public void DeleteObjective(StageNodeInfo sni, int objIndex)
        {
            var stage = sni.Stage; if (stage?.objectives == null || objIndex < 0 || objIndex >= stage.objectives.Length) return;
            var list = stage.objectives.ToList(); list.RemoveAt(objIndex); stage.objectives = list.ToArray(); EditorUtility.SetDirty(sni.quest);
        }

        private static DialogueEdge GetEdge(DialogNodeInfo dni, int edgeIndex)
        { var node = dni.Node; if (node?.edges == null || edgeIndex < 0 || edgeIndex >= node.edges.Length) return null; return node.edges[edgeIndex]; }

        private static bool HasReward(QuestReward r) => r != null && (r.credits > 0 || (r.items != null && r.items.Length > 0) || (r.cargoItems != null && r.cargoItems.Length > 0) || (r.reputation != null && r.reputation.Length > 0));
    }
}
#endif
