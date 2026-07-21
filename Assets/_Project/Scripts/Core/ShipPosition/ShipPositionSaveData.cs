// =====================================================================================
// ShipPositionSaveData.cs — DTO для сохранения позиций кораблей (T-PERSIST-DTO)
// =====================================================================================
// Документация:
//   • docs/Ships/SHIP_POSITION_PERSISTENCE_FINAL.md
//
// Чистый DTO без логики. Используется ShipPositionServer для save/restore.
// JsonUtility требует [Serializable] и public поля.
// =====================================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectC.Core.ShipPosition
{
    /// <summary>
    /// Данные сохранения одного корабля (player или NPC).
    /// Матчинг при restore — по shipId (_shipPersistentId).
    /// </summary>
    [Serializable]
    public class ShipPositionSaveData
    {
        // ═══ Identity ═══
        public string shipId;            // _shipPersistentId — стабильный ключ матчинга
        public string sceneName;         // валидация (сцена должна совпадать)
        public bool isNpc;               // true = NPC ship

        // ═══ Transform (всегда) ═══
        public float px, py, pz;         // world position
        public float rx, ry, rz, rw;     // world rotation (quaternion)

        // ═══ Player ship state ═══
        public bool isDocked;
        public bool isEngineRunning;     // T-PLAYER-PERSIST: состояние двигателя

        // ═══ NPC NavTick state ═══
        public int navMode;              // (int)NavMode
        public float dwellTime;          // DwellTime на момент save
        public float dockedSinceTimeOffset; // Time.time - DockedSinceTime (для продолжения dwell)
        public bool scheduleAdvancedAfterDock;
        public bool cargoTradeDone;      // T-CARGO-NPC-01
        public string assignedPadId;     // null если не назначен

        // ═══ NPC flight state ═══
        public float pxCruise, pyCruise, pzCruise;  // CruiseTargetPos
        public float liftStartY;

        // ═══ NPC schedule FSM state (NpcShipState) ═══
        public int scheduleIndex;        // индекс в routes[]
        public string fromLocationId;    // CurrentRoute.fromLocationId
        public string toLocationId;      // CurrentRoute.toLocationId

        // ═══ Meta ═══
        public long savedAtUnix;         // DateTimeOffset.UtcNow.ToUnixTimeSeconds()
    }

    /// <summary>
    /// JsonUtility требует контейнер верхнего уровня для массивов.
    /// </summary>
    [Serializable]
    public class ShipPositionListWrapper
    {
        public List<ShipPositionSaveData> ships = new List<ShipPositionSaveData>();
        public List<PlayerPositionSaveData> players = new List<PlayerPositionSaveData>(); // T-PLAYER-PERSIST
    }

    /// <summary>
    /// T-PLAYER-PERSIST: Данные сохранения позиции игрока.
    /// Матчинг при restore — по clientId (NGO NetworkClientId).
    /// </summary>
    [Serializable]
    public class PlayerPositionSaveData
    {
        public ulong clientId;           // NGO NetworkClientId
        public float px, py, pz;         // world position
        public bool inShip;              // игрок был на корабле в момент save?
        public string shipPersistentId;  // _shipPersistentId корабля (если inShip)
        public long savedAtUnix;
    }
}
