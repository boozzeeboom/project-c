using System;
using UnityEngine;
using ProjectC.Quests;

namespace ProjectC.World.NpcPrimum
{
    /// <summary>
    /// Конфиг пилотной толпы PRIMUM (T1 = 100 NPC, далее 300/1000/10000).
    /// Один ассет на сцену: Assets/_Project/Data/NPC/PrimumCrowdConfig.asset.
    /// Собирается генератором Tools/Project C/NPC/Primum Crowd.
    /// </summary>
    [CreateAssetMenu(fileName = "PrimumCrowdConfig", menuName = "ProjectC/NPC/Primum Crowd Config", order = 210)]
    public class PrimumCrowdConfig : ScriptableObject
    {
        [Header("Население")]
        [Tooltip("Сколько NPC держать в сцене. Тиры пилота: 100 / 300 / 1000 / 10000.")]
        [Min(0)] public int targetCount = 100;

        [Tooltip("Жёсткий лимит резидентов на один houseId (не больше 4 по ТЗ).")]
        [Range(1, 4)] public int maxResidentsPerHouse = 4;

        [Tooltip("Радиус поиска навмеша вокруг центра дома.")]
        public float houseSampleRadius = 30f;

        [Tooltip("Второй проход поиска навмеша, если первый не нашёл.")]
        public float houseSampleFallbackRadius = 60f;

        [Tooltip("Сколько NPC инстанцировать за кадр при батчевом спавне.")]
        [Range(1, 100)] public int spawnBatchPerFrame = 8;

        [Header("Шаблон и визуал")]
        public GameObject npcTemplate;
        public RuntimeAnimatorController animatorControllerFemale;
        public RuntimeAnimatorController animatorControllerMale;

        [Tooltip("Все ригнутые модели NPC (FBX с humanoid-аватаром). Выбор: index % length.")]
        public GameObject[] modelCatalog = Array.Empty<GameObject>();

        [Tooltip("NpcDefinition по порядку индексов NPC (index % length, если NPC больше).")]
        public NpcDefinition[] residentDefinitions = Array.Empty<NpcDefinition>();

        [Header("Движение")]
        public float walkSpeedMin = 0.7f;
        public float walkSpeedMax = 1.4f;
        public float acceleration = 8f;
        public float angularSpeed = 270f;
        public float stoppingDistance = 0.4f;
        public float walkAnimReferenceSpeed = 1.4f;

        [Tooltip("Прогулка рядом с домом.")]
        public float wanderRadiusNear = 45f;

        [Tooltip("Дальняя прогулка (редкая), всё ещё «заякорена» на дом.")]
        public float wanderRadiusFar = 110f;

        [Range(0f, 1f)] public float farWanderChance = 0.3f;
        public float minRadiusFromHome = 6f;

        public float idleMinSeconds = 1.5f;
        public float idleMaxSeconds = 5f;

        [Header("Тик и LOD")]
        public float decisionIntervalMin = 0.2f;
        public float decisionIntervalMax = 0.5f;

        [Tooltip("Дистанция видимости нейм-плейта.")]
        public float nameplateRadius = 28f;

        [Tooltip("Дальше — рендереры NPC выключены.")]
        public float visualRadius = 400f;

        [Tooltip("Дальше — Animator выключен (поза замирает).")]
        public float animatorRadius = 120f;

        [Tooltip("Отключить avoidance навмеша (для тиров 300+).")]
        public bool disableAvoidance = false;

        [Header("Отладка")]
        public bool logStats = true;
    }
}
