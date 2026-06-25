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
            Debug.Log("[CombatServer] OnNetworkSpawn: Instance set, IsServer=True.");

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
            var playerAttackers = FindObjectsByType<PlayerAttacker>(FindObjectsSortMode.None);
            foreach (var pa in playerAttackers)
            {
                if (pa == null) continue;
                ulong id = pa.ClientId;  // 0 = host player, ВАЛИДНО
                if (!_attackers.ContainsKey(id))
                {
                    _attackers[id] = pa;
                    if (Debug.isDebugBuild) Debug.Log($"[CombatServer] RecoverExistingEntities: registered PlayerAttacker id={id}");
                }
            }

            var playerTargets = FindObjectsByType<PlayerTarget>(FindObjectsSortMode.None);
            foreach (var pt in playerTargets)
            {
                if (pt == null) continue;
                ulong id = pt.ClientId;  // 0 = host player, ВАЛИДНО
                if (!_targets.ContainsKey(id))
                {
                    _targets[id] = pt;
                    if (Debug.isDebugBuild) Debug.Log($"[CombatServer] RecoverExistingEntities: registered PlayerTarget id={id}");
                }
            }

            var npcAttackers = FindObjectsByType<NpcAttacker>(FindObjectsSortMode.None);
            foreach (var na in npcAttackers)
            {
                if (na == null) continue;
                ulong id = na.GetAttackerId();
                if (id == 0) continue;  // для NPC id==0 = не инициализирован
                if (!_attackers.ContainsKey(id))
                {
                    _attackers[id] = na;
                    if (Debug.isDebugBuild) Debug.Log($"[CombatServer] RecoverExistingEntities: registered NpcAttacker id={id}");
                }
            }

            var npcTargets = FindObjectsByType<NpcTarget>(FindObjectsSortMode.None);
            foreach (var nt in npcTargets)
            {
                if (nt == null) continue;
                ulong id = nt.GetTargetId();
                if (id == 0) continue;  // для NPC id==0 = не инициализирован
                if (!_targets.ContainsKey(id))
                {
                    _targets[id] = nt;
                    if (Debug.isDebugBuild) Debug.Log($"[CombatServer] RecoverExistingEntities: registered NpcTarget id={id}");
                }
            }

            if (Debug.isDebugBuild)
            {
                Debug.Log($"[CombatServer] RecoverExistingEntities done: attackers={_attackers.Count}, targets={_targets.Count}");
            }
        }

        // === Registration (called by PlayerAttacker/NpcAttacker at spawn) ===

        public void RegisterAttacker(ulong id, IAttacker attacker)
        {
            if (attacker == null) return;
            _attackers[id] = attacker;
            if (Debug.isDebugBuild) Debug.Log($"[CombatServer] Registered attacker id={id} ({attacker.GetType().Name})");
        }

        public void RegisterTarget(ulong id, IDamageTarget target)
        {
            if (target == null) return;
            _targets[id] = target;
            if (Debug.isDebugBuild) Debug.Log($"[CombatServer] Registered target id={id} ({target.GetType().Name})");
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

        [Rpc(SendTo.Server, RequireOwnership = true)]
        public void RequestAttackRpc(ulong targetId, ulong sourceId, RpcParams rpcParams = default)
        {
            ulong attackerId = rpcParams.Receive.SenderClientId;
            if (!RateLimit(attackerId)) return;
            ResolveAttack(attackerId, targetId, sourceId);
        }

        // === Server-side damage flow ===

        public void ResolveAttack(ulong attackerId, ulong targetId, ulong sourceId)
        {
            if (!_attackers.TryGetValue(attackerId, out var attacker))
            {
                if (Debug.isDebugBuild) Debug.LogWarning($"[CombatServer] ResolveAttack: attacker {attackerId} not registered.");
                return;
            }
            if (!_targets.TryGetValue(targetId, out var target))
            {
                if (Debug.isDebugBuild) Debug.LogWarning($"[CombatServer] ResolveAttack: target {targetId} not registered.");
                return;
            }
            if (!target.IsAlive())
            {
                SendErrorToClient(attackerId, "AlreadyDead");
                return;
            }
            if (!attacker.IsAlive())
            {
                if (Debug.isDebugBuild) Debug.LogWarning($"[CombatServer] ResolveAttack: attacker {attackerId} not alive.");
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
            if (Debug.isDebugBuild)
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
            if (Debug.isDebugBuild) Debug.Log($"[CombatServer] AttackError → client={clientId}: {code}");
        }
    }
}
