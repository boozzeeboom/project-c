// Project C: Character Progression — T-P01
// StatsConfig ScriptableObject: источники XP, per-source multipliers, формула роста, глобальный множитель.
// HARDCODED mapping source→stat (см. XpSource.GetStatFor switch).
//
// Дизайн: docs/Character/03_DATA_MODEL.md §1, docs/Character/04_STATS_PROGRESSION.md §1-§2.
// Решения пользователя (CHANGELOG.md 2026-06-14):
//   Q1.3 — globalMultiplier без upper bound.
//   Q1.4 — dialog XP = unique-event (НЕ cooldown), реализация в T-P05 (StatsServer).
//   Q1.5 — walk XP per 1m, +track total walked для ачивок.
//   Q10.3 — убраны per-stat mapping (mining→STR hardcoded).
//   Q10.5 — _debugLogging toggle в инспекторе.
//
// Не делаем в T-P01: runtime использование полей (всё в T-P05 StatsServer).
// Verify: создать .asset через меню, инспектор показывает все поля, 0 compile errors.

using UnityEngine;

namespace ProjectC.Stats
{
    // T-P01 stub-forward-declare: T-P02 (PlayerStats.cs) объявит настоящий enum StatType с теми же значениями.
    // Нужен прямо сейчас, чтобы GetStatFor() компилировался. После T-P02 stub удалится,
    // настоящий enum придёт через `using ProjectC.Stats` (тот же namespace).
    public enum StatType : byte
    {
        Strength     = 0,
        Dexterity    = 1,
        Intelligence = 2,
    }

    [CreateAssetMenu(fileName = "StatsConfig", menuName = "Project C/Stats/Stats Config", order = 10)]
    public class StatsConfig : ScriptableObject
    {
        [Header("Per-action multipliers (per-source XP gain)")]
        [Tooltip("XP за добычу 1 единицы ресурса. 0.1 = +0.1 STR за каждый item")]
        [SerializeField, Min(0f)] private float _miningXpPerItem = 1f;

        [Tooltip("XP за завершённый крафт. +N INT за каждый craft")]
        [SerializeField, Min(0f)] private float _craftingXpPerItem = 5f;

        [Tooltip("XP за операцию обмена (Pack/Unpack). +N INT за op")]
        [SerializeField, Min(0f)] private float _exchangeXpPerOp = 2f;

        [Tooltip("XP за покупку/продажу. +N INT за op")]
        [SerializeField, Min(0f)] private float _marketXpPerOp = 1f;

        [Tooltip("XP за принятый квест. +N INT за quest")]
        [SerializeField, Min(0f)] private float _questAcceptedXp = 3f;

        [Tooltip("XP за завершённый квест. +N INT за quest")]
        [SerializeField, Min(0f)] private float _questCompletedXp = 10f;

        [Tooltip("XP за уникальный dialog (см. 04_STATS_PROGRESSION.md §3). +N INT за unique event. " +
                 "Антиспам реализован через per-(player, npc, dialogNode) unique-set в T-P05, НЕ через cooldown.")]
        [SerializeField, Min(0f)] private float _dialogXpPerVisit = 1f;

        [Tooltip("XP за прыжок. +N DEX за jump")]
        [SerializeField, Min(0f)] private float _jumpXp = 0.5f;

        [Tooltip("XP за 1 метр пешей ходьбы (Q1.5: per 1m, не за 10m). +N DEX за 1m")]
        [SerializeField, Min(0f)] private float _walkXpPerMeter = 1f;

        [Tooltip("XP за 1 метр пилотирования. +N INT за 1m")]
        [SerializeField, Min(0f)] private float _pilotXpPerMeter = 1f;

        [Header("Distance thresholds (для batched XP в StatsServer)")]
        [Tooltip("Walked distance accumulator threshold (meters). XP начисляется кратно этому значению. " +
                 "Q1.5: per 1m, настраиваемо.")]
        [SerializeField, Min(1f)] private float _walkDistanceXpThreshold = 1f;

        [Tooltip("Piloted distance accumulator threshold (meters). XP начисляется кратно этому значению.")]
        [SerializeField, Min(1f)] private float _pilotDistanceXpThreshold = 10f;

        [Header("Track total walked/piloted distance (для ачивок/трекеров — Q1.5)")]
        [Tooltip("Зацемпить суммарную пройденную дистанцию. StatsServer ведёт Dictionary<clientId, float> " +
                 "для walked/piloted; expose через GetTotalWalkedDistance/GetTotalPilotedDistance.")]
        [SerializeField] private bool _trackTotalDistance = true;

        [Header("Global multiplier (тест/event-баффы)")]
        [Tooltip("Применяется ко ВСЕМ XP gains. 1.0 = норма. " +
                 "Q1.3: БЕЗ upper bound — 10 000 может быть нужно для теста. Min(0) — без отрицательных.")]
        [SerializeField, Min(0f)] private float _globalMultiplier = 1f;

        [Header("Формула роста (геометрическая, без капа)")]
        [Tooltip("XP_for_next_tier(tier) = _tierBaseXp * (_tierGrowthRate ^ tier). " +
                 "Без капа по дизайну (см. 04_STATS_PROGRESSION.md §1.1). " +
                 "При tier=50 / growth=1.5 → ~4×10^10 XP, double precision теряется; soft UI cap в StatsClientState (T-P04).")]
        [SerializeField, Min(1f)] private float _tierBaseXp = 100f;

        [Tooltip("Множитель роста между тирами. 1.0 = линейно, 1.5 = классическая RPG-экспонента.")]
        [SerializeField, Range(1.01f, 3.0f)] private float _tierGrowthRate = 1.5f;

        [Header("Tier-up уведомление")]
        [Tooltip("Показывать toast при tier-up (интеграция с QuestToast в T-P04 / T-P15).")]
        [SerializeField] private bool _announceTierUp = true;

        [Header("Debug (Q10.5)")]
        [Tooltip("Verbose logging в StatsServer/EquipmentServer/SkillsServer: gain XP, tier-up, denied equip. " +
                 "Включи в инспекторе для отладки, выключи в проде.")]
        [SerializeField] private bool _debugLogging = false;

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

        public float WalkDistanceXpThreshold   => _walkDistanceXpThreshold;
        public float PilotDistanceXpThreshold  => _pilotDistanceXpThreshold;

        public bool   TrackTotalDistance  => _trackTotalDistance;
        public float  GlobalMultiplier    => _globalMultiplier;
        public float  TierBaseXp          => _tierBaseXp;
        public float  TierGrowthRate      => _tierGrowthRate;
        public bool   AnnounceTierUp      => _announceTierUp;
        public bool   DebugLogging        => _debugLogging;

        // === Формула роста ===

        /// <summary>
        /// XP required для перехода с текущего tier на следующий.
        /// geometric: baseXp * (growthRate ^ currentTier). currentTier<0 → baseXp.
        /// </summary>
        public float XpForNextTier(int currentTier)
        {
            if (currentTier < 0) return _tierBaseXp;
            return _tierBaseXp * Mathf.Pow(_tierGrowthRate, currentTier);
        }

        public float ApplyGlobalMultiplier(float xp) => xp * _globalMultiplier;

        // === HARDCODED source → stat mapping (Q10.3) ===

        /// <summary>
        /// Q10.3: mining → STR, walk → DEX, всё остальное → INT. Hardcoded — не нужна вариативность.
        /// Если когда-то понадобится mapping — добавим поле в SO.
        /// </summary>
        public StatType GetStatFor(XpSource source) => source switch
        {
            XpSource.Mining         => StatType.Strength,
            XpSource.Walk           => StatType.Dexterity,
            XpSource.Jump           => StatType.Dexterity,
            XpSource.Pilot          => StatType.Intelligence,
            XpSource.Crafting       => StatType.Intelligence,
            XpSource.Exchange       => StatType.Intelligence,
            XpSource.Market         => StatType.Intelligence,
            XpSource.QuestAccepted  => StatType.Intelligence,
            XpSource.QuestCompleted => StatType.Intelligence,
            XpSource.Dialog         => StatType.Intelligence,
            _                       => StatType.Intelligence,
        };

        /// <summary>
        /// Базовое количество XP за единицу источника (без global multiplier).
        /// </summary>
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

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Tier warning при очень больших значениях — soft cap в UI, без жёсткого среза (без капа по дизайну).
            // Tier cap 50 = примерно 4×10^10 XP при growth=1.5 — после этого float precision теряется.
            // Это advisory warning, не блокирующий.
        }
#endif
    }
}
