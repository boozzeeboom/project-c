// =====================================================================================
// KeyRodInstanceRepository.cs — persistence для KeyRodInstance (R2-SHIP-KEY-003, T-KEY-PERSIST)
// =====================================================================================
// Документация:
//   • docs/Ships/Key-subsystem/20_UNIQUE_KEY_INSTANCE.md §2.5
//   • docs/Ships/Key-subsystem/23_ROADMAP.md T-KEY-PERSIST
//
// Паттерн скопирован с JsonInventoryRepository (Assets/_Project/Core/JsonInventoryRepository.cs):
//   • IKeyRodInstanceRepository — interface
//   • KeyRodInstanceSaveData — public [Serializable] DTO для JsonUtility
//   • JsonKeyRodInstanceRepository — JSON-файл в Application.persistentDataPath
//
// Формат: один файл KeyRodInstances.json на все экземпляры (проще, чем per-client,
// т.к. instance может менять владельца или быть в мире (OWNER_NONE) без файла владельца).
//
// Auto-save: каждое мутирующее действие (Create/Transfer/UpdateState/Destroy) сохраняет
// ВСЕ instance'ы в файл (fire-and-forget, не debounce). При нагрузке 200+ instance
// можно оптимизировать до diff-saves.
// =====================================================================================

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ProjectC.Ship.Key
{
    /// <summary>
    /// Repository для persistence KeyRodInstance. Все instance'ы в одном JSON-файле.
    /// </summary>
    public interface IKeyRodInstanceRepository
    {
        /// <summary>Load all persisted instances. Returns empty list if file not found.</summary>
        List<KeyRodInstanceSaveData> LoadAll();

        /// <summary>Save all instances overwriting the file. Fire-and-forget (no debounce).</summary>
        void SaveAll(List<KeyRodInstance> instances);
    }

    /// <summary>
    /// Plain DTO для JsonUtility. JsonUtility не сериализует private fields
    /// KeyRodInstance, поэтому создаём посредника с public полями.
    /// </summary>
    [Serializable]
    public class KeyRodInstanceSaveData
    {
        // Identity (не сохраняем instanceId — пересоздаётся при load)
        // instanceId восстанавливается как _nextInstanceId из max(loadedIds)+1
        public int    itemId;
        public ulong  registeredShipId;
        public ulong  ownerPlayerId;
        public ulong  originalOwnerId;
        public int    state;           // (int)KeyRodInstanceState
        public long   createdAtUnix;
    }

    /// <summary>
    /// Default impl: JSON файл в Application.persistentDataPath/KeyRodInstances.json.
    /// Один файл на все instance'ы (simple, atomic, debuggable).
    /// </summary>
    public class JsonKeyRodInstanceRepository : IKeyRodInstanceRepository
    {
        private readonly object _ioLock = new object();

        private string FilePath
        {
            get
            {
                try { return Path.Combine(Application.persistentDataPath, "KeyRodInstances.json"); }
                catch { return "KeyRodInstances.json"; } // fallback для Editor/тесты
            }
        }

        public List<KeyRodInstanceSaveData> LoadAll()
        {
            var path = FilePath;
            lock (_ioLock)
            {
                if (!File.Exists(path))
                {
                    Debug.Log($"[JsonKeyRodInstanceRepository] No save file at {path}. Returning empty.");
                    return new List<KeyRodInstanceSaveData>();
                }

                try
                {
                    var json = File.ReadAllText(path);
                    var wrapper = JsonUtility.FromJson<KeyRodInstanceListWrapper>(json);
                    if (wrapper == null || wrapper.instances == null)
                    {
                        return new List<KeyRodInstanceSaveData>();
                    }
                    Debug.Log($"[JsonKeyRodInstanceRepository] Loaded {wrapper.instances.Count} instances from {path}");
                    return wrapper.instances;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[JsonKeyRodInstanceRepository] LoadAll failed: {ex.Message}. Returning empty.");
                    return new List<KeyRodInstanceSaveData>();
                }
            }
        }

        public void SaveAll(List<KeyRodInstance> instances)
        {
            var path = FilePath;

            var dtoList = new List<KeyRodInstanceSaveData>(instances.Count);
            foreach (var inst in instances)
            {
                dtoList.Add(new KeyRodInstanceSaveData
                {
                    itemId          = inst.itemId,
                    registeredShipId = inst.registeredShipId,
                    ownerPlayerId   = inst.ownerPlayerId,
                    originalOwnerId = inst.originalOwnerId,
                    state           = (int)inst.state,
                    createdAtUnix   = inst.createdAtUnix,
                });
            }

            var wrapper = new KeyRodInstanceListWrapper { instances = dtoList };

            lock (_ioLock)
            {
                try
                {
                    var json = JsonUtility.ToJson(wrapper, prettyPrint: false);
                    File.WriteAllText(path, json);
                    Debug.Log($"[JsonKeyRodInstanceRepository] Saved {instances.Count} instances to {path}");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[JsonKeyRodInstanceRepository] SaveAll failed: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// JsonUtility требует контейнер верхнего уровня для массивов.
        /// </summary>
        [Serializable]
        private class KeyRodInstanceListWrapper
        {
            public List<KeyRodInstanceSaveData> instances = new List<KeyRodInstanceSaveData>();
        }
    }
}