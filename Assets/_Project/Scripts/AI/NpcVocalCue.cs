// Project C: Real-Time Combat Engine — T-NPC-S11
// NpcVocalCue: 5 типов голосовых сигналов NPC.
// Design: docs/Character/Skills/real-time-combat/npc-enemy/04_UNIFIED_BEHAVIOR_ARCHITECTURE.md §6.

namespace ProjectC.AI
{
    /// <summary>
    /// Голосовые сигналы NPC.
    /// Каждый сигнал — Animation Trigger + Gameplay-эффект через NpcGroupController.
    /// </summary>
    public enum NpcVocalCue
    {
        /// <summary>Переход Alert→Chase. Привлекает NPC в 15м (AllyInCombat).</summary>
        AlertCall,
        /// <summary>HP=0. Триггерит AllyKilled в 20м.</summary>
        DeathScream,
        /// <summary>Только что атаковал (Victory emotion). Дебафф цели (Phase 2+).</summary>
        Taunt,
        /// <summary>Fear emotion. Понижает morale союзников (−0.05).</summary>
        FearCry,
        /// <summary>Убил цель. Повышает morale союзников (+0.1), понижает врагам (−0.05).</summary>
        VictoryRoar,
    }
}
