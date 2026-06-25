// Project C: Real-Time Combat Engine — T-RTC07
// CombatClientState: singleton MonoBehaviour, клиентский event-bus для combat UI/notifications.
// Design: docs/Character/Skills/real-time-combat/10_DESIGN.md §6, 20_TECHNICAL.md §2.2.
//
// Создаётся в NetworkManagerController.Awake() → CreateCombatClientState() (root GO, DontDestroyOnLoad).
// Server broadcast DamageResultDto → AttackLandedTargetRpc → CombatClientState.HandleAttackLanded.

using System;
using UnityEngine;
using ProjectC.Combat.Core;
using ProjectC.Combat.Network;

namespace ProjectC.Combat.Client
{
    public class CombatClientState : MonoBehaviour
    {
        public static CombatClientState Instance { get; private set; }

        /// <summary>Стреляет при получении AttackLandedTargetRpc (hit или miss). Полезно для UI / SFX.</summary>
        public event Action<DamageResult> OnAttackLanded;
        /// <summary>Стреляет только при result.isHit (урон реально нанесён).</summary>
        public event Action<DamageResult> OnDamageDealt;
        /// <summary>Стреляет при result.isHit && target HP → 0.</summary>
        public event Action<DamageResult> OnEntityKilled;
        /// <summary>OutOfRange / OnCooldown / AlreadyDead error.</summary>
        public event Action<string> OnAttackError;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[CombatClientState] Duplicate instance, destroying.");
                Destroy(this);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Debug.Log("[CombatClientState] Awake: singleton created.");
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // === Server → Client handlers ===

        public void HandleAttackLanded(DamageResultDto dto)
        {
            var result = ToResult(dto);
            OnAttackLanded?.Invoke(result);

            if (Debug.isDebugBuild)
            {
                if (result.isHit)
                {
                    Debug.Log($"[CombatClientState] AttackLanded: attacker={result.attackerId} → target={result.targetId}, dmg={result.finalDamage}, crit={result.isCrit}, type={result.damageType}");
                }
                else
                {
                    Debug.Log($"[CombatClientState] AttackMissed: attacker={result.attackerId} → target={result.targetId}, hitChance={result.hitChance:F2}");
                }
            }

            if (result.isHit) OnDamageDealt?.Invoke(result);
        }

        public void HandleEntityKilled(DamageResultDto dto)
        {
            var result = ToResult(dto);
            OnEntityKilled?.Invoke(result);
            if (Debug.isDebugBuild)
            {
                Debug.Log($"[CombatClientState] EntityKilled: target={result.targetId}, finalDmg={result.finalDamage}");
            }
        }

        public void HandleError(string code)
        {
            OnAttackError?.Invoke(code);
            if (Debug.isDebugBuild) Debug.Log($"[CombatClientState] AttackError: {code}");
        }

        private static DamageResult ToResult(DamageResultDto dto)
        {
            return new DamageResult
            {
                baseAttack = dto.baseAttack,
                locMult = dto.locMult,
                critMult = dto.critMult,
                skillMult = dto.skillMult,
                hitChance = dto.hitChance,
                preDefenseDamage = dto.preDefenseDamage,
                effectiveDefense = dto.effectiveDefense,
                finalDamage = dto.finalDamage,
                isCrit = dto.isCrit,
                isHit = dto.isHit,
                hitLocation = dto.hitLocation,
                damageType = (DamageType)dto.damageType,
                attackerId = dto.attackerId,
                targetId = dto.targetId,
                sourceId = dto.sourceId,
                attackerPosition = dto.attackerPosition,
                targetPosition = dto.targetPosition,
            };
        }
    }
}
