// T-NS00: NpcShipState — сервер-авторитативное состояние NPC-корабля. POCO.
// Pattern: QuestInstance (Quests/Core/QuestInstance.cs) — server-side state per instance.
// Convention: каждый class = отдельный .cs файл (Unity 6: T-DOCK-13c fix).

using UnityEngine;
using ProjectC.Player;

namespace ProjectC.PeacefulShip.Core
{
    /// <summary>
    /// Сервер-авторитативное состояние NPC-корабля. Owned by NpcShipWorld.
    /// NpcInstanceId = NetworkObjectId | 0x8000_0000_0000_0000UL (см. 03_V2_ARCHITECTURE.md §4.1).
    /// </summary>
    public class NpcShipState
    {
        /// <summary>Stable id с sentinel-битом. Отличается от clientId игрока.</summary>
        public readonly ulong NpcInstanceId;

        /// <summary>Ссылка на scene-placed ShipController (server-owned).</summary>
        public readonly ShipController Ship;

        /// <summary>Текущий статус FSM (см. NpcShipStatus.cs).</summary>
        public NpcShipStatus Status;

        /// <summary>Текущий leg маршрута.</summary>
        public NpcShipRoute CurrentRoute;

        /// <summary>Когда вошли в текущий Status (Time.time на сервере).</summary>
        public float StateEnteredAt;

        /// <summary>Индекс в NpcShipSchedule.routes.</summary>
        public int ScheduleIndex;

        /// <summary>Последняя известная позиция — для логирования и трассировки.</summary>
        public Vector3 LastKnownPosition;

        /// <summary>V2 hook (Q10): cargo manifest. В M1 — пустой (capacity=0, items=null).</summary>
        public NpcShipCargoManifest Cargo;

        /// <summary>ID пада, назначенного через AssignPadForNpc. null пока не назначен.</summary>
        public string AssignedPadId;

        /// <summary>Y координата ground-уровня при первом Departing. Используется для удержания cruiseAlt во всех state.</summary>
        public float StartCruiseY;

        public NpcShipState(ulong npcInstanceId, ShipController ship)
        {
            NpcInstanceId = npcInstanceId;
            Ship = ship;
            Status = NpcShipStatus.Idle;
            StateEnteredAt = Time.time;
            ScheduleIndex = 0;
            Cargo = default; // пустой manifest в M1
        }
    }
}