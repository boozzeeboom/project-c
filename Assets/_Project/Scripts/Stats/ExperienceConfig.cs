// Project C: Character Progression — T-P01 refactor (P4)
// ExperienceConfig: per-source XP values + tier growth formula + global multiplier.
// Вынесен из StatsConfig согласно аудиту 12_STATS_ARCHITECTURE_AUDIT_V2.md §4, Q2.6.
//
// Design: docs/Character/04_STATS_PROGRESSION.md §1-§2

using UnityEngine;

namespace ProjectC.Stats
{
    [CreateAssetMenu(fileName = "ExperienceConfig", menuName = "Project C/Stats/Experience Config", order = 11)]
    public class ExperienceConfig : ScriptableObject
    {
        [Header("Per-action multipliers (per-source XP gain)")]
        [Tooltip("XP за добычу 1 единицы ресурса.")]
        [SerializeField, Min(0f)] private float _miningXpPerItem = 1f;

        [Tooltip("XP за завершённый крафт.")]
        [SerializeField, Min(0f)] private float _craftingXpPerItem = 5f;

        [Tooltip("XP за операцию обмена (Pack/Unpack).")]
        [SerializeField, Min(0f)] private float _exchangeXpPerOp = 2f;

        [Tooltip("XP за покупку/продажу.")]
        [SerializeField, Min(0f)] private float _marketXpPerOp = 1f;

        [Tooltip("XP за принятый квест.")]
        [SerializeField, Min(0f)] private float _questAcceptedXp = 3f;

        [Tooltip("XP за завершённый квест.")]
        [SerializeField, Min(0f)] private float _questCompletedXp = 10f;

        [Tooltip("XP за уникальный dialog.")]
        [SerializeField, Min(0f)] private float _dialogXpPerVisit = 1f;

        [Tooltip("XP за прыжок.")]
        [SerializeField, Min(0f)] private float _jumpXp = 0.5f;

        [Tooltip("XP за 1 метр пешей ходьбы.")]
        [SerializeField, Min(0f)] private float _walkXpPerMeter = 1f;

        [Tooltip("XP за 1 метр пилотирования.")]
        [SerializeField, Min(0f)] private float _pilotXpPerMeter = 1f;

        [Header("Global multiplier")]
        [Tooltip("Применяется ко ВСЕМ XP gains. 1.0 = норма.")]
        [SerializeField, Min(0f)] private float _globalMultiplier = 1f;

        [Header("Формула роста (геометрическая, без капа)")]
        [SerializeField, Min(1f)] private float _tierBaseXp = 100f;

        [Tooltip("Множитель роста между тирами.")]
        [SerializeField, Range(1.01f, 3.0f)] private float _tierGrowthRate = 1.5f;

        // === Public read-only API ===

        public float MiningXpPerItem      => _miningXpPerItem;
        public float CraftingXpPerItem    => _craftingXpPerItem;
        public float ExchangeXpPerOp      => _exchangeXpPerOp;
        public float MarketXpPerOp        => _marketXpPerOp;
        public float QuestAcceptedXp      => _questAcceptedXp;
        public float QuestCompletedXp     => _questCompletedXp;
        public float DialogXpPerVisit     => _dialogXpPerVisit;
        public float JumpXp               => _jumpXp;
        public float WalkXpPerMeter       => _walkXpPerMeter;
        public float PilotXpPerMeter      => _pilotXpPerMeter;

        public float GlobalMultiplier    => _globalMultiplier;
        public float TierBaseXp          => _tierBaseXp;
        public float TierGrowthRate      => _tierGrowthRate;

        /// <summary>XP required для перехода с текущего tier на следующий.</summary>
        public float XpForNextTier(int currentTier)
        {
            if (currentTier < 0) return _tierBaseXp;
            return _tierBaseXp * Mathf.Pow(_tierGrowthRate, currentTier);
        }

        public float ApplyGlobalMultiplier(float xp) => xp * _globalMultiplier;

        /// <summary>Базовое количество XP за единицу источника (без global multiplier).</summary>
        public float GetBaseXp(XpSource source) => source switch
        {
            XpSource.Mining         => _miningXpPerItem,
            XpSource.Crafting       => _craftingXpPerItem,
            XpSource.Exchange       => _exchangeXpPerOp,
            XpSource.Market         => _marketXpPerOp,
            XpSource.QuestAccepted  => _questAcceptedXp,
            XpSource.QuestCompleted => _questCompletedXp,
            XpSource.Dialog         => _dialogXpPerVisit,
            XpSource.Jump           => _jumpXp,
            XpSource.Walk           => _walkXpPerMeter,
            XpSource.Pilot          => _pilotXpPerMeter,
            _                       => 0f,
        };
    }
}
