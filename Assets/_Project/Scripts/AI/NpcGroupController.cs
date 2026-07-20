// Project C: Real-Time Combat Engine — T-NPC-S10
// NpcGroupController: групповой координатор NPC.
// Один на группу. Server-side NetworkBehaviour.
// Design: docs/Character/Skills/real-time-combat/npc-enemy/04_UNIFIED_BEHAVIOR_ARCHITECTURE.md §7.

using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using ProjectC.Combat.Core;
using ProjectC.Factions;

namespace ProjectC.AI
{
    /// <summary>
    /// T-NPC-S10: Групповой контроллер NPC.
    /// Создаётся NpcSpawner при групповом спавне.
    /// Координирует Alarm, AllyDeath, Retreat, Vocal Cues.
    /// </summary>
    /// <summary>
    /// Тип групповой формации.
    /// </summary>
    public enum FormationType
    {
        /// <summary>Без формации — каждый действует сам.</summary>
        None,
        /// <summary>Линия фронта.</summary>
        Line,
        /// <summary>Окружение цели.</summary>
        Circle,
        /// <summary>Фланговый обход: 1-2 обходят, остальные фронт.</summary>
        Flank,
    }

    public class NpcGroupController : NetworkBehaviour
    {
        [Header("Debug")]
        [Tooltip("Включить подробные логи.")]
        [SerializeField] private bool _debugLog = false;

        [Header("Group Tactics (T-NPC-S15)")]
        [Tooltip("Тип формации по умолчанию.")]
        public FormationType formationType = FormationType.Line;
        [Tooltip("Радиус, в котором союзники считаются «в формации».")]
        [Range(3f, 30f)] public float formationRadius = 10f;
        [Tooltip("Расстояние между NPC в линии.")]
        [Range(1f, 5f)] public float lineSpacing = 3f;
        [Tooltip("Максимальное количество фланкеров.")]
        [Range(1, 4)] public int maxFlankers = 2;

        /// <summary>Все члены группы (живые + мёртвые).</summary>
        public List<NpcSocialBrain> members = new List<NpcSocialBrain>();

        /// <summary>Лидер группы (первый заспавненный или назначенный).</summary>
        public NpcSocialBrain leader;

        private float _nextTacticsTick;
        private const float TACTICS_TICK_INTERVAL = 1.0f;
        private System.Random _rng = new System.Random();

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

            // T-NPC-S19: проверяем совместимость фракций. В группе — только Allied.
            if (members.Count > 0 && members[0] != null && members[0].faction != null && brain.faction != null)
            {
                if (!members[0].faction.IsAlliedWith(brain.faction.factionId))
                {
                    if (_debugLog)
                        Debug.Log($"[NpcGroupController] {brain.name} rejected: faction mismatch ({brain.faction?.CombatKey} vs {members[0].faction?.CombatKey})");
                    return;
                }
            }

            members.Add(brain);
            brain.Group = this;

            // Назначаем лидера (предпочитаем isLeader=true).
            if (leader == null || (brain.isGuard && leader != null && !leader.isGuard))
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

            // Если умер лидер — выбираем нового и наносим удар по морали группы.
            bool wasLeader = (victim == leader);
            if (wasLeader)
                ElectNewLeader();

            if (wasLeader)
            {
                foreach (var member in members)
                {
                    if (member == victim || member == null || member.IsDead) continue;
                    member.OnLeaderDied();
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
                        if (member._brain != null && source._brain?.CurrentAggroTarget != null)
                        {
                            if (member._brain.CurrentState == NpcBrain.BrainState.Idle)
                                member._brain.ForceChaseTarget(source._brain.CurrentAggroTarget);
                        }
                        break;
                    case NpcVocalCue.DeathScream:
                        break;
                    case NpcVocalCue.FearCry:
                        member.HearFearCry();
                        break;
                    case NpcVocalCue.VictoryRoar:
                        // Союзники воодушевляются, враги деморализуются.
                        if (member.faction != null && source.faction != null && member.faction.IsAlliedWith(source.faction.factionId))
                            member.HearVictoryRoar();
                        else if (member.faction != null && source.faction != null && member.faction.IsHostileTowards(source.faction.factionId))
                            member.HearEnemyVictoryRoar();
                        break;
                    case NpcVocalCue.Taunt:
                        // Taunt: дебафф цели (TODO: применить дебафф через combat system).
                        break;
                }

            }
        }

        // ============================================================
        // T-NPC-S15: Group Tactics — formations, flanking, focus fire
        // ============================================================

        private void Update()
        {
            if (!IsServer) return;
            if (Time.unscaledTime < _nextTacticsTick) return;
            _nextTacticsTick = Time.unscaledTime + TACTICS_TICK_INTERVAL;
            TacticsTick();
        }

        /// <summary>
        /// Тактический тик: проверяет условия для формаций и флангов.
        /// Вызывается раз в TACTICS_TICK_INTERVAL (~1 сек).
        /// </summary>
        private void TacticsTick()
        {
            if (formationType == FormationType.None) return;
            if (AliveCount < 3) return; // Нужно минимум 3 для формаций.

            // Ищем общую цель группы (цель лидера или любой общий aggroTarget).
            IDamageTarget groupTarget = GetGroupTarget();
            if (groupTarget == null || !groupTarget.IsAlive()) return;

            switch (formationType)
            {
                case FormationType.Line:
                    ApplyFormationLine(groupTarget);
                    break;
                case FormationType.Flank:
                    ApplyFormationFlank(groupTarget);
                    break;
                case FormationType.Circle:
                    ApplyFormationCircle(groupTarget);
                    break;
            }

            // FocusFire: если лидер атакует — вся группа фокусит ту же цель.
            if (leader != null && leader._brain != null)
            {
                var leaderTarget = leader._brain.CurrentAggroTarget;
                if (leaderTarget != null)
                    FocusFire(leaderTarget);
            }
        }

        private IDamageTarget GetGroupTarget()
        {
            if (leader != null && leader._brain?.CurrentAggroTarget != null)
                return leader._brain.CurrentAggroTarget;
            foreach (var m in members)
            {
                if (m == null || m.IsDead || m._brain == null) continue;
                if (m._brain.CurrentAggroTarget != null)
                    return m._brain.CurrentAggroTarget;
            }
            return null;
        }

        /// <summary>
        /// FormationLine: все живые NPC выстраиваются в линию фронта перпендикулярно цели.
        /// </summary>
        private void ApplyFormationLine(IDamageTarget target)
        {
            Vector3 targetPos = target.GetPosition();
            Vector3 center = GetGroupCenter();
            Vector3 toTarget = (targetPos - center).normalized;
            Vector3 lineDir = Vector3.Cross(Vector3.up, toTarget).normalized;

            int index = 0;
            foreach (var m in members)
            {
                if (m == null || m.IsDead || m._brain == null) continue;
                // Не двигаем тех, кто уже в бою/укрытии.
                if (m._brain.CurrentState == NpcBrain.BrainState.Attack) continue;
                if (m.CurrentCover != null) continue;

                float offset = (index - (AliveCount - 1) * 0.5f) * lineSpacing;
                Vector3 formationPos = center + lineDir * offset;

                UnityEngine.AI.NavMeshAgent agent = m.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (agent != null && agent.isOnNavMesh)
                {
                    agent.isStopped = false;
                    agent.SetDestination(formationPos);
                }
                index++;
            }
        }

        /// <summary>
        /// FormationFlank: 1-2 NPC обходят с флангов, остальные держат фронт.
        /// </summary>
        private void ApplyFormationFlank(IDamageTarget target)
        {
            Vector3 targetPos = target.GetPosition();
            Vector3 center = GetGroupCenter();
            Vector3 toTarget = (targetPos - center).normalized;
            Vector3 rightDir = Vector3.Cross(Vector3.up, toTarget).normalized;

            int aliveProcessed = 0;
            int flankerCount = 0;
            foreach (var m in members)
            {
                if (m == null || m.IsDead || m._brain == null) continue;
                if (m._brain.CurrentState == NpcBrain.BrainState.Attack) continue;
                if (m.CurrentCover != null) continue;

                UnityEngine.AI.NavMeshAgent agent = m.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (agent == null || !agent.isOnNavMesh) continue;

                // Первые maxFlankers — фланкеры.
                if (flankerCount < maxFlankers)
                {
                    float side = (flankerCount % 2 == 0) ? 1f : -1f;
                    float flankDist = formationRadius * 0.7f;
                    Vector3 flankPos = targetPos + rightDir * side * flankDist + toTarget * (-formationRadius * 0.3f);
                    agent.isStopped = false;
                    agent.SetDestination(flankPos);
                    flankerCount++;
                }
                else
                {
                    // Остальные — фронт.
                    float offset = (aliveProcessed - flankerCount - (AliveCount - flankerCount - 1) * 0.5f) * lineSpacing;
                    Vector3 formationPos = center + rightDir * offset;
                    agent.isStopped = false;
                    agent.SetDestination(formationPos);
                }
                aliveProcessed++;
            }

            if (_debugLog && flankerCount > 0)
                Debug.Log($"[NpcGroupController] FormationFlank: {flankerCount} flankers, target={target.GetPosition()}");
        }

        /// <summary>
        /// FormationCircle: NPC окружают цель.
        /// </summary>
        private void ApplyFormationCircle(IDamageTarget target)
        {
            Vector3 targetPos = target.GetPosition();
            float circleRadius = formationRadius * 0.6f;
            float angleStep = 360f / Mathf.Max(1, AliveCount);

            int index = 0;
            foreach (var m in members)
            {
                if (m == null || m.IsDead || m._brain == null) continue;
                if (m._brain.CurrentState == NpcBrain.BrainState.Attack) continue;

                UnityEngine.AI.NavMeshAgent agent = m.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (agent == null || !agent.isOnNavMesh) continue;

                float angle = index * angleStep * Mathf.Deg2Rad;
                Vector3 circlePos = targetPos + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * circleRadius;
                agent.isStopped = false;
                agent.SetDestination(circlePos);
                index++;
            }
        }

        /// <summary>
        /// FocusFire: вся группа перенаправляется на одну цель.
        /// </summary>
        public void FocusFire(IDamageTarget target)
        {
            if (target == null || !IsServer) return;
            foreach (var m in members)
            {
                if (m == null || m.IsDead || m._brain == null) continue;
                if (m._brain.CurrentState != NpcBrain.BrainState.Idle &&
                    m._brain.CurrentState != NpcBrain.BrainState.Chase) continue;
                if (m._brain.CurrentAggroTarget != target)
                    m._brain.ForceChaseTarget(target);
            }
        }

        /// <summary>
        /// Вычислить центр группы (среднее позиций живых членов).
        /// </summary>
        private Vector3 GetGroupCenter()
        {
            Vector3 sum = Vector3.zero;
            int count = 0;
            foreach (var m in members)
            {
                if (m != null && !m.IsDead) { sum += m.transform.position; count++; }
            }
            return count > 0 ? sum / count : transform.position;
        }
    }
}
