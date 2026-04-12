# Промпт для Сессии 5_2: Исправление ошибок Session 5

> **Контекст:** Этот документ — готовый промпт для запуска сессии 5_2 (исправления) для Project C: The Clouds. Скопируйте его содержимое и используйте как инструкцию для Qwen Code.

---

## 📋 ПРОМПТ НАЧАЛО

Ты начинаешь **Сессию 5_2: Исправление ошибок** для проекта Project C: The Clouds — MMO/Co-Op авиасимулятора над облаками по книге «Интеграл Пьявица».

**Ветка:** `qwen-gamestudio-agent-dev`
**Дата:** Апрель 2026
**Предыдущая сессия:** Сессия 5 — Meziy Thrust & Advanced Modules (частично работает, `docs/Ships/SESSION_5_COMPLETE.md`)

---

## ЧТО УЖЕ ГОТОВО

### Сессия 1-4 ✅
- `ShipController.cs` v2.1-v2.3 — smooth movement, altitude corridors, wind, modules
- `ShipModule.cs`, `ModuleSlot.cs`, `ShipModuleManager.cs` — система модулей

### Сессия 5 (частично) ✅/❌
- `ShipFuelSystem.cs` — ✅ работает: расход/регенерация топлива
- `MeziyModuleActivator.cs` — ✅ создан, ❌ не тестирован (модули не активируются)
- `MeziyThrusterVisual.cs` — ✅ создан, ❌ частицы всегда видны
- `CreateMeziyModuleAssets.cs` (Editor) — ✅ создаёт 4 модуля
- `ShipController.cs` v2.4c — ⚠️ thrust сломан, roll не работает, междуy не работает

### Текущие известные баги

| # | Баг | Приоритет | Файл бага |
|---|-----|-----------|-----------|
| 1 | **Корабль не летит вперёд** — fuel тратится, корабль НЕ двигается | 🔴 P0 | — |
| 2 | **Частицы всегда видны** — ParticleSystem рендерится постоянно | 🟡 P1 | `docs/bugs/SESSION5_MEZIY_VISUAL_NOT_VISIBLE.md` |
| 3 | **Lift и A/D работают при низком топливе** — threshold=5 слишком мало | 🟡 P1 | `docs/bugs/SESSION5_FUEL_EMPTY_CONTROLS_NOT_BLOCKED.md` |
| 4 | **Z/C не работают** — roll не вращает корабль | 🟡 P1 | `docs/bugs/SESSION5_ROLL_KEYS_ZC.md` |
| 5 | **Мезиевые модули не работают** — не активируются | 🟡 P1 | — |

### Что работает корректно
- ✅ L дозаправка (только когда корабль на месте, 2.0 fuel/s)
- ✅ Fuel система: расход от всех действий, regen на idle
- ✅ Fuel threshold (блокировка при fuel < 5)
- ✅ Input System — `IsKeyDown()` работает через `Keyboard.current`
- ✅ Editor утилита создания модулей

---

## ЗАДАЧА СЕССИИ 5_2: Исправление всех багов

### Приоритет 1: Корабль не летит вперёд (P0)

**Симптомы:**
- Нажимается W → топливо уходит
- Корабль НЕ двигается вперёд

**Что проверить:**
1. `ApplyThrustForce(_currentThrust)` вызывается? Добавить Debug.Log
2. `_currentThrust` имеет правильное значение? (не 0 из-за engineStalled)
3. Rigidbody mass correct? (Medium = 1000)
4. Thrust force применяется правильно?

**Возможные причины:**
- `fuelSystem.CurrentFuel < controlThreshold (5)` → `engineStalled = true` → `_currentThrust = 0`
- `_currentThrust` вычисляется но не применяется
- Что-то блокирует движение (drag слишком высокий?)

**Исправление:**
```csharp
// В FixedUpdate добавить Debug логи:
Debug.Log($"[ShipController] Thrust: {avgThrust:F2}, currentThrust: {_currentThrust:F2}, fuel: {fuelSystem?.CurrentFuel:F1}, stalled: {engineStalled}");
```

---

### Приоритет 2: Частицы всегда видны (P1)

**Симптомы:**
- ParticleSystem рендерится всегда, а не только при активации мезиевого модуля

**Что проверить:**
1. Старые дочерние объекты `MeziyThruster` в Hierarchy — удалить вручную
2. `AutoCreateParticles()` создаёт несколько объектов?
3. `renderer.enabled` переключается правильно?

**Исправление:**
1. Удалить все дочерние объекты "MeziyThruster" перед тестом
2. Добавить проверку в `AutoCreateParticles()`: если уже есть — не создавать
3. В `Awake()` или `OnEnable()` — явно выключить renderer если есть

---

### Приоритет 3: Lift и A/D работают при низком топливе (P1)

**Симптомы:**
- Порог `controlThreshold = 5` — при ~5 fuel управление снова работает
- Пользователь видит что корабль управляется до полной дозаправки

**Исправление:**
Увеличить порог до **10 fuel** (≈33 секунды regen для Medium класса):
```csharp
const float controlThreshold = 10f;
```

---

### Приоритет 4: Z/C не работают — roll не вращает (P1)

**Симптомы:**
- MODULE_ROLL установлен
- Z/C нажимаются
- Корабль НЕ кренится

**Что проверить:**
1. `IsKeyDown(KeyCode.Z)` / `IsKeyDown(KeyCode.C)` возвращает true? (Input System)
2. `GetCurrentRollInput()` возвращает правильное значение?
3. `_currentRollRate` имеет значение > 0?
4. `ApplyRotation(..., _currentRollRate)` вызывается?
5. `transform.forward * rollRate` — правильный вектор torque?

**Возможные причины:**
- `_rollUnlocked = false` — MODULE_ROLL не найден в слотах
- `GetCurrentRollInput()` возвращает 0 (Input System не работает)
- Roll force (15f) слишком мал для массы корабля (1000+)

**Исправление:**
1. Добавить Debug логи в `GetCurrentRollInput()` и `ApplyRotation()`
2. Увеличить `rollForce` до 150-300 (для массы 1000+)
3. Проверить что `IsKeyDown()` работает для Z/C

---

### Приоритет 5: Мезиевые модули не работают (P1)

**Симптомы:**
- MODULE_MEZIY_PITCH и другие не активируются
- `meziyActivator` может быть null или не настроен

**Что проверить:**
1. `meziyActivator` назначен в Inspector?
2. Модули установлены в ModuleSlot?
3. `ActivateMeziyModule()` вызывается?
4. `IsOnCooldown()` / `IsModuleInstalled()` возвращают правильные значения?

**Исправление:**
1. Добавить Debug логи в `ActivateMeziyModuleRpc()` и `ActivateModule()`
2. Добавить Debug HUD с состоянием модулей

---

### Дополнительно: Debug HUD (P2)

Добавить текстовый overlay в Game View для отладки:
```
[DEBUG]
Fuel: 45/100 (45%)
Stalled: No
Roll Unlocked: Yes
Refueling: No
Active Meziy: None
```

Включается по `F3` (development builds).

---

## АГЕНТЫ ДЛЯ ВЫЗОВА

### 1. @engine-programmer — отладка и исправление
**Задачи:**
- Добавить Debug логи в ключевые места
- Исправить thrust (корабль должен лететь)
- Исправить roll (Z/C должны работать)
- Исправить particles (renderer выключен по умолчанию)
- Увеличить fuel control threshold до 10

**Файлы:**
```
Assets/_Project/Scripts/Player/ShipController.cs      (ИЗМЕНИТЬ — debug, roll force, threshold)
Assets/_Project/Scripts/Ship/MeziyThrusterVisual.cs    (ИЗМЕНИТЬ — renderer fix)
```

### 2. @qa-tester — проверка всех багов
**Задачи:**
- Проверить что корабль летит при W
- Проверить что Z/C кренят корабль
- Проверить что частицы НЕ видны до активации
- Проверить что lift и A/D заблокированы при fuel < 10
- Проверить что L дозаправка работает на месте
- Проверить что мезиевые модули активируются

---

## КРИТЕРИИ ПРИЁМКИ СЕССИИ 5_2

| Критерий | Метрика | Pass/Fail |
|----------|---------|-----------|
| Корабль летит при W | thrustForce применяется | ☐ |
| Топливо уходит при любом вводе | fuel уменьшается | ☐ |
| Z/C кренят корабль (с MODULE_ROLL) | roll visible | ☐ |
| Частицы НЕ видны до активации | renderer off | ☐ |
| Lift и A/D заблокированы при fuel < 10 | no input response | ☐ |
| L дозаправка работает на месте | fuel растёт ~2/s | ☐ |
| Debug HUD показывает состояние | F3 overlay | ☐ |
| Компиляция без ошибок | Unity Console = 0 errors | ☐ |

---

## СВЯЗАННЫЕ ФАЙЛЫ (ЧИТАТЬ ПЕРЕД РАБОТОЙ)

| Файл | Зачем |
|------|-------|
| `docs/Ships/SESSION_5_COMPLETE.md` | Итоги сессии 5, что работает/сломано |
| `docs/bugs/SESSION5_*.md` | Документация по багам |
| `Assets/_Project/Scripts/Player/ShipController.cs` | v2.4c — основной файл |
| `Assets/_Project/Scripts/Ship/ShipFuelSystem.cs` | Топливо (работает) |
| `Assets/_Project/Scripts/Ship/MeziyModuleActivator.cs` | Активатор (не тестирован) |
| `Assets/_Project/Scripts/Ship/MeziyThrusterVisual.cs` | Визуал (частицы всегда видны) |

---

## ПОШАГОВЫЙ ПЛАН СЕССИИ

### Шаг 1: Диагностика (15 мин)
```
1. Прочитать SESSION_5_COMPLETE.md
2. Добавить Debug логи в ShipController FixedUpdate:
   - thrust, currentThrust, fuel, stalled, rollUnlocked, rollInput
3. Попросить пользователя запустить и показать логи
```

### Шаг 2: Исправление thrust (P0) (20 мин)
```
1. Найти причину почему корабль не летит
2. Исправить (скорее всего engineStalled или fuel check)
3. Проверить что thrust применяется
```

### Шаг 3: Исправление roll и particles (P1) (20 мин)
```
1. Исправить Z/C (Input System, roll force)
2. Исправить particles (renderer off)
3. Увеличить control threshold до 10
```

### Шаг 4: Мезиевые модули (P1) (20 мин)
```
1. Проверить что meziyActivator работает
2. Добавить Debug логи в активацию
3. Проверить что модули установлены
```

### Шаг 5: Debug HUD (P2) (15 мин)
```
1. Создать DebugOverlay.cs — текстовый HUD
2. F3 toggle
3. Показать fuel, stalled, roll, междуy state
```

### Шаг 6: Финальная проверка и коммит
```
1. Все критерии приёмки проверены
2. Debug логи убрать (кроме HUD)
3. Git commit
```

---

## ВАЖНЫЕ ПРЕДОСТЕРЕЖЕНИЯ

### ⚠️ НЕ ЛОМАТЬ
- **RPC сигнатуры:** `SubmitShipInputRpc`, `AddPilotRpc`, `RemovePilotRpc`, `ActivateMeziyModuleRpc`
- **Fuel система:** расход/регенерация уже работает — не менять
- **L дозаправка:** работает корректно — не менять логику
- **Input System:** `IsKeyDown()` работает — не менять

### ⚠️ ПРОВЕРЯТЬ
- **Компиляцию в Unity** после каждого изменения
- **Добавлять Debug логи** для каждой исправленной проблемы
- **Тестировать в Play Mode** перед коммитом

---

*Промпт подготовлен на основе: SESSION_5_COMPLETE.md, docs/bugs/SESSION5_*.md*
*Текущая версия ShipController: v2.4c | Следующая версия после 5_2: v2.5*
