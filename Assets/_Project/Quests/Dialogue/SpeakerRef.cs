// T-Q03: SpeakerRef — кто говорит реплику в диалоге.
// См. docs/NPC_quests/02_V2_ARCHITECTURE.md §2.3.8.

using UnityEngine;

namespace ProjectC.Dialogue
{
    /// <summary>
    /// Speaker reference in a dialogue node. Three flavours:
    /// <list type="bullet">
    ///   <item><see cref="Kind"/> == <c>Npc</c> → <see cref="RefId"/> is an NPC id
    ///   (matches <c>NpcDefinition.npcId</c>); portrait + name come from that SO.</item>
    ///   <item><see cref="Kind"/> == <c>Player</c> → <see cref="RefId"/> ignored
    ///   (player portrait/name shown from the local player state).</item>
    ///   <item><see cref="Kind"/> == <c>Narrator</c> → no portrait, italic text,
    ///   used for stage directions, system messages, ambient flavour.</item>
    /// </list>
    /// </summary>
    /// <remarks>
    /// Localized later (T-Q09 or localization pass) — for v1 we just take strings
    /// as-is. E1 (gender) deferred per docs/NPC_quests/09_OPEN_QUESTIONS.md §E1.
    /// </remarks>
    [System.Serializable]
    public class SpeakerRef
    {
        public enum Kind : byte
        {
            Npc = 0,
            Player = 1,
            Narrator = 2
        }

        [Tooltip("Тип говорящего: NPC / Player / Narrator")]
        public Kind speakerKind = Kind.Npc;

        [Tooltip("NPC id (если speakerKind=Npc), иначе ignored")]
        public string refId = "";
    }
}
