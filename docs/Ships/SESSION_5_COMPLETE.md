# Сессия 5: Meziy Thrust & Advanced Modules — Partial Complete

**Дата:** 12 апреля 2026 | **Статус:** ⚠️ Частично работает | **Ветка:** `qwen-gamestudio-agent-dev`
**ShipController версия:** v2.4 (было v2.3)

---

## Результаты Тестирования

### ✅ Работает
- Скрипты созданы и компилируются
- Модули создаются через Editor утилиту
- Крен работает (MODULE_ROLL разблокирует)
- Топливо уменьшается при тяге
- При fuel=0 двигатель глохнет (thrust = 0)

### ⚠️ Частично работает
- Крен работает, но на A/D (конфликт с yaw) → нужно Z/C
- Визуал сопел добавлен в Inspector, но не виден в Play Mode
- Топливо восстанавливается на idle, но НЕ восстанавливается при fuel=0

### ❌ Не работает / Баги
- При fuel=0 yaw/pitch/lift НЕ блокируются
- Топливо перестаёт восстанавливаться при fuel=0 (IsEmpty блокирует regen)
- Визуал ParticleSystem + Light не отображается

---

## Известные Баги (задокументированы)

| Баг | Файл | Приоритет |
|-----|------|-----------|
| Fuel=0 не блокирует yaw/pitch/lift | `docs/bugs/SESSION5_FUEL_EMPTY_CONTROLS_NOT_BLOCKED.md` | P1 |
| Regen не работает при fuel=0 | `docs/bugs/SESSION5_FUEL_EMPTY_CONTROLS_NOT_BLOCKED.md` | P1 |
| Визуал сопел не виден | `docs/bugs/SESSION5_MEZIY_VISUAL_NOT_VISIBLE.md` | P2 |
| Крен на A/D неудобен | `docs/bugs/SESSION5_ROLL_KEYS_ZC.md` | P2 |

## Запрошенные Фичи

| Фича | Файл | Приоритет |
|------|------|-----------|
| Кнопка L — дозаправка паров | `docs/bugs/SESSION5_REFUEL_KEY_L_FEATURE.md` | P2 |

---

## Что Реализовано (Технически)

### 1. ShipFuelSystem.cs (НОВЫЙ)
**Путь:** `Assets/_Project/Scripts/Ship/ShipFuelSystem.cs`

Система топлива корабля:
- `currentFuel` / `maxFuel` — текущий и максимальный уровень топлива
- `FuelPercent` — процент топлива (0..1)
- `IsEmpty` — топливо закончилось
- `ConsumeFuel(amount)` — потребить топливо (вернуть false если недостаточно)
- `ConsumeFuelPerSecond(dt, thrustFactor)` — расход за кадр с учётом тяги
- `RegenFuel(dt)` — восстановление на idle (тяга = 0)
- `Refuel(amount)` / `RefuelFull()` — заправка (для доков)
- `Initialize(ShipFlightClass)` — авто-настройка ёмкости и расхода по классу

**fuelCapacity по классам (из ShipRegistry):**
| Класс | fuelCapacity | fuelConsumptionRate |
|-------|-------------|---------------------|
| Light (Тороид) | 50 | 0.5 fuel/s |
| Medium (Баржа) | 100 | 0.8 fuel/s |
| Heavy (Платформа) | 200 | 1.2 fuel/s |
| HeavyII (Открытый) | 300 | 1.5 fuel/s |

**Восстановление (idle):** 0.3 fuel/s для всех классов

### 2. MeziyModuleActivator.cs (НОВЫЙ)
**Путь:** `Assets/_Project/Scripts/Ship/MeziyModuleActivator.cs`

Активатор мезиевых модулей:
- `ActivateModule(ShipModule)` — активировать мезиевый модуль
- `IsOnCooldown(moduleId)` — проверка кулдауна
- `GetCooldownRemaining(moduleId)` — оставшееся время кулдауна
- `UpdateCooldowns(dt)` — обновление кулдаунов и активных эффектов (каждый FixedUpdate)
- `GetActiveEffects()` — получить все активные эффекты
- `FindInstalledMeziyModule(moduleId)` — найти установленный модуль по ID

**Логика активации:**
1. Проверить что модуль установлен в слоте
2. Проверить что не на cooldown
3. Проверить что достаточно топлива (`meziyFuelCost`)
4. Списать топливо
5. Запустить эффект (`meziyForce` × `meziyDuration`)
6. Запустить cooldown (`meziyCooldown`)

**MeziyState** — класс состояния эффекта:
- `moduleId`, `timeRemaining`, `cooldownRemaining`
- `isActive` → `timeRemaining > 0`
- `isOnCooldown` → `cooldownRemaining > 0`

### 3. MeziyThrusterVisual.cs (НОВЫЙ)
**Путь:** `Assets/_Project/Scripts/Ship/MeziyThrusterVisual.cs`

Визуальный эффект сопел (опциональный):
- `thrustParticle` — ParticleSystem сопла
- `glowLight` — Light свечения
- `Activate()` / `Deactivate()` — вкл/выкл визуала
- `SetIntensity(0..1)` — интенсивность частиц

**Примечание:** Если не назначен — мезиевая тяга работает без визуала.

### 4. Интеграция в ShipController.cs v2.4
**Путь:** `Assets/_Project/Scripts/Player/ShipController.cs`

**Новые поля:**
```csharp
[Header("Мезиевая Тяга (Сессия 5)")]
[SerializeField] private MeziyModuleActivator meziyActivator;
[SerializeField] private ShipFuelSystem fuelSystem;
[SerializeField] private MeziyThrusterVisual meziyVisual;

// Состояние
private bool _rollUnlocked = false;
private Vector3 _activeMeziyTorque;
private bool _meziyActive = false;
```

**Изменения в FixedUpdate():**
1. `meziyActivator.UpdateCooldowns(dt)` — обновление кулдаунов
2. Проверка `fuelSystem.IsEmpty` → `engineStalled = true` → `targetThrust = 0`
3. `fuelSystem.RegenFuel(dt)` при idle / `fuelSystem.ConsumeFuelPerSecond(dt, avgThrust)` при тяге
4. `ApplyMeziyEffects(dt)` — применение мезиевых эффектов после стабилизации

**Новые методы:**
- `ApplyMeziyEffects(dt)` — применяет torque от активных мезиевых эффектов
- `GetCurrentRollInput()` / `GetCurrentPitchInput()` / `GetCurrentYawInput()` — чтение ввода для мезиевых модулей
- `InitializeFuelSystem()` — инициализация в Awake()
- `ActivateMeziyModuleRpc(string moduleId)` — RPC для активации (сервер)
- `ActivateMeziyModule(string moduleId)` — публичный метод для вызова из InputManager
- `CheckRollUnlock()` — проверка MODULE_ROLL в ApplyModuleModifiers()

**MODULE_ROLL — разблокировка крена:**
- Если MODULE_ROLL установлен → `_rollUnlocked = true`
- В `ApplyStabilization()`: если `_rollUnlocked` → `rollStabForce` уменьшается до 30% (мягкая стабилизация)

### 5. Editor Утилита
**Путь:** `Assets/_Project/Scripts/Editor/CreateMeziyModuleAssets.cs`

Menu: **Tools → Project C → Create Meziy Module Assets**

Создаёт 4 тестовых модуля в `Assets/_Project/Data/Modules/`:
1. **MODULE_MEZIY_ROLL**: meziyForce=25, meziyDuration=2s, meziyCooldown=10s, fuelCost=5, power=15, tier 2
2. **MODULE_MEZIY_PITCH**: meziyForce=10, meziyDuration=1.5s, meziyCooldown=8s, fuelCost=5, power=15, tier 2
3. **MODULE_MEZIY_YAW**: meziyForce=30, meziyDuration=0.5s, meziyCooldown=12s, fuelCost=5, power=15, tier 2
4. **MODULE_ROLL**: Utility, power=10, tier 2 (разблокировка крена)

---

## Как Настроить в Unity Editor

### Шаг 1: Создать мезиевые модули
1. В Unity Editor: **Tools → Project C → Create Meziy Module Assets**
2. Модули создадутся в `Assets/_Project/Data/Modules/`

### Шаг 2: Настроить корабль
1. Выбери корабль в сцене
2. В Inspector → ShipController:
   - **Fuel System**: добавь компонент `ShipFuelSystem` (если ещё нет) и перетащи сюда
   - **Meziy Activator**: добавь компонент `MeziyModuleActivator` и перетащи сюда
   - **Meziy Visual**: (опционально) добавь `MeziyThrusterVisual` с ParticleSystem + Light
3. На `MeziyModuleActivator`:
   - **Fuel System**: перетащи ShipFuelSystem
   - **Module Manager**: перетащи ShipModuleManager
4. На корабле (или дочерних объектах):
   - Добавь `ModuleSlot` для мезиевых модулей (тип: Propulsion)
   - Перетащи созданные мезиевые модули (из Data/Modules/) в `Installed Module` поля ModuleSlot

### Шаг 3: Тестирование
1. Запусти Play Mode
2. Убедись что топливо уменьшается при тяге (Inspector → ShipFuelSystem → Current Fuel)
3. Отпусти тягу → топливо должно восстанавливаться (Regen)
4. Установи MODULE_ROLL → крен должен работать (A/D)
5. Для мезиевых модулей: вызови `shipController.ActivateMeziyModule("MODULE_MEZIY_PITCH")` из кода или InputManager

---

## Параметры Топлива по Классам

| Класс | fuelCapacity | Consumption (fuel/s) | Regen (fuel/s) | Время работы (полная тяга) |
|-------|-------------|---------------------|----------------|--------------------------|
| Light | 50 | 0.5 | 0.3 | ~100с |
| Medium | 100 | 0.8 | 0.3 | ~125с |
| Heavy | 200 | 1.2 | 0.3 | ~167с |
| HeavyII | 300 | 1.5 | 0.3 | ~200с |

**Мезиевые модули:** каждый активация = -5 fuel (независимо от базового расхода)

---

## Управление Мезиевыми Модулями

### Активация из InputManager (пример)
```csharp
// Left Shift + W = MODULE_MEZIY_PITCH (нос вверх)
// Left Shift + S = MODULE_MEZIY_PITCH (нос вниз)
// Left Shift + A = MODULE_MEZIY_ROLL (крен влево)
// Left Shift + D = MODULE_MEZIY_ROLL (крен вправо)
// Left Shift + Space = MODULE_MEZIY_YAW (резкий поворот)

if (Input.GetKey(KeyCode.LeftShift) && Input.GetKeyDown(KeyCode.W))
{
    shipController.ActivateMeziyModule("MODULE_MEZIY_PITCH");
}
```

### Поведение мезиевых модулей
| Модуль | Эффект | Длительность | Кулдаун | Стоимость |
|--------|--------|-------------|---------|-----------|
| MODULE_MEZIY_ROLL | Бросок крена ±25° | 2с | 10с | 5 fuel |
| MODULE_MEZIY_PITCH | Бросок тангажа ±10° | 1.5с | 8с | 5 fuel |
| MODULE_MEZIY_YAW | Резкий поворот 30° | 0.5с | 12с | 5 fuel |
| MODULE_ROLL | Разблокировка крена | Постоянно | Нет | 10 power |

---

## Критерии Приёмки

| Критерий | Статус |
|----------|--------|
| ShipFuelSystem работает | ☐ Требуется тест |
| Топливо уменьшается при тяге | ☐ Требуется тест |
| Топливо восстанавливается на idle | ☐ Требуется тест |
| При empty fuel двигатель глохнет | ☐ Требуется тест |
| MODULE_MEZIY_PITCH активирует бросок | ☐ Требуется тест |
| После активации — cooldown | ☐ Требуется тест |
| Топливо уменьшается на 5 за активацию | ☐ Требуется тест |
| MODULE_ROLL разблокирует крен | ☐ Требуется тест |
| Визуал сопла при активации | ☐ Требуется тест |
| Сетевая совместимость сохранена | ☐ Требуется тест |
| Editor утилита создаёт мезиевые модули | ☐ Требуется тест |
| Компиляция без ошибок | ☐ Требуется проверка |

---

## Известные Ограничения

1. **Нет GUI для активации мезиевых модулей** — активация через код или InputManager
2. **Нет системы дозаправки** — `RefuelFull()` готова для доков, но доков ещё нет
3. **Мезиевые модули не реплицируются визуально** — междуyVisual работает только локально (будет улучшено в будущих сессиях)
4. **Input для мезиевых модулей** — нужно добавить в InputManager (Shift+W/A/S/D/Space)

---

## Связанные Файлы

| Файл | Описание |
|------|----------|
| `Assets/_Project/Scripts/Ship/ShipFuelSystem.cs` | Система топлива (НОВЫЙ) |
| `Assets/_Project/Scripts/Ship/MeziyModuleActivator.cs` | Активатор мезиевых модулей (НОВЫЙ) |
| `Assets/_Project/Scripts/Ship/MeziyThrusterVisual.cs` | Визуал сопел (НОВЫЙ) |
| `Assets/_Project/Scripts/Player/ShipController.cs` | v2.4 — топливо + мезиевые модули |
| `Assets/_Project/Scripts/Ship/ShipModule.cs` | ScriptableObject (meziy поля уже были) |
| `Assets/_Project/Scripts/Editor/CreateMeziyModuleAssets.cs` | Editor утилита (НОВАЯ) |
| `docs/Ships/ShipRegistry.md` | Каталог модулей, совместимость |
| `docs/Ships/SESSION_4_COMPLETE.md` | Предыдущая сессия |

---

## Извлечённые Уроки

- **(Будет дополнено после тестирования)**

---

## Рекомендации по Тестированию в Unity

### Тест 1: Система топлива
1. Открой сцену с кораблём
2. Запусти Play Mode
3. В Inspector → ShipFuelSystem → смотри `Current Fuel`
4. Нажми W (тяга) → топливо должно уменьшаться
5. Отпусти W → топливо должно восстанавливаться
6. Дождись `Current Fuel = 0` → тяга должна отключиться

### Тест 2: MODULE_ROLL (разблокировка крена)
1. Установи MODULE_ROLL в ModuleSlot (Utility)
2. Запусти Play Mode
3. Нажми A/D → корабль должен начать крениться (раньше было заблокировано)
4. Отпусти A/D → стабилизация должна вернуть к 0° (мягче чем раньше)
5. Сними MODULE_ROLL → крен снова заблокирован

### Тест 3: Мезиевые модули
1. Установи MODULE_MEZIY_PITCH в ModuleSlot (Propulsion)
2. Вызови `shipController.ActivateMeziyModule("MODULE_MEZIY_PITCH")` из консоли или кода
3. Нос корабля должен наклониться (бросок тангажа)
4. После 1.5с эффект должен закончиться
5. Попробуй активировать снова → должно заблокировано (cooldown 8с)
6. Проверь что топливо уменьшилось на 5

### Тест 4: Сетевая совместимость
1. Запусти хост + клиент
2. Убедись что RPC `ActivateMeziyModuleRpc` работает
3. Убедись что кооп-пилотирование не сломано

---

*Документ создан: 12 апреля 2026 | Сессия 5: Meziy Thrust & Advanced Modules*
*Следующая сессия: Сессия 6+ (Co-Op, KeyRod, Docking — будущие)*
