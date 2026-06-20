// T-DOCK-00: DTOs для подсистемы стыковочных портов.
// Содержит 4 INetworkSerializable struct для NGO 2.x RPC.
// Паттерн: see Assets/_Project/Quests/Dto/QuestResultDto.cs.

using Unity.Netcode;
using UnityEngine;

namespace ProjectC.Docking.Dto
{
    /// <summary>
    /// Статичная информация о станции (имя, координаты, altitude).
    /// Не меняется в runtime — для UI отображения.
    /// </summary>
    public struct DockStationInfoDto : INetworkSerializable
    {
        public string stationId;
        public string locationId;
        public string displayName;
        public Vector3 platformCenter;
        public float platformAltitude;

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref stationId);
            s.SerializeValue(ref locationId);
            s.SerializeValue(ref displayName);
            s.SerializeValue(ref platformCenter);
            s.SerializeValue(ref platformAltitude);
        }
    }

    /// <summary>
    /// Информация об одном pad'е на станции.
    /// </summary>
    public struct DockPadInfoDto : INetworkSerializable
    {
        public string padId;
        public Vector3 localPosition;
        public Vector3 localEulerAngles;
        public Vector3 triggerBoxSize;
        public bool isOccupied;             // текущее состояние (для UI)
        public ulong occupiedByClientId;    // 0 = свободен

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref padId);
            s.SerializeValue(ref localPosition);
            s.SerializeValue(ref localEulerAngles);
            s.SerializeValue(ref triggerBoxSize);
            s.SerializeValue(ref isOccupied);
            s.SerializeValue(ref occupiedByClientId);
        }
    }

    /// <summary>
    /// Ответ сервера на RequestDockingRpc: успешное назначение pad'а ИЛИ failure.
    /// </summary>
    public struct DockingAssignmentDto : INetworkSerializable
    {
        public string stationId;
        public string padId;
        public Vector3 approachPoint;        // в мировых координатах
        public float approachAltitude;
        public float approachHeading;        // градусы, Y rotation
        public float landingWindowSeconds;
        public string voiceLine;             // уже выбранная фраза диспетчера
        public ulong shipNetworkObjectId;    // к которому привязано назначение
        public bool success;                 // false = отказ (см. reason)
        public string failReason;            // "NO_SUITABLE_PAD", "STATION_FULL", "RATE_LIMITED"

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref stationId);
            s.SerializeValue(ref padId);
            s.SerializeValue(ref approachPoint);
            s.SerializeValue(ref approachAltitude);
            s.SerializeValue(ref approachHeading);
            s.SerializeValue(ref landingWindowSeconds);
            s.SerializeValue(ref voiceLine);
            s.SerializeValue(ref shipNetworkObjectId);
            s.SerializeValue(ref success);
            s.SerializeValue(ref failReason);
        }
    }

    /// <summary>
    /// Текущий статус стыковки (Assigned/Docked/Cancelled/WrongPad/AwaitingConfirmation).
    /// Сервер шлёт в SendDockingStatusTargetRpc.
    /// </summary>
    public enum DockingStatus : byte
    {
        Idle = 0,             // нет активной стыковки
        Assigned = 1,         // пилот подтвердил, идёт окно посадки
        Approaching = 2,      // (Phase 2: автопилот)
        TouchedDown = 3,      // пилот коснулся pad'а (любого)
        Docked = 4,           // успешная стыковка на правильном pad'е
        Cancelled = 5,        // пилот отменил / окно истекло
        WrongPad = 6          // коснулся чужого pad'а (warning toast)
    }

    public struct DockingStatusDto : INetworkSerializable
    {
        public DockingStatus status;
        public string stationId;
        public string padId;
        public float timestamp;

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            byte sByte = (byte)status;
            s.SerializeValue(ref sByte);
            status = (DockingStatus)sByte;
            s.SerializeValue(ref stationId);
            s.SerializeValue(ref padId);
            s.SerializeValue(ref timestamp);
        }
    }
}
