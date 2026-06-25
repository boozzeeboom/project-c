// Project C: Real-Time Combat Engine — T-RTC09
// CombatConfig: SO с настройками баланса. Server-authoritative defaults.
// Design: docs/Character/Skills/real-time-combat/10_DESIGN.md §7.
//
// MVP: hardcoded defaults в DamageCalculator/MeleeRangePolicy. Этот SO —
// designer-tunable overrides (пока не подключены в MVP-1, но asset создаём
// в Resources/ для будущего).

using UnityEngine;

namespace ProjectC.Combat.Config
{
    [CreateAssetMenu(fileName = "CombatConfig_Default", menuName = "Project C/Combat/Combat Config")]
    public class CombatConfig : ScriptableObject
    {
        [Header("Hit chance")]
        [Range(0f, 1f)] public float baseMeleeHitChance = 0.85f;
        [Range(0f, 1f)] public float baseRangedHitChance = 0.75f;
        [Range(0f, 0.1f)] public float dexHitMultiplier = 0.015f;  // per answer 2.1: (DEX-10)*0.015

        [Header("Crit")]
        [Range(1, 200)] public int baseCritThreshold = 100;
        [Range(1f, 5f)] public float critMultiplier = 2.0f;

        [Header("Defense multipliers by damage type")]
        [Range(0f, 1f)] public float antigravArmorMult = 0.5f;
        [Range(0f, 1f)] public float explosiveArmorMult = 0.7f;
        [Range(0f, 1f)] public float mesiumArmorMult = 0.0f;

        [Header("Cooldown (seconds, fallback if IDamageSource не задаёт)")]
        [Range(0.1f, 5f)] public float baseMeleeCooldown = 1.0f;
        [Range(0.5f, 5f)] public float baseRangedCooldown = 1.5f;

        [Header("Network")]
        [Range(10, 60)] public int serverTickRate = 30;

        [Header("UI (Phase 2)")]
        public bool showDamageNumbers = true;
        public bool showHitFlash = true;
        public float damageNumberDuration = 1.5f;

        /// <summary>Singleton accessor для CombatServer. Загружается из Resources/Combat/CombatConfig_Default.asset.</summary>
        public static CombatConfig Instance
        {
            get
            {
                if (_instance != null) return _instance;
                _instance = Resources.Load<CombatConfig>("Combat/CombatConfig_Default");
                if (_instance == null)
                {
                    Debug.LogWarning("[CombatConfig] CombatConfig_Default.asset not found in Resources/Combat/. Using hardcoded defaults.");
                }
                return _instance;
            }
        }
        private static CombatConfig _instance;
    }
}
