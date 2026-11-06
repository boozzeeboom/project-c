using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ProjectC.World
{
    [Serializable]
    public class HouseDiscoveryDto
    {
        public string houseId;
        public int number;
        public string chunkId;
        public string city;
        public float x, y, z;
        public double utc;
    }

    [Serializable]
    public class HouseDiscoveryData
    {
        public float rbx, rby, rbz;
        public int rbFrame;
        public List<HouseDiscoveryDto> houses = new List<HouseDiscoveryDto>();
    }

    public static class HouseDiscoveryStore
    {
        public static string PathFor(ulong clientId)
        {
            try { return Path.Combine(Application.persistentDataPath, "Chart", $"houses_{clientId}.json"); }
            catch { return $"houses_{clientId}.json"; }
        }

        public static bool TryLoad(ulong clientId, out HouseDiscoveryData data)
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
                data = JsonUtility.FromJson<HouseDiscoveryData>(json);
                if (data == null) return false;
                if (data.houses == null) data.houses = new List<HouseDiscoveryDto>();
                ShiftToLiveFrame(data);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[HouseDiscovery] Load failed ({path}): {e.Message}");
                return false;
            }
        }

        public static void Save(ulong clientId, HouseDiscoveryData data)
        {
            if (data == null) return;
            string path;
            try { path = PathFor(clientId); }
            catch (Exception e)
            {
                Debug.LogWarning($"[HouseDiscovery] Save path failed: {e.Message}");
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
                Debug.LogWarning($"[HouseDiscovery] Save failed ({path}): {e.Message}");
            }
        }

        private static void ShiftToLiveFrame(HouseDiscoveryData data)
        {
            var fileOffset = new Vector3(data.rbx, data.rby, data.rbz);
            var live = ProjectC.World.FloatingOrigin.Network.GlobalMotionControlledRebaseSlice.CumulativeRebaseOffset;
            var delta = live - fileOffset;
            foreach (var h in data.houses)
            {
                h.x += delta.x; h.y += delta.y; h.z += delta.z;
            }
        }
    }
}
