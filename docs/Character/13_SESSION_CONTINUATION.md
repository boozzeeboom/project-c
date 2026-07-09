# Stats Audit — Session Continuation

**Последняя сессия:** 2026-07-09
**Коммиты:** `8c49ee1` (T-STATS02), `7b8460c` (T-STATS03)

## Выполнено (6/10 проблем)

| # | Проблема | Коммит |
|---|---------|--------|
| P0 | Combat equip bypass | `8c49ee1` |
| P4 | StatsConfig split → 3 SO | `7b8460c` |
| P5 | GatheringServer mapping bypass | `8c49ee1` |
| P6 | Effective stat formula | `8c49ee1` |
| P7 | Skill stat bonuses → combat | `8c49ee1` |
| P10 | DamageResultDto breakdown | `8c49ee1` |

## Осталось (4 проблемы)

| # | Проблема | Приоритет | Файлов |
|---|---------|----------|--------|
| **P1** | Flat 3×3 struct → Dictionary | 🔴 Q2 | ✅ `f4ca1af` |
| P2 | PlayerStatsRef workaround | 🟢 авто-P1 | ✅ `f4ca1af` |
| P3/P9 | Unify Player/NPC stat formula | 🟡 Q3 | ~5 |
| P8 | Equipment multipliers | 🟢 Q3 | ✅ `d609dbb` |

## Следующий шаг: P1

Заменить `PlayerStats` struct (3×3 поля) на `Dictionary<StatType, float>` или `SerializableDictionary`.

### Файлы к изменению:
1. `Assets/_Project/Scripts/Stats/PlayerStats.cs` — struct → dict-based
2. `Assets/_Project/Scripts/Stats/PlayerStatsRef.cs` — упростить или удалить
3. `Assets/_Project/Scripts/Stats/Persistence/CharacterSaveData.cs` — PlayerStatsSave
4. `Assets/_Project/Scripts/Stats/Dto/StatsSnapshotDto.cs` — 12 полей → массив
5. `Assets/_Project/Scripts/Stats/StatsServer.cs` — ApplyXp, SendSnapshotToOwner
6. `Assets/_Project/Scripts/Stats/StatsWorld.cs` — BuildSaveData, LoadPlayer
7. `Assets/_Project/Scripts/Equipment/EquipmentWorld.cs` — GetEquipStatBonuses (out params)
8. `Assets/_Project/Scripts/Equipment/ClothingItemData.cs` — bonus fields
9. `Assets/_Project/Scripts/Equipment/ModuleItemData.cs` — bonus fields
10. `Assets/_Project/Scripts/Combat/Implementations/PlayerAttacker.cs` — GetStrength/Dex/Int
11. `Assets/_Project/Scripts/UI/Client/CharacterWindow.cs` — RefreshStatsDisplay

### Ключевые риски:
- `INetworkSerializable` в `StatsSnapshotDto` — NGO требует фиксированные поля
- `EquipmentWorld.GetEquipStatBonuses` с out-параметрами
- `ClothingItemData`/`ModuleItemData` — ScriptableObject поля (сериализация Unity)

### Планы:
- `Assets/.Aura/plans/stats_audit_fixes_q0_q1_v1.md` — ✅
- `Assets/.Aura/plans/stats_audit_q2_p4_statsconfig_split_v1.md` — ✅

### Документация:
- `docs/Character/12_STATS_ARCHITECTURE_AUDIT_V2.md` — обновлён с отметками выполнения
- `docs/Character/CHANGELOG.md` — запись 2026-07-09
