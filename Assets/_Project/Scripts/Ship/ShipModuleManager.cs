using System.Collections.Generic;
using UnityEngine;
using ProjectC.Player;

namespace ProjectC.Ship
{
    /// <summary>
    /// ShipModuleManager — менеджер модулей на корабле.
    /// Управляет слотами, валидацией установки, применением эффектов модулей.
    /// Сессия 4: Module System Foundation.
    /// </summary>
    public class ShipModuleManager : MonoBehaviour
    {
        [Header("Слоты модулей")]
        [Tooltip("Список слотов модулей на корабле")]
        public List<ModuleSlot> slots = new List<ModuleSlot>();

        [Header("Энергия корабля")]
        [Tooltip("Доступная энергия корабля (зависит от класса)")]
        public int availablePower = 35;

        /// <summary>
        /// Текущее потребление энергии всеми установленными модулями.
        /// </summary>
        public int currentPowerUsage { get; private set; }

        /// <summary>
        /// Кэш установленного класса корабля (для валидации совместимости).
        /// Назначается из ShipController при инициализации.
        /// </summary>
        private ShipFlightClass _shipClass = ShipFlightClass.Medium;

        /// <summary>
        /// Инициализировать менеджер с классом корабля.
        /// Вызывается из ShipController.Awake().
        /// </summary>
        public void Initialize(ShipFlightClass shipClass)
        {
            _shipClass = shipClass;

            // Всегда пересинхронизируем слоты из иерархии для надёжности
            var discoveredSlots = new List<ModuleSlot>(GetComponentsInChildren<ModuleSlot>(true));
            if (discoveredSlots.Count > 0)
            {
                slots = discoveredSlots;
            }
            else if (slots == null || slots.Count == 0)
            {
                Debug.LogWarning("[ShipModuleManager] No module slots found! Check hierarchy.");
            }

            // Рассчитываем текущее потребление
            RecalculatePowerUsage();
        }

        /// <summary>
        /// Установить модуль в указанный слот.
        /// </summary>
        /// <param name="slot">Слот для установки</param>
        /// <param name="module">Модуль для установки</param>
        /// <returns>true если установка успешна</returns>
        public bool InstallModule(ModuleSlot slot, ShipModule module)
        {
            if (slot == null || module == null)
            {
                Debug.LogWarning("[ShipModuleManager] Cannot install: slot or module is null.");
                return false;
            }

            // Проверяем энергию
            if (currentPowerUsage + module.powerConsumption > availablePower)
            {
                Debug.LogWarning($"[ShipModuleManager] Not enough power. Need: {module.powerConsumption}, Available: {availablePower - currentPowerUsage}");
                return false;
            }

            // Проверяем совместимость с классом корабля
            if (!module.IsCompatibleWithClass(_shipClass))
            {
                Debug.LogWarning($"[ShipModuleManager] Module '{module.moduleId}' not compatible with ship class '{_shipClass}'.");
                return false;
            }

            // Проверяем совместимость с другими установленными модулями
            if (!ValidateModuleCompatibility(module))
            {
                Debug.LogWarning($"[ShipModuleManager] Module '{module.moduleId}' has incompatible modules installed.");
                return false;
            }

            // Проверяем требуемые модули
            List<string> installedIds = GetInstalledModuleIds();
            if (!module.AreRequiredModulesInstalled(installedIds))
            {
                Debug.LogWarning($"[ShipModuleManager] Module '{module.moduleId}' requires modules that are not installed.");
                return false;
            }

            // Устанавливаем в слот
            bool success = slot.InstallModule(module);
            if (success)
            {
                RecalculatePowerUsage();
                Debug.Log($"[ShipModuleManager] Module '{module.moduleId}' installed. Power: {currentPowerUsage}/{availablePower}");
            }
            return success;
        }

        /// <summary>
        /// Удалить модуль из указанного слота.
        /// </summary>
        public void RemoveModule(ModuleSlot slot)
        {
            if (slot == null) return;

            slot.RemoveModule();
            RecalculatePowerUsage();
        }

        /// <summary>
        /// Получить суммарный множитель тяги от всех модулей.
        /// </summary>
        public float GetThrustMultiplier()
        {
            float mult = 1f;
            foreach (var slot in slots)
            {
                if (slot.isOccupied)
                    mult *= slot.installedModule.thrustMultiplier;
            }
            return mult;
        }

        /// <summary>
        /// Получить суммарный множитель рыскания от всех модулей.
        /// </summary>
        public float GetYawMultiplier()
        {
            float mult = 1f;
            foreach (var slot in slots)
            {
                if (slot.isOccupied)
                    mult *= slot.installedModule.yawMultiplier;
            }
            return mult;
        }

        /// <summary>
        /// Получить суммарный множитель тангажа от всех модулей.
        /// </summary>
        public float GetPitchMultiplier()
        {
            float mult = 1f;
            foreach (var slot in slots)
            {
                if (slot.isOccupied)
                    mult *= slot.installedModule.pitchMultiplier;
            }
            return mult;
        }

        /// <summary>
        /// Получить суммарный множитель лифта от всех модулей.
        /// </summary>
        public float GetLiftMultiplier()
        {
            float mult = 1f;
            foreach (var slot in slots)
            {
                if (slot.isOccupied)
                    mult *= slot.installedModule.liftMultiplier;
            }
            return mult;
        }

        /// <summary>
        /// Получить суммарный множитель крена от всех модулей.
        /// </summary>
        public float GetRollMultiplier()
        {
            float mult = 1f;
            foreach (var slot in slots)
            {
                if (slot.isOccupied)
                    mult *= slot.installedModule.rollMultiplier;
            }
            return mult;
        }

        /// <summary>
        /// Получить модификатор максимальной скорости от всех модулей.
        /// </summary>
        public float GetMaxSpeedModifier()
        {
            float modifier = 0f;
            foreach (var slot in slots)
            {
                if (slot.isOccupied)
                    modifier += slot.installedModule.maxSpeedModifier;
            }
            return modifier;
        }

        /// <summary>
        /// Получить модификатор экспозиции к ветру от всех модулей.
        /// </summary>
        public float GetWindExposureModifier()
        {
            float modifier = 0f;
            foreach (var slot in slots)
            {
                if (slot.isOccupied)
                    modifier += slot.installedModule.windExposureModifier;
            }
            return modifier;
        }

        /// <summary>
        /// Проверить валидность конфигурации (все модули совместимы).
        /// </summary>
        public bool ValidateConfiguration()
        {
            List<string> installedIds = GetInstalledModuleIds();

            foreach (var slot in slots)
            {
                if (!slot.isOccupied) continue;

                var module = slot.installedModule;

                // Проверяем совместимость с классом
                if (!module.IsCompatibleWithClass(_shipClass))
                    return false;

                // Проверяем совместимость между модулями
                foreach (var otherId in installedIds)
                {
                    if (otherId == module.moduleId) continue;
                    if (!module.IsCompatibleWithModule(otherId))
                        return false;
                }

                // Проверяем требуемые модули
                if (!module.AreRequiredModulesInstalled(installedIds))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Получить доступную энергию (available - used).
        /// </summary>
        public int GetAvailablePower()
        {
            return availablePower - currentPowerUsage;
        }

        /// <summary>
        /// Пересчитать текущее потребление энергии.
        /// </summary>
        private void RecalculatePowerUsage()
        {
            currentPowerUsage = 0;
            foreach (var slot in slots)
            {
                if (slot.isOccupied)
                    currentPowerUsage += slot.installedModule.powerConsumption;
            }
        }

        /// <summary>
        /// Получить список ID всех установленных модулей.
        /// </summary>
        private List<string> GetInstalledModuleIds()
        {
            List<string> ids = new List<string>();
            foreach (var slot in slots)
            {
                if (slot.isOccupied)
                    ids.Add(slot.installedModule.moduleId);
            }
            return ids;
        }

        /// <summary>
        /// Проверить совместимость нового модуля с уже установленными.
        /// </summary>
        private bool ValidateModuleCompatibility(ShipModule newModule)
        {
            foreach (var slot in slots)
            {
                if (!slot.isOccupied) continue;

                // Новый модуль несовместим с установленным?
                if (!newModule.IsCompatibleWithModule(slot.installedModuleId))
                    return false;

                // Установленный модуль несовместим с новым?
                if (!slot.installedModule.IsCompatibleWithModule(newModule.moduleId))
                    return false;
            }
            return true;
        }
    }
}
