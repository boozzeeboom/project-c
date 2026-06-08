// T-Q18: JSON file-based IQuestStateRepository impl.
// Использует UnityEngine.JsonUtility + Application.persistentDataPath.
// Per 09_OPEN_QUESTIONS.md §H: no debounce, fire-and-forget immediate save.

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ProjectC.Quests.Persistence
{
    public class JsonQuestStateRepository : IQuestStateRepository
    {
        private const string FilePrefix = "quest_state_";
        private const string FileSuffix = ".json";

        // In-memory cache (mirror of disk). Снижает disk reads при частых Load.
        // On boot — populated lazily on first Load for a clientId.
        private readonly Dictionary<ulong, QuestSaveData> _cache = new Dictionary<ulong, QuestSaveData>();
        private readonly object _lock = new object();

        private string GetPath(ulong clientId)
        {
            return Path.Combine(Application.persistentDataPath, FilePrefix + clientId + FileSuffix);
        }

        public QuestSaveData Load(ulong clientId)
        {
            lock (_lock)
            {
                if (_cache.TryGetValue(clientId, out var cached))
                {
                    return cached;
                }
                string path = GetPath(clientId);
                if (!File.Exists(path))
                {
                    return null;
                }
                try
                {
                    string json = File.ReadAllText(path);
                    var data = JsonUtility.FromJson<QuestSaveData>(json);
                    if (data != null)
                    {
                        _cache[clientId] = data;
                    }
                    return data;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[JsonQuestStateRepository] Load failed for client {clientId}: {ex.Message}");
                    return null;
                }
            }
        }

        public bool Save(ulong clientId, QuestSaveData data)
        {
            if (data == null) return false;
            data.savedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            string path = GetPath(clientId);
            string json;
            try
            {
                json = JsonUtility.ToJson(data);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[JsonQuestStateRepository] ToJson failed for client {clientId}: {ex.Message}");
                return false;
            }

            // T-Q18: immediate save (per §H). Atomic write: temp file → rename.
            // Не используем File.WriteAllText напрямую (риск partial write при crash).
            try
            {
                lock (_lock)
                {
                    string tmp = path + ".tmp";
                    File.WriteAllText(tmp, json);
                    if (File.Exists(path)) File.Delete(path);
                    File.Move(tmp, path);
                    _cache[clientId] = data;
                }
                if (Debug.isDebugBuild) Debug.Log($"[JsonQuestStateRepository] Saved player {clientId} state ({json.Length / 1024.0:F1} KB)");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[JsonQuestStateRepository] Save failed for client {clientId}: {ex.Message}");
                return false;
            }
        }

        public bool Delete(ulong clientId)
        {
            lock (_lock)
            {
                _cache.Remove(clientId);
                string path = GetPath(clientId);
                if (!File.Exists(path)) return true;
                try
                {
                    File.Delete(path);
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[JsonQuestStateRepository] Delete failed for client {clientId}: {ex.Message}");
                    return false;
                }
            }
        }

        public int SavedPlayerCount
        {
            get
            {
                lock (_lock)
                {
                    return _cache.Count;
                }
            }
        }
    }
}
