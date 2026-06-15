// Project C: Character Progression — T-P08
// EquipResultDto: server → client ack/deny of TryEquip/TryUnequip. INetworkSerializable.
// Design: docs/Character/05_CLOTHING_AND_MODULES.md §4.2 (EquipmentServer.RequestEquipRpc).
//
// Stub T-P08 stub: EquipResultDto не используется в T-P08, но нужен в T-P09.
// Один result type для обоих RPC (equip/unequip): code enum + itemId + slot + reason string.

using System;
using Unity.Netcode;

namespace ProjectC.Equipment.Dto
{
    public enum EquipResultCode : byte
    {
        Equipped   = 0,  // успех
        Unequipped = 1,  // успех (unequip)
        Denied     = 2,  // generic deny
    }

    [Serializable]
    public struct EquipResultDto : INetworkSerializable, IEquatable<EquipResultDto>
    {
        public EquipResultCode code;
        public int itemId;
        public EquipSlot slot;
        public string reason;  // пустая при success

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            byte codeByte = (byte)code;
            serializer.SerializeValue(ref codeByte);
            if (serializer.IsReader) code = (EquipResultCode)codeByte;
            serializer.SerializeValue(ref itemId);
            byte slotByte = (byte)slot;
            serializer.SerializeValue(ref slotByte);
            if (serializer.IsReader) slot = (EquipSlot)slotByte;

            // string — manual null-guard (NGO 2.x null-string pitfall)
            string reasonCopy = reason ?? string.Empty;
            int len = reasonCopy.Length;
            serializer.SerializeValue(ref len);
            if (len > 0)
            {
                if (serializer.IsReader)
                {
                    var bytes = new byte[len];
                    for (int i = 0; i < len; i++) serializer.SerializeValue(ref bytes[i]);
                    reason = System.Text.Encoding.UTF8.GetString(bytes);
                }
                else
                {
                    var bytes = System.Text.Encoding.UTF8.GetBytes(reasonCopy);
                    for (int i = 0; i < len; i++) serializer.SerializeValue(ref bytes[i]);
                }
            }
            else if (serializer.IsReader)
            {
                reason = string.Empty;
            }
        }

        public static EquipResultDto Equipped(int itemId, EquipSlot slot) => new EquipResultDto
        {
            code = EquipResultCode.Equipped, itemId = itemId, slot = slot, reason = string.Empty,
        };

        public static EquipResultDto Unequipped(EquipSlot slot) => new EquipResultDto
        {
            code = EquipResultCode.Unequipped, itemId = 0, slot = slot, reason = string.Empty,
        };

        public static EquipResultDto Denied(string reason) => new EquipResultDto
        {
            code = EquipResultCode.Denied, itemId = 0, slot = EquipSlot.None, reason = reason ?? string.Empty,
        };

        public bool Equals(EquipResultDto other) =>
            code == other.code && itemId == other.itemId && slot == other.slot && reason == other.reason;

        public override bool Equals(object obj) => obj is EquipResultDto o && Equals(o);
        public override int GetHashCode() => HashCode.Combine((byte)code, itemId, (byte)slot, reason ?? "");
        public static bool operator ==(EquipResultDto a, EquipResultDto b) => a.Equals(b);
        public static bool operator !=(EquipResultDto a, EquipResultDto b) => !a.Equals(b);
    }
}
