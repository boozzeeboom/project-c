// Project C: Real-Time Combat Engine — T-NPC-S01
// NpcSocialBrain: companion MonoBehaviour для NpcBrain.
// Добавляет patrol, flee, grudge memory поверх существующего FSM (add-only).
// Design: docs/Character/Skills/real-time-combat/npc-enemy/04_UNIFIED_BEHAVIOR_ARCHITECTURE.md §1.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using ProjectC.Combat;
using ProjectC.Combat.Core;

namespace ProjectC.AI
{
    /// <summary>
    /// T-NPC-S01: Social brain companion. Server-side only.
    /// Работает поверх NpcBrain — читает state/aggroTarget/HP, пишет через ForceChaseTarget/ForceFlee.
    /// Содержит Patrol, Flee, Grudge memory.
    /// Anti-restrictive: если NpcBrain._socialEnabled=false — этот компонент не вызывается.
    /// </summary>
    public class NpcSocialBrain : MonoBehaviour
    {
        [Header("Debug")]
        [Tooltip("Включить подробные логи.")]
        [SerializeField] private bool _debugLog = false;

        [Header("Patrol (T-NPC-S03)")]
        [Tooltip("Тип idle-активности. StandStill = ничего не делать (backward compat).")]
        public NpcIdleActivity idleActivity = NpcIdleActivity.StandStill;
        [Tooltip("Паттерн патрулирования.")]
        public PatrolPattern patrolPattern = PatrolPattern.Loop;
        [Tooltip("Точки патруля (мировые координаты). Если пусто + activity=Patrol → fallback к StandStill.")]
        public Vector3[] patrolWaypoints;
        [Tooltip("Секунд ожидания на каждой точке.")]
        public float idleAtWaypointSec = 3f;
        [Tooltip("Радиус блуждания для Wander.")]
        public float wanderRadius = 8f;
        [Tooltip("Скорость движения при патруле (0 = использовать moveSpeed из NpcBrain).")]
        [Range(0f, 5f)] public float patrolSpeed = 0f;

        [Header("Flee (T-NPC-S04)")]
        [Tooltip("Может ли NPC убегать.")]
        public bool canFlee = true;
        [Tooltip("Порог HP (доля 0..1), ниже которого начинается Flee.")]
        [Range(0f, 1f)] public float fleeHpThreshold = 0.25f;
        [Tooltip("Радиус поиска союзников для бегства.")]
        public float fleeAllySeekRadius = 30f;
        [Tooltip("Максимальная дистанция бегства от spawn (leash для Flee).")]
        public float fleeLeash = 80f;
        [Tooltip("Таймаут Flee (сек) — после истечения возврат к idle.")]
        public float fleeTimeout = 15f;

        [Header("Grudge Memory (T-NPC-S05)")]
        [Tooltip("Помнит ли NPC обидчика.")]
        public bool enableGrudgeMemory = true;
        [Tooltip("Длительность памяти (сек).")]
        public float grudgeDurationSec = 300f;

        [Header("Alarm (T-NPC-S11 prep)")]
        [Tooltip("Радиус, в котором NPC слышит Alarm.")]
        public float alarmHearingRadius = 15f;
        [Tooltip("Радиус реакции на смерть союзника.")]
        public float allyDeathRadius = 20f;
        [Tooltip("Этот NPC — стражник.")]
        public bool isGuard = false;

        // --- runtime ---
        private NpcBrain _brain;
        private NavMeshAgent _agent;
        private NpcTarget _target;
        private GrudgeTable _grudgeTable;

        // Patrol state
        private int _patrolIndex;
        private bool _patrolPingPongForward = true;
        private float _patrolWaitUntil;
        private Vector3 _wanderTarget;
        private float _wanderCooldown;

        // Flee state
        private bool _isFleeing;
        private float _fleeStartTime;
        private Vector3 _fleeTarget;

        // Social tick throttling (каждые 0.5с, не каждый AI-тик)
        private float _nextSocialTick;

        public bool IsFleeing => _isFleeing;
        public GrudgeTable Grudge => _grudgeTable;

        private void Awake()
        {
            _brain = GetComponent<NpcBrain>();
            _agent = GetComponent<NavMeshAgent>();
            _target = GetComponent<NpcTarget>();
            _grudgeTable = new GrudgeTable { grudgeDurationSec = grudgeDurationSec };
        }

        /// <summary>
        /// Вызывается из NpcBrain.Tick() каждый AI-тик.
        /// NpcSocialBrain решает: патрулировать, убегать или стоять.
        /// </summary>
        public void Tick(NpcBrain brain)
        {
            // Throttle social logic to ~2Hz (каждые 0.5с) чтобы не спамить FindObjects.
            if (Time.unscaledTime < _nextSocialTick) return;
            _nextSocialTick = Time.unscaledTime + 0.5f;

            if (_brain == null || _agent == null) return;
            // Только в Idle или когда social override активен.
            if (_brain.CurrentState != NpcBrain.BrainState.Idle &&
                _brain.CurrentState != NpcBrain.BrainState.Chase) return;

            // 1. Проверяем Grudge → если помним игрока в aggroRange → ForceChase.
            if (CheckGrudgeTrigger()) return;

            // 2. Проверяем Flee conditions.
            if (CheckFleeConditions()) return;

            // 3. Если в Idle — выполняем patrol/idle активность.
            if (_brain.CurrentState == NpcBrain.BrainState.Idle)
            {
                ExecuteIdleActivity();
            }
        }

        // ============================================================
        // Grudge Memory (T-NPC-S05)
        // ============================================================

        /// <summary>
        /// Вызывается извне (NpcTarget.OnHpChanged или NpcBrain) при получении урона от игрока.
        /// </summary>
        public void RecordPlayerHit(ulong playerClientId)
        {
            if (!enableGrudgeMemory) return;
            _grudgeTable.RecordHit(playerClientId);
            if (_debugLog)
                Debug.Log($"[NpcSocialBrain] {name}: recorded grudge against player {playerClientId}");
        }

        private bool CheckGrudgeTrigger()
        {
            if (!enableGrudgeMemory) return false;
            if (_brain.CurrentState != NpcBrain.BrainState.Idle) return false;

            // Проверяем каждого connected player: если есть grudge + в aggroRange → Chase.
            if (Unity.Netcode.NetworkManager.Singleton == null) return false;
            foreach (var client in Unity.Netcode.NetworkManager.Singleton.ConnectedClientsList)
            {
                if (client?.PlayerObject == null) continue;
                ulong clientId = client.ClientId;
                if (!_grudgeTable.HasGrudge(clientId)) continue;

                var playerTarget = client.PlayerObject.GetComponent<ProjectC.Combat.PlayerTarget>();
                if (playerTarget == null || !playerTarget.IsAlive()) continue;

                float dist = Vector3.Distance(transform.position, client.PlayerObject.transform.position);
                if (dist <= _brain.aggroRange)
                {
                    if (_debugLog)
                        Debug.Log($"[NpcSocialBrain] {name}: GrudgeTrigger → ForceChase player {clientId}");
                    _brain.ForceChaseTarget(playerTarget);
                    return true;
                }
            }
            return false;
        }

        // ============================================================
        // Flee (T-NPC-S04)
        // ============================================================

        private bool CheckFleeConditions()
        {
            if (!canFlee) return false;
            if (_target == null) return false;

            // HP ниже порога?
            float hpPercent = _target.GetMaxHp() > 0 ? (float)_target.GetCurrentHp() / _target.GetMaxHp() : 1f;
            if (hpPercent > fleeHpThreshold) return false;

            // Уже в процессе Flee?
            if (_isFleeing)
            {
                // Таймаут или достигли цели?
                if (Time.unscaledTime - _fleeStartTime > fleeTimeout ||
                    Vector3.Distance(transform.position, _fleeTarget) < 2f)
                {
                    StopFlee();
                    return false;
                }
                // Продолжаем бегство — обновляем destination если агент застрял.
                if (_agent != null && _agent.isOnNavMesh && _agent.remainingDistance < 1f)
                {
                    _agent.SetDestination(_fleeTarget);
                }
                return true;
            }

            // Начинаем Flee.
            StartFlee();
            return true;
        }

        private void StartFlee()
        {
            _isFleeing = true;
            _fleeStartTime = Time.unscaledTime;

            // Цель бегства: от текущего аггрора к spawnPoint, или к союзникам.
            Vector3 threatPos = _brain.CurrentAggroTarget != null
                ? _brain.CurrentAggroTarget.GetPosition()
                : transform.position;

            // Ищем союзников поблизости.
            Vector3 allyPos = FindNearestAlly();
            Vector3 fleeDir = (transform.position - threatPos).normalized;

            if (allyPos.sqrMagnitude > 0.1f &&
                Vector3.Distance(allyPos, threatPos) > Vector3.Distance(transform.position, threatPos))
            {
                _fleeTarget = allyPos;
            }
            else
            {
                _fleeTarget = transform.position + fleeDir * 20f;
                // Предпочитаем spawnPoint если он дальше от угрозы.
                float spawnDist = Vector3.Distance(_brain.SpawnPoint, threatPos);
                float fleeDist = Vector3.Distance(_fleeTarget, threatPos);
                if (spawnDist > fleeDist) _fleeTarget = _brain.SpawnPoint;
            }

            // Clamp to flee leash.
            if (Vector3.Distance(_fleeTarget, _brain.SpawnPoint) > fleeLeash)
            {
                Vector3 dirToSpawn = (_brain.SpawnPoint - threatPos).normalized;
                _fleeTarget = _brain.SpawnPoint + dirToSpawn * Mathf.Min(20f, fleeLeash * 0.5f);
            }

            _brain.ForceFlee(threatPos);

            if (_debugLog)
                Debug.Log($"[NpcSocialBrain] {name}: Flee started → {_fleeTarget} (from {threatPos})");
        }

        private void StopFlee()
        {
            _isFleeing = false;
            if (_debugLog)
                Debug.Log($"[NpcSocialBrain] {name}: Flee stopped, returning to idle");
        }

        private Vector3 FindNearestAlly()
        {
            Vector3 best = Vector3.zero;
            float bestDistSq = fleeAllySeekRadius * fleeAllySeekRadius;
            var others = FindObjectsByType<NpcSocialBrain>(FindObjectsSortMode.None);
            foreach (var other in others)
            {
                if (other == this) continue;
                if (other._brain == null || other._brain.CurrentState == NpcBrain.BrainState.Dead) continue;
                float d = (other.transform.position - transform.position).sqrMagnitude;
                if (d < bestDistSq && d > 0.01f)
                {
                    bestDistSq = d;
                    best = other.transform.position;
                }
            }
            return best;
        }

        // ============================================================
        // Idle Activity — Patrol / Wander / LookAround / StandStill (T-NPC-S03)
        // ============================================================

        private void ExecuteIdleActivity()
        {
            switch (idleActivity)
            {
                case NpcIdleActivity.Patrol:
                    ExecutePatrol();
                    break;
                case NpcIdleActivity.Wander:
                    ExecuteWander();
                    break;
                case NpcIdleActivity.LookAround:
                    // Phase 2: head-tracking анимация. Пока — StandStill.
                    break;
                case NpcIdleActivity.StandStill:
                default:
                    // Ничего не делаем — старый FSM сам стоит.
                    break;
            }
        }

        private void ExecutePatrol()
        {
            if (patrolWaypoints == null || patrolWaypoints.Length == 0)
            {
                // Anti-restrictive: без waypoints → StandStill.
                idleActivity = NpcIdleActivity.StandStill;
                return;
            }

            if (_agent == null || !_agent.isOnNavMesh) return;

            // Ждём на точке?
            if (Time.unscaledTime < _patrolWaitUntil) return;

            Vector3 currentTarget = patrolWaypoints[_patrolIndex];
            float dist = Vector3.Distance(transform.position, currentTarget);

            if (dist < 1.5f)
            {
                // Достигли точки — ждём.
                _patrolWaitUntil = Time.unscaledTime + idleAtWaypointSec;
                AdvancePatrolIndex();
                if (_agent.isOnNavMesh)
                {
                    _agent.isStopped = true;
                    _agent.ResetPath();
                }
                return;
            }

            // Двигаемся к следующей точке.
            float speed = patrolSpeed > 0f ? patrolSpeed : _brain.moveSpeed;
            if (_agent.speed != speed) _agent.speed = speed;
            _agent.isStopped = false;
            _agent.SetDestination(currentTarget);
        }

        private void AdvancePatrolIndex()
        {
            if (patrolWaypoints == null || patrolWaypoints.Length == 0) return;

            switch (patrolPattern)
            {
                case PatrolPattern.Loop:
                    _patrolIndex = (_patrolIndex + 1) % patrolWaypoints.Length;
                    break;
                case PatrolPattern.PingPong:
                    if (_patrolPingPongForward)
                    {
                        _patrolIndex++;
                        if (_patrolIndex >= patrolWaypoints.Length)
                        {
                            _patrolIndex = Mathf.Max(0, patrolWaypoints.Length - 2);
                            _patrolPingPongForward = false;
                        }
                    }
                    else
                    {
                        _patrolIndex--;
                        if (_patrolIndex < 0)
                        {
                            _patrolIndex = Mathf.Min(1, patrolWaypoints.Length - 1);
                            _patrolPingPongForward = true;
                        }
                    }
                    break;
                case PatrolPattern.Random:
                    _patrolIndex = Random.Range(0, patrolWaypoints.Length);
                    break;
            }
        }

        private void ExecuteWander()
        {
            if (_agent == null || !_agent.isOnNavMesh) return;

            if (Time.unscaledTime < _wanderCooldown) return;

            // Выбираем случайную точку в wanderRadius от spawnPoint.
            Vector2 disc = Random.insideUnitCircle * wanderRadius;
            Vector3 candidate = _brain.SpawnPoint + new Vector3(disc.x, 0, disc.y);

            // Проверяем, что точка на NavMesh.
            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, wanderRadius, NavMesh.AllAreas))
            {
                _wanderTarget = hit.position;
                float speed = patrolSpeed > 0f ? patrolSpeed : _brain.moveSpeed;
                if (_agent.speed != speed) _agent.speed = speed;
                _agent.isStopped = false;
                _agent.SetDestination(_wanderTarget);
                _wanderCooldown = Time.unscaledTime + Random.Range(3f, 8f);
            }
        }

        // ============================================================
        // Public API для NpcGroupController (Phase 2 prep)
        // ============================================================

        public bool IsDead => _brain != null && _brain.CurrentState == NpcBrain.BrainState.Dead;

        /// <summary>Применить конфигурацию из NpcSpawnerConfig (вызывается NpcSpawner после спавна).</summary>
        public void ApplySpawnerConfig(NpcSpawnerConfig config)
        {
            if (config == null) return;
            idleActivity = config.defaultIdleActivity;
            patrolPattern = config.patrolPattern;
            patrolWaypoints = config.patrolWaypoints;
            idleAtWaypointSec = config.idleAtWaypointSec;
            wanderRadius = config.wanderRadius;
            canFlee = config.canFlee;
            fleeHpThreshold = config.fleeHpThreshold;
            fleeAllySeekRadius = config.fleeAllySeekRadius;
            alarmHearingRadius = config.alarmRadius;
            allyDeathRadius = config.allyDeathRadius;
            isGuard = config.isGuard;
            enableGrudgeMemory = config.enableGrudgeMemory;
            grudgeDurationSec = config.grudgeDurationSec;
            if (_grudgeTable != null) _grudgeTable.grudgeDurationSec = grudgeDurationSec;
        }
    }
}
