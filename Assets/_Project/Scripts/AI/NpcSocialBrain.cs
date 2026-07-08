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
