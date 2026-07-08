// Project C: Real-Time Combat Engine — T-NPC-S03
// NpcIdleActivity: enum 8 типов idle-активностей для NPC.
// Design: docs/Character/Skills/real-time-combat/npc-enemy/04_UNIFIED_BEHAVIOR_ARCHITECTURE.md §5.

namespace ProjectC.AI
{
    /// <summary>
    /// Тип idle-активности NPC (когда не в бою).
    /// </summary>
    public enum NpcIdleActivity
    {
        /// <summary>Текущее поведение (default). NPC стоит на месте.</summary>
        StandStill,
        /// <summary>Патруль по waypoints: Loop / PingPong / Random.</summary>
        Patrol,
        /// <summary>Head-tracking анимация (осматривается).</summary>
        LookAround,
        /// <summary>Взаимодействие с другим NPC (жесты, диалог).</summary>
        Socialize,
        /// <summary>Имитация работы (анимация).</summary>
        Work,
        /// <summary>Сидит на chair/box.</summary>
        Sit,
        /// <summary>Лежит, не реагирует на proximity.</summary>
        Sleep,
        /// <summary>Случайные движения в небольшом радиусе от spawn.</summary>
        Wander,
    }

    /// <summary>
    /// Паттерн движения по waypoints.
    /// </summary>
    public enum PatrolPattern
    {
        /// <summary>По кругу: 0→1→2→...→n→0→1→...</summary>
        Loop,
        /// <summary>Туда-обратно: 0→1→2→...→n→n-1→...→0→1→...</summary>
        PingPong,
        /// <summary>Случайный выбор следующей точки.</summary>
        Random,
    }
}
