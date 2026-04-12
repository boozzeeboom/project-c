using System.Collections.Generic;
using UnityEngine;
using ProjectC.Player;

namespace ProjectC.Ship
{
    /// <summary>
    /// Тип модуля корабля — определяет категорию эффектов.
    /// </summary>
    public enum ModuleType
    {
        Propulsion,   // Движение (yaw, pitch, lift enhancement)
        Utility,      // Утилиты (roll, veil, stealth)
        Special       // Специальные (auto-dock, auto-nav, space)
    }

    /// <summary>
    /// ShipModule — ScriptableObject определяющий модуль корабля.
    /// Модули устанавливаются в слоты и изменяют характеристики корабля.
    /// Сессия 4: Module System Foundation.
    /// </summary>
    [CreateAssetMenu(menuName = "ProjectC/Ship/Module", fileName = "Module_")]
    public class ShipModule : ScriptableObject
    {
        [Header("Идентификатор")]
        [Tooltip("Уникальный ID модуля (например MODULE_YAW_ENH)")]
        public string moduleId;

        [Tooltip("Отображаемое имя")]
        public string displayName;

        [Tooltip("Тип модуля")]
        public ModuleType type;

        [Tooltip("Тир модуля (1-4)")]
        [Range(1, 4)]
        public int tier;

        [Header("Эффекты")]
        [Tooltip("Множитель тяги (1.0 = базовый)")]
        public float thrustMultiplier = 1f;

        [Tooltip("Множитель скорости рыскания (1.0 = базовый)")]
        public float yawMultiplier = 1f;

        [Tooltip("Множитель скорости тангажа (1.0 = базовый)")]
        public float pitchMultiplier = 1f;

        [Tooltip("Множитель скорости крена (1.0 = базовый)")]
        public float rollMultiplier = 1f;

        [Tooltip("Множитель скорости лифта (1.0 = базовый)")]
        public float liftMultiplier = 1f;

        [Tooltip("Изменение максимальной скорости (±м/с)")]
        public float maxSpeedModifier = 0f;

        [Tooltip("Изменение экспозиции к ветру (±к windExposure)")]
        public float windExposureModifier = 0f;

        [Header("Требования")]
        [Tooltip("Потребление энергии (0 = не потребляет)")]
        public int powerConsumption = 0;

        [Tooltip("На каких классах кораблей работает модуль")]
        public List<ShipFlightClass> compatibleClasses = new List<ShipFlightClass>();

        [Tooltip("Какие модули должны быть установлены для работы этого")]
        public List<string> requiredModules = new List<string>();

        [Tooltip("Какие модули НЕсовместимы с этим")]
        public List<string> incompatibleModules = new List<string>();

        [Header("Мезиевая Тяга (только для MEZIY модулей)")]
        [Tooltip("Является ли модуль мезиевой тяги")]
        public bool isMeziyModule = false;

        [Tooltip("Сила мезиевого толчка")]
        public float meziyForce = 0f;

        [Tooltip("Длительность мезиевого эффекта (сек)")]
        public float meziyDuration = 0f;

        [Tooltip("Кулдаун между активациями (сек)")]
        public float meziyCooldown = 0f;

        [Tooltip("Стоимость топлива за активацию")]
        public float meziyFuelCost = 0f;

        /// <summary>
        /// Проверить совместимость модуля с классом корабля.
        /// </summary>
        public bool IsCompatibleWithClass(ShipFlightClass shipClass)
        {
            if (compatibleClasses == null || compatibleClasses.Count == 0)
                return true; // Если список пуст — совместим со всеми

            return compatibleClasses.Contains(shipClass);
        }

        /// <summary>
        /// Проверить совместимость с другим модулем.
        /// </summary>
        public bool IsCompatibleWithModule(string otherModuleId)
        {
            if (incompatibleModules != null && incompatibleModules.Contains(otherModuleId))
                return false;

            return true;
        }

        /// <summary>
        /// Проверить все требуемые модули установлены.
        /// </summary>
        public bool AreRequiredModulesInstalled(List<string> installedModuleIds)
        {
            if (requiredModules == null || requiredModules.Count == 0)
                return true;

            foreach (var requiredId in requiredModules)
            {
                if (!installedModuleIds.Contains(requiredId))
                    return false;
            }
            return true;
        }
    }
}
