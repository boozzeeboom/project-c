// Project C: Real-Time Combat Engine — T-NPC-11
// SpawnRestartTriggerZone: перезапускает цикл спавна при входе/выходе игрока из зоны.
// Design: docs/Character/Skills/real-time-combat/npc-enemy/07_SPAWN_CYCLE_CONTROL.md §3.4-B

using UnityEngine;

namespace ProjectC.AI
{
    /// <summary>
    /// Триггер перезапуска по входу/выходу в зону (OnTriggerEnter/Exit).
    /// Требует Collider с isTrigger=true на этом же GameObject.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class SpawnRestartTriggerZone : MonoBehaviour, ISpawnRestartTrigger
    {
        public enum TriggerEvent
        {
            OnEnter,
            OnExit
        }

        [Header("Trigger Settings")]
        [Tooltip("OnEnter = игрок вошёл → перезапуск. OnExit = игрок вышел → перезапуск.")]
        public TriggerEvent triggerOn = TriggerEvent.OnExit;

        [Tooltip("Какие теги считать 'игроком'. По умолчанию только 'Player'.")]
        public string[] playerTags = { "Player" };

        [Tooltip("Сбрасывать ли триггер при старте нового цикла. " +
                 "Если true — после перезапуска зона должна быть заново активирована. " +
                 "Если false — одноразовый триггер (после первого срабатывания всегда true).")]
        public bool resetOnCycleStart = true;

        public bool IsTriggered => _triggered;

        private bool _triggered;

        private void Awake()
        {
            var col = GetComponent<Collider>();
            col.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (triggerOn != TriggerEvent.OnEnter) return;
            if (MatchesPlayerTag(other))
                _triggered = true;
        }

        private void OnTriggerExit(Collider other)
        {
            if (triggerOn != TriggerEvent.OnExit) return;
            if (MatchesPlayerTag(other))
                _triggered = true;
        }

        private bool MatchesPlayerTag(Collider other)
        {
            foreach (var tag in playerTags)
            {
                if (other.CompareTag(tag))
                    return true;
            }
            return false;
        }

        public void OnCycleExhausted()
        {
            // Не сбрасываем — триггер мог сработать ещё до exhaust.
            // Если resetOnCycleStart=true, сбросится в OnCycleStarted().
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
