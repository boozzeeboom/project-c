# Промпт для Сессии 3: Wind & Turbulence

> **Контекст:** Этот документ — готовый промпт для запуска 3-й сессии разработки системы управления кораблями. Скопируйте его содержимое и используйте как инструкцию для Qwen Code.

---

## 📋 ПРОМПТ НАЧАЛО

Ты начинаешь **Сессию 3: Wind & Environmental Forces** для проекта Project C: The Clouds — MMO/Co-Op авиасимулятора над облаками по книге «Интеграл Пьявица».

**Ветка:** `qwen-gamestudio-agent-dev`
**Дата:** Апрель 2026

---

## ЧТО УЖЕ ГОТОВО (Сессии 1-2)

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

### Текущий ShipController.cs v2.1 — что есть сейчас
- `FixedUpdate()`: AverageInputs → SmoothThrust/Yaw/Pitch/Lift → ValidateAltitude → ApplyForces → Stabilization → Clamp
- Сетевая совместимость: `SubmitShipInputRpc`, `AddPilotRpc`, `RemovePilotRpc` — НЕ ЛОМАТЬ
- Работает только на сервере (`if (!IsServer) return`)
- Корабли ощущаются как плавные баржи — SmoothDamp + decay + stabilization
- **Документ:** `Assets/_Project/Scripts/Player/ShipController.cs`

---

## ЗАДАЧА СЕССИИ 3: Wind & Environmental Forces

Сессия 3 описана в `docs/Ships/SHIP_MOVEMENT_IMPLEMENTATION_PLAN.md` §Сессия 3.

### Что нужно реализовать

#### 1. WindZone.cs — объёмные триггеры зон ветра

WindZone — это объёмные зоны (BoxCollider/SphereCollider trigger) которые применяют силу ветра к кораблю когда он входит в зону.

**Требования:**
- ScriptableObject `WindZoneData` для данных зоны (направление, сила, профиль)
- Volume trigger: BoxCollider или SphereCollider с `isTrigger = true`
- При `OnTriggerEnter` корабль начинает получать силу ветра
- При `OnTriggerExit` сила ветра плавно затухает
- Несколько зон могут перекрываться — силы суммируются (vector addition)
- Wind lanes: визуальные воздушные коридоры между пиками (для Сессии 3 — заглушки, данные для будущего)

**WindZoneData (ScriptableObject):**
```csharp
[CreateAssetMenu]
public class WindZoneData : ScriptableObject {
    public string zoneId;
    public string displayName;
    public Vector3 windDirection = Vector3.forward;  // направление ветра
    public float windForce = 50f;                     // сила ветра
    public float windVariation = 0.2f;               // случайное отклонение (0-1)
    public WindProfile profile = WindProfile.Constant; // Constant, Gust, Shear
    public float gustInterval = 2f;                   // интервал порывов (для Gust)
    public float shearGradient = 0.1f;               // градиент сдвига (для Shear)
}

public enum WindProfile {
    Constant,    // Постоянный ветер
    Gust,        // Порывистый (периодические усиления)
    Shear        // Сдвиг ветра (меняется с высотой)
}
```

**WindZone (MonoBehaviour):**
```csharp
[RequireComponent(typeof(Collider))]
public class WindZone : MonoBehaviour {
    public WindZoneData windData;
    private HashSet<ShipController> _shipsInZone = new();

    void OnTriggerEnter(Collider other) {
        if (other.TryGetComponent<ShipController>(out var ship)) {
            _shipsInZone.Add(ship);
            ship.RegisterWindZone(this);
        }
    }

    void OnTriggerExit(Collider other) {
        if (other.TryGetComponent<ShipController>(out var ship)) {
            _shipsInZone.Remove(ship);
            ship.UnregisterWindZone(this);
        }
    }

    public Vector3 GetWindForceAtPosition(Vector3 position) {
        // Рассчитать силу ветра с учётом профиля
        // Constant: windDirection.normalized * windForce
        // Gust: + sin(Time.time / gustInterval) * variation
        // Shear: + position.y * shearGradient
    }
}
```

#### 2. Интеграция ветра в ShipController.cs

ShipController должен:
- Иметь список зарегистрированных WindZone (`List<WindZone>`)
- В `FixedUpdate()` вызывать `ApplyWind(dt)` (заглушка из Сессии 1 → реальная логика)
- Суммировать силы ветра от всех зон (vector addition)
- Применять силу ветра к Rigidbody: `_rb.AddForce(totalWindForce, ForceMode.Force)`
- Учитывать массу корабля: тяжёлые корабли меньше сносит (`windExposure` параметр)
- Плавное затухание при выходе из зоны (lerp к 0 за 1-2 секунды)

**Новые поля ShipController:**
```csharp
[Header("Wind & Environmental Forces (Сессия 3)")]
[Tooltip("Влияние ветра на корабль (1.0 = полный снос, 0.0 = игнор)")]
[SerializeField] private float windInfluence = 0.5f;
[Tooltip("Экспозиция к ветру (зависит от класса: Light=1.2, Medium=1.0, Heavy=0.7)")]
[SerializeField] private float windExposure = 1.0f;
[Tooltip("Время затухания ветра при выходе из зоны")]
[SerializeField] private float windDecayTime = 1.5f;

// Wind state
private List<WindZone> _activeWindZones = new();
private Vector3 _currentWindForce;
```

**ApplyWind метод:**
```csharp
private void ApplyWind(float dt) {
    if (_activeWindZones.Count == 0) {
        // Затухание к 0 при выходе из всех зон
        _currentWindForce = Vector3.Lerp(_currentWindForce, Vector3.zero, dt / windDecayTime);
    } else {
        // Суммировать ветер от всех активных зон
        Vector3 totalWind = Vector3.zero;
        foreach (var zone in _activeWindZones) {
            totalWind += zone.GetWindForceAtPosition(transform.position);
        }
        // Lerp к целевой силе (плавный переход между зонами)
        _currentWindForce = Vector3.Lerp(_currentWindForce, totalWind, dt / windDecayTime);
    }

    // Применить с учётом влияния и экспозиции
    Vector3 windEffect = _currentWindForce * windInfluence * windExposure;
    _rb.AddForce(windEffect, ForceMode.Force);
}
```

#### 3. Turbulence у Завесы — улучшенная версия

Текущий `TurbulenceEffect.cs` работает но:
- Применяется только на сервере (клиенты не видят тряску)
- Нет Cinemachine Impulse для тряски камеры
- Параметры калиброваны но не связаны с классами кораблей

**Улучшения для Сессии 3:**
- Cinemachine Impulse: при турбулентности > порога → посылать импульс камере
- Класс корабля влияет на турбулентность: Light трясёт сильнее, Heavy меньше
- Плавное нарастание: чем ближе к Завесе (ниже minAlt), тем сильнее тряска
- Severity уже есть в `TurbulenceEffect.CalculateSeverity()` — использовать

**Cinemachine Impulse (заглушка если пакет не установлен):**
```csharp
// Проверить наличие Cinemachine
#if CINEMACHINE_ENABLED
private CinemachineImpulseSource _impulseSource;

void InitializeImpulse() {
    _impulseSource = GetComponent<CinemachineImpulseSource>();
    if (_impulseSource == null) {
        _impulseSource = gameObject.AddComponent<CinemachineDefaultImpulseSource>();
    }
}

void ApplyTurbulenceImpulse(float severity) {
    if (severity > 0.3f && _impulseSource != null) {
        _impulseSource.GenerateImpulse(severity * 2f);
    }
}
#else
// Заглушка — логирование для отладки
void ApplyTurbulenceImpulse(float severity) {
    if (severity > 0.5f) {
        Debug.Log($"[ShipController] TURBULENCE IMPULSE (Cinemachine not installed): severity={severity:F2}");
    }
}
#endif
```

#### 4. Editor утилиты для Сессии 3

**CreateWindZones:**
```
Menu: Tools → Project C → Create Wind Zone Test Scene
- Создаёт тестовую сцену с 3 зонами ветра:
  1. Constant Wind: северо-западный ветер, сила 30
  2. Gust Wind: порывистый, интервал 2с
  3. Shear Wind: сдвиг по высоте
- Корабль можно перемещать между зонами для тестирования
```

**WindZone Gizmo:**
```
Menu: Tools → Project C → Enable Wind Zone Gizmos
- В Scene view рисует стрелки направления ветра
- Цвет = сила (зелёный=слабый, жёлтый=средний, красный=сильный)
```

---

## АГЕНТЫ ДЛЯ ВЫЗОВА

Запусти этих агентов из game-studio (папка `.qwenencode/agents/`) для параллельной работы:

### 1. @engine-programmer — основная реализация
**Задачи:**
- Создать `WindZoneData.cs` (ScriptableObject)
- Создать `WindZone.cs` (MonoBehaviour с trigger logic)
- Интегрировать ветер в `ShipController.cs` (ApplyWind метод)
- Добавить wind fields в Editor утилиту
- Проверить компиляцию в Unity

**Файлы для создания/изменения:**
```
Assets/_Project/Scripts/Ship/WindZoneData.cs         (НОВЫЙ)
Assets/_Project/Scripts/Ship/WindZone.cs              (НОВЫЙ)
Assets/_Project/Scripts/Player/ShipController.cs      (ИЗМЕНИТЬ — ApplyWind + поля)
Assets/_Project/Editor/CreateWindZoneTestScene.cs     (НОВЫЙ — опционально)
```

### 2. @gameplay-programmer — баланс и feel
**Задачи:**
- Настроить параметры ветра: windInfluence, windExposure по классам
- Настроить профили ветра (Constant, Gust, Shear)
- Баланс турбулентности: как сильно трясёт разные классы
- Калибровка Cinemachine Impulse силы
- Написать формулы зависимости от массы корабля

**Формулы для баланса:**
```
windExposure по классам:
  Light:  1.2 (сильнее сносит)
  Medium: 1.0 (баланс)
  Heavy:  0.7 (меньше сносит)
  HeavyII: 0.5 (очень устойчив)

Turbulence intensity по классам:
  Light:  turbulenceIntensity × 1.3
  Medium: turbulenceIntensity × 1.0
  Heavy:  turbulenceIntensity × 0.7
  HeavyII: turbulenceIntensity × 0.5

Wind force расчёт:
  finalWindForce = windDirection.normalized × windForce × windInfluence × windExposure × (1 - mass/2000)
```

### 3. @unity-specialist — Cinemachine + тесты
**Задачи:**
- Cinemachine Impulse интеграция (с проверкой наличия пакета)
- Создать тестовую сцену для ветра
- Написать 5 Unity тестов (см. ниже)
- Создать WindZone Gizmo для визуализации в Scene view
- Проверить работу в Play Mode

**Тесты (WindAndTurbulenceTests.cs):**
```csharp
[UnityTest] IEnumerator WindZone_AppliesForceToShip()
[UnityTest] IEnumerator MultipleWindZones_VectorSum()
[UnityTest] IEnumerator HeavyShipLessAffectedByWind()
[UnityTest] IEnumerator WindDecay_AfterExitingZone()
[UnityTest] IEnumerator Turbulence_IncreasesNearMinAlt()
```

### 4. @qa-tester — проверка качества
**Задачи:**
- Проверить что компиляция работает
- Проверить что корабль сносится в зоне ветра
- Проверить что тяжёлый корабль меньше сносит
- Проверить что при выходе из зоны ветер затухает
- Проверить турбулентность на границе коридора
- Проверить что сетевая совместимость не сломана (кооп работает)

---

## КРИТЕРИИ ПРИЁМКИ СЕССИИ 3

| Критерий | Метрика | Pass/Fail |
|----------|---------|-----------|
| WindZone Data ScriptableObject создан | Файл существует, создаётся через Create Asset | ☐ |
| WindZone MonoBehaviour работает | Trigger enter/exit логирует | ☐ |
| Ветер применяет силу к кораблю | Корабль сносится в зоне | ☐ |
| Векторная сумма нескольких зон | 2 противоположных ветра = 0 | ☐ |
| WindDecay плавный | Затухание за 1.5с | ☐ |
| Heavy ship менее подвержен ветру | windExposure = 0.7 vs 1.2 | ☐ |
| Турбулентность нарастает плавно | Severity 0→1 при углублении | ☐ |
| Cinemachine Impulse (если установлен) | Камера трясётся при turbulence > 0.3 | ☐ |
| Сетевая совместимость сохранена | RPC работают, кооп не сломан | ☐ |
| Editor утилита создаёт тестовые зоны | Menu → Tools → Create Wind Zones | ☐ |
| 5 Unity тестов проходят | WindAndTurbulenceTests.cs | ☐ |
| Компиляция без ошибок | Unity Console = 0 errors | ☐ |

---

## СВЯЗАННЫЕ ФАЙЛЫ (ЧИТАТЬ ПЕРЕД РАБОТОЙ)

| Файл | Зачем |
|------|-------|
| `docs/Ships/SESSION_1_COMPLETE.md` | Параметры ShipController, извлечённые уроки |
| `docs/Ships/SESSION_2_COMPLETE.md` | Система коридоров, турбулентность, UI |
| `docs/Ships/SHIP_MOVEMENT_IMPLEMENTATION_PLAN.md` | Общий план, §Сессия 3 |
| `docs/Ships/NEXT_SESSION_CONTEXT.md` | Контекст для новой сессии |
| `Assets/_Project/Scripts/Player/ShipController.cs` | Текущий код — v2.1 |
| `Assets/_Project/Scripts/Ship/AltitudeCorridorSystem.cs` | Менеджер коридоров |
| `Assets/_Project/Scripts/Ship/TurbulenceEffect.cs` | Текущая турбулентность |
| `Assets/_Project/Scripts/Ship/SystemDegradationEffect.cs` | Деградация систем |
| `Assets/_Project/Scripts/Ship/AltitudeCorridorData.cs` | ScriptableObject коридоров |
| `Assets/_Project/Scripts/UI/AltitudeUI.cs` | HUD высоты |
| `game-studio/QWENCODE.md` | Архитектура агентов game-studio |
| `game-studio/README.md` | Список всех 39 агентов |

---

## ПОШАГОВЫЙ ПЛАН СЕССИИ

### Шаг 1: Оркестрация (10 мин)
```
1. Прочитать docs/Ships/SESSION_2_COMPLETE.md — что готово
2. Прочитать docs/Ships/SHIP_MOVEMENT_IMPLEMENTATION_PLAN.md §Сессия 3 — что нужно
3. Запустить 3 агентов параллельно:
   - @engine-programmer → WindZone + WindZoneData + интеграция
   - @gameplay-programmer → баланс, формулы, windExposure по классам
   - @unity-specialist → Cinemachine, тесты, gizmo
```

### Шаг 2: Создание WindZone системы (30 мин)
```
1. Создать WindZoneData.cs (ScriptableObject)
2. Создать WindZone.cs (MonoBehaviour)
3. Создать Editor утилиту для тестовых зон
4. Коммит: "feat: WindZone system — volume triggers, profiles"
```

### Шаг 3: Интеграция в ShipController (20 мин)
```
1. Добавить поля: windInfluence, windExposure, windDecayTime
2. Добавить список _activeWindZones
3. Переписать ApplyWind() заглушку → реальная логика
4. Добавить RegisterWindZone / UnregisterWindZone методы
5. Применить windExposure в Awake() для каждого класса
6. Коммит: "feat: integrate wind into ShipController"
```

### Шаг 4: Улучшение турбулентности (15 мин)
```
1. Добавить Cinemachine Impulse (с проверкой пакета)
2. Связать turbulence intensity с классом корабля
3. Убедиться что severity работает корректно
4. Коммит: "feat: enhanced turbulence — class-based, Cinemachine"
```

### Шаг 5: Баланс и калибровка (15 мин)
```
1. Настроить windInfluence = 0.5 (дефолт)
2. Настроить windExposure по классам (Light=1.2, Medium=1.0, Heavy=0.7, HeavyII=0.5)
3. Настроить windDecayTime = 1.5s
4. Настроить turbulenceIntensity по классам
5. Пользователь тестирует в Unity → фидбек → итерация
6. Коммит: "balance: wind parameters tuned per ship class"
```

### Шаг 6: Тесты (20 мин)
```
1. Создать WindAndTurbulenceTests.cs (5 тестов)
2. Проверить компиляцию
3. Запустить тесты
4. Коммит: "test: 5 wind & turbulence tests"
```

### Шаг 7: Финальная проверка
```
1. Проверить 0 ошибок компиляции
2. Проверить что кооп-пилотирование работает
3. Проверить что коридоры высот работают
4. Проверить что ветер суммируется векторно
5. Git push
```

---

## ВАЖНЫЕ ПРЕДОСТЕРЕЖЕНИЯ

### ⚠️ НЕ ЛОМАТЬ
- **RPC сигнатуры:** `SubmitShipInputRpc`, `AddPilotRpc`, `RemovePilotRpc` — не менять
- **NetworkObject/NetworkTransform конфигурацию** — не трогать
- **AltitudeCorridorSystem** — уже работает, не менять без причины
- **asmdef файлы** — НЕ СОЗДАВАТЬ (см. SESSION_1_COMPLETE.md каскад ошибок)

### ⚠️ ПРОВЕРЯТЬ
- **Компиляцию в Unity** после каждого изменения — открывать Editor и проверять Console
- **Сетевую совместимость** — кооп-пилотирование должно работать
- **Классы кораблей** — каждый класс должен иметь свой windExposure

### ⚠️ ИЗВЛЕЧЁННЫЕ УРОКИ
1. **НЕ создавать asmdef** без полного анализа зависимостей (SESSION_1: 57 ошибок)
2. **Проверять компиляцию в Unity** перед коммитом
3. **Mass = 1000** для кораблей (не 1kg дефолт)
4. **angularDrag = 8.0** — достаточно для гашения вращения
5. **pitchStabForce = 15, rollStabForce = 20** — стабильность

---

## ОЖИДАЕМЫЙ РЕЗУЛЬТАТ

После Сессии 3:
- ✅ Ветер работает как объёмные зоны (войти → снесло, выйти → затухло)
- ✅ 3 профиля ветра: Constant, Gust, Shear
- ✅ Тяжёлые корабли устойчивее к ветру
- ✅ Турбулентность нарастает плавно при приближении к Завесе
- ✅ Cinemachine Impulse трясёт камеру (если пакет установлен)
- ✅ 5 тестов проходят
- ✅ Сетевая совместимость сохранена
- ✅ **Документ:** `docs/Ships/SESSION_3_COMPLETE.md`

---

*Промпт подготовлен на основе: SESSION_1_COMPLETE.md, SESSION_2_COMPLETE.md, SHIP_MOVEMENT_IMPLEMENTATION_PLAN.md*
*Текущая версия ShipController: v2.1 | Следующая версия после Сессии 3: v2.2*
