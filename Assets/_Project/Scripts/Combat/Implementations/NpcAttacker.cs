// Project C: Real-Time Combat Engine — T-RTC03
// NpcAttacker: реализация IAttacker для NPC-врага. NetworkBehaviour (был MonoBehaviour —
// изменено v0.1 для self-register в CombatServer через OnNetworkSpawn).
// Design: docs/Character/Skills/real-time-combat/10_DESIGN.md §3.3.
//
// Cooldown — per-NPC (внутри компонента), не централизованный: NPC-враги малочисленны
// и не конкурируют за ресурсы cooldown-таблицы.
//
// MVP: NpcAttacker оперирует дефолтным оружием из NpcCombatData (d6, base=2, range=2м).
// После T-CB03 — NpcAttacker может иметь массив weapons (IDamageSource[]).

using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using ProjectC.Combat.Core;

namespace ProjectC.Combat
{
    public class NpcAttacker : NetworkBehaviour, IAttacker
    {
        [SerializeField] private NpcCombatData _data;
        [Tooltip("Stable id для RPC. Default = GetInstanceID. Переопредели если NPC спавнится динамически.")]
        [SerializeField] private ulong _attackerIdOverride = 0;

        /// <summary>Public accessor для AI/subsystem access (T-NPC-01: NpcBrain читает cooldown/data).</summary>
        public NpcCombatData Data => _data;

        private IDamageSource _defaultSource;
        private float _lastAttackTime = float.NegativeInfinity;
        private int _currentHpCache;  // mirror NpcTarget._currentHp для IsAlive()

        public NpcTarget Target { get; set; }  // wired в NpcAttacker.Initialize или через инспектор

        public ulong GetAttackerId() => _attackerIdOverride != 0 ? _attackerIdOverride : (ulong)GetInstanceID();

        public void Initialize(NpcCombatData data, NpcTarget target)
        {
            _data = data;
            Target = target;
            _defaultSource = new NpcDefaultDamageSource(this);
        }

        /// <summary>
        /// T-RTC06 (v0.1 fix): Self-register в CombatServer при NetworkSpawn (server-side only).
        /// </summary>
        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (!IsServer) return;
            if (CombatServer.Instance != null)
            {
                CombatServer.Instance.RegisterAttacker(GetAttackerId(), this);
            }
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            if (!IsServer) return;
            if (CombatServer.Instance != null)
            {
                CombatServer.Instance.UnregisterAttacker(GetAttackerId());
            }
        }

        public Vector3 GetPosition() => transform.position;
        public int GetStrength() => _data != null ? _data.strength : 10;
        public int GetDexterity() => _data != null ? _data.dexterity : 10;
        public int GetIntelligence() => _data != null ? _data.intelligence : 10;

        public IReadOnlyList<IDamageSource> GetActiveDamageSources() =>
            _defaultSource != null ? new IDamageSource[] { _defaultSource } : System.Array.Empty<IDamageSource>();

        public IDamageSource GetDamageSource(ulong sourceId) =>
            _defaultSource != null && _defaultSource.GetSourceId() == sourceId ? _defaultSource : null;

        public bool IsAlive() => Target != null && Target.IsAlive();
        public bool IsPlayer() => false;

        public bool CanAttack(IDamageSource source, float now) =>
            now >= _lastAttackTime + (_data != null ? _data.cooldownSeconds : 1.5f);

        public void SetCooldown(IDamageSource source, float until) => _lastAttackTime = until;

        /// <summary>NpcDefaultDamageSource — wraps NpcCombatData параметры.</summary>
        private sealed class NpcDefaultDamageSource : IDamageSource
        {
            private readonly NpcAttacker _attacker;
            public NpcDefaultDamageSource(NpcAttacker attacker) { _attacker = attacker; }
            public ulong GetSourceId() => _attacker.GetAttackerId();
            public DamageType GetDamageType() => _attacker._data != null ? _attacker._data.damageType : DamageType.Physical;
            public DamageDice GetDamageDice() => _attacker._data != null ? _attacker._data.damageDice : DamageDice.d6;
            public int GetBaseDamage() => _attacker._data != null ? _attacker._data.baseDamage : 2;
            public int GetCritModifier() => _attacker._data != null ? _attacker._data.critModifier : 0;
            public float GetRange() => _attacker._data != null ? _attacker._data.range : 2.0f;
            public float GetCooldownSeconds() => _attacker._data != null ? _attacker._data.cooldownSeconds : 1.5f;
            public float GetSkillMultiplier(ulong attackerId) => 1.0f;
            public string GetDisplayName() => _attacker._data != null ? _attacker._data.displayName : "NPC";
        }
    }
}
