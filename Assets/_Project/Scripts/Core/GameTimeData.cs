using System;
using Unity.Netcode;
using UnityEngine;

namespace ProjectC.Core
{
    /// <summary>
    /// Server-authoritative game calendar data (pure data struct).
    /// Calendar rules (daysPerMonth, monthsPerYear, names) live in CalendarConfig.
    /// </summary>
    [System.Serializable]
    public struct GameTimeData : INetworkSerializable
    {
        [Tooltip("Game year (1-based)")]
        public int Year;

        [Tooltip("Month of year (1-based)")]
        public int Month;

        [Tooltip("Day of month (1-based)")]
        public int Day;

        [Tooltip("Day of year (1-based)")]
        public int DayOfYear;

        [Tooltip("Day of week (0-based)")]
        public int Weekday;

        // ────────────────────────────────
        //  Factory
        // ────────────────────────────────

        public static GameTimeData Epoch => new GameTimeData
        {
            Year = 1,
            Month = 1,
            Day = 1,
            DayOfYear = 1,
            Weekday = 0
        };

        // ────────────────────────────────
        //  Calendar advance
        // ────────────────────────────────

        /// <summary>Bitmask of what changed during day advance.</summary>
        [Flags]
        public enum Changed : byte
        {
            None = 0,
            Day = 1 << 0,
            Week = 1 << 1,
            Month = 1 << 2,
            Year = 1 << 3
        }

        /// <summary>
        /// Advance by one game day.
        /// </summary>
        /// <param name="daysPerMonth">From CalendarConfig.daysPerMonth</param>
        /// <param name="monthsPerYear">From CalendarConfig.monthsPerYear</param>
        /// <param name="daysPerWeek">From CalendarConfig.daysPerWeek</param>
        public Changed AdvanceDay(int daysPerMonth, int monthsPerYear, int daysPerWeek)
        {
            Changed changed = Changed.Day;

            Day++;
            DayOfYear++;
            Weekday = (Weekday + 1) % daysPerWeek;

            if (Weekday == 0) changed |= Changed.Week;

            if (Day > daysPerMonth)
            {
                Day = 1;
                Month++;
                changed |= Changed.Month;

                if (Month > monthsPerYear)
                {
                    Month = 1;
                    DayOfYear = 1;
                    Year++;
                    changed |= Changed.Year;
                }
            }

            return changed;
        }

        /// <summary>
        /// Total elapsed game days since epoch (Year 1 Month 1 Day 1 = 0).
        /// Requires calendar config for calculation.
        /// </summary>
        public int TotalDaysElapsed(int daysPerMonth, int monthsPerYear)
        {
            return (Year - 1) * monthsPerYear * daysPerMonth
                 + (Month - 1) * daysPerMonth
                 + (Day - 1);
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
