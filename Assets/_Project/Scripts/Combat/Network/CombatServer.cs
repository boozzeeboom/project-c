// Project C: Real-Time Combat Engine — T-RTC06 + T-RTC08
// CombatServer: NetworkBehaviour, server-authoritative hub для combat.
// Design: docs/Character/Skills/real-time-combat/10_DESIGN.md §5, 20_TECHNICAL.md §2-3.
//
// Scene-placed в BootstrapScene (Phase: scene-integration). Singleton через Instance.
// Не знает о Player/Npc/Ship — работает через IAttacker/IDamageTarget/IDamageSource.
//
// Flow: client → RequestAttackRpc → ResolveAttack → DamageCalculator → broadcast AttackLandedTargetRpc.

using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using ProjectC.Combat.Client;
using ProjectC.Combat.Core;
using ProjectC.Combat.Network;
using ProjectC.Core;

namespace ProjectC.Combat
{
    public class CombatServer : NetworkBehaviour
    {
        public static CombatServer Instance { get; private set; }

        [Header("Debug")]
        [Tooltip("Включить подробные логи в консоль.")]
        [SerializeField] private bool _debugLog = false;

        // === Registries (server-side only) ===
        private readonly Dictionary<ulong, IAttacker> _attackers = new Dictionary<ulong, IAttacker>();
        private readonly Dictionary<ulong, IDamageTarget> _targets = new Dictionary<ulong, IDamageTarget>();

        // === Cooldown (per 2.3 — централизованно в CombatServer) ===
        // (attackerId, sourceId) → readyTime (Time.unscaledTime)
        private readonly Dictionary<(ulong, ulong), float> _cooldowns = new Dictionary<(ulong, ulong), float>();

        // === Rate limit (anti-spam, 10 ops/sec per client) ===
        private readonly Dictionary<ulong, float> _nextAllowedTime = new Dictionary<ulong, float>();
        private const float RATE_LIMIT_INTERVAL = 0.1f;

        // === Lifecycle ===

        /// <summary>
        /// T-RTC06 (v0.1.2): Second-chance recovery через 1 сек после OnNetworkSpawn.
        /// Race condition: если Player.NetworkObject spawned'ится ПОЗЖЕ CombatServer,
        /// push-down в OnNetworkSpawn его не ловит (Player ещё не в сцене FindObjectsByType
        /// на момент OnNetworkSpawn). Invoke через 1 сек повторяет find — подхватывает.
        /// </summary>
        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (!IsServer) return;
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[CombatServer] OnNetworkSpawn: another Instance already set. Replacing.");
            }
            Instance = this;
            if (_debugLog) Debug.Log("[CombatServer] OnNetworkSpawn: Instance set, IsServer=True.");

            // T-RTC06 (v0.1 fix): Push-down registration — подобрать всех PlayerAttacker/NpcAttacker/
            // PlayerTarget/NpcTarget в сцене, которые уже NetworkSpawn'нулись, но не зарегистрированы
            // (race condition: их OnNetworkSpawn мог сработать раньше нашего Instance = this).
            // После этого — все combat entities синхронизированы.
            RecoverExistingEntities();

            // T-RTC06 (v0.1.2): Second-chance через 1 сек — для Player, чей NetworkObject
            // spawned'ится ПОЗЖЕ CombatServer (push-down в OnNetworkSpawn его не ловит).
            if (IsInvoking(nameof(RecoverExistingEntities)))
            {
                CancelInvoke(nameof(RecoverExistingEntities));
            }
            Invoke(nameof(RecoverExistingEntities), 1.0f);
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// T-RTC06 (v0.1 fix): Подобрать уже-спавненные IAttacker/IDamageTarget в сцене
        /// и зарегистрировать тех, кто ещё не зарегистрирован. Страховка от race condition.
        /// v0.1.2 fix: убран skip `if (id == 0) continue;` для Player (0 = валидный clientId для host).
        /// v0.1.2 fix: добавлен second-chance recovery через Start (для Player, чей NetworkObject
        /// spawned'ится ПОЗЖЕ CombatServer — push-down в OnNetworkSpawn его не ловит).
        /// </summary>
        private void RecoverExistingEntities()
        {
            var playerAttackers = FindObjectsByType<PlayerAttacker>();
            foreach (var pa in playerAttackers)
            {
                if (pa == null) continue;
                ulong id = pa.ClientId;  // 0 = host player, ВАЛИДНО
                if (!_attackers.ContainsKey(id))
                {
                    _attackers[id] = pa;
                    if (_debugLog) Debug.Log($"[CombatServer] RecoverExistingEntities: registered PlayerAttacker id={id}");
                }
            }

            var playerTargets = FindObjectsByType<PlayerTarget>();
            foreach (var pt in playerTargets)
            {
                if (pt == null) continue;
                ulong id = pt.ClientId;  // 0 = host player, ВАЛИДНО
                if (!_targets.ContainsKey(id))
                {
                    _targets[id] = pt;
                    if (_debugLog) Debug.Log($"[CombatServer] RecoverExistingEntities: registered PlayerTarget id={id}");
                }
            }

            var npcAttackers = FindObjectsByType<NpcAttacker>();
            foreach (var na in npcAttackers)
            {
                if (na == null) continue;
                ulong id = na.GetAttackerId();
                if (id == 0) continue;  // для NPC id==0 = не инициализирован
                if (!_attackers.ContainsKey(id))
                {
                    _attackers[id] = na;
                    if (_debugLog) Debug.Log($"[CombatServer] RecoverExistingEntities: registered NpcAttacker id={id}");
                }
            }

            var npcTargets = FindObjectsByType<NpcTarget>();
            foreach (var nt in npcTargets)
            {
                if (nt == null) continue;
                ulong id = nt.GetTargetId();
                if (id == 0) continue;  // для NPC id==0 = не инициализирован
                if (!_targets.ContainsKey(id))
                {
                    _targets[id] = nt;
                    if (_debugLog) Debug.Log($"[CombatServer] RecoverExistingEntities: registered NpcTarget id={id}");
                }
            }

            if (_debugLog)
            {
                Debug.Log($"[CombatServer] RecoverExistingEntities done: attackers={_attackers.Count}, targets={_targets.Count}");
            }
        }

        // === Registration (called by PlayerAttacker/NpcAttacker at spawn) ===

        public void RegisterAttacker(ulong id, IAttacker attacker)
        {
            if (attacker == null) return;
            _attackers[id] = attacker;
            if (_debugLog) Debug.Log($"[CombatServer] Registered attacker id={id} ({attacker.GetType().Name})");
        }

        public void RegisterTarget(ulong id, IDamageTarget target)
        {
            if (target == null) return;
            _targets[id] = target;
            if (_debugLog) Debug.Log($"[CombatServer] Registered target id={id} ({target.GetType().Name})");
        }

        public void UnregisterAttacker(ulong id)
        {
            _attackers.Remove(id);
        }

        public void UnregisterTarget(ulong id)
        {
            _targets.Remove(id);
        }

        // === Cooldown API (для IAttacker.CanAttack/SetCooldown) ===

        public bool IsCooldownReady(ulong attackerId, ulong sourceId, float now)
        {
            return !_cooldowns.TryGetValue((attackerId, sourceId), out float ready) || now >= ready;
        }

        public void SetCooldown(ulong attackerId, ulong sourceId, float until)
        {
            _cooldowns[(attackerId, sourceId)] = until;
        }

        // === Client → Server RPC ===

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void RequestAttackRpc(ulong targetId, ulong sourceId, RpcParams rpcParams = default)
        {
            ulong attackerId = rpcParams.Receive.SenderClientId;
            if (!RateLimit(attackerId)) return;
            ResolveAttack(attackerId, targetId, sourceId);
        }

        // === T-INP-03: Skill-based AOE cast ===

        /// <summary>
        /// T-INP-03: RPC для active skill'а с AOE (или single-target) формулой.
        /// Сервер резолвит SO через <see cref="SkillsWorld.GetSkillById(string)"/>, читает AOE параметры,
        /// собирает IDamageTarget через <see cref="TargetingService.CollectAoeTargets"/>, для каждой считает
        /// damage и применяет. Аналогично <see cref="ResolveAttack"/> но multi-target.
        /// </summary>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void RequestSkillCastRpc(string skillId, ulong primaryTargetId, ulong sourceId, RpcParams rpcParams = default)
        {
            ulong attackerId = rpcParams.Receive.SenderClientId;
            if (!RateLimit(attackerId)) return;
            ResolveSkillCast(attackerId, skillId, primaryTargetId, sourceId, null);
        }

        // Phase T2: Thrown/grenade variant — accepts impact point for AOE origin.
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void RequestSkillCastAtPointRpc(string skillId, Vector3 targetPoint, ulong sourceId, RpcParams rpcParams = default)
        {
            ulong attackerId = rpcParams.Receive.SenderClientId;
            if (!RateLimit(attackerId)) return;
            ResolveSkillCast(attackerId, skillId, 0UL, sourceId, targetPoint);
        }

        // === Server-side damage flow ===

        public void ResolveAttack(ulong attackerId, ulong targetId, ulong sourceId)
        {
            if (!_attackers.TryGetValue(attackerId, out var attacker))
            {
                if (_debugLog) Debug.LogWarning($"[CombatServer] ResolveAttack: attacker {attackerId} not registered.");
                return;
            }
            if (!_targets.TryGetValue(targetId, out var target))
            {
                if (_debugLog) Debug.LogWarning($"[CombatServer] ResolveAttack: target {targetId} not registered.");
                return;
            }
            if (!target.IsAlive())
            {
                SendErrorToClient(attackerId, "AlreadyDead");
                return;
            }
            if (!attacker.IsAlive())
            {
                if (_debugLog) Debug.LogWarning($"[CombatServer] ResolveAttack: attacker {attackerId} not alive.");
                return;
            }

            var source = attacker.GetDamageSource(sourceId);
            if (source == null)
            {
                SendErrorToClient(attackerId, "InvalidSource");
                return;
            }

            float now = Time.unscaledTime;
            if (!attacker.CanAttack(source, now))
            {
                SendErrorToClient(attackerId, "OnCooldown");
                return;
            }

            // Range policy: melee if range < 3м, else ranged
            IRangePolicy rangePolicy = source.GetRange() < 3.0f
                ? (IRangePolicy)new MeleeRangePolicy()
                : new RangedRangePolicy();

            if (!rangePolicy.IsInRange(attacker, target, source))
            {
                SendErrorToClient(attackerId, "OutOfRange");
                return;
            }

            // === Calculate damage (server rolls dice) ===
            var result = DamageCalculator.Calculate(attacker, target, source, rangePolicy);

            // Set cooldown
            attacker.SetCooldown(source, now + source.GetCooldownSeconds());

            // === Damage log (verify) ===
            if (_debugLog)
            {
                if (result.isHit)
                {
                    Debug.Log($"[DamageCalculator] attacker={attackerId} → target={targetId}, source={source.GetDisplayName()}: baseAttack={result.baseAttack}, hitChance={result.hitChance:F2}, isHit=True, isCrit={result.isCrit}, preDefense={result.preDefenseDamage}, defense={result.effectiveDefense}, final={result.finalDamage}, type={result.damageType}");
                }
                else
                {
                    Debug.Log($"[DamageCalculator] attacker={attackerId} → target={targetId}, source={source.GetDisplayName()}: MISS (hitChance={result.hitChance:F2})");
                }
            }

            // === Apply damage (server-side authoritative) ===
            if (result.isHit) target.ApplyDamage(result, attackerId);

            // === Broadcast (multicast) ===
            var dto = DamageResultDto.FromResult(result);
            var rpcParams = new RpcParams { Send = new RpcSendParams { Target = RpcTarget.Everyone } };
            AttackLandedTargetRpc(dto, rpcParams);

            // === World events ===
            WorldEventBus.Publish(new AttackLandedEvent { PlayerId = attackerId, Result = result });
            if (result.isHit) WorldEventBus.Publish(new DamageDealtEvent { PlayerId = attackerId, Result = result });
            if (result.isHit && !target.IsAlive()) WorldEventBus.Publish(new EntityKilledEvent { PlayerId = attackerId, Result = result });

            // === Server → client notification for death (separate RPC, simpler) ===
            if (result.isHit && !target.IsAlive())
            {
                EntityKilledTargetRpc(dto, rpcParams);
            }
        }

        // === Server → Client TargetRPCs (multicast) ===

        [Rpc(SendTo.SpecifiedInParams)]
        public void AttackLandedTargetRpc(DamageResultDto dto, RpcParams rpcParams)
        {
            var state = CombatClientState.Instance;
            if (state != null) state.HandleAttackLanded(dto);
        }

        [Rpc(SendTo.SpecifiedInParams)]
        public void EntityKilledTargetRpc(DamageResultDto dto, RpcParams rpcParams)
        {
            var state = CombatClientState.Instance;
            if (state != null) state.HandleEntityKilled(dto);
        }

        [Rpc(SendTo.SpecifiedInParams)]
        public void AttackErrorTargetRpc(ulong clientId, string code, RpcParams rpcParams)
        {
            var state = CombatClientState.Instance;
            if (state != null) state.HandleError(code);
        }

        // === Helpers ===

        private bool RateLimit(ulong clientId)
        {
            float now = Time.unscaledTime;
            if (_nextAllowedTime.TryGetValue(clientId, out float next) && now < next)
            {
                return false;
            }
            _nextAllowedTime[clientId] = now + RATE_LIMIT_INTERVAL;
            return true;
        }

        private void SendErrorToClient(ulong clientId, string code)
        {
            var rpcParams = new RpcParams { Send = new RpcSendParams { Target = RpcTarget.Single(clientId, RpcTargetUse.Temp) } };
            AttackErrorTargetRpc(clientId, code, rpcParams);
            if (_debugLog) Debug.Log($"[CombatServer] AttackError → client={clientId}: {code}");
        }

        // === T-INP-03: Server-side skill cast resolution ===

        /// <summary>
        /// Резолвит skillId → SkillNodeConfig, собирает AOE цели, для каждой считает damage.
        /// Поведение максимально близко к <see cref="ResolveAttack"/> — но без primary target и с multi-hit.
        /// </summary>
        public void ResolveSkillCast(ulong attackerId, string skillId, ulong primaryTargetId, ulong sourceId, Vector3? targetPoint = null)
        {
            if (string.IsNullOrEmpty(skillId))
            {
                SendErrorToClient(attackerId, "EmptySkillId");
                return;
            }

            if (!_attackers.TryGetValue(attackerId, out var attacker))
            {
                if (_debugLog) Debug.LogWarning($"[CombatServer] ResolveSkillCast: attacker {attackerId} not registered.");
                return;
            }

            if (!attacker.IsAlive())
            {
                if (_debugLog) Debug.LogWarning($"[CombatServer] ResolveSkillCast: attacker {attackerId} not alive.");
                return;
            }

            // Resolve SkillNodeConfig server-side (SkillsWorld — authoritative source of truth)
            var skillsWorld = ProjectC.Skills.SkillsWorld.Instance;
            if (skillsWorld == null)
            {
                SendErrorToClient(attackerId, "SkillsWorldMissing");
                return;
            }
            if (!skillsWorld.TryGetSkill(skillId, out var skillConfig) || skillConfig == null)
            {
                if (_debugLog) Debug.LogWarning($"[CombatServer] ResolveSkillCast: skillId '{skillId}' not found.");
                SendErrorToClient(attackerId, "UnknownSkill");
                return;
            }
            if (!skillConfig.isActive)
            {
                SendErrorToClient(attackerId, "SkillNotActive");
                return;
            }

            var source = attacker.GetDamageSource(sourceId);
            if (source == null)
            {
                SendErrorToClient(attackerId, "InvalidSource");
                return;
            }

            float now = Time.unscaledTime;
            if (!attacker.CanAttack(source, now))
            {
                SendErrorToClient(attackerId, "OnCooldown");
                return;
            }

            // === AOE origin + direction ===
            // Phase T2: если targetPoint задан (grenade throw), AOE от точки броска.
            // Иначе от атакующего (melee/self-cast).
            bool useTargetPoint = targetPoint.HasValue && targetPoint.Value.sqrMagnitude > 0.01f;
            Vector3 aoeOrigin;
            if (useTargetPoint)
            {
                aoeOrigin = targetPoint.Value;
            }
            else
            {
                // Prefer attacker's transform.forward (PlayerAttacker/NpcAttacker). Fallback на Vector3.forward.
            aoeOrigin = attacker.GetPosition() + Vector3.up * 1.2f;  // chest height
            }
            Vector3 forward = Vector3.forward;
            if (attacker is MonoBehaviour mb && mb.transform != null)
            {
                forward = mb.transform.forward;
            }

            // === Collect targets via AOE formula ===
            var results = new System.Collections.Generic.List<ProjectC.Combat.Core.IDamageTarget>();
            var hitPoints = new System.Collections.Generic.List<Vector3>();

            // SingleTarget → use legacy raycast against primaryTargetId's position OR TryGetTarget
            if (skillConfig.aoeFormula == ProjectC.Skills.AoeFormula.SingleTarget)
            {
                if (primaryTargetId != 0 && _targets.TryGetValue(primaryTargetId, out var primary))
                {
                    results.Add(primary);
                    hitPoints.Add(primary.GetPosition());
                }
                else if (!ProjectC.Combat.Core.TargetingService.TryGetTarget(
                    aoeOrigin, forward, ProjectC.Combat.Core.TargetingService.DefaultMaxDistance,
                    ProjectC.Combat.Core.TargetingService.DefaultMask,
                    out var hit, out var hp))
                {
                    if (_debugLog) Debug.Log($"[CombatServer] ResolveSkillCast: no target in range.");
                    return;
                }
                else
                {
                    results.Add(hit);
                    hitPoints.Add(hp);
                }
            }
            else
            {
                ProjectC.Combat.Core.TargetingService.CollectAoeTargets(
                    aoeOrigin, forward,
                    skillConfig.aoeFormula, skillConfig.aoeSize, skillConfig.aoeConeAngleDeg, skillConfig.aoeWidth,
                    ProjectC.Combat.Core.TargetingService.DefaultMaxDistance,
                    ProjectC.Combat.Core.TargetingService.DefaultMask,
                    results, hitPoints);
            }

            if (results.Count == 0)
            {
                if (_debugLog) Debug.Log($"[CombatServer] ResolveSkillCast: no targets in AOE for skill '{skillId}'.");
                return;
            }

            // === Set cooldown ONCE (per cast, не per hit) ===
            attacker.SetCooldown(source, now + source.GetCooldownSeconds());

            // === Apply damage to each ===
            int hitsLanded = 0;
            for (int i = 0; i < results.Count; i++)
            {
                var target = results[i];
                if (target == null) continue;
                if (!target.IsAlive()) continue;

                // Range policy per target.
                // Phase T2: для thrown навыков (useTargetPoint) пропускаем attacker-distance check —
                // цели уже отфильтрованы AOE-радиусом от targetPoint.
                if (!useTargetPoint)
                {
                    IRangePolicy rangePolicy = source.GetRange() < 3.0f
                        ? (IRangePolicy)new MeleeRangePolicy()
                        : new RangedRangePolicy();
                    if (!rangePolicy.IsInRange(attacker, target, source)) continue;
                }
                IRangePolicy rangePolicyForCalc = source.GetRange() < 3.0f
                    ? (IRangePolicy)new MeleeRangePolicy()
                    : new RangedRangePolicy();

                var result = DamageCalculator.Calculate(attacker, target, source, rangePolicyForCalc);

                if (_debugLog)
                {
                    if (result.isHit)
                    {
                        Debug.Log($"[DamageCalculator/AOE] attacker={attackerId} → target={target.GetTargetId()}, skill='{skillId}', source={source.GetDisplayName()}: preDefense={result.preDefenseDamage}, final={result.finalDamage}, isCrit={result.isCrit}");
                    }
                }

                if (result.isHit)
                {
                    target.ApplyDamage(result, attackerId);
                    hitsLanded++;
                }

                // Broadcast per-target
                var dto = DamageResultDto.FromResult(result);
                var rpcParams = new RpcParams { Send = new RpcSendParams { Target = RpcTarget.Everyone } };
                AttackLandedTargetRpc(dto, rpcParams);

                // Events per-target
                WorldEventBus.Publish(new AttackLandedEvent { PlayerId = attackerId, Result = result });
                if (result.isHit) WorldEventBus.Publish(new DamageDealtEvent { PlayerId = attackerId, Result = result });
                if (result.isHit && !target.IsAlive())
                {
                    WorldEventBus.Publish(new EntityKilledEvent { PlayerId = attackerId, Result = result });
                    EntityKilledTargetRpc(dto, rpcParams);
                }
            }

            if (_debugLog)
            {
                Debug.Log($"[CombatServer] ResolveSkillCast: skill='{skillId}' formula={skillConfig.aoeFormula} targets={results.Count} hits={hitsLanded} attacker={attackerId}");
            }
        }
    }
}
