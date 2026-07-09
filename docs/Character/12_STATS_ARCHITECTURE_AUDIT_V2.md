# Архитектурный аудит системы статов (STR/DEX/INT) — v2

**Дата:** 2026-07-09
**Предыдущий аудит:** `docs/Character/11_STATS_ARCHITECTURE_AUDIT.md` (эталон, не перезаписан)
**Метод:** глубокий trace code: от `DamageCalculator.Calculate` → `IAttacker.GetStrength()` → `PlayerAttacker` / `NpcAttacker` → `StatsWorld` / `NpcCombatData` → `StatsServer` / `EquipmentWorld` → source-to-stat mapping.

---

## 1. Общий вывод

Система статов **работоспособна** для MVP-прототипа (XP копится, тиры растут, урон рассчитывается), но имеет **10 архитектурных проблем**, из которых **6 не описаны в первом аудите** (11_STATS_ARCHITECTURE_AUDIT.md). Проблемы P0–P3 (из первого аудита) **подтверждены кодом**. Добавлены P4–P10.

---

## 2. Классификация проблем

### P0 [Критическая] — Combat bypasses equip bonuses (подтверждён)

**Где:** `PlayerAttacker.cs:136`, `DamageCalculator.cs:45`

```csharp
// PlayerAttacker
public int GetStrength() => StatsToFlat(StatsWorld.Instance?.GetOrCreateStats(_clientId).strengthTier ?? 0);
private static int StatsToFlat(int tier) => tier * 5 + 10;

// DamageCalculator
int baseAttack = roll + source.GetBaseDamage() + attacker.GetStrength(); // ← ТОЛЬКО tier-based
```

Equipment бонусы (`ClothingItemData.strengthBonus`, `ModuleItemData.strengthBonus`) **НЕ включены** в `GetStrength()`. `StatsServer.RecomputeAndSendSnapshot` вычисляет `effectiveStrength` для UI, но это значение никем не читается в combat path.

**Последствие:** Бесполезная экипировка. Игрок надевает +5 STR belt → урон не меняется.

---

### P1 [Средняя] — Flat 3×3 struct — rigid for expansion (подтверждён)

**Где:** `PlayerStats.cs`, `PlayerStatsRef.cs`, `StatsSnapshotDto.cs`, `EquipmentWorld.cs`

Добавление нового стата (например, `Luck`, `Charisma`) требует правки **8+ файлов**:
1. `PlayerStats.cs` — поле
2. `PlayerStatsRef.cs` — switch case
3. `PlayerStatsSave.cs` — сериализация
4. `StatsSnapshotDto.cs` — 3 поля + `NetworkSerialize` + `Equals`/`GetHashCode`
5. `EquipmentWorld.cs` — `out float bonusLuck` + switch
6. `ClothingItemData.cs` — `luckBonus` + `luckMultiplier`
7. `ModuleItemData.cs` — `luckBonus`
8. Все места где читается `effectiveStrength`/`effectiveDexterity`/`effectiveIntelligence` в UI

**Последствие:** Каждый новый стат — инвазия по всему стеку.

---

### P2 [Низкая] — PlayerStatsRef — workaround for rigid struct (подтверждён)

`PlayerStatsRef` — static helper с `ref return` switch. Существует только потому, что P1 не решён. При `Dictionary<StatType, float>` этот класс не нужен.

---

### P3 [Средняя] — Two stat systems — Player vs NPC (подтверждён, расширен)

| Аспект | Player | NPC |
|---|---|---|
| Хранилище | `PlayerStats` struct (tier + XP) | `NpcCombatData` SO (flat int) |
| Формула | `tier × 5 + 10` | `_data.strength` напрямую |
| Progression | XP-накопление, tier promotion | Нет (статичный SO) |
| Equip bonuses | Есть поля, не используются | Нет |
| Skill bonuses | Есть (`SkillEffect.StatMod`), не применяются | Нет |

NPC при атаке другого NPC используют свой `_data.strength`, при атаке игрока — тоже. Два разных вычисления для одного и того же `IAttacker.GetStrength()`.

---

### P4 [Средняя] — NEW: StatsConfig — single SO doing 4 responsibilities

**Где:** `StatsConfig.cs`

Один ScriptableObject делает:
1. **Mapping** XpSource → StatType (9 строк switch)
2. **XP formula**: `TierBaseXp`, `TierGrowthRate`, `GlobalMultiplier`
3. **Distance thresholds**: `WalkDistanceXpThreshold`, `PilotDistanceXpThreshold`, `TrackTotalDistance`
4. **Debug flag**: `DebugLogging`

Если понадобится другой баланс для PvP-зоны или туториала — нельзя изолировать формулу от mapping'а без дублирования всего SO.

---

### P5 [Средняя] — NEW: StatsConfig mapping bypassed in code

**Где:** `GatheringServer.cs:167`

```csharp
ss.ApplyXp(clientId, ProjectC.Stats.StatType.Strength, ...);
```

Вместо `_config.GetStatFor(XpSource.Mining)`. Если дизайнер поменяет маппинг Mining → Dexterity в StatsConfig, GatheringServer всё равно будет давать STR XP.

Остальные 8 ивент-хендлеров в `StatsServer.cs` используют `_config.GetStatFor(XpSource.*)` — корректно. Проблема только в GatheringServer.

---

### P6 [Критическая] — NEW: Effective stat formula inconsistency

В combat path:
```csharp
// PlayerAttacker
GetStrength() = tier × 5 + 10
```

В snapshot path:
```csharp
// StatsServer.SendSnapshotToOwner (line 480)
effectiveStrength = stats.strength + bonusStr
// где stats.strength = ТЕКУЩИЙ XP в тире (0..XpForNextTier), НЕ tier-based flat
```

**Два разных вычисления для одного понятия.** `stats.strength` — это XP (0..threshold), не stat. Snapshot показывает некорректный «effectiveStrength», который не совпадает с боевым значением.

---

### P7 [Средняя] — NEW: Skill stat bonuses never applied to combat

**Где:** `SkillEffect.cs:43-46` (определение), `PlayerAttacker.cs:136` (использование)

`SkillEffect.Type.StatMod` может давать `+floatValue` и `×multiplier` к любому StatType. Но:
- `SkillsWorld` / `SkillsServer` не записывают бонусы в PlayerStats
- `PlayerAttacker.GetStrength()` не читает SkillsWorld
- Результат: изучение навыка «Master of Arms +2 STR» не влияет на урон

---

### P8 [Низкая] — NEW: Equipment multipliers silently ignored

**Где:** `EquipmentWorld.cs:79-89`

```csharp
if (data is ClothingItemData c)
{
    bonusStrength += c.strengthBonus;      // ✅ additive учтён
    // c.strengthMultiplier не читается!    // ❌ multiplicative игнорируется
}
```

`ClothingItemData` имеет `strengthMultiplier/dexterityMultiplier/intelligenceMultiplier` (range 0..5), но `GetEquipStatBonuses` складывает только additive бонусы. Мультипликаторы определены в модели данных, но никогда не применяются.

---

### P9 [Низкая] — NEW: NPC stats not linked to any formula

```csharp
// NpcCombatData
public int strength = 10;  // Просто число.

// NpcAttacker.GetStrength()
public int GetStrength() => _data != null ? _data.strength : 10;
```

Нет общей формулы `tier*5+10` для NPC. Если у игрока tier 0 → STR=10 (совпадает с NPC default). При tier 10 → STR=60. NPC всегда STR=10 (если дизайнер не выставит больше). Разрыв растёт с прогрессией.

---

### P10 [Низкая] — NEW: DamageResultDto lacks stat breakdown

`DamageResultDto.cs` имеет одно поле `baseAttack = roll + base + STR`. Невозможно диагностировать: «урон слишком низкий из-за STR, из-за base damage или из-за dice?»

---

## 3. Сверка с первым аудитом (11_STATS_ARCHITECTURE_AUDIT.md)

| Проблема | Первый аудит | Мой аудит | Статус |
|---|---|---|---|
| P0 — Combat equip | ✅ P0 | ✅ P0 (подтверждён, код прочитан) | Совпадает |
| P1 — Flat struct | ✅ P1 | ✅ P1 (подтверждён, +8 файлов инвазии) | Совпадает |
| P2 — PlayerStatsRef | ✅ P2 | ✅ P2 (подтверждён) | Совпадает |
| P3 — Player vs NPC | ✅ P3 | ✅ P3 (расширен таблицей различий) | Углублён |
| P4 — StatsConfig overload | — | ✅ NEW | Добавлен |
| P5 — Mapping bypass | — | ✅ NEW | Добавлен |
| P6 — Formula inconsistency | — | ✅ NEW | Добавлен |
| P7 — Skill bonuses gap | — | ✅ NEW | Добавлен |
| P8 — Multipliers ignored | — | ✅ NEW | Добавлен |
| P9 — NPC formula gap | — | ✅ NEW | Добавлен |
| P10 — DTO no breakdown | — | ✅ NEW | Добавлен |

---

## 4. Рекомендации (взвешенные, по приоритету)

### Q0 — Немедленно (до следующего коммита) ✅ DONE (T-STATS02, 8c49ee1)
1. **P0**: ✅ `PlayerAttacker.GetStrength()` включает `EquipmentWorld.GetEquipStatBonuses` + `SkillsWorld.GetStatModBonuses`.
   ```csharp
   // PlayerAttacker.cs
   public int GetStrength()
   {
       int tier = StatsWorld.Instance?.GetOrCreateStats(_clientId).strengthTier ?? 0;
       int fromTier = StatsToFlat(tier); // tier * 5 + 10
       float equipBonus = 0f;
       EquipmentWorld.Instance?.GetEquipStatBonuses(_clientId, out float s, out _, out _);
       return fromTier + Mathf.RoundToInt(s);
   }
   ```
   Аналогично для DEX и INT.

2. **P6**: ✅ `effectiveStrength = PlayerStats.StatsToFlat(stats.strengthTier) + bonusStr`.

### Q1 — В текущем спринте ✅ DONE (T-STATS02, 8c49ee1)
3. **P5**: ✅ `GatheringServer.cs:167` — `ss.GetStatFor(XpSource.Mining)` через StatsServer.
4. **P7**: ✅ `SkillsWorld.GetStatModBonuses()` + чтение в `PlayerAttacker`.

### Q2 — В следующем спринте (P4 done, P1 pending)
5. **P1**: ⏳ Замена `PlayerStats` struct на `Dictionary<StatType, float>` — **следующая сессия**.
6. **P4**: ✅ DONE (T-STATS03, 7b8460c) — разделён на `ExperienceConfig`, `StatSourceMapConfig`, `StatDebugConfig`.

### Q3 — Фаза 3
7. **P3/P9**: ⏳ Унифицировать Player/NPC stat провайдеры — после P1.
8. **P8**: ⏳ Multiplier support в `GetEquipStatBonuses`.

### Q4 — Не срочно
9. **P10**: ✅ DONE (T-STATS02) — `diceRoll`, `strengthContribution`, `baseContribution` в `DamageResult/Dto`.

---

## 5. Статус выполнения (2026-07-09)

| Q | Проблемы | Статус |
|---|---------|--------|
| Q0 | P0, P6 | ✅ DONE |
| Q1 | P5, P7 | ✅ DONE |
| Q2 | P1, P4 | P4 ✅, P1 ⏳ |
| Q3 | P3/P9, P8 | P3/P9 ✅, P8 ⏳ |
| Q4 | P10 | ✅ DONE |

**Рекомендация для следующей сессии:** начать с P1 (замена flat struct на dict) — самый трудоёмкий рефакторинг, затрагивает ~11 файлов.

---

*Документ обновлён 2026-07-09: добавлены отметки выполнения.*
