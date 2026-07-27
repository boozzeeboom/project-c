using UnityEngine;

namespace ProjectC.Core
{
    /// <summary>
    /// Configurable calendar rules and locale names.
    /// Assign to ServerWeatherController._calendarConfig in the Inspector.
    /// </summary>
    [CreateAssetMenu(fileName = "CalendarConfig", menuName = "ProjectC/Calendar/CalendarConfig")]
    public class CalendarConfig : ScriptableObject
    {
        [Header("Calendar Rules")]
        [Tooltip("Game days per month")]
        [Min(1)]
        public int daysPerMonth = 30;

        [Tooltip("Game months per year")]
        [Min(1)]
        public int monthsPerYear = 12;

        [Tooltip("Days per week (determines WeekdayNames length)")]
        [Min(1)]
        public int daysPerWeek = 7;

        // ────────────────────────────────
        //  Names
        // ────────────────────────────────

        [Header("Names")]
        public string[] weekdayNames = {
            "Manday", "Tirsday", "Wotanday", "Thorsday",
            "Freyday", "Saturnight", "Sunsrest"
        };

        public string[] monthNames = {
            "Зимний Свет", "Ледяной Покой", "Пробуждение",
            "Весенний Ветер", "Цветущий Сад", "Солнечный Зенит",
            "Жаркий Полдень", "Урожайная Луна", "Золотая Осень",
            "Туманный Вечер", "Тёмный Холод", "Годоворот"
        };

        // ────────────────────────────────
        //  Derived
        // ────────────────────────────────

        public int DaysPerYear => daysPerMonth * monthsPerYear;

        // ────────────────────────────────
        //  Helpers
        // ────────────────────────────────

        public string GetWeekdayName(int weekday)
        {
            if (weekdayNames == null || weekdayNames.Length == 0)
                return weekday.ToString();
            return weekday >= 0 && weekday < weekdayNames.Length
                ? weekdayNames[weekday]
                : weekday.ToString();
        }

        public string GetMonthName(int month) // month is 1-based
        {
            if (monthNames == null || monthNames.Length == 0)
                return month.ToString();
            int idx = month - 1;
            return idx >= 0 && idx < monthNames.Length
                ? monthNames[idx]
                : month.ToString();
        }

        // ────────────────────────────────
        //  Validation
        // ────────────────────────────────

        public bool IsValid(out string error)
        {
            if (weekdayNames == null || weekdayNames.Length != daysPerWeek)
            {
                error = $"weekdayNames length ({weekdayNames?.Length ?? 0}) != daysPerWeek ({daysPerWeek})";
                return false;
            }
            if (monthNames == null || monthNames.Length != monthsPerYear)
            {
                error = $"monthNames length ({monthNames?.Length ?? 0}) != monthsPerYear ({monthsPerYear})";
                return false;
            }
            error = null;
            return true;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Keep arrays in sync with count fields
            if (weekdayNames == null || weekdayNames.Length != daysPerWeek)
            {
                var old = weekdayNames ?? new string[0];
                System.Array.Resize(ref weekdayNames, daysPerWeek);
                for (int i = old.Length; i < daysPerWeek; i++)
                    weekdayNames[i] = $"Day{i}";
            }
            if (monthNames == null || monthNames.Length != monthsPerYear)
            {
                var old = monthNames ?? new string[0];
                System.Array.Resize(ref monthNames, monthsPerYear);
                for (int i = old.Length; i < monthsPerYear; i++)
                    monthNames[i] = $"Month{i + 1}";
            }
        }
#endif
    }
}
