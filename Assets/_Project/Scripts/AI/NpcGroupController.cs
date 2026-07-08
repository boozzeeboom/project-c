// Project C: Real-Time Combat Engine — T-NPC-S10
// NpcGroupController: групповой координатор NPC.
// Один на группу. Server-side NetworkBehaviour.
// Design: docs/Character/Skills/real-time-combat/npc-enemy/04_UNIFIED_BEHAVIOR_ARCHITECTURE.md §7.

using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using ProjectC.Combat.Core;

namespace ProjectC.AI
{
    /// <summary>
    /// T-NPC-S10: Групповой контроллер NPC.
    /// Создаётся NpcSpawner при групповом спавне.
    /// Координирует Alarm, AllyDeath, Retreat, Vocal Cues.
    /// </summary>
    public class NpcGroupController : NetworkBehaviour
    {
        [Header("Debug")]
        [Tooltip("Включить подробные логи.")]
        [SerializeField] private bool _debugLog = false;

        /// <summary>Все члены группы (живые + мёртвые).</summary>
        public List<NpcSocialBrain> members = new List<NpcSocialBrain>();

        /// <summary>Лидер группы (первый заспавненный или назначенный).</summary>
        public NpcSocialBrain leader;

        /// <summary>Количество живых членов группы.</summary>
        public int AliveCount
        {
            get
            {
                int count = 0;
                foreach (var m in members)
                    if (m != null && !m.IsDead) count++;
                return count;
            }
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (!IsServer) { enabled = false; return; }
        }

        public override void OnNetworkDespawn()
        {
            // Отвязываем всех членов.
            foreach (var m in members)
            {
                if (m != null) m.Group = null;
            }
            members.Clear();
            leader = null;
            base.OnNetworkDespawn();
        }

        /// <summary>Добавить NPC в группу и установить обратную ссылку.</summary>
        public void AddMember(NpcSocialBrain brain)
        {
            if (brain == null || members.Contains(brain)) return;
            members.Add(brain);
            brain.Group = this;

            // Назначаем лидера, если ещё нет.
            if (leader == null)
            {
                leader = brain;
                if (_debugLog)
                    Debug.Log($"[NpcGroupController] {brain.name} назначен лидером группы (size={members.Count})");
            }
        }

        /// <summary>Удалить NPC из группы.</summary>
        public void RemoveMember(NpcSocialBrain brain)
        {
            if (brain == null) return;
            members.Remove(brain);
            brain.Group = null;

            if (leader == brain)
                ElectNewLeader();
        }

        /// <summary>
        /// Вызывается NpcSocialBrain'ом при AllyInCombat.
        /// Оповещает всех членов группы в радиусе.
        /// </summary>
        public void BroadcastAlarm(NpcSocialBrain source, IDamageTarget target, float radius)
        {
            if (!IsServer) return;
            foreach (var member in members)
            {
                if (member == source || member == null || member.IsDead) continue;
                float dist = Vector3.Distance(source.transform.position, member.transform.position);
                if (dist > radius) continue;

                // Если член в Idle — подключаем к бою через ForceChase.
                if (member._brain != null &&
                    member._brain.CurrentState == NpcBrain.BrainState.Idle &&
                    target != null)
                {
                    if (_debugLog)
                        Debug.Log($"[NpcGroupController] BroadcastAlarm: {member.name} joins combat (source={source.name}, dist={dist:F1})");
                    member._brain.ForceChaseTarget(target);
                }
            }
        }

        /// <summary>
        /// Вызывается при смерти члена группы.
        /// Триггерит AllyKilled у всех в allyDeathRadius.
        /// </summary>
        public void OnMemberKilled(NpcSocialBrain victim)
        {
            if (!IsServer) return;
            if (victim == null) return;

            if (_debugLog)
                Debug.Log($"[NpcGroupController] Member killed: {victim.name}, alive={AliveCount}");

            // Если умер лидер — выбираем нового.
            if (victim == leader)
                ElectNewLeader();

            // Эффект на группу: -0.3 morale если умер лидер.
            if (victim == leader)
            {
                foreach (var member in members)
                {
                    if (member == victim || member == null || member.IsDead) continue;
                    // NpcMoraleData — struct, нужен доступ через рефлексию или публичный метод.
                    // Пока — опосредованно через NpcSocialBrain (будет в Phase 3).
                }
            }

            // DeathScream от умершего (если ещё не вызван).
            victim.DispatchVocalCue(NpcVocalCue.DeathScream);
        }

        /// <summary>
        /// Лидер приказывает отступить.
        /// Все живые члены группы переходят в Flee.
        /// </summary>
        public void OrderRetreat(Vector3 fallbackPoint)
        {
            if (!IsServer) return;
            if (_debugLog)
                Debug.Log($"[NpcGroupController] OrderRetreat to {fallbackPoint}, alive={AliveCount}");

            foreach (var member in members)
            {
                if (member == null || member.IsDead) continue;
                if (member._brain != null)
                    member._brain.ForceFlee(fallbackPoint);
            }
        }

        /// <summary>
        /// Выбрать нового лидера (первый живой в списке).
        /// </summary>
        public void ElectNewLeader()
        {
            leader = null;
            foreach (var member in members)
            {
                if (member != null && !member.IsDead)
                {
                    leader = member;
                    if (_debugLog)
                        Debug.Log($"[NpcGroupController] New leader elected: {leader.name}");
                    return;
                }
            }
            if (_debugLog)
                Debug.Log($"[NpcGroupController] No alive members left to lead.");
        }

        /// <summary>
        /// T-NPC-S11: Получен голосовой сигнал от члена группы.
        /// Распространяет gameplay-эффекты на остальных.
        /// </summary>
        public void OnVocalCue(NpcSocialBrain source, NpcVocalCue cue)
        {
            if (!IsServer) return;

            foreach (var member in members)
            {
                if (member == source || member == null || member.IsDead) continue;

                float dist = Vector3.Distance(source.transform.position, member.transform.position);
                float hearingRadius = cue switch
                {
                    NpcVocalCue.DeathScream => 20f,
                    NpcVocalCue.AlertCall => 15f,
                    NpcVocalCue.VictoryRoar => 30f,
                    NpcVocalCue.FearCry => 20f,
                    NpcVocalCue.Taunt => 15f,
                    _ => 15f,
                };

                if (dist > hearingRadius) continue;

                // Gameplay-эффекты согласно §6.
                switch (cue)
                {
                    case NpcVocalCue.AlertCall:
                        // Привлекает NPC в 15м: AllyInCombat.
                        if (member._brain != null && source._brain?.CurrentAggroTarget != null)
                        {
                            if (member._brain.CurrentState == NpcBrain.BrainState.Idle)
                                member._brain.ForceChaseTarget(source._brain.CurrentAggroTarget);
                        }
                        break;

                    case NpcVocalCue.DeathScream:
                        // Триггерит AllyKilled (проверяется в EvaluateTriggers).
                        break;

                    case NpcVocalCue.FearCry:
                        // Понижает morale союзников.
                        // (доступ через публичный API будет в Phase 3)
                        break;

                    case NpcVocalCue.VictoryRoar:
                        // +0.1 morale союзникам.
                        break;

                    case NpcVocalCue.Taunt:
                        // Phase 2+: дебафф цели.
                        break;
                }
            }
        }
    }
}
