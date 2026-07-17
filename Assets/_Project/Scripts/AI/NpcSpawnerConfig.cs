// Project C: Real-Time Combat Engine — T-NPC-02
// NpcSpawnerConfig: SO с параметрами спавна NPC-врагов.
// Design: docs/Character/Skills/real-time-combat/70_NPC_ENEMIES.md §2.1, §3.
//
// Designer-конфигурируемый, 0 hard-coded параметров в спавнере.

using UnityEngine;

namespace ProjectC.AI
{
    [CreateAssetMenu(fileName = "NpcSpawnerConfig_", menuName = "Project C/AI/Npc Spawner Config")]
    public class NpcSpawnerConfig : ScriptableObject
    {
        [Header("Prefab")]
        [Tooltip("Префаб NPC-врага (должен иметь NetworkObject + NpcAttacker + NpcTarget + NpcBrain).")]
        public GameObject npcPrefab;

        [Header("Spawn rules (around spawner)")]
        [Tooltip("Минимальный радиус спавна от spawner'а (anchor). NPC не появятся вплотную — нечестно.")]
        [Range(1f, 50f)] public float spawnRadiusMin = 5f;
        [Tooltip("Максимальный радиус спавна от spawner'а — определяет зону спавна.")]
        [Range(5f, 200f)] public float spawnRadiusMax = 20f;
        [Tooltip("T-NPC-08 v0.2: Игрок должен быть в этом радиусе от spawner'а чтобы спавнить NPC. " +
                 "Если 0 = всегда спавним (даже когда игрок далеко). " +
                 "Полезно для зонирования — NPC спавнятся только когда игрок входит в зону.")]
        [Range(0f, 300f)] public float activationRadius = 60f;
        [Tooltip("Лимит одновременно живых NPC от этого spawner'а.")]
        [Range(1, 50)] public int maxAliveCount = 5;
        [Tooltip("Интервал между проверками спавна (сек).")]
        [Range(0.5f, 30f)] public float spawnCheckInterval = 4f;
        [Tooltip("Вероятность спавна за каждую проверку (0..1). 0.5 = в среднем раз в 2 проверки.")]
        [Range(0f, 1f)] public float spawnChance = 0.5f;
        [Tooltip("Лимит спавнов на игрока в минуту (rate-limit, анти-спам).")]
        [Range(1, 60)] public int maxSpawnsPerPlayerPerMinute = 8;

        [Header("Spawn Cycle (T-NPC-11)")]
        [Tooltip("Infinite = текущее поведение (рефилл бесконечно). " +
                 "Finite = спавнить totalSpawnLimit и остановиться навсегда. " +
                 "FiniteCycle = спавнить totalSpawnLimit за цикл, ждать restart trigger.")]
        public SpawnMode spawnMode = SpawnMode.Infinite;

        [Tooltip("Лимит NPC за цикл (для Finite/FiniteCycle). " +
                 "0 = без лимита (используется только maxAliveCount, backward compat).")]
        [Range(0, 100)] public int totalSpawnLimit = 0;

        [Header("Chunk Integration (T-NPC-09)")]
        [Tooltip("Подписаться на ChunkLoader.OnChunkLoaded/OnChunkUnloaded. При загрузке чанка — спавнить NPC в его центре. false = старое zone-based поведение.")]
        public bool autoPopulateChunks = false;

        [Tooltip("Радиус спавна вокруг центра чанка (метры).")]
        [Range(5f, 100f)] public float chunkSpawnRadius = 30f;

        [Tooltip("Максимум NPC на один чанк. 0 = chunk-spawn выключен даже при autoPopulateChunks=true.")]
        [Range(0, 20)] public int maxAlivePerChunk = 3;

        [Header("Surface validation")]
        [Tooltip("LayerMask террейна для raycast вниз (точка должна попасть на поверхность).")]
        public LayerMask groundMask = 1; // default layer
        [Tooltip("Максимальная дистанция raycast вниз для поиска земли.")]
        [Range(5f, 100f)] public float groundRaycastDistance = 30f;
        [Tooltip("Минимальная дистанция от других NPC при спавне (чтобы не spawn-in-place).")]
        [Range(1f, 20f)] public float minDistanceFromOtherNpc = 5f;

        [Header("Difficulty scaling (post-MVP)")]
        [Tooltip("Множитель урона NPC в зависимости от расстояния от игрока. " +
                 "X=0 = возле игрока (легче), X=1 = на границе радиуса (сложнее).")]
        public AnimationCurve difficultyByDistance = AnimationCurve.Linear(0, 1f, 1, 1f);

        [Header("Behavior (T-NPC-14)")]
        [Tooltip("Aggressive = атакует по proximity (стандартный враг).\n" +
                 "Passive = мирный квестовый NPC. Агрится только после удара игрока.\n" +
                 "Neutral = никогда не атакует (декорация).\n\n" +
                 "ВАЖНО: спавнер НЕ подтирает поле behaviorType на префабе — он вызывает " +
                 "NpcBrain.ApplySpawnerBehavior() после Instantiate. Это значит, что префаб " +
                 "сохраняет свой baseline (например Aggressive), а спавнер задаёт только " +
                 "оверрайд для конкретного региона (например, мирный квестовый лагерь).")]
        public NpcBrain.BehaviorType behaviorType = NpcBrain.BehaviorType.Aggressive;

        [Tooltip("T-NPC-14 (только для Passive): % от maxHp, после которого NPC " +
                 "становится агрессивным. 25 = при потере 25% HP NPC переходит в Chase.\n" +
                 "Если 0 — fallback к значению из префаба NPC.")]
        [Range(0f, 100f)] public float passiveAggroHpThreshold = 25f;

        [Tooltip("T-NPC-14 (только для Passive): за сколько ударов в минуту NPC точно " +
                 "станет агрессивным (даже если cumulativeDamage% < passiveAggroHpThreshold). " +
                 "Fallback для защиты от фарма квестовых NPC мелкими ударами.\n" +
                 "0 = отключить fallback (только threshold). Если -1 — fallback к префабу.")]
        [Range(-1, 20)] public int passiveMaxHitsPerMinute = 3;

        [Header("Visual (anti-restrictive T-NPC-05)")]
        [Tooltip("Опционально. Применяется к NPC при спавне (материал/цвет/масштаб/имя). " +
                 "Если null — дефолтный вид из префаба (HumanM_Model без изменений). " +
                 "Anti-restrictive: позволяет иметь один префаб и разные фракции через разные .asset.")]
        public NpcVisualConfig visualConfig;

        [Header("Skills (T-NPC-SKILL-04)")]
        [Tooltip("Набор скилов NPC (SkillNodeConfig с оверрайдами). " +
                 "Если null — NPC использует дефолтную атаку из NpcCombatData (backward compat). " +
                 "Применяется к NpcAttacker при спавне — НЕ подтирает префаб.")]
        public NpcSkillSet npcSkillSet;

        // ============================================================
        // Phase 1-2: Social Behavior (T-NPC-S06/S07) — add-only, backward compat
        // ============================================================

        [Header("Social Behavior (T-NPC-S01+)")]
        [Tooltip("Включает NpcSocialBrain при спавне. Если false — NPC использует только старый FSM.")]
        public bool socialEnabled = true;

        [Header("Personality (T-NPC-S07)")]
        [Tooltip("Конфиг личности NPC. Если null — используются дефолты (courage=0.7 и т.д.).")]
        public NpcPersonalityConfig personalityConfig;

        [Header("Idle Activity")]
        [Tooltip("Тип idle-активности по умолчанию. StandStill = текущее поведение (backward compat).")]
        public NpcIdleActivity defaultIdleActivity = NpcIdleActivity.StandStill;
        [Tooltip("Паттерн патрулирования: Loop = по кругу, PingPong = туда-обратно, Random = случайный выбор.")]
        public PatrolPattern patrolPattern = PatrolPattern.Loop;
        [Tooltip("Точки патруля (ручная расстановка). Если пусто — activity = StandStill.")]
        public Vector3[] patrolWaypoints;
        [Tooltip("Секунд ожидания на каждой точке патруля.")]
        public float idleAtWaypointSec = 3f;
        [Tooltip("Радиус случайного блуждания (Wander).")]
        public float wanderRadius = 8f;

        [Header("Flee")]
        [Tooltip("Может ли NPC убегать при низком HP.")]
        public bool canFlee = true;
        [Tooltip("Порог HP (доля 0..1), ниже которого NPC начинает Flee.")]
        [Range(0f, 1f)] public float fleeHpThreshold = 0.25f;
        [Tooltip("Радиус поиска союзников для бегства к ним.")]
        public float fleeAllySeekRadius = 30f;

        [Header("Alarm")]
        [Tooltip("Радиус тревоги: NPC в этом радиусе реагируют на Alarm.")]
        public float alarmRadius = 15f;
        [Tooltip("Радиус реакции на смерть союзника.")]
        public float allyDeathRadius = 20f;
        [Tooltip("Этот NPC — стражник (реагирует на Alarm → Chase, а не Investigate).")]
        public bool isGuard = false;

        [Header("Group")]
        [Tooltip("Назначать NPC в группу при спавне нескольких в groupSpawnRadius.")]
        public bool assignGroupOnSpawn = true;
        [Tooltip("Радиус группировки: NPC в этом радиусе попадают в одну группу.")]
        public float groupSpawnRadius = 25f;

        [Header("Memory")]
        [Tooltip("NPC помнит обидчика и агрится при повторной встрече.")]
        public bool enableGrudgeMemory = true;
        [Tooltip("Длительность памяти обидчика (сек).")]
        public float grudgeDurationSec = 300f;

        [Header("Threat Assessment (T-NPC-S13)")]
        [Tooltip("Радиус оценки соотношения сил перед боем. 0 = использовать дефолт из NpcSocialBrain.")]
        [Range(0f, 100f)] public float threatEvaluationRange = 30f;

        [Header("Cover (T-NPC-S14)")]
        [Tooltip("Радиус поиска укрытий. 0 = отключить cover-seeking.")]
        [Range(0f, 50f)] public float coverSeekRadius = 25f;
        [Tooltip("NPC ищет укрытие при HP ниже этого порога (0..1).")]
        [Range(0f, 1f)] public float coverHpThreshold = 0.5f;

        [Header("Surrender (T-NPC-S16)")]
        [Tooltip("Порог HP (доля 0..1), ниже которого NPC может сдаться. 0 = никогда.")]
        [Range(0f, 1f)] public float surrenderHpThreshold = 0.10f;
        [Tooltip("Может ли NPC сдаться.")]
        public bool canSurrender = true;

        [Header("Post-Combat (T-NPC-S17)")]
        [Tooltip("Включает post-combat поведение (wounded/heal/reinforcement).")]
        public bool enablePostCombat = true;
        [Tooltip("Длительность wounded-состояния (сек).")]
        [Range(5f, 30f)] public float woundedDuration = 15f;
        [Tooltip("Порог HP для heal (доля 0..1).")]
        [Range(0f, 1f)] public float healHpThreshold = 0.4f;

        [Header("Social Role (T-NPC-S18)")]
        [Tooltip("Пресет социальной роли (Guard, Civilian, Merchant, etc). Если null — используется personalityConfig.")]
        public SocialRoleConfig socialRole;

        [Header("Faction (T-NPC-S19)")]
        [Tooltip("Фракция NPC. Определяет отношения «свой/чужой».")]
        public NpcFaction faction;

        [Header("Vengeance (T-NPC-S20)")]
        [Tooltip("Включает кросс-спавн vengeance-память.")]
        public bool enableVengeanceMemory = true;

        [Header("Loot (T-NPC-12)")]
        [Tooltip("Префаб дропа (визуал). Если null — programmatic жёлтая сфера (backward compat).")]
        public GameObject lootPrefab;

        [Tooltip("Таблица дропа (items + credits). Если null — fallback к формуле maxHp/4 credits (backward compat).")]
        public Items.LootTable lootTable;

        [Header("Debug")]
        [Tooltip("Включить debug-логи спавнера в консоль.")]
        public bool showDebugLogs = false;
    }
}
