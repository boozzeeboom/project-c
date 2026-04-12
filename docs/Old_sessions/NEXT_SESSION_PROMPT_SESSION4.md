# Промпт для Сессии 4: Module System Foundation

> **Контекст:** Этот документ — готовый промпт для запуска 4-й сессии разработки системы управления кораблями. Скопируйте его содержимое и используйте как инструкцию для Qwen Code.

---

## 📋 ПРОМПТ НАЧАЛО

Ты начинаешь **Сессию 4: Module System Foundation** для проекта Project C: The Clouds — MMO/Co-Op авиасимулятора над облаками по книге «Интеграл Пьявица».

**Ветка:** `qwen-gamestudio-agent-dev`
**Дата:** Апрель 2026

---

## ЧТО УЖЕ ГОТОВО (Сессии 1-3)

### Сессия 1: Core Smooth Movement ✅
- `ShipController.cs` v2.1 переписан с `Mathf.SmoothDamp` для frame-rate независимого сглаживания
- 4 класса кораблей (Light/Medium/Heavy/HeavyII) с разными параметрами
- Стабилизация: pitchStabForce=15, rollStabForce=20, angularDrag=8.0
- Thrust ramp-up 0.3s, yaw decay 1.0s, lift clamp 2.5 м/с
- **Извлечённые уроки:** НЕ создавать asmdef без анализа зависимостей; Mass=1000 для кораблей; Collider=8×1.5×4
- **Документ:** `docs/Ships/SESSION_1_COMPLETE.md`

### Сессия 2: Altitude Corridor System ✅
- `AltitudeCorridorData.cs` — ScriptableObject коридоров высот
- `AltitudeCorridorSystem.cs` — менеджер-синглтон с 6 коридорами (глобальный + 5 городов)
- `TurbulenceEffect.cs` — класс турбулентности (тряска ниже minAlt)
- `SystemDegradationEffect.cs` — класс деградации систем (выше maxAlt)
- `AltitudeUI.cs` — HUD программно создающий панель предупреждений
- Editor утилита `CreateAltitudeCorridorAssets.cs`
- **Известные проблемы Сессии 2:** UI работает но требует ручного назначения в Unity; деградация рассчитывает модификаторы но не применяет их к ShipController напрямую; турбулентность только на сервере
- **Документ:** `docs/Ships/SESSION_2_COMPLETE.md`

### Сессия 3: Wind & Environmental Forces ✅
- `WindZoneData.cs` — ScriptableObject данных зон ветра (Constant, Gust, Shear)
- `WindZone.cs` — MonoBehaviour объёмных триггерных зон с Gizmos
- Интеграция ветра в `ShipController.cs` v2.2: `ApplyWind()`, `RegisterWindZone()`, `UnregisterWindZone()`
- `windExposure` по классам: Light=1.2, Medium=1.0, Heavy=0.7, HeavyII=0.5
- Улучшенная турбулентность: `SetShipClassMultiplier()` + Cinemachine Impulse (с проверкой пакета)
- Editor утилита `CreateWindZoneTestScene.cs` — создаёт 3 тестовые зоны
- **Известные ограничения Сессии 3:** Визуальная проверка ветра затруднена (зоны не видны в Game view); Cinemachine Impulse работает только при установленном пакете; 5 Unity тестов не созданы — отложено
- **Документ:** `docs/Ships/SESSION_3_COMPLETE.md`

### Текущий ShipController.cs v2.2 — что есть сейчас
- `FixedUpdate()`: AverageInputs → SmoothThrust/Yaw/Pitch/Lift → ValidateAltitude → ApplyForces → Stabilization → ApplyWind → Clamp
- Сетевая совместимость: `SubmitShipInputRpc`, `AddPilotRpc`, `RemovePilotRpc` — НЕ ЛОМАТЬ
- Работает только на сервере (`if (!IsServer) return`)
- Корабли ощущаются как плавные баржи — SmoothDamp + decay + stabilization
- Ветер работает как объёмные зоны (войти → снесло, выйти → затухло)
- Турбулентность нарастает плавно при приближении к Завесе
- **Документ:** `Assets/_Project/Scripts/Player/ShipController.cs`

---

## ЗАДАЧА СЕССИИ 4: Module System Foundation

Сессия 4 описана в `docs/Ships/SHIP_MOVEMENT_IMPLEMENTATION_PLAN.md` §Сессия 4.

### Что нужно реализовать

#### 1. ShipModule.cs — базовый класс модуля (ScriptableObject)

ShipModule — это ScriptableObject который определяет модуль корабля: его тип, эффекты, требования к слоту, совместимость.

**ShipModule (ScriptableObject):**
```csharp
[CreateAssetMenu(menuName = "ProjectC/Ship/Module", fileName = "Module_")]
public class ShipModule : ScriptableObject {
    [Header("Идентификатор")]
    public string moduleId;          // Уникальный ID (MODULE_YAW_ENH)
    public string displayName;       // Отображаемое имя
    public ModuleType type;          // Propulsion, Utility, Special
    public int tier;                 // 1-4 — тир модуля

    [Header("Эффекты")]
    public float thrustMultiplier = 1f;
    public float yawMultiplier = 1f;
    public float pitchMultiplier = 1f;
    public float liftMultiplier = 1f;
    public float maxSpeedModifier = 0f;   // ±м/с
    public float windExposureModifier = 0f; // ±к windExposure

    [Header("Требования")]
    public int powerConsumption = 0;   // Единицы энергии
    public List<ShipFlightClass> compatibleClasses; // На каких классах работает
    public List<string> requiredModules;  // Какие модули должны быть установлены
    public List<string> incompatibleModules; // Какие модули НЕсовместимы

    [Header("Мезиевая Тяга (только для MEZIY модулей)")]
    public bool isMeziyModule = false;
    public float meziyForce = 0f;
    public float meziyDuration = 0f;
    public float meziyCooldown = 0f;
    public float meziyFuelCost = 0f;
}

public enum ModuleType {
    Propulsion,   // Движение (yaw, pitch, lift enhancement)
    Utility,      // Утилиты (roll, veil, stealth)
    Special       // Специальные (auto-dock, auto-nav, space)
}
```

#### 2. ModuleSlot.cs — слот модуля на корабле

ModuleSlot — компонент на корабле который представляет слот для установки модуля.

**ModuleSlot (MonoBehaviour):**
```csharp
public class ModuleSlot : MonoBehaviour {
    public SlotType slotType;  // Propulsion, Utility, Special
    public ShipModule installedModule;  // Установленный модуль (null = пусто)
    public bool isOccupied => installedModule != null;

    public bool InstallModule(ShipModule module);
    public void RemoveModule();
    public bool ValidateCompatibility(ShipModule module);
}

public enum SlotType {
    Propulsion,
    Utility,
    Special
}
```

#### 3. ShipModuleManager.cs — менеджер модулей на корабле

ShipModuleManager управляет слотами, валидацией, применением эффектов модулей.

**ShipModuleManager (MonoBehaviour):**
```csharp
public class ShipModuleManager : MonoBehaviour {
    public List<ModuleSlot> slots = new();
    public int availablePower;
    public int currentPowerUsage;

    public bool InstallModule(ModuleSlot slot, ShipModule module);
    public void RemoveModule(ModuleSlot slot);
    public void ApplyModuleEffects(ShipController ship);
    public bool ValidateConfiguration();
    public int GetAvailablePower();
}
```

#### 4. Интеграция модулей в ShipController.cs

ShipController должен:
- Иметь ссылку на ShipModuleManager
- В `Awake()` применять эффекты установленных модулей
- В `FixedUpdate()` учитывать модификаторы модулей (thrustMultiplier, yawMultiplier и т.д.)
- Мезиевые модули: отдельный метод активации с cooldown и fuel cost

**Новые поля ShipController:**
```csharp
[Header("Модули (Сессия 4)")]
[Tooltip("Менеджер модулей корабля")]
[SerializeField] private ShipModuleManager moduleManager;

// Модификаторы от модулей (применяются в FixedUpdate)
private float _moduleThrustMult = 1f;
private float _moduleYawMult = 1f;
private float _modulePitchMult = 1f;
private float _moduleLiftMult = 1f;
```

**Интеграция в FixedUpdate:**
```csharp
// После AverageInputs, перед применением сил:
ApplyModuleModifiers();

// В ApplyModuleModifiers:
_moduleThrustMult = moduleManager != null ? moduleManager.GetThrustMultiplier() : 1f;
// ... и т.д. для yaw, pitch, lift

// В ApplyThrustForce:
_rb.AddForce(transform.forward * currentThrust * _moduleThrustMult * cargoPenalty, ForceMode.Force);
```

#### 5. Editor утилиты для Сессии 4

**CreateModuleAssets:**
```
Menu: Tools → Project C → Create Module Test Assets
- Создаёт 3 тестовых модуля:
  1. MODULE_YAW_ENH: yawMultiplier = 1.4, tier 1, Propulsion
  2. MODULE_PITCH_ENH: pitchMultiplier = 1.3, tier 1, Propulsion
  3. MODULE_LIFT_ENH: liftMultiplier = 1.5, tier 1, Propulsion
- Сохраняет в Assets/_Project/Data/Modules/
```

**Module Inspector Extension:**
```
Menu: Tools → Project C → Module Compatibility Checker
- Выбрать корабль → показать совместимые модули
- Выбрать модуль → показать совместимые корабли
```

---

## АГЕНТЫ ДЛЯ ВЫЗОВА

Запусти этих агентов из game-studio (папка `.qwenencode/agents/`) для параллельной работы:

### 1. @engine-programmer — основная реализация
**Задачи:**
- Создать `ShipModule.cs` (ScriptableObject)
- Создать `ModuleSlot.cs` (MonoBehaviour)
- Создать `ShipModuleManager.cs` (MonoBehaviour)
- Интегрировать модули в `ShipController.cs` (ApplyModuleModifiers)
- Проверить компиляцию в Unity

**Файлы для создания/изменения:**
```
Assets/_Project/Scripts/Ship/ShipModule.cs          (НОВЫЙ)
Assets/_Project/Scripts/Ship/ModuleSlot.cs           (НОВЫЙ)
Assets/_Project/Scripts/Ship/ShipModuleManager.cs    (НОВЫЙ)
Assets/_Project/Scripts/Player/ShipController.cs      (ИЗМЕНИТЬ — ApplyModuleModifiers)
Assets/_Project/Editor/CreateModuleTestAssets.cs     (НОВЫЙ — опционально)
```

### 2. @gameplay-programmer — баланс и совместимость
**Задачи:**
- Настроить эффекты модулей тира 1 (YAW_ENH, PITCH_ENH, LIFT_ENH)
- Настроить систему энергии (powerConsumption vs availablePower)
- Настроить совместимость модулей с классами кораблей
- Написать формулы эффектов модулей

**Формулы для баланса (тир 1 модули):**
```
MODULE_YAW_ENH:
  yawMultiplier = 1.4  (+40% к yawSpeed)
  powerConsumption = 5
  compatibleClasses: All
  incompatibleModules: none

MODULE_PITCH_ENH:
  pitchMultiplier = 1.3  (+30% к pitchSpeed)
  powerConsumption = 5
  compatibleClasses: All
  incompatibleModules: none

MODULE_LIFT_ENH:
  liftMultiplier = 1.5  (+50% к liftSpeed)
  powerConsumption = 8
  compatibleClasses: All
  incompatibleModules: none

Энергия корабля по классам:
  Light:  20 power
  Medium: 35 power
  Heavy:  50 power
  HeavyII: 65 power
```

### 3. @unity-specialist — ScriptableObject pipeline + тесты
**Задачи:**
- Создать Editor утилиту для создания тестовых модулей
- Написать 3 Unity теста (см. ниже)
- Проверить работу в Play Mode
- Проверить ScriptableObject pipeline (создать → настроить → применить)

**Тесты (ModuleSystemTests.cs):**
```csharp
[UnityTest] IEnumerator InstallModule_AppliesEffects()
[UnityTest] IEnumerator IncompatibleModule_BlocksInstallation()
[UnityTest] IEnumerator RemoveModule_EffectsReturnToBase()
```

### 4. @qa-tester — проверка качества
**Задачи:**
- Проверить что компиляция работает
- Проверить что модуль устанавливается в слот
- Проверить что эффекты модуля применяются к кораблю
- Проверить что несовместимый модуль блокируется
- Проверить что при снятии модуля эффекты возвращаются
- Проверить что сетевая совместимость не сломана (кооп работает)

---

## КРИТЕРИИ ПРИЁМКИ СЕССИИ 4

| Критерий | Метрика | Pass/Fail |
|----------|---------|-----------|
| ShipModule ScriptableObject создан | Файл существует, создаётся через Create Asset | ☐ |
| ModuleSlot MonoBehaviour работает | Слот показывает occupied/unoccupied | ☐ |
| ShipModuleManager управляет слотами | Install/Remove работает | ☐ |
| Модуль YAW_ENH ускоряет поворот | yawMultiplier = 1.4 применяется | ☐ |
| Несовместимый модуль блокируется | ValidateCompatibility возвращает false | ☐ |
| Снятие модуля возвращает эффекты | yawMultiplier = 1.0 после снятия | ☐ |
| Энергия корабля ограничивает модули | Превышение = нельзя установить | ☐ |
| Сетевая совместимость сохранена | RPC работают, кооп не сломан | ☐ |
| Editor утилита создаёт тестовые модули | Menu → Tools → Create Module Assets | ☐ |
| 3 Unity теста проходят | ModuleSystemTests.cs | ☐ |
| Компиляция без ошибок | Unity Console = 0 errors | ☐ |

---

## СВЯЗАННЫЕ ФАЙЛЫ (ЧИТАТЬ ПЕРЕД РАБОТОЙ)

| Файл | Зачем |
|------|-------|
| `docs/Ships/SESSION_3_COMPLETE.md` | Что готово в Сессии 3 |
| `docs/Ships/SHIP_MOVEMENT_IMPLEMENTATION_PLAN.md` | Общий план, §Сессия 4 |
| `docs/Ships/ShipRegistry.md` | Каталог модулей и совместимость |
| `docs/Ships/SHIP_CLASS_PRESETS.md` | Параметры классов кораблей |
| `Assets/_Project/Scripts/Player/ShipController.cs` | Текущий код — v2.2 |
| `Assets/_Project/Scripts/Ship/WindZone.cs` | Система ветра (Сессия 3) |
| `Assets/_Project/Scripts/Ship/TurbulenceEffect.cs` | Турбулентность |
| `game-studio/QWENCODE.md` | Архитектура агентов game-studio |
| `game-studio/README.md` | Список всех 39 агентов |

---

## ПОШАГОВЫЙ ПЛАН СЕССИИ

### Шаг 1: Оркестрация (10 мин)
```
1. Прочитать docs/Ships/SESSION_3_COMPLETE.md — что готово
2. Прочитать docs/Ships/SHIP_MOVEMENT_IMPLEMENTATION_PLAN.md §Сессия 4 — что нужно
3. Прочитать docs/Ships/ShipRegistry.md §3 — каталог модулей
4. Запустить 3 агентов параллельно:
   - @engine-programmer → ShipModule + ModuleSlot + ShipModuleManager
   - @gameplay-programmer → баланс, формулы, энергия
   - @unity-specialist → Editor утилита, тесты
```

### Шаг 2: Создание Module System (40 мин)
```
1. Создать ShipModule.cs (ScriptableObject)
2. Создать ModuleSlot.cs (MonoBehaviour)
3. Создать ShipModuleManager.cs (MonoBehaviour)
4. Создать Editor утилиту для тестовых модулей
5. Коммит: "feat: Module System foundation — ShipModule, ModuleSlot, Manager"
```

### Шаг 3: Интеграция в ShipController (20 мин)
```
1. Добавить ссылку на ShipModuleManager
2. Добавить модификаторы (_moduleThrustMult и т.д.)
3. Создать ApplyModuleModifiers() метод
4. Интегрировать в FixedUpdate
5. Коммит: "feat: integrate modules into ShipController"
```

### Шаг 4: Баланс и калибровка (15 мин)
```
1. Настроить эффекты тир 1 модулей (YAW_ENH +40%, PITCH_ENH +30%, LIFT_ENH +50%)
2. Настроить энергию корабля по классам
3. Настроить совместимость модулей
4. Пользователь тестирует в Unity → фидбек → итерация
5. Коммит: "balance: module parameters tuned"
```

### Шаг 5: Тесты (20 мин)
```
1. Создать ModuleSystemTests.cs (3 теста)
2. Проверить компиляцию
3. Запустить тесты
4. Коммит: "test: 3 module system tests"
```

### Шаг 6: Финальная проверка
```
1. Проверить 0 ошибок компиляции
2. Проверить что кооп-пилотирование работает
3. Проверить что ветер и коридоры работают
4. Проверить что модули устанавливаются и применяются
5. Git push
```

---

## ВАЖНЫЕ ПРЕДОСТЕРЕЖЕНИЯ

### ⚠️ НЕ ЛОМАТЬ
- **RPC сигнатуры:** `SubmitShipInputRpc`, `AddPilotRpc`, `RemovePilotRpc` — не менять
- **NetworkObject/NetworkTransform конфигурацию** — не трогать
- **AltitudeCorridorSystem** — уже работает, не менять без причины
- **WindZone system** — уже работает, не менять без причины
- **asmdef файлы** — НЕ СОЗДАВАТЬ (см. SESSION_1_COMPLETE.md каскад ошибок)

### ⚠️ ПРОВЕРЯТЬ
- **Компиляцию в Unity** после каждого изменения — открывать Editor и проверять Console
- **Сетевую совместимость** — кооп-пилотирование должно работать
- **Классы кораблей** — каждый класс должен иметь свою энергию

### ⚠️ ИЗВЛЕЧЁННЫЕ УРОКИ
1. **НЕ создавать asmdef** без полного анализа зависимостей (SESSION_1: 57 ошибок)
2. **Проверять компиляцию в Unity вручную** перед коммитом
3. **Mass = 1000** для кораблей (не 1kg дефолт)
4. **angularDrag = 8.0** — достаточно для гашения вращения
5. **Конфликт имён WindZone** — использовать полное имя `ProjectC.Ship.WindZone`
6. **Проверять компиляцию в Unity в ручную пользователем** перед коммитом

---

## ОЖИДАЕМЫЙ РЕЗУЛЬТАТ

После Сессии 4:
- ✅ ShipModule ScriptableObject создан и работает
- ✅ ModuleSlot показывает occupied/unoccupied
- ✅ ShipModuleManager управляет слотами и эффектами
- ✅ YAW_ENH модуль ускоряет поворот на 40%
- ✅ Несовместимые модули блокируются
- ✅ Снятие модуля возвращает базовые эффекты
- ✅ Энергия корабля ограничивает установку модулей
- ✅ 3 теста проходят
- ✅ Сетевая совместимость сохранена
- ✅ **Документ:** `docs/Ships/SESSION_4_COMPLETE.md`

---

*Промпт подготовлен на основе: SESSION_3_COMPLETE.md, ShipRegistry.md, SHIP_MOVEMENT_IMPLEMENTATION_PLAN.md*
*Текущая версия ShipController: v2.2 | Следующая версия после Сессии 4: v2.3*
