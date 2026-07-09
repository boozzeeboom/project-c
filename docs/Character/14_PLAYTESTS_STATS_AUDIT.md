# Playtest Guide — Stats Audit Fixes (P0–P10)

**Дата:** 2026-07-09
**Коммиты:** `8c49ee1` → `7b8460c` → `f4ca1af` → `d609dbb` → `2b977ec`
**Цель:** Полный playtest всех 10 исправлений аудита `12_STATS_ARCHITECTURE_AUDIT_V2.md`

---

## 1. Сводка всех изменений

| # | Проблема | Что изменилось | Коммит |
|---|---------|---------------|--------|
| P0 | Combat equip bypass | `PlayerAttacker.GetStrength/Dex/Int` теперь включает equip + skill бонусы | `8c49ee1` |
| P4 | StatsConfig → 3 SO | `ExperienceConfig`, `StatSourceMapConfig`, `StatDebugConfig` | `7b8460c` |
| P5 | GatheringServer mapping | `StatType.Strength` → `StatsServer.GetStatFor(XpSource.Mining)` | `8c49ee1` |
| P6 | Effective stat formula | `effectiveStrength = StatsToFlat(tier) + bonus` (было: xp + bonus) | `8c49ee1` |
| P7 | Skill bonuses → combat | `SkillsWorld.GetStatModBonuses()` + чтение в `PlayerAttacker` | `8c49ee1` |
| P10 | DamageResultDto breakdown | `diceRoll`, `strengthContribution`, `baseContribution` в DTO | `8c49ee1` |
| P1 | Flat struct → StatBucket | `PlayerStats` со static ref accessors (Xp/Tier/TotalXp/GetBucket) | `f4ca1af` |
| P2 | PlayerStatsRef удалён | Static методы в `PlayerStats` | `f4ca1af` |
| P8 | Equipment multipliers | `effective = (StatsToFlat(tier) + flatBonus) * (1 + mult)` | `d609dbb` |
| P3/P9 | Unify Player/NPC formula | `NpcCombatData.strengthTier` + `PlayerStats.StatsToFlat(tier)` | `2b977ec` |

---

## 2. Что нужно перенастроить в Editor

### 2.1 ⚠️ [StatsServer] BootstrapScene — назначить конфиги (RECOMMENDED)

**Текущее состояние:** Все 3 поля = `None`. Есть `Resources.Load` fallback, но лучше явно назначить.

**Что сделать:**
1. Открыть `BootstrapScene.unity`
2. Найти `[StatsServer]` GameObject
3. В инспекторе StatsServer:
   - `Exp Config` → `Assets/_Project/Resources/Stats/ExperienceConfig_Default.asset`
   - `Source Map Config` → `Assets/_Project/Resources/Stats/StatSourceMapConfig_Default.asset`
   - `Debug Config` → `Assets/_Project/Resources/Stats/StatDebugConfig_Default.asset`

**Почему:** Fallback работает через `Resources.Load`, но явное назначение:
- убирает warning-логи при старте
- позволяет подменить конфиги для тестов (PvP-зона, туториал)

### 2.2 PlayerAttacker — проверка наличия на NetworkPlayer

**Текущее состояние:** `PlayerAttacker` добавляется динамически в `NetworkPlayer.RegisterWithCombatServer()`. На префабе `NetworkPlayer.prefab` компонента нет.

**Что проверить:** Что `RegisterWithCombatServer` вызывается корректно при спавне игрока.

### 2.3 NPC — NpcCombatData с новыми tier-полями

**Текущее состояние:** 
- Единственный ассет `Npc_Goblin.asset` сконвертирован: `strengthTier=0`, `dexterityTier=0`, `intelligenceTier=0`
- При tier=0: `StatsToFlat(0) = 10` → STR=10, DEX=10, INT=10 (как старые значения 10/10/8)

**Что проверить:** Что Goblin наносит тот же урон, что и раньше (STR 10 = +10 к baseAttack).

### 2.4 ⚠️ Старый StatsConfig — удалить (CLEANUP)

**Текущее состояние:** `Assets/_Project/Resources/Stats/StatsConfig_Default.asset` всё ещё существует, но нигде не используется.

**Что сделать:**
1. Проверить что ни один GO в сцене не ссылается на `StatsConfig`
2. Удалить `Assets/_Project/Resources/Stats/StatsConfig_Default.asset`
3. Удалить `Assets/_Project/Scripts/Stats/StatsConfig.cs` (класс больше не нужен)

### 2.5 ClothingItemData/ModuleItemData — проверить multiplier-поля

**Текущее состояние:** P8 сделал множители активными. Если у предметов стоят multiplier > 0 — они теперь РЕАЛЬНО влияют на effective stat и combat damage.

**Что проверить:** Открыть каждый Clothing/Module ассет и убедиться что multiplier-значения адекватны. Например:
- `Clothing_SteelChestplate` — multiplier не должен делать игрока неуязвимым
- Рекомендуется для MVP: все multipliers = 0, только flat бонусы

---

## 3. Полный план Playtest

### Test 1: Player stat formula (P0, P6, P7, P8)

**Цель:** Проверить что боевые статы игрока считаются правильно.

**Шаги:**
1. Запустить BootstrapScene + WorldScene_0_0 (host)
2. Открыть CharacterWindow → вкладка Характеристики
3. Запомнить `effectiveStrength` (должно быть `StatsToFlat(tier) + equipBonus + skillBonus`, всё × multiplier)

**Проверка формулы (консольный лог):**
```
[StatsServer] snapshot built: effectiveStrength = (StatsToFlat(tier) + bonus) * (1+mult)
```

**Ожидаемый результат:**
- Без экипировки: effective = `tier*5+10`
- С бронёй +5 STR: effective = `(tier*5+10 + 5) * (1+mult)`
- С multiplier 0.5: effective = `(tier*5+10 + 5) * 1.5`

### Test 2: NPC combat — tier-based stats (P3/P9)

**Цель:** Проверить что NPC используют ту же формулу `StatsToFlat(tier)`.

**Шаги:**
1. Найти Goblin'а в мире (или заспавнить через NpcSpawner)
2. Включить `[CombatServer]` debug log
3. Атаковать гоблина
4. Проверить консольный лог DamageCalculator

**Ожидаемый результат:**
```
[DamageCalculator] baseAttack = diceRoll + baseDamage + strengthContribution
strengthContribution = 10 (для Goblin tier=0)
```

**Вариант с повышенным tier:**
- Создать копию `Npc_Goblin.asset` с `strengthTier=2` (STR=20)
- Заспавнить NPC с этим конфигом
- Урон NPC должен быть на 10 выше (20 vs 10)

### Test 3: Equipment stat bonuses in combat (P0, P8)

**Цель:** Проверить что экипировка РЕАЛЬНО меняет урон.

**Шаги:**
1. Без экипировки: записать `baseAttack` из консоли `DamageCalculator`
2. Надеть `Clothing_SteelChestplate` (strengthBonus, strengthMultiplier)
3. Атаковать снова: сравнить `baseAttack`

**Ожидаемый результат:**
- `strengthContribution` вырос на `strengthBonus * (1 + multiplier)`
- Документировать конкретные цифры в логе

### Test 4: Skill stat bonuses (P7)

**Цель:** Проверить что изученные навыки с `StatMod` эффектами влияют на статы.

**Шаги:**
1. Открыть CharacterWindow → Навыки
2. Найти навык с `StatMod` эффектом (например `+2 STR`)
3. Изучить навык (Learn)
4. Проверить что `effectiveStrength` вырос на 2
5. Проверить что combat damage вырос

**Ожидаемый результат:**
- Без навыка: STR = `StatsToFlat(tier)`
- С навыком +2 STR: STR = `StatsToFlat(tier) + 2`

### Test 5: GatheringServer mapping (P5)

**Цель:** Проверить что mining XP идёт в правильный стат через `StatSourceMapConfig`.

**Шаги:**
1. Открыть `StatSourceMapConfig_Default.asset`
2. Проверить mapping: `Mining → Strength`
3. Зайти в шахту, намайнить руду
4. Проверить что XP пошёл в Strength (а не хардкодом)

**Ожидаемый результат:**
- Mining XP → Strength XP (согласно mapping)
- Если изменить mapping на Dexterity → XP идёт в Dexterity

### Test 6: DamageResultDto breakdown (P10)

**Цель:** Проверить что DamageResult содержит детальную разбивку урона.

**Шаги:**
1. Включить debug log в `[CombatServer]`
2. Атаковать цель
3. Проверить консольный лог

**Ожидаемый результат:**
```
[CombatServer] DamageResult: finalDamage=X, diceRoll=Y, strengthContrib=Z, baseContrib=W, locMult=M
X = (Y + W + Z) * M  (до брони)
```

### Test 7: Сохранение/загрузка (P1)

**Цель:** Проверить что P1-рефакторинг не сломал персистентность.

**Шаги:**
1. Заработать XP во всех трёх статах
2. Запомнить значения (tier + xp в каждом)
3. Выйти из игры (Disconnect)
4. Перезапустить, зайти снова
5. Сравнить значения

**Ожидаемый результат:** Все значения восстановлены. Формат save: `StatBucket[3]` с полями `xp, tier, totalXp`.

---

## 4. Ключевые консольные логи для отладки

| Подсистема | Ключевые строки |
|-----------|----------------|
| StatsServer init | `[StatsServer] OnNetworkSpawn — subscribed to 9 WorldEventBus events` |
| Stats load | `[StatsServer] OnClientConnectedForStats: client=X — loaded STR=...` |
| Snapshot | `[StatsServer] snapshot built for client X: STR=...` |
| Combat damage | `[CombatServer] DamageResult: finalDamage=...` |
| DamageCalculator | `baseAttack = roll + base + STR` |
| PlayerAttacker | `[PlayerAttacker] Added Unarmed fallback source (id=0)` |
| NpcAttacker | (no init log by default) |
| Equipment bonuses | `[EquipmentWorld] GetEquipStatBonuses: client=...` |

---

## 5. Что НЕ тестировать (риски)

| Риск | Почему не страшно |
|------|-------------------|
| Несоответствие effective в UI и combat | P6 исправлен — теперь одна формула |
| NPC tier=0 даёт INT=10 вместо старых 8 | Балансный шифт минимален (Goblin INT 8→10) |
| StatsConfig удалён — не сломает ли старые ссылки | Класс существует, но SO не используется; удалить после верификации |
| PlayerAttacker не на префабе | Добавляется динамически через RegisterWithCombatServer |

---

## 6. Checklist для Tester'а

- [ ] **Pre-flight:** Назначить 3 конфига в [StatsServer] (п. 2.1)
- [ ] **Test 1:** Player stat formula — effectiveStrength в UI совпадает с combat damage
- [ ] **Test 2:** NPC tier-based — Goblin STR=10 (tier 0), урон предсказуем
- [ ] **Test 3:** Equip bonuses — надеть броню → урон вырос
- [ ] **Test 4:** Skill bonuses — изучить +STR навык → effectiveStrength вырос
- [ ] **Test 5:** Mining mapping — XP в STR согласно StatSourceMapConfig
- [ ] **Test 6:** Damage breakdown — консоль показывает diceRoll/strengthContrib/baseContrib
- [ ] **Test 7:** Save/Load — статы сохраняются и восстанавливаются
- [ ] **Cleanup:** Удалить StatsConfig_Default.asset + StatsConfig.cs (п. 2.4)
- [ ] **Final:** Закрыть все 10 проблем в `13_SESSION_CONTINUATION.md`
