using UnityEngine;
using ProjectC.Player;

namespace ProjectC.Ship
{
    /// <summary>
    /// ShipFuelSystem — система топлива корабля.
    /// Сессия 5: Meziy Thrust & Advanced Modules.
    /// 
    /// Топливо расходуется при работе двигателя (thrust > 0) и восстанавливается на idle.
    /// Мезиевые модули потребляют топливо отдельно через MeziyModuleActivator.
    /// При пустом топлиле двигатель глохнет (thrust = 0).
    /// 
    /// fuelCapacity по классам (из ShipRegistry):
    ///   Light (Тороид):  50
    ///   Medium (Баржа):  100
    ///   Heavy (Платформа):  200
    ///   HeavyII (Открытый): 300
    /// 
    /// Базовый расход (в секунду при тяге):
    ///   Light:  0.5 fuel/s
    ///   Medium: 0.8 fuel/s
    ///   Heavy:  1.2 fuel/s
    ///   HeavyII: 1.5 fuel/s
    /// 
    /// Восстановление (idle, тяга = 0):
    ///   Все классы: 0.3 fuel/s
    /// </summary>
    public class ShipFuelSystem : MonoBehaviour
    {
        [Header("Топливо")]
        [Tooltip("Текущий уровень топлива")]
        [SerializeField] private float currentFuel;

        [Tooltip("Максимальная ёмкость топлива (зависит от класса корабля)")]
        [SerializeField] private float maxFuel = 100f;

        [Tooltip("Базовый расход топлива за секунду полёта (при thrust > 0)")]
        [SerializeField] private float fuelConsumptionRate = 0.8f;

        [Tooltip("Скорость восстановления топлива на idle (тяга = 0)")]
        [SerializeField] private float fuelRegenRate = 0.3f;

        [Header("Атмосферная дозаправка (клавиша L)")]
        [Tooltip("Скорость дозаправки из атмосферы (fuel/s)")]
        [SerializeField] private float atmosphericRefuelRate = 2.0f;

        [Tooltip("Штраф к тяге во время дозаправки (0.5 = -50%)")]
        [SerializeField] [Range(0f, 1f)] private float thrustPenaltyDuringRefuel = 0.5f;

        [Tooltip("Штраф к скорости во время дозаправки (0.7 = -30%)")]
        [SerializeField] [Range(0f, 1f)] private float speedPenaltyDuringRefuel = 0.7f;

        /// <summary>
        /// Текущий уровень топлива.
        /// </summary>
        public float CurrentFuel => currentFuel;

        /// <summary>
        /// Максимальная ёмкость топлива.
        /// </summary>
        public float MaxFuel => maxFuel;

        /// <summary>
        /// Процент топлива (0..1).
        /// </summary>
        public float FuelPercent => maxFuel > 0 ? currentFuel / maxFuel : 0f;

        /// <summary>
        /// Топливо закончилось?
        /// </summary>
        public bool IsEmpty => currentFuel <= 0f;

        /// <summary>
        /// Топливо полное?
        /// </summary>
        public bool IsFull => currentFuel >= maxFuel;

        /// <summary>
        /// Идёт ли сейчас атмосферная дозаправка.
        /// </summary>
        public bool isRefueling { get; private set; }

        /// <summary>
        /// Штраф к тяге во время дозаправки (0..1).
        /// </summary>
        public float thrustPenaltyMult => isRefueling ? thrustPenaltyDuringRefuel : 1f;

        /// <summary>
        /// Штраф к скорости во время дозаправки (0..1).
        /// </summary>
        public float speedPenaltyMult => isRefueling ? speedPenaltyDuringRefuel : 1f;

        /// <summary>
        /// Начать атмосферную дозаправку.
        /// </summary>
        public void StartRefueling()
        {
            if (!IsFull)
                isRefueling = true;
        }

        /// <summary>
        /// Остановить атмосферную дозаправку.
        /// </summary>
        public void StopRefueling()
        {
            isRefueling = false;
        }

        /// <summary>
        /// Восстановить топливо из атмосферы (быстрее чем idle regen).
        /// Вызывается каждый FixedUpdate когда зажата L.
        /// </summary>
        public void RefuelAtmospheric(float dt)
        {
            if (IsFull || maxFuel <= 0)
            {
                isRefueling = false;
                return;
            }

            isRefueling = true;
            currentFuel = Mathf.Min(currentFuel + atmosphericRefuelRate * dt, maxFuel);
        }

        /// <summary>
        /// Потребить указанное количество топлива.
        /// Возвращает false если недостаточно топлива.
        /// </summary>
        public bool ConsumeFuel(float amount)
        {
            if (amount <= 0f) return true;

            if (currentFuel < amount)
            {
                // Недостаточно топлива — потребляем что осталось
                currentFuel = 0f;
                return false;
            }

            currentFuel -= amount;
            return true;
        }

        /// <summary>
        /// Восстановить топливо за кадр (regen на idle).
        /// Работает даже при fuel=0 — корабль восстанавливает топливо.
        /// </summary>
        public void RegenFuel(float dt)
        {
            if (maxFuel <= 0) return;

            currentFuel = Mathf.Min(currentFuel + fuelRegenRate * dt, maxFuel);
        }

        /// <summary>
        /// Расходовать топливо за кадр (при работе двигателя).
        /// </summary>
        /// <param name="dt">Время кадра</param>
        /// <param name="thrustFactor">Множитель тяги (0..1, влияет на расход)</param>
        /// <returns>false если топливо закончилось</returns>
        public bool ConsumeFuelPerSecond(float dt, float thrustFactor = 1f)
        {
            if (IsEmpty) return false;

            float consumed = fuelConsumptionRate * dt * Mathf.Clamp01(thrustFactor);
            return ConsumeFuel(consumed);
        }

        /// <summary>
        /// Заправить корабль (для доков, станций и т.д.).
        /// </summary>
        public void Refuel(float amount)
        {
            if (amount <= 0f) return;
            currentFuel = Mathf.Min(currentFuel + amount, maxFuel);
        }

        /// <summary>
        /// Полностью заправить корабль.
        /// </summary>
        public void RefuelFull()
        {
            currentFuel = maxFuel;
        }

        /// <summary>
        /// Инициализировать систему топлива с параметрами класса корабля.
        /// Вызывается из ShipController при старте.
        /// </summary>
        public void Initialize(ShipFlightClass shipClass)
        {
            // Установить ёмкость и расход по классу
            switch (shipClass)
            {
                case ShipFlightClass.Light:
                    maxFuel = 50f;
                    fuelConsumptionRate = 0.5f;
                    break;
                case ShipFlightClass.Medium:
                    maxFuel = 100f;
                    fuelConsumptionRate = 0.8f;
                    break;
                case ShipFlightClass.Heavy:
                    maxFuel = 200f;
                    fuelConsumptionRate = 1.2f;
                    break;
                case ShipFlightClass.HeavyII:
                    maxFuel = 300f;
                    fuelConsumptionRate = 1.5f;
                    break;
            }

            // Полная заправка при старте
            currentFuel = maxFuel;

            Debug.Log($"[ShipFuelSystem] Initialized. Class: {shipClass}, Capacity: {maxFuel}, Consumption: {fuelConsumptionRate}/s");
        }
    }
}
