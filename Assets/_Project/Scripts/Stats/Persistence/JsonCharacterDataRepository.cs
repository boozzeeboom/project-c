// Project C: Character Progression — T-P06
// ICharacterDataRepository + JsonCharacterDataRepository.
// Design: docs/Character/03_DATA_MODEL.md §5, docs/Character/08_ROADMAP.md T-P06
//
// Pattern: копия JsonInventoryRepository (Assets/_Project/Core/JsonInventoryRepository.cs).
// Per-clientId file: Application.persistentDataPath/character_<clientId>.json.
// Sections (расширяемые): stats (T-P06, реально) + equipment (T-P06 stub) + skills (T-P06 stub).
//
// T-P06 SCOPE: stats save/load работает реально. Equipment/Skills поля в DTO уже есть
// как null-готовые, но TryLoad/Save читают/пишут только stats (T-P09/T-P13 добавят).

using System;
using System.IO;
using ProjectC.Stats.Persistence;
using UnityEngine;

namespace ProjectC.Stats.Persistence
{
    /// <summary>
    /// Repository для character progression persistence (stats + equipment + skills).
    /// Один файл на clientId. Per-section expansion: T-P09 (equipment), T-P13 (skills).
    /// </summary>
    public interface ICharacterDataRepository
    {
        /// <summary>Load character data for clientId. Returns false if file not found / corrupt.</summary>
        bool TryLoad(ulong clientId, out CharacterSaveData data);

        /// <summary>Save character data for clientId. Fire-and-forget.</summary>
        void Save(ulong clientId, CharacterSaveData data);

        /// <summary>Delete saved file (admin reset).</summary>
        void Delete(ulong clientId);

        /// <summary>Путь к файлу (для diagnostics/tests).</summary>
        string GetSavePath(ulong clientId);
    }

    /// <summary>
    /// Default impl: per-clientId JSON в Application.persistentDataPath/Character/character_&lt;clientId&gt;.json.
    /// Thread-safe через _ioLock.
    /// </summary>
    /// <remarks>
    /// Atomic write: tmp + Move (как JsonQuestStateRepository рекомендует в roadmap, см. 03_DATA_MODEL.md §5.2
    /// "tmp + Move pattern"). Если process crash между WriteAllText и Move — остаётся .tmp файл,
    /// Load() увидит только .json (tmp игнорируется). В худшем случае — потеря последнего save (OK для MVP).
    /// </remarks>
    public class JsonCharacterDataRepository : ICharacterDataRepository
    {
        private readonly object _ioLock = new object();
        private readonly string _folder;

        /// <summary>
        /// Ctor: customizable folder (для тестов). Default: Application.persistentDataPath/Character.
        /// </summary>
        public JsonCharacterDataRepository(string folder = null)
        {
            _folder = folder ?? Path.Combine(Application.persistentDataPath, "Character");
            if (!Directory.Exists(_folder))
            {
                Directory.CreateDirectory(_folder);
            }
        }

        public string GetSavePath(ulong clientId)
        {
            return Path.Combine(_folder, $"character_{clientId}.json");
        }

        public bool TryLoad(ulong clientId, out CharacterSaveData data)
        {
            data = new CharacterSaveData();
            var path = GetSavePath(clientId);
            lock (_ioLock)
            {
                if (!File.Exists(path)) return false;
                try
                {
                    var json = File.ReadAllText(path);
                    var dto = JsonUtility.FromJson<CharacterSaveData>(json);
                    if (dto == null) return false;
                    data = dto;
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[JsonCharacterDataRepository] Load failed for client {clientId}: {ex.Message}. Returning default.");
                    data = new CharacterSaveData();
                    return false;
                }
            }
        }

        public void Save(ulong clientId, CharacterSaveData data)
        {
            if (data == null)
            {
                Debug.LogError($"[JsonCharacterDataRepository] Save called with null data for client {clientId}");
                return;
            }
            var path = GetSavePath(clientId);
            var tmpPath = path + ".tmp";
            lock (_ioLock)
            {
                try
                {
                    var json = JsonUtility.ToJson(data, prettyPrint: false);
                    File.WriteAllText(tmpPath, json);
                    if (File.Exists(path)) File.Delete(path);
                    File.Move(tmpPath, path);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[JsonCharacterDataRepository] Save failed for client {clientId}: {ex.Message}");
                }
            }
        }

        public void Delete(ulong clientId)
        {
            var path = GetSavePath(clientId);
            lock (_ioLock)
            {
                if (File.Exists(path))
                {
                    try { File.Delete(path); }
                    catch (Exception ex) { Debug.LogError($"[JsonCharacterDataRepository] Delete failed: {ex.Message}"); }
                }
            }
        }
    }
}
