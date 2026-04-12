#if UNITY_EDITOR
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using ProjectC.Ship;
using ProjectC.Player;

namespace ProjectC.Editor
{
    /// <summary>
    /// Editor утилита для создания тестовых модулей Сессии 4.
    /// Menu: Tools → Project C → Create Module Test Assets
    /// Создаёт 3 тестовых модуля: YAW_ENH, PITCH_ENH, LIFT_ENH.
    /// </summary>
    public class CreateModuleTestAssets : EditorWindow
    {
        private const string OutputFolder = "Assets/_Project/Data/Modules";

        [MenuItem("Tools/Project C/Create Module Test Assets")]
        public static void CreateTestModules()
        {
            // Создаём папку если нет
            if (!Directory.Exists(OutputFolder))
            {
                Directory.CreateDirectory(OutputFolder);
                Debug.Log($"[CreateModuleTestAssets] Created folder: {OutputFolder}");
            }

            int created = 0;

            // 1. MODULE_YAW_ENH
            if (CreateModuleAsset(
                "MODULE_YAW_ENH",
                "Улучшенное Рыскание",
                ModuleType.Propulsion,
                tier: 1,
                thrustMultiplier: 1f,
                yawMultiplier: 1.4f,
                pitchMultiplier: 1f,
                liftMultiplier: 1f,
                powerConsumption: 5,
                compatibleClasses: new List<ShipFlightClass> { ShipFlightClass.Light, ShipFlightClass.Medium, ShipFlightClass.Heavy, ShipFlightClass.HeavyII },
                OutputFolder))
            {
                created++;
            }

            // 2. MODULE_PITCH_ENH
            if (CreateModuleAsset(
                "MODULE_PITCH_ENH",
                "Улучшенный Тангаж",
                ModuleType.Propulsion,
                tier: 1,
                thrustMultiplier: 1f,
                yawMultiplier: 1f,
                pitchMultiplier: 1.3f,
                liftMultiplier: 1f,
                powerConsumption: 5,
                compatibleClasses: new List<ShipFlightClass> { ShipFlightClass.Light, ShipFlightClass.Medium, ShipFlightClass.Heavy, ShipFlightClass.HeavyII },
                OutputFolder))
            {
                created++;
            }

            // 3. MODULE_LIFT_ENH
            if (CreateModuleAsset(
                "MODULE_LIFT_ENH",
                "Улучшенный Лифт",
                ModuleType.Propulsion,
                tier: 1,
                thrustMultiplier: 1f,
                yawMultiplier: 1f,
                pitchMultiplier: 1f,
                liftMultiplier: 1.5f,
                powerConsumption: 8,
                compatibleClasses: new List<ShipFlightClass> { ShipFlightClass.Light, ShipFlightClass.Medium, ShipFlightClass.Heavy, ShipFlightClass.HeavyII },
                OutputFolder))
            {
                created++;
            }

            AssetDatabase.Refresh();
            Debug.Log($"[CreateModuleTestAssets] Created {created} module assets in {OutputFolder}");
            EditorUtility.DisplayDialog("Module Test Assets", $"Создано {created} тестовых модулей в {OutputFolder}", "OK");
        }

        private static bool CreateModuleAsset(
            string moduleId,
            string displayName,
            ModuleType type,
            int tier,
            float thrustMultiplier,
            float yawMultiplier,
            float pitchMultiplier,
            float liftMultiplier,
            int powerConsumption,
            List<ShipFlightClass> compatibleClasses,
            string folder)
        {
            string path = $"{folder}/{moduleId}.asset";

            // Проверяем существует ли уже
            ShipModule existing = AssetDatabase.LoadAssetAtPath<ShipModule>(path);
            if (existing != null)
            {
                Debug.Log($"[CreateModuleTestAssets] Module already exists: {path}");
                return false;
            }

            // Создаём
            ShipModule module = ScriptableObject.CreateInstance<ShipModule>();
            module.moduleId = moduleId;
            module.displayName = displayName;
            module.type = type;
            module.tier = tier;
            module.thrustMultiplier = thrustMultiplier;
            module.yawMultiplier = yawMultiplier;
            module.pitchMultiplier = pitchMultiplier;
            module.liftMultiplier = liftMultiplier;
            module.powerConsumption = powerConsumption;
            module.compatibleClasses = compatibleClasses;

            AssetDatabase.CreateAsset(module, path);
            Debug.Log($"[CreateModuleTestAssets] Created: {path}");
            return true;
        }
    }
}
#endif
