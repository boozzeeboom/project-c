// T-Q02: NpcDefinition ScriptableObject (v2-replacement for v1 NpcData).
// См. docs/NPC_quests/02_V2_ARCHITECTURE.md §2.3.3 + §2.3.3a (AttitudeLink)
// + §7.1 (пример Mira). T-Q02 добавляет attitudeLinks[] per 09_OPEN_QUESTIONS.md §G.

using System;
using UnityEngine;
using ProjectC.Dialogue;

namespace ProjectC.Quests
{
    /// <summary>
    /// Per-NPC cross-faction influence: improving or worsening relationship with
    /// this NPC drags the related faction's reputation up/down.
    /// MVA: storage is live, cross-calc is applied in <c>QuestWorld.ModifyNpcAttitude</c>
    /// (T-Q13). Full formula → v2.
    /// </summary>
    [Serializable]
    public class AttitudeLink
    {
        [Tooltip("Фракция, репутация с которой сдвигается при изменении отношений с этим NPC")]
        public Factions.FactionId targetFaction = Factions.FactionId.None;

        [Tooltip("Дельта для targetFaction, когда отношение с этим NPC улучшается (может быть отрицательной — \"прокачка с вражеским NPC бьёт по союзной фракции\")")]
        public int deltaOnLike = 0;

        [Tooltip("Дельта для targetFaction, когда отношение с этим NPC ухудшается (например +3 к враждебной фракции, когда ты ссоришься с нашим NPC)")]
        public int deltaOnDislike = 0;
    }

    /// <summary>
    /// Service flags an NPC can provide. Bit flags so multiple can be set.
    /// Drives <c>DialogueAction.OpenMarket</c> / <c>OpenService</c> availability.
    /// </summary>
    [Flags]
    public enum NpcService
    {
        None = 0,
        Trade = 1 << 0,      // торговец (artifact vendor, common vendor)
        Repair = 1 << 1,     // ремонт корабля / модулей
        Refuel = 1 << 2,     // заправка / пополнение расходников
        Restock = 1 << 3,    // пополнение стандартного ассортимента
        Banking = 1 << 4,    // (future) кредиты / escrow
        Healing = 1 << 5     // (future) лечение / статус-effects
    }

    /// <summary>
    /// Canonical v2 NPC data. Replaces v1 <c>ProjectC.World.Npc.NpcData</c>
    /// (kept for backward compat as v1, slated for deletion in T-Q19).
    /// </summary>
    /// <remarks>
    /// Backend wiring (<c>NpcEntity</c> NetworkBehaviour + scene placement +
    /// <c>ScenePlacedObjectSpawner</c>) lands in T-Q05. Until then this SO
    /// can be authored in the Inspector and saved — no runtime use.
    /// </remarks>
    [CreateAssetMenu(fileName = "Npc_", menuName = "ProjectC/NPC Definition", order = 100)]
    public class NpcDefinition : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Уникальный ID NPC (например: 'mira_01', 'zoric_03'). Используется сервером как ключ в quest/attitude таблицах — НЕ МЕНЯТЬ после релиза.")]
        public string npcId = "";

        [Tooltip("Отображаемое имя (loc key в будущем)")]
        public string displayName = "Unknown NPC";

        [Tooltip("Фракция NPC (определяет стартовое отношение, репутационные пороги)")]
        public Factions.FactionId faction = Factions.FactionId.Neutral;

        [Header("Visuals")]
        [Tooltip("Портрет для DialogWindow (256x256+ желательно)")]
        public Sprite portrait;

        [Tooltip("Префаб NPC (mesh + animator + collider). T-Q05: scene placement через NpcEntity.")]
        public GameObject prefab;

        [Tooltip("Префикс имён Animator-триггеров (T-Q05: NpcEntity использует при вызове SetTrigger). " +
                 "Один SO AnimatorConfig не создаём в T-Q02 — обходимся string prefix, миграция позже при необходимости.")]
        public string animatorTriggerPrefix = "";

        [Header("Dialogue")]
        [Tooltip("Default dialog tree. Можно свапнуть в runtime через DialogueAction.SwitchDialogTree.")]
        public DialogTree defaultDialogTree;

        [Header("Quests")]
        [Tooltip("QuestId, которые этот NPC может предложить (dialogue offer → QuestServer.TryOffer)")]
        public string[] questOffers = Array.Empty<string>();

        [Tooltip("QuestId, которые этот NPC может принять как turn-in (objective 'return to <npcId>')")]
        public string[] questTurnIns = Array.Empty<string>();

        [Header("Services")]
        [Tooltip("Какие сервисы NPC предоставляет (битовая маска). Определяет доступность OpenMarket/OpenService диалоговых action'ов.")]
        public NpcService services = NpcService.None;

        [Header("Interaction")]
        [Tooltip("Радиус interact в метрах (E-key pipeline, T-Q08)")]
        [Min(0.1f)]
        public float interactionRadius = 3f;

        [Tooltip("Показывать ли greeting (HUD tooltip) при подходе (T-Q08 — пока stub, в v1 v1-логика NpcInteraction.ShowGreeting — заменяется в T-Q10)")]
        public bool showGreeting = true;

        [Tooltip("Текст greeting'а (loc key в будущем). Если showGreeting=false — игнорируется.")]
        [TextArea(1, 2)]
        public string greetingText = "Greetings, traveler.";

        [Header("Attitude (per A3 + §G)")]
        [Tooltip("Cross-faction influence: изменение отношения с этим NPC двигает reputation у других фракций. " +
                 "MVA-stub в T-Q13 (storage live, cross-calc MVP), full formula → v2.")]
        public AttitudeLink[] attitudeLinks = Array.Empty<AttitudeLink>();

        [Tooltip("Минимальное значение NpcAttitude (default = -100, hostile)")]
        [Range(-100, 200)]
        public int personalAttitudeMin = -100;

        [Tooltip("Максимальное значение NpcAttitude (default = 200, revered)")]
        [Range(-100, 200)]
        public int personalAttitudeMax = 200;

        [Header("Audio (optional)")]
        [Tooltip("Voice line префикс для интеграции с локализацией (если оставлено пустым — voice lines не воспроизводятся)")]
        public string voicePrefix = "";
    }
}
