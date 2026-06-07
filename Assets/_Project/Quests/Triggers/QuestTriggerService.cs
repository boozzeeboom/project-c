// T-Q06: QuestTriggerService — server-side singleton. Manages trigger instances
// per player, evaluates при получении event'ов от WorldEventBus.
// См. docs/NPC_quests/06_TRIGGERS_AND_INTEGRATION.md §6.3, §6.5, §6.7.
//
// Design: full event-driven, не polling. QuestServer подписывается на
// WorldEventBus и вызывает service.Evaluate(playerId, hint) при получении event.
//
// T-Q06 scope: Evaluate() + Attach/Detach + factories по triggerTypeId.
// Hint-формат: "TriggerId" (e.g. "HaveItem:42", "TalkedToNpc:mira_01") —
// service фильтрует triggers по hint prefix.

using System;
using System.Collections.Generic;
using ProjectC.Quests;

namespace ProjectC.Quests.Triggers
{
    /// <summary>
    /// Manages IQuestTrigger instances per player. Singleton (one per server).
    /// </summary>
    public sealed class QuestTriggerService
    {
        /// <summary>Per-player list of active triggers.</summary>
        private readonly Dictionary<ulong, List<IQuestTrigger>> _playerTriggers = new Dictionary<ulong, List<IQuestTrigger>>();

        /// <summary>Factory registry: trigger type id (e.g. "TalkedToNpc") → constructor.</summary>
        private readonly Dictionary<string, Func<IQuestTrigger>> _factories = new Dictionary<string, Func<IQuestTrigger>>();

        private readonly QuestWorld _world;

        public QuestTriggerService(QuestWorld world)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            RegisterDefaultFactories();
        }

        /// <summary>
        /// Register factory for trigger type id. Used to spawn new trigger instances
        /// per (playerId, questId, objectiveId) tuple.
        /// </summary>
        public void RegisterTriggerType(string typeId, Func<IQuestTrigger> factory)
        {
            if (string.IsNullOrEmpty(typeId) || factory == null) return;
            _factories[typeId] = factory;
        }

        /// <summary>
        /// Attach trigger instance к player. Server creates QuestInstance + attaches
        /// all matching triggers when player accepts a quest.
        /// </summary>
        public void Attach(ulong playerId, IQuestTrigger trigger)
        {
            if (trigger == null) return;
            if (!_playerTriggers.TryGetValue(playerId, out var list))
            {
                list = new List<IQuestTrigger>();
                _playerTriggers[playerId] = list;
            }
            list.Add(trigger);
        }

        /// <summary>Detach all triggers for a quest instance (на stage/quest completion).</summary>
        public void Detach(ulong playerId, QuestInstance instance)
        {
            if (instance == null) return;
            if (!_playerTriggers.TryGetValue(playerId, out var list)) return;
            // T-Q06: triggers не привязаны к instance напрямую (нет QuestInstance ref в IQuestTrigger).
            // Detach all — full reset per quest. Simpler. QuestServer track'ит когда заново attach'ить.
            list.Clear();
        }

        /// <summary>
        /// Evaluate triggers для player. Optional hint фильтрует triggers по
        /// triggerId prefix (для efficiency). Hint format: "TriggerId" (e.g. "HaveItem:42").
        /// </summary>
        /// <returns>Number of objectives marked complete (для diagnostics).</returns>
        public int Evaluate(ulong playerId, string hint = null)
        {
            if (!_playerTriggers.TryGetValue(playerId, out var list)) return 0;
            if (list.Count == 0) return 0;

            int advances = 0;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var trigger = list[i];
                if (hint != null && !trigger.TriggerId.StartsWith(hint, StringComparison.Ordinal)) continue;

                // Iterate all active quests for this player
                var playerQuests = _world.GetPlayerQuests(playerId);
                for (int q = 0; q < playerQuests.Count; q++)
                {
                    var quest = playerQuests[q];
                    if (quest.state != QuestState.Active) continue;

                    // QuestInstance не имеет direct objectiveId ref, проверяем все objectives в current stage
                    var def = _world.GetQuest(quest.questId);
                    if (def == null) continue;
                    var stage = def.GetStage(quest.currentStageId);
                    if (stage == null) continue;

                    for (int o = 0; o < stage.objectives.Length; o++)
                    {
                        var obj = stage.objectives[o];
                        if (obj == null) continue;
                        if (MatchesObjective(trigger, obj))
                        {
                            if (_world.TryAdvanceObjective(playerId, quest.questId, obj.objectiveId))
                            {
                                advances++;
                            }
                        }
                    }
                }
            }
            return advances;
        }

        /// <summary>
        /// Check if a trigger's data matches an objective.
        /// T-Q06 minimal: TriggerId содержит objectiveId (e.g. "HaveItem:42" vs objective.objectiveId="HaveItem:42").
        /// T-Q15: расширить — string-params, faction, etc.
        /// </summary>
        private static bool MatchesObjective(IQuestTrigger trigger, QuestObjective obj)
        {
            // Convention: trigger.TriggerId = obj.objectiveId.
            // T-Q15: добавить type-aware matching (TalkToNpc → obj.targetNpcId, etc).
            return trigger.TriggerId == obj.objectiveId;
        }

        // ============================================================
        // Default factories
        // ============================================================

        private void RegisterDefaultFactories()
        {
            _factories["TalkedToNpc"] = () => new TalkedToNpcTrigger();
            _factories["HaveItem"] = () => new HaveItemTrigger();
            _factories["ReputationAtLeast"] = () => new ReputationAtLeastTrigger();
            _factories["NpcAttitudeAtLeast"] = () => new NpcAttitudeAtLeastTrigger();
            _factories["DayNightPhase"] = () => new DayNightPhaseTrigger();
            _factories["Event"] = () => new EventTrigger();
            _factories["CargoHasItem"] = () => new CargoHasItemTrigger();
            _factories["LocationReached"] = () => new LocationReachedTrigger();
            _factories["KilledEntity"] = () => new KilledEntityTrigger();
        }

        /// <summary>Total triggers across all players (debug stat).</summary>
        public int TotalTriggers
        {
            get
            {
                int n = 0;
                foreach (var kvp in _playerTriggers) n += kvp.Value.Count;
                return n;
            }
        }
    }
}
