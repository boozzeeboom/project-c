// =====================================================================================
// ItemVariationTypes.cs — данные профиля/ролла вариаций предметов (T-VAR01)
// =====================================================================================
// Документация:
//   • docs/pickble_items/variation_generator/01_DESIGN.md — модель
//   • docs/pickble_items/variation_generator/02_API.md   — публичный API
//
// Все типы — обычные [Serializable] C#-классы/структуры с public-полями:
//   • JsonUtility (профиль из JSON на сервере),
//   • Unity SerializedObject (редактор профиля),
//   • будущие DTO/сейвы.
// Никаких UnityEngine/UnityEditor-зависимостей здесь нет: ядро можно использовать
// в dedicated server без сцены и AssetDatabase.
//
// Статы в ItemVariationBaseStats/ItemStatRoll хранятся в канонических единицах
// (см. ItemStatId.cs): целые, дробные — в тысячных.
// =====================================================================================

using System;
using System.Collections.Generic;

namespace ProjectC.Items.Variation
{
    [Serializable]
    public struct ItemVariationBaseStats
    {
        public int damage;
        public int critChance;
        public int rangeMilli;
        public int weightMilli;
        public int price;
        public int durability;
        public int armor;
        public int strengthMilli;
        public int dexterityMilli;
        public int intelligenceMilli;
        public int stackSize;
        public int moveSpeed;

        public static ItemVariationBaseStats CreateDefault()
        {
            return new ItemVariationBaseStats { stackSize = 1 };
        }

        public int Get(ItemStatId stat)
        {
            switch (stat)
            {
                case ItemStatId.Damage: return damage;
                case ItemStatId.CritChance: return critChance;
                case ItemStatId.Range: return rangeMilli;
                case ItemStatId.Weight: return weightMilli;
                case ItemStatId.Price: return price;
                case ItemStatId.Durability: return durability;
                case ItemStatId.Armor: return armor;
                case ItemStatId.StrengthBonus: return strengthMilli;
                case ItemStatId.DexterityBonus: return dexterityMilli;
                case ItemStatId.IntelligenceBonus: return intelligenceMilli;
                case ItemStatId.StackSize: return stackSize;
                case ItemStatId.MoveSpeed: return moveSpeed;
                default: return 0;
            }
        }

        public void Set(ItemStatId stat, int value)
        {
            switch (stat)
            {
                case ItemStatId.Damage: damage = value; break;
                case ItemStatId.CritChance: critChance = value; break;
                case ItemStatId.Range: rangeMilli = value; break;
                case ItemStatId.Weight: weightMilli = value; break;
                case ItemStatId.Price: price = value; break;
                case ItemStatId.Durability: durability = value; break;
                case ItemStatId.Armor: armor = value; break;
                case ItemStatId.StrengthBonus: strengthMilli = value; break;
                case ItemStatId.DexterityBonus: dexterityMilli = value; break;
                case ItemStatId.IntelligenceBonus: intelligenceMilli = value; break;
                case ItemStatId.StackSize: stackSize = value; break;
                case ItemStatId.MoveSpeed: moveSpeed = value; break;
            }
        }

        public bool Equals(ItemVariationBaseStats other)
        {
            return damage == other.damage
                && critChance == other.critChance
                && rangeMilli == other.rangeMilli
                && weightMilli == other.weightMilli
                && price == other.price
                && durability == other.durability
                && armor == other.armor
                && strengthMilli == other.strengthMilli
                && dexterityMilli == other.dexterityMilli
                && intelligenceMilli == other.intelligenceMilli
                && stackSize == other.stackSize
                && moveSpeed == other.moveSpeed;
        }
    }

    public enum ItemVariationNameStyle
    {
        /// <summary>"Закалённый Клинок · Лёгкий" (аффикс-прилагательное + базовое имя).</summary>
        PrefixSuffix = 0,

        /// <summary>"Клинок [Закалённый, Лёгкий]" — нейтрально к языку базового имени.</summary>
        BaseWithAffixList = 1,
    }

    [Serializable]
    public class ItemVariationTier
    {
        public string tierId = "common";
        public string displayName = "Обычный";
        public string colorHex = "#C8C8C8";

        [TooltipCompat("Относительный вес при выборе тира. 0 = тир выключен.")]
        public int weight = 100;

        [TooltipCompat("Сколько дополнительных аффиксов даёт тир сверх minRolls..maxRolls профиля.")]
        public int extraRolls = 0;

        [TooltipCompat("Множитель силы роллов аффиксов, bps. 10000 = x1.0, 12500 = x1.25.")]
        public int valueMultiplierBps = 10000;

        [TooltipCompat("Множитель итоговой цены, bps. 10000 = x1.0.")]
        public int priceMultiplierBps = 10000;

        [TooltipCompat("База качества 0..100; случайность роллов добавляет до +12.")]
        public int qualityBase = 5;
    }

    [Serializable]
    public class ItemVariationModifier
    {
        public string affixId = "affix";
        public string affixName = "Аффикс";

        [TooltipCompat("true = аффикс пишется после имени предмета, false = перед именем.")]
        public bool isSuffix;

        public ItemStatId stat = ItemStatId.Damage;

        [TooltipCompat("Flat-диапазон в канонических единицах (0/0 = не роллить flat).")]
        public int flatMin;
        public int flatMax;

        [TooltipCompat("Процент от базового стата в bps (0/0 = не роллить процент). 500 = +5%.")]
        public int percentMinBps;
        public int percentMaxBps;

        [TooltipCompat("Относительный вес выбора аффикса среди доступных.")]
        public int weight = 100;

        [TooltipCompat("С какого индекса тира доступен аффикс (0 = со всех).")]
        public int minTierIndex;

        [TooltipCompat("По какой индекс тира включительно доступен аффикс.")]
        public int maxTierIndex = 99;

        [TooltipCompat("true = аффикс нельзя выпасть дважды в одном инстансе.")]
        public bool unique = true;

        public bool HasFlatRange => flatMin != 0 || flatMax != 0;
        public bool HasPercentRange => percentMinBps != 0 || percentMaxBps != 0;

        public bool IsAvailableForTier(int tierIndex)
        {
            return tierIndex >= minTierIndex && tierIndex <= maxTierIndex;
        }
    }

    [Serializable]
    public class ItemVariationProfile
    {
        public const int CurrentSchemaVersion = 1;

        public int schemaVersion = CurrentSchemaVersion;
        public string profileId = "variation_profile";
        public string displayName = "Вариация предмета";
        public int baseItemId;
        public string baseItemName = "Предмет";
        public int seedSalt;

        public ItemVariationNameStyle nameStyle = ItemVariationNameStyle.PrefixSuffix;

        [TooltipCompat("Минимум аффиксов на инстанс (без extraRolls тира).")]
        public int minRolls = 1;

        [TooltipCompat("Максимум аффиксов на инстанс (без extraRolls тира).")]
        public int maxRolls = 2;

        public List<ItemVariationTier> tiers = new List<ItemVariationTier>();
        public List<ItemVariationModifier> modifiers = new List<ItemVariationModifier>();

        /// <summary>Снапшот базовых статов: серверу не нужен ItemData/AssetDatabase.</summary>
        public ItemVariationBaseStats baseStats = ItemVariationBaseStats.CreateDefault();
    }

    [Serializable]
    public struct ItemStatRoll
    {
        public ItemStatId stat;
        public int flat;
        public int percentBps;

        /// <summary>Индекс в ItemVariationProfile.modifiers (для имени аффикса/аудита).</summary>
        public int modifierIndex;

        /// <summary>Множитель тира на момент ролла (в ролл уже применён; хранится для аудита).</summary>
        public int tierMultiplierBps;

        public int ApplyTo(int baseCanonical)
        {
            return ItemStatCatalog.Apply(baseCanonical, flat, percentBps);
        }
    }

    [Serializable]
    public class ItemVariationResult
    {
        public int schemaVersion = ItemVariationProfile.CurrentSchemaVersion;

        public string profileId;
        public int baseItemId;
        public string baseItemName;
        public long seed;
        public int luckBps;

        public int tierIndex;
        public string tierId;
        public string tierName;
        public string tierColorHex;

        public string displayName;
        public string prefix;
        public string suffix;

        public int quality;
        public int priceMultiplierBps;
        public int finalPrice;

        public ItemVariationBaseStats baseStats;
        public ItemVariationBaseStats finalStats;
        public List<ItemStatRoll> rolls = new List<ItemStatRoll>();

        public int RollCount => rolls != null ? rolls.Count : 0;
    }

    public enum ItemVariationSeverity
    {
        Info = 0,
        Warning = 1,
        Error = 2,
    }

    [Serializable]
    public struct ItemVariationIssue
    {
        public ItemVariationSeverity severity;
        public string message;

        public ItemVariationIssue(ItemVariationSeverity severity, string message)
        {
            this.severity = severity;
            this.message = message;
        }
    }

    /// <summary>
    /// Tooltip без UnityEngine: редактор показывает текст из этого атрибута,
    /// а ядро остаётся независимым от Unity. В JSON не попадает.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public sealed class TooltipCompatAttribute : Attribute
    {
        public readonly string Text;

        public TooltipCompatAttribute(string text)
        {
            Text = text;
        }
    }
}
