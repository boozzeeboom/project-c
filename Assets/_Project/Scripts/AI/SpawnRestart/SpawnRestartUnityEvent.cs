// Project C: Real-Time Combat Engine — T-NPC-11
// SpawnRestartUnityEvent: ручной перезапуск цикла через вызов Restart() из любого скрипта.
// Design: docs/Character/Skills/real-time-combat/npc-enemy/07_SPAWN_CYCLE_CONTROL.md §3.4-C

using UnityEngine;
using UnityEngine.Events;

namespace ProjectC.AI
{
    /// <summary>
    /// Ручной триггер перезапуска.
    /// Вызови Restart() из любого скрипта, через Inspector (кнопка/событие) или из анимации.
    /// </summary>
    public class SpawnRestartUnityEvent : MonoBehaviour, ISpawnRestartTrigger
    {
        [Header("Manual Trigger")]
        [Tooltip("Вызывается когда кто-то дёргает Restart(). Можно подписать другие объекты.")]
        public UnityEvent onRestartRequested;

        [Tooltip("Сбрасывать ли триггер при старте нового цикла.")]
        public bool resetOnCycleStart = true;

        public bool IsTriggered => _triggered;

        private bool _triggered;

        /// <summary>
        /// Вызвать из любого скрипта чтобы сигнализировать перезапуск.
        /// Можно также повесить на кнопку через Inspector.
        /// </summary>
        public void Restart()
        {
            _triggered = true;
            onRestartRequested?.Invoke();
        }

        /// <summary>
        /// Сбросить триггер вручную (если нужно отменить).
        /// </summary>
        public void ResetTrigger()
        {
            _triggered = false;
        }

        public void OnCycleExhausted()
        {
            // Не сбрасываем: Restart() мог быть вызван заранее.
        }

        public void OnCycleStarted()
        {
            if (resetOnCycleStart)
                _triggered = false;
        }

        public void OnRegistered(NpcSpawner spawner)
        {
            // No-op.
        }
    }
}
