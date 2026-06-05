// =====================================================================================
// InventoryResultCode.cs — коды результатов операций инвентаря (Project C: The Clouds)
// =====================================================================================
// Документация:
//   • docs/dev/INVENTORY_V2_REFACTOR.md — Phase 1 (DTO)
//   • docs/dev/CONTRACT_V2_MIGRATION.md   — эталон C2-рефакторинга
//
// Назначение: enum байт-кодов для InventoryResultDto.code. Паттерн скопирован
// с TradeResultCode / ContractResultCode (ProjectC.Trade.Dto). Байты 0..15 зарезервированы
// под базовые коды, 16..127 — под subsystem-specific.
//
// Лимит: byte (0..255) — больше кодов не нужно.
// =====================================================================================

namespace ProjectC.Items.Dto
{
    public enum InventoryResultCode : byte
    {
        Ok                = 0,
        NotInZone         = 1,   // клиент не в зоне взаимодействия
        InventoryFull     = 2,   // maxSlots превышен
        ItemNotFound      = 3,   // itemId не зарегистрирован в ItemDatabase
        NotEnoughQuantity = 4,   // drop/use больше чем есть в слоте
        InvalidSlot       = 5,   // slotIndex вне диапазона [0, maxSlots)
        RateLimited       = 6,   // слишком много операций в минуту
        InternalError     = 7,   // неожиданный server exception
        NoPermission      = 8,   // RPC вызван не Owner'ом
        ItemNotOwned      = 9,   // операция над предметом, который не в инвентаре
        StackOverflow     = 10,  // qty > maxStack при stack-merge
    }
}
