using System;
using System.IO;
using UnityEngine;

namespace ProjectC.Core.TimePersistence
{
    /// <summary>
    /// JSON-backed time persistence. Saves to Application.persistentDataPath/time_state.json.
    /// Follows the same pattern as JsonShipPositionRepository.
    /// </summary>
    public class JsonTimeRepository : ITimeRepository
    {
        private readonly string _filePath;

        public JsonTimeRepository()
        {
            _filePath = Path.Combine(Application.persistentDataPath, "time_state.json");
        }

        public void Save(GameTimeData data, float timeOfDay)
        {
            try
            {
                var dto = new TimeStateDto
                {
                    year = data.Year,
                    month = data.Month,
                    day = data.Day,
                    dayOfYear = data.DayOfYear,
                    weekday = data.Weekday,
                    timeOfDay = timeOfDay
                };
                string json = JsonUtility.ToJson(dto, prettyPrint: false);
                File.WriteAllText(_filePath, json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[JsonTimeRepository] Failed to save time state: {ex}");
            }
        }

        public bool TryLoad(out GameTimeData data, out float timeOfDay)
        {
            data = GameTimeData.Epoch;
            timeOfDay = 12f;

            if (!File.Exists(_filePath))
                return false;

            try
            {
                string json = File.ReadAllText(_filePath);
                var dto = JsonUtility.FromJson<TimeStateDto>(json);

                if (dto == null || dto.year <= 0)
                    return false;

                data = new GameTimeData
                {
                    Year = dto.year,
                    Month = dto.month,
                    Day = dto.day,
                    DayOfYear = dto.dayOfYear,
                    Weekday = dto.weekday
                };

                timeOfDay = Mathf.Clamp(dto.timeOfDay, 0f, 23.999f);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[JsonTimeRepository] Failed to load time state (will use epoch): {ex.Message}");
                return false;
            }
        }

        [System.Serializable]
        private class TimeStateDto
        {
            public int year;
            public int month;
            public int day;
            public int dayOfYear;
            public int weekday;
            public float timeOfDay;
        }
    }
}
