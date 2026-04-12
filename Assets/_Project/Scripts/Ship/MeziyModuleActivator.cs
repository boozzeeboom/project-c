using System.Collections.Generic;
using UnityEngine;

namespace ProjectC.Ship
{
    /// <summary>
    /// Ось мезиевого модуля — для запроса пассивных множителей.
    /// </summary>
    public enum MeziyAxis { Pitch, Roll, Yaw, Thrust }

    /// <summary>
    /// Состояние мезиевого модуля с passive/active/overheat режимами.
    /// </summary>
    public class MeziyContinuousState
    {
        public ShipModule module;

        // Режимы
        public bool isPassive;       // модуль установлен — пассивный эффект всегда активен
        public bool isActive;        // клавиша направления зажата — активный выхлоп
        public bool isOverheated;    // перегрет — кулдаун

        // Таймеры
        public float continuousActiveTime; // Время непрерывного активного использования (для перегрева)
        public float cooldownRemaining;    // Оставшийся кулдаун (перегрев)

        // Направление активного выхлопа
        public float activeDirection; // -1, 0, +1

        // Свойства
        public bool isOnCooldown => cooldownRemaining > 0f;
        public float overheatThreshold = 10f; // Секунд непрерывной работы до перегрева
    }

    /// <summary>
    /// MeziyModuleActivator — v2.6: passive/active/overheat архитектура.
    ///
    /// Принцип:
    /// - Модуль установлен → пассивный эффект (без расхода топлива, без частиц)
    ///   Пассивный эффект: небольшое усиление управления (~1.1x множитель)
    /// - Клавиша направления зажата → активный выхлоп (расход топлива, частицы, сильный torque)
    /// - Перегрев после 10 сек непрерывного активного использования → кулдаун 15 сек
    ///
    /// Управление (зажатие клавиш):
    /// - MODULE_MEZIY_PITCH: W (нос вверх, dir=-1), S (нос вниз, dir=+1)
    /// - MODULE_MEZIY_ROLL:  Z (крен влево, dir=-1), C (крен вправо, dir=+1)
    /// - MODULE_MEZIY_YAW:   A (влево, dir=-1), D (вправо, dir=+1)
    ///
    /// Расход топлива:
    /// - Пассивный: 0 fuel/s
    /// - Активный: meziyFuelCost * 2 * dt fuel/s
    /// - Перегрев: штраф meziyFuelCost, кулдаун 15 сек
    ///
    /// Частицы:
    /// - Пассивный: НЕ видны
    /// - Активный: видны (оранжевое пламя)
    /// - Перегрев: НЕ видны
    /// </summary>
    public class MeziyModuleActivator : MonoBehaviour
    {
        [Header("Ссылки")]
        [Tooltip("Система топлива корабля")]
        [SerializeField] private ShipFuelSystem fuelSystem;

        [Tooltip("Менеджер модулей корабля")]
        [SerializeField] private ShipModuleManager moduleManager;

        [Header("Настройки перегрева")]
        [Tooltip("Время непрерывной активной работы до перегрева (сек)")]
        [Min(1f)]
        [SerializeField] private float overheatThreshold = 10f;

        [Tooltip("Время охлаждения после перегрева (сек)")]
        [Min(1f)]
        [SerializeField] private float cooldownDuration = 15f;

        [Header("Пассивный эффект")]
        [Tooltip("Множитель пассивного усиления управления (1.1 = +10%)")]
        [SerializeField] private float passiveModifier = 1.1f;

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
            // Защита от 0 кулдауна (сериализованное значение может быть сброшено)
            if (cooldownDuration < 1f)
            {
                Debug.LogWarning("[MeziyModuleActivator] cooldownDuration too low, forcing to 15s.");
                cooldownDuration = 15f;
            }
            if (overheatThreshold < 1f)
            {
                Debug.LogWarning("[MeziyModuleActivator] overheatThreshold too low, forcing to 10s.");
                overheatThreshold = 10f;
            }

            meziyStates.Clear();

            if (moduleManager != null)
            {
                foreach (var slot in moduleManager.slots)
                {
                    if (slot != null && slot.isOccupied && slot.installedModule != null && slot.installedModule.isMeziyModule)
                    {
                        string moduleId = slot.installedModule.moduleId;
                        if (!meziyStates.ContainsKey(moduleId))
                        {
                            meziyStates[moduleId] = new MeziyContinuousState
                            {
                                module = slot.installedModule,
                                isPassive = true,
                                isActive = false,
                                isOverheated = false,
                                continuousActiveTime = 0f,
                                cooldownRemaining = 0f,
                                activeDirection = 0f,
                                overheatThreshold = overheatThreshold
                            };
                            Debug.Log($"[MeziyModuleActivator] Registered passive meziy module: '{moduleId}' (force={slot.installedModule.meziyForce})");
                        }
                    }
                }
            }

            Debug.Log($"[MeziyModuleActivator] Initialized {meziyStates.Count} meziy modules (passive mode).");
        }

        /// <summary>
        /// Активировать между модуль с направлением (клавиша зажата).
        /// Возвращает true если модуль активен (не на кулдауне, достаточно топлива).
        /// </summary>
        /// <param name="moduleId">ID модуля</param>
        /// <param name="direction">Направление выхлопа: -1 или +1</param>
        public bool TryActivate(string moduleId, float direction)
        {
            if (!meziyStates.ContainsKey(moduleId))
            {
                Debug.LogWarning($"[MeziyModuleActivator] Module '{moduleId}' not found! Available: {string.Join(", ", meziyStates.Keys)}");
                return false;
            }

            var state = meziyStates[moduleId];

            // На кулдауне — не активируем
            if (state.isOnCooldown) return false;

            // Проверка топлива
            if (fuelSystem != null && fuelSystem.CurrentFuel < state.module.meziyFuelCost)
            {
                Debug.LogWarning($"[MeziyModuleActivator] Not enough fuel for '{moduleId}'. Need: {state.module.meziyFuelCost}, Have: {fuelSystem.CurrentFuel:F1}");
                return false;
            }

            state.isActive = true;
            state.activeDirection = direction;
            state.isOverheated = false;
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
                meziyStates[moduleId].activeDirection = 0f;
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
                    if (state.cooldownRemaining <= 0f)
                    {
                        state.cooldownRemaining = 0f;
                        state.isOverheated = false;
                        Debug.Log($"[MeziyModuleActivator] '{kvp.Key}' cooled down. Ready to use.");
                    }
                    continue; // На кулдауне — ничего не делаем
                }

                // Обновить время непрерывной активности
                if (state.isActive)
                {
                    state.continuousActiveTime += dt;

                    // Проверить перегрев
                    if (state.continuousActiveTime >= state.overheatThreshold)
                    {
                        state.isActive = false;
                        state.activeDirection = 0f;
                        state.continuousActiveTime = 0f;
                        state.isOverheated = true;
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
                    // Сбросить таймер при отпускании клавиши
                    state.continuousActiveTime = 0f;
                    state.isOverheated = false;
                }
            }
        }

        /// <summary>
        /// Получить пассивный множитель для оси.
        /// Вызывается из ShipController.ApplyModuleModifiers().
        /// Возвращает passiveModifier (1.1) если модуль установлен, иначе 1.0.
        /// </summary>
        public float GetPassiveModifier(MeziyAxis axis)
        {
            string moduleId = axis switch
            {
                MeziyAxis.Pitch => "MODULE_MEZIY_PITCH",
                MeziyAxis.Roll => "MODULE_MEZIY_ROLL",
                MeziyAxis.Yaw => "MODULE_MEZIY_YAW",
                MeziyAxis.Thrust => "MODULE_MEZIY_THRUST",
                _ => null
            };

            if (moduleId != null && meziyStates.ContainsKey(moduleId))
            {
                var state = meziyStates[moduleId];
                if (state.isPassive && !state.isOnCooldown)
                    return passiveModifier;
            }

            return 1.0f;
        }

        /// <summary>
        /// Расходовать топливо за активные модули.
        /// Вызывается из ShipController при расчёте расхода.
        /// Пассивные модули НЕ расходуют топливо.
        /// Перегретые модули НЕ расходуют топливо.
        /// </summary>
        public void ConsumeFuelForActiveModules(float dt)
        {
            if (fuelSystem == null) return;

            foreach (var kvp in meziyStates)
            {
                var state = kvp.Value;
                // Только активные (не пассивные) и не на кулдауне
                if (state.isActive && !state.isOnCooldown)
                {
                    float cost = state.module.meziyFuelCost * dt * 2f; // x2 multiplier для активного режима
                    fuelSystem.ConsumeFuel(cost);
                }
            }
        }

        /// <summary>
        /// Проверить, перегрет ли модуль (для UI индикации).
        /// </summary>
        public bool IsOverheated(string moduleId)
        {
            return meziyStates.ContainsKey(moduleId) && meziyStates[moduleId].isOnCooldown;
        }

        /// <summary>
        /// Получить прогресс перегрева 0..1 (для UI бара).
        /// </summary>
        public float GetOverheatProgress(string moduleId)
        {
            if (!meziyStates.ContainsKey(moduleId)) return 0f;
            var state = meziyStates[moduleId];
            if (state.isActive)
                return Mathf.Clamp01(state.continuousActiveTime / state.overheatThreshold);
            return 0f;
        }

        /// <summary>
        /// Получить состояние модуля.
        /// </summary>
        public MeziyContinuousState GetState(string moduleId)
        {
            return meziyStates.ContainsKey(moduleId) ? meziyStates[moduleId] : null;
        }

        /// <summary>
        /// Получить все состояния модулей.
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
