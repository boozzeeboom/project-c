# Audit: Items ↔ Equipment ↔ Skills ↔ Combat — Refactoring Plan

> **Дата:** 2026-07-24
> **Статус:** ✅ Завершён (R1-R5)
> **Предыдущий аудит:** `AUDIT_2026-06-26_CURRENT_STATE_AND_NEXT_STEPS.md`

---

## Результаты рефакторинга

### R1 ✅ Унифицирован класс оружия
- `ThrowableItemData` удалён. Все предметы теперь `WeaponItemData`.
- Добавлен `WeaponHandling` enum (Melee / Ranged / Thrown / Placed).
- Добавлен `WeaponClass.Throwable` в enum и `WeaponClassMask`.
- Поля `explosionRadius`, `throwRange`, `fuseTimeSec` в `WeaponItemData`.
- 4 `.asset` конвертированы из `Throwables/` → `Weapons/`.
- `Hunting Crossbow` исправлен: `WeaponClass.Crossbow`, `handling=Ranged`.

### R2 ✅ Единая регистрация предметов
- `InventoryWorld` получил публичные `GetItemId()`, `IsItemRegistered()`, `RegisterIfMissing()`.
- `EquipmentServer.RegisterEquipmentAssets` переписан без reflection-хака.
- `FindItemIdByName`, equip/unequip flow больше не лезут в `_itemDatabase` через reflection.

### R3 ✅ Граната = расходник
- `OnValidate` ставит `equipSlot = None` для `WeaponClass.Throwable`.
- Гранаты больше не экипируются в слоты оружия.

### R4 ✅ Client-side skill кэш
- `SkillsClientState` кэширует `Dictionary<string, SkillNodeConfig>` при `Awake()`.
- `SkillInputService.TryActivate` читает из кэша (fallback: `Resources.LoadAll`).

### R5 ✅ Прямые вызовы вместо reflection
- `EquipmentServer.TriggerStatsRecompute` → прямой `StatsServer.Instance.RecomputeAndSendSnapshot()`
- `EquipmentWorld.GetLearnedSkillIdsSafe` → прямой `SkillsWorld.Instance.GetLearnedSkillIds()`
- `SkillsServer.TriggerStatsRecompute` → прямой `StatsServer.Instance.RecomputeAndSendSnapshot()`
- `SkillsServer.TriggerEquipmentRecheck` → прямой `EquipmentWorld.Instance`
- `SkillsWorld.TryLearnSkill` → прямой `StatsServer.Instance.ApplyXpDirect()`

### Изменённые файлы (11)
| Файл | Изменение |
|---|---|
| `WeaponItemData.cs` | +WeaponHandling, +WeaponClass.Throwable, +throw-поля, R3 equipSlot |
| `ThrowableItemData.cs` | 🗑 Удалён |
| `ICombatDamageProvider.cs` | Комментарий |
| `WeaponClassCatalog.cs` | +Throwable entry |
| `EquipmentServer.cs` | R2 API, R5 прямой вызов |
| `EquipmentWorld.cs` | R5 прямой вызов |
| `InventoryWorld.cs` | R2 публичные методы |
| `SkillsServer.cs` | R5 прямые вызовы |
| `SkillsWorld.cs` | R5 прямой вызов |
| `SkillsClientState.cs` | R4 кэш навыков |
| `SkillInputService.cs` | R4 кэш, +Throwable в DescribeMaskShort |

### Конвертированные ассеты (4)
| Старый | Новый |
|---|---|
| `Throwables/Hunting Crossbow.asset` | `Weapons/Weapon_Hunting Crossbow.asset` (Crossbow) |
| `Throwables/Throwable_Grenade_Basic.asset` | `Weapons/Weapon_Grenade_Basic.asset` |
| `Throwables/Throwable_Grenade_Antigrav.asset` | `Weapons/Weapon_Grenade_Antigrav.asset` |
| `Throwables/Grenade.asset` | `Weapons/Weapon_Grenade.asset` |

