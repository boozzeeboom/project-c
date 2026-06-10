// CraftingTimeService.cs (T-C02) - server-only tick for crafting jobs (1Hz)
// Pattern: MarketTimeService. Singleton. Placed in Bootstrap scene.
using System;
using UnityEngine;
using UnityEngine.Events;

namespace ProjectC.Crafting
{
    /// <summary>Серверный 1Hz тикатор крафта. Только сервер: <see cref="OnTick"/>.
    /// Подписчик - <see cref="CraftingServer"/>, который пробрасывает dt в <see cref="CraftingWorld.OnTick"/>.</summary>
    public class CraftingTimeService : MonoBehaviour
    {
        public static CraftingTimeService Instance { get; private set; }

        [Header("Tick Settings")]
        [Tooltip("Базовая частота тиков. 1.0 = 1Hz (server-time секунды, не Time.realtimeSinceStartup!)")]
        [SerializeField] private float baseTickIntervalSeconds = 1f;
        [SerializeField] private float minTickIntervalSeconds = 0.1f;
        [SerializeField] private float maxTickIntervalSeconds = 10f;

        [Header("User Multiplier (debug)")]
        [Tooltip("Ускорить/замедлить крафт для отладки. 1.0 = базовая скорость, 0.1 = в 10 раз медленнее.")]
        [SerializeField, Range(0.1f, 100f)] private float craftingTimeMultiplier = 1f;

        [Header("Events")]
        [Tooltip("Серверный подписчик. Аргумент — dt в секундах (для совместимости с OnTick pattern).")]
        public UnityEvent<float> onCraftingTick = new UnityEvent<float>();

        public float CurrentTickInterval { get; private set; } = 1f;
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

        public float CraftingTimeMultiplier
        {
            get => craftingTimeMultiplier;
            set
            {
                craftingTimeMultiplier = Mathf.Clamp(value, 0.1f, 100f);
                RecomputeInterval();
            }
        }

        private void Update()
        {
            if (!_isServer) return;
            _tickTimer += Time.deltaTime;
            if (_tickTimer >= CurrentTickInterval)
            {
                _tickTimer = 0f;
                TotalTicksFired++;
                // dt для CraftingWorld = interval * multiplier (server-time секунды)
                float dt = CurrentTickInterval;
                onCraftingTick?.Invoke(dt);
            }
        }

        private void RecomputeInterval()
        {
            float mult = Mathf.Max(0.0001f, craftingTimeMultiplier);
            float interval = baseTickIntervalSeconds / mult;
            CurrentTickInterval = Mathf.Clamp(interval, minTickIntervalSeconds, maxTickIntervalSeconds);
        }
    }
}