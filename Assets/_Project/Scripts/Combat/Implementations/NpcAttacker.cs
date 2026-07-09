// Project C: Real-Time Combat Engine — T-RTC03 / T-NPC-SKILL-02
// NpcAttacker: реализация IAttacker для NPC-врага. NetworkBehaviour.
// Design: docs/Character/Skills/real-time-combat/10_DESIGN.md §3.3.
//
// v0.1: один NpcDefaultDamageSource из NpcCombatData.
// v0.2: multi-source из NpcSkillSet (SkillNodeConfig + NpcSkillOverride).
//       Backward-compat: если _skillSet == null → fallback на старый NpcDefaultDamageSource.
//
// Cooldown — per-source (словарь), не глобальный: позволяет разным скилам иметь
// независимые кулдауны.

using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using ProjectC.Combat.Core;
using ProjectC.Skills;
using ProjectC.AI;

namespace ProjectC.Combat
{
    public class NpcAttacker : NetworkBehaviour, IAttacker
    {
        [SerializeField] private NpcCombatData _data;
        [Tooltip("Stable id для RPC. Default = GetInstanceID. Переопредели если NPC спавнится динамически.")]
        [SerializeField] private ulong _attackerIdOverride = 0;

        [Header("Skill Set (T-NPC-SKILL-02)")]
        [Tooltip("Набор скилов из SkillNodeConfig с оверрайдами. Если null — используется NpcDefaultDamageSource из NpcCombatData.")]
        [SerializeField] private NpcSkillSet _skillSet;

        /// <summary>Public accessor для AI/subsystem access.</summary>
        public NpcCombatData Data => _data;
        public NpcSkillSet SkillSet => _skillSet;

        private IDamageSource _defaultSource;
        private NpcSkillDamageSource[] _skillSources;
        private Dictionary<ulong, float> _sourceCooldowns; // sourceId → cooldownUntil
        private int _currentHpCache;

        public NpcTarget Target { get; set; }

        public ulong GetAttackerId() => _attackerIdOverride != 0 ? _attackerIdOverride : EntityId.ToULong(GetEntityId());

        public void Initialize(NpcCombatData data, NpcTarget target)
        {
            _data = data;
            Target = target;
            BuildDamageSources();
        }

        /// <summary>
        /// T-NPC-SKILL-02: назначить NpcSkillSet через спавнер (оверрайдит префаб).
        /// Вызывать ДО OnNetworkSpawn / Spawn.
        /// </summary>
        public void SetSkillSet(NpcSkillSet skillSet)
        {
            _skillSet = skillSet;
            BuildDamageSources();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (!IsServer) return;
            BuildDamageSources();
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

        /// <summary>
        /// Все активные источники урона: скилы из NpcSkillSet + fallback default.
        /// sourceId = 0 → NpcDefaultDamageSource (fallback)
        /// sourceId = 1..N → NpcSkillDamageSource по индексу в _skillSources
        /// </summary>
        public IReadOnlyList<IDamageSource> GetActiveDamageSources()
        {
            var list = new List<IDamageSource>();
            if (_skillSources != null)
            {
                for (int i = 0; i < _skillSources.Length; i++)
                    if (_skillSources[i] != null)
                        list.Add(_skillSources[i]);
            }
            // Fallback всегда доступен
            EnsureDefaultSource();
            if (_defaultSource != null)
                list.Add(_defaultSource);
            return list;
        }

        public IDamageSource GetDamageSource(ulong sourceId)
        {
            // sourceId = 0 → fallback default source
            if (sourceId == 0)
            {
                EnsureDefaultSource();
                return _defaultSource;
            }

            // sourceId = 1..N → skill source
            if (_skillSources != null)
            {
                int index = (int)(sourceId - 1);
                if (index >= 0 && index < _skillSources.Length && _skillSources[index] != null)
                    return _skillSources[index];
            }

            // try default as last resort
            EnsureDefaultSource();
            return _defaultSource != null && _defaultSource.GetSourceId() == sourceId ? _defaultSource : null;
        }

        public bool IsAlive() => Target != null && Target.IsAlive();

        private void EnsureDefaultSource()
        {
            if (_defaultSource != null) return;
            if (_data == null) _data = GetComponent<NpcCombatData>();
            _defaultSource = new NpcDefaultDamageSource(this);
        }

        public bool IsPlayer() => false;

        public bool CanAttack(IDamageSource source, float now)
        {
            if (_sourceCooldowns == null) _sourceCooldowns = new Dictionary<ulong, float>();
            ulong sid = source.GetSourceId();
            if (_sourceCooldowns.TryGetValue(sid, out float until))
                return now >= until;
            return true;
        }

        public void SetCooldown(IDamageSource source, float until)
        {
            if (_sourceCooldowns == null) _sourceCooldowns = new Dictionary<ulong, float>();
            _sourceCooldowns[source.GetSourceId()] = until;
        }

        // ============================================================
        // Build sources
        // ============================================================

        private void BuildDamageSources()
        {
            // Всегда пересоздаём _defaultSource
            _defaultSource = null;

            // Build skill sources from NpcSkillSet
            if (_skillSet != null && _skillSet.skills != null && _skillSet.skills.Length > 0)
            {
                var validOverrides = new List<NpcSkillOverride>();
                for (int i = 0; i < _skillSet.skills.Length; i++)
                {
                    if (_skillSet.skills[i].IsValid)
                        validOverrides.Add(_skillSet.skills[i]);
                }

                _skillSources = new NpcSkillDamageSource[validOverrides.Count];
                for (int i = 0; i < validOverrides.Count; i++)
                {
                    // sourceId = index + 1 (0 зарезервирован под default)
                    _skillSources[i] = new NpcSkillDamageSource(this, validOverrides[i], (ulong)(i + 1));
                }
            }
            else
            {
                _skillSources = null;
            }
        }

        // ============================================================
        // NpcDefaultDamageSource — fallback (backward compat)
        // ============================================================

        /// <summary>NpcDefaultDamageSource — wraps NpcCombatData параметры.</summary>
        private sealed class NpcDefaultDamageSource : IDamageSource
        {
            private readonly NpcAttacker _attacker;
            public NpcDefaultDamageSource(NpcAttacker attacker) { _attacker = attacker; }
            public ulong GetSourceId() => 0; // fallback source
            public DamageType GetDamageType() => _attacker._data != null ? _attacker._data.damageType : DamageType.Physical;
            public DamageDice GetDamageDice() => _attacker._data != null ? _attacker._data.damageDice : DamageDice.d6;
            public int GetBaseDamage() => _attacker._data != null ? _attacker._data.baseDamage : 2;
            public int GetCritModifier() => _attacker._data != null ? _attacker._data.critModifier : 0;
            public float GetRange() => _attacker._data != null ? _attacker._data.range : 2.0f;
            public float GetCooldownSeconds() => _attacker._data != null ? _attacker._data.cooldownSeconds : 1.5f;
            public float GetSkillMultiplier(ulong attackerId) => 1.0f;
            public string GetDisplayName() => _attacker._data != null ? _attacker._data.displayName : "NPC";
        }

        // ============================================================
        // NpcSkillDamageSource — skill-based damage source (T-NPC-SKILL-02)
        // ============================================================

        /// <summary>
        /// IDamageSource, параметры которого читаются из SkillNodeConfig с опциональными оверрайдами из NpcSkillOverride.
        /// Если значение оверрайда = 0/null — используется значение из SkillNodeConfig.
        /// </summary>
        public sealed class NpcSkillDamageSource : IDamageSource
        {
            private readonly NpcAttacker _attacker;
            private readonly NpcSkillOverride _override;
            private readonly ulong _sourceId;

            public NpcSkillDamageSource(NpcAttacker attacker, NpcSkillOverride skillOverride, ulong sourceId)
            {
                _attacker = attacker;
                _override = skillOverride;
                _sourceId = sourceId;
            }

            private SkillNodeConfig Skill => _override.skillConfig;

            public ulong GetSourceId() => _sourceId;

            public DamageType GetDamageType()
            {
                // DamageType сейчас НЕ в SkillNodeConfig напрямую — используем NpcCombatData
                return _attacker._data != null ? _attacker._data.damageType : DamageType.Physical;
            }

            public DamageDice GetDamageDice()
            {
                if (_override.overrideDamageDice != DamageDice.d4) // d4 — минимальное значение в enum
                    return _override.overrideDamageDice;
                // Fallback: используем из NpcCombatData (SkillNodeConfig не хранит damageDice)
                return _attacker._data != null ? _attacker._data.damageDice : DamageDice.d6;
            }

            public int GetBaseDamage()
            {
                if (_override.overrideBaseDamage > 0)
                    return _override.overrideBaseDamage;
                return _attacker._data != null ? _attacker._data.baseDamage : 2;
            }

            public int GetCritModifier()
            {
                return _attacker._data != null ? _attacker._data.critModifier : 0;
            }

            public float GetRange()
            {
                if (_override.overrideRange > 0f)
                    return _override.overrideRange;
                return _attacker._data != null ? _attacker._data.range : 2.0f;
            }

            public float GetCooldownSeconds()
            {
                if (_override.overrideCooldown > 0f)
                    return _override.overrideCooldown;
                if (Skill != null)
                    return Skill.cooldownSeconds;
                return _attacker._data != null ? _attacker._data.cooldownSeconds : 1.5f;
            }

            public float GetSkillMultiplier(ulong attackerId) => 1.0f;

            public string GetDisplayName()
            {
                if (Skill != null && !string.IsNullOrEmpty(Skill.displayName))
                    return Skill.displayName;
                return _attacker._data != null ? _attacker._data.displayName : "NPC";
            }

            /// <summary>AnimationClip для этого скилла (с учётом оверрайда).</summary>
            public AnimationClip GetAnimationClip()
            {
                if (_override.overrideAnimation != null)
                    return _override.overrideAnimation;
                if (Skill != null)
                    return Skill.attackClip;
                return null;
            }

            /// <summary>Скорость анимации (с учётом оверрайда).</summary>
            public float GetAnimationSpeed()
            {
                if (_override.overrideAnimationSpeed > 0f)
                    return _override.overrideAnimationSpeed;
                if (Skill != null)
                    return Skill.attackClipSpeed;
                return 1.0f;
            }

            /// <summary>SkillNodeConfig для доступа к AOE/дисциплине/подтипу.</summary>
            public SkillNodeConfig GetSkillConfig() => Skill;

            /// <summary>Доступен ли этот скилл при текущем % HP (фильтр minHpPercent/maxHpPercent).</summary>
            public bool IsHpAvailable(float hpPercent)
            {
                return hpPercent >= _override.minHpPercent && hpPercent <= _override.maxHpPercent;
            }
        }

        // ============================================================
        // Public skill access API (для NpcBrain)
        // ============================================================

        /// <summary>Кол-во доступных skill-based источников (без учёта fallback default).</summary>
        public int SkillSourceCount => _skillSources != null ? _skillSources.Length : 0;

        /// <summary>
        /// Получить NpcSkillDamageSource по индексу (0..SkillSourceCount-1).
        /// Возвращает null если индекс вне диапазона или _skillSet == null.
        /// </summary>
        public NpcSkillDamageSource GetSkillSource(int index)
        {
            if (_skillSources == null || index < 0 || index >= _skillSources.Length)
                return null;
            return _skillSources[index];
        }
    }
}
