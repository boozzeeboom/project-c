// =====================================================================================
// ShipTelemetryState.cs — NetworkVariable payload для ship telemetry (R2-SHIP-KEY-003, T-KEY-07)
// =====================================================================================
// Документация:
//   • docs/Ships/Key-subsystem/22_SHIP_TELEMETRY_PLAN.md §2.2
//   • docs/Ships/Key-subsystem/23_ROADMAP.md T-KEY-07
//
// Назначение: server-authoritative state корабля, синхронизируется клиентам через
// NetworkVariable<ShipTelemetryState> на ShipController. Содержит достаточно данных
// для HUD (топливо, скорость) и UI "Мои корабли" (груз, модули, position, owner).
//
// NetworkVariable semantics:
//   • Server пишет в ShipController.UpdateTelemetryState() (5 Hz throttle)
//   • Клиент читает через ShipTelemetryClientState (агрегатор)
//   • IEquatable нужен для NGO delta-detection (NetworkVariable не шлёт если Equals=true)
// =====================================================================================

using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace ProjectC.Ship.Network
{
    /// <summary>Server-authoritative state корабля. Полностью синхронизируется
    /// клиентам через NetworkVariable. Хранит достаточно данных для HUD + UI.</summary>
    public struct ShipTelemetryState : INetworkSerializable, IEquatable<ShipTelemetryState>
    {
        public ulong shipNetworkObjectId;
        public int   keyInstanceId;          // → KeyRodInstance.instanceId (0 = не привязан)
        public FixedString64Bytes displayName;
        public FixedString32Bytes className;
        public Vector3 position;
        public Vector3 rotationEuler;
        public float  fuelNormalized;         // 0..1
        public float  fuelMax;                // абсолютный максимум
        public int    cargoUsed;
        public int    cargoMax;
        public int    moduleCount;
        public byte   state;                  // (byte)ShipState enum (или generic byte если enum ещё не стабилен)
        public ulong  ownerClientId;          // ← кто владеет ключом (для фильтрации)
        public double lastUpdateServerTime;   // для отладки stale-данных

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref shipNetworkObjectId);
            serializer.SerializeValue(ref keyInstanceId);
            serializer.SerializeValue(ref displayName);
            serializer.SerializeValue(ref className);
            serializer.SerializeValue(ref position);
            serializer.SerializeValue(ref rotationEuler);
            serializer.SerializeValue(ref fuelNormalized);
            serializer.SerializeValue(ref fuelMax);
            serializer.SerializeValue(ref cargoUsed);
            serializer.SerializeValue(ref cargoMax);
            serializer.SerializeValue(ref moduleCount);
            serializer.SerializeValue(ref state);
            serializer.SerializeValue(ref ownerClientId);
            serializer.SerializeValue(ref lastUpdateServerTime);
        }

        public bool Equals(ShipTelemetryState other)
        {
            return shipNetworkObjectId == other.shipNetworkObjectId
                && keyInstanceId == other.keyInstanceId
                && displayName.Equals(other.displayName)
                && className.Equals(other.className)
                && position == other.position
                && rotationEuler == other.rotationEuler
                && Mathf.Approximately(fuelNormalized, other.fuelNormalized)
                && Mathf.Approximately(fuelMax, other.fuelMax)
                && cargoUsed == other.cargoUsed
                && cargoMax == other.cargoMax
                && moduleCount == other.moduleCount
                && state == other.state
                && ownerClientId == other.ownerClientId;
        }

        public override bool Equals(object obj) => obj is ShipTelemetryState o && Equals(o);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + shipNetworkObjectId.GetHashCode();
                hash = hash * 31 + keyInstanceId;
                hash = hash * 31 + displayName.GetHashCode();
                hash = hash * 31 + className.GetHashCode();
                hash = hash * 31 + position.GetHashCode();
                hash = hash * 31 + rotationEuler.GetHashCode();
                hash = hash * 31 + fuelNormalized.GetHashCode();
                hash = hash * 31 + fuelMax.GetHashCode();
                hash = hash * 31 + cargoUsed;
                hash = hash * 31 + cargoMax;
                hash = hash * 31 + moduleCount;
                hash = hash * 31 + state;
                hash = hash * 31 + ownerClientId.GetHashCode();
                return hash;
            }
        }

        public static bool operator ==(ShipTelemetryState a, ShipTelemetryState b) => a.Equals(b);
        public static bool operator !=(ShipTelemetryState a, ShipTelemetryState b) => !a.Equals(b);
    }
}