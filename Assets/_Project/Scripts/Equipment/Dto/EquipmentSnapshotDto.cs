// Project C: Character Progression — T-P08
// EquipmentSnapshotDto: server → client sync payload для equipment.
// INetworkSerializable struct. Design: docs/Character/05_CLOTHING_AND_MODULES.md §5.

using System;
using Unity.Netcode;

namespace ProjectC.Equipment.Dto
{
    [Serializable]
    public struct EquipmentSnapshotDto : INetworkSerializable, IEquatable<EquipmentSnapshotDto>
    {
        public EquipmentData equip;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            // delegate to EquipmentData's NetworkSerialize
            EquipmentData copy = equip;
            copy.NetworkSerialize(serializer);
            equip = copy;
        }

        public bool Equals(EquipmentSnapshotDto other) => equip.Equals(other.equip);

        public override bool Equals(object obj) => obj is EquipmentSnapshotDto o && Equals(o);

        public override int GetHashCode() => equip.GetHashCode();

        public static bool operator ==(EquipmentSnapshotDto a, EquipmentSnapshotDto b) => a.Equals(b);
        public static bool operator !=(EquipmentSnapshotDto a, EquipmentSnapshotDto b) => !a.Equals(b);
    }
}
