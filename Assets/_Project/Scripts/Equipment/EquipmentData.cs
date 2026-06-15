// Project C: Character Progression — T-P08
// EquipmentData: parallel arrays для NGO 2.x (Dictionary<,> не сериализуется).
// Design: docs/Character/05_CLOTHING_AND_MODULES.md §5, docs/Character/08_ROADMAP.md T-P08
//
// 13 fixed slots (10 clothing + 3 module, slot index 0..12):
//   0  = Head
//   1  = Chest
//   2  = Legs
//   3  = Feet
//   4  = Back
//   5  = Hands
//   6  = Accessory1
//   7  = Accessory2
//   8  = WeaponMain
//   9  = WeaponOff
//   10 = Module1
//   11 = Module2
//   12 = Module3
// 22 bytes на snapshot (1+4 × 13) — компактно.

using System;
using System.Collections.Generic;
using Unity.Netcode;

namespace ProjectC.Equipment
{
    [Serializable]
    public struct EquipmentData : INetworkSerializable, IEquatable<EquipmentData>
    {
        public const int SLOT_COUNT = 13;

        /// <summary>slotOccupied[i] = 0 или 1. 0 = slot пуст, 1 = slotItemIds[i] валиден.</summary>
        public byte[] slotOccupied;

        /// <summary>slotItemIds[i] = inventory itemId. Валидно только если slotOccupied[i] == 1.</summary>
        public int[] slotItemIds;

        public static EquipmentData Empty => new EquipmentData
        {
            slotOccupied = new byte[SLOT_COUNT],
            slotItemIds   = new int[SLOT_COUNT],
        };

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            if (slotOccupied == null) slotOccupied = new byte[SLOT_COUNT];
            if (slotItemIds   == null) slotItemIds   = new int[SLOT_COUNT];

            for (int i = 0; i < SLOT_COUNT; i++)
            {
                serializer.SerializeValue(ref slotOccupied[i]);
                serializer.SerializeValue(ref slotItemIds[i]);
            }
        }

        // === Slot access ===

        public bool TryGetItemId(EquipSlot slot, out int itemId)
        {
            int idx = SlotToIndex(slot);
            if (idx < 0 || slotOccupied[idx] == 0) { itemId = 0; return false; }
            itemId = slotItemIds[idx];
            return true;
        }

        public void SetItem(EquipSlot slot, int itemId)
        {
            int idx = SlotToIndex(slot);
            if (idx < 0) return;
            slotOccupied[idx] = 1;
            slotItemIds[idx]   = itemId;
        }

        public void ClearSlot(EquipSlot slot)
        {
            int idx = SlotToIndex(slot);
            if (idx < 0) return;
            slotOccupied[idx] = 0;
            slotItemIds[idx]   = 0;
        }

        public bool IsSlotOccupied(EquipSlot slot)
        {
            int idx = SlotToIndex(slot);
            return idx >= 0 && slotOccupied[idx] == 1;
        }

        /// <summary>Вернуть все занятые слоты (для UI refresh / Recompute).</summary>
        public IEnumerable<EquipSlot> EnumerateOccupiedSlots()
        {
            for (int i = 0; i < SLOT_COUNT; i++)
            {
                if (slotOccupied[i] == 0) continue;
                yield return IndexToSlot(i);
            }
        }

        // === Normalization: EquipSlot (enum) → array index (0..12) и обратно ===

        /// <summary>EquipSlot.None → -1 (invalid). Head..WeaponOff → 0..9, Module1..3 → 10..12.</summary>
        public static int SlotToIndex(EquipSlot slot)
        {
            int v = (int)slot;
            if (v == 0) return -1;                // None
            if (v >= 1 && v <= 10) return v - 1;  // Head..WeaponOff → 0..9
            if (v >= 20 && v <= 22) return v - 10; // Module1..3 → 10..12
            return -1;                            // unknown
        }

        public static EquipSlot IndexToSlot(int idx)
        {
            if (idx < 0 || idx >= SLOT_COUNT) return EquipSlot.None;
            if (idx <= 9) return (EquipSlot)(idx + 1);     // 0..9 → 1..10 (Head..WeaponOff)
            return (EquipSlot)(idx + 10);                 // 10..12 → 20..22 (Module1..3)
        }

        // === Equality (для NetworkVariable detection, IEquatable contract) ===

        public bool Equals(EquipmentData other)
        {
            if (slotOccupied == null || other.slotOccupied == null) return false;
            if (slotItemIds   == null || other.slotItemIds   == null) return false;
            for (int i = 0; i < SLOT_COUNT; i++)
            {
                if (slotOccupied[i] != other.slotOccupied[i]) return false;
                if (slotItemIds[i]   != other.slotItemIds[i])   return false;
            }
            return true;
        }

        public override bool Equals(object obj) => obj is EquipmentData o && Equals(o);

        public override int GetHashCode()
        {
            // manual mix (HashCode.Combine max 8 args, у нас 26 элементов)
            unchecked
            {
                int hash = 17;
                if (slotOccupied != null) for (int i = 0; i < SLOT_COUNT; i++) hash = hash * 31 + slotOccupied[i].GetHashCode();
                if (slotItemIds   != null) for (int i = 0; i < SLOT_COUNT; i++) hash = hash * 31 + slotItemIds[i].GetHashCode();
                return hash;
            }
        }

        public static bool operator ==(EquipmentData a, EquipmentData b) => a.Equals(b);
        public static bool operator !=(EquipmentData a, EquipmentData b) => !a.Equals(b);
    }
}
