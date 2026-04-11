using UnityEngine;

namespace ProjectC.Ship
{
    /// <summary>
    /// Профиль ветра — определяет как сила ветра меняется во времени/высоте.
    /// </summary>
    public enum WindProfile
    {
        /// <summary>Постоянный ветер без изменений</summary>
        Constant,
        /// <summary>Порывистый ветер с синусоидальными колебаниями</summary>
        Gust,
        /// <summary>Ветровой сдвиг — сила зависит от высоты</summary>
        Shear
    }

    /// <summary>
    /// Данные зоны ветра — ScriptableObject для настройки в Inspector.
    /// Создаётся через Create → ProjectC → Ship → Wind Zone Data.
    /// </summary>
    [CreateAssetMenu(menuName = "ProjectC/Ship/Wind Zone Data", fileName = "WindZoneData")]
    public class WindZoneData : ScriptableObject
    {
        [Header("Идентификатор")]
        [Tooltip("Уникальный ID зоны ветра (например: 'jetstream_01', 'turbulence_zone_a')")]
        public string zoneId;

        [Tooltip("Отображаемое имя в HUD")]
        public string displayName;

        [Header("Вектор Ветра")]
        [Tooltip("Направление ветра (мировые координаты). Нормализуется автоматически.")]
        public Vector3 windDirection = Vector3.forward;

        [Tooltip("Базовая сила ветра (в ньютонах)")]
        public float windForce = 50f;

        [Header("Профиль")]
        [Range(0f, 1f)]
        [Tooltip("Амплитуда вариации (0 = нет вариации, 1 = полная сила вариации)")]
        public float windVariation = 0.2f;

        [Tooltip("Профиль изменения ветра во времени/высоте")]
        public WindProfile profile = WindProfile.Constant;

        [Header("Порывы (только для Gust профиля)")]
        [Tooltip("Интервал порывов в секундах")]
        public float gustInterval = 2f;

        [Header("Сдвиг (только для Shear профиля)")]
        [Tooltip("Градиент силы ветра на единицу высоты (Н/м)")]
        public float shearGradient = 0.1f;
    }
}
