using UnityEngine;
using UnityEditor;
using System.IO;
using ProjectC.Ship;

namespace ProjectC.Editor
{
    /// <summary>
    /// Editor утилита для создания ассетов коридоров высот.
    /// Menu: Tools → Project C → Create Altitude Corridor Assets
    /// 
    /// Создаёт .asset файлы в Assets/_Project/Data/AltitudeCorridors/
    /// </summary>
    public static class CreateAltitudeCorridorAssets
    {
        private const string CorridorDataPath = "Assets/_Project/Data/AltitudeCorridors";

        [MenuItem("Tools/Project C/Create Altitude Corridor Assets")]
        public static void CreateCorridorAssets()
        {
            // Создаём директорию если нет
            if (!Directory.Exists(CorridorDataPath))
            {
                Directory.CreateDirectory(CorridorDataPath);
                Debug.Log($"[CreateCorridorAssets] Created directory: {CorridorDataPath}");
            }

            // Определяем коридоры
            var corridorDefs = new[]
            {
                new CorridorDef("Global", "Global Corridor", true, 1200f, 4450f, Vector3.zero, 0f),
                new CorridorDef("Primus", "Primus City", false, 4100f, 4450f, new Vector3(0, 4348f, 0), 500f),
                new CorridorDef("Tertius", "Tertius City", false, 2300f, 2600f, new Vector3(1000, 2462f, 1000), 500f),
                new CorridorDef("Quartus", "Quartus City", false, 1500f, 1850f, new Vector3(-1000, 1690f, 500), 500f),
                new CorridorDef("Kilimanjaro", "Kilimanjaro City", false, 1200f, 1550f, new Vector3(500, 1395f, -1000), 500f),
                new CorridorDef("Secundus", "Secundus City", false, 1000f, 1250f, new Vector3(-500, 1142f, -500), 500f),
            };

            int created = 0;
            int skipped = 0;

            foreach (var def in corridorDefs)
            {
                string assetPath = $"{CorridorDataPath}/{def.FileName}.asset";

                // Проверяем существует ли
                if (File.Exists(assetPath))
                {
                    Debug.Log($"[CreateCorridorAssets] Skipped (exists): {def.DisplayName}");
                    skipped++;
                    continue;
                }

                // Создаём ScriptableObject
                var corridor = ScriptableObject.CreateInstance<AltitudeCorridorData>();
                corridor.corridorId = def.Id;
                corridor.displayName = def.DisplayName;
                corridor.isGlobal = def.IsGlobal;
                corridor.minAltitude = def.MinAltitude;
                corridor.maxAltitude = def.MaxAltitude;
                corridor.warningMargin = 100f;
                corridor.criticalUpperMargin = 200f;
                corridor.cityCenter = def.CityCenter;
                corridor.cityRadius = def.CityRadius;
                corridor.requiresRegistration = !def.IsGlobal;

                AssetDatabase.CreateAsset(corridor, assetPath);
                created++;
                Debug.Log($"[CreateCorridorAssets] Created: {def.DisplayName} at {assetPath}");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[CreateCorridorAssets] Complete! Created: {created}, Skipped: {skipped}");
            EditorUtility.DisplayDialog("Altitude Corridors",
                $"Created {created} corridor assets.\nSkipped {skipped} (already exist).\n\nPath: {CorridorDataPath}",
                "OK");
        }

        private struct CorridorDef
        {
            public string Id;
            public string DisplayName;
            public bool IsGlobal;
            public float MinAltitude;
            public float MaxAltitude;
            public Vector3 CityCenter;
            public float CityRadius;
            public string FileName => $"Corridor_{Id}";

            public CorridorDef(string id, string displayName, bool isGlobal,
                float minAltitude, float maxAltitude, Vector3 cityCenter, float cityRadius)
            {
                Id = id;
                DisplayName = displayName;
                IsGlobal = isGlobal;
                MinAltitude = minAltitude;
                MaxAltitude = maxAltitude;
                CityCenter = cityCenter;
                CityRadius = cityRadius;
            }
        }
    }
}
