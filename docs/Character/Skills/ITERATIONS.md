# ITERATIONS — Items/Weapons Refactoring

## Итерация от 2026-07-24

**Задача:** T-WPN-01-REF-02 — Унификация оружия, Items ↔ Equipment ↔ Skills ↔ Combat рефакторинг
**Коммит:** `ac4e8a7ee91dad1ab6a4956502ba2156f457c58e` — T-WPN-01-REF-02: Унификация оружия — WeaponItemData + ThrowableItemData → единый класс

**Изменения:**
- `WeaponItemData.cs` — +WeaponHandling enum, +WeaponClass.Throwable, +throw-поля, R3 equipSlot fix
- `ThrowableItemData.cs` — 🗑 Удалён
- `InventoryWorld.cs` — R2 публичные методы (GetItemId, IsItemRegistered, RegisterIfMissing)
- `EquipmentServer.cs` — R2 без reflection, R5 прямые вызовы
- `EquipmentWorld.cs` — R5 прямой вызов SkillsWorld
- `SkillsServer.cs` — R5 прямые вызовы StatsServer/EquipmentWorld
- `SkillsWorld.cs` — R5 прямой вызов StatsServer.ApplyXpDirect
- `SkillsClientState.cs` — R4 кэш навыков
- `SkillInputService.cs` — R4 кэш, +Throwable в DescribeMaskShort
- `ICombatDamageProvider.cs` — комментарий
- `WeaponClassCatalog.cs` — +Throwable entry
- 4 `.asset` конвертированы Throwables/ → Weapons/
- 4 старых `.asset` удалены из Throwables/
