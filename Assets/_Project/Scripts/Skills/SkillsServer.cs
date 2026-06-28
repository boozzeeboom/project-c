// Project C: Character Progression — T-P13
// SkillsServer: NetworkBehaviour RPC hub. Scene-placed в BootstrapScene.
// Design: docs/Character/06_SKILL_TREE.md §2, docs/Character/08_ROADMAP.md T-P13
//
// Pattern: копия EquipmentServer (T-P09) + QuestServer rate limit.
//
// RPCs (client→server):
//   - RequestLearnSkillRpc(string skillId)   — TryLearnSkill
//   - RequestForgetSkillRpc(string skillId)  — TryForgetSkill (Q3.4 free respec)
//
// Server→client (TargetRPCs через NetworkPlayer):
//   - ReceiveSkillResultTargetRpc(SkillResultDto result)
//   - ReceiveSkillsSnapshotTargetRpc(SkillsSnapshotDto snapshot)
// Stub: используем reflection для null-safe call (пока NMC auto-spawn добавит реальный RPC — работает через reflection)
//
// Cross-NetworkObject deps:
//   - SkillsWorld (T-P12, same script)         — primary
//   - StatsServer.Instance (T-P05)              — RecomputeAndSendSnapshot после learn (T-P09 hook уже есть)
//   - EquipmentWorld (T-P09)                   — null-safe (skill requirement check через reflection — T-P09 уже)

using System;
using System.Collections.Generic;
using ProjectC.Player;
using ProjectC.Skills.Dto;
using Unity.Netcode;
using UnityEngine;

namespace ProjectC.Skills
{
    public class SkillsServer : NetworkBehaviour
    {
        public static SkillsServer Instance { get; private set; }

        [Header("Config")]
        [Tooltip("SkillsConfig.asset: defaultSkills (empty Q3.2), MaxOpsPerSec, Resources path. " +
                 "Загружается в OnNetworkSpawn через Resources.Load.")]
        [SerializeField] private SkillsConfig _config;

        [Header("Rate limit (T-P13)")]
        [Tooltip("Защита от спама RequestLearnSkillRpc/RequestForgetSkillRpc. Per-client.")]
        private readonly Dictionary<ulong, float> _nextAllowedTimePerClient = new Dictionary<ulong, float>();

        private SkillsWorld _world;
        private int _maxOpsPerSec = 5;

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[SkillsServer] Duplicate instance detected, replacing.");
            }
            Instance = this;

            if (_config == null)
            {
                _config = Resources.Load<SkillsConfig>("Skills/SkillsConfig_Default");
                if (_config == null)
                {
                    Debug.LogWarning("[SkillsServer] SkillsConfig not assigned and Resources/Skills/SkillsConfig_Default.asset not found. " +
                                     "Skills will not load until config is wired up. (see T-P12)");
                }
            }
            if (_config != null) _maxOpsPerSec = _config.MaxOpsPerSec;

            // SkillsWorld singleton (server-only)
            _world = new SkillsWorld();
            if (_config != null) _world.LoadAllSkills(_config);

            if (Debug.isDebugBuild)
            {
                Debug.Log($"[SkillsServer] OnNetworkSpawn — IsServer=true, skillsLoaded={_world?.SkillCount ?? 0}, maxOps={_maxOpsPerSec}/sec");
            }
        }

        public override void OnNetworkDespawn()
        {
            if (!IsServer) return;
            SkillsWorld.Reset();
            if (Instance == this) Instance = null;
        }

        // === Rate limit ===

        private bool RateLimit(ulong clientId)
        {
            float now = Time.unscaledTime;
            if (_nextAllowedTimePerClient.TryGetValue(clientId, out var next) && now < next)
            {
                if (Debug.isDebugBuild)
                {
                    Debug.Log($"[SkillsServer] Rate limit hit for client {clientId}, retry in {next - now:F2}s");
                }
                return false;
            }
            _nextAllowedTimePerClient[clientId] = now + 1f / _maxOpsPerSec;
            return true;
        }

        // === Client → Server RPCs ===

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void RequestLearnSkillRpc(string skillId, RpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;
            if (!RateLimit(clientId))
            {
                SendSkillResult(clientId, SkillResultDto.Denied(skillId, "Слишком быстро"));
                return;
            }

            if (!_world.TryLearnSkill(clientId, skillId, out var reason))
            {
                SendSkillResult(clientId, SkillResultDto.Denied(skillId, reason));
                return;
            }

            // T-CB07: получить узел для ApplySkillEffects
            var learnedSkill = _world.TryGetSkill(skillId, out var s) ? s : null;
            SendSkillResult(clientId, SkillResultDto.Learned(skillId));
            ApplySkillEffects(clientId, learnedSkill, isLearn: true);
            TriggerEquipmentRecheck(clientId);
            // Recompute effective stats (T-P09 already calls this on equip/unequip)
            TriggerStatsRecompute(clientId);
            SendSnapshotToOwner(clientId);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void RequestForgetSkillRpc(string skillId, RpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;
            if (!RateLimit(clientId))
            {
                SendSkillResult(clientId, SkillResultDto.Denied(skillId, "Слишком быстро"));
                return;
            }

            if (!_world.TryForgetSkill(clientId, skillId, out var reason))
            {
                SendSkillResult(clientId, SkillResultDto.Denied(skillId, reason));
                return;
            }

            // T-CB07: получить узел для ApplySkillEffects (откат)
            var forgottenSkill = _world.TryGetSkill(skillId, out var s2) ? s2 : null;
            SendSkillResult(clientId, SkillResultDto.Forgotten(skillId));
            ApplySkillEffects(clientId, forgottenSkill, isLearn: false);
            TriggerEquipmentRecheck(clientId);
            TriggerStatsRecompute(clientId);
            SendSnapshotToOwner(clientId);
        }

        // === Server → Client (TargetRPCs через NetworkPlayer) ===

        private void SendSkillResult(ulong clientId, SkillResultDto result)
        {
            if (NetworkManager.Singleton == null) return;
            if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client)) return;
            var netPlayer = client.PlayerObject?.GetComponent<NetworkPlayer>();
            if (netPlayer == null) return;

            // Stub-RPC: NMC auto-spawn + actual RPC. Ищем через reflection (null-safe).
            var mi = typeof(NetworkPlayer).GetMethod("ReceiveSkillResultTargetRpc");
            if (mi != null)
            {
                var defaultRpcParams = System.Activator.CreateInstance(typeof(RpcParams));
                mi.Invoke(netPlayer, new object[] { result, defaultRpcParams });
            }
            else if (Debug.isDebugBuild)
            {
                Debug.Log($"[SkillsServer] (T-P13 stub) skill result for {clientId}: code={result.code} skill='{result.skillId}' reason='{result.reason}'");
            }
        }

        private void SendSnapshotToOwner(ulong clientId)
        {
            if (NetworkManager.Singleton == null) return;
            if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client)) return;
            var netPlayer = client.PlayerObject?.GetComponent<NetworkPlayer>();
            if (netPlayer == null) return;

            var snap = new SkillsSnapshotDto
            {
                learnedSkillIds = new List<string>(_world.GetLearnedSkillIds(clientId)).ToArray(),
            };

            var mi = typeof(NetworkPlayer).GetMethod("ReceiveSkillsSnapshotTargetRpc");
            if (mi != null)
            {
                var defaultRpcParams = System.Activator.CreateInstance(typeof(RpcParams));
                mi.Invoke(netPlayer, new object[] { snap, defaultRpcParams });
            }
            else if (Debug.isDebugBuild)
            {
                Debug.Log($"[SkillsServer] (T-P13 stub) skills snapshot for {clientId}: {snap.learnedSkillIds.Length} learned");
            }
        }

        /// <summary>
        /// Cross-NetworkObject: после learn/forget — recompute effective stats.
        /// Null-safe (T-P05 уже добавил RecomputeAndSendSnapshot, T-P09 уже подключил).
        /// </summary>
        private void TriggerStatsRecompute(ulong clientId)
        {
            var ssType = System.Type.GetType("ProjectC.Stats.StatsServer, Assembly-CSharp");
            if (ssType == null) return;
            var instProp = ssType.GetProperty("Instance");
            var inst = instProp?.GetValue(null);
            if (inst == null) return;
            var method = ssType.GetMethod("RecomputeAndSendSnapshot");
            method?.Invoke(inst, new object[] { clientId });
        }

        // === T-CB07: ApplySkillEffects + EquipmentRecheck ===

        /// <summary>
        /// T-CB07: обработчик effects при learn/forget навыка.
        /// StatMod уже обрабатывается StatsServer.RecomputeAndSendSnapshot.
        /// Для новых типов (WeaponProficiencyUnlock и т.п.) — пока log + stub.
        /// Phase 2: реализовать unlock в реестрах (WeaponClassCatalog и т.п.).
        /// </summary>
        private void ApplySkillEffects(ulong clientId, SkillNodeConfig skill, bool isLearn)
        {
            if (skill == null) return;
            if (skill.effects == null || skill.effects.Length == 0) return;

            foreach (var effect in skill.effects)
            {
                // SkillEffect — struct, не может быть null. Проверяем default.
                if (effect.type == SkillEffect.Type.StatMod && effect.floatValue == 0 && effect.multiplier == 0 && string.IsNullOrEmpty(effect.stringParam)) continue;
                ApplySingleEffect(clientId, skill, effect, isLearn);
            }
        }

        private void ApplySingleEffect(ulong clientId, SkillNodeConfig skill, SkillEffect effect, bool isLearn)
        {
            string verb = isLearn ? "Learned" : "Forgot";

            // Новые типы (Phase 2 — реальная логика unlock)
            switch (effect.type)
            {
                case SkillEffect.Type.WeaponProficiencyUnlock:
                case SkillEffect.Type.ArmorProficiencyUnlock:
                case SkillEffect.Type.WeaponTechniqueUnlock:
                case SkillEffect.Type.ExplosiveRecipeUnlock:
                case SkillEffect.Type.AntigravTechniqueUnlock:
                    if (Debug.isDebugBuild)
                    {
                        Debug.Log($"[SkillsServer/T-CB07] {verb} {effect.type} from skill='{skill.skillId}' for client={clientId} (Phase 2 will implement unlock registry)");
                    }
                    break;

                // StatMod / Damage / Heal — обрабатываются StatsServer.RecomputeAndSendSnapshot
                default:
                    if (Debug.isDebugBuild)
                    {
                        Debug.Log($"[SkillsServer/T-CB07] {verb} stat-affecting effect {effect.type} from skill='{skill.skillId}' for client={clientId} (StatsServer will recompute)");
                    }
                    break;
            }
        }

        /// <summary>
        /// T-CB07: после learn/forget — recheck надетых предметов.
        /// Phase 2: force-unequip оружия/брони, если required skill больше не learned.
        /// Сейчас — no-op (TryEquip уже имеет hard gate, повторный equip заблокируется).
        /// </summary>
        private void TriggerEquipmentRecheck(ulong clientId)
        {
            var ewType = System.Type.GetType("ProjectC.Equipment.EquipmentWorld, Assembly-CSharp");
            if (ewType == null) return;
            var instProp = ewType.GetProperty("Instance");
            var inst = instProp?.GetValue(null);
            if (inst == null) return;

            if (Debug.isDebugBuild)
            {
                Debug.Log($"[SkillsServer/T-CB07] Equipment recheck triggered for client={clientId} (Phase 2 will add force-unequip)");
            }
        }
    }
}
