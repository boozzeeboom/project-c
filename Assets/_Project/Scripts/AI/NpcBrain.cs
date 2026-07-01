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
        [Tooltip("Гистерезис схода с платформы (кадры без опоры).")]
        [Min(1)] [SerializeField] private int _platformMissFramesToClear = 3;


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


        // T-NPC-14: passive aggro tracking.
        private bool _isAggrod;
        private float _aggroDamageAccumulator;   // cumulative damage с последнего reset (post-death или resetAggro).
        private readonly System.Collections.Generic.Queue<float> _recentHitTimes = new System.Collections.Generic.Queue<float>(); // timestamps за последние 60с.

        public BrainState CurrentState => _state;
        public Vector3 SpawnPoint => _spawnPoint;
        public BehaviorType CurrentBehavior => _behaviorType;
        public bool IsAggrod => _isAggrod;
        public float AggroDamagePercent => _target != null && _target.GetMaxHp() > 0f
            ? (_aggroDamageAccumulator / _target.GetMaxHp()) * 100f
            : 0f;

        /// <summary>T-NPC-14: вызывается из NpcSpawner после Instantiate для применения
        /// параметров агрессии из SpawnerConfig. Anti-restrictive: спавнер задаёт
        /// только behavior-related поля, остальные ranges/HPS остаются от префаба.</summary>
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
            _agent = GetComponent<NavMeshAgent>();
            _animator = GetComponentInChildren<Animator>();
            _spawnPoint = transform.position;

            // T-NPC-14: подписка на изменение HP для passive aggro tracking.
            if (_target != null)
            {
                _target.OnHpChanged += OnNpcHpChanged;
            }

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
            {
                _target.OnHpChanged -= OnNpcHpChanged;
            }
            if (_proxyGo != null) { Destroy(_proxyGo); _proxyGo = null; _proxyAgent = null; }
        }

        // T-NPC-14: server-side обработчик удара по NPC.
        private void OnNpcHpChanged(int newHp, int deltaHp)
        {
            if (!IsServer) return;
            if (_state == BrainState.Dead) return;
            if (deltaHp <= 0) return;  // только damage (не healing)
            if (_behaviorType != BehaviorType.Passive) return;
            if (_isAggrod) return;     // уже агрессивный — копим дальше, но триггерим уже сработавший

            // Логируем удар в очередь (timestamps старше 60с — отбрасываем).
            float now = Time.unscaledTime;
            _recentHitTimes.Enqueue(now);
            while (_recentHitTimes.Count > 0 && now - _recentHitTimes.Peek() > 60f)
            {
                _recentHitTimes.Dequeue();
            }
            _aggroDamageAccumulator += deltaHp;

            // Проверяем условия перехода в Aggro:
            // 1) cumulativeDamage% >= aggroHpThreshold
            // 2) hits in 60s >= maxHitsPerMinute (fallback при мелких ударах)
            bool thresholdReached = AggroDamagePercent >= _aggroHpThreshold;
            bool hitsReached = _maxHitsPerMinute > 0 && _recentHitTimes.Count >= _maxHitsPerMinute;

            if (thresholdReached || hitsReached)
            {
                _isAggrod = true;
                if (Debug.isDebugBuild)
                {
                    Debug.Log($"[NpcBrain] {gameObject.name} (Passive→Aggro): " +
                        $"damage%={AggroDamagePercent:F1}/{_aggroHpThreshold}, " +
                        $"hits={_recentHitTimes.Count}/{_maxHitsPerMinute}, reason={(thresholdReached ? "threshold" : "hits")}");
                }
                // Если игрок в aggroRange — сразу Chase, иначе дождёмся в Idle.
                // _aggroTarget может быть ещё не выбран (если игрок далеко) — Tick его подберёт.
                if (_aggroTarget == null)
                {
                    _aggroTarget = FindNearestPlayerTarget(aggroRange * 2f);  // чуть шире для подбора цели после удара
                }
                if (_aggroTarget != null && _state == BrainState.Idle)
                {
                    EnterChase();
                }
            }
        }

        private void Update()
        {
            if (!IsServer || _state == BrainState.Dead) return;
            // Server-side throttling (избегаем лишних FindObjects каждые frame).
            if (Time.unscaledTime < _nextTickTime) return;
            _nextTickTime = Time.unscaledTime + (1f / Mathf.Max(1, tickRate));
            Tick();
        }
        // === Moving-platform carry (server-side, T-CREW-01) ===
        // NavMeshAgent привязан к мировому NavMesh и не движется с палубой, поэтому на
        // движущемся корабле NPC "сдувает". Возим NPC за палубой на сервере (transform
        // авторитетен и реплицируется NetworkTransform). Формула — общий PlatformRideHelper.
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

            // T-CREW-03: если у палубы есть готовый ShipDeckNav — навигация прокси-агентом
            // в локальных координатах палубы; позиция/поворот берутся из прокси (carry не нужен).
            if (_deckNavActive && _proxyAgent != null && _state != BrainState.Dead)
            {
                DriveDeckNav();
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
            // Пока на палубе — NavMeshAgent не должен сам управлять позицией/поворотом
            // (мировой NavMesh под кораблём тянул бы NPC назад).
            if (_agent != null && !_agentAutoDrivePaused)
            {
                _agent.updatePosition = false;
                _agent.updateRotation = false;
                _agentAutoDrivePaused = true;
            }

            // T-CREW-03: если корабль имеет готовый ShipDeckNav — навигация по палубе через прокси.
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

            if (Debug.isDebugBuild) Debug.Log($"[NpcBrain] {gameObject.name} entered moving platform '{platform.name}' (deckNav={_deckNavActive})");
        }

        private void EndRide()
        {
            if (Debug.isDebugBuild && _ridePlatform != null)
                Debug.Log($"[NpcBrain] {gameObject.name} left moving platform '{_ridePlatform.name}'");
            _ridePlatform = null;

            // T-CREW-03: выключить нав по палубе.
            _deckNavActive = false;
            _deckNav = null;
            if (_proxyGo != null) _proxyGo.SetActive(false);

            // Вернуть управление агенту и ресинхронизировать с NavMesh.
            if (_agent != null && _agentAutoDrivePaused)
            {
                _agent.updatePosition = true;
                _agent.updateRotation = true;
                _agentAutoDrivePaused = false;
                if (_agent.isOnNavMesh) _agent.Warp(transform.position);
            }
        }

        // === Deck navigation via proxy agent (T-CREW-03) ===
        // Прокси-агент живёт в фиксированном нав-фрейме ShipDeckNav и ходит по статичному
        // навмешу палубы в ЛОКАЛЬНЫХ координатах. Мировая поза NPC = ShipRoot.TransformPoint(local),
        // поэтому NPC корректно едет с кораблём (вкл. крен) без пере-регистрации навмеша.
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
            _proxyAgent.updateRotation = false; // поворот NPC считаем сами
            _proxyAgent.updateUpAxis = false;
        }

        private void WarpProxyToNpc()
        {
            if (_proxyAgent == null || _deckNav == null) return;
            Vector3 navPos = _deckNav.DeckLocalToNav(_deckNav.WorldToDeckLocal(transform.position));
            if (NavMesh.SamplePosition(navPos, out NavMeshHit hit, 5f, NavMesh.AllAreas))
                _proxyAgent.Warp(hit.position);
            else
                _proxyAgent.Warp(navPos);
        }

        private void DriveDeckNav()
        {
            if (_proxyAgent == null || _deckNav == null) return;
            if (!_proxyAgent.isOnNavMesh) { WarpProxyToNpc(); return; }

            // Цель преследования → в нав-фрейм палубы.
            if (_state == BrainState.Chase && _aggroTarget != null)
            {
                Vector3 targetNav = _deckNav.DeckLocalToNav(_deckNav.WorldToDeckLocal(_aggroTarget.GetPosition()));
                _proxyAgent.isStopped = false;
                _proxyAgent.SetDestination(targetNav);
            }
            else
            {
                // Idle/Attack — стоим (позиция всё равно едет с кораблём через TransformPoint).
                _proxyAgent.isStopped = true;
            }

            // Мировая поза NPC из локальной позиции прокси на палубе.
            Vector3 deckLocal = _deckNav.NavToDeckLocal(_proxyAgent.transform.position);
            transform.position = _deckNav.DeckLocalToWorld(deckLocal);

            // Поворот по направлению движения прокси (в мировых осях палубы).
            Vector3 v = _proxyAgent.velocity;
            if (v.sqrMagnitude > 0.01f)
            {
                Vector3 worldDir = _deckNav.transform.TransformVector(v);
                worldDir.y = 0f;
                if (worldDir.sqrMagnitude > 0.0001f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(worldDir);
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, angularSpeed * Time.fixedDeltaTime);
                }
            }
        }


        private void Tick()
        {
            if (_attacker == null || _target == null) return;
            if (!_target.IsAlive()) { EnterDead(); return; }

            float distFromSpawn = Vector3.Distance(transform.position, _spawnPoint);

            // Сначала смотрим текущего aggro target (если ещё жив и в зоне leash).
            if (_aggroTarget != null)
            {
                if (!_aggroTarget.IsAlive() || distFromSpawn > leashRange * 1.5f)
                {
                    _aggroTarget = null;
                }
            }

            // Ищем ближайшего player в aggroRange.
            if (_aggroTarget == null)
            {
                _aggroTarget = FindNearestPlayerTarget(aggroRange);
            }

            switch (_state)
            {
                case BrainState.Idle:
                    HandleIdle();
                    break;
                case BrainState.Chase:
                    HandleChase();
                    break;
                case BrainState.Attack:
                    HandleAttack();
                    break;
            }

            UpdateAnimator();
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
            // T-NPC-14: Passive NPC (не агрившийся) не ищет target по proximity.
            if (_behaviorType == BehaviorType.Passive && !_isAggrod)
            {
                return;  // стоит мирно, ждёт удара
            }
            // Neutral NPC никогда не реагирует — остаётся в Idle всегда (кроме Dead).
            if (_behaviorType == BehaviorType.Neutral)
            {
                return;
            }
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

            // Leash: слишком далеко от spawn → возврат.
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

            // Перешли в attack range?
            if (dist <= attackRange) { EnterAttack(); return; }

            // Update destination (player может двигаться).
            if (_agent != null && _agent.isOnNavMesh)
            {
                _agent.SetDestination(targetPos);
            }
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
            // Первая попытка атаки — сразу.
            TryAttack();
        }

        private void HandleAttack()
        {
            if (_aggroTarget == null) { EnterIdle(); return; }
            if (!_aggroTarget.IsAlive()) { _aggroTarget = null; EnterIdle(); return; }

            float dist = Vector3.Distance(transform.position, _aggroTarget.GetPosition());
            // Цель отошла → Chase снова.
            if (dist > attackRange * 1.3f) { EnterChase(); return; }

            // Cooldown готов → атака.
            float now = Time.unscaledTime;
            if (now >= _lastAttackTime + (_attacker != null && _attacker.Data != null ? _attacker.Data.cooldownSeconds : 1.5f))
            {
                TryAttack();
            }
            else
            {
                // Face target (плавный разворот к цели).
                FaceTarget(_aggroTarget.GetPosition());
            }
        }

        private void TryAttack()
        {
            // T-NPC-14: Neutral NPC не атакует в принципе.
            if (_behaviorType == BehaviorType.Neutral) return;
            if (_aggroTarget == null) return;
            if (CombatServer.Instance == null) return;
            ulong attackerId = _attacker.GetAttackerId();
            ulong targetId = _aggroTarget.GetTargetId();
            ulong sourceId = attackerId; // MVP: NPC использует default source id = attacker id (1 source per NPC).
            CombatServer.Instance.ResolveAttack(attackerId, targetId, sourceId);
            _lastAttackTime = Time.unscaledTime;
            // Animation: kick "Attack" trigger (Animator должен иметь trigger "Attack" с коротким состоянием).
            if (_animator != null) _animator.SetTrigger("Attack");
        }

        // === Dead ===

        private void EnterDead()
        {
            _state = BrainState.Dead;
            if (_agent != null && _agent.isOnNavMesh) _agent.isStopped = true;
            if (_animator != null) _animator.SetTrigger("Death");
            // Death anim + loot будет в T-NPC-04 (post-MVP). Пока — NpcTarget уже Destroy(go, 3s).
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
            _animator.SetBool("IsGrounded", true); // T-NPC-13: NPC всегда на NavMesh
        }
    }
}