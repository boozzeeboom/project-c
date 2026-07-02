// =====================================================================================
// ShipTelemetryState.cs — NetworkVariable payload для ship telemetry (R2-SHIP-KEY-003, T-KEY-07)
// =====================================================================================
// Документация:
//   • docs/Ships/Key-subsystem/22_SHIP_TELEMETRY_PLAN.md §2.2
//   • docs/Ships/Key-subsystem/23_ROADMAP.md T-KEY-07
//   • docs/Ships/cargo_system/CARGO_UI_01_DESIGN_2026-07-02.md (T-CARGO-UI-01: cargoDetail)
//
// Назначение: server-authoritative state корабля, синхронизируется клиентам через
// NetworkVariable<ShipTelemetryState> на ShipController. Содержит достаточно данных
// для HUD (топливо, скорость) и UI "Мои корабли" (груз, модули, position, owner).
//
// NetworkVariable semantics:
//   • Server пишет в ShipController.UpdateTelemetryState() (5 Hz throttle)
//   • Клиент читает через ShipTelemetryClientState (агрегатор)
//   • IEquatable нужен для NGO delta-detection (NetworkVariable не шлёт если Equals=true)
//
// T-CARGO-UI-01: добавлен cargoDetail[] — детальный список items (itemId + displayName +
// quantity + unitWeight + dangerous/fragile flags). Сервер резолвит displayName/flags из
// TradeItemDefinition, клиент получает готовую проекцию (без Resources.Load).
// =====================================================================================

using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace ProjectC.Ship.Network
{
    /// <summary>T-CARGO-UI-01: одна запись груза корабля в telemetry snapshot.
    /// Сервер резолвит displayName/unitWeight/dangerous/fragile из TradeItemDefinition —
    /// клиент получает готовую проекцию (без Resources.Load на каждом tick).</summary>
    public struct CargoDetailDto : INetworkSerializable, IEquatable<CargoDetailDto>
    {
        public string itemId;            // "mesium_canister_v01"
        public FixedString64Bytes displayName; // "Мезиум (канистра)"
        public int    quantity;          // 5
        public float  unitWeight;        // 100 kg (per-unit вес)
        public byte   flags;             // bit0 = isDangerous, bit1 = isFragile (T-CARGO-UI-01 экономит bandwidth)

        public bool IsDangerous => (flags & 0x01) != 0;
        public bool IsFragile   => (flags & 0x02) != 0;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref itemId);
            serializer.SerializeValue(ref displayName);
            serializer.SerializeValue(ref quantity);
            serializer.SerializeValue(ref unitWeight);
            serializer.SerializeValue(ref flags);
        }

        public bool Equals(CargoDetailDto other)
            => itemId == other.itemId
            && displayName.Equals(other.displayName)
            && quantity == other.quantity
            && Mathf.Approximately(unitWeight, other.unitWeight)
            && flags == other.flags;

        public override bool Equals(object obj) => obj is CargoDetailDto o && Equals(o);
        public override int GetHashCode() => HashCode.Combine(itemId, displayName, quantity, unitWeight, flags);
    }

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
        public int    cargoUsed;              // T-CARGO-UI-01: теперь = sum(qty * slots), не Items.Count
        public int    cargoMax;               // T-CARGO-UI-01: фикс — был всегда 0, теперь через GetEffectiveCargoLimits()
        public int    moduleCount;
        public byte   state;                  // (byte)ShipState enum (или generic byte если enum ещё не стабилен)
        public ulong  ownerClientId;          // ← кто владеет ключом (для фильтрации)
        public double lastUpdateServerTime;   // для отладки stale-данных

        // T-CARGO-UI-01: детальный список items. null/empty = трюм пуст.
        // Server резолвит displayName/unitWeight/dangerous/fragile на сервере.
        // Cap = 32 items (Light=4 / Medium=10 / Heavy=20 / HeavyII=30 + ~6-12 module-bonus slots).
        public CargoDetailDto[] cargoDetail;

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

            // T-CARGO-UI-01: cargoDetail array. NGO 2.x pattern: re-create on reader, copy on writer.
            int len = cargoDetail != null ? cargoDetail.Length : 0;
            serializer.SerializeValue(ref len);
            if (serializer.IsReader && len > 0)
                cargoDetail = new CargoDetailDto[len];
            for (int i = 0; i < len; i++)
            {
                var entry = (cargoDetail != null && i < cargoDetail.Length) ? cargoDetail[i] : default;
                entry.NetworkSerialize(serializer);
                if (cargoDetail != null) cargoDetail[i] = entry;
            }
        }

        public bool Equals(ShipTelemetryState other)
        {
            if (shipNetworkObjectId != other.shipNetworkObjectId) return false;
            if (keyInstanceId != other.keyInstanceId) return false;
            if (!displayName.Equals(other.displayName)) return false;
            if (!className.Equals(other.className)) return false;
            if (position != other.position) return false;
            if (rotationEuler != other.rotationEuler) return false;
            if (!Mathf.Approximately(fuelNormalized, other.fuelNormalized)) return false;
            if (!Mathf.Approximately(fuelMax, other.fuelMax)) return false;
            if (cargoUsed != other.cargoUsed) return false;
            if (cargoMax != other.cargoMax) return false;
            if (moduleCount != other.moduleCount) return false;
            if (state != other.state) return false;
            if (ownerClientId != other.ownerClientId) return false;
            // T-CARGO-UI-01: cargoDetail включаем в Equals — иначе NetworkVariable
            // не увидит изменение items и не пошлёт delta. Сравнение по длине + поэлементно.
            int thisLen = cargoDetail != null ? cargoDetail.Length : 0;
            int otherLen = other.cargoDetail != null ? other.cargoDetail.Length : 0;
            if (thisLen != otherLen) return false;
            for (int i = 0; i < thisLen; i++)
            {
                if (!cargoDetail[i].Equals(other.cargoDetail[i])) return false;
            }
            return true;
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