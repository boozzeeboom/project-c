using UnityEngine;
using UnityEditor;
using System.IO;
using ProjectC.World.Core;
using ProjectC.Ship;

namespace ProjectC.Editor
{
    /// <summary>
    /// Editor-скрипт для автоматического создания всех ассетов мира (сессия A1).
    /// Меню: Tools → Project C → World Setup → Create All World Assets
    /// </summary>
    public static class WorldAssetCreator
    {
        private const string DATA_WORLD_PATH = "Assets/_Project/Data/World";
        private const string BIOME_PROFILES_PATH = "Assets/_Project/Data/World/BiomeProfiles";
        private const string MASSIFS_PATH = "Assets/_Project/Data/World/Massifs";
        private const string MATERIALS_CLOUDS_PATH = "Assets/_Project/Materials/Clouds";

        #region Главное меню

        [MenuItem("Tools/Project C/World Setup/Create All World Assets")]
        public static void CreateAllWorldAssets()
        {
            Debug.Log("=== [WorldAssetCreator] Начинаю создание ассетов мира ===");

            CreateFolders();
            CreateBiomeProfiles();
            CreateMountainMassifs();
            CreateWorldData();
            CreateVeilMaterial();
            UpdateAltitudeCorridors();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("=== [WorldAssetCreator] ✅ Все ассеты мира созданы! ===");
            EditorUtility.DisplayDialog("Project C", "Все ассеты мира созданы успешно!\nПроверьте Assets/_Project/Data/World/", "OK");
        }

        [MenuItem("Tools/Project C/World Setup/Create Folders Only")]
        public static void CreateFoldersOnly()
        {
            CreateFolders();
            EditorUtility.DisplayDialog("Project C", "Папки созданы:\n" + DATA_WORLD_PATH + "\n" + BIOME_PROFILES_PATH + "\n" + MASSIFS_PATH, "OK");
        }

        [MenuItem("Tools/Project C/World Setup/Create Biome Profiles")]
        public static void CreateBiomeProfilesOnly()
        {
            CreateFolders();
            CreateBiomeProfiles();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Project C", "5 BiomeProfile ассетов созданы!", "OK");
        }

        [MenuItem("Tools/Project C/World Setup/Create Mountain Massifs")]
        public static void CreateMountainMassifsOnly()
        {
            CreateFolders();
            CreateBiomeProfiles(); // Massifs ссылаются на BiomeProfiles
            CreateMountainMassifs();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Project C", "5 MountainMassif ассетов созданы!", "OK");
        }

        [MenuItem("Tools/Project C/World Setup/Create WorldData")]
        public static void CreateWorldDataOnly()
        {
            CreateFolders();
            CreateBiomeProfiles();
            CreateMountainMassifs();
            CreateWorldData();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Project C", "WorldData.asset создан!", "OK");
        }

        [MenuItem("Tools/Project C/World Setup/Create Veil Material")]
        public static void CreateVeilMaterialOnly()
        {
            CreateVeilMaterial();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Project C", "VeilMaterial.mat создан!", "OK");
        }

        [MenuItem("Tools/Project C/World Setup/Update Altitude Corridors")]
        public static void UpdateCorridorsOnly()
        {
            UpdateAltitudeCorridors();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Project C", "6 AltitudeCorridorData ассетов обновлены!", "OK");
        }

        #endregion

        #region Создание папок

        private static void CreateFolders()
        {
            CreateFolder(DATA_WORLD_PATH);
            CreateFolder(BIOME_PROFILES_PATH);
            CreateFolder(MASSIFS_PATH);
            CreateFolder(MATERIALS_CLOUDS_PATH);
            Debug.Log("[WorldAssetCreator] Папки созданы/проверены");
        }

        private static void CreateFolder(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
                Debug.Log($"[WorldAssetCreator] Создана папка: {path}");
            }
            AssetDatabase.Refresh();
        }

        #endregion

        #region Biome Profiles

        private static void CreateBiomeProfiles()
        {
            CreateBiomeProfile("Biome_Himalayan", "himalayan", "Гималайский",
                HexColor("#4a6fa5"), HexColor("#5a5a5a"), HexColor("#ffffff"), 1.0f, 45f,
                "Суровый, величественный — глубокий синий, яркий жёсткий свет");

            CreateBiomeProfile("Biome_Alpine", "alpine", "Альпийский",
                HexColor("#6a8fb5"), HexColor("#8a8a7a"), HexColor("#e0e0e0"), 0.9f, 40f,
                "Живописный, крутой — голубое небо, мягкий рассеянный свет");

            CreateBiomeProfile("Biome_African", "african", "Африканский",
                HexColor("#8a7a6a"), HexColor("#7a5a4a"), HexColor("#f0d0a0"), 1.1f, 35f,
                "Экзотический, тёплый — тёплые тона, золотистое освещение");

            CreateBiomeProfile("Biome_Andean", "andean", "Андийский",
                HexColor("#7a9ab5"), HexColor("#6a5a4a"), HexColor("#f5f0e0"), 1.05f, 50f,
                "Суровый, пустынный — бледно-голубое небо, жёсткий сухой свет");

            CreateBiomeProfile("Biome_Alaskan", "alaskan", "Аляскинский",
                HexColor("#5a6a7a"), HexColor("#4a4a4a"), HexColor("#c0d0e0"), 0.8f, 25f,
                "Изолированный, ледяной — холодный серо-синий, низкий угол света");

            Debug.Log("[WorldAssetCreator] 5 BiomeProfile ассетов созданы");
        }

        private static void CreateBiomeProfile(string fileName, string biomeId, string displayName,
            Color skyColor, Color rockColor, Color lightColor, float lightIntensity, float lightAngle,
            string atmosphere)
        {
            string path = $"{BIOME_PROFILES_PATH}/{fileName}.asset";

            // Проверяем существует ли
            BiomeProfile profile = AssetDatabase.LoadAssetAtPath<BiomeProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<BiomeProfile>();
                profile.biomeId = biomeId;
                profile.displayName = displayName;
                profile.skyColor = skyColor;
                profile.atmosphereColor = skyColor * 0.5f;
                profile.rockColor = rockColor;
                profile.snowColor = new Color(0.94f, 0.94f, 0.96f);
                profile.lightIntensity = lightIntensity;
                profile.lightColor = lightColor;
                profile.lightAngle = lightAngle;
                profile.atmosphereDescription = atmosphere;

                AssetDatabase.CreateAsset(profile, path);
                Debug.Log($"[WorldAssetCreator] Создан: {path}");
            }
            else
            {
                Debug.Log($"[WorldAssetCreator] Уже существует: {path}");
            }
        }

        #endregion

        #region Mountain Massifs

        private static void CreateMountainMassifs()
        {
            // Загружаем BiomeProfiles для привязки
            BiomeProfile himalayanBio = AssetDatabase.LoadAssetAtPath<BiomeProfile>($"{BIOME_PROFILES_PATH}/Biome_Himalayan.asset");
            BiomeProfile alpineBio = AssetDatabase.LoadAssetAtPath<BiomeProfile>($"{BIOME_PROFILES_PATH}/Biome_Alpine.asset");
            BiomeProfile africanBio = AssetDatabase.LoadAssetAtPath<BiomeProfile>($"{BIOME_PROFILES_PATH}/Biome_African.asset");
            BiomeProfile andeanBio = AssetDatabase.LoadAssetAtPath<BiomeProfile>($"{BIOME_PROFILES_PATH}/Biome_Andean.asset");
            BiomeProfile alaskanBio = AssetDatabase.LoadAssetAtPath<BiomeProfile>($"{BIOME_PROFILES_PATH}/Biome_Alaskan.asset");

            // ⚠️ Y координаты в РЕАЛЬНЫХ МЕТРАХ (НЕ scaled!)
            // XZ — в игровом масштабе (1:2000)
            // Радиусы увеличены: покрывают все пики + запас для хребтов
            CreateMountainMassif("HimalayanMassif", "himalayan", "Гималайский массив",
                new Vector3(0, 8848f, 0), 3000f, himalayanBio);

            CreateMountainMassif("AlpineMassif", "alpine", "Альпийский массив",
                new Vector3(-1310, 4808f, 2810), 1500f, alpineBio);

            CreateMountainMassif("AfricanMassif", "african", "Африканский массив",
                new Vector3(-1881, 5895f, -3010), 1000f, africanBio);

            CreateMountainMassif("AndeanMassif", "andean", "Андийский массив",
                new Vector3(-4176, 6962f, -2110), 2500f, andeanBio);

            CreateMountainMassif("AlaskanMassif", "alaskan", "Аляскинский массив",
                new Vector3(1255, 6190f, 4685), 1500f, alaskanBio);

            Debug.Log("[WorldAssetCreator] 5 MountainMassif ассетов созданы");
        }

        private static void CreateMountainMassif(string fileName, string massifId, string displayName,
            Vector3 centerPosition, float massifRadius, BiomeProfile biomeProfile)
        {
            string path = $"{MASSIFS_PATH}/{fileName}.asset";

            MountainMassif massif = AssetDatabase.LoadAssetAtPath<MountainMassif>(path);
            if (massif == null)
            {
                massif = ScriptableObject.CreateInstance<MountainMassif>();
                massif.massifId = massifId;
                massif.displayName = displayName;
                massif.centerPosition = centerPosition;
                massif.massifRadius = massifRadius;
                massif.biomeProfile = biomeProfile;
                // peaks, ridges, farms, cityCorridor — пустые (заполним в следующих сессиях)

                AssetDatabase.CreateAsset(massif, path);
                Debug.Log($"[WorldAssetCreator] Создан: {path}");
            }
            else
            {
                Debug.Log($"[WorldAssetCreator] Уже существует: {path}");
            }
        }

        #endregion

        #region WorldData

        private static void CreateWorldData()
        {
            string path = $"{DATA_WORLD_PATH}/WorldData.asset";

            WorldData worldData = AssetDatabase.LoadAssetAtPath<WorldData>(path);
            if (worldData == null)
            {
                worldData = ScriptableObject.CreateInstance<WorldData>();
                worldData.heightScale = 0.01f;
                worldData.distanceScale = 0.0005f;
                worldData.worldMinX = -5500f;
                worldData.worldMaxX = 2500f;
                worldData.worldMinZ = -3500f;
                worldData.worldMaxZ = 5500f;

                // Загружаем все MountainMassif ассеты
                worldData.massifs = new System.Collections.Generic.List<MountainMassif>();
                worldData.massifs.Add(AssetDatabase.LoadAssetAtPath<MountainMassif>($"{MASSIFS_PATH}/HimalayanMassif.asset"));
                worldData.massifs.Add(AssetDatabase.LoadAssetAtPath<MountainMassif>($"{MASSIFS_PATH}/AlpineMassif.asset"));
                worldData.massifs.Add(AssetDatabase.LoadAssetAtPath<MountainMassif>($"{MASSIFS_PATH}/AfricanMassif.asset"));
                worldData.massifs.Add(AssetDatabase.LoadAssetAtPath<MountainMassif>($"{MASSIFS_PATH}/AndeanMassif.asset"));
                worldData.massifs.Add(AssetDatabase.LoadAssetAtPath<MountainMassif>($"{MASSIFS_PATH}/AlaskanMassif.asset"));

                // Завеса
                worldData.veilHeight = 1200f;
                worldData.veilColor = HexColor("#2d1b4e");
                worldData.veilFogDensity = 0.003f;

                AssetDatabase.CreateAsset(worldData, path);
                Debug.Log($"[WorldAssetCreator] Создан: {path}");
            }
            else
            {
                Debug.Log($"[WorldAssetCreator] Уже существует: {path}");
            }
        }

        #endregion

        #region Veil Material

        private static void CreateVeilMaterial()
        {
            string path = $"{MATERIALS_CLOUDS_PATH}/VeilMaterial.mat";

            // Проверяем существует ли
            if (!File.Exists(path))
            {
                // Ищем шейдер
                Shader veilShader = Shader.Find("Project C/Clouds/VeilShader");
                if (veilShader == null)
                {
                    Debug.LogWarning("[WorldAssetCreator] Шейдер 'Project C/Clouds/VeilShader' не найден! " +
                        "Убедись что VeilShader.shader импортирован. Создаю с дефолтным шейдером.");
                    veilShader = Shader.Find("Unlit/Color");
                }

                Material mat = new Material(veilShader);
                mat.SetColor("_VeilColor", HexColor("#2d1b4e"));
                mat.SetFloat("_FogDensity", 0.003f);
                mat.SetColor("_LightningColor", HexColor("#b366ff"));
                mat.SetFloat("_LightningIntensity", 0f);
                mat.SetFloat("_DepthFadeStart", 100f);
                mat.SetFloat("_DepthFadeEnd", 500f);
                mat.SetFloat("_NoiseScale", 10f);
                mat.SetFloat("_NoiseSpeed", 0.1f);

                AssetDatabase.CreateAsset(mat, path);
                Debug.Log($"[WorldAssetCreator] Создан: {path}");
            }
            else
            {
                Debug.Log($"[WorldAssetCreator] Уже существует: {path}");
            }
        }

        #endregion

        #region Altitude Corridors

        private static void UpdateAltitudeCorridors()
        {
            UpdateCorridor("Corridor_Global", 1200, 9500);
            UpdateCorridor("Corridor_Primus", 4100, 9500);
            UpdateCorridor("Corridor_Secundus", 3000, 5500);
            UpdateCorridor("Corridor_Kilimanjaro", 4000, 6500);
            UpdateCorridor("Corridor_Tertius", 4500, 8000);
            UpdateCorridor("Corridor_Quartus", 4000, 7000);

            Debug.Log("[WorldAssetCreator] 6 AltitudeCorridorData ассетов обновлены");
        }

        private static void UpdateCorridor(string fileName, float newMin, float newMax)
        {
            string path = $"Assets/_Project/Data/AltitudeCorridors/{fileName}.asset";

            AltitudeCorridorData corridor = AssetDatabase.LoadAssetAtPath<AltitudeCorridorData>(path);
            if (corridor != null)
            {
                float oldMin = corridor.minAltitude;
                float oldMax = corridor.maxAltitude;

                corridor.minAltitude = newMin;
                corridor.maxAltitude = newMax;

                EditorUtility.SetDirty(corridor);
                Debug.Log($"[WorldAssetCreator] {fileName}: min {oldMin}→{newMin}, max {oldMax}→{newMax}");
            }
            else
            {
                Debug.LogWarning($"[WorldAssetCreator] Не найден: {path}");
            }
        }

        #endregion

        #region Утилиты

        /// <summary>
        /// Преобразует HEX-цвет в Unity Color.
        /// Формат: "#RRGGBB" или "RRGGBB"
        /// </summary>
        private static Color HexColor(string hex)
        {
            if (string.IsNullOrEmpty(hex)) return Color.white;

            hex = hex.Replace("#", "");

            if (hex.Length != 6)
            {
                Debug.LogWarning($"[WorldAssetCreator] Некорректный HEX цвет: {hex}");
                return Color.magenta;
            }

            byte r = byte.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
            byte g = byte.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
            byte b = byte.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);

            return new Color32(r, g, b, 255);
        }

        #endregion
    }
}
