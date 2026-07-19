// =====================================================================================
// ShipPositionRepository.cs — persistence для позиций кораблей (T-PERSIST-REPO)
// =====================================================================================
// Документация:
//   • docs/Ships/SHIP_POSITION_PERSISTENCE_FINAL.md §5.2
//
// Паттерн скопирован с JsonKeyRodInstanceRepository:
//   • IShipPositionRepository — interface
//   • JsonShipPositionRepository — JSON-файл в Application.persistentDataPath
//   • lock (_ioLock) для thread safety
//   • JsonUtility.ToJson/FromJson
// =====================================================================================

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ProjectC.Core.ShipPosition
{
    /// <summary>
    /// Repository для persistence позиций кораблей.
    /// Все корабли в одном JSON-файле: ShipPositions.json.
    /// </summary>
    public interface IShipPositionRepository
    {
        /// <summary>Load all persisted ship positions. Returns empty list if file not found.</summary>
        List<ShipPositionSaveData> LoadAll();

        /// <summary>Save all ship positions overwriting the file.</summary>
        void SaveAll(List<ShipPositionSaveData> ships);
    }

    /// <summary>
    /// Default impl: JSON файл в Application.persistentDataPath/ShipPositions.json.
    /// Один файл на все корабли (simple, atomic, debuggable).
    /// </summary>
    public class JsonShipPositionRepository : IShipPositionRepository
    {
        private readonly object _ioLock = new object();

        private string FilePath
        {
            get
            {
                try { return Path.Combine(Application.persistentDataPath, "ShipPositions.json"); }
                catch { return "ShipPositions.json"; } // fallback для Editor/тесты
            }
        }

        public List<ShipPositionSaveData> LoadAll()
        {
            var path = FilePath;
            lock (_ioLock)
            {
                if (!File.Exists(path))
                {
                    Debug.Log($"[JsonShipPositionRepository] No save file at {path}. Returning empty.");
                    return new List<ShipPositionSaveData>();
                }

                try
                {
                    var json = File.ReadAllText(path);
                    var wrapper = JsonUtility.FromJson<ShipPositionListWrapper>(json);
                    if (wrapper == null || wrapper.ships == null)
                    {
                        return new List<ShipPositionSaveData>();
                    }
                    Debug.Log($"[JsonShipPositionRepository] Loaded {wrapper.ships.Count} ships from {path}");
                    return wrapper.ships;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[JsonShipPositionRepository] LoadAll failed: {ex.Message}. Returning empty.");
                    return new List<ShipPositionSaveData>();
                }
            }
        }

        public void SaveAll(List<ShipPositionSaveData> ships)
        {
            var path = FilePath;

            var wrapper = new ShipPositionListWrapper { ships = ships };

            lock (_ioLock)
            {
                try
                {
                    var json = JsonUtility.ToJson(wrapper, prettyPrint: false);
                    File.WriteAllText(path, json);
                    Debug.Log($"[JsonShipPositionRepository] Saved {ships.Count} ships to {path}");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[JsonShipPositionRepository] SaveAll failed: {ex.Message}");
                }
            }
        }
    }
}
