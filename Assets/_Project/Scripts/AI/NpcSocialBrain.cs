// Project C: Real-Time Combat Engine — T-NPC-S01
// NpcSocialBrain: companion MonoBehaviour для NpcBrain.
// Phase 2: emotion, morale, social triggers, vocal cues, group coordination.
// Design: docs/Character/Skills/real-time-combat/npc-enemy/04_UNIFIED_BEHAVIOR_ARCHITECTURE.md

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using ProjectC.Combat;
using ProjectC.Combat.Core;

namespace ProjectC.AI
{
    public class NpcSocialBrain : MonoBehaviour
    {
        [Header("Debug")]
        [SerializeField] private bool _debugLog = false;

        [Header("Personality (T-NPC-S07)")]
        public NpcPersonalityConfig personalityConfig;

        [Header("Patrol (T-NPC-S03)")]
        public NpcIdleActivity idleActivity = NpcIdleActivity.StandStill;
        public PatrolPattern patrolPattern = PatrolPattern.Loop;
        public Vector3[] patrolWaypoints;
        public float idleAtWaypointSec = 3f;
        public float wanderRadius = 8f;
        [Range(0f, 5f)] public float patrolSpeed = 0f;

        [Header("Flee (T-NPC-S04)")]
        public bool canFlee = true;
        [Range(0f, 1f)] public float fleeHpThreshold = 0.25f;
        public float fleeAllySeekRadius = 30f;
        public float fleeLeash = 80f;
        public float fleeTimeout = 15f;

        [Header("Grudge Memory (T-NPC-S05)")]
        public bool enableGrudgeMemory = true;
        public float grudgeDurationSec = 300f;

        [Header("Alarm (T-NPC-S11 prep)")]
        public float alarmHearingRadius = 15f;
        public float allyDeathRadius = 20f;
        public bool isGuard = false;

        [Header("Threat Assessment (T-NPC-S13)")]
        [Tooltip("Радиус оценки соотношения сил (считает врагов и союзников).")]
        [Range(10f, 100f)] public float threatEvaluationRange = 30f;
        [Tooltip("Результат последней оценки угрозы (ReadOnly, для дебага).")]
        [SerializeField] private ThreatResult _lastThreatResult = ThreatResult.Confident;

        [Header("Cover (T-NPC-S14)")]
        [Tooltip("Радиус поиска укрытий.")]
        [Range(5f, 50f)] public float coverSeekRadius = 25f;
        [Tooltip("Время в укрытии перед сменой позиции (сек).")]
        [Range(2f, 15f)] public float coverSwitchInterval = 8f;
        [Tooltip("NPC ищет укрытие при HP ниже этого порога.")]
        [Range(0f, 1f)] public float coverHpThreshold = 0.5f;
        [Tooltip("Текущее укрытие (ReadOnly, для дебага).")]
        [SerializeField] private CoverPoint _currentCover;
        private float _coverEnterTime;
        private float _coverSwitchCooldown;

        [Header("Surrender (T-NPC-S16)")]
        [Tooltip("Порог HP (доля 0..1), ниже которого NPC может сдаться.")]
        [Range(0f, 1f)] public float surrenderHpThreshold = 0.10f;
        [Tooltip("Радиус проверки союзников: если союзников нет — NPC сдаётся.")]
        [Range(5f, 50f)] public float surrenderAllyRadius = 20f;
        [Tooltip("Может ли этот NPC сдаться (зависит от personality.mercy).")]
        public bool canSurrender = true;
        private bool _hasSurrendered;

        [Header("Post-Combat (T-NPC-S17)")]
        [Tooltip("Если true — NPC уходит в wounded retreat при HP<60% после боя.")]
        public bool enablePostCombat = true;
        [Tooltip("Длительность wounded-состояния после выхода из боя (сек).")]
        [Range(5f, 30f)] public float woundedDuration = 15f;
        [Tooltip("Порог HP для попытки heal (доля 0..1).")]
        [Range(0f, 1f)] public float healHpThreshold = 0.4f;
        [Tooltip("Скорость HP regen при heal (% от maxHp в секунду).")]
        [Range(0.01f, 0.2f)] public float healRegenRate = 0.05f;
        [Tooltip("Радиус поиска подкрепления после боя.")]
        [Range(20f, 80f)] public float reinforcementSeekRadius = 50f;

        private enum PostCombatState { None, Wounded, Healing, SeekingReinforcement }
        private PostCombatState _postCombat = PostCombatState.None;
        private float _postCombatTimer;
        private float _postCombatHpSnapshot;
        private bool _wasInCombat;
        private bool _postCombatAggroBlock;

        internal NpcBrain _brain;
        private NavMeshAgent _agent;
        private NpcTarget _target;
        private GrudgeTable _grudgeTable;
        private NpcEmotionState _emotion = new NpcEmotionState();
        private NpcMoraleData _morale;
        private readonly List<SocialTriggerData> _activeTriggers = new List<SocialTriggerData>();
        [System.NonSerialized] public NpcGroupController Group;
        private int _patrolIndex;
        private bool _patrolPingPongForward = true;
        private float _patrolWaitUntil;
        private Vector3 _wanderTarget;
        private float _wanderCooldown;
        private bool _isFleeing;
        private float _fleeStartTime;
        private Vector3 _fleeTarget;
        private float _nextSocialTick;

        public bool IsFleeing => _isFleeing;
        public GrudgeTable Grudge => _grudgeTable;

        private void Awake()
        {
            _brain = GetComponent<NpcBrain>();
            _agent = GetComponent<NavMeshAgent>();
            _target = GetComponent<NpcTarget>();
            _grudgeTable = new GrudgeTable { grudgeDurationSec = grudgeDurationSec };
            _morale.Initialize(personalityConfig);
            _emotion.Reset();
        }

        public void Tick(NpcBrain brain)
        {
            if (Time.unscaledTime < _nextSocialTick) return;
            _nextSocialTick = Time.unscaledTime + 0.5f;
            if (_brain == null || _agent == null) return;
            if (_brain.CurrentState != NpcBrain.BrainState.Idle &&
                _brain.CurrentState != NpcBrain.BrainState.Chase) return;

            _emotion.Tick();
            UpdateEmotion();
            EvaluateTriggers();
            if (CheckGrudgeTrigger()) return;
            if (CheckFleeConditions()) return;

            // T-NPC-S13: Threat assessment перед входом в бой.
            if (_activeTriggers.Count > 0 && EvaluateThreatBeforeCombat()) return;

            // T-NPC-S14: Cover seeking — если под огнём и HP низкий.
            if (CheckCover()) return;

            // T-NPC-S16: Surrender — если HP критический и нет союзников.
            if (CheckSurrender()) return;

            // T-NPC-S17: Post-combat — wounded retreat, heal, reinforcement.
            CheckPostCombat();

            if (ResolveActiveTriggers()) return;
            if (_brain.CurrentState == NpcBrain.BrainState.Idle) ExecuteIdleActivity();
        }

        public void RecordPlayerHit(ulong playerClientId)
        {
            if (!enableGrudgeMemory) return;
            _grudgeTable.RecordHit(playerClientId);
            if (_debugLog) Debug.Log($"[NpcSocialBrain] {name}: recorded grudge against player {playerClientId}");
        }

        private bool CheckGrudgeTrigger()
        {
            if (!enableGrudgeMemory) return false;
            if (_brain.CurrentState != NpcBrain.BrainState.Idle) return false;
            if (Unity.Netcode.NetworkManager.Singleton == null) return false;
            foreach (var client in Unity.Netcode.NetworkManager.Singleton.ConnectedClientsList)
            {
                if (client?.PlayerObject == null) continue;
                if (!_grudgeTable.HasGrudge(client.ClientId)) continue;
                var pt = client.PlayerObject.GetComponent<ProjectC.Combat.PlayerTarget>();
                if (pt == null || !pt.IsAlive()) continue;
                if (Vector3.Distance(transform.position, client.PlayerObject.transform.position) <= _brain.aggroRange)
                {
                    if (_debugLog) Debug.Log($"[NpcSocialBrain] {name}: GrudgeTrigger");
                    _brain.ForceChaseTarget(pt);
                    return true;
                }
            }
            return false;
        }

        private bool CheckFleeConditions()
        {
            if (!canFlee) return false;
            if (_target == null) return false;
            float hpPercent = _target.GetMaxHp() > 0 ? (float)_target.GetCurrentHp() / _target.GetMaxHp() : 1f;
            if (hpPercent > fleeHpThreshold) return false;
            if (_isFleeing)
            {
                if (Time.unscaledTime - _fleeStartTime > fleeTimeout || Vector3.Distance(transform.position, _fleeTarget) < 2f)
                { StopFlee(); return false; }
                if (_agent != null && _agent.isOnNavMesh && _agent.remainingDistance < 1f) _agent.SetDestination(_fleeTarget);
                return true;
            }
            StartFlee();
            return true;
        }

        private void StartFlee()
        {
            _isFleeing = true;
            _fleeStartTime = Time.unscaledTime;
            Vector3 threatPos = _brain.CurrentAggroTarget != null ? _brain.CurrentAggroTarget.GetPosition() : transform.position;
            Vector3 allyPos = FindNearestAlly();
            if (allyPos.sqrMagnitude > 0.1f && Vector3.Distance(allyPos, threatPos) > Vector3.Distance(transform.position, threatPos))
                _fleeTarget = allyPos;
            else
            {
                Vector3 fleeDir = (transform.position - threatPos).normalized;
                _fleeTarget = transform.position + fleeDir * 20f;
                if (Vector3.Distance(_brain.SpawnPoint, threatPos) > Vector3.Distance(_fleeTarget, threatPos))
                    _fleeTarget = _brain.SpawnPoint;
            }
            if (Vector3.Distance(_fleeTarget, _brain.SpawnPoint) > fleeLeash)
            {
                Vector3 dir = (_brain.SpawnPoint - threatPos).normalized;
                _fleeTarget = _brain.SpawnPoint + dir * Mathf.Min(20f, fleeLeash * 0.5f);
            }
            _brain.ForceFlee(threatPos);
        }

        private void StopFlee() { _isFleeing = false; }

        private Vector3 FindNearestAlly()
        {
            Vector3 best = Vector3.zero;
            float bestD = fleeAllySeekRadius * fleeAllySeekRadius;
            foreach (var o in FindObjectsByType<NpcSocialBrain>(FindObjectsSortMode.None))
            {
                if (o == this || o._brain == null || o._brain.CurrentState == NpcBrain.BrainState.Dead) continue;
                float d = (o.transform.position - transform.position).sqrMagnitude;
                if (d < bestD && d > 0.01f) { bestD = d; best = o.transform.position; }
            }
            return best;
        }

        private void ExecuteIdleActivity()
        {
            switch (idleActivity)
            {
                case NpcIdleActivity.Patrol: ExecutePatrol(); break;
                case NpcIdleActivity.Wander: ExecuteWander(); break;
            }
        }

        private void ExecutePatrol()
        {
            if (patrolWaypoints == null || patrolWaypoints.Length == 0) { idleActivity = NpcIdleActivity.StandStill; return; }
            if (_agent == null || !_agent.isOnNavMesh) return;
            if (Time.unscaledTime < _patrolWaitUntil) return;
            Vector3 tgt = patrolWaypoints[_patrolIndex];
            if (Vector3.Distance(transform.position, tgt) < 1.5f)
            {
                _patrolWaitUntil = Time.unscaledTime + idleAtWaypointSec;
                AdvancePatrolIndex();
                if (_agent.isOnNavMesh) { _agent.isStopped = true; _agent.ResetPath(); }
                return;
            }
            float speed = patrolSpeed > 0f ? patrolSpeed : _brain.moveSpeed;
            if (_agent.speed != speed) _agent.speed = speed;
            _agent.isStopped = false;
            _agent.SetDestination(tgt);
        }

        private void AdvancePatrolIndex()
        {
            if (patrolWaypoints == null || patrolWaypoints.Length == 0) return;
            switch (patrolPattern)
            {
                case PatrolPattern.Loop: _patrolIndex = (_patrolIndex + 1) % patrolWaypoints.Length; break;
                case PatrolPattern.PingPong:
                    if (_patrolPingPongForward) { _patrolIndex++; if (_patrolIndex >= patrolWaypoints.Length) { _patrolIndex = Mathf.Max(0, patrolWaypoints.Length - 2); _patrolPingPongForward = false; } }
                    else { _patrolIndex--; if (_patrolIndex < 0) { _patrolIndex = Mathf.Min(1, patrolWaypoints.Length - 1); _patrolPingPongForward = true; } }
                    break;
                case PatrolPattern.Random: _patrolIndex = Random.Range(0, patrolWaypoints.Length); break;
            }
        }

        private void ExecuteWander()
        {
            if (_agent == null || !_agent.isOnNavMesh) return;
            if (Time.unscaledTime < _wanderCooldown) return;
            Vector2 disc = Random.insideUnitCircle * wanderRadius;
            Vector3 cand = _brain.SpawnPoint + new Vector3(disc.x, 0, disc.y);
            if (NavMesh.SamplePosition(cand, out NavMeshHit hit, wanderRadius, NavMesh.AllAreas))
            {
                _wanderTarget = hit.position;
                float speed = patrolSpeed > 0f ? patrolSpeed : _brain.moveSpeed;
                if (_agent.speed != speed) _agent.speed = speed;
                _agent.isStopped = false;
                _agent.SetDestination(_wanderTarget);
                _wanderCooldown = Time.unscaledTime + Random.Range(3f, 8f);
            }
        }

        public bool IsDead => _brain != null && _brain.CurrentState == NpcBrain.BrainState.Dead;

        public void ApplySpawnerConfig(NpcSpawnerConfig config, Vector3[] waypointsOverride = null)
        {
            if (config == null) return;
            idleActivity = config.defaultIdleActivity;
            patrolPattern = config.patrolPattern;
            patrolWaypoints = (waypointsOverride != null && waypointsOverride.Length > 0) ? waypointsOverride : config.patrolWaypoints;
            idleAtWaypointSec = config.idleAtWaypointSec;
            wanderRadius = config.wanderRadius;
            canFlee = config.canFlee;
            fleeHpThreshold = config.fleeHpThreshold;
            fleeAllySeekRadius = config.fleeAllySeekRadius;
            alarmHearingRadius = config.alarmRadius;
            allyDeathRadius = config.allyDeathRadius;
            isGuard = config.isGuard;
            if (config.threatEvaluationRange > 0f) threatEvaluationRange = config.threatEvaluationRange;
            if (config.coverSeekRadius > 0f) coverSeekRadius = config.coverSeekRadius;
            if (config.coverHpThreshold > 0f) coverHpThreshold = config.coverHpThreshold;
            if (config.surrenderHpThreshold > 0f) surrenderHpThreshold = config.surrenderHpThreshold;
            canSurrender = config.canSurrender;
            enablePostCombat = config.enablePostCombat;
            if (config.woundedDuration > 0f) woundedDuration = config.woundedDuration;
            if (config.healHpThreshold > 0f) healHpThreshold = config.healHpThreshold;

            // T-NPC-S18: Применяем SocialRoleConfig последним (переопределяет поля выше).
            if (config.socialRole != null)
                config.socialRole.ApplyTo(this);
            enableGrudgeMemory = config.enableGrudgeMemory;
            grudgeDurationSec = config.grudgeDurationSec;
            if (_grudgeTable != null) _grudgeTable.grudgeDurationSec = grudgeDurationSec;
            if (config.personalityConfig != null) { personalityConfig = config.personalityConfig; _morale.Initialize(personalityConfig); }
        }

        private void UpdateEmotion()
        {
            if (_target == null) return;
            float hp = _target.GetMaxHp() > 0 ? (float)_target.GetCurrentHp() / _target.GetMaxHp() : 1f;
            bool inCombat = _brain.CurrentState == NpcBrain.BrainState.Chase || _brain.CurrentState == NpcBrain.BrainState.Attack;
            NpcEmotion target;
            if (hp <= 0f) target = NpcEmotion.Despair;
            else if (_isFleeing) target = NpcEmotion.Fear;
            else if (_brain.IsAggrod || inCombat) target = NpcEmotion.Anger;
            else if (_brain.CurrentAggroTarget != null) target = NpcEmotion.Alert;
            else if (_emotion.Current == NpcEmotion.Victory && _emotion.TimeInCurrentState < 5f) target = NpcEmotion.Victory;
            else if (_morale.ShouldSurrender(hp)) target = NpcEmotion.Despair;
            else if (_morale.ShouldFlee(personalityConfig) && hp < 0.5f) target = NpcEmotion.Fear;
            else target = NpcEmotion.Calm;
            _emotion.Set(target);
            if (_debugLog && target != NpcEmotion.Calm && Time.frameCount % 60 == 0)
                Debug.Log($"[NpcSocialBrain] {name}: emotion={target}, morale={_morale.current:F2}");
        }

        private void EvaluateTriggers()
        {
            _activeTriggers.Clear();
            if (CheckAllyKilled(out var kt, out ulong kid))
            { _activeTriggers.Add(new SocialTriggerData(SocialTriggerType.AllyKilled, kt, kid)); return; }
            if (CheckLeaderAggrod(out var lt))
            { _activeTriggers.Add(new SocialTriggerData(SocialTriggerType.LeaderAggrod, lt)); return; }
            if (CheckAllyInCombat(out var at))
            { _activeTriggers.Add(new SocialTriggerData(SocialTriggerType.AllyInCombat, at)); }
            if (CheckOutnumbered()) { _activeTriggers.Add(new SocialTriggerData(SocialTriggerType.Outnumbered)); _morale.OnOutnumbered(); }
            if (CheckReinforcementNearby()) { _activeTriggers.Add(new SocialTriggerData(SocialTriggerType.ReinforcementNearby)); _morale.OnReinforcementNearby(); }
        }

        private bool ResolveActiveTriggers()
        {
            if (_activeTriggers.Count == 0) return false;
            SocialTriggerType bestType = SocialTriggerType.ReinforcementNearby;
            SocialTriggerData bestTrigger = default;
            foreach (var t in _activeTriggers) if (t.Type >= bestType) { bestType = t.Type; bestTrigger = t; }
            if (bestTrigger.Target != null && (_brain.CurrentState == NpcBrain.BrainState.Idle || _brain.CurrentState == NpcBrain.BrainState.Chase))
            { _brain.ForceChaseTarget(bestTrigger.Target); return true; }
            return false;
        }

        private bool CheckAllyKilled(out IDamageTarget killerTarget, out ulong killerClientId)
        {
            killerTarget = null; killerClientId = 0;
            if (Group == null) return false;
            foreach (var m in Group.members)
            {
                if (m == this || m == null || !m.IsDead) continue;
                if (Vector3.Distance(transform.position, m.transform.position) > allyDeathRadius) continue;
                killerTarget = m._brain?.CurrentAggroTarget ?? FindNearestPlayerInRange(allyDeathRadius * 1.5f);
                if (killerTarget != null)
                {
                    float loyalty = personalityConfig != null ? personalityConfig.loyalty : 0.8f;
                    _emotion.Set(loyalty > 0.7f ? NpcEmotion.Anger : NpcEmotion.Fear);
                    _morale.OnAllyKilled(personalityConfig);
                    DispatchVocalCue(NpcVocalCue.AlertCall);
                    return true;
                }
            }
            return false;
        }

        private bool CheckLeaderAggrod(out IDamageTarget target)
        {
            target = null;
            if (Group == null || Group.leader == null || Group.leader == this) return false;
            var lb = Group.leader._brain;
            if (lb == null) return false;
            if ((lb.CurrentState == NpcBrain.BrainState.Chase || lb.CurrentState == NpcBrain.BrainState.Attack) && lb.CurrentAggroTarget != null)
            { target = lb.CurrentAggroTarget; return true; }
            return false;
        }

        private bool CheckAllyInCombat(out IDamageTarget target)
        {
            target = null;
            if (Group == null) return false;
            foreach (var m in Group.members)
            {
                if (m == this || m == null || m._brain == null) continue;
                bool inCombat = m._brain.CurrentState == NpcBrain.BrainState.Chase || m._brain.CurrentState == NpcBrain.BrainState.Attack;
                if (!inCombat || Vector3.Distance(transform.position, m.transform.position) > 15f) continue;
                target = m._brain.CurrentAggroTarget;
                if (target != null) { _emotion.Set(NpcEmotion.Alert); return true; }
            }
            return false;
        }

        private bool CheckOutnumbered()
        {
            if (Group == null) return false;
            if (Group.AliveCount <= 1 && _brain.CurrentAggroTarget != null)
            { float r = personalityConfig != null ? personalityConfig.recklessness : 0.3f; if (r < 0.7f) return true; }
            return false;
        }

        private bool CheckReinforcementNearby() { return Group != null && Group.AliveCount >= 3; }

        // T-NPC-S13: Threat assessment перед переходом в Chase.
        // Если odds не в пользу NPC — может отступить вместо атаки.
        private bool EvaluateThreatBeforeCombat()
        {
            if (_brain == null) return false;
            var ta = ThreatAssessment.Evaluate(_brain, Group, threatEvaluationRange);
            _lastThreatResult = ta.result;

            switch (ta.result)
            {
                case ThreatResult.Confident:
                    // Уверены — продолжаем обычный путь (ResolveActiveTriggers → ForceChase).
                    return false;

                case ThreatResult.Cautious:
                    // Осторожны: если есть personality, reckless > 0.7 игнорирует осторожность.
                    if (personalityConfig != null && personalityConfig.recklessness > 0.7f)
                        return false;
                    // Иначе: пропускаем этот тик (ждём подкрепления/лучших условий).
                    if (_debugLog && Time.frameCount % 60 == 0)
                        Debug.Log($"[NpcSocialBrain] {name}: Cautious — threatScore={ta.threatScore:F2}, waiting");
                    return true;

                case ThreatResult.Afraid:
                    // Боимся: Flee или CallForHelp.
                    // Если recklessness > 0.8 — всё равно лезет в бой.
                    if (personalityConfig != null && personalityConfig.recklessness > 0.8f)
                        return false;
                    // Иначе — форсируем Flee.
                    if (canFlee && !_isFleeing && _brain.CurrentAggroTarget != null)
                    {
                        if (_debugLog)
                            Debug.Log($"[NpcSocialBrain] {name}: Afraid — threatScore={ta.threatScore:F2}, fleeing");
                        StartFlee();
                        return true;
                    }
                    // Если не можем flee — Dispatch AlertCall для привлечения союзников.
                    DispatchVocalCue(NpcVocalCue.AlertCall);
                    return true;

                default:
                    return false;
            }
        }

        // T-NPC-S14: Cover seeking — ищет укрытие при низком HP или под огнём.
        private bool CheckCover()
        {
            if (_target == null || _brain == null || _agent == null) return false;
            if (!_agent.isOnNavMesh) return false;

            // Если уже в укрытии:
            if (_currentCover != null)
            {
                // Проверяем, пора ли сменить укрытие.
                if (Time.unscaledTime - _coverEnterTime > coverSwitchInterval)
                {
                    // Меняем укрытие если всё ещё под угрозой.
                    if (IsUnderThreat())
                    {
                        _coverSwitchCooldown = Time.unscaledTime + coverSwitchInterval * 0.5f;
                        SeekCover();
                    }
                    else
                    {
                        LeaveCover();
                    }
                }
                return true; // В укрытии — не делаем другие действия.
            }

            // Нужно ли искать укрытие?
            float hpPercent = _target.GetMaxHp() > 0 ? (float)_target.GetCurrentHp() / _target.GetMaxHp() : 1f;
            bool lowHp = hpPercent <= coverHpThreshold;
            bool underThreat = IsUnderThreat();

            if (lowHp && underThreat && Time.unscaledTime > _coverSwitchCooldown)
            {
                SeekCover();
                return _currentCover != null;
            }

            return false;
        }

        private bool IsUnderThreat()
        {
            if (_brain.CurrentAggroTarget != null) return true;
            // Проверяем, есть ли враг в aggroRange.
            return ThreatAssessment.HasEnemiesInRange(transform.position, _brain.aggroRange);
        }

        private void SeekCover()
        {
            if (_agent == null || !_agent.isOnNavMesh) return;

            CoverPoint best = null;
            float bestScore = float.MaxValue;

            // Поиск ближайшего укрытия (ручные CoverPoint маркеры).
            foreach (var cp in FindObjectsByType<CoverPoint>(FindObjectsSortMode.None))
            {
                if (cp == null || cp == _currentCover) continue;
                if (cp.IsOccupied) continue;

                float d = Vector3.Distance(transform.position, cp.StandPosition);
                if (d > coverSeekRadius) continue;

                // Score = distance / priority (выше приоритет = меньше score).
                float score = d / Mathf.Max(0.1f, cp.priority);
                if (score < bestScore)
                {
                    bestScore = score;
                    best = cp;
                }
            }

            if (best != null)
            {
                // Покидаем текущее укрытие.
                if (_currentCover != null)
                    _currentCover._currentOccupant = null;

                _currentCover = best;
                _currentCover._currentOccupant = this;
                _coverEnterTime = Time.unscaledTime;

                // Двигаемся к позиции укрытия.
                _agent.isStopped = false;
                _agent.SetDestination(best.StandPosition);

                if (_debugLog)
                    Debug.Log($"[NpcSocialBrain] {name}: seek cover → {best.name} (dist={Vector3.Distance(transform.position, best.StandPosition):F1}, priority={best.priority})");
            }
            else
            {
                // Нет ручных маркеров — пробуем auto-detect через raycast.
                Vector3? autoCover = AutoDetectCover();
                if (autoCover.HasValue)
                {
                    _agent.isStopped = false;
                    _agent.SetDestination(autoCover.Value);
                    _coverSwitchCooldown = Time.unscaledTime + coverSwitchInterval;
                    if (_debugLog)
                        Debug.Log($"[NpcSocialBrain] {name}: auto-cover → {autoCover.Value}");
                }
            }
        }

        /// <summary>
        /// Auto-detect cover by raycasting toward threat and finding walls.
        /// Fallback когда нет ручных CoverPoint маркеров.
        /// </summary>
        private Vector3? AutoDetectCover()
        {
            Vector3 threatPos = _brain.CurrentAggroTarget != null
                ? _brain.CurrentAggroTarget.GetPosition()
                : transform.position + transform.forward * 10f;

            Vector3 awayFromThreat = (transform.position - threatPos).normalized;

            // Веером проверяем направления от угрозы: прямо, ±30°, ±60°.
            float[] angles = { 0f, 30f, -30f, 60f, -60f };
            foreach (float angle in angles)
            {
                Vector3 dir = Quaternion.AngleAxis(angle, Vector3.up) * awayFromThreat;
                Vector3 checkPos = transform.position + dir * coverSeekRadius * 0.7f;

                // Raycast вниз для проверки земли.
                if (Physics.Raycast(checkPos + Vector3.up * 2f, Vector3.down, out RaycastHit groundHit, 10f, ~0, QueryTriggerInteraction.Ignore))
                {
                    // Raycast от угрозы к проверяемой позиции — есть ли препятствие?
                    Vector3 toThreat = threatPos - groundHit.point;
                    if (Physics.Raycast(groundHit.point, toThreat.normalized, out RaycastHit wallHit, toThreat.magnitude, ~0, QueryTriggerInteraction.Ignore))
                    {
                        // Есть стена между точкой и угрозой — хорошее укрытие!
                        if (UnityEngine.AI.NavMesh.SamplePosition(groundHit.point, out UnityEngine.AI.NavMeshHit navHit, 2f, UnityEngine.AI.NavMesh.AllAreas))
                        {
                            return navHit.position;
                        }
                    }
                }
            }
            return null;
        }

        private void LeaveCover()
        {
            if (_currentCover != null)
            {
                _currentCover._currentOccupant = null;
                _currentCover = null;
            }
            _coverEnterTime = 0f;
            if (_debugLog)
                Debug.Log($"[NpcSocialBrain] {name}: leaving cover");
        }

        /// <summary>Публичный доступ к текущему укрытию (для Group Tactics).</summary>
        public CoverPoint CurrentCover => _currentCover;

        // T-NPC-S16: Surrender — проверка условий и вход в состояние сдачи.
        private bool CheckSurrender()
        {
            if (!canSurrender) return false;
            if (_hasSurrendered) return false;
            if (_target == null || _brain == null) return false;

            float hpPercent = _target.GetMaxHp() > 0 ? (float)_target.GetCurrentHp() / _target.GetMaxHp() : 1f;
            if (hpPercent > surrenderHpThreshold) return false;

            // Проверяем, есть ли союзники рядом.
            bool hasNearbyAlly = false;
            if (Group != null)
            {
                foreach (var m in Group.members)
                {
                    if (m == this || m == null || m.IsDead) continue;
                    if (Vector3.Distance(transform.position, m.transform.position) <= surrenderAllyRadius)
                    {
                        hasNearbyAlly = true;
                        break;
                    }
                }
            }

            // Сдаёмся если HP < порог И нет союзников рядом.
            if (hasNearbyAlly) return false;

            // Учитываем personality.mercy: высокий mercy → NPC сам склонен к сдаче.
            // Низкий mercy → NPC не сдаётся (дерётся до смерти).
            float mercy = personalityConfig != null ? personalityConfig.mercy : 0.2f;
            if (mercy < 0.15f) return false; // Не сдаётся никогда.

            EnterSurrender();
            return true;
        }

        private void EnterSurrender()
        {
            _hasSurrendered = true;
            if (_brain != null)
                _brain.ForceSurrender();

            if (_debugLog)
                Debug.Log($"[NpcSocialBrain] {name}: surrendered (HP critical, no allies nearby)");
        }

        // T-NPC-S17: Post-combat behavior — Wounded/Heal/CallReinforcement.
        private void CheckPostCombat()
        {
            if (!enablePostCombat) return;
            if (_brain == null || _target == null) return;

            bool inCombat = _brain.CurrentState == NpcBrain.BrainState.Chase ||
                            _brain.CurrentState == NpcBrain.BrainState.Attack;

            // Детектируем выход из боя.
            if (_wasInCombat && !inCombat && _postCombat == PostCombatState.None)
            {
                float hpPercent = _target.GetMaxHp() > 0 ? (float)_target.GetCurrentHp() / _target.GetMaxHp() : 1f;

                if (hpPercent < healHpThreshold && HasNearbyDeadAllies())
                {
                    // AllDead nearby + есть союзники в 50м → бежим за подмогой.
                    StartPostCombat(PostCombatState.SeekingReinforcement);
                }
                else if (hpPercent < healHpThreshold)
                {
                    // HP < 40% → пытаемся лечиться.
                    StartPostCombat(PostCombatState.Healing);
                }
                else if (hpPercent < 0.6f)
                {
                    // HP < 60% → wounded retreat.
                    StartPostCombat(PostCombatState.Wounded);
                }
            }

            _wasInCombat = inCombat;

            // Tick текущего post-combat состояния.
            switch (_postCombat)
            {
                case PostCombatState.Wounded:
                    TickWounded();
                    break;
                case PostCombatState.Healing:
                    TickHealing();
                    break;
                case PostCombatState.SeekingReinforcement:
                    TickSeekingReinforcement();
                    break;
            }
        }

        private void StartPostCombat(PostCombatState state)
        {
            _postCombat = state;
            _postCombatTimer = Time.unscaledTime;
            _postCombatAggroBlock = true;
            _postCombatHpSnapshot = _target != null && _target.GetMaxHp() > 0
                ? (float)_target.GetCurrentHp() / _target.GetMaxHp()
                : 1f;

            if (_debugLog)
                Debug.Log($"[NpcSocialBrain] {name}: post-combat → {state}, hp={_postCombatHpSnapshot:F1}");
        }

        private void EndPostCombat()
        {
            _postCombat = PostCombatState.None;
            _postCombatAggroBlock = false;
            _morale.OnSuccessfulRetreat();

            if (_debugLog)
                Debug.Log($"[NpcSocialBrain] {name}: post-combat ended, resuming activity");
        }

        private void TickWounded()
        {
            // Идём к spawnPoint, не агримся.
            if (_agent != null && _agent.isOnNavMesh && !_agent.isStopped)
            {
                float distToSpawn = Vector3.Distance(transform.position, _brain.SpawnPoint);
                if (distToSpawn > 2f)
                {
                    _agent.SetDestination(_brain.SpawnPoint);
                }
                else if (_agent.remainingDistance < 1f)
                {
                    _agent.isStopped = true;
                }
            }

            if (Time.unscaledTime - _postCombatTimer > woundedDuration)
                EndPostCombat();
        }

        private void TickHealing()
        {
            // Стоим на месте, лечимся.
            if (_agent != null && _agent.isOnNavMesh)
                _agent.isStopped = true;

            if (_target != null && _target.GetMaxHp() > 0)
            {
                float hpPercent = (float)_target.GetCurrentHp() / _target.GetMaxHp();
                // Лечимся до 60% HP.
                if (hpPercent < 0.6f)
                {
                    int healAmount = Mathf.CeilToInt(_target.GetMaxHp() * healRegenRate * 0.5f); // 0.5 = SocialTick interval
                    if (healAmount > 0 && _target.GetCurrentHp() < _target.GetMaxHp())
                    {
                        // HP regen через ModifyHp.
                        // NOTE: требуется метод на NpcTarget для лечения. Пока — placeholder.
                    }
                }
                else
                {
                    EndPostCombat();
                }
            }

            if (Time.unscaledTime - _postCombatTimer > woundedDuration * 1.5f)
                EndPostCombat();
        }

        private void TickSeekingReinforcement()
        {
            // Бежим к ближайшему живому союзнику.
            Vector3 allyPos = FindNearestAlly();
            if (allyPos.sqrMagnitude > 0.1f && _agent != null && _agent.isOnNavMesh)
            {
                _agent.isStopped = false;
                _agent.SetDestination(allyPos);
            }
            else
            {
                // Нет союзников — просто ранен.
                _postCombat = PostCombatState.Wounded;
                _postCombatTimer = Time.unscaledTime;
            }

            if (Time.unscaledTime - _postCombatTimer > woundedDuration * 2f)
                EndPostCombat();
        }

        private bool HasNearbyDeadAllies()
        {
            if (Group == null) return false;
            foreach (var m in Group.members)
            {
                if (m == this || m == null || !m.IsDead) continue;
                if (Vector3.Distance(transform.position, m.transform.position) <= allyDeathRadius)
                    return true;
            }
            return false;
        }

        /// <summary>Блокирует ли post-combat состояние агрессию?</summary>
        public bool IsPostCombatAggroBlocked => _postCombatAggroBlock;

        private IDamageTarget FindNearestPlayerInRange(float range)
        {
            IDamageTarget best = null;
            float bestD = range * range;
            if (Unity.Netcode.NetworkManager.Singleton == null) return null;
            foreach (var c in Unity.Netcode.NetworkManager.Singleton.ConnectedClientsList)
            {
                if (c?.PlayerObject == null) continue;
                var pt = c.PlayerObject.GetComponent<ProjectC.Combat.PlayerTarget>();
                if (pt == null || !pt.IsAlive()) continue;
                float d = (c.PlayerObject.transform.position - transform.position).sqrMagnitude;
                if (d <= bestD) { bestD = d; best = pt; }
            }
            return best;
        }

        public void DispatchVocalCue(NpcVocalCue cue)
        {
            if (_brain == null) return;
            var anim = GetComponentInChildren<Animator>();
            if (anim != null)
            {
                string tn = cue switch
                {
                    NpcVocalCue.AlertCall => "AlertCall",
                    NpcVocalCue.DeathScream => "DeathScream",
                    NpcVocalCue.Taunt => "Taunt",
                    NpcVocalCue.FearCry => "FearCry",
                    NpcVocalCue.VictoryRoar => "VictoryRoar",
                    _ => "AlertCall",
                };
                anim.SetTrigger(tn);
            }
            if (Group != null) Group.OnVocalCue(this, cue);
            if (_debugLog) Debug.Log($"[NpcSocialBrain] {name}: VocalCue {cue}");
        }
    }
}
