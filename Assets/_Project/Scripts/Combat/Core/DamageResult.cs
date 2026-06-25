// Project C: Real-Time Combat Engine — T-RTC01
// DamageResult struct (POCO, server-side authoritative).
// Design: docs/Character/Skills/real-time-combat/10_DESIGN.md §2.5,
//         docs/Character/Skills/Battle/10_DESIGN.md §7.1.
//
// Server считает DamageResult → broadcast через DamageResultDto (T-RTC08) →
// client получает, конвертирует обратно в DamageResult → UI / logs / analytics.
// НЕ сериализуется напрямую через NGO (это DTO-ответственность).

using UnityEngine;

namespace ProjectC.Combat.Core
{
    /// <summary>
    /// Полная информация о результате одной атаки (hit или miss).
    /// Server-authoritative, immutable per-attack. UI/logs читают поля напрямую.
    /// </summary>
    /// <remarks>
    /// В real-time <c>locMult = 1.0</c> и <c>hitLocation = 1</c> (Torso default)
    /// per answer 2.17 — hit location отключён в real-time, активен только в turn-based.
    /// </remarks>
    public struct DamageResult
    {
        // === Raw damage ===
        public int baseAttack;          // 1dN + base + STR
        public float locMult;            // 1.0 в real-time (отключён per 2.17)
        public float critMult;           // 2.0 при crit, иначе 1.0
        public float skillMult;          // от навыков, opt-in, БЕЗ cap (per 2.18)
        public float hitChance;          // 0..1 (до броска)

        // === Defense / final ===
        public int preDefenseDamage;     // round(base * loc * crit * skill)
        public int effectiveDefense;     // round(armor * typeMult)
        public int finalDamage;          // max(0, preDefense - defense)

        // === Flags ===
        public bool isCrit;              // (1d100 + critMod) >= 100
        public bool isHit;               // random < hitChance
        public byte hitLocation;         // 1=default (Torso) в real-time

        // === Identity ===
        public DamageType damageType;
        public ulong attackerId;         // 0 = NPC attacker
        public ulong targetId;           // 0 = NPC target
        public ulong sourceId;           // id IDamageSource
        public Vector3 attackerPosition;
        public Vector3 targetPosition;

        /// <summary>Helper для miss-результата (server rolls, но не попал).</summary>
        public static DamageResult Miss(
            float hitChance,
            DamageType damageType,
            ulong attackerId,
            ulong targetId,
            ulong sourceId,
            Vector3 attackerPos,
            Vector3 targetPos)
        {
            return new DamageResult
            {
                isHit = false,
                hitChance = hitChance,
                damageType = damageType,
                attackerId = attackerId,
                targetId = targetId,
                sourceId = sourceId,
                attackerPosition = attackerPos,
                targetPosition = targetPos,
            };
        }
    }
}
