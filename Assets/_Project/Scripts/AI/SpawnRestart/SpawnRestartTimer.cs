// Project C: Real-Time Combat Engine — T-NPC-11
// SpawnRestartTimer: перезапускает цикл спавна через N секунд после exhaust.
// Design: docs/Character/Skills/real-time-combat/npc-enemy/07_SPAWN_CYCLE_CONTROL.md §3.4-A

using UnityEngine;

namespace ProjectC.AI
{
    /// <summary>
    /// Триггер перезапуска по таймеру.
    /// После вызова OnCycleExhausted() ждёт delaySeconds, затем выставляет IsTriggered=true.
    /// </summary>
    public class SpawnRestartTimer : MonoBehaviour, ISpawnRestartTrigger
    {
        [Header("Timer")]
        [Tooltip("Секунд ожидания после exhaust перед сигналом перезапуска.")]
        [Range(1f, 3600f)]
        public float delaySeconds = 120f;

        [Tooltip("Если true — таймер запускается автоматически при старте (без ожидания exhaust). " +
                 "Полезно для самой первой волны: задержка перед первым спавном.")]
        public bool startOnAwake = false;

        public bool IsTriggered => _triggered;

        private bool _triggered;
        private float _timerStartedAt = -1f;
        private bool _timerRunning;

        private void Awake()
        {
            if (startOnAwake)
            {
                _timerStartedAt = Time.unscaledTime;
                _timerRunning = true;
            }
        }

        private void Update()
        {
            if (!_timerRunning) return;

            if (Time.unscaledTime - _timerStartedAt >= delaySeconds)
            {
                _triggered = true;
                _timerRunning = false;
            }
        }

        public void OnCycleExhausted()
        {
            _triggered = false;
            _timerStartedAt = Time.unscaledTime;
            _timerRunning = true;
        }

        public void OnCycleStarted()
        {
            _triggered = false;
            _timerRunning = false;
        }

        public void OnRegistered(NpcSpawner spawner)
        {
            // No-op: таймер самодостаточен.
        }
    }
}
