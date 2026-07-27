using System;
using ProjectC.Core.TimePersistence;
using Unity.Netcode;
using UnityEngine;

namespace ProjectC.Core
{
    /// <summary>
    /// Server-authoritative weather controller.
    /// Broadcasts wind updates to all clients at 0.5 Hz (every 2 seconds).
    /// Manages game calendar and persists time state to disk.
    /// Must be on a NetworkObject with server authority.
    /// </summary>
    public class ServerWeatherController : NetworkBehaviour
    {
        private ITimeRepository _timeRepo;
        private float _nextSaveTime;
        private const float TIME_SAVE_INTERVAL = 30f;
        [Header("Wind Settings")]
        [SerializeField] private Vector3 _windDirection = Vector3.right;
        [SerializeField] private float _windSpeed = 0f;

        [Header("Broadcast")]
        [SerializeField] private float _broadcastInterval = 2f;

        [Header("Variation")]
        [SerializeField] private bool _enableWindVariation = true;
        [SerializeField] private float _directionVariationAngle = 15f;
        [SerializeField] private float _speedVariationPercent = 0.2f;

        private float _timer = 0f;

        public static ServerWeatherController Instance { get; private set; }

        [Header("Time of Day")]
        [SerializeField] private float _timeOfDay = 12f;
        [SerializeField] private float _dayCycleRealHours = 1f;
        [SerializeField] private bool _enableTimeAutoAdvance = true;
        [SerializeField] private float _timeBroadcastInterval = 5f;
        private float _timeTimer = 0f;

        [Header("Calendar")]
        [SerializeField] private CalendarConfig _calendarConfig;
        [SerializeField] private GameTimeData _gameTime = default;
        [SerializeField] private float _calendarBroadcastInterval = 10f;
        private float _calendarTimer = 0f;

        [Header("Temperature")]
        [SerializeField] private float _temperature = 20f;
        [SerializeField] private float _tempBroadcastInterval = 10f;
        private float _tempTimer = 0f;

        // Events for clients to subscribe
        public event System.Action<float> OnTimeOfDayChanged;
        public event System.Action<float> OnTemperatureChanged;
        public event System.Action<GameTimeData> OnCalendarChanged;

        public float TimeOfDay => _timeOfDay;
        public float Temperature => _temperature;

        /// <summary>Total elapsed game days as float (for backwards compat — MoonController, DayNightController).</summary>
        public float TotalGameDays => _gameTime.TotalDaysElapsed(_calendarConfig?.daysPerMonth ?? 30, _calendarConfig?.monthsPerYear ?? 12) + _timeOfDay / 24f;

        public float DayCycleRealHours => _dayCycleRealHours;

        /// <summary>Current calendar config (editable in Inspector).</summary>
        public CalendarConfig CalendarConfig => _calendarConfig;

        /// <summary>Current calendar state (server-authoritative).</summary>
        public GameTimeData CurrentGameTime => _gameTime;

        public int CurrentYear => _gameTime.Year;
        public int CurrentMonth => _gameTime.Month;
        public int CurrentDay => _gameTime.Day;
        public int CurrentWeekday => _gameTime.Weekday;

        // Convenience name helpers (delegate to CalendarConfig)
        public string GetWeekdayName(int weekday) => _calendarConfig != null ? _calendarConfig.GetWeekdayName(weekday) : weekday.ToString();
        public string GetMonthName(int month) => _calendarConfig != null ? _calendarConfig.GetMonthName(month) : month.ToString();
        public string CurrentWeekdayName => GetWeekdayName(_gameTime.Weekday);
        public string CurrentMonthName => GetMonthName(_gameTime.Month);

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                Instance = this;
            }

            if (!IsServer)
            {
                enabled = false;
                return;
            }

            // Init calendar from persistence or epoch
            _timeRepo = new JsonTimeRepository();
            if (_timeRepo.TryLoad(out var savedTime, out var savedTimeOfDay))
            {
                _gameTime = savedTime;
                _timeOfDay = savedTimeOfDay;
                Debug.Log($"[ServerWeatherController] Restored time: {CurrentWeekdayName}, Day {_gameTime.Day} of {CurrentMonthName}, Year {_gameTime.Year} | {_timeOfDay:F2}h");
            }
            else if (_gameTime.Year == 0)
            {
                _gameTime = GameTimeData.Epoch;
                Debug.Log("[ServerWeatherController] Starting from epoch — Manday, Day 1 of Зимний Свет, Year 1");
            }

            _nextSaveTime = Time.time + TIME_SAVE_INTERVAL;

            ApplyWindToLocal(_windDirection, _windSpeed);
            BroadcastTimeOfDayClientRpc(_timeOfDay);
            BroadcastCalendarClientRpc(_gameTime);
            BroadcastTemperatureClientRpc(_temperature);
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            if (!IsServer) return;

            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            {
                return;
            }

            _timer += Time.deltaTime;
            if (_timer >= _broadcastInterval)
            {
                BroadcastWindClientRpc(_windDirection, _windSpeed);
                _timer = 0f;
            }

            if (_enableWindVariation)
            {
                ApplyWindVariation();
            }

            if (_enableTimeAutoAdvance && IsServer)
            {
                float gameHoursPerRealSecond = 24f / (_dayCycleRealHours * 3600f);
                _timeOfDay += gameHoursPerRealSecond * Time.deltaTime;
                if (_timeOfDay >= 24f)
                {
                    _timeOfDay -= 24f;
                    AdvanceCalendar();
                }
            }

            _timeTimer += Time.deltaTime;
            if (_timeTimer >= _timeBroadcastInterval)
            {
                BroadcastTimeOfDayClientRpc(_timeOfDay);
                _timeTimer = 0f;
            }

            _calendarTimer += Time.deltaTime;
            if (_calendarTimer >= _calendarBroadcastInterval)
            {
                BroadcastCalendarClientRpc(_gameTime);
                _calendarTimer = 0f;
            }

            _tempTimer += Time.deltaTime;
            if (_tempTimer >= _tempBroadcastInterval)
            {
                BroadcastTemperatureClientRpc(_temperature);
                _tempTimer = 0f;
            }

            // Periodic persistence
            if (Time.time >= _nextSaveTime)
            {
                _nextSaveTime = Time.time + TIME_SAVE_INTERVAL;
                _timeRepo?.Save(_gameTime, _timeOfDay);
            }
        }

        private void ApplyWindToLocal(Vector3 direction, float speed)
        {
            if (WindManager.Instance != null)
            {
                WindManager.Instance.ApplyWindUpdate(direction, speed);
            }
            else
            {
                Debug.LogError("[ServerWeatherController] WindManager.Instance is NULL on server! Check script execution order.");
            }
        }

        private void ApplyWindVariation()
        {
            float angleOffset = Mathf.Sin(Time.time * 0.1f) * _directionVariationAngle;
            Quaternion rot = Quaternion.Euler(0, angleOffset, 0);
            _windDirection = (rot * _windDirection).normalized;

            float speedMod = 1f + Mathf.Sin(Time.time * 0.15f) * _speedVariationPercent;
            float newSpeed = _windSpeed * speedMod;

            _windSpeed = Mathf.Clamp(newSpeed, 1f, 100f);

            ApplyWindToLocal(_windDirection, _windSpeed);
        }

        [ClientRpc]
        private void BroadcastWindClientRpc(Vector3 direction, float speed)
        {
            if (WindManager.Instance != null)
            {
                WindManager.Instance.ApplyWindUpdate(direction, speed);
            }
            else
            {
                Debug.LogWarning("[ServerWeatherController] WindManager.Instance is null on client");
            }
        }

        /// <summary>
        /// Called by server-side systems to change wind
        /// </summary>
        public void SetWind(Vector3 direction, float speed)
        {
            if (!IsServer) return;
            _windDirection = direction.normalized;
            _windSpeed = Mathf.Max(0f, speed);
            ApplyWindToLocal(_windDirection, _windSpeed);
        }

        /// <summary>
        /// Change wind over time (for weather transitions)
        /// </summary>
        public void TransitionWind(Vector3 targetDirection, float targetSpeed, float duration)
        {
            if (!IsServer) return;
            StartCoroutine(TransitionWindCoroutine(targetDirection, targetSpeed, duration));
        }

        private System.Collections.IEnumerator TransitionWindCoroutine(Vector3 targetDirection, float targetSpeed, float duration)
        {
            Vector3 startDir = _windDirection;
            float startSpeed = _windSpeed;
            float elapsed = 0f;

            targetDirection = targetDirection.normalized;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float smoothT = Mathf.SmoothStep(0f, 1f, t);

                _windDirection = Vector3.Lerp(startDir, targetDirection, smoothT).normalized;
                _windSpeed = Mathf.Lerp(startSpeed, targetSpeed, smoothT);

                ApplyWindToLocal(_windDirection, _windSpeed);

                yield return null;
            }

            _windDirection = targetDirection.normalized;
            _windSpeed = Mathf.Max(0f, targetSpeed);
            ApplyWindToLocal(_windDirection, _windSpeed);
        }

        // ────────────────────────────────
        //  Calendar
        // ────────────────────────────────

        private void AdvanceCalendar()
        {
            int dpm = _calendarConfig != null ? _calendarConfig.daysPerMonth : 30;
            int mpy = _calendarConfig != null ? _calendarConfig.monthsPerYear : 12;
            int dpw = _calendarConfig != null ? _calendarConfig.daysPerWeek : 7;

            var changed = _gameTime.AdvanceDay(dpm, mpy, dpw);

            if ((changed & GameTimeData.Changed.Year) != 0)
                WorldEventBus.Publish(new GameYearChangedEvent { PlayerId = 0, TimestampUnix = NowUnix(), Year = _gameTime.Year });

            if ((changed & GameTimeData.Changed.Month) != 0)
                WorldEventBus.Publish(new GameMonthChangedEvent { PlayerId = 0, TimestampUnix = NowUnix(), Month = _gameTime.Month, Year = _gameTime.Year });

            if ((changed & GameTimeData.Changed.Week) != 0)
                WorldEventBus.Publish(new GameWeekChangedEvent { PlayerId = 0, TimestampUnix = NowUnix(), Day = _gameTime.Day, Month = _gameTime.Month, Year = _gameTime.Year });

            if ((changed & GameTimeData.Changed.Day) != 0)
                WorldEventBus.Publish(new GameDayChangedEvent { PlayerId = 0, TimestampUnix = NowUnix(), Day = _gameTime.Day, Month = _gameTime.Month, Year = _gameTime.Year, Weekday = _gameTime.Weekday });

            OnCalendarChanged?.Invoke(_gameTime);
        }

        public void SetGameTime(GameTimeData data, float timeOfDay)
        {
            if (!IsServer) return;
            _gameTime = data;
            _timeOfDay = Mathf.Clamp(timeOfDay, 0f, 23.999f);
            BroadcastCalendarClientRpc(_gameTime);
            BroadcastTimeOfDayClientRpc(_timeOfDay);
        }

        private static long NowUnix() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // ────────────────────────────────
        //  ClientRpc broadcasts
        // ────────────────────────────────

        [ClientRpc]
        private void BroadcastTimeOfDayClientRpc(float time)
        {
            _timeOfDay = time;
            OnTimeOfDayChanged?.Invoke(time);
        }

        [ClientRpc]
        private void BroadcastCalendarClientRpc(GameTimeData data)
        {
            _gameTime = data;
            OnCalendarChanged?.Invoke(data);
        }

        [ClientRpc]
        private void BroadcastTemperatureClientRpc(float temp)
        {
            _temperature = temp;
            OnTemperatureChanged?.Invoke(temp);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void SetTimeOfDayServerRpc(float time)
        {
            _timeOfDay = Mathf.Repeat(time, 24f);
            BroadcastTimeOfDayClientRpc(_timeOfDay);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void SetTemperatureServerRpc(float temp)
        {
            _temperature = temp;
            BroadcastTemperatureClientRpc(_temperature);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void SetDayCycleSpeedServerRpc(float realHoursForFullCycle)
        {
            _dayCycleRealHours = realHoursForFullCycle;
        }
    }
}