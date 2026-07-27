using System;
using Unity.Netcode;
using UnityEngine;

namespace ProjectC.Core
{
    /// <summary>
    /// Server-authoritative game calendar. Drives day/week/month/year semantics
    /// on top of the 24-hour cycle managed by ServerWeatherController.
    /// </summary>
    [System.Serializable]
    public struct GameTimeData : INetworkSerializable
    {
        [Tooltip("Game year (1-based)")]
        public int Year;

        [Tooltip("Month of year (1-12)")]
        public int Month;

        [Tooltip("Day of month (1-30)")]
        public int Day;

        [Tooltip("Day of year (1-360)")]
        public int DayOfYear;

        [Tooltip("Day of week (0-6)")]
        public int Weekday;

        // ────────────────────────────────
        //  Static name tables
        // ────────────────────────────────

        public static readonly string[] WeekdayNames =
        {
            "Manday", "Tirsday", "Wotanday", "Thorsday",
            "Freyday", "Saturnight", "Sunsrest"
        };

        public static readonly string[] MonthNames =
        {
            "Зимний Свет", "Ледяной Покой", "Пробуждение",
            "Весенний Ветер", "Цветущий Сад", "Солнечный Зенит",
            "Жаркий Полдень", "Урожайная Луна", "Золотая Осень",
            "Туманный Вечер", "Тёмный Холод", "Годоворот"
        };

        // ────────────────────────────────
        //  Computed helpers
        // ────────────────────────────────

        public string WeekdayName =>
            Weekday >= 0 && Weekday < WeekdayNames.Length ? WeekdayNames[Weekday] : "?";

        public string MonthName =>
            Month >= 1 && Month <= MonthNames.Length ? MonthNames[Month - 1] : "?";

        /// <summary>Total elapsed game days since epoch (Year 1 Month 1 Day 1 = 0).</summary>
        public int TotalDaysElapsed => (Year - 1) * 360 + (DayOfYear - 1);

        // ────────────────────────────────
        //  Factory
        // ────────────────────────────────

        public static GameTimeData Epoch => new GameTimeData
        {
            Year = 1,
            Month = 1,
            Day = 1,
            DayOfYear = 1,
            Weekday = 0 // Manday
        };

        // ────────────────────────────────
        //  Calendar advance
        // ────────────────────────────────

        /// <summary>Advance by one game day. Returns bitmask of what changed.</summary>
        [Flags]
        public enum Changed : byte
        {
            None = 0,
            Day = 1 << 0,
            Week = 1 << 1,
            Month = 1 << 2,
            Year = 1 << 3
        }

        public Changed AdvanceDay()
        {
            Changed changed = Changed.Day;

            Day++;
            DayOfYear++;
            Weekday = (Weekday + 1) % 7;

            if (Weekday == 0) changed |= Changed.Week;

            if (Day > 30)
            {
                Day = 1;
                Month++;
                changed |= Changed.Month;

                if (Month > 12)
                {
                    Month = 1;
                    DayOfYear = 1;
                    Year++;
                    changed |= Changed.Year;
                }
            }

            return changed;
        }

        // ────────────────────────────────
        //  Netcode serialization
        // ────────────────────────────────

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Year);
            serializer.SerializeValue(ref Month);
            serializer.SerializeValue(ref Day);
            serializer.SerializeValue(ref DayOfYear);
            serializer.SerializeValue(ref Weekday);
        }
    }
}
