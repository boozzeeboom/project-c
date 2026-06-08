// T-Q03: DialogueNode + DialogueEdge — flat-list POCO graph nodes.
// См. docs/NPC_quests/02_V2_ARCHITECTURE.md §2.3.7-2.3.9.
//
// Graph model: DialogTree.nodes[] — flat list of DialogueNode. Each node
// carries DialogueEdge[] which references targetNodeId by string (NOT
// ScriptableObject ref, чтобы можно было rename'ить / link'ить). Tradeoff:
// dangling edges need runtime validation. Edge with targetNodeId == "" or null
// == EndConversation.

using System;
using UnityEngine;

namespace ProjectC.Dialogue
{
    /// <summary>
    /// One player-facing choice on a dialogue node. Carries its own
    /// <see cref="DialogueCondition"/> (gate) and <see cref="DialogueAction"/>
    /// (effect fired on selection BEFORE the transition).
    /// </summary>
    /// <remarks>
    /// We avoid direct ScriptableObject refs to nodes — edges reference
    /// targetNodeId by string. This keeps rename-friendly and avoids
    /// "asset dependency explosion" in the asset graph.
    /// </remarks>
    [Serializable]
    public class DialogueEdge
    {
        [Tooltip("Player-visible label, e.g. 'Я помогу. (Принять квест)'. Loc key в будущем.")]
        public string label = "Continue";

        [Tooltip("nodeId целевой ноды. Empty/null = EndConversation (Edge.fireAction всё равно сработает).")]
        public string targetNodeId = "";

        [Tooltip("Single condition (legacy/shortcut). null = always available. Prefer conditions[] для multi-AND.")]
        public DialogueCondition condition;

        [Tooltip("Multiple atomic conditions, all must pass (AND semantics). " +
                 "Предпочтительнее чем single condition для multi-gate. " +
                 "Если задан — `condition` ignored. Empty = no condition.")]
        public DialogueCondition[] conditions = Array.Empty<DialogueCondition>();

        [Tooltip("Effect, fires on selection BEFORE transition к targetNodeId. " +
                 "Типичные: OfferQuest, GiveCredits, AddReputation, OpenMarket, EndConversation.")]
        public DialogueAction action;

        [Tooltip("Если true и condition false → edge скрывается полностью. " +
                 "Если false → edge отображается серым (greyed out) с подсказкой.")]
        public bool hideIfUnavailable = true;
    }

    /// <summary>
    /// One node in a dialogue tree. Owns its outgoing <see cref="DialogueEdge"/>
    /// array (graph edge-list representation; alternative would be separate
    /// edges[] on the tree level, but per-node keeps authoring local).
    /// </summary>
    [Serializable]
    public class DialogueNode
    {
        [Tooltip("Unique id within tree, referenced by edges' targetNodeId. " +
                 "Do NOT reuse across trees unless you want cross-tree jumps.")]
        public string nodeId = "";

        [Tooltip("Кто говорит эту реплику (NPC / Player / Narrator)")]
        public SpeakerRef speaker = new SpeakerRef { speakerKind = SpeakerRef.Kind.Npc, refId = "" };

        [Tooltip("Текст реплики. Loc key в будущем, пока литерал. Plain string — UI Toolkit рендерит как <Label>.")]
        [TextArea(2, 6)]
        public string text = "";

        [Tooltip("Variant of portrait (e.g. 'neutral', 'angry', 'thoughtful'). " +
                 "NPC prefab is expected to have variants named '<base>_<variant>'. Empty = default.")]
        public string portraitEmotion = "neutral";

        [Tooltip("Outgoing edges (player-visible choices). Пустой массив = dead end → UI показывает только 'EndConversation'.")]
        public DialogueEdge[] edges = Array.Empty<DialogueEdge>();

        [Tooltip("Effects, fires ONCE when this node is shown (server evaluates). " +
                 "Use для: voice line cue, ambient SFX, scripted camera move.")]
        public DialogueAction[] onEnterActions = Array.Empty<DialogueAction>();
    }
}
