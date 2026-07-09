// Project C: Real-Time Combat Engine — T-NPC-11
// ISpawnRestartTrigger: интерфейс для компонентов, управляющих перезапуском цикла спавна.
// Design: docs/Character/Skills/real-time-combat/npc-enemy/07_SPAWN_CYCLE_CONTROL.md §3.3

namespace ProjectC.AI
{
    /// <summary>
    /// Компонент, который сообщает NpcSpawner'у «пора перезапустить цикл».
    /// Вешается на любой GameObject. NpcSpawner опрашивает все зарегистрированные
    /// триггеры и перезапускает цикл когда все сработали.
    /// </summary>
    public interface ISpawnRestartTrigger
    {
        /// <summary>Состояние триггера прямо сейчас. Вызывается спавнером каждый опрос (≈1 раз/сек).</summary>
        bool IsTriggered { get; }

        /// <summary>Вызывается спавнером когда цикл исчерпан (все NPC мертвы).</summary>
        void OnCycleExhausted();

        /// <summary>Вызывается спавнером когда цикл перезапущен.</summary>
        void OnCycleStarted();

        /// <summary>Вызывается при регистрации в спавнере (один раз в OnNetworkSpawn).</summary>
        void OnRegistered(NpcSpawner spawner);
    }
}
