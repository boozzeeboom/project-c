#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using ProjectC.Ship;
using ProjectC.Player;

namespace ProjectC.Editor
{
    /// <summary>
    /// Editor утилита: создание мезиевых модулей для Сессии 5.
    /// Menu: Tools → Project C → Create Meziy Module Assets
    ///
    /// Создаёт 4 тестовых модуля:
    /// 1. MODULE_MEZIY_ROLL: meziyForce=25, meziyDuration=2s, meziyCooldown=10s, fuelCost=5
    /// 2. MODULE_MEZIY_PITCH: meziyForce=10, meziyDuration=1.5s, meziyCooldown=8s, fuelCost=5
    /// 3. MODULE_MEZIY_YAW: meziyForce=30, meziyDuration=0.5s, meziyCooldown=12s, fuelCost=5
    /// 4. MODULE_ROLL: roll utility, power=10, tier 2
    ///
    /// Сохраняет в Assets/_Project/Data/Modules/
    /// </summary>
    public static class CreateMeziyModuleAssets
    {
        private const string MODULES_PATH = "Assets/_Project/Data/Modules";

        [MenuItem("Tools/Project C/Create Meziy Module Assets")]
        public static void CreateMeziyModules()
        {
            // Создать директорию если нет
            if (!Directory.Exists(MODULES_PATH))
            {
                Directory.CreateDirectory(MODULES_PATH);
                Debug.Log($"[CreateMeziyModules] Created directory: {MODULES_PATH}");
            }

            int created = 0;

            // 1. MODULE_MEZIY_ROLL
            if (CreateMeziyModule(
                "MODULE_MEZIY_ROLL",
                "Мезиевая Тяга (Крен)",
                ModuleType.Propulsion,
                tier: 2,
                meziyForce: 25f,
                meziyDuration: 2f,
                meziyCooldown: 10f,
                meziyFuelCost: 5f,
                powerConsumption: 15,
                compatibleClasses: new[] { ShipFlightClass.Light, ShipFlightClass.Medium, ShipFlightClass.Heavy, ShipFlightClass.HeavyII }))
            {
                created++;
            }

            // 2. MODULE_MEZIY_PITCH
            if (CreateMeziyModule(
                "MODULE_MEZIY_PITCH",
                "Мезиевая Тяга (Тангаж)",
                ModuleType.Propulsion,
                tier: 2,
                meziyForce: 10f,
                meziyDuration: 1.5f,
                meziyCooldown: 8f,
                meziyFuelCost: 5f,
                powerConsumption: 15,
                compatibleClasses: new[] { ShipFlightClass.Light, ShipFlightClass.Medium, ShipFlightClass.Heavy, ShipFlightClass.HeavyII }))
            {
                created++;
            }

            // 3. MODULE_MEZIY_YAW
            if (CreateMeziyModule(
                "MODULE_MEZIY_YAW",
                "Мезиевая Тяга (Рыскание)",
                ModuleType.Propulsion,
                tier: 2,
                meziyForce: 30f,
                meziyDuration: 0.5f,
                meziyCooldown: 12f,
                meziyFuelCost: 5f,
                powerConsumption: 15,
                compatibleClasses: new[] { ShipFlightClass.Light, ShipFlightClass.Medium, ShipFlightClass.Heavy, ShipFlightClass.HeavyII }))
            {
                created++;
            }

            // 4. MODULE_ROLL
            if (CreateRollModule())
            {
                created++;
            }

            AssetDatabase.Refresh();

            Debug.Log($"[CreateMeziyModules] Created {created} modules in {MODULES_PATH}");
            EditorUtility.DisplayDialog("Meziy Modules", $"Created {created} modules in {MODULES_PATH}", "OK");
        }

        /// <summary>
        /// Создать мезиевый модуль.
        /// </summary>
        private static bool CreateMeziyModule(
            string moduleId,
            string displayName,
            ModuleType type,
            int tier,
            float meziyForce,
            float meziyDuration,
            float meziyCooldown,
            float meziyFuelCost,
            int powerConsumption,
            ShipFlightClass[] compatibleClasses)
        {
            string assetPath = $"{MODULES_PATH}/{moduleId}.asset";

            // Проверить существует ли уже
            ShipModule existing = AssetDatabase.LoadAssetAtPath<ShipModule>(assetPath);
            if (existing != null)
            {
                Debug.LogWarning($"[CreateMeziyModules] Module '{moduleId}' already exists at {assetPath}. Skipping.");
                return false;
            }

            // Создать
            var module = ScriptableObject.CreateInstance<ShipModule>();
            module.moduleId = moduleId;
            module.displayName = displayName;
            module.type = type;
            module.tier = tier;

            // Мезиевые параметры
            module.isMeziyModule = true;
            module.meziyForce = meziyForce;
            module.meziyDuration = meziyDuration;
            module.meziyCooldown = meziyCooldown;
            module.meziyFuelCost = meziyFuelCost;

            // Требования
            module.powerConsumption = powerConsumption;
            module.compatibleClasses = new List<ShipFlightClass>(compatibleClasses);

            // Сохранить
            AssetDatabase.CreateAsset(module, assetPath);
            Debug.Log($"[CreateMeziyModules] Created: {moduleId}");
            return true;
        }

        /// <summary>
        /// Создать MODULE_ROLL (утилита разблокировки крена).
        /// </summary>
        private static bool CreateRollModule()
        {
            string assetPath = $"{MODULES_PATH}/MODULE_ROLL.asset";

            // Проверить существует ли уже
            ShipModule existing = AssetDatabase.LoadAssetAtPath<ShipModule>(assetPath);
            if (existing != null)
            {
                Debug.LogWarning($"[CreateMeziyModules] MODULE_ROLL already exists at {assetPath}. Skipping.");
                return false;
            }

            // Создать
            var module = ScriptableObject.CreateInstance<ShipModule>();
            module.moduleId = "MODULE_ROLL";
            module.displayName = "Модуль Крена";
            module.type = ModuleType.Utility;
            module.tier = 2;

            // Эффекты (разблокировка крена проверяется в ShipController по moduleId)
            module.thrustMultiplier = 1f;
            module.yawMultiplier = 1f;
            module.pitchMultiplier = 1f;
            module.liftMultiplier = 1f;

            // Требования
            module.powerConsumption = 10;
            module.compatibleClasses = new List<ShipFlightClass>
            {
                ShipFlightClass.Light,
                ShipFlightClass.Medium,
                ShipFlightClass.Heavy,
                ShipFlightClass.HeavyII
            };

            // Сохранить
            AssetDatabase.CreateAsset(module, assetPath);
            Debug.Log("[CreateMeziyModules] Created: MODULE_ROLL");
            return true;
        }
    }
}
#endif
