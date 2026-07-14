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
        Special,      // Специальные (auto-dock, auto-nav, space)
        Engine        // Визуальные двигатели (T-ENG02)
    }

    /// <summary>
    /// Ориентация визуала модуля относительно reference-вектора.
    /// </summary>
    public enum ModuleAttachAxis
    {
        Slot,        // Локальная ориентация слота (default)
        ShipForward, // Вдоль forward-вектора корабля
        ShipDown,    // Вниз (к земле)
        WorldUp      // Мировой вверх
    }

    /// <summary>
    /// Режим коллайдеров на визуале модуля.
    /// </summary>
    public enum ModuleColliderMode
    {
        None,    // Все коллайдеры отключены (как у персонажа, default)
        Trigger, // Коллайдеры → isTrigger (для raycast)
        Solid    // Коллайдеры включены, влияют на физику
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

        [Header("Cargo Expansion (T-CARGO-06)")]
        [Tooltip("Бонус к максимальному количеству грузовых слотов (flat, +N)")]
        [Min(0)] public int cargoSlotsBonus = 0;

        [Tooltip("Бонус к максимальному весу груза (flat, +N кг)")]
        [Min(0f)] public float cargoWeightBonus = 0f;

        [Tooltip("Бонус к максимальному объёму груза (flat, +N м³)")]
        [Min(0f)] public float cargoVolumeBonus = 0f;

        [Tooltip("Снижение коэффициента штрафа скорости от груза (flat, -N). Отрицательное = увеличение штрафа.")]
        public float cargoPenaltyReduction = 0f;

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

        [Header("Visual (L1 — module visualPrefab)")]
        [Tooltip("Префаб меша модуля. При install — спавнится как child слота; при remove — уничтожается. Если null — модуль без визуала.")]
        public GameObject visualPrefab;

        [Tooltip("Путь к дочернему socket'у внутри слота (например 'Socket_A'). Пусто = сам слот.")]
        public string visualSocketPath = "";

        [Tooltip("Локальный offset от слота/socket'а к визуалу (local space родителя).")]
        public Vector3 attachPositionOffset = Vector3.zero;

        [Tooltip("Локальное вращение визуала относительно слота/socket'а (Euler degrees).")]
        public Vector3 attachRotationOffset = Vector3.zero;

        [Tooltip("Локальный масштаб визуала. (1,1,1) = без изменений. x=-1 зеркалирует.")]
        public Vector3 attachScale = Vector3.one;

        [Tooltip("Ориентация визуала относительно reference-вектора (Slot/ShipForward/ShipDown/WorldUp).")]
        public ModuleAttachAxis attachAxis = ModuleAttachAxis.Slot;

        [Tooltip("Режим коллайдеров на визуале (None=отключены, Trigger=isTrigger, Solid=включены).")]
        public ModuleColliderMode colliderMode = ModuleColliderMode.None;

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
