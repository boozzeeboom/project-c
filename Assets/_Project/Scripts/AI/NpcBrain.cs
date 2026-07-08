// Project C: Real-Time Combat Engine — T-NPC-01 / T-NPC-14
// NpcBrain: server-side Finite State Machine для пешего NPC (враг/пассивный).
// Design: docs/Character/Skills/real-time-combat/70_NPC_ENEMIES.md §2.3.
//
// States (server-side only):
//   [Idle]    --player in AggroRange (10м) AND aggrod--> [Chase]
//   [Chase]   --dist <= AttackRange (2м)-->   [Attack]
//   [Chase]   --dist > LeashRange (40м)-->    [Idle] (return to spawnPoint)
//   [Attack]  --cooldownElapsed + dist<=AttackRange--> [Attack]
//   [Any]     --HP<=0-->                      [Dead]
//
// T-NPC-14 Passive behavior:
//   - behaviorType = Passive: NPC мирный, не агрится по proximity, атакует только
//     после удара игрока при выполнении одного из условий:
//       1. cumulativeDamage / maxHp * 100 >= aggroHpThreshold (default 25%)
//       2. hits in last 60s >= maxHitsPerMinute (default 3, fallback)
//   - behaviorType = Aggressive: классическое поведение (агро по proximity)
//   - behaviorType = Neutral: никогда не атакует (для декораций)
//
// Movement: NavMeshAgent (server-authoritative, replicates via NetworkTransform).
// Attacks: вызывает CombatServer.Instance.ResolveAttack напрямую (server-side call, не RPC).
//
// MVP scope: базовый 1v1 chase + melee attack + passive/aggro split. Без группирования,
// без flee, без patrol. Расширение — через NpcBrainState pattern или наследники (post-MVP).
//
// v0.1 (T-NPC-01): singleton per-NPC, hard-coded config.
// v0.2 (T-NPC-02): designer-override через NpcSpawnerConfig (post-spawn apply).
// v0.3 (T-NPC-14): passive/aggressive/neutral behavior + aggro-by-damage thresholds.
// v0.4 (T-NPC-S02): social brain API — ForceChaseTarget, ForceFlee, SocialTick hook.
//
// Анти-рестриктивное: NpcBrain НЕ знает о Player/NPC конкретно — работает с
// IDamageTarget (любой объект, реализующий интерфейс).

using UnityEngine;
using UnityEngine.AI;
using Unity.Netcode;
using ProjectC.Combat;
using ProjectC.Combat.Core;
using System.Linq;
using ProjectC.Core;
using ProjectC.Ship;

namespace ProjectC.AI
{
    /// <summary>
    /// T-NPC-01: NPC brain (FSM AI). Server-side only (client не делает AI decisions).
    /// Агрессия и cooldown централизованно — через CombatServer / NpcAttacker.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class NpcBrain : NetworkBehaviour
    {
        public enum BrainState
        {
            Idle,
            Chase,
            Attack,
            Dead,
            /// <summary>T-NPC-S16: NPC сдался. Не атакует, ждёт решения игрока.</summary>
            Surrendered,
        }

        public enum BehaviorType
        {
            /// <summary>Классика: агрится по proximity (aggroRange).</summary>
            Aggressive,
            /// <summary>T-NPC-14: мирный NPC (квестодатель). Атакует только после удара игрока,
            /// при выполнении одного из условий: cumulativeDamage% >= aggroHpThreshold
            /// или hits in 60s >= maxHitsPerMinute.</summary>
            Passive,
            /// <summary>Декорация: никогда не атакует даже после удара (для тренировочных манекенов).</summary>
            Neutral,
        }

        [Header("Debug")]
        [Tooltip("Включить подробные логи в консоль.")]
        [SerializeField] private bool _debugLog = false;

        [Header("Behavior (T-NPC-14)")]
        [Tooltip("Aggressive = атакует по proximity (стандарт).\n" +
                 "Passive = мирный NPC (квестодатель), атакует только после удара игрока " +
                 "при выполнении aggroHpThreshold или maxHitsPerMinute.\n" +
                 "Neutral = никогда не атакует (декорация).")]
        [SerializeField] private BehaviorType _behaviorType = BehaviorType.Aggressive;

        [Tooltip("Passive-only: % от maxHp, после которого NPC становится агрессивным. " +
                 "Например, 25 = при потере 25% HP NPC переходит в Chase. " +
                 "Применяется только если behaviorType == Passive. " +
                 "Fallback: если за 60с получено hits >= maxHitsPerMinute — тоже агрится.")]
        [Range(1f, 100f)] [SerializeField] private float _aggroHpThreshold = 25f;

        [Tooltip("Passive-only: за сколько ударов в минуту NPC точно станет агрессивным " +
                 "(даже если cumulativeDamage% < aggroHpThreshold). Fallback для защиты от " +
                 "фарма квестовых NPC мелкими ударами. 0 = отключить fallback.")]
        [Range(0, 20)] [SerializeField] private int _maxHitsPerMinute = 3;

        [Header("Refs (auto-resolve)")]
        [SerializeField] private NpcAttacker _attacker;
        [SerializeField] private NpcTarget _target;

        [Header("Ranges (MVP — designer-override через NpcSpawnerConfig post-T-NPC-02)")]
        [Tooltip("Радиус агрессии: при входе player в этот радиус → Chase.")]
        [Range(2f, 30f)] public float aggroRange = 10f;
        [Tooltip("Радиус атаки: при dist<=attackRange в Chase → Attack.")]
        [Range(0.5f, 5f)] public float attackRange = 2.5f;
        [Tooltip("Радиус leash: при dist>leashRange от spawnPoint → возврат в Idle.")]
        [Range(10f, 200f)] public float leashRange = 40f;
        [Tooltip("Скорость NavMeshAgent при патруле/chase.")]
        [Range(0.5f, 10f)] public float moveSpeed = 3.5f;
        [Tooltip("Угловая скорость для разворота (degrees/sec).")]
        [Range(60f, 720f)] public float angularSpeed = 360f;

        [Header("Tick (server-side)")]
        [Tooltip("AI tick rate (server-side Update). 30 = ~2× в сек @ 60fps.")]
        [Range(1, 60)] public int tickRate = 10;

        [Header("Social Brain (T-NPC-S01)")]
        [Tooltip("Включает NpcSocialBrain — patrol, flee, grudge, social triggers. " +
                 "Если false — NPC использует только старый FSM (backward compat).")]
        [SerializeField] private bool _socialEnabled = true;

        [Header("Платформа (moving-platform carry, server-side)")]
        [Tooltip("Возить NPC вместе с движущейся палубой корабля (не сдувать). См. docs/Character/Skills/real-time-combat/npc-enemy/01_CREW_ON_MOVING_SHIP.md")]
        [SerializeField] private bool _platformCarryEnabled = true;
        [Tooltip("Слои палуб/движущихся платформ. Пусто (0) = carry выключен.")]
        [SerializeField] private LayerMask _platformMask;
        [Tooltip("Высота точки старта probe над пивотом NPC (м).")]
        [SerializeField] private float _platformProbeUp = 0.5f;
        [Tooltip("Добавочная дальность probe вниз ниже пивота (м).")]
        [SerializeField] private float _platformProbeDistance = 0.6f;
        [Tooltip("Радиус SphereCast для поиска палубы (м).")]
        [SerializeField] private float _platformProbeRadius = 0.3f;
        [Tooltip("Переносить курсовой поворот (yaw) палубы. Pitch/roll НЕ переносятся.")]
        [SerializeField] private bool _carryYaw = true;
        [Min(1)] [SerializeField] private int _platformMissFramesToClear = 3;
        [Tooltip("На кораблях с NetworkObject приклеивать NPC через TrySetParent (надёжнее carry). Fallback — carry без NetworkObject.")]
        [SerializeField] private bool _useParentingOnShips = true;


        // --- runtime ---
        private NavMeshAgent _agent;
        private Animator _animator;
        private BrainState _state = BrainState.Idle;
        private Vector3 _spawnPoint;
        private IDamageTarget _aggroTarget;
        private float _nextTickTime;
        private float _lastAttackTime = -10f;
        // Moving-platform carry (server-side)
        private Transform _ridePlatform;
        private Vector3 _rideLastPos;
        private Quaternion _rideLastRot;
        private int _rideMissFrames;
        private bool _agentAutoDrivePaused;
        // Deck navigation (T-CREW-03): прокси-агент в нав-фрейме ShipDeckNav
        private ShipDeckNav _deckNav;
        private NavMeshAgent _proxyAgent;
        private GameObject _proxyGo;
        private bool _deckNavActive;
        private Vector3 _proxyLastPos;
        private NetworkObject _netObject;
        private bool _parentedToShip;

        // T-NPC-S02: social brain companion
        private NpcSocialBrain _socialBrain;
        private bool _socialOverrideLock;
        private float _socialOverrideLockExpireTime;
        private const float SOCIAL_OVERRIDE_TIMEOUT = 1.5f;

        // T-NPC-14: passive aggro tracking.
        private bool _isAggrod;
        private float _aggroDamageAccumulator;
        private readonly System.Collections.Generic.Queue<float> _recentHitTimes = new System.Collections.Generic.Queue<float>();

        public BrainState CurrentState => _state;
        public Vector3 SpawnPoint => _spawnPoint;
        public BehaviorType CurrentBehavior => _behaviorType;
        public bool IsAggrod => _isAggrod;
        public float AggroDamagePercent => _target != null && _target.GetMaxHp() > 0f
            ? (_aggroDamageAccumulator / _target.GetMaxHp()) * 100f
            : 0f;

        // T-NPC-S02: public accessor для NpcSocialBrain (читает приватное _aggroTarget).
        public IDamageTarget CurrentAggroTarget => _aggroTarget;
        public bool IsSocialEnabled => _socialEnabled;

        public void ApplySpawnerBehavior(BehaviorType behavior, float aggroHpThreshold, int maxHitsPerMinute)
        {
            if (!IsServer) return;
            _behaviorType = behavior;
            if (aggroHpThreshold > 0f) _aggroHpThreshold = aggroHpThreshold;
            if (maxHitsPerMinute >= 0) _maxHitsPerMinute = maxHitsPerMinute;
            ResetAggroState();
        }

        private void ResetAggroState()
        {
            _isAggrod = false;
            _aggroDamageAccumulator = 0f;
            _recentHitTimes.Clear();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (!IsServer) { enabled = false; return; }
            if (_attacker == null) _attacker = GetComponent<NpcAttacker>();
            if (_target == null) _target = GetComponent<NpcTarget>();
            // T-NPC-S19 fix: NpcAttacker.Target = null → IsAlive()=false, _defaultSource=null → InvalidSource.
            if (_attacker != null && _attacker.Target == null && _target != null)
                _attacker.Target = _target;
            _agent = GetComponent<NavMeshAgent>();
            _animator = GetComponentInChildren<Animator>();
            _spawnPoint = transform.position;

            // T-NPC-S02: ищем NpcSocialBrain компаньона (если social enabled).
            if (_socialEnabled)
                _socialBrain = GetComponent<NpcSocialBrain>();

            if (_target != null)
                _target.OnHpChanged += OnNpcHpChanged;

            if (_agent != null)
            {
                _agent.speed = moveSpeed;
                _agent.angularSpeed = angularSpeed;
                _agent.stoppingDistance = attackRange * 0.9f;
                _agent.autoBraking = true;
            }
            EnterIdle();
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            if (_target != null)
                _target.OnHpChanged -= OnNpcHpChanged;
            if (_proxyGo != null) { Destroy(_proxyGo); _proxyGo = null; _proxyAgent = null; }
        }

        private void OnNpcHpChanged(int newHp, int deltaHp)
        {
            if (!IsServer) return;
            if (_state == BrainState.Dead) return;
            if (deltaHp <= 0) return;
            if (_behaviorType != BehaviorType.Passive) return;
            if (_isAggrod) return;

            float now = Time.unscaledTime;
            _recentHitTimes.Enqueue(now);
            while (_recentHitTimes.Count > 0 && now - _recentHitTimes.Peek() > 60f)
                _recentHitTimes.Dequeue();
            _aggroDamageAccumulator += deltaHp;

            bool thresholdReached = AggroDamagePercent >= _aggroHpThreshold;
            bool hitsReached = _maxHitsPerMinute > 0 && _recentHitTimes.Count >= _maxHitsPerMinute;

            if (thresholdReached || hitsReached)
            {
                _isAggrod = true;
                if (_aggroTarget == null)
                    _aggroTarget = FindNearestPlayerTarget(aggroRange * 2f);
                if (_aggroTarget != null && _state == BrainState.Idle)
                    EnterChase();
            }
        }

        private void Update()
        {
            if (!IsServer || _state == BrainState.Dead || _state == BrainState.Surrendered) return;
            if (Time.unscaledTime < _nextTickTime) return;
            _nextTickTime = Time.unscaledTime + (1f / Mathf.Max(1, tickRate));
            Tick();
        }

        private void FixedUpdate()
        {
            if (!IsServer || !_platformCarryEnabled) return;
            if (_platformMask == 0) return;

            Vector3 origin = transform.position + Vector3.up * _platformProbeUp;
            float castDist = _platformProbeUp + _platformProbeDistance;
            Transform platform = PlatformRideHelper.DetectPlatform(origin, _platformProbeRadius, castDist, _platformMask);

            if (platform == null)
            {
                _rideMissFrames++;
                if (_rideMissFrames >= _platformMissFramesToClear && _ridePlatform != null) EndRide();
                return;
            }

            _rideMissFrames = 0;
            if (platform != _ridePlatform) { BeginRide(platform); return; }

            if (_parentedToShip)
            {
                if (_deckNavActive && _proxyAgent != null && _state != BrainState.Dead) DriveDeckNav();
                _rideLastPos = platform.position;
                _rideLastRot = platform.rotation;
                return;
            }

            Vector3 deltaPos = PlatformRideHelper.ComputeCarryDelta(
                platform, transform.position, _rideLastPos, _rideLastRot, _carryYaw, out float deltaYaw);

            if (deltaPos.sqrMagnitude > 0f) transform.position += deltaPos;
            if (Mathf.Abs(deltaYaw) > 0.0001f)
                transform.rotation = Quaternion.AngleAxis(deltaYaw, Vector3.up) * transform.rotation;

            _rideLastPos = platform.position;
            _rideLastRot = platform.rotation;
        }

        private void BeginRide(Transform platform)
        {
            _ridePlatform = platform;
            _rideLastPos = platform.position;
            _rideLastRot = platform.rotation;
            if (_agent != null && !_agentAutoDrivePaused)
            {
                _agent.updatePosition = false;
                _agent.updateRotation = false;
                _agentAutoDrivePaused = true;
            }

            if (_netObject == null) _netObject = GetComponent<NetworkObject>();
            NetworkObject shipNo = platform.GetComponentInParent<NetworkObject>();
            _parentedToShip = false;
            if (_useParentingOnShips && _netObject != null && shipNo != null && shipNo.IsSpawned)
            {
                if (_netObject.transform.parent != shipNo.transform)
                    _netObject.TrySetParent(shipNo, true);
                _parentedToShip = _netObject.transform.parent == shipNo.transform;
            }

            _deckNav = platform.GetComponentInParent<ShipDeckNav>();
            if (_deckNav != null && _deckNav.IsReady)
            {
                EnsureProxy();
                if (_proxyAgent != null)
                {
                    WarpProxyToNpc();
                    _deckNavActive = true;
                }
            }
        }

        private void EndRide()
        {
            _ridePlatform = null;
            if (_parentedToShip && _netObject != null)
                _netObject.TrySetParent((Transform)null, true);
            _parentedToShip = false;
            _deckNavActive = false;
            _deckNav = null;
            if (_proxyGo != null) _proxyGo.SetActive(false);

            if (_agent != null && _agentAutoDrivePaused)
            {
                _agent.updatePosition = true;
                _agent.updateRotation = true;
                _agentAutoDrivePaused = false;
                if (_agent.isOnNavMesh) _agent.Warp(transform.position);
            }
        }

        private void EnsureProxy()
        {
            if (_proxyGo != null) { _proxyGo.SetActive(true); return; }
            _proxyGo = new GameObject($"NpcDeckNavProxy_{name}");
            _proxyGo.hideFlags = HideFlags.HideAndDontSave;
            _proxyAgent = _proxyGo.AddComponent<NavMeshAgent>();
            if (_agent != null)
            {
                _proxyAgent.agentTypeID = _agent.agentTypeID;
                _proxyAgent.radius = _agent.radius;
                _proxyAgent.height = _agent.height;
                _proxyAgent.speed = _agent.speed;
                _proxyAgent.angularSpeed = _agent.angularSpeed;
                _proxyAgent.acceleration = _agent.acceleration;
                _proxyAgent.stoppingDistance = _agent.stoppingDistance;
                _proxyAgent.autoBraking = _agent.autoBraking;
            }
            _proxyAgent.updateRotation = false;
            _proxyAgent.updateUpAxis = false;
        }

        private float _warpWarnCooldown;
        private const float WARP_WARN_INTERVAL = 2f;
        private void WarpProxyToNpc()
        {
            if (_proxyAgent == null || _deckNav == null) return;
            Vector3 deckLocal = _parentedToShip ? transform.localPosition : _deckNav.WorldToDeckLocal(transform.position);
            Vector3 navPos = _deckNav.DeckLocalToNav(deckLocal);

            float[] radii = { 2f, 10f, 50f };
            NavMeshHit hit = default;
            bool found = false;
            for (int i = 0; i < radii.Length; i++)
            {
                if (NavMesh.SamplePosition(navPos, out hit, radii[i], NavMesh.AllAreas))
                {
                    found = true;
                    break;
                }
            }

            if (found)
            {
                _proxyAgent.Warp(hit.position);
            }
            else if (Time.unscaledTime >= _warpWarnCooldown)
            {
                _warpWarnCooldown = Time.unscaledTime + WARP_WARN_INTERVAL;
                float nearestMiss = -1f;
                NavMeshHit probe = default;
                if (NavMesh.SamplePosition(navPos, out probe, 200f, NavMesh.AllAreas))
                    nearestMiss = Vector3.Distance(navPos, probe.position);
                Debug.LogWarning(
                    $"[NpcBrain] {gameObject.name}: deckNav Warp miss — navPos={navPos:F2}, deckLocal={deckLocal:F2}, " +
                    $"agentTypeID={_proxyAgent.agentTypeID}, navmesh-nearest={nearestMiss:F2}m.");
            }

            _proxyLastPos = _proxyAgent.transform.position;
        }

        private void DriveDeckNav()
        {
            if (_proxyAgent == null || _deckNav == null) return;
            if (!_proxyAgent.isOnNavMesh) { WarpProxyToNpc(); return; }

            if (_state == BrainState.Chase && _aggroTarget != null)
            {
                Vector3 tgtLocal = _parentedToShip
                    ? _deckNav.transform.InverseTransformPoint(_aggroTarget.GetPosition())
                    : _deckNav.WorldToDeckLocal(_aggroTarget.GetPosition());
                _proxyAgent.isStopped = false;
                _proxyAgent.SetDestination(_deckNav.DeckLocalToNav(tgtLocal));
            }
            else
            {
                _proxyAgent.isStopped = true;
            }

            Vector3 proxyDelta = _proxyAgent.transform.position - _proxyLastPos;
            _proxyLastPos = _proxyAgent.transform.position;

            if (_parentedToShip)
            {
                if (proxyDelta.sqrMagnitude > 0f) transform.localPosition += proxyDelta;
                Vector3 flat = proxyDelta; flat.y = 0f;
                if (flat.sqrMagnitude > 1e-6f)
                {
                    Quaternion look = Quaternion.LookRotation(flat);
                    transform.localRotation = Quaternion.RotateTowards(transform.localRotation, look, angularSpeed * Time.fixedDeltaTime);
                }
            }
            else
            {
                Vector3 worldDelta = _deckNav.transform.TransformVector(proxyDelta);
                if (worldDelta.sqrMagnitude > 0f) transform.position += worldDelta;
                Vector3 flat = worldDelta; flat.y = 0f;
                if (flat.sqrMagnitude > 1e-6f)
                {
                    Quaternion look = Quaternion.LookRotation(flat);
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, look, angularSpeed * Time.fixedDeltaTime);
                }
            }
        }

        // ============================================================
        // T-NPC-S02: Social Brain API (add-only, Phase 1)
        // ============================================================

        public void ForceChaseTarget(IDamageTarget target)
        {
            if (target == null) return;
            _aggroTarget = target;
            _socialOverrideLock = true;
            _socialOverrideLockExpireTime = Time.unscaledTime + SOCIAL_OVERRIDE_TIMEOUT;
            if (_deckNavActive)
            {
                EnsureProxy();
                if (_proxyAgent != null)
                {
                    Vector3 tgtLocal = _deckNav.WorldToDeckLocal(target.GetPosition());
                    _proxyAgent.SetDestination(_deckNav.DeckLocalToNav(tgtLocal));
                    _proxyAgent.isStopped = false;
                }
            }
            EnterChase();
        }

        public void ForceFlee(Vector3 fromPosition)
        {
            _socialOverrideLock = true;
            _socialOverrideLockExpireTime = Time.unscaledTime + SOCIAL_OVERRIDE_TIMEOUT;
            Vector3 fleeDir = (transform.position - fromPosition).normalized;
            Vector3 fleeTarget = transform.position + fleeDir * 20f;
            float spawnDistToThreat = Vector3.Distance(_spawnPoint, fromPosition);
            float fleeDistToThreat = Vector3.Distance(fleeTarget, fromPosition);
            if (spawnDistToThreat > fleeDistToThreat)
                fleeTarget = _spawnPoint;

            if (_deckNavActive)
            {
                EnsureProxy();
                if (_proxyAgent != null)
                {
                    Vector3 fleeLocal = _deckNav.WorldToDeckLocal(fleeTarget);
                    _proxyAgent.SetDestination(_deckNav.DeckLocalToNav(fleeLocal));
                    _proxyAgent.isStopped = false;
                }
            }
            else if (_agent != null && _agent.isOnNavMesh)
            {
                _agent.isStopped = false;
                _agent.SetDestination(fleeTarget);
            }
            _aggroTarget = null;
        }

        // --- Main Tick (FSM core) ---

        private void Tick()
        {
            if (_attacker == null || _target == null) return;
            if (!_target.IsAlive()) { EnterDead(); return; }

            float distFromSpawn = Vector3.Distance(transform.position, _spawnPoint);

            if (_socialOverrideLock)
            {
                if (Time.unscaledTime > _socialOverrideLockExpireTime)
                    _socialOverrideLock = false;
            }
            else
            {
                if (_aggroTarget != null)
                {
                    if (!_aggroTarget.IsAlive() || distFromSpawn > leashRange * 1.5f)
                        _aggroTarget = null;
                }

                if (_aggroTarget == null)
                    _aggroTarget = FindNearestHostileTarget(aggroRange);
            }

            switch (_state)
            {
                case BrainState.Idle: HandleIdle(); break;
                case BrainState.Chase: HandleChase(); break;
                case BrainState.Attack: HandleAttack(); break;
                case BrainState.Surrendered: HandleSurrendered(); break;
            }

            UpdateAnimator();

            // T-NPC-S02: SocialTick — throttled внутри NpcSocialBrain (~0.5с).
            if (_socialEnabled)
            {
                // Lazy init: если компонент добавлен после OnNetworkSpawn (edge case).
                if (_socialBrain == null)
                    _socialBrain = GetComponent<NpcSocialBrain>();
                if (_socialBrain != null)
                    _socialBrain.Tick(this);
            }
        }

        // === Idle ===

        private void EnterIdle()
        {
            _state = BrainState.Idle;
            if (_agent != null && _agent.isOnNavMesh)
            {
                _agent.isStopped = true;
                _agent.ResetPath();
            }
        }

        private void HandleIdle()
        {
            if (_behaviorType == BehaviorType.Passive && !_isAggrod) return;
            if (_behaviorType == BehaviorType.Neutral) return;
            if (_aggroTarget == null) return;
            if (Vector3.Distance(transform.position, _aggroTarget.GetPosition()) > aggroRange) { _aggroTarget = null; return; }
            EnterChase();
        }

        // === Chase ===

        private void EnterChase()
        {
            _state = BrainState.Chase;
            if (_agent != null && _agent.isOnNavMesh) _agent.isStopped = false;
        }

        private void HandleChase()
        {
            if (_aggroTarget == null) { EnterIdle(); return; }
            Vector3 targetPos = _aggroTarget.GetPosition();
            float dist = Vector3.Distance(transform.position, targetPos);

            if (!_deckNavActive && Vector3.Distance(_spawnPoint, targetPos) > leashRange)
            {
                _aggroTarget = null;
                EnterIdle();
                if (_agent != null && _agent.isOnNavMesh)
                {
                    _agent.isStopped = false;
                    _agent.SetDestination(_spawnPoint);
                }
                return;
            }

            if (dist <= attackRange) { EnterAttack(); return; }

            if (_agent != null && _agent.isOnNavMesh)
                _agent.SetDestination(targetPos);
        }

        // === Attack ===

        private void EnterAttack()
        {
            _state = BrainState.Attack;
            if (_agent != null && _agent.isOnNavMesh)
            {
                _agent.isStopped = true;
                _agent.ResetPath();
            }
            TryAttack();
        }

        private void HandleAttack()
        {
            if (_aggroTarget == null) { EnterIdle(); return; }
            if (!_aggroTarget.IsAlive()) { _aggroTarget = null; EnterIdle(); return; }

            float dist = Vector3.Distance(transform.position, _aggroTarget.GetPosition());
            if (dist > attackRange * 1.3f) { EnterChase(); return; }

            float now = Time.unscaledTime;
            if (now >= _lastAttackTime + (_attacker != null && _attacker.Data != null ? _attacker.Data.cooldownSeconds : 1.5f))
                TryAttack();
            else
                FaceTarget(_aggroTarget.GetPosition());
        }

        private void TryAttack()
        {
            if (_behaviorType == BehaviorType.Neutral) return;
            if (_aggroTarget == null) return;
            if (CombatServer.Instance == null) return;
            ulong attackerId = _attacker.GetAttackerId();
            ulong targetId = _aggroTarget.GetTargetId();
            ulong sourceId = attackerId;
            CombatServer.Instance.ResolveAttack(attackerId, targetId, sourceId);
            _lastAttackTime = Time.unscaledTime;
            if (_animator != null) _animator.SetTrigger("Attack");
        }

        // === Dead ===

        private void EnterDead()
        {
            _state = BrainState.Dead;
            if (_agent != null && _agent.isOnNavMesh) _agent.isStopped = true;
            if (_animator != null) _animator.SetTrigger("Death");
        }

        // === Surrendered (T-NPC-S16) ===

        private void EnterSurrendered()
        {
            _state = BrainState.Surrendered;
            _aggroTarget = null;
            _socialOverrideLock = false;
            if (_agent != null && _agent.isOnNavMesh)
            {
                _agent.isStopped = true;
                _agent.ResetPath();
            }
            if (_animator != null) _animator.SetTrigger("Surrender");
        }

        private void HandleSurrendered()
        {
            // Surrendered — пассивное состояние. Ничего не делаем.
            // Выход из Surrender — только через внешний API (ForceChaseTarget) или смерть.
            if (!_target.IsAlive()) { EnterDead(); return; }
        }

        /// <summary>
        /// T-NPC-S16: Принудительно перевести NPC в состояние Surrendered.
        /// Вызывается NpcSocialBrain при выполнении условий сдачи.
        /// </summary>
        public void ForceSurrender()
        {
            if (_state == BrainState.Dead || _state == BrainState.Surrendered) return;
            EnterSurrendered();
        }

        // === Helpers ===

        private IDamageTarget FindNearestPlayerTarget(float range)
        {
            IDamageTarget best = null;
            float bestDistSq = range * range;
            if (NetworkManager.Singleton == null) return null;
            foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
            {
                if (client?.PlayerObject == null) continue;
                var tgt = client.PlayerObject.GetComponent<ProjectC.Combat.PlayerTarget>();
                if (tgt == null || !tgt.IsAlive()) continue;
                float d = (client.PlayerObject.transform.position - transform.position).sqrMagnitude;
                if (d <= bestDistSq) { bestDistSq = d; best = tgt; }
            }
            return best;
        }

        /// <summary>
        /// T-NPC-S19: поиск ближайшей вражеской цели — игроки ИЛИ NPC враждебных фракций.
        /// Заменяет FindNearestPlayerTarget для faction-aware поведения.
        /// </summary>
        private IDamageTarget FindNearestHostileTarget(float range)
        {
            IDamageTarget best = null;
            float bestDistSq = range * range;
            Vector3 myPos = transform.position;

            // 1. Ищем игроков (всегда hostile).
            if (NetworkManager.Singleton != null)
            {
                foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
                {
                    if (client?.PlayerObject == null) continue;
                    var pt = client.PlayerObject.GetComponent<ProjectC.Combat.PlayerTarget>();
                    if (pt == null || !pt.IsAlive()) continue;
                    float d = (client.PlayerObject.transform.position - myPos).sqrMagnitude;
                    if (d <= bestDistSq) { bestDistSq = d; best = pt; }
                }
            }

            // 2. Ищем NPC враждебных фракций (T-NPC-S19).
            if (_socialBrain != null && _socialBrain.faction != null)
            {
                foreach (var npc in FindObjectsByType<NpcSocialBrain>(FindObjectsSortMode.None))
                {
                    if (npc == null || npc == _socialBrain || npc.IsDead || npc._brain == null || npc.faction == null) continue;
                    if (!_socialBrain.faction.IsHostile(npc.faction)) continue;
                    var nt = npc.GetComponent<NpcTarget>();
                    if (nt == null || !nt.IsAlive()) continue;
                    float d = (npc.transform.position - myPos).sqrMagnitude;
                    if (d <= bestDistSq) { bestDistSq = d; best = nt; }
                }
            }

            return best;
        }

        private void FaceTarget(Vector3 targetPos)
        {
            Vector3 dir = targetPos - transform.position;
            dir.y = 0;
            if (dir.sqrMagnitude < 0.01f) return;
            Quaternion targetRot = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, angularSpeed * Time.deltaTime);
        }

        private void UpdateAnimator()
        {
            if (_animator == null) return;
            float speed = 0f;
            if (_deckNavActive && _proxyAgent != null) speed = _proxyAgent.velocity.magnitude;
            else if (_agent != null && _agent.isOnNavMesh && !_agent.isStopped) speed = _agent.velocity.magnitude;
            _animator.SetFloat("Speed", speed);
            _animator.SetBool("IsAttacking", _state == BrainState.Attack);
            _animator.SetBool("IsGrounded", true);
        }
    }
}
