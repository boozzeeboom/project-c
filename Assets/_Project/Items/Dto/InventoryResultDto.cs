// =====================================================================================
// InventoryResultDto.cs — результат операции над инвентарём (Project C: The Clouds)
// =====================================================================================
// Документация:
//   • docs/dev/INVENTORY_V2_REFACTOR.md — Phase 1 (DTO)
//
// Назначение: ответ сервера на RequestPickupRpc / RequestDropRpc / RequestMoveRpc /
// RequestUseRpc. Шлётся через NetworkPlayer.ReceiveInventoryResultTargetRpc. Клиент
// обновляет InventoryClientState.LastResult и дёргает OnInventoryResult — UI показывает
// feedback (message label, error toast, optimistic-update revert).
//
// Поля:
//   • code      — (byte)InventoryResultCode. Ok = 0 = success.
//   • message   — локализованное сообщение для UI (на русском, как в Trade).
//                 На сервере: локализация берётся из InventoryClientState.LocalizeResultCode
//                 (вызов НЕ из server-кода, а из world-кода, чтобы не тащить Unity локализацию
//                 в логику). Pitfall: сервер шлёт уже локализованную строку, клиент показывает as-is.
//   • itemId    — какой item был целью операции (-1 если не применимо, например rate-limit).
//   • slotIndex — какой слот был целью (-1 если не применимо).
//   • newCredits — баланс кредитов после операции (для cross-tab в CharacterWindow).
//                 -1 если операция не меняла credits.
//
// NGO 2.x pitfall #14 (struct == null): InventoryResultDto — struct, не class.
// Поэтому `if (result == null) return;` НЕ КОМПИЛИРУЕТСЯ. Используй `if (!result.IsSuccess) ...`.
// =====================================================================================

using System;
using Unity.Netcode;

namespace ProjectC.Items.Dto
{
    [Serializable]
    public struct InventoryResultDto : INetworkSerializable
    {
        public byte   code;
        public string message;
        public int    itemId;
        public int    slotIndex;
        public float  newCredits;

        public bool IsSuccess => code == (byte)InventoryResultCode.Ok;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref code);

            // string nullable
            bool hasMsg = !string.IsNullOrEmpty(message);
            serializer.SerializeValue(ref hasMsg);
            if (hasMsg)
            {
                if (serializer.IsReader) message = string.Empty;
                serializer.SerializeValue(ref message);
            }
            else
            {
                if (serializer.IsReader) message = null;
            }

            serializer.SerializeValue(ref itemId);
            serializer.SerializeValue(ref slotIndex);
            serializer.SerializeValue(ref newCredits);
        }
    }
}
