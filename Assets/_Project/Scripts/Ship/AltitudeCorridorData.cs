using UnityEngine;

namespace ProjectC.Ship
{
    /// <summary>
    /// Данные коридора высот — ScriptableObject для настройки в Inspector.
    /// Глобальные и городские коридоры хранятся в отдельных ассетах.
    /// </summary>
    [CreateAssetMenu(menuName = "ProjectC/Ship/Altitude Corridor Data", fileName = "AltitudeCorridor")]
    public class AltitudeCorridorData : ScriptableObject
    {
        [Header("Идентификатор")]
        [Tooltip("Уникальный ID коридора (например: 'global', 'primus', 'tertius')")]
        public string corridorId;

        [Tooltip("Отображаемое имя в HUD")]
        public string displayName;

        [Header("Границы Высот (метры)")]
        [Tooltip("Минимальная безопасная высота")]
        public float minAltitude;

        [Tooltip("Максимальная безопасная высота")]
        public float maxAltitude;

        [Tooltip("Расстояние до warning (м) — предупреждение при приближении к границе")]
        public float warningMargin = 100f;

        [Tooltip("Запас для критической верхней границы (м)")]
        public float criticalUpperMargin = 200f;

        [Header("Тип Коридора")]
        [Tooltip("true = глобальный коридор, false = локальный городской")]
        public bool isGlobal;

        [Header("Городские Параметры (только для локальных коридоров)")]
        [Tooltip("Центр города в мировых координатах")]
        public Vector3 cityCenter;

        [Tooltip("Радиус города (м)")]
        public float cityRadius = 500f;

        [Tooltip("Корабль должен быть зарегистрирован для доступа к коридору города")]
        public bool requiresRegistration;

        /// <summary>
        /// Проверить находится ли позиция в радиусе города.
        /// </summary>
        public bool IsInCityZone(Vector3 position)
        {
            if (isGlobal) return false;
            return Vector3.Distance(position, cityCenter) <= cityRadius;
        }

        /// <summary>
        /// Получить статус высоты для данной позиции.
        /// </summary>
        public AltitudeStatus GetStatus(float altitude)
        {
            if (altitude < minAltitude)
                return AltitudeStatus.DangerLower;

            if (altitude < minAltitude + warningMargin)
                return AltitudeStatus.WarningLower;

            if (altitude > maxAltitude + criticalUpperMargin)
                return AltitudeStatus.DangerUpper;

            if (altitude > maxAltitude - warningMargin)
                return AltitudeStatus.WarningUpper;

            return AltitudeStatus.Safe;
        }
    }

    /// <summary>
    /// Статус высоты корабля относительно активного коридора.
    /// </summary>
    public enum AltitudeStatus
    {
        /// <summary>В безопасном диапазоне — всё OK</summary>
        Safe,

        /// <summary>Приближение к нижней границе (minAlt + warningMargin)</summary>
        WarningLower,

        /// <summary>Приближение к верхней границе (maxAlt - warningMargin)</summary>
        WarningUpper,

        /// <summary>Ниже минимума — турбулентность, Завеса!</summary>
        DangerLower,

        /// <summary>Выше критического порога — деградация систем</summary>
        DangerUpper
    }
}
