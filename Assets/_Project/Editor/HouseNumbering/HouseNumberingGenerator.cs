#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;

using ProjectC.World;
using UnityEngine;
using TMPro;

namespace ProjectC.EditorTools
{
    /// <summary>
    /// Генератор маркеров домов Примума: создаёт HouseMarker + плавающий текст
    /// под корнем HOUSE_SIGNS (одна коллекция, потом заменится 3D-ассетом).
    /// Меню: ProjectC → World → PRIMUM → Нумерация домов (создать маркеры).
    /// </summary>
    public static class HouseNumberingGenerator
    {
        private const string CityRootName = "PRIMUM";
        private const string DoorsRootName = "PRIMUM_DOORS";
        private const string SignsRootName = "HOUSE_SIGNS";

        [MenuItem("ProjectC/World/PRIMUM/Нумерация домов: создать маркеры (только WorldScene_0_0)", false, 400)]
        public static void Generate()
        {
            var scene = EditorSceneManager.GetActiveScene();
            var worldRoot = GameObject.Find("WorldRoot_0_0");
            if (worldRoot == null)
            {
                Debug.LogError("[HouseNumbering] Не найден WorldRoot_0_0. Откройте WorldScene_0_0.");
                return;
            }
            var cityRoot = worldRoot.transform.Find(CityRootName);
            var doorsRoot = worldRoot.transform.Find(DoorsRootName);
            if (cityRoot == null || doorsRoot == null)
            {
                Debug.LogError("[HouseNumbering] Не найдены PRIMUM / PRIMUM_DOORS.");
                return;
            }

            // Снести старую коллекцию
            var oldSigns = cityRoot.Find(SignsRootName);
            if (oldSigns != null) UnityEngine.Object.DestroyImmediate(oldSigns.gameObject);

            var signsRoot = new GameObject(SignsRootName);
            signsRoot.transform.SetParent(cityRoot, false);
            signsRoot.transform.localPosition = Vector3.zero;
            signsRoot.transform.localRotation = Quaternion.identity;
            signsRoot.transform.localScale = Vector3.one;

            int totalHouses = 0;
            int totalDoors = 0;

            // Обход чанков дверей
            foreach (Transform chunkDoors in doorsRoot)
            {
                string chunkName = chunkDoors.name;
                string chunkPrefix = ExtractChunkPrefix(chunkName); // S2 -> N2
                var doorNames = new List<string>();
                foreach (Transform d in chunkDoors) doorNames.Add(d.name);
                doorNames.Sort(StringComparer.Ordinal);

                // Группировка по houseId
                var groups = new Dictionary<string, List<Transform>>(StringComparer.Ordinal);
                foreach (Transform d in chunkDoors)
                {
                    string hid = ParseHouseId(d.name);
                    if (string.IsNullOrEmpty(hid)) continue;
                    if (!groups.TryGetValue(hid, out var list))
                    {
                        list = new List<Transform>();
                        groups[hid] = list;
                    }
                    list.Add(d);
                }

                // Нумерация
                int seq = 1;
                foreach (var kv in groups.OrderBy(g => g.Key, StringComparer.Ordinal))
                {
                    string houseId = kv.Key;
                    var doors = kv.Value;
                    totalDoors += doors.Count;

                    // Внешние входы
                    var external = doors.Where(d => !IsInternalDoor(d.name)).ToList();
                    if (external.Count == 0) external = doors; // fallback

                    // Центроид
                    Vector3 centroid = Vector3.zero;
                    foreach (var d in external) centroid += d.position;
                    centroid /= external.Count;

                    // Номер дома
                    int number = ExtractNumber(houseId);
                    if (number <= 0) number = seq;
                    seq++;

                    string displayId = $"{chunkPrefix}-{number}";
                    // Если в имени уже есть номер чанка? оставляем как есть
                    // Итоговый ID для карты: N2-101
                    string mapId = $"{chunkPrefix}-{number}";

                    // Создать маркер
                    var signGo = new GameObject($"HouseSign_{mapId}");
                    signGo.transform.SetParent(signsRoot.transform, false);
                    signGo.transform.position = centroid + Vector3.up * 2f;
                    signGo.transform.rotation = Quaternion.identity;

                    var marker = signGo.AddComponent<HouseMarker>();
                    marker.houseId = mapId;
                    marker.number = number;
                    marker.chunkId = chunkName;
                    marker.city = "Primum";
                    marker.mapPoint = centroid;
                    marker.hasMapPoint = true;

                    // Плавающий текст (TextMeshPro)
                    var tmp = signGo.AddComponent<TextMeshPro>();
                    tmp.text = mapId;
                    tmp.fontSize = 3f;
                    tmp.alignment = TextAlignmentOptions.Center;
                    tmp.color = Color.white;

                    totalHouses++;
                }
            }

            EditorUtility.SetDirty(signsRoot);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[HouseNumbering] Создано домов: {totalHouses}, дверей обработано: {totalDoors}. Коллекция: {CityRootName}/{SignsRootName}");
        }

        private static string ParseHouseId(string doorName)
        {
            if (string.IsNullOrEmpty(doorName)) return "";
            // Отрезать от _DOOR / _IN_DOOR / _INT_DOOR
            string s = doorName;
            int idx = s.IndexOf("_DOOR", StringComparison.Ordinal);
            if (idx < 0) idx = s.IndexOf("_IN_DOOR", StringComparison.Ordinal);
            if (idx < 0) idx = s.IndexOf("_INT_DOOR", StringComparison.Ordinal);
            if (idx >= 0) s = s.Substring(0, idx);

            // Отрезать trailing _TQ/_VER/_BALC... (оставить префикс, заканчивающийся цифрой)
            var m = Regex.Match(s, @"^(.*?_\d+)");
            if (m.Success) s = m.Groups[1].Value;
            return s.Trim();
        }

        private static bool IsInternalDoor(string doorName)
        {
            return doorName.IndexOf("IN_DOOR", StringComparison.Ordinal) >= 0 ||
                   doorName.IndexOf("INT_DOOR", StringComparison.Ordinal) >= 0;
        }

        private static int ExtractNumber(string houseId)
        {
            if (string.IsNullOrEmpty(houseId)) return 0;
            var m = Regex.Match(houseId, @"_(\d+)$");
            if (m.Success && int.TryParse(m.Groups[1].Value, out int n)) return n;
            return 0;
        }

        private static string ExtractChunkPrefix(string chunkName)
        {
            // PRIMUM_HOUSES_S2_TRD_P1_28H -> S2 -> N2
            var m = Regex.Match(chunkName, @"_S(\d+)_");
            if (m.Success) return "N" + m.Groups[1].Value;
            // fallback: первая буква после HOUSES_
            m = Regex.Match(chunkName, @"HOUSES_([A-Z])");
            if (m.Success) return "N" + m.Groups[1].Value;
            return "N?";
        }
    }
}
#endif
