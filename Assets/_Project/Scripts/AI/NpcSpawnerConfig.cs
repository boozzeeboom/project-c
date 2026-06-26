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

        [Header("Spawn rules (around nearest player)")]
        [Tooltip("Минимальный радиус спавна от игрока (внутри не спавним — нечестно).")]
        [Range(5f, 100f)] public float spawnRadiusMin = 20f;
        [Tooltip("Максимальный радиус спавна от игрока (дальше — деспавн leash).")]
        [Range(20f, 200f)] public float spawnRadiusMax = 60f;
        [Tooltip("Лимит одновременно живых NPC от этого spawner'а.")]
        [Range(1, 50)] public int maxAliveCount = 5;
        [Tooltip("Интервал между проверками спавна (сек).")]
        [Range(0.5f, 30f)] public float spawnCheckInterval = 4f;
        [Tooltip("Вероятность спавна за каждую проверку (0..1). 0.5 = в среднем раз в 2 проверки.")]
        [Range(0f, 1f)] public float spawnChance = 0.5f;
        [Tooltip("Лимит спавнов на игрока в минуту (rate-limit, анти-спам).")]
        [Range(1, 60)] public int maxSpawnsPerPlayerPerMinute = 8;

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
    }
}