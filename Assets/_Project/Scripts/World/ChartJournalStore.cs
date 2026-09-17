using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ProjectC.World
{
    /// <summary>DTO точки трека (JsonUtility: только public поля).</summary>
    [Serializable]
    public class ChartTrackDto
    {
        public float x, y, z;
        public double utc;
        public int source;
    }

    /// <summary>DTO метки.</summary>
    [Serializable]
    public class ChartMarkDto
    {
        public int id;
        public float x, y, z;
        public int type;
        public string text;
        public double utc;
        public int source;
    }

    /// <summary>
    /// Файл журнала карты целиком (треки + метки атомарно).
    /// rb* — кумулятив сдвига на момент сейва (паттерн PERSIST01).
    /// </summary>
    [Serializable]
    public class ChartJournalData
    {
        public float rbx, rby, rbz;
        public int rbFrame;
        public int nextMarkId = 1;
        public List<ChartTrackDto> tracks = new List<ChartTrackDto>();
        public List<ChartMarkDto> marks = new List<ChartMarkDto>();
    }

    /// <summary>
    /// Файловое хранилище журнала карты: per-clientId JSON
    /// (`Application.persistentDataPath/Chart/chart_&lt;clientId&gt;.json`),
    /// как `Character/character_&lt;clientId&gt;.json`.
    ///
    /// Перенос через сдвиг мира (зеркалит `ShiftWrapperByOffset` из
    /// ShipPositionServer): в файле лежат позиции кадра сейва + офсет сейва.
    /// При загрузке: pos = file − fileOffset + liveCumulative.
    /// Новая сессия стартует с нулевым кумулятивом — позиции возвращаются
    /// в исходный кадр; та же сессия после F8 — остаются на месте.
    /// </summary>
    public static class ChartJournalStore
    {
        public static string PathFor(ulong clientId)
        {
            try { return Path.Combine(Application.persistentDataPath, "Chart", $"chart_{clientId}.json"); }
            catch { return $"chart_{clientId}.json"; }
        }

        /// <summary>Прочитать и привести к живому кадру. false — файла нет/битый.</summary>
        public static bool TryLoad(ulong clientId, out ChartJournalData data)
        {
            data = null;
            string path;
            try { path = PathFor(clientId); }
            catch { return false; }
            if (!File.Exists(path)) return false;

            try
            {
                string json = File.ReadAllText(path);
                if (string.IsNullOrEmpty(json)) return false;
                data = JsonUtility.FromJson<ChartJournalData>(json);
                if (data == null) return false;
                if (data.tracks == null) data.tracks = new List<ChartTrackDto>();
                if (data.marks == null) data.marks = new List<ChartMarkDto>();
                if (data.nextMarkId < 1) data.nextMarkId = 1;
                ShiftToLiveFrame(data);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ChartJournal] Load failed ({path}): {e.Message}");
                return false;
            }
        }

        /// <summary>Записать журнал (штампуем живой кумулятив).</summary>
        public static void Save(ulong clientId, ChartJournalData data)
        {
            if (data == null) return;
            string path;
            try { path = PathFor(clientId); }
            catch (Exception e)
            {
                Debug.LogWarning($"[ChartJournal] Save path failed: {e.Message}");
                return;
            }
            try
            {
                var live = ProjectC.World.FloatingOrigin.Network.GlobalMotionControlledRebaseSlice.CumulativeRebaseOffset;
                data.rbx = live.x; data.rby = live.y; data.rbz = live.z;
                data.rbFrame = ProjectC.World.FloatingOrigin.Network.GlobalMotionControlledRebaseSlice.CumulativeRebaseFrame;
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                File.WriteAllText(path, JsonUtility.ToJson(data, prettyPrint: false));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ChartJournal] Save failed ({path}): {e.Message}");
            }
        }

        private static void ShiftToLiveFrame(ChartJournalData data)
        {
            var fileOffset = new Vector3(data.rbx, data.rby, data.rbz);
            var live = ProjectC.World.FloatingOrigin.Network.GlobalMotionControlledRebaseSlice.CumulativeRebaseOffset;
            Vector3 delta = live - fileOffset;
            if (delta == Vector3.zero) return;
            if (data.tracks != null)
                foreach (var t in data.tracks)
                {
                    if (t == null) continue;
                    t.x += delta.x; t.y += delta.y; t.z += delta.z;
                }
            if (data.marks != null)
                foreach (var m in data.marks)
                {
                    if (m == null) continue;
                    m.x += delta.x; m.y += delta.y; m.z += delta.z;
                }
            Debug.Log($"[ChartJournal] Frame shift applied: fileOffset={fileOffset} live={live}");
        }
    }
}
