using UnityEngine;

namespace ProjectC.Trade
{
    [CreateAssetMenu(fileName = "TradeItem_", menuName = "ProjectC/Trade Item")]
    public class TradeItemDefinition : ScriptableObject
    {
        [Header("Basic Info")]
        [Tooltip("Уникальный идентификатор товара (например: 'mesium_canister_v01')")]
        public string itemId;

        [Tooltip("Отображаемое название")]
        public string displayName;

        [Tooltip("Иконка товара")]
        public Sprite icon;

        [Header("Economy")]
        [Tooltip("Базовая цена в кредитах (CR)")]
        public float basePrice = 10f;

        [Header("Physical Properties")]
        [Tooltip("Вес за единицу (кг)")]
        public float weight = 1f;

        [Tooltip("Объём за единицу (м³)")]
        public float volume = 0.1f;

        [Tooltip("Количество грузовых слотов за единицу")]
        public int slots = 1;

        [Header("Special Properties")]
        [Tooltip("Опасный груз (мезий — протечка при столкновении)")]
        public bool isDangerous;

        [Tooltip("Хрупкий груз (двигатели, МНП — повреждение при столкновении)")]
        public bool isFragile;

        [Tooltip("Контрабанда (нелегальный товар)")]
        public bool isContraband;

        [Header("Faction Restrictions")]
        [Tooltip("Фракция, которая может продавать (null = все)")]
        public Faction requiredFaction;
    }

    public enum Faction
    {
        None,
        NP,             // Новое Правительство
        Aurora,         // Мануфактура Аврора
        Titan,          // Мануфактура Титан
        Hermes,         // Мануфактура Гермес
        Prometheus,     // Мануфактура Прометей
        FreeTraders,    // Свободные торговцы
        Military        // Военные анклавы
    }
}
