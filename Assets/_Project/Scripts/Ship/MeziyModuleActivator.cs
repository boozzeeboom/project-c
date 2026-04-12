using System.Collections.Generic;
using UnityEngine;

namespace ProjectC.Ship
{
    /// <summary>
    /// Состояние мезиевого модуля в continuous режиме.
    /// </summary>
    public class MeziyContinuousState
    {
        public ShipModule module;
        public bool isActive;
        public float continuousActiveTime; // Время непрерывного использования (для перегрева)
        public float cooldownRemaining;    // Оставшийся кулдаун (перегрев)

        public bool isOnCooldown => cooldownRemaining > 0;
        public float overheatThreshold = 10f; // Секунд непрерывной работы до перегрева
    }

    /// <summary>
    /// MeziyModuleActivator -- continuous mode (Сессия 5_2).
    ///
    /// Новая логика:
    /// - Модули работают пока зажата клавиша (не one-shot)
    /// - Топливо расходуется с повышенным rate при активации
    /// - Перегрев после 10 сек непрерывного использования -> кулдаун на охлаждение
    /// - При перегреве модуль выключается автоматически
    ///
    /// Управление (клавиши зажатия):
    /// - MODULE_MEZIY_PITCH: W (нос вверх), S (нос вниз)
    /// - MODULE_MEZIY_ROLL:  Z (крен влево), X (крен вправо) -- Z/C уже используется для обычного roll
    /// - MODULE_MEZIY_YAW:   A (влево), D (вправо)
    ///
    /// NOTE: Для тестирования кулдаун = 0 (перегрев сразу остывает).
    /// </summary>
    public class MeziyModuleActivator : MonoBehaviour
    {
        [Header("Ссылки")]
        [Tooltip("Система топлива корабля")]
        [SerializeField] private ShipFuelSystem fuelSystem;

        [Tooltip("Менеджер модулей корабля")]
        [SerializeField] private ShipModuleManager moduleManager;

        [Header("Настройки перегрева")]
        [Tooltip("Время непрерывной работы до перегрева (сек)")]
        [SerializeField] private float overheatThreshold = 10f;

        [Tooltip("Время охлаждения после перегрева (сек, 0 = мгновенно для тестов)")]
        [SerializeField] private float cooldownDuration = 0f;

        /// <summary>
        /// Состояния всех мезиевых модулей (key = moduleId).
        /// </summary>
        private Dictionary<string, MeziyContinuousState> meziyStates = new();

        /// <summary>
        /// Инициализировать состояния модулей.
        /// Вызывается из ShipController при старте.
        /// </summary>
        public void Initialize()
        {
            meziyStates.Clear();

            if (moduleManager != null)
            {
                foreach (var slot in moduleManager.slots)
                {
                    if (slot != null && slot.isOccupied && slot.installedModule.isMeziyModule)
                    {
                        string moduleId = slot.installedModule.moduleId;
                        if (!meziyStates.ContainsKey(moduleId))
                        {
                            meziyStates[moduleId] = new MeziyContinuousState
                            {
                                module = slot.installedModule,
                                isActive = false,
                                continuousActiveTime = 0f,
                                cooldownRemaining = 0f,
                                overheatThreshold = overheatThreshold
                            };
                        }
                    }
                }
            }

            Debug.Log($"[MeziyModuleActivator] Initialized {meziyStates.Count} meziy modules.");
        }

        /// <summary>
        /// Активировать между модуль (вызывается каждый кадр пока клавиша зажата).
        /// Возвращает true если модуль активен (не на кулдауне, достаточно топлива).
        /// </summary>
        public bool TryActivate(string moduleId)
        {
            if (!meziyStates.ContainsKey(moduleId)) return false;

            var state = meziyStates[moduleId];

            // На кулдауне
            if (state.isOnCooldown) return false;

            // Уже активен -- просто продолжаем
            if (state.isActive) return true;

            // Достаточно топлива для запуска?
            if (fuelSystem != null && fuelSystem.CurrentFuel < state.module.meziyFuelCost)
            {
                Debug.LogWarning($"[MeziyModuleActivator] Not enough fuel for '{moduleId}'. Need: {state.module.meziyFuelCost}, Have: {fuelSystem.CurrentFuel:F1}");
                return false;
            }

            state.isActive = true;
            return true;
        }

        /// <summary>
        /// Деактивировать между модуль (клавиша отпущена).
        /// </summary>
        public void Deactivate(string moduleId)
        {
            if (meziyStates.ContainsKey(moduleId))
            {
                meziyStates[moduleId].isActive = false;
            }
        }

        /// <summary>
        /// Обновить состояния модулей.
        /// Вызывается каждый FixedUpdate из ShipController.
        /// Отслеживает перегрев и кулдауны.
        /// </summary>
        public void Tick(float dt)
        {
            foreach (var kvp in meziyStates)
            {
                var state = kvp.Value;

                // Обновить кулдаун
                if (state.isOnCooldown)
                {
                    state.cooldownRemaining -= dt;
                    if (state.cooldownRemaining <= 0)
                    {
                        state.cooldownRemaining = 0f;
                        Debug.Log($"[MeziyModuleActivator] '{kvp.Key}' cooled down. Ready to use.");
                    }
                    continue; // На кулдауне -- ничего не делаем
                }

                // Обновить время непрерывной активности
                if (state.isActive)
                {
                    state.continuousActiveTime += dt;

                    // Проверить перегрев
                    if (state.continuousActiveTime >= state.overheatThreshold)
                    {
                        state.isActive = false;
                        state.continuousActiveTime = 0f;
                        state.cooldownRemaining = cooldownDuration;
                        Debug.LogWarning($"[MeziyModuleActivator] '{kvp.Key}' OVERHEATED! Cooldown: {cooldownDuration:F1}s");

                        // Списать топливо за перегрев (штраф)
                        if (fuelSystem != null)
                        {
                            fuelSystem.ConsumeFuel(state.module.meziyFuelCost);
                        }
                    }
                }
                else
                {
                    // Сбросить таймер при отпускании
                    state.continuousActiveTime = 0f;
                }
            }
        }

        /// <summary>
        /// Расходовать топливо за активный модуль.
        /// Вызывается из ShipController при расчёте расхода.
        /// </summary>
        public void ConsumeFuelForActiveModules(float dt)
        {
            if (fuelSystem == null) return;

            foreach (var kvp in meziyStates)
            {
                if (kvp.Value.isActive)
                {
                    // Повышенный расход: междуyFuelCost * dt * multiplier
                    float cost = kvp.Value.module.meziyFuelCost * dt * 2f; // x2 multiplier
                    fuelSystem.ConsumeFuel(cost);
                }
            }
        }

        /// <summary>
        /// Получить состояние модуля.
        /// </summary>
        public MeziyContinuousState GetState(string moduleId)
        {
            return meziyStates.ContainsKey(moduleId) ? meziyStates[moduleId] : null;
        }

        /// <summary>
        /// Получить все активные модули.
        /// </summary>
        public Dictionary<string, MeziyContinuousState> GetActiveStates()
        {
            return meziyStates;
        }

        /// <summary>
        /// Проверить, установлен ли модуль с данным ID.
        /// </summary>
        public bool IsModuleInstalled(string moduleId)
        {
            return meziyStates.ContainsKey(moduleId);
        }

        /// <summary>
        /// Получить суммарную активность (0 = ничего, 1+ = активны модули).
        /// </summary>
        public int GetActiveCount()
        {
            int count = 0;
            foreach (var kvp in meziyStates)
            {
                if (kvp.Value.isActive) count++;
            }
            return count;
        }
    }
}
