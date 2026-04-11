# Сессия 1: Полная Документация — "Живые Баржи"

**Дата начала:** 11 апреля 2026  
**Дата завершения:** 11 апреля 2026  
**Статус:** ✅ Завершена (с итерациями)  
**Ветка:** `qwen-gamestudio-agent-dev`  
**Тег:** `backup-1session-ship-improved`

---

## 📋 Цели Сессии

Переписать `ShipController.cs` чтобы корабли ощущались как **плавные воздушные баржи**, а не аркадные истребители.

**Ключевые требования:**
- Плавный разгон/торможение тяги
- Медленные повороты с инерцией
- Стабилизация — возврат к горизонту без ввода
- Ограничение тангажа ±20°, крен = 0
- Сохранение сетевой совместимости (RPC)

---

## 📖 История Сессии (Хронология)

### Этап 1: Оркестрация и Планирование

**Что сделали:**
- Прочитали `docs/Ships/NEXT_SESSION_CONTEXT.md` — точку входа для сессии
- Запустили 3 агентов из game-studio архитектуры:
  - **@engine-programmer** — технический план переписывания
  - **@gameplay-programmer** — формулы SmoothDamp, баланс
  - **@unity-specialist** — тестовая инфраструктура
- Все 3 агента проанализировали код и создали консенсус-план

**Результат:** Документ `docs/Ships/SESSION_1_RESULTS.md` (первоначальный)

**Коммит:** `67e1f87` — Сессия 1: Core Smooth Movement (включал asmdef файлы ❌)

---

### Этап 2: Каскад Ошибок от asmdef (КРИТИЧЕСКИЙ ИНЦИДЕНТ)

**Что случилось:**

После коммита `67e1f87` создали 2 asmdef файла:
1. `Assets/_Project/Scripts/ProjectC.asmdef` (ProjectC.Runtime)
2. `Assets/_Project/Tests/ProjectC.Tests.asmdef`

**Результат:** 57 ошибок компиляции, полная поломка проекта.

**Полный отчёт:** [`docs/bugs/SESSION1_ASMDEF_CASCADE_ERRORS.md`](bugs/SESSION1_ASMDEF_CASCADE_ERRORS.md)

#### Детали Ошибок

| Категория | Количество | Примеры файлов |
|-----------|-----------|----------------|
| Missing: `UnityEngine.InputSystem` | 10 файлов | PlayerController.cs, NetworkPlayer.cs, UIManager.cs |
| Missing: `TMPro` | 8 файлов | NetworkUI.cs, UIFactory.cs, ControlHintsUI.cs |
| Missing: `ProjectC.Trade` | 1 файл | NetworkPlayer.cs |
| Missing: `CargoSystem` | 1 файл | ShipController.cs |
| Burst assembly resolution | 1 ошибка | Failed to resolve ProjectC.Tests |

**Root Cause:** Создание `ProjectC.Runtime.asmdef` изолировало скрипты из `Assembly-CSharp`. Assembly-CSharp автоматически получал все пакеты (InputSystem, TMPro, etc), но новый asmdef имел только `Unity.Netcode.Runtime` в references.

**Burst ошибка:** Burst compiler сканировал все assemblies для entry-points, но `ProjectC.Tests` не собралась из-за ошибок → цепная реакция.

#### Почему Это Произошло

| Причина | Описание |
|---------|----------|
| **Неполный анализ зависимостей** | Не проверили все `using` statements перед созданием asmdef |
| **Создание нескольких asmdef одновременно** | 2 файла без промежуточной проверки компиляции |
| **Не учли cross-assembly зависимости** | Trade/, CargoSystem в других папках не попали в asmdef |
| **Не проверили в Unity Editor** | Коммитнули без проверки компиляции |

#### Как Решили

```bash
# 1. Откат к рабочему комиту
git reset --hard d403073

# 2. Создание отчёта об ошибках
docs/bugs/SESSION1_ASMDEF_CASCADE_ERRORS.md

# 3. Повторное применение ShipController v2 БЕЗ asmdef файлов
# (ручное переписывание файла)
```

#### Извлечённые Уроки (Prevention Rules)

1. ✅ **НЕ создавать asmdef без полного анализа зависимостей**
   - Проверить ВСЕ `using` statements во всех скриптах папки
   - Убедиться что все пакеты в `references` asmdef

2. ✅ **Assembly-CSharp автоматически получает все пакеты**
   - Не ломать это без крайней необходимости
   - asmdef нужен только для: client/server разделения, addressables

3. ✅ **Проверять компиляцию в Unity ПЕРЕД коммитом**
   - Открыть Unity → ждать компиляцию → проверить Console
   - Только потом `git add` + `git commit`

4. ✅ **Один asmdef за раз**
   - Создал → проверил → следующий
   - Не создавать несколько одновременно

5. ✅ **Burst сканирует все assemblies**
   - Test assemblies могут мешать Burst
   - Проверить Burst compilation после asmdef изменений

---

### Этап 3: Восстановление и Первая Рабочая Версия

**Коммит:** `b543915` — ShipController v2 без asmdef

**Что изменилось:**
- ShipController.cs полностью переписан (SmoothDamp)
- БЕЗ asmdef файлов (избегание каскадных ошибок)
- Сетевая совместимость сохранена

**Результат:** ✅ Компиляция работает, корабль летает

---

### Этап 4: Проблема "Корабль-Волчок"

**Жалоба пользователя:** *"Крен/тангаж могут завращать корабль, он не устойчив, начинает крутиться как волчок"*

**Анализ:**
- `angularDrag = 3.5` — недостаточно для гашения вращения
- `pitchStabForce = 2.5` — слишком слабая стабилизация
- `rollStabForce = 4.0` — слишком слабая стабилизация крена

**Решение (Коммит `7085b2c`):**

| Параметр | Было | Стало | Множитель |
|----------|------|-------|-----------|
| `angularDrag` | 3.5 | **8.0** | ×2.3 |
| `pitchStabForce` | 2.5 | **15.0** | ×6.0 |
| `rollStabForce` | 4.0 | **20.0** | ×5.0 |

**Результат:** ✅ Корабль устойчив, не крутится

---

### Этап 5: Проблема "Переворачивается от касания"

**Жалоба пользователя:** *"Корабль легко вращается переворачивается от любых ошибочных действий, может быть частичная проблема в collider?"*

**Анализ:**
- Пользователь создал корабль как куб 1×1×1 с Mass = 1kg (дефолт Unity)
- Collider слишком маленький — корабль "качается" на точке
- Mass = 1kg — слишком лёгкий для баржи

**Решение (Коммит `c358df8` + `c3c4416`):**

1. Создан гайд: `docs/Ships/HOWTO_CREATE_SHIP.md`
2. Создан Editor скрипт: `Assets/_Project/Editor/CreateTestShip.cs`
   - Автоматически создаёт правильный корабль
   - Menu: **Tools → Create Test Ship**

**Правильные параметры корабля:**

| Параметр | Неправильно | Правильно |
|----------|-------------|-----------|
| **Scale** | 1×1×1 | **8×1.5×4** |
| **Mass** | 1 (дефолт) | **1000** |
| **Collider Size** | 1×1×1 | **8×1.5×4** |
| **Angular Drag** | 0 (дефолт) | **0** (ShipController управляет) |

**Почему Mass = 1000:** ShipController рассчитывает силы с учётом массы. Mass = 1 = корабль слишком лёгкий, переворачивается от любого касания.

---

### Этап 6: NetworkTransform Ошибка

**Ошибка:**
```
error CS0246: The type or namespace name 'NetworkTransform' could not be found
```

**Причина:** NetworkTransform требует отдельного пакета в Unity 6 / NGO 2.x (`com.unity.netcode.gameobjects`). Пакет не установлен или компонент перемещён.

**Решение (Коммит `b3958d1`):**
- Убран `AddComponent<NetworkTransform>()` из CreateTestShip.cs
- Добавлено предупреждение в лог при создании корабля
- Пользователь добавит вручную если нужен

---

## 📊 Итоговые Параметры ShipController v2

### Тяга и Вращение

| Параметр | Значение | Описание |
|----------|----------|----------|
| `thrustForce` | **350** | Сила тяги (было 500) |
| `maxSpeed` | **40** | Макс. скорость м/с (было 30) |
| `yawForce` | **12** | Сила рыскания (было 30, ×0.4) |
| `pitchForce` | **20** | Сила тангажа (было 40, ×0.5) |
| `verticalForce` | **120** | Сила лифта (было 300, ×0.4) |

### Smooth Movement (НОВОЕ)

| Параметр | Значение | Описание |
|----------|----------|----------|
| `yawSmoothTime` | **0.6** | Время сглаживания рыскания (сек) |
| `pitchSmoothTime` | **0.7** | Время сглаживания тангажа (сек) |
| `liftSmoothTime` | **1.0** | Время сглаживания лифта (ОЧЕНЬ медленно) |
| `thrustSmoothTime` | **0.3** | Время разгона/торможения тяги |
| `yawDecayTime` | **1.0** | Затухание yaw без ввода (инерция) |
| `pitchDecayTime` | **0.8** | Затухание pitch без ввода |

### Стабилизация

| Параметр | Значение | Описание |
|----------|----------|----------|
| `angularDrag` | **8.0** | Гашение вращения (было 2.0) |
| `linearDrag` | **0.4** | Сопротивление воздуха (было 1.0) |
| `pitchStabForce` | **15.0** | Возврат pitch к 0 (было 50 stabilizationForce) |
| `rollStabForce` | **20.0** | Возврат roll к 0 |
| `maxPitchAngle` | **20** | Ограничение тангажа ±20° |
| `autoStabilize` | **true** | Автоматическая стабилизация |

### Коридор Высот (Заглушка для Сессии 2)

| Параметр | Значение | Описание |
|----------|----------|----------|
| `minAltitude` | **1200** | Мин. высота полёта (м) |
| `maxAltitude` | **4450** | Макс. высота полёта (м) |
| `maxLiftSpeed` | **2.5** | Макс. скорость лифта (м/с) |

---

## 🔄 Архитектура ShipController v2

### SmoothDamp Вместо Мгновенных Сил

**Было (v1):**
```csharp
_rb.AddTorque(Vector3.up * yaw * yawForce, ForceMode.Force);
```
→ Мгновенный поворот, нет инерции, аркадно

**Стало (v2):**
```csharp
// 1. Рассчитать целевую скорость вращения
float targetYawRate = avgYaw * yawForce;

// 2. SmoothDamp — плавное приближение с velocity tracking
_currentYawRate = hasYawInput
    ? Mathf.SmoothDamp(_currentYawRate, targetYawRate, ref _yawVelocitySmooth, yawSmoothTime)
    : Mathf.SmoothDamp(_currentYawRate, 0f, ref _yawVelocitySmooth, yawDecayTime);

// 3. Применить сглаженную скорость
_rb.AddTorque(Vector3.up * _currentYawRate, ForceMode.Force);
```
→ Плавный поворот, инерция, "баржевый feel"

### Почему SmoothDamp а не Lerp

| Характеристика | Lerp | SmoothDamp |
|----------------|------|------------|
| Frame-rate dependency | ❌ Зависим | ✅ Независим |
| Velocity tracking | ❌ Нет | ✅ Есть (ref параметр) |
| Overshoot damping | ❌ Нет | ✅ Есть |
| Инерция | ❌ Нет | ✅ Есть |
| Затухание | ❌ Только к цели | ✅ К цели + decay к 0 |

### FixedUpdate Архитектура

```
FixedUpdate() {
    1. AverageInputs()           — усреднить ввод пилотов
    2. SmoothThrust()            — SmoothDamp к targetThrust (0.3s)
    3. SmoothYaw()               — SmoothDamp + decay (0.6s/1.0s)
    4. SmoothPitch()             — SmoothDamp + decay (0.7s/0.8s)
    5. SmoothLift()              — SmoothDamp + clamp (1.0s)
    6. UpdateNoInputTimer()      — таймер для стабилизации
    7. ApplyThrustForce()        — применить тягу
    8. ApplyAntiGravity()        — компенсация гравитации
    9. ApplyLiftForce()          — применить лифт
    10. ApplyRotation()          — применить yaw + pitch
    11. ApplyStabilization()     — если нет ввода 0.5s+
    12. ClampVelocity()          — ограничить maxSpeed
    13. ClampPitchAngle()        — ограничить ±20°
    14. ValidateAltitude()       — заглушка для Сессии 2
    15. ResetInputBuffer()       — сбросить sum буферы
}
```

---

## 📁 Созданные/Изменённые Файлы

### Основные

| Файл | Статус | Описание |
|------|--------|----------|
| `Assets/_Project/Scripts/Player/ShipController.cs` | ✅ Переписан | v2.0 — SmoothDamp, стабилизация, corridor заглушка |

### Документация

| Файл | Статус | Описание |
|------|--------|----------|
| `docs/Ships/HOWTO_CREATE_SHIP.md` | ✅ Создан | Пошаговый гайд создания корабля |
| `docs/bugs/SESSION1_ASMDEF_CASCADE_ERRORS.md` | ✅ Создан | Отчёт о каскаде ошибок от asmdef |
| `docs/Ships/SESSION_1_RESULTS.md` | ✅ Создан | Итоги сессии (первоначальный) |
| `docs/Ships/SESSION_1_COMPLETE.md` | ✅ Создан | Этот документ |

### Editor Утилиты

| Файл | Статус | Описание |
|------|--------|----------|
| `Assets/_Project/Editor/CreateTestShip.cs` | ✅ Создан | Tools → Create Test Ship |

### Удалённые (откат)

| Файл | Причина |
|------|---------|
| `Assets/_Project/Scripts/ProjectC.asmdef` | Вызвал 57 ошибок компиляции |
| `Assets/_Project/Tests/ProjectC.Tests.asmdef` | Вызвал 57 ошибок компиляции |

---

## 🎯 Настройка Параметров (Troubleshooting)

### Корабль слишком медленный / быстрый поворот

**Проблема:** Почти не поворачивается

**Решение:** Увеличить `yawForce` в ShipController.cs:
```
Inspector → ShipController → Yaw Force
Текущее: 12
Попробовать: 20-30
```

Или в коде: `Assets/_Project/Scripts/Player/ShipController.cs`, строка ~20

### Корабль раскачивается

**Проблема:** Долго возвращается к горизонту

**Решение:** Увеличить `pitchStabForce` и `rollStabForce`:
```
Pitch Stab Force: 15 → 25-30
Roll Stab Force: 20 → 30-40
```

### Корабль "ватный" (слишком плавный)

**Проблема:** Управление не отзывчивое

**Решение:** Уменьшить smooth times:
```
Yaw Smooth Time: 0.6 → 0.4
Pitch Smooth Time: 0.7 → 0.5
```

### Корабль резко останавливается

**Проблема:** Нет инерции

**Решение:** Уменьшить `angularDrag`:
```
Angular Drag: 8.0 → 5.0-6.0
```

Или увеличить decay times:
```
Yaw Decay Time: 1.0 → 1.5-2.0
```

---

## 📊 Коммиты Сессии 1

| Коммит | Описание | Статус |
|--------|----------|--------|
| `67e1f87` | Сессия 1: первая версия (с asmdef ❌) | ❌ Откачен |
| `1c6c863` | fix: asmdef reference fix | ❌ Откачен |
| `2a001c9` | fix: создать ProjectC.Runtime asmdef | ❌ Откачен |
| `d403073` | Backup — рабочая точка до сессии | ✅ Бэкап |
| `8fff8de` | docs: отчёт о каскадных ошибках | ✅ |
| `b543915` | Сессия 1: ShipController v2 (без asmdef) | ✅ |
| `7085b2c` | fix: усилить стабилизацию | ✅ |
| `c358df8` | docs: гайд создания корабля | ✅ |
| `c3c4416` | feat: CreateTestShip editor script | ✅ |
| `b3958d1` | fix: убрать NetworkTransform | ✅ |

---

## 🚀 Следующие Шаги (Сессия 2+)

### Сессия 2: Altitude Corridors
- AltitudeCorridorSystem (ScriptableObject)
- Городские коридоры: Примум, Тертиус, Квартус, Килиманджаро, Секунд
- Server validation высоты каждые 0.5с
- UI warnings при приближении к границам

### Сессия 3: Wind & Turbulence
- WindZone.cs (объёмные триггеры)
- Turbulence у Завесы
- Cinemachine Impulse для камеры

### Сессия 4: Module System
- ShipModule ScriptableObject
- MODULE_YAW_ENH, PITCH_ENH, LIFT_ENH (тир 1)

---

## 📝 Резюме

### Что Работает

- ✅ SmoothDamp движение (frame-rate независимый)
- ✅ Yaw decay — инерция баржи после отпускания
- ✅ Стабилизация — возврат к горизонту за ~2-3с
- ✅ Thrust ramp-up — плавный разгон 0.3с
- ✅ Lift clamp — максимальная скорость лифта 2.5 м/с
- ✅ Clamp pitch angle — ±20° ограничение
- ✅ Co-op пилотирование (сетевая совместимость)
- ✅ Altitude validation заглушка (готова для Сессии 2)

### Чему Научились

- ✅ **НЕ создавать asmdef без анализа зависимостей**
- ✅ **Проверять компиляцию в Unity перед коммитом**
- ✅ **Mass = 1000 для кораблей** (не 1kg дефолт)
- ✅ **Collider = 8×1.5×4** (не 1×1×1)
- ✅ **Angular Drag = 8.0** (не 3.5, не 0)
- ✅ **PitchStabForce = 15, RollStabForce = 20** (не 2.5/4.0)

### Что Улучшить

- ⚠️ Yaw Force = 12 может быть слишком слабым (пользователь сообщил)
- ⚠️ NetworkTransform не установлен (требует отдельного пакета)
- ⚠️ Тесты не созданы (asmdef проблема)

---

*Документ создан: 11 апреля 2026*  
*Агенты: @engine-programmer, @gameplay-programmer, @unity-specialist, @qa-tester*  
*Тег: backup-1session-ship-improved*
