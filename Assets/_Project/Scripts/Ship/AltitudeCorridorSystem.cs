using UnityEngine;
using System.Collections.Generic;

namespace ProjectC.Ship
{
    /// <summary>
    /// Менеджер системы коридоров высот.
    /// Управляет глобальными и городскими коридорами, определяет активный коридор для корабля.
    /// 
    /// Архитектура:
    /// - Глобальный коридор применяется всегда как fallback
    /// - Городские коридоры применяются когда корабль в радиусе города
    /// - Приоритет: ближайший городской коридор > глобальный
    /// 
    /// Настройка:
    /// - Создать пустой GameObject "AltitudeCorridorSystem" в сцене
    /// - Повесить этот скрипт
    /// - Назначить список коридоров в Inspector (или использовать SetupDefaultCorridors)
    /// </summary>
    public class AltitudeCorridorSystem : MonoBehaviour
    {
        [Header("Коридоры")]
        [Tooltip("Список всех коридоров (глобальный + городские). Назначить в Inspector или использовать SetupDefaultCorridors().")]
        public List<AltitudeCorridorData> corridors = new List<AltitudeCorridorData>();

        // Кэш глобального коридора для быстрого доступа
        private AltitudeCorridorData _globalCorridor;

        // Синглтон для удобного доступа из ShipController
        private static AltitudeCorridorSystem _instance;
        public static AltitudeCorridorSystem Instance => _instance;

        private void Awake()
        {
            // Синглтон паттерн
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;

            // Кэшируем глобальный коридор
            FindGlobalCorridor();
        }

        /// <summary>
        /// Найти и закэшировать глобальный коридор из списка.
        /// </summary>
        private void FindGlobalCorridor()
        {
            foreach (var corridor in corridors)
            {
                if (corridor != null && corridor.isGlobal)
                {
                    _globalCorridor = corridor;
                    return;
                }
            }
            CreateFallbackGlobalCorridor();
        }

        /// <summary>
        /// Создать fallback глобальный коридор если не назначен в Inspector.
        /// </summary>
        private void CreateFallbackGlobalCorridor()
        {
            _globalCorridor = ScriptableObject.CreateInstance<AltitudeCorridorData>();
            _globalCorridor.corridorId = "global";
            _globalCorridor.displayName = "Global Corridor";
            _globalCorridor.isGlobal = true;
            _globalCorridor.minAltitude = 1200f;
            _globalCorridor.maxAltitude = 4450f;
            _globalCorridor.warningMargin = 100f;
            _globalCorridor.criticalUpperMargin = 200f;

            corridors.Add(_globalCorridor);
        }

        /// <summary>
        /// Получить активный коридор для позиции корабля.
        /// Приоритет: ближайший городской коридор > глобальный.
        /// </summary>
        /// <param name="shipPosition">Позиция корабля в мировых координатах</param>
        /// <returns>Активный коридор (никогда не null)</returns>
        public AltitudeCorridorData GetActiveCorridor(Vector3 shipPosition)
        {
            // Ищем ближайший городской коридор
            AltitudeCorridorData closestCity = null;
            float closestDistance = float.MaxValue;

            foreach (var corridor in corridors)
            {
                if (corridor == null || corridor.isGlobal) continue;

                // Проверяем радиус города
                float distance = Vector3.Distance(shipPosition, corridor.cityCenter);
                if (distance <= corridor.cityRadius && distance < closestDistance)
                {
                    closestDistance = distance;
                    closestCity = corridor;
                }
            }

            // Возвращаем городской или глобальный
            return closestCity != null ? closestCity : _globalCorridor;
        }

        /// <summary>
        /// Проверить статус высоты для корабля в данной позиции.
        /// </summary>
        /// <param name="position">Позиция корабля</param>
        /// <param name="corridor">Коридор для проверки (или null для автоопределения)</param>
        /// <returns>Статус высоты</returns>
        public AltitudeStatus ValidateAltitude(Vector3 position, AltitudeCorridorData corridor = null)
        {
            if (corridor == null)
                corridor = GetActiveCorridor(position);

            return corridor.GetStatus(position.y);
        }

        /// <summary>
        /// Проверить находится ли позиция в зоне конкретного города.
        /// </summary>
        public bool IsInCityZone(Vector3 position, string cityId)
        {
            foreach (var corridor in corridors)
            {
                if (corridor == null || corridor.isGlobal) continue;
                if (corridor.corridorId == cityId)
                {
                    return corridor.IsInCityZone(position);
                }
            }
            return false;
        }

        /// <summary>
        /// Получить все городские коридоры.
        /// </summary>
        public List<AltitudeCorridorData> GetCityCorridors()
        {
            List<AltitudeCorridorData> cityCorridors = new List<AltitudeCorridorData>();
            foreach (var corridor in corridors)
            {
                if (corridor != null && !corridor.isGlobal)
                    cityCorridors.Add(corridor);
            }
            return cityCorridors;
        }

        /// <summary>
        /// Установить коридоры программно (для тестов или динамической настройки).
        /// </summary>
        public void SetCorridors(List<AltitudeCorridorData> newCorridors)
        {
            corridors.Clear();
            corridors.AddRange(newCorridors);
            FindGlobalCorridor();
        }

#if UNITY_EDITOR
        /// <summary>
        /// Создать дефолтные коридоры через Editor (меню Tools).
        /// Вызвать из Editor скрипта или через ExecuteInEditMode.
        /// </summary>
        [UnityEditor.MenuItem("Tools/Project C/Setup Altitude Corridors")]
        public static void SetupDefaultCorridors()
        {
            Debug.Log("[AltitudeCorridorSystem] Setting up default corridors...");

            // Получаем или создаём менеджер на сцене
            AltitudeCorridorSystem system = FindAnyObjectByType<AltitudeCorridorSystem>();
            if (system == null)
            {
                GameObject go = new GameObject("AltitudeCorridorSystem");
                system = go.AddComponent<AltitudeCorridorSystem>();
            }

            // Создаём ScriptableObject ассеты если их нет
            CreateCorridorAsset(system, "Global", "Global Corridor", true, 1200f, 4450f, Vector3.zero, 0f);
            CreateCorridorAsset(system, "Primus", "Primus City", false, 4100f, 4450f, new Vector3(0, 4348f, 0), 500f);
            CreateCorridorAsset(system, "Tertius", "Tertius City", false, 2300f, 2600f, new Vector3(1000, 2462f, 1000), 500f);
            CreateCorridorAsset(system, "Quartus", "Quartus City", false, 1500f, 1850f, new Vector3(-1000, 1690f, 500), 500f);
            CreateCorridorAsset(system, "Kilimanjaro", "Kilimanjaro City", false, 1200f, 1550f, new Vector3(500, 1395f, -1000), 500f);
            CreateCorridorAsset(system, "Secundus", "Secundus City", false, 1000f, 1250f, new Vector3(-500, 1142f, -500), 500f);

            Debug.Log("[AltitudeCorridorSystem] Default corridors setup complete!");
        }

        private static void CreateCorridorAsset(AltitudeCorridorSystem system,
            string id, string name, bool isGlobal, float minAlt, float maxAlt, Vector3 center, float radius)
        {
            // Проверяем существует ли уже
            foreach (var existing in system.corridors)
            {
                if (existing != null && existing.corridorId == id)
                {
                    Debug.Log($"[AltitudeCorridorSystem] Corridor '{id}' already exists, skipping.");
                    return;
                }
            }

            // Создаём новый
            var newCorridor = ScriptableObject.CreateInstance<AltitudeCorridorData>();
            newCorridor.corridorId = id;
            newCorridor.displayName = name;
            newCorridor.isGlobal = isGlobal;
            newCorridor.minAltitude = minAlt;
            newCorridor.maxAltitude = maxAlt;
            newCorridor.warningMargin = 100f;
            newCorridor.criticalUpperMargin = 200f;
            newCorridor.cityCenter = center;
            newCorridor.cityRadius = radius;
            newCorridor.requiresRegistration = !isGlobal;

            system.corridors.Add(newCorridor);
            Debug.Log($"[AltitudeCorridorSystem] Created corridor: {name}");
        }
#endif
    }
}
