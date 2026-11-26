// =====================================================================================
// ItemStatId.cs — канонические характеристики вариаций предметов (T-VAR01)
// =====================================================================================
// Документация:
//   • docs/pickble_items/variation_generator/01_DESIGN.md §2 (модель статов)
//
// Одно место, где перечислены статы, которые умеет крутить генератор вариаций.
// Значения хранятся в "канонических" целых единицах (milli-units для дробных статов),
// чтобы одинаковый seed давал одинаковый результат на всех платформах и на сервере:
//   • Scale = 1    → значение как есть (урон, крит, цена, броня...);
//   • Scale = 1000 → значение в тысячных (0.1 кг = 100, 2.5 м = 2500).
//
// ВАЖНО: не менять числовые значения enum'а — они попадают в JSON/DTO и в сохранённые
// инстансы. Новые статы добавлять только в конец.
// =====================================================================================

using System;
using System.Globalization;

namespace ProjectC.Items.Variation
{
    public enum ItemStatId
    {
        Damage = 0,
        CritChance = 1,
        Range = 2,
        Weight = 3,
        Price = 4,
        Durability = 5,
        Armor = 6,
        StrengthBonus = 7,
        DexterityBonus = 8,
        IntelligenceBonus = 9,
        StackSize = 10,
        MoveSpeed = 11,
    }

    public readonly struct ItemStatInfo
    {
        public readonly string DisplayName;
        public readonly string Unit;
        public readonly int Scale;

        public ItemStatInfo(string displayName, string unit, int scale)
        {
            DisplayName = displayName;
            Unit = unit;
            Scale = scale <= 0 ? 1 : scale;
        }

        public bool IsInteger => Scale == 1;
    }

    public static class ItemStatCatalog
    {
        public const int Count = 12;

        private static readonly ItemStatInfo[] Info =
        {
            new ItemStatInfo("Урон", "ед.", 1),
            new ItemStatInfo("Крит", "%", 1),
            new ItemStatInfo("Дальность", "м", 1000),
            new ItemStatInfo("Вес", "кг", 1000),
            new ItemStatInfo("Цена", "CR", 1),
            new ItemStatInfo("Прочность", "ед.", 1),
            new ItemStatInfo("Броня", "ед.", 1),
            new ItemStatInfo("Сила", "ед.", 1000),
            new ItemStatInfo("Ловкость", "ед.", 1000),
            new ItemStatInfo("Интеллект", "ед.", 1000),
            new ItemStatInfo("Стек", "шт.", 1),
            new ItemStatInfo("Скорость", "%", 1),
        };

        public static ItemStatInfo Get(ItemStatId stat)
        {
            int index = (int)stat;
            return index >= 0 && index < Info.Length ? Info[index] : Info[0];
        }

        public static float ToDisplay(ItemStatId stat, int canonical)
        {
            return canonical / (float)Get(stat).Scale;
        }

        public static int FromDisplay(ItemStatId stat, float value)
        {
            return (int)Math.Round(value * Get(stat).Scale, MidpointRounding.AwayFromZero);
        }

        public static string Format(ItemStatId stat, int canonical)
        {
            ItemStatInfo info = Get(stat);
            string number = info.IsInteger
                ? canonical.ToString(CultureInfo.InvariantCulture)
                : (canonical / (double)info.Scale).ToString("0.###", CultureInfo.InvariantCulture);
            return string.IsNullOrEmpty(info.Unit) ? number : number + " " + info.Unit;
        }

        public static string FormatSigned(ItemStatId stat, int canonical)
        {
            string sign = canonical >= 0 ? "+" : "-";
            return sign + Format(stat, Math.Abs(canonical));
        }

        /// <summary>
        /// Применить сумму flat + percent модификаторов один раз (порядок роллов не влияет):
        /// final = base + base * percentBps / 10000 + flat.
        /// Проценты считаются целочисленно (truncate toward zero), результат не ниже нуля.
        /// </summary>
        public static int Apply(int baseCanonical, long flat, long percentBps)
        {
            long percent = (long)baseCanonical * percentBps / 10000L;
            return ClampToInt((long)baseCanonical + percent + flat);
        }

        /// <summary>Умножить целое на basis points со округлением half away from zero.</summary>
        public static int MultiplyBps(int value, int bps)
        {
            long product = (long)value * bps;
            return ClampToInt(product >= 0 ? (product + 5000L) / 10000L : (product - 5000L) / 10000L);
        }

        public static int RoundPrice(long credits)
        {
            return ClampToInt(credits);
        }

        public static int ClampToInt(long value)
        {
            if (value < 0L) return 0;
            if (value > int.MaxValue) return int.MaxValue;
            return (int)value;
        }
    }
}
