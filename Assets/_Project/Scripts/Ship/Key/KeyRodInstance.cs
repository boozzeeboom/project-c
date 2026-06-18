// =====================================================================================
// KeyRodInstance.cs — POCO runtime record для уникального ключа корабля (R2-SHIP-KEY-003, T-KEY-01)
// =====================================================================================
// Документация:
//   • docs/Ships/Key-subsystem/00_OVERVIEW.md §13
//   • docs/Ships/Key-subsystem/20_UNIQUE_KEY_INSTANCE.md §2.2
//   • docs/Ships/Key-subsystem/23_ROADMAP.md T-KEY-01
//
// Назначение: server-only runtime record, представляющий ОДИН физический экземпляр
// ключа корабля. В отличие от ItemData (definition, общий для всех экземпляров),
// каждый KeyRodInstance — уникальная пара (instanceId ↔ registeredShipId).
//
// POCO class (НЕ MonoBehaviour, НЕ NetworkBehaviour). Хранится только в
// KeyRodInstanceWorld (server static facade по типу CraftingWorld). Клиент получает
// данные через RPC/NetworkVariable (см. 22_SHIP_TELEMETRY_PLAN.md).
//
// Lifecycle:
//   • Create:  KeyRodInstanceWorld.CreateInstance(itemId, shipNetId, ownerId)
//   • Modify:  KeyRodInstanceWorld.TransferInstance / UpdateState
//   • Destroy: KeyRodInstanceWorld.DestroyInstance
//
// MVP-граница (Этап 1):
//   • 1 корабль ↔ 1 ключ (1:1, расширение до 1:N — фаза 2)
//   • Передача через drop → pickup другого игрока
//   • Persist через IPlayerDataRepository (T-KEY-PERSIST, отдельный тикет)
//   • НЕ синхронизируется напрямую клиентам — только через NetworkVariable wrappers
//     (см. 22_SHIP_TELEMETRY_PLAN.md §2.4 ShipOwnershipRegistry)
// =====================================================================================

using System;
using UnityEngine;

namespace ProjectC.Ship.Key
{
    /// <summary>
    /// Server-only runtime record для уникального ключа корабля.
    /// Создаётся при крафте/спавне, удаляется при уничтожении предмета.
    /// </summary>
    [Serializable]
    public class KeyRodInstance
    {
        // ===========================================================
        // Identity (immutable after CreateInstance)
        // ===========================================================

        /// <summary>Server-unique монотонный counter. Назначается KeyRodInstanceWorld при создании.
        /// НЕ сохраняется в persistence — при restore пересоздаётся из счётчика репозитория.</summary>
        public int instanceId;

        /// <summary>→ ItemData definition (LightRod/MediumRod/HeavyRod...). Резолвится через
        /// InventoryWorld._itemDatabase. Используется для фильтрации (HasKeyForShip, GetMyShips).</summary>
        public int itemId;

        /// <summary>NetworkObjectId корабля, к которому привязан этот ключ.
        /// 0 = не привязан (salvage/orphaned instance, TODO).</summary>
        public ulong registeredShipId;

        /// <summary>ClientId текущего владельца. Меняется при передаче (drop → pickup другого).
        /// OWNER_NONE = ulong.MaxValue = ключ в мире, не в чьём-то инвентаре.</summary>
        public ulong ownerPlayerId;

        /// <summary>ClientId ПЕРВОГО владельца (при создании). Сохраняется при передачах для истории.
        /// Используется для фазы 2 (salvage/origin tracking). Сейчас только пишется, не читается.</summary>
        public ulong originalOwnerId;

        // ===========================================================
        // Mutable state
        // ===========================================================

        /// <summary>Текущее состояние экземпляра. См. <see cref="KeyRodInstanceState"/>.</summary>
        public KeyRodInstanceState state = KeyRodInstanceState.Active;

        /// <summary>Unix timestamp создания (для отладки/истории).</summary>
        public long createdAtUnix;

        // ===========================================================
        // Sentinel values
        // ===========================================================

        /// <summary>Sentinel: ключ в мире, не в инвентаре игрока.
        /// (Q3, 2026-06-18: пользователь подтвердил ulong.MaxValue).
        /// Не путать с _pendingCanBoardShipId в NetworkPlayer — это другой домен.</summary>
        public const ulong OWNER_NONE = ulong.MaxValue;

        // ===========================================================
        // Helpers
        // ===========================================================

        /// <summary>True если ключ принадлежит конкретному игроку (не в мире).</summary>
        public bool IsOwnedBy(ulong clientId) => ownerPlayerId == clientId && clientId != OWNER_NONE;

        /// <summary>True если ключ можно использовать (в инвентаре валидного владельца).</summary>
        public bool IsActiveAndOwned => state == KeyRodInstanceState.Active
            && ownerPlayerId != OWNER_NONE;

        public override string ToString()
        {
            return $"KeyRodInstance(id={instanceId}, itemId={itemId}, ship={registeredShipId}, " +
                   $"owner={ownerPlayerId}, state={state})";
        }
    }

    /// <summary>
    /// Состояние жизненного цикла экземпляра ключа.
    /// </summary>
    public enum KeyRodInstanceState
    {
        /// <summary>Активен: либо в инвентаре игрока, либо привязан к pickup в мире.</summary>
        Active = 0,

        /// <summary>Удалён из инвентаря навсегда (игрок уничтожил, фаза 2 salvage).</summary>
        Destroyed = 1,

        /// <summary>Дропнут на земле, не подобран. Может быть подобран позже.
        /// TTL не реализован (Q9, 2026-06-18: "пока живёт вечно").</summary>
        Lost = 2,
    }
}