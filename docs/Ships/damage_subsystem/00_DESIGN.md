# Ship Damage Subsystem — Дизайн (MVP)

**Дата:** 2026-07-20
**Статус:** ✅ Реализовано (техническая часть, без визуала)
**Scope:** MVP — единый HP корпуса, без визуальных изменений

---

## 1. Обзор

Корабль получает **единый HP корпуса** (hull). HP сервер-авторитативен через `NetworkVariable<int>`. Урон поступает из двух источников:

| Источник | Механика | Канал |
|----------|----------|-------|
| **Столкновения** | `ShipController.OnCollisionEnter` → `ShipHull.ApplyCollisionDamage(energy)` | Импульс → урон по формуле |
| **Боевое оружие** | `CombatServer.ResolveAttack` → `ShipHull.ApplyDamage(DamageResult)` | Существующий боевой движок (ERPR) |

При **0 HP** корабль **«сломан»**:
- Двигатель продолжает работать (НЕ выключается)
- Все скорости **×0.1** (−90%): thrust, yaw, pitch, vertical, maxSpeed
- Груз **обнуляется** (`CargoData.Clear()` + `NotifyCargoChanged`)
- Корабль **не уничтожается**, не деспаунится

Ремонт — **только в доке** через `ShipModuleServer.RequestRepairHullRpc` (владение ключом + `IsDocked` + кредиты).

---

## 2. Формула урона

### 2.1 Столкновения

```
energy = col.impulse.magnitude
if energy < collisionEnergyThreshold: урон не наносится

hullDamage = floor((energy - threshold) * collisionDamageCoefficient)
hullDamage = min(hullDamage, collisionDamageCap)
```

**Параметры по умолчанию** (`ShipDamageConfig`):

| Параметр | Значение | Описание |
|----------|----------|----------|
| `collisionEnergyThreshold` | 8 | Минимальный импульс для урона |
| `collisionDamageCoefficient` | 0.5 | energy → HP (1 импульс = 0.5 HP выше порога) |
| `collisionDamageCap` | 50 | Макс. урон за один удар |

### 2.2 Боевое оружие

Урон рассчитывается существующим `DamageCalculator.Calculate()`:
```
finalDamage = max(0, preDefenseDamage - effectiveDefense)
effectiveDefense = round(armorHull * typeMultiplier)
```

**Типы урона** (из `DamageType`):

| Тип | Множитель брони | Описание |
|-----|-----------------|----------|
| Physical | ×1 | Стандартный |
| Ballistic | ×1 | Арбалеты, пневматика |
| Antigrav | ×0.5 | Антиграв. клинки |
| Explosive | ×1 | (зарезервировано) |
| Mesium | ×0 | Игнорирует броню |

`armorHull = 5` (по умолчанию) — вычитается из `preDefenseDamage` после множителя типа.

### 2.3 Защиты от ложных ударов при стыковке/отстыковке

При отстыковке физика выталкивает корабль из геометрии дока — `impulse` огромный (1994+), но `relativeVelocity ≈ 0` (реального сближения нет). Три уровня защиты:

| Защита | Параметр | Default | Где |
|--------|----------|---------|-----|
| Мин. скорость сближения | `minCollisionRelativeSpeed` | 3 м/с | `ShipController.OnCollisionEnter` — фильтр ДО вызова `ShipHull` |
| Грейс-период отстыковки | `postUndockGraceSeconds` | 3 сек | `ShipHull.ApplyCollisionDamage` — `Time.time - LastUndockTime < grace` |
| В доке | — | — | `ShipHull.ApplyCollisionDamage` — `IsDocked → return` |

---

## 3. HP по классу корабля

| ShipFlightClass | Max Hull | Лор |
|-----------------|----------|-----|
| Light | 100 | Маленький, хрупкий |
| Medium | 200 | Баланс |
| Heavy | 400 | Толстая обшивка |
| HeavyII | 600 | Летающая крепость |

---

## 4. Состояния

```
                    ┌────────────────────────┐
                    │   OPERATIONAL (HP > 0) │
                    │   Полные скорости       │
                    │   Груз в трюме          │
                    └──────┬─────────────────┘
                           │ HP → 0 (столкновение/оружие)
                           ▼
                    ┌────────────────────────┐
                    │   BROKEN (HP = 0)      │
                    │   Скорости ×0.1        │
                    │   Груз обнулён         │
                    │   Двигатель работает   │
                    │   Корабль не уничтожен │
                    └──────┬─────────────────┘
                           │ Ремонт в доке (RPC)
                           ▼
                    ┌────────────────────────┐
                    │   OPERATIONAL (HP = max)│
                    │   Скорости восстановлены│
                    └────────────────────────┘
```

### 4.1 Важно

- `IsAlive()` всегда `true` — `CombatServer` **не публикует** `EntityKilledEvent` для корабля
- Двигатель **не выключается** при поломке — корабль может ковылять на 10% скоростей
- Груз обнуляется **один раз** при переходе в Broken, не каждый кадр
- Ремонт восстанавливает HP **до max** и снимает флаг `_hullBroken`

---

## 5. Что НЕ входит в MVP

| Функция | Статус |
|---------|--------|
| Визуальные эффекты (дым, искры, деформация) | Post-MVP |
| Щиты как отдельный ресурс | Post-MVP |
| Урон по отдельным модулям/слотам | Post-MVP |
| Градации деградации (50% HP = −20% скоростей) | Post-MVP — сейчас только 0/1 (Broken/Operational) |
| HUD-полоса HP | Post-MVP (событие `OnHullChanged` уже доступно) |
| Урон от перегрева двигателя | Post-MVP |
| Саморемонт в полёте | Post-MVP |

---

## 6. Связь с существующими подсистемами

| Подсистема | Интеграция |
|------------|------------|
| **Combat Engine** (`CombatServer`) | `ShipHull` реализует `IDamageTarget`, self-register в `OnNetworkSpawn` |
| **Cargo System** (`TradeWorld`) | `CargoData.Clear()` + `NotifyCargoChanged` при поломке |
| **Engine State** (`ShipController`) | `_hullBroken` флаг умножает скорости в `FixedUpdate` |
| **Module System** (`ShipModuleServer`) | `RequestRepairHullRpc` — тот же паттерн валидации (ключ + док + кредиты) |
| **Altitude Corridor** | Не затрагивается (деградация от высоты — отдельная система) |
| **SystemDegradationEffect** | Не затрагивается (деградация от высоты, не от HP) |

---

*Документация ведётся агентом Aura. 2026-07-20*
