# Промпт для Сессии 5: Meziy Thrust & Advanced Modules

> **Контекст:** Этот документ — готовый промпт для запуска 5-й сессии разработки системы управления кораблями. Скопируйте его содержимое и используйте как инструкцию для Qwen Code.

---

## 📋 ПРОМПТ НАЧАЛО

Ты начинаешь **Сессию 5: Meziy Thrust & Advanced Modules** для проекта Project C: The Clouds — MMO/Co-Op авиасимулятора над облаками по книге «Интеграл Пьявица».

**Ветка:** `qwen-gamestudio-agent-dev`
**Дата:** Апрель 2026

---

## ЧТО УЖЕ ГОТОВО (Сессии 1-4)

### Сессия 1: Core Smooth Movement ✅
- `ShipController.cs` v2.1 — `Mathf.SmoothDamp` для frame-rate независимого сглаживания
- 4 класса кораблей (Light/Medium/Heavy/HeavyII) с разными параметрами
- Стабилизация: pitchStabForce=15, rollStabForce=20, angularDrag=8.0
- Thrust ramp-up 0.3s, yaw decay 1.0s, lift clamp 2.5 м/с

### Сессия 2: Altitude Corridor System ✅
- `AltitudeCorridorData.cs` + `AltitudeCorridorSystem.cs` — 6 коридоров
- `TurbulenceEffect.cs` + `SystemDegradationEffect.cs`
- `AltitudeUI.cs` — HUD предупреждений

### Сессия 3: Wind & Environmental Forces ✅
- `WindZoneData.cs` + `WindZone.cs` — объёмные триггерные зоны
- Интеграция ветра в ShipController v2.2: `ApplyWind()`, `RegisterWindZone()`
- `windExposure` по классам: Light=1.2, Medium=1.0, Heavy=0.7, HeavyII=0.5

### Сессия 4: Module System Foundation ✅
- `ShipModule.cs` (ScriptableObject) — данные модулей с полями:
  - `isMeziyModule`, `meziyForce`, `meziyDuration`, `meziyCooldown`, `meziyFuelCost`
- `ModuleSlot.cs` (MonoBehaviour) — слоты с `OnValidate()` валидацией типа
- `ShipModuleManager.cs` — менеджер слотов, энергии, модификаторов
- ShipController v2.3 — `ApplyModuleModifiers()` интегрирован в FixedUpdate
- Editor утилита: 3 тестовых модуля (YAW_ENH, PITCH_ENH, LIFT_ENH)
- **Исправленный баг:** ModuleSlot OnValidate блокирует несовместимые типы через Inspector

### Текущий ShipController.cs v2.3 — что есть сейчас
- `FixedUpdate()`: AverageInputs → ApplyModuleModifiers → SmoothThrust/Yaw/Pitch/Lift → ValidateAltitude → ApplyForces → Stabilization → ApplyWind → Clamp
- Roll **заблокирован** (стабилизация возвращает к 0°, нет ввода roll)
- **Нет системы топлива** — корабли бесконечные
- **Нет активации мезиевых модулей** — данные в ShipModule есть, логики нет
- Сетевая совместимость: `SubmitShipInputRpc`, `AddPilotRpc`, `RemovePilotRpc` — НЕ ЛОМАТЬ
- **Документ:** `docs/Ships/SESSION_4_COMPLETE.md`

### ShipRegistry.md — каталог мезиевых модулей

| Module ID | Название | Эффект | Power | Тир |
|-----------|----------|--------|-------|-----|
| `MODULE_MEZIY_ROLL` | Мезиевая Тяга (Крен) | Бросок ±25° (2с, CD 10с). -5 fuel | 15 | 2 |
| `MODULE_MEZIY_PITCH` | Мезиевая Тяга (Тангаж) | Бросок ±10° (1.5с, CD 8с). -5 fuel | 15 | 2 |
| `MODULE_MEZIY_YAW` | Мезиевая Тяга (Рыскание) | Резкий поворот 30° (0.5с, CD 12с). -5 fuel | 15 | 2 |

Совместимость:
- `SHIP_LG_01` Стриж: ❌ Рама слишком лёгкая
- `SHIP_LG_02` Тороид: ✅ Все 3 мезиевых модуля
- `SHIP_MD_*`: ✅ Все 3 мезиевых модуля
- `SHIP_HV_*`, `SHIP_H2_*`: ✅ Все 3 мезиевых модуля

---

## ЗАДАЧА СЕССИИ 5: Meziy Thrust & Advanced Modules

### Что нужно реализовать

#### 1. ShipFuelSystem.cs — система топлива корабля

**ShipFuelSystem (MonoBehaviour):**
```csharp
public class ShipFuelSystem : MonoBehaviour {
    [Header("Топливо")]
    public float currentFuel;       // Текущий уровень
    public float maxFuel;           // Максимум (зависит от fuelCapacity в ShipRegistry)
    public float fuelConsumptionRate; // Базовый расход (за секунду полёта)
    public float fuelRegenRate;     // Восстановление (когда двигатель на idle)

    public float FuelPercent => currentFuel / maxFuel;
    public bool IsEmpty => currentFuel <= 0f;

    public bool ConsumeFuel(float amount);  // Вернуть false если недостаточно
    public void RegenFuel(float dt);        // Восстановить за кадр
    public void Refuel(float amount);       // Заправка (для доков)
}
```

**fuelCapacity по классам (из ShipRegistry):**
| Класс | fuelCapacity | maxFuel |
|-------|-------------|---------|
| LIGHT (Тороид) | 50 | 50 |
| MEDIUM (Баржа) | 100 | 100 |
| HEAVY (Платформа) | 200 | 200 |
| HEAVYII (Открытый) | 300 | 300 |

**Интеграция в ShipController:**
- Ссылка на `ShipFuelSystem`
- В `FixedUpdate()`: `fuelSystem.RegenFuel(dt)` при idle, `fuelSystem.ConsumeFuel()` при thrust > 0
- При `currentFuel <= 0`: thrust = 0 (двигатель глохнет)
- **Базовый расход** НЕ для мезиевой тяги — мезиевые модули берут топливо отдельно

#### 2. MeziyModuleActivator.cs — активатор мезиевых модулей

**MeziyModuleActivator (MonoBehaviour):**
```csharp
public class MeziyModuleActivator : MonoBehaviour {
    public ShipFuelSystem fuelSystem;
    public ShipModuleManager moduleManager;

    // Состояние активных мезиевых эффектов
    private Dictionary<string, MeziyState> activeMeziyEffects = new();

    public bool ActivateModule(ShipModule meziyModule);  // Активировать мезиевый модуль
    public bool IsOnCooldown(string moduleId);            // Проверить кулдаун
    public float GetCooldownRemaining(string moduleId);   // Оставшееся время CD
    public void UpdateCooldowns(float dt);                // Обновить кулдауны
}

public class MeziyState {
    public string moduleId;
    public float timeRemaining;   // Оставшееся время эффекта
    public float cooldownRemaining; // Оставшийся кулдаун
    public bool isActive => timeRemaining > 0;
    public bool isOnCooldown => cooldownRemaining > 0;
}
```

**Логика активации:**
1. Проверить что модуль установлен в слоте
2. Проверить что не на cooldown
3. Проверить что достаточно топлива (`meziyFuelCost`)
4. Списать топливо
5. Запустить эффект (`meziyForce` × `meziyDuration`)
7. Запустить cooldown (`meziyCooldown`)

#### 3. Интеграция мезиевых модулей в ShipController.cs

**Новые поля ShipController:**
```csharp
[Header("Мезиевая Тяга (Сессия 5)")]
[Tooltip("Активатор мезиевых модулей")]
[SerializeField] private MeziyModuleActivator meziyActivator;

[Tooltip("Система топлива корабля")]
[SerializeField] private ShipFuelSystem fuelSystem;

// Текущее состояние мезиевых эффектов
private Vector3 _activeMeziyTorque;  // Применяемый момент от мезиевой тяги
private float _activeMeziyTime;      // Оставшееся время эффекта
```

**Интеграция в FixedUpdate:**
```csharp
// Обновить кулдауны мезиевых модулей
if (meziyActivator != null)
    meziyActivator.UpdateCooldowns(dt);

// Применить активный мезиевый эффект
ApplyMeziyEffects(dt);

// Проверить топливо — если пусто, отключить тягу
if (fuelSystem != null && fuelSystem.IsEmpty)
{
    // thrust = 0 — двигатель заглох
    targetThrust = 0f;
}
```

**ApplyMeziyEffects:**
```csharp
private void ApplyMeziyEffects(float dt)
{
    if (meziyActivator == null || fuelSystem == null) return;

    // Получить активные мезиевые эффекты
    var activeEffects = meziyActivator.GetActiveEffects();

    // Применить torque к Rigidbody
    _rb.AddTorque(_activeMeziyTorque, ForceMode.Force);
}
```

**Input для мезиевых модулей:**
- MODULE_MEZIY_ROLL: **Left Shift + A/D** (крен влево/вправо)
- MODULE_MEZIY_PITCH: **Left Shift + W/S** (нос вверх/вниз)
- MODULE_MEZIY_YAW: **Left Shift + пробел** (резкий поворот в текущем направлении)

**Новый RPC для активации:**
```csharp
[Rpc(SendTo.Server)]
private void ActivateMeziyModuleRpc(string moduleId, RpcParams rpcParams = default);
```

#### 4. MODULE_ROLL — разблокировка крена

MODULE_ROLL (Utility, тир 2, power=10) разблокирует roll для корабля.

**Изменения в ShipController:**
- Добавить поле `private bool _rollUnlocked = false;`
- В `ApplyModuleModifiers()`: если MODULE_ROLL установлен → `_rollUnlocked = true`
- В `FixedUpdate()`: если `_rollUnlocked` → разрешить roll от ввода (A/D + Shift)
- В `ApplyStabilization()`: если `_rollUnlocked` → `rollStabForce` уменьшить (мягкая стабилизация)

**Параметры MODULE_ROLL:**
- `rollMultiplier = 1.0` (базовый крен)
- `maxRollAngle = 15f` (разрешённый угол крена)
- `powerConsumption = 10`
- tier 2, Utility

#### 5. Визуальный эффект сопел (базовый)

**MeziyThrusterVisual.cs (MonoBehaviour):**
```csharp
public class MeziyThrusterVisual : MonoBehaviour {
    [Header("Визуал")]
    public ParticleSystem thrustParticle;  // ParticleSystem сопла
    public Light glowLight;                // Свечение при активации
    public float particleIntensity = 1f;

    public void Activate();   // Включить частицы + свечение
    public void Deactivate(); // Выключить
}
```

**Примечание:** Это опциональный компонент. Если не назначен — мезиевая тяга работает без визуала.

#### 6. Editor утилиты для Сессии 5

**CreateMeziyModuleAssets:**
```
Menu: Tools → Project C → Create Meziy Module Assets
- Создаёт 4 тестовых модуля:
  1. MODULE_MEZIY_ROLL: meziyForce=25, meziyDuration=2s, междуyCooldown=10s, fuelCost=5
  2. MODULE_MEZIY_PITCH: meziyForce=10, междуyDuration=1.5s, междуyCooldown=8s, fuelCost=5
  3. MODULE_MEZIY_YAW: meziyForce=30, междуyDuration=0.5s, междуyCooldown=12s, fuelCost=5
  4. MODULE_ROLL: rollMultiplier=1.0, maxRollAngle=15, power=10
- Сохраняет в Assets/_Project/Data/Modules/
```

---

## АГЕНТЫ ДЛЯ ВЫЗОВА

### 1. @engine-programmer — основная реализация
**Задачи:**
- Создать `ShipFuelSystem.cs`
- Создать `MeziyModuleActivator.cs`
- Создать `MeziyThrusterVisual.cs`
- Интегрировать топливо и мезиевую тягу в `ShipController.cs`
- Добавить RPC для активации мезиевых модулей
- Проверить компиляцию в Unity

**Файлы:**
```
Assets/_Project/Scripts/Ship/ShipFuelSystem.cs           (НОВЫЙ)
Assets/_Project/Scripts/Ship/MeziyModuleActivator.cs     (НОВЫЙ)
Assets/_Project/Scripts/Ship/MeziyThrusterVisual.cs      (НОВЫЙ)
Assets/_Project/Scripts/Player/ShipController.cs          (ИЗМЕНИТЬ — топливо, meziy)
```

### 2. @gameplay-programmer — баланс мезиевых модулей
**Задачи:**
- Настроить параметры мезиевых модулей (force, duration, cooldown, fuelCost)
- Настроить топливо по классам кораблей
- Настроить базовый расход топлива
- Настроить MODULE_ROLL (разблокировка крена)

**Формулы баланса:**
```
MODULE_MEZIY_ROLL:
  meziyForce = 25        // Момент крена
  междуyDuration = 2s    // Длительность
  meziyCooldown = 10s    // Кулдаун
  meziyFuelCost = 5      // Стоимость

MODULE_MEZIY_PITCH:
  meziyForce = 10        // Момент тангажа
  междуyDuration = 1.5s
  meziyCooldown = 8s
  meziyFuelCost = 5

MODULE_MEZIY_YAW:
  meziyForce = 30        // Момент рыскания
  междуyDuration = 0.5s  // Быстрый бросок
  meziyCooldown = 12s
  meziyFuelCost = 5

MODULE_ROLL:
  maxRollAngle = 15°     // Разрешённый угол
  powerConsumption = 10

Топливо по классам:
  Light (Тороид):  50
  Medium (Баржа):  100
  Heavy (Платформа):  200
  HeavyII (Открытый): 300

Базовый расход (в секунду при тяге):
  Light:  0.5 fuel/s
  Medium: 0.8 fuel/s
  Heavy:  1.2 fuel/s
  HeavyII: 1.5 fuel/s

Восстановление (idle, тяга = 0):
  Все классы: 0.3 fuel/s
```

### 3. @unity-specialist — Editor утилита + визуал
**Задачи:**
- Создать Editor утилиту `CreateMeziyModuleAssets.cs`
- Создать `MeziyThrusterVisual.cs` с ParticleSystem + Light
- Проверить работу в Play Mode

### 4. @qa-tester — проверка качества
**Задачи:**
- Проверить что топливо расходуется при тяге
- Проверить что мезиевый модуль активируется и создаёт бросок
- Проверить что cooldown блокирует повторную активацию
- Проверить что топливо списывается на meziyFuelCost
- Проверить что при empty fuel двигатель глохнет
- Проверить что MODULE_ROLL разблокирует крен
- Проверить что сетевая совместимость сохранена

---

## КРИТЕРИИ ПРИЁМКИ СЕССИИ 5

| Критерий | Метрика | Pass/Fail |
|----------|---------|-----------|
| ShipFuelSystem работает | Топливо уменьшается при тяге | ☐ |
| Топливо восстанавливается на idle | fuel regen > 0 при thrust=0 | ☐ |
| При empty fuel двигатель глохнет | thrust = 0 когда fuel <= 0 | ☐ |
| MODULE_MEZIY_PITCH активирует бросок | Нос наклоняется на ~10° за 1.5с | ☐ |
| После активации — cooldown 8с | Нельзя активировать раньше | ☐ |
| Топливо уменьшается на 5 | fuelCost списывается | ☐ |
| MODULE_ROLL разблокирует крен | Крен работает при установленном модуле | ☐ |
| Визуал сопла при активации | ParticleSystem включается | ☐ |
| Сетевая совместимость сохранена | RPC работают, кооп не сломан | ☐ |
| Editor утилита создаёт мезиевые модули | Menu → Tools → Create Meziy Assets | ☐ |
| Компиляция без ошибок | Unity Console = 0 errors | ☐ |

---

## СВЯЗАННЫЕ ФАЙЛЫ (ЧИТАТЬ ПЕРЕД РАБОТОЙ)

| Файл | Зачем |
|------|-------|
| `docs/Ships/SESSION_4_COMPLETE.md` | Что готово в Сессии 4 |
| `docs/Ships/SHIP_MOVEMENT_IMPLEMENTATION_PLAN.md` | Общий план, §Сессия 5 |
| `docs/Ships/ShipRegistry.md` | Каталог модулей, совместимость, fuelCapacity |
| `docs/Ships/SHIP_CLASS_PRESETS.md` | Параметры классов кораблей |
| `Assets/_Project/Scripts/Player/ShipController.cs` | Текущий код — v2.3 |
| `Assets/_Project/Scripts/Ship/ShipModule.cs` | ScriptableObject модуля (meziy поля уже есть!) |
| `Assets/_Project/Scripts/Ship/ShipModuleManager.cs` | Менеджер модулей |
| `docs/bugs/SESSION4_MODULESLOT_TYPE_VALIDATION.md` | Исправленный баг валидации |

---

## ПОШАГОВЫЙ ПЛАН СЕССИИ

### Шаг 1: Оркестрация (10 мин)
```
1. Прочитать docs/Ships/SESSION_4_COMPLETE.md — что готово
2. Прочитать docs/Ships/ShipRegistry.md §3 — каталог мезиевых модулей
3. Запустить 3 агентов параллельно:
   - @engine-programmer → ShipFuelSystem + MeziyModuleActivator
   - @gameplay-programmer → баланс, топливо, формулы
   - @unity-specialist → Editor утилита, MeziyThrusterVisual
```

### Шаг 2: Система топлива (30 мин)
```
1. Создать ShipFuelSystem.cs
2. Создать MeziyModuleActivator.cs
3. Интегрировать топливо в ShipController (расход + regen + empty check)
4. Коммит: "feat: Ship fuel system — consumption, regen, empty stall"
```

### Шаг 3: Мезиевые модули (40 мин)
```
1. Интегрировать активацию мезиевых модулей в ShipController
2. Добавить RPC для активации
3. Добавить MODULE_ROLL (разблокировка крена)
4. Создать Editor утилиту для мезиевых модулей
5. Коммит: "feat: Meziy Thrust modules — roll, pitch, yaw activation + cooldown"
```

### Шаг 4: Визуал и баланс (20 мин)
```
1. Создать MeziyThrusterVisual.cs
2. Настроить параметры модулей по ShipRegistry
3. Настроить топливо по классам
4. Пользователь тестирует → фидбек → итерация
5. Коммит: "balance: meziy module parameters tuned"
```

### Шаг 5: Финальная проверка
```
1. Проверить 0 ошибок компиляции
2. Проверить что кооп-пилотирование работает
3. Проверить что топливо расходуется
4. Проверить что мезиевые модули активируются
5. Проверить что MODULE_ROLL разблокирует крен
6. Git push
```

---

## ВАЖНЫЕ ПРЕДОСТЕРЕЖЕНИЯ

### ⚠️ НЕ ЛОМАТЬ
- **RPC сигнатуры:** `SubmitShipInputRpc`, `AddPilotRpc`, `RemovePilotRpc` — не менять
- **NetworkObject/NetworkTransform конфигурацию** — не трогать
- **Module System (Сессия 4)** — уже работает, не менять без причины
- **AltitudeCorridorSystem** — уже работает
- **WindZone system** — уже работает
- **asmdef файлы** — НЕ СОЗДАВАТЬ

### ⚠️ ПРОВЕРЯТЬ
- **Компиляцию в Unity** после каждого изменения
- **Сетевую совместимость** — мезиевая активация должна реплицироваться
- **Топливо** — корректный расход и восстановление для каждого класса

### ⚠️ ИЗВЛЕЧЁННЫЕ УРОКИ
1. **НЕ создавать asmdef** без анализа зависимостей
2. **Проверять компиляцию в Unity вручную** перед коммитом
3. **OnValidate()** для ModuleSlot решает баг Inspector валидации (Сессия 4)
4. Roll заблокирован — разблокировать ТОЛЬКО через MODULE_ROLL

---

## ОЖИДАЕМЫЙ РЕЗУЛЬТАТ

После Сессии 5:
- ✅ ShipFuelSystem работает (расход + regen + empty stall)
- ✅ MODULE_MEZIY_PITCH активируется — бросок тангажа
- ✅ MODULE_MEZIY_ROLL активируется — бросок крена
- ✅ MODULE_MEZIY_YAW активируется — резкий поворот
- ✅ Cooldown система работает
- ✅ MODULE_ROLL разблокирует крен
- ✅ Визуал сопел при активации
- ✅ Топливо корректно по классам
- ✅ Сетевая совместимость сохранена
- ✅ **Документ:** `docs/Ships/SESSION_5_COMPLETE.md`

---

*Промпт подготовлен на основе: SESSION_4_COMPLETE.md, ShipRegistry.md, SHIP_MOVEMENT_IMPLEMENTATION_PLAN.md*
*Текущая версия ShipController: v2.3 | Следующая версия после Сессии 5: v2.4*
