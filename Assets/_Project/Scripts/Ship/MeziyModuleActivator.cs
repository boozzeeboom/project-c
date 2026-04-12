using System.Collections.Generic;
using UnityEngine;

namespace ProjectC.Ship
{
    /// <summary>
    /// Состояние активного мезиевого эффекта.
    /// </summary>
    public class MeziyState
    {
        public string moduleId;
        public float timeRemaining;       // Оставшееся время эффекта
        public float cooldownRemaining;   // Оставшийся кулдаун

        public bool isActive => timeRemaining > 0;
        public bool isOnCooldown => cooldownRemaining > 0;
    }

    /// <summary>
    /// MeziyModuleActivator — активатор мезиевых модулей.
    /// Сессия 5: Meziy Thrust & Advanced Modules.
    /// 
    /// Логика активации:
    /// 1. Проверить что модуль установлен в слоте
    /// 2. Проверить что не на cooldown
    /// 3. Проверить что достаточно топлива (meziyFuelCost)
    /// 4. Списать топливо
    /// 5. Запустить эффект (meziyForce × meziyDuration)
    /// 6. Запустить cooldown (meziyCooldown)
    /// 
    /// Параметры мезиевых модулей (из ShipRegistry):
    ///   MODULE_MEZIY_ROLL:  force=25, duration=2s,   cooldown=10s,  fuelCost=5
    ///   MODULE_MEZIY_PITCH: force=10, duration=1.5s, cooldown=8s,   fuelCost=5
    ///   MODULE_MEZIY_YAW:   force=30, duration=0.5s, cooldown=12s,  fuelCost=5
    /// </summary>
    public class MeziyModuleActivator : MonoBehaviour
    {
        [Header("Ссылки")]
        [Tooltip("Система топлива корабля")]
        [SerializeField] private ShipFuelSystem fuelSystem;

        [Tooltip("Менеджер модулей корабля")]
        [SerializeField] private ShipModuleManager moduleManager;

        /// <summary>
        /// Активные мезиевые эффекты (key = moduleId).
        /// </summary>
        private Dictionary<string, MeziyState> activeMeziyEffects = new();

        /// <summary>
        /// Кулдауны модулей (key = moduleId).
        /// </summary>
        private Dictionary<string, float> cooldowns = new();

        /// <summary>
        /// Активировать мезиевый модуль.
        /// Возвращает true если активация успешна.
        /// </summary>
        public bool ActivateModule(ShipModule meziyModule)
        {
            if (meziyModule == null || !meziyModule.isMeziyModule)
            {
                Debug.LogWarning("[MeziyModuleActivator] Module is not a meziy module.");
                return false;
            }

            string moduleId = meziyModule.moduleId;

            // 1. Проверить что модуль установлен в слоте
            if (!IsModuleInstalled(moduleId))
            {
                Debug.LogWarning($"[MeziyModuleActivator] Module '{moduleId}' is not installed.");
                return false;
            }

            // 2. Проверить что не на cooldown
            if (IsOnCooldown(moduleId))
            {
                Debug.LogWarning($"[MeziyModuleActivator] Module '{moduleId}' is on cooldown ({GetCooldownRemaining(moduleId):F1}s remaining).");
                return false;
            }

            // 3. Проверить что достаточно топлива
            if (fuelSystem != null && fuelSystem.CurrentFuel < meziyModule.meziyFuelCost)
            {
                Debug.LogWarning($"[MeziyModuleActivator] Not enough fuel. Need: {meziyModule.meziyFuelCost}, Have: {fuelSystem.CurrentFuel:F1}");
                return false;
            }

            // 4. Списать топливо
            if (fuelSystem != null)
            {
                bool success = fuelSystem.ConsumeFuel(meziyModule.meziyFuelCost);
                if (!success)
                {
                    Debug.LogWarning($"[MeziyModuleActivator] Failed to consume fuel for '{moduleId}'.");
                    return false;
                }
            }

            // 5. Запустить эффект
            var state = new MeziyState
            {
                moduleId = moduleId,
                timeRemaining = meziyModule.meziyDuration,
                cooldownRemaining = 0f
            };
            activeMeziyEffects[moduleId] = state;

            // 6. Запустить cooldown
            cooldowns[moduleId] = meziyModule.meziyCooldown;

            Debug.Log($"[MeziyModuleActivator] Activated '{moduleId}'. Force: {meziyModule.meziyForce}, Duration: {meziyModule.meziyDuration}s, Cooldown: {meziyModule.meziyCooldown}s, Fuel: -{meziyModule.meziyFuelCost}");
            return true;
        }

        /// <summary>
        /// Проверить, находится ли модуль на кулдауне.
        /// </summary>
        public bool IsOnCooldown(string moduleId)
        {
            return cooldowns.ContainsKey(moduleId) && cooldowns[moduleId] > 0;
        }

        /// <summary>
        /// Получить оставшееся время кулдауна модуля.
        /// </summary>
        public float GetCooldownRemaining(string moduleId)
        {
            return cooldowns.ContainsKey(moduleId) ? cooldowns[moduleId] : 0f;
        }

        /// <summary>
        /// Обновить кулдауны и активные эффекты.
        /// Вызывается каждый FixedUpdate.
        /// </summary>
        public void UpdateCooldowns(float dt)
        {
            // Обновить активные эффекты
            var toRemove = new List<string>();
            foreach (var kvp in activeMeziyEffects)
            {
                var state = kvp.Value;
                state.timeRemaining -= dt;
                if (state.timeRemaining <= 0)
                {
                    toRemove.Add(kvp.Key);
                }
            }
            foreach (var key in toRemove)
            {
                activeMeziyEffects.Remove(key);
                Debug.Log($"[MeziyModuleActivator] Effect '{key}' expired.");
            }

            // Обновить кулдауны
            var cooldownsToRemove = new List<string>();
            foreach (var kvp in cooldowns)
            {
                cooldowns[kvp.Key] -= dt;
                if (cooldowns[kvp.Key] <= 0)
                {
                    cooldownsToRemove.Add(kvp.Key);
                }
            }
            foreach (var key in cooldownsToRemove)
            {
                cooldowns.Remove(key);
                Debug.Log($"[MeziyModuleActivator] Cooldown '{key}' expired. Module ready.");
            }
        }

        /// <summary>
        /// Получить данные активного эффекта по moduleId.
        /// Возвращает null если эффект не активен.
        /// </summary>
        public MeziyState GetActiveEffect(string moduleId)
        {
            return activeMeziyEffects.ContainsKey(moduleId) ? activeMeziyEffects[moduleId] : null;
        }

        /// <summary>
        /// Получить все активные эффекты.
        /// </summary>
        public Dictionary<string, MeziyState> GetActiveEffects()
        {
            return activeMeziyEffects;
        }

        /// <summary>
        /// Проверить, установлен ли модуль с данным ID в какой-либо слот.
        /// </summary>
        private bool IsModuleInstalled(string moduleId)
        {
            if (moduleManager == null) return false;

            foreach (var slot in moduleManager.slots)
            {
                if (slot != null && slot.isOccupied && slot.installedModule.moduleId == moduleId)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Найти мезиевый модуль по ID среди установленных.
        /// </summary>
        public ShipModule FindInstalledMeziyModule(string moduleId)
        {
            if (moduleManager == null) return null;

            foreach (var slot in moduleManager.slots)
            {
                if (slot != null && slot.isOccupied && slot.installedModule.moduleId == moduleId)
                {
                    return slot.installedModule;
                }
            }
            return null;
        }
    }
}
