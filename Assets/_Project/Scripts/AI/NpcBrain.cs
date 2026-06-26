// Project C: Real-Time Combat Engine — T-NPC-01
// NpcBrain: server-side Finite State Machine для пешего NPC-врага.
// Design: docs/Character/Skills/real-time-combat/70_NPC_ENEMIES.md §2.3.
//
// States (server-side only):
//   [Idle]    --player in AggroRange (10м)--> [Chase]
//   [Chase]   --dist <= AttackRange (2м)-->   [Attack]
//   [Chase]   --dist > LeashRange (40м)-->    [Idle] (return to spawnPoint)
//   [Attack]  --cooldownElapsed + dist<=AttackRange--> [Attack]
//   [Any]     --HP<=0-->                      [Dead]
//
// Movement: NavMeshAgent (server-authoritative, replicates via NetworkTransform).
// Attacks: вызывает CombatServer.Instance.ResolveAttack напрямую (server-side call, не RPC).
//
// MVP scope: базовый 1v1 chase + melee attack. Без группирования (post-MVP), без flee,
// без patrol. Расширение — через NpcBrainState pattern или наследники (post-MVP).
//
// v0.1 (T-NPC-01): singleton per-NPC, hard-coded config. Designer-override через
// NpcCombatData (после T-NPC-02 — NpcSpawnerConfig параметры).
//
// Анти-рестриктивное: NpcBrain НЕ знает о Player/NPC конкретно — работает с
// IDamageTarget (любой объект, реализующий интерфейс).

using UnityEngine;
using UnityEngine.AI;
using Unity.Netcode;
using ProjectC.Combat;
using ProjectC.Combat.Core;
using System.Linq;

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

        // --- runtime ---
        private NavMeshAgent _agent;
        private Animator _animator;
        private BrainState _state = BrainState.Idle;
        private Vector3 _spawnPoint;
        private IDamageTarget _aggroTarget;
        private float _nextTickTime;
        private float _lastAttackTime = -10f;

        public BrainState CurrentState => _state;
        public Vector3 SpawnPoint => _spawnPoint;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (!IsServer) { enabled = false; return; }
            if (_attacker == null) _attacker = GetComponent<NpcAttacker>();
            if (_target == null) _target = GetComponent<NpcTarget>();
            _agent = GetComponent<NavMeshAgent>();
            _animator = GetComponentInChildren<Animator>();
            _spawnPoint = transform.position;
            if (_agent != null)
            {
                _agent.speed = moveSpeed;
                _agent.angularSpeed = angularSpeed;
                _agent.stoppingDistance = attackRange * 0.9f;
                _agent.autoBraking = true;
            }
            EnterIdle();
        }

        private void Update()
        {
            if (!IsServer || _state == BrainState.Dead) return;
            // Server-side throttling (избегаем лишних FindObjects каждые frame).
            if (Time.unscaledTime < _nextTickTime) return;
            _nextTickTime = Time.unscaledTime + (1f / Mathf.Max(1, tickRate));
            Tick();
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
            if (Vector3.Distance(_spawnPoint, targetPos) > leashRange)
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
            if (_agent != null && _agent.isOnNavMesh && !_agent.isStopped) speed = _agent.velocity.magnitude;
            _animator.SetFloat("Speed", speed);
            _animator.SetBool("IsAttacking", _state == BrainState.Attack);
            _animator.SetBool("IsGrounded", true); // T-NPC-13: NPC всегда на NavMesh
        }
    }
}