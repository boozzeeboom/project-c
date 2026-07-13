// Project C: Health System — T-HP01
// HealthConfig: настройки здоровья персонажа (базовое HP + STR-множитель).
// Используется StatsServer для вычисления max HP игрока.
// Design: итерация по запросу пользователя (HP = baseHp + STR_flat * strToHpMultiplier).

using UnityEngine;

namespace ProjectC.Stats
{
    [CreateAssetMenu(fileName = "HealthConfig", menuName = "Project C/Stats/Health Config", order = 12)]
    public class HealthConfig : ScriptableObject
    {
        [Header("Base HP")]
        [Tooltip("Базовое HP персонажа до учёта STR.")]
        [SerializeField, Min(1f)] private float _baseHp = 100f;

        [Header("STR Scaling")]
        [Tooltip("Множитель: HP += STR_flat_value × multiplier. STR_flat = tier * 5 + 10.")]
        [SerializeField, Min(0f)] private float _strToHpMultiplier = 10f;

        [Header("Respawn")]
        [Tooltip("Процент восстановления HP при респавне после смерти (0.0 - 1.0).")]
        [SerializeField, Range(0f, 1f)] private float _respawnHpPercent = 0.3f;

        // === Public read-only API ===

        public float BaseHp => _baseHp;
        public float StrToHpMultiplier => _strToHpMultiplier;
        public float RespawnHpPercent => _respawnHpPercent;

        /// <summary>
        /// Вычислить максимальное HP по STR flat value.
        /// STR_flat = tier * 5 + 10 (формула из PlayerStats.StatsToFlat).
        /// </summary>
        public int ComputeMaxHp(int strFlatValue)
        {
            return Mathf.Max(1, Mathf.RoundToInt(_baseHp + strFlatValue * _strToHpMultiplier));
        }

        /// <summary>
        /// Вычислить HP после респавна (процент от максимума).
        /// </summary>
        public int ComputeRespawnHp(int maxHp)
        {
            return Mathf.Max(1, Mathf.RoundToInt(maxHp * _respawnHpPercent));
        }
    }
}
