using System;
using System.Collections.Generic;
using ProjectC.Trade.Core;
using UnityEngine;
using UnityEngine.Events;

namespace ProjectC.Trade.Network
{
    /// <summary>
    /// Серверный тикатор рынка. Аналог <see cref="ProjectC.Core.ServerWeatherController"/>,
    /// но для торговли.
    ///
    /// • <see cref="MarketTimeMultiplier"/> — пользовательский множитель (для отладки / демо).
    ///   1.0 = базовая скорость (тик каждые 5 минут).
    ///   10.0 = ускорение в 10 раз (тик каждые 30 сек).
    ///   0.1 = замедление.
    ///
    /// • Time-based decay: при изменении multiplier частота тиков меняется, но
    ///   физика затухания (half-life в секундах) остаётся прежней. Это значит,
    ///   что при multiplier=10x цены реально движутся быстрее, а не «то же
    ///   самое чаще» (как в старой системе).
    ///
    /// • Опционально подписан на <see cref="ProjectC.Core.ServerWeatherController"/>
    ///   для учёта time-of-day. По умолчанию выключено — чтобы не ломать баланс.
    ///
    /// Ставится в Bootstrap сцене рядом с NetworkManager.
    /// </summary>
    public class MarketTimeService : MonoBehaviour
    {
        public static MarketTimeService Instance { get; private set; }

        [Header("Tick Settings")]
        [SerializeField] private float baseTickIntervalSeconds = 300f;   // 5 мин
        [SerializeField] private float minTickIntervalSeconds = 1f;     // защита
        [SerializeField] private float maxTickIntervalSeconds = 3600f;  // 1 час (при slow)

        [Header("User Multiplier")]
        [Tooltip("Пользовательский множитель. 1.0 = базовая скорость, 10.0 = ускорить в 10 раз")]
        [SerializeField, Range(0.1f, 100f)] private float marketTimeMultiplier = 1f;

        [Header("Weather Coupling (optional)")]
        [Tooltip("Если true — multiplier умножается на weather-фактор (день=1, ночь=0.5)")]
        [SerializeField] private bool useWeatherFactor = false;
        [SerializeField] private ProjectC.Core.ServerWeatherController weatherController;

        [Header("Events")]
        public UnityEvent onMarketTick = new UnityEvent();   // серверный подписчик (TradeWorld.MarketTick)

        public float MarketTimeMultiplier
        {
            get => marketTimeMultiplier;
            set
            {
                marketTimeMultiplier = Mathf.Clamp(value, 0.1f, 100f);
                RecomputeInterval();
            }
        }

        public float CurrentTickInterval { get; private set; } = 300f;
        public float SecondsUntilNextTick => Mathf.Max(0f, CurrentTickInterval - _tickTimer);
        public int TotalTicksFired { get; private set; }

        private float _tickTimer;
        private bool _isServer;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            RecomputeInterval();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void OnServerStarted()
        {
            _isServer = true;
            _tickTimer = 0f;
            RecomputeInterval();
        }

        public void OnServerStopped()
        {
            _isServer = false;
        }

        private void Update()
        {
            if (!_isServer) return;
            if (TradeWorld.Instance == null) return;

            _tickTimer += Time.deltaTime;
            if (_tickTimer >= CurrentTickInterval)
            {
                _tickTimer = 0f;
                TotalTicksFired++;
                float dt = CurrentTickInterval; // на сервере time-based
                TradeWorld.Instance.MarketTick(dt);
                onMarketTick?.Invoke();
                if (useWeatherFactor) RecomputeInterval();  // пересчитать на случай смены времени суток
            }
        }

        private void RecomputeInterval()
        {
            float mult = marketTimeMultiplier;
            if (useWeatherFactor) mult *= ComputeWeatherFactor();
            float interval = baseTickIntervalSeconds / Mathf.Max(0.0001f, mult);
            CurrentTickInterval = Mathf.Clamp(interval, minTickIntervalSeconds, maxTickIntervalSeconds);
        }

        /// <summary>
        /// Дневной множитель: днём 1.0, ночью 0.5, плавно между.
        /// </summary>
        private float ComputeWeatherFactor()
        {
            if (weatherController == null) return 1f;
            float hour = weatherController.TimeOfDay;
            // Пик 1.0 в 12:00, минимум 0.5 в 00:00
            // 24-hour cosine, peak at 12, min at 0/24
            float t = (hour - 12f) / 12f * Mathf.PI;
            return Mathf.Lerp(0.5f, 1.0f, (Mathf.Cos(t) + 1f) * 0.5f);
        }

        // ========================================================
        // RPC для изменения множителя (отладка)
        // ========================================================

        /// <summary>
        /// Серверный вызов: установить множитель.
        /// </summary>
        public void SetMultiplierServer(float multiplier)
        {
            if (!Network.NetworkingUtils.IsServerSafe()) return;
            MarketTimeMultiplier = multiplier;
        }

        /// <summary>
        /// Клиентский вызов: попросить сервер изменить множитель.
        /// </summary>
        public void RequestSetMultiplier(float multiplier)
        {
            if (Network.NetworkingUtils.IsServerSafe())
            {
                SetMultiplierServer(multiplier);
            }
            // Remote-client branch — handled by MarketServer RPC, добавлю ниже
        }
    }

    /// <summary>
    /// Утилиты для проверки сетевого контекста, чтобы не зависеть от
    /// NetworkManager.Singleton в каждом файле.
    /// </summary>
    public static class NetworkingUtils
    {
        public static bool IsServerSafe()
        {
            var nm = Unity.Netcode.NetworkManager.Singleton;
            return nm != null && nm.IsListening && nm.IsServer;
        }
        public static bool IsClientSafe()
        {
            var nm = Unity.Netcode.NetworkManager.Singleton;
            return nm != null && nm.IsListening;
        }
    }
}
