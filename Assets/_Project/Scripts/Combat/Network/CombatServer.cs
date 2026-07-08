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
using ProjectC.Items;

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

            RecoverExistingEntities();

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

        private void RecoverExistingEntities()
        {
            var playerAttackers = FindObjectsByType<PlayerAttacker>();
            foreach (var pa in playerAttackers)
            {
                if (pa == null) continue;
                ulong id = pa.ClientId;
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
                ulong id = pt.ClientId;
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
                if (id == 0) continue;
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
                if (id == 0) continue;
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

        public void UnregisterAttacker(ulong id) { _attackers.Remove(id); }
        public void UnregisterTarget(ulong id) { _targets.Remove(id); }

        public bool IsCooldownReady(ulong attackerId, ulong sourceId, float now)
        {
            return !_cooldowns.TryGetValue((attackerId, sourceId), out float ready) || now >= ready;
        }

        public void SetCooldown(ulong attackerId, ulong sourceId, float until)
        {
            _cooldowns[(attackerId, sourceId)] = until;
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void RequestAttackRpc(ulong targetId, ulong sourceId, RpcParams rpcParams = default)
        {
            ulong attackerId = rpcParams.Receive.SenderClientId;
            if (!RateLimit(attackerId)) return;
            ResolveAttack(attackerId, targetId, sourceId);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void RequestSkillCastRpc(string skillId, ulong primaryTargetId, ulong sourceId, RpcParams rpcParams = default)
        {
            ulong attackerId = rpcParams.Receive.SenderClientId;
            if (!RateLimit(attackerId)) return;
            ResolveSkillCast(attackerId, skillId, primaryTargetId, sourceId, null);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void RequestSkillCastAtPointRpc(string skillId, Vector3 targetPoint, ulong sourceId, RpcParams rpcParams = default)
        {
            ulong attackerId = rpcParams.Receive.SenderClientId;
            if (!RateLimit(attackerId)) return;
            ResolveSkillCast(attackerId, skillId, 0UL, sourceId, targetPoint);
        }

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
            if (!target.IsAlive()) { SendErrorToClient(attackerId, "AlreadyDead"); return; }
            if (!attacker.IsAlive()) return;

            var source = attacker.GetDamageSource(sourceId);
            if (source == null) { SendErrorToClient(attackerId, "InvalidSource"); return; }

            float now = Time.unscaledTime;
            if (!attacker.CanAttack(source, now)) { SendErrorToClient(attackerId, "OnCooldown"); return; }

            // T-LOCK-01: Obstruction check для unarmed/melee (ResolveAttack).
            // Серверный raycast от атакующего к цели. Если другой IDamageTarget на пути —
            // перенаправляем урон в него. Если стена — miss.
            {
                Vector3 attackerPos = attacker.GetPosition() + Vector3.up * 1.2f;
                Vector3 toTarget = target.GetPosition() - attackerPos;
                float tDist = toTarget.magnitude;
                if (tDist > 0.01f && Physics.Raycast(attackerPos, toTarget.normalized,
                    out RaycastHit obsHit, tDist, ~0, QueryTriggerInteraction.Ignore))
                {
                    var obstruction = obsHit.collider.GetComponentInParent<IDamageTarget>();
                    if (obstruction != null && obstruction.GetTargetId() != targetId)
                    {
                        Debug.Log($"[CombatServer/Obstruction] ResolveAttack: target={target.GetDisplayName()} blocked by {obstruction.GetDisplayName()}, redirecting damage");
                        target = obstruction;
                        targetId = target.GetTargetId();
                    }
                    else if (obstruction == null)
                    {
                        Debug.Log($"[CombatServer/Obstruction] ResolveAttack: target={target.GetDisplayName()} blocked by non-damageable ({obsHit.collider.name}), MISS");
                        SendErrorToClient(attackerId, "LineOfSightBlocked");
                        return;
                    }
                    // else: obstruction == target — OK.
                }
            }

            IRangePolicy rangePolicy = source.GetRange() < 3.0f
                ? (IRangePolicy)new MeleeRangePolicy()
                : new RangedRangePolicy();

            if (!rangePolicy.IsInRange(attacker, target, source))
            {
                SendErrorToClient(attackerId, "OutOfRange");
                return;
            }

            var result = DamageCalculator.Calculate(attacker, target, source, rangePolicy);
            attacker.SetCooldown(source, now + source.GetCooldownSeconds());

            if (_debugLog)
            {
                if (result.isHit)
                    Debug.Log($"[DamageCalculator] attacker={attackerId} → target={targetId}, source={source.GetDisplayName()}: baseAttack={result.baseAttack}, hitChance={result.hitChance:F2}, isHit=True, isCrit={result.isCrit}, preDefense={result.preDefenseDamage}, defense={result.effectiveDefense}, final={result.finalDamage}, type={result.damageType}");
                else
                    Debug.Log($"[DamageCalculator] attacker={attackerId} → target={targetId}, source={source.GetDisplayName()}: MISS (hitChance={result.hitChance:F2})");
            }

            if (result.isHit) target.ApplyDamage(result, attackerId);

            var dto = DamageResultDto.FromResult(result);
            var rpcParams = new RpcParams { Send = new RpcSendParams { Target = RpcTarget.Everyone } };
            AttackLandedTargetRpc(dto, rpcParams);

            WorldEventBus.Publish(new AttackLandedEvent { PlayerId = attackerId, Result = result });
            if (result.isHit) WorldEventBus.Publish(new DamageDealtEvent { PlayerId = attackerId, Result = result });
            if (result.isHit && !target.IsAlive())
            {
                WorldEventBus.Publish(new EntityKilledEvent { PlayerId = attackerId, Result = result });
                EntityKilledTargetRpc(dto, rpcParams);
            }
        }

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

        private bool RateLimit(ulong clientId)
        {
            float now = Time.unscaledTime;
            if (_nextAllowedTime.TryGetValue(clientId, out float next) && now < next) return false;
            _nextAllowedTime[clientId] = now + RATE_LIMIT_INTERVAL;
            return true;
        }

        private void SendErrorToClient(ulong clientId, string code)
        {
            var rpcParams = new RpcParams { Send = new RpcSendParams { Target = RpcTarget.Single(clientId, RpcTargetUse.Temp) } };
            AttackErrorTargetRpc(clientId, code, rpcParams);
            if (_debugLog) Debug.Log($"[CombatServer] AttackError → client={clientId}: {code}");
        }

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
            
            // R5: If source not found but sourceId looks like an equipped weapon (non-zero, non-thrown),
            // try rebuilding PlayerAttacker sources — RebuildSources may have run before equipment was loaded.
            if (source == null && sourceId != 0)
            {
                if (attacker is PlayerAttacker pa)
                {
                    pa.RebuildSources();
                    source = attacker.GetDamageSource(sourceId);
                    if (source != null && _debugLog)
                        Debug.Log($"[CombatServer/R5] RebuildSources recovered: sourceId={sourceId} → {source.GetDisplayName()}");
                }
            }
            
            // R4: for thrown skills, resolve damage source from inventory (throwables are not equipped)
            bool useTargetPoint = targetPoint.HasValue && targetPoint.Value.sqrMagnitude > 0.01f;
            if (useTargetPoint && (source == null || source.GetDisplayName() == "Unarmed"))
            {
                source = ResolveThrowableSourceFromInventory(attackerId);
            }
            
            if (source == null)
            {
                Debug.LogWarning($"[CombatServer] ResolveSkillCast: InvalidSource — sourceId={sourceId} not found in attacker's active sources (count={attacker.GetActiveDamageSources().Count}). skill='{skillId}'");
                SendErrorToClient(attackerId, "InvalidSource");
                return;
            }

            float now = Time.unscaledTime;
            if (!attacker.CanAttack(source, now))
            {
                SendErrorToClient(attackerId, "OnCooldown");
                return;
            }

            // R4: consume throwable(s) BEFORE target collection
            if (useTargetPoint)
            {
                int consumeCount = Mathf.Max(1, skillConfig.throwCount);
                ConsumeThrowableFromInventory(attackerId, consumeCount);
            }

            Vector3 aoeOrigin;
            if (useTargetPoint) { aoeOrigin = targetPoint.Value; }
            else { aoeOrigin = attacker.GetPosition() + Vector3.up * 1.2f; }

            Vector3 forward = Vector3.forward;
            if (attacker is MonoBehaviour mb && mb.transform != null) { forward = mb.transform.forward; }

            Debug.Log($"[CombatServer] ResolveSkillCast: skill='{skillId}' aoeOrigin={aoeOrigin} aoeSize={skillConfig.aoeSize} useTargetPoint={useTargetPoint} targetsInRegistry={_targets.Count}");

            var results = new System.Collections.Generic.List<ProjectC.Combat.Core.IDamageTarget>();
            var hitPoints = new System.Collections.Generic.List<Vector3>();

            // R5: For ranged single-target skills, log the resolved source for debugging.
            bool isRangedSingleTarget = skillConfig.discipline == ProjectC.Skills.CombatDiscipline.Ranged
                && skillConfig.subtype != ProjectC.Skills.CombatSubtype.Throwables
                && skillConfig.aoeFormula == ProjectC.Skills.AoeFormula.SingleTarget;

            if (isRangedSingleTarget)
            {
                Debug.Log($"[CombatServer/R5] Ranged single-target: skill='{skillId}' sourceId={sourceId} source={source?.GetDisplayName() ?? "NULL"} sourceType={source?.GetDamageType().ToString() ?? "N/A"} sourceRange={source?.GetRange() ?? 0f:F1}m");
            }

            if (skillConfig.aoeFormula == ProjectC.Skills.AoeFormula.SingleTarget)
            {
                // T-LOCK-01: Obstruction check for locked/primary target.
                // Server-side raycast: attacker → preferredTarget.
                // If obstruction = another IDamageTarget → redirect damage.
                // If obstruction = non-damageable (wall) → miss.
                if (primaryTargetId != 0 && _targets.TryGetValue(primaryTargetId, out var preferredTarget))
                {
                    Vector3 attackerPos = attacker.GetPosition() + Vector3.up * 1.2f;
                    Vector3 toPreferred = preferredTarget.GetPosition() - attackerPos;
                    float dist = toPreferred.magnitude;

                    if (dist > 0.01f && Physics.Raycast(attackerPos, toPreferred.normalized,
                        out RaycastHit obstructionHit, dist, ~0, QueryTriggerInteraction.Ignore))
                    {
                        var obstruction = obstructionHit.collider.GetComponentInParent<ProjectC.Combat.Core.IDamageTarget>();
                        if (obstruction != null && obstruction.GetTargetId() != primaryTargetId)
                        {
                            // Obstruction is another damageable — redirect damage to it.
                            Debug.Log($"[CombatServer/Obstruction] skill='{skillId}': preferred={preferredTarget.GetDisplayName()} blocked by {obstruction.GetDisplayName()}, redirecting damage");
                            results.Add(obstruction);
                            hitPoints.Add(obstructionHit.point);
                        }
                        else if (obstruction == null)
                        {
                            // Raycast hit a wall/terrain — shot blocked, miss.
                            Debug.Log($"[CombatServer/Obstruction] skill='{skillId}': preferred={preferredTarget.GetDisplayName()} blocked by non-damageable ({obstructionHit.collider.name}), MISS");
                            SendErrorToClient(attackerId, "LineOfSightBlocked");
                            return;
                        }
                        else
                        {
                            // obstruction == preferredTarget — raycast hit the target directly, OK.
                            results.Add(preferredTarget);
                            hitPoints.Add(preferredTarget.GetPosition());
                        }
                    }
                    else
                    {
                        // No obstruction — raycast missed everything (or dist too small), target is clear.
                        results.Add(preferredTarget);
                        hitPoints.Add(preferredTarget.GetPosition());
                    }
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
                CollectAoeTargetsFromRegistry(aoeOrigin, forward,
                    skillConfig.aoeFormula, skillConfig.aoeSize, skillConfig.aoeConeAngleDeg, skillConfig.aoeWidth,
                    results, hitPoints);
            }

            if (results.Count == 0)
            {
                string context = isRangedSingleTarget ? "NO target found (SingleTarget)" : "NO targets in AOE";
                Debug.LogWarning($"[CombatServer] ResolveSkillCast: {context}! skill='{skillId}' origin={aoeOrigin} size={skillConfig.aoeSize} registryTargets={_targets.Count}");
                return;
            }

            attacker.SetCooldown(source, now + source.GetCooldownSeconds());

            int hitsLanded = 0;
            for (int i = 0; i < results.Count; i++)
            {
                var target = results[i];
                if (target == null) continue;
                if (!target.IsAlive()) continue;

                if (!useTargetPoint)
                {
                    IRangePolicy rangePolicy = source.GetRange() < 3.0f
                        ? (IRangePolicy)new MeleeRangePolicy()
                        : new RangedRangePolicy();
                    if (!rangePolicy.IsInRange(attacker, target, source)) continue;
                }

                // T-LOCK-01: Per-target obstruction check for AOE skills.
                // Raycast from attacker to each target. If another IDamageTarget blocks → redirect.
                // If non-damageable blocks → skip this target.
                if (!useTargetPoint)
                {
                    Vector3 attackerPos = attacker.GetPosition() + Vector3.up * 1.2f;
                    Vector3 toTarget = target.GetPosition() - attackerPos;
                    float tDist = toTarget.magnitude;
                    if (tDist > 0.01f && Physics.Raycast(attackerPos, toTarget.normalized,
                        out RaycastHit aoeObsHit, tDist, ~0, QueryTriggerInteraction.Ignore))
                    {
                        var aoeObs = aoeObsHit.collider.GetComponentInParent<ProjectC.Combat.Core.IDamageTarget>();
                        if (aoeObs != null && aoeObs.GetTargetId() != target.GetTargetId())
                        {
                            // Redirect damage to obstruction instead.
                            Debug.Log($"[CombatServer/AOE-Obstruction] skill='{skillId}': target={target.GetDisplayName()} blocked by {aoeObs.GetDisplayName()}, redirecting");
                            target = aoeObs;
                        }
                        else if (aoeObs == null)
                        {
                            // Wall/terrain — skip this target entirely.
                            Debug.Log($"[CombatServer/AOE-Obstruction] skill='{skillId}': target={target.GetDisplayName()} blocked by non-damageable ({aoeObsHit.collider.name}), skipping");
                            continue;
                        }
                        // else: aoeObs == target — raycast hit the target, OK.
                    }
                }

                IRangePolicy rangePolicyForCalc;
                if (useTargetPoint) { rangePolicyForCalc = new AoeRangePolicy(); }
                else
                {
                    rangePolicyForCalc = source.GetRange() < 3.0f
                        ? (IRangePolicy)new MeleeRangePolicy()
                        : new RangedRangePolicy();
                }

                var result = DamageCalculator.Calculate(attacker, target, source, rangePolicyForCalc);

                // R5: Bows/Crossbows D100 hit/damage scaling.
                // Roll D100: if roll <= rangedHitChance → hit with roll% of base damage (1-100%).
                // If roll > rangedHitChance → miss.
                bool isRangedWeaponSkill = skillConfig != null
                    && (skillConfig.subtype == ProjectC.Skills.CombatSubtype.Bows
                        || skillConfig.subtype == ProjectC.Skills.CombatSubtype.Crossbows);
                if (isRangedWeaponSkill && result.isHit)
                {
                    int d100Roll = Random.Range(1, 101); // 1..100
                    float hitChance = Mathf.Clamp(skillConfig.rangedHitChance, 0f, 100f);
                    if (d100Roll <= hitChance)
                    {
                        float dmgPct = d100Roll / 100f; // 0.01..1.00
                        int unscaledPre = result.preDefenseDamage;
                        int unscaledFinal = result.finalDamage;
                        result.preDefenseDamage = Mathf.Max(1, (int)(unscaledPre * dmgPct));
                        result.finalDamage = Mathf.Max(1, (int)(unscaledFinal * dmgPct));
                        Debug.Log($"[CombatServer/R5-D100] skill='{skillId}' d100={d100Roll} <= hitChance={hitChance} → HIT, dmgPct={dmgPct:F2}, preDefense: {unscaledPre}→{result.preDefenseDamage}, final: {unscaledFinal}→{result.finalDamage}");
                    }
                    else
                    {
                        result.isHit = false;
                        result.finalDamage = 0;
                        Debug.Log($"[CombatServer/R5-D100] skill='{skillId}' d100={d100Roll} > hitChance={hitChance} → MISS");
                    }
                }

                Debug.Log($"[DamageCalculator/AOE] attacker={attackerId} → target={target.GetTargetId()} ({target.GetDisplayName()}), skill='{skillId}', source={source.GetDisplayName()}: isHit={result.isHit} preDefense={result.preDefenseDamage} final={result.finalDamage} isCrit={result.isCrit}");

                if (result.isHit)
                {
                    target.ApplyDamage(result, attackerId);
                    hitsLanded++;
                    Debug.Log($"[CombatServer] AOE damage applied: target={target.GetDisplayName()} hpAfter={target.GetCurrentHp()}/{target.GetMaxHp()}");
                }

                var dto = DamageResultDto.FromResult(result);
                var rpcParams = new RpcParams { Send = new RpcSendParams { Target = RpcTarget.Everyone } };
                AttackLandedTargetRpc(dto, rpcParams);

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

        private void CollectAoeTargetsFromRegistry(
            Vector3 origin, Vector3 forward,
            ProjectC.Skills.AoeFormula formula, float size, float coneAngleDeg, float width,
            System.Collections.Generic.List<ProjectC.Combat.Core.IDamageTarget> outResults,
            System.Collections.Generic.List<Vector3> outHitPoints)
        {
            var seen = new HashSet<ulong>();
            int totalChecked = 0, aliveCount = 0, inRangeCount = 0;
            foreach (var kvp in _targets)
            {
                var target = kvp.Value;
                if (target == null) continue;
                totalChecked++;
                if (!target.IsAlive()) continue;
                aliveCount++;

                Vector3 targetPos = target.GetPosition();
                Vector3 toTarget = targetPos - origin;
                float dist = toTarget.magnitude;

                bool inAoe = false;
                switch (formula)
                {
                    case ProjectC.Skills.AoeFormula.Sphere:
                        inAoe = dist <= size;
                        break;
                    case ProjectC.Skills.AoeFormula.Cone:
                        if (dist <= size && dist > 0.001f)
                        {
                            float dot = Vector3.Dot(toTarget.normalized, forward);
                            float cosHalfAngle = Mathf.Cos(coneAngleDeg * 0.5f * Mathf.Deg2Rad);
                            inAoe = dot >= cosHalfAngle;
                        }
                        break;
                    case ProjectC.Skills.AoeFormula.Line:
                    {
                        float halfW = Mathf.Max(0.1f, width * 0.5f);
                        Vector3 lineEnd = origin + forward * size;
                        Vector3 closest = ClosestPointOnSegment(origin, lineEnd, targetPos);
                        float lateralDist = Vector3.Distance(targetPos, closest);
                        inAoe = lateralDist <= halfW && dist <= size + halfW;
                        break;
                    }
                    case ProjectC.Skills.AoeFormula.Box:
                    {
                        float halfW = Mathf.Max(0.1f, width * 0.5f);
                        float halfL = size * 0.5f;
                        Vector3 center = origin + forward * halfL;
                        Vector3 local = Quaternion.Inverse(Quaternion.LookRotation(forward)) * (targetPos - center);
                        inAoe = Mathf.Abs(local.x) <= halfW && Mathf.Abs(local.y) <= halfW && Mathf.Abs(local.z) <= halfL;
                        break;
                    }
                }

                Debug.Log($"[CombatServer] AOE check: target='{target.GetDisplayName()}' pos={targetPos} dist={dist:F1}m inAoe={inAoe} (origin={origin}, size={size})");

                if (!inAoe) continue;
                inRangeCount++;

                ulong id = target.GetTargetId();
                if (!seen.Add(id)) continue;

                outResults.Add(target);
                outHitPoints.Add(targetPos);
            }

            Debug.Log($"[CombatServer] CollectAoeTargetsFromRegistry: formula={formula} size={size} totalTargets={totalChecked} alive={aliveCount} inRange={inRangeCount} uniqueFound={outResults.Count}");
        }

        private static Vector3 ClosestPointOnSegment(Vector3 a, Vector3 b, Vector3 point)
        {
            Vector3 ab = b - a;
            float t = Vector3.Dot(point - a, ab) / ab.sqrMagnitude;
            t = Mathf.Clamp01(t);
            return a + t * ab;
        }

        private IDamageSource ResolveThrowableSourceFromInventory(ulong attackerId)
        {
            var inv = ProjectC.Items.InventoryWorld.Instance;
            if (inv == null) { if (_debugLog) Debug.LogWarning("[CombatServer] ResolveThrowableSourceFromInventory: InventoryWorld.Instance is null."); return null; }

            var data = inv.GetOrCreate(attackerId);
            foreach (ItemType type in System.Enum.GetValues(typeof(ItemType)))
            {
                var ids = data.GetIdsForType(type);
                if (ids == null) continue;
                foreach (int itemId in ids)
                {
                    var def = inv.GetItemDefinition(itemId);
                    if (def is ProjectC.Equipment.WeaponItemData w && w.weaponClass == ProjectC.Equipment.WeaponClass.Throwable)
                    {
                        if (_debugLog) Debug.Log($"[CombatServer] ResolveThrowableSourceFromInventory: found {w.itemName} (id={itemId}, dmg={w.damageDice}+{w.baseDamage}, radius={w.explosionRadius}m)");
                        return new ProjectC.Combat.WeaponDamageSource(w, (ulong)itemId);
                    }
                }
            }
            if (_debugLog) Debug.LogWarning("[CombatServer] ResolveThrowableSourceFromInventory: no Throwable weapon found in player inventory.");
            return null;
        }

        private void ConsumeThrowableFromInventory(ulong attackerId, int count)
        {
            var inv = ProjectC.Items.InventoryWorld.Instance;
            if (inv == null) { if (_debugLog) Debug.LogWarning("[CombatServer] ConsumeThrowableFromInventory: InventoryWorld.Instance is null."); return; }

            int remaining = count;
            var data = inv.GetOrCreate(attackerId);
            foreach (ItemType type in System.Enum.GetValues(typeof(ItemType)))
            {
                var ids = data.GetIdsForType(type);
                if (ids == null) continue;
                foreach (int itemId in ids)
                {
                    var def = inv.GetItemDefinition(itemId);
                    if (def is ProjectC.Equipment.WeaponItemData w && w.weaponClass == ProjectC.Equipment.WeaponClass.Throwable)
                    {
                        int available = inv.CountOf(attackerId, itemId);
                        if (available <= 0) continue;
                        int toRemove = Mathf.Min(remaining, available);
                        var result = inv.RemoveItems(attackerId, itemId, def.itemType, toRemove);
                        if (_debugLog)
                            Debug.Log($"[CombatServer] ConsumeThrowableFromInventory: removed {toRemove}x {def.itemName} (id={itemId}) code={result.code} msg={result.message}");
                        remaining -= toRemove;
                        if (remaining <= 0)
                        {
                            if (ProjectC.Items.Network.InventoryServer.Instance != null)
                                ProjectC.Items.Network.InventoryServer.Instance.PushSnapshot(attackerId);
                            return;
                        }
                    }
                }
            }
            if (remaining < count && ProjectC.Items.Network.InventoryServer.Instance != null)
                ProjectC.Items.Network.InventoryServer.Instance.PushSnapshot(attackerId);
            if (_debugLog && remaining > 0)
                Debug.LogWarning($"[CombatServer] ConsumeThrowableFromInventory: could only consume {count - remaining}/{count} throwables.");
        }
    }
}