// Project C: Real-Time Combat Engine — T-NPC-S01
// NpcSocialBrain: companion MonoBehaviour для NpcBrain.
// Phase 2: emotion, morale, social triggers, vocal cues, group coordination.
// Phase 3: threat, cover, surrender, post-combat, social roles.
// Phase 4: faction, vengeance, full idle activities.
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

        [Header("Faction (T-NPC-S19)")]
        public NpcFaction faction;

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
        [Range(10f, 100f)] public float threatEvaluationRange = 30f;
        [SerializeField] private ThreatResult _lastThreatResult = ThreatResult.Confident;

        [Header("Cover (T-NPC-S14)")]
        [Range(5f, 50f)] public float coverSeekRadius = 25f;
        [Range(2f, 15f)] public float coverSwitchInterval = 8f;
        [Range(0f, 1f)] public float coverHpThreshold = 0.5f;
        [SerializeField] private CoverPoint _currentCover;
        private float _coverEnterTime;
        private float _coverSwitchCooldown;

        [Header("Surrender (T-NPC-S16)")]
        [Range(0f, 1f)] public float surrenderHpThreshold = 0.10f;
        [Range(5f, 50f)] public float surrenderAllyRadius = 20f;
        public bool canSurrender = true;
        private bool _hasSurrendered;

        [Header("Vengeance (T-NPC-S20)")]
        public bool enableVengeanceMemory = true;

        [Header("Post-Combat (T-NPC-S17)")]
        public bool enablePostCombat = true;
        [Range(5f, 30f)] public float woundedDuration = 15f;
        [Range(0f, 1f)] public float healHpThreshold = 0.4f;
        [Range(0.01f, 0.2f)] public float healRegenRate = 0.05f;
        [Range(20f, 80f)] public float reinforcementSeekRadius = 50f;

        // T-NPC-S19: короткая пауза после боя перед возобновлением idle.
        private float _postCombatGuardUntil;

        private enum PostCombatState { None, Wounded, Healing, SeekingReinforcement }
        private PostCombatState _postCombat = PostCombatState.None;
        private float _postCombatTimer;
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

        // S21: Idle activity fields
        private float _socializeCooldown;
        private NpcSocialBrain _socializePartner;
        private float _workAnimTimer;
        private SitPoint _sitPoint;
        private float _sitSearchCooldown;
        private float _sleepWakeTime;
        private bool _sleepInitialized;

        public bool IsFleeing => _isFleeing;
        public GrudgeTable Grudge => _grudgeTable;
        public bool IsDead => _brain != null && _brain.CurrentState == NpcBrain.BrainState.Dead;
        public bool IsPostCombatAggroBlocked => _postCombatAggroBlock;
        public CoverPoint CurrentCover => _currentCover;

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

            if (CheckVengeanceTrigger()) return;
            if (CheckGrudgeTrigger()) return;
            if (CheckFleeConditions()) return;

            // T-NPC-S19: проверяем вражеских NPC других фракций.
            if (CheckHostileNpcNearby()) return;

            if (_activeTriggers.Count > 0 && EvaluateThreatBeforeCombat()) return;
            if (CheckCover()) return;
            if (CheckSurrender()) return;

            CheckPostCombat();

            // T-NPC-S19: при выходе из боя ставим guard-паузу на 4-6 сек.
            if (_wasInCombat && _brain.CurrentState == NpcBrain.BrainState.Idle
                && _postCombat == PostCombatState.None && Time.unscaledTime > _postCombatGuardUntil)
            {
                _postCombatGuardUntil = Time.unscaledTime + Random.Range(4f, 6f);
                _wasInCombat = false;
            }

            if (ResolveActiveTriggers()) return;
            if (_brain.CurrentState == NpcBrain.BrainState.Idle) ExecuteIdleActivity();
        }

        public void RecordPlayerHit(ulong playerClientId)
        {
            if (!enableGrudgeMemory) return;
            _grudgeTable.RecordHit(playerClientId);
            if (_debugLog) Debug.Log($"[NpcSocialBrain] {name}: recorded grudge against player {playerClientId}");
        }

        // ==================== S19: Hostile NPC detection ====================
        private bool CheckHostileNpcNearby()
        {
            if (faction == null || _brain == null) return false;
            // T-NPC-S19 fix: проверяем врагов только из Idle. Если уже есть цель — не переключаемся.
            if (_brain.CurrentState != NpcBrain.BrainState.Idle) return false;
            if (_brain.CurrentAggroTarget != null) return false;

            // Ищем NPC враждебных фракций в aggroRange.
            foreach (var o in FindObjectsByType<NpcSocialBrain>(FindObjectsSortMode.None))
            {
                if (o == this || o == null || o.IsDead || o._brain == null || o.faction == null) continue;
                if (!faction.IsHostile(o.faction)) continue;
                float d = Vector3.Distance(transform.position, o.transform.position);
                if (d <= _brain.aggroRange)
                {
                    // Нашли врага! Агримся через его NpcTarget.
                    var enemyTarget = o.GetComponent<NpcTarget>();
                    if (enemyTarget != null && enemyTarget.IsAlive())
                    {
                        if (_debugLog)
                            Debug.Log($"[NpcSocialBrain] {name} (faction={faction.factionId}) aggro on {o.name} (faction={o.faction.factionId}), dist={d:F1}");
                        _brain.ForceChaseTarget(enemyTarget);
                        return true;
                    }
                }
            }
            return false;
        }

        // ==================== S20: Vengeance ====================
        private bool CheckVengeanceTrigger()
        {
            if (!enableVengeanceMemory) return false;
            if (faction == null || string.IsNullOrEmpty(faction.factionId)) return false;
            if (VengeanceMemory.Instance == null) return false;
            if (_brain.CurrentState != NpcBrain.BrainState.Idle) return false;

            if (Unity.Netcode.NetworkManager.Singleton == null) return false;
            foreach (var client in Unity.Netcode.NetworkManager.Singleton.ConnectedClientsList)
            {
                if (client?.PlayerObject == null) continue;
                if (!VengeanceMemory.Instance.HasVengeance(faction.factionId, client.ClientId)) continue;
                var pt = client.PlayerObject.GetComponent<ProjectC.Combat.PlayerTarget>();
                if (pt == null || !pt.IsAlive()) continue;
                float dist = Vector3.Distance(transform.position, client.PlayerObject.transform.position);
                if (dist <= VengeanceMemory.Instance.vengeanceTriggerRadius)
                {
                    if (_debugLog) Debug.Log($"[NpcSocialBrain] {name}: VengeanceTrigger player {client.ClientId} faction={faction.factionId}");
                    _brain.ForceChaseTarget(pt);
                    return true;
                }
            }
            return false;
        }

        // ==================== S05: Grudge ====================
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

        // ==================== S04: Flee ====================
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
                if (faction != null && o.faction != null && !faction.IsAllied(o.faction)) continue;
                float d = (o.transform.position - transform.position).sqrMagnitude;
                if (d < bestD && d > 0.01f) { bestD = d; best = o.transform.position; }
            }
            return best;
        }

        // ==================== S21: All Idle Activities ====================
        private void ExecuteIdleActivity()
        {
            // T-NPC-S19: если недавно вышел из боя — стоим на страже, не патрулируем.
            if (Time.unscaledTime < _postCombatGuardUntil)
            {
                if (_agent != null && _agent.isOnNavMesh) _agent.isStopped = true;
                return;
            }

            switch (idleActivity)
            {
                case NpcIdleActivity.StandStill: ExecuteStandStill(); break;
                case NpcIdleActivity.Patrol: ExecutePatrol(); break;
                case NpcIdleActivity.Wander: ExecuteWander(); break;
                case NpcIdleActivity.LookAround: ExecuteLookAround(); break;
                case NpcIdleActivity.Socialize: ExecuteSocialize(); break;
                case NpcIdleActivity.Work: ExecuteWork(); break;
                case NpcIdleActivity.Sit: ExecuteSit(); break;
                case NpcIdleActivity.Sleep: ExecuteSleep(); break;
            }
        }

        private void ExecuteStandStill()
        {
            if (_agent != null && _agent.isOnNavMesh) { _agent.isStopped = true; _agent.ResetPath(); }
        }

        private void ExecuteLookAround()
        {
            if (_agent != null && _agent.isOnNavMesh) _agent.isStopped = true;
        }

        private void ExecuteSocialize()
        {
            if (_agent == null || !_agent.isOnNavMesh) return;
            if (Time.unscaledTime < _socializeCooldown) return;
            if (_socializePartner == null || _socializePartner.IsDead ||
                Vector3.Distance(transform.position, _socializePartner.transform.position) > 15f)
                _socializePartner = FindSocializePartner();
            if (_socializePartner != null)
            {
                Vector3 midPoint = (transform.position + _socializePartner.transform.position) * 0.5f;
                if (Vector3.Distance(transform.position, midPoint) > 2f)
                {
                    _agent.isStopped = false;
                    _agent.SetDestination(midPoint);
                }
                else { _agent.isStopped = true; FaceTarget(_socializePartner.transform.position); }
            }
            _socializeCooldown = Time.unscaledTime + Random.Range(3f, 6f);
        }

        private NpcSocialBrain FindSocializePartner()
        {
            NpcSocialBrain best = null;
            float bestD = 15f * 15f;
            foreach (var o in FindObjectsByType<NpcSocialBrain>(FindObjectsSortMode.None))
            {
                if (o == this || o == null || o.IsDead || o._brain == null) continue;
                if (faction != null && o.faction != null && !faction.IsAllied(o.faction)) continue;
                if (o._brain.CurrentState != NpcBrain.BrainState.Idle) continue;
                float d = (o.transform.position - transform.position).sqrMagnitude;
                if (d < bestD && d > 0.01f) { bestD = d; best = o; }
            }
            return best;
        }

        private void ExecuteWork()
        {
            if (_agent != null && _agent.isOnNavMesh) _agent.isStopped = true;
            if (Time.unscaledTime > _workAnimTimer)
            {
                var anim = GetComponentInChildren<Animator>();
                if (anim != null) { anim.SetInteger("WorkVariant", Random.Range(0, 3)); anim.SetTrigger("Work"); }
                _workAnimTimer = Time.unscaledTime + Random.Range(5f, 15f);
            }
        }

        private void ExecuteSit()
        {
            if (_agent == null || !_agent.isOnNavMesh) return;
            if (_sitPoint != null)
            {
                if (_sitPoint.IsOccupied && _sitPoint._currentOccupant != this) _sitPoint = null;
                else { _agent.isStopped = true; return; }
            }
            if (Time.unscaledTime < _sitSearchCooldown) return;
            _sitSearchCooldown = Time.unscaledTime + 5f;
            SitPoint best = null;
            float bestD = 25f * 25f;
            foreach (var sp in FindObjectsByType<SitPoint>(FindObjectsSortMode.None))
            {
                if (sp == null || sp.IsOccupied) continue;
                float d = (transform.position - sp.SitPosition).sqrMagnitude;
                if (d < bestD) { bestD = d; best = sp; }
            }
            if (best != null)
            {
                _sitPoint = best;
                _sitPoint._currentOccupant = this;
                _agent.isStopped = false;
                _agent.SetDestination(best.SitPosition);
            }
            else _agent.isStopped = true;
        }

        private void ExecuteSleep()
        {
            if (!_sleepInitialized)
            {
                _sleepInitialized = true;
                _sleepWakeTime = Time.unscaledTime + Random.Range(30f, 120f);
                var anim = GetComponentInChildren<Animator>();
                if (anim != null) anim.SetBool("IsSleeping", true);
                if (_agent != null && _agent.isOnNavMesh) _agent.isStopped = true;
            }
            if (Time.unscaledTime > _sleepWakeTime)
            {
                _sleepInitialized = false;
                idleActivity = NpcIdleActivity.StandStill;
                var anim = GetComponentInChildren<Animator>();
                if (anim != null) anim.SetBool("IsSleeping", false);
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

        private void FaceTarget(Vector3 targetPos)
        {
            Vector3 dir = targetPos - transform.position; dir.y = 0;
            if (dir.sqrMagnitude < 0.01f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), 180f * Time.deltaTime);
        }

        // ==================== Config ====================
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
            if (config.socialRole != null) config.socialRole.ApplyTo(this);
            if (config.faction != null) faction = config.faction;
            enableGrudgeMemory = config.enableGrudgeMemory;
            grudgeDurationSec = config.grudgeDurationSec;
            if (_grudgeTable != null) _grudgeTable.grudgeDurationSec = grudgeDurationSec;
            if (config.personalityConfig != null) { personalityConfig = config.personalityConfig; _morale.Initialize(personalityConfig); }
        }

        // ==================== S07: Emotion ====================
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
        }

        // ==================== S09: Social Triggers ====================
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

            // T-NPC-S19 fix: если уже дерёмся с живой целью — не переключаемся.
            if (_brain.CurrentState == NpcBrain.BrainState.Chase ||
                _brain.CurrentState == NpcBrain.BrainState.Attack)
            {
                if (_brain.CurrentAggroTarget != null) return false;
            }

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
                    if (enableVengeanceMemory && faction != null && VengeanceMemory.Instance != null && killerClientId != 0)
                        VengeanceMemory.Instance.RegisterKill(faction.factionId, killerClientId);
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
            if (Group != null)
            {
                foreach (var m in Group.members)
                {
                    if (m == this || m == null || m._brain == null) continue;
                    bool inCombat = m._brain.CurrentState == NpcBrain.BrainState.Chase || m._brain.CurrentState == NpcBrain.BrainState.Attack;
                    if (!inCombat || Vector3.Distance(transform.position, m.transform.position) > 15f) continue;
                    target = m._brain.CurrentAggroTarget;
                    if (target != null) { _emotion.Set(NpcEmotion.Alert); return true; }
                }
            }
            if (faction != null)
            {
                foreach (var o in FindObjectsByType<NpcSocialBrain>(FindObjectsSortMode.None))
                {
                    if (o == this || o == null || o._brain == null) continue;
                    if (o.faction == null || !faction.IsAllied(o.faction)) continue;
                    bool inCombat = o._brain.CurrentState == NpcBrain.BrainState.Chase || o._brain.CurrentState == NpcBrain.BrainState.Attack;
                    if (!inCombat || Vector3.Distance(transform.position, o.transform.position) > 15f) continue;
                    target = o._brain.CurrentAggroTarget;
                    if (target != null) { _emotion.Set(NpcEmotion.Alert); return true; }
                }
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

        // ==================== S13: Threat Assessment ====================
        private bool EvaluateThreatBeforeCombat()
        {
            if (_brain == null) return false;
            var ta = ThreatAssessment.Evaluate(_brain, Group, threatEvaluationRange);
            _lastThreatResult = ta.result;
            switch (ta.result)
            {
                case ThreatResult.Confident: return false;
                case ThreatResult.Cautious:
                    if (personalityConfig != null && personalityConfig.recklessness > 0.7f) return false;
                    return true;
                case ThreatResult.Afraid:
                    if (personalityConfig != null && personalityConfig.recklessness > 0.8f) return false;
                    if (canFlee && !_isFleeing && _brain.CurrentAggroTarget != null) { StartFlee(); return true; }
                    DispatchVocalCue(NpcVocalCue.AlertCall);
                    return true;
                default: return false;
            }
        }

        // ==================== S14: Cover ====================
        private bool CheckCover()
        {
            if (_target == null || _brain == null || _agent == null || !_agent.isOnNavMesh) return false;
            if (_currentCover != null)
            {
                if (Time.unscaledTime - _coverEnterTime > coverSwitchInterval)
                {
                    if (IsUnderThreat()) { _coverSwitchCooldown = Time.unscaledTime + coverSwitchInterval * 0.5f; SeekCover(); }
                    else LeaveCover();
                }
                return true;
            }
            float hpPercent = _target.GetMaxHp() > 0 ? (float)_target.GetCurrentHp() / _target.GetMaxHp() : 1f;
            if (hpPercent <= coverHpThreshold && IsUnderThreat() && Time.unscaledTime > _coverSwitchCooldown)
            { SeekCover(); return _currentCover != null; }
            return false;
        }

        private bool IsUnderThreat()
        {
            if (_brain.CurrentAggroTarget != null) return true;
            return ThreatAssessment.HasEnemiesInRange(transform.position, _brain.aggroRange);
        }

        private void SeekCover()
        {
            if (_agent == null || !_agent.isOnNavMesh) return;
            CoverPoint best = null;
            float bestScore = float.MaxValue;
            foreach (var cp in FindObjectsByType<CoverPoint>(FindObjectsSortMode.None))
            {
                if (cp == null || cp == _currentCover || cp.IsOccupied) continue;
                float d = Vector3.Distance(transform.position, cp.StandPosition);
                if (d > coverSeekRadius) continue;
                float score = d / Mathf.Max(0.1f, cp.priority);
                if (score < bestScore) { bestScore = score; best = cp; }
            }
            if (best != null)
            {
                if (_currentCover != null) _currentCover._currentOccupant = null;
                _currentCover = best; _currentCover._currentOccupant = this;
                _coverEnterTime = Time.unscaledTime;
                _agent.isStopped = false;
                _agent.SetDestination(best.StandPosition);
            }
            else
            {
                Vector3? autoCover = AutoDetectCover();
                if (autoCover.HasValue) { _agent.isStopped = false; _agent.SetDestination(autoCover.Value); _coverSwitchCooldown = Time.unscaledTime + coverSwitchInterval; }
            }
        }

        private Vector3? AutoDetectCover()
        {
            Vector3 threatPos = _brain.CurrentAggroTarget != null ? _brain.CurrentAggroTarget.GetPosition() : transform.position + transform.forward * 10f;
            Vector3 awayFromThreat = (transform.position - threatPos).normalized;
            float[] angles = { 0f, 30f, -30f, 60f, -60f };
            foreach (float angle in angles)
            {
                Vector3 dir = Quaternion.AngleAxis(angle, Vector3.up) * awayFromThreat;
                Vector3 checkPos = transform.position + dir * coverSeekRadius * 0.7f;
                if (Physics.Raycast(checkPos + Vector3.up * 2f, Vector3.down, out RaycastHit groundHit, 10f, ~0, QueryTriggerInteraction.Ignore))
                {
                    Vector3 toThreat = threatPos - groundHit.point;
                    if (Physics.Raycast(groundHit.point, toThreat.normalized, out RaycastHit wallHit, toThreat.magnitude, ~0, QueryTriggerInteraction.Ignore))
                    {
                        if (NavMesh.SamplePosition(groundHit.point, out NavMeshHit navHit, 2f, NavMesh.AllAreas))
                            return navHit.position;
                    }
                }
            }
            return null;
        }

        private void LeaveCover()
        {
            if (_currentCover != null) { _currentCover._currentOccupant = null; _currentCover = null; }
            _coverEnterTime = 0f;
        }

        // ==================== S16: Surrender ====================
        private bool CheckSurrender()
        {
            if (!canSurrender || _hasSurrendered || _target == null || _brain == null) return false;
            float hpPercent = _target.GetMaxHp() > 0 ? (float)_target.GetCurrentHp() / _target.GetMaxHp() : 1f;
            if (hpPercent > surrenderHpThreshold) return false;
            bool hasNearbyAlly = false;
            if (Group != null)
            {
                foreach (var m in Group.members)
                { if (m != this && m != null && !m.IsDead && Vector3.Distance(transform.position, m.transform.position) <= surrenderAllyRadius) { hasNearbyAlly = true; break; } }
            }
            if (hasNearbyAlly) return false;
            float mercy = personalityConfig != null ? personalityConfig.mercy : 0.2f;
            if (mercy < 0.15f) return false;
            _hasSurrendered = true;
            if (_brain != null) _brain.ForceSurrender();
            return true;
        }

        // ==================== S17: Post-Combat ====================
        private void CheckPostCombat()
        {
            if (!enablePostCombat || _brain == null || _target == null) return;
            bool inCombat = _brain.CurrentState == NpcBrain.BrainState.Chase || _brain.CurrentState == NpcBrain.BrainState.Attack;
            if (_wasInCombat && !inCombat && _postCombat == PostCombatState.None)
            {
                float hpPercent = _target.GetMaxHp() > 0 ? (float)_target.GetCurrentHp() / _target.GetMaxHp() : 1f;
                if (hpPercent < healHpThreshold && HasNearbyDeadAllies()) StartPostCombat(PostCombatState.SeekingReinforcement);
                else if (hpPercent < healHpThreshold) StartPostCombat(PostCombatState.Healing);
                else if (hpPercent < 0.6f) StartPostCombat(PostCombatState.Wounded);
            }
            _wasInCombat = inCombat;
            switch (_postCombat)
            {
                case PostCombatState.Wounded: TickWounded(); break;
                case PostCombatState.Healing: TickHealing(); break;
                case PostCombatState.SeekingReinforcement: TickSeekingReinforcement(); break;
            }
        }

        private void StartPostCombat(PostCombatState s) { _postCombat = s; _postCombatTimer = Time.unscaledTime; _postCombatAggroBlock = true; }
        private void EndPostCombat() { _postCombat = PostCombatState.None; _postCombatAggroBlock = false; _morale.OnSuccessfulRetreat(); }

        private void TickWounded()
        {
            if (_agent != null && _agent.isOnNavMesh && !_agent.isStopped)
            {
                if (Vector3.Distance(transform.position, _brain.SpawnPoint) > 2f) _agent.SetDestination(_brain.SpawnPoint);
                else if (_agent.remainingDistance < 1f) _agent.isStopped = true;
            }
            if (Time.unscaledTime - _postCombatTimer > woundedDuration) EndPostCombat();
        }

        private void TickHealing()
        {
            if (_agent != null && _agent.isOnNavMesh) _agent.isStopped = true;
            if (Time.unscaledTime - _postCombatTimer > woundedDuration * 1.5f) EndPostCombat();
        }

        private void TickSeekingReinforcement()
        {
            Vector3 allyPos = FindNearestAlly();
            if (allyPos.sqrMagnitude > 0.1f && _agent != null && _agent.isOnNavMesh) { _agent.isStopped = false; _agent.SetDestination(allyPos); }
            else { _postCombat = PostCombatState.Wounded; _postCombatTimer = Time.unscaledTime; }
            if (Time.unscaledTime - _postCombatTimer > woundedDuration * 2f) EndPostCombat();
        }

        private bool HasNearbyDeadAllies()
        {
            if (Group == null) return false;
            foreach (var m in Group.members)
            { if (m != this && m != null && m.IsDead && Vector3.Distance(transform.position, m.transform.position) <= allyDeathRadius) return true; }
            return false;
        }

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
                    NpcVocalCue.AlertCall => "AlertCall", NpcVocalCue.DeathScream => "DeathScream",
                    NpcVocalCue.Taunt => "Taunt", NpcVocalCue.FearCry => "FearCry", NpcVocalCue.VictoryRoar => "VictoryRoar",
                    _ => "AlertCall",
                };
                anim.SetTrigger(tn);
            }
            if (Group != null) Group.OnVocalCue(this, cue);
        }
    }
}
