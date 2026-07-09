// Project C: Real-Time Combat Engine — T-NPC-11
// SpawnRestartGate: AND/OR-композитор нескольких ISpawnRestartTrigger.
// Design: docs/Character/Skills/real-time-combat/npc-enemy/07_SPAWN_CYCLE_CONTROL.md §3.4-D

using System.Collections.Generic;
using UnityEngine;

namespace ProjectC.AI
{
    /// <summary>
    /// Композитор триггеров перезапуска.
    /// Позволяет комбинировать несколько триггеров через AND (все должны сработать)
    /// или OR (любой сработал — перезапуск).
    /// </summary>
    public class SpawnRestartGate : MonoBehaviour, ISpawnRestartTrigger
    {
        public enum GateMode
        {
            All,  // AND — все триггеры должны быть true
            Any   // OR  — хотя бы один true
        }

        [Header("Composition")]
        [Tooltip("All = AND (все должны сработать). Any = OR (любой сработал → перезапуск).")]
        public GateMode mode = GateMode.All;

        [Tooltip("Список триггеров. Перетащи сюда GameObject'ы с компонентами ISpawnRestartTrigger " +
                 "(или сами компоненты).")]
        public List<MonoBehaviour> triggers = new List<MonoBehaviour>();

        public bool IsTriggered
        {
            get
            {
                if (triggers.Count == 0) return false;

                foreach (var mb in triggers)
                {
                    if (mb == null || !mb.isActiveAndEnabled) continue;

                    bool triggered = false;
                    if (mb is ISpawnRestartTrigger t)
                        triggered = t.IsTriggered;

                    if (mode == GateMode.Any && triggered) return true;
                    if (mode == GateMode.All && !triggered) return false;
                }

                return mode == GateMode.All;
            }
        }

        public void OnCycleExhausted()
        {
            foreach (var mb in triggers)
            {
                if (mb != null && mb is ISpawnRestartTrigger t)
                    t.OnCycleExhausted();
            }
        }

        public void OnCycleStarted()
        {
            foreach (var mb in triggers)
            {
                if (mb != null && mb is ISpawnRestartTrigger t)
                    t.OnCycleStarted();
            }
        }

        public void OnRegistered(NpcSpawner spawner)
        {
            foreach (var mb in triggers)
            {
                if (mb != null && mb is ISpawnRestartTrigger t)
                    t.OnRegistered(spawner);
            }
        }
    }
}
