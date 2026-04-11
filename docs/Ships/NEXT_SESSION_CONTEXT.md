# NEXT SESSION CONTEXT — Ship Movement Overhaul «Живые Баржи»

**Дата создания:** Апрель 2026 | **Ветка:** `qwen-gamestudio-agent-dev`
**Проект:** Project C: The Clouds — MMO/Co-Op авиасимулятор над облаками

---

## 🚀 КРАТКАЯ ИНСТРУКЦИЯ ДЛЯ НОВОЙ СЕССИИ

Ты продолжаешь работу над **Project C** — MMO/Co-Op игрой над облаками по книге «Интеграл Пьявица».

**Контекст:**已完成 оркестрация и дизайн новой системы управления кораблями. Все документы созданы и запушены на GitHub.

**Немедленная задача:** Начать **Сессию 1: Core Smooth Movement** — переписать `ShipController.cs` чтобы корабль ощущался как плавная воздушная баржа, а не аркадный истребитель.

---

## 📂 Ключевые Документы (ЧИТАТЬ В ЭТОМ ПОРЯДКЕ)

### 1. Начни отсюда:
| Документ | Путь | Зачем |
|----------|------|-------|
| **Agent Summary** | `docs/Ships/AGENTS_SHIP_SYSTEM_SUMMARY.md` | Общая картина, роли, план сессий |
| **Implementation Plan** | `docs/Ships/SHIP_MOVEMENT_IMPLEMENTATION_PLAN.md` | Конкретный код, тесты, критерии приёмки |

### 2. Дизайн и спецификации:
| Документ | Путь | Зачем |
|----------|------|-------|
| **GDD_10 v4.0** | `docs/gdd/GDD_10_Ship_System.md` | Полный GDD: физика, модули, коридоры, Co-Op |
| **Ship Registry** | `docs/Ships/ShipRegistry.md` | 10 кораблей, 12 модулей, матрица совместимости |
| **GDD_02 (обновлён)** | `docs/gdd/GDD_02_World_Environment.md` | Мир + секция 6.5 «Altitude Corridors» |

### 3. Лор и контекст:
| Документ | Путь | Зачем |
|----------|------|-------|
| **Ship Lore** | `docs/SHIP_LORE_AND_MECHANICS.md` | Лор кораблей из книги |
| **World Lore Book** | `docs/WORLD_LORE_BOOK.md` | Полный лор мира |
| **MMO Development Plan** | `docs/MMO_Development_Plan.md` | Общий план разработки |

### 4. Текущий код:
| Файл | Путь | Статус |
|------|------|--------|
| **ShipController.cs** | `Assets/_Project/Scripts/Player/ShipController.cs` | 🟡 Нужно переписать |

---

## 🎯 Сессия 1: Core Smooth Movement — ЧТО ДЕЛАТЬ

### Проблема
Текущий ShipController.cs имеет **резкое** управление:
- Yaw (A/D) — мгновенный поворот, нет инерции
- Pitch (мышь Y) — мгновенный наклон
- Lift (Q/E) — рывок по вертикали
- Нет стабилизации — корабль «зависает» в наклоне
- Нет «чувства баржи»

### Решение (подробно в `SHIP_MOVEMENT_IMPLEMENTATION_PLAN.md` §3)

#### Новые поля ShipController
```csharp
// Smooth Movement
float yawSmoothTime = 0.6f;         // Lerp time для yaw
float pitchSmoothTime = 0.7f;       // Lerp time для pitch
float liftSmoothTime = 1.0f;        // Lerp time для lift (ОЧЕНЬ медленно)
float thrustSmoothTime = 0.3f;      // Ramp-up тяги
float yawDecayTime = 1.0f;          // Затухание yaw без ввода
float pitchDecayTime = 0.8f;        // Затухание pitch без ввода

// Stabilization
float pitchStabForce = 2.5f;        // Сила возврата pitch к 0
float rollStabForce = 4.0f;         // Сила возврата roll к 0
float maxPitchAngle = 20f;          // ° — ограничение тангажа
float maxRollAngle = 0f;            // ° (0 = заблокирован)

// Altitude
float minAltitude = 1200f;          // Глобальный минимум
float maxAltitude = 4450f;          // Глобальный максимум
float maxLiftSpeed = 2.5f;          // м/с — макс. скорость лифта
```

#### Новые state-переменные (текущие сглаженные значения)
```csharp
float _currentYawRate;
float _currentPitchRate;
float _currentLiftForce;
float _currentThrust;
float _currentRoll;
```

#### Новый FixedUpdate (псевдокод)
```
1. Усреднить ввод от пилотов (AverageInputs)
2. Smooth thrust: Lerp к целевой тяге (0.3s ramp-up)
3. Smooth yaw: Lerp к targetYaw, decay к 0 без ввода
4. Smooth pitch: Lerp к targetPitch, decay к 0, clamp ±20°
5. Smooth lift: Lerp к targetLift, clamp ±maxLiftSpeed
6. Smooth roll: Lerp к 0 (заблокирован)
7. Применить силы: thrust, lift, rotation
8. Stabilization: если нет ввода — возврат к горизонту
9. Ветер: ApplyWind() (заглушка для Сессии 3)
10. Турбулентность: если близко к Завесе (заглушка)
11. ClampVelocity: ограничить maxSpeed
12. ValidateAltitude: серверная проверка высоты
```

#### Изменение параметров (текущие → целевые для LIGHT класса)
| Параметр | Сейчас | Цель | Изменение |
|----------|--------|------|-----------|
| thrustForce | 500 | 350 | ×0.7 |
| yawForce | 30 | 12 (×0.4) | РЕЗКО медленнее |
| pitchForce | 40 | 20 (×0.5) | Значительно медленнее |
| verticalForce | 300 | 120 | ×0.4 |
| linearDrag | 1.0 | 0.4 | Меньше |
| angularDrag | 2.0 | 3.5 (×1.75) | Больше — гасит вращение |

### Тесты (подробно в Implementation Plan §4)
7 Unity тестов в `ShipMovementTests.cs`:
1. SmoothThrust_RampsUpOverTime
2. SmoothYaw_SlowTurnWithInertia
3. YawDecay_ContinuesAfterInputReleased
4. Stabilization_ReturnsToLevel
5. SlowLift_VeryGentleAltitudeChange
6. AltitudeValidation_BelowMin_Turbulence
7. AutoHover_NoPilots_ShipHovers

### Критерии приёмки (подробно в Implementation Plan §6)
- [ ] Yaw плавный: < 40°/s, Lerp 0.6s
- [ ] Pitch плавный: < 30°/s, Lerp 0.7s, clamp ±20°
- [ ] Lift медленный: < 2.5 м/с, Lerp 1.0s
- [ ] Thrust ramp-up: 0.3s до полной тяги
- [ ] Stabilization: возврат к 0° за < 3с
- [ ] Yaw decay: затухание за ~1с без ввода
- [ ] Angular drag: вращение гасится
- [ ] Нет резких стопов: инерция сохраняется
- [ ] AutoHover: корабль зависает без пилотов

---

## 📋 Полный План Сессий

| # | Сессия | Фокус | Статус |
|---|--------|-------|--------|
| **1** | Core Smooth Movement | Переписать ShipController.cs | 🔴 НЕ начата |
| **2** | Altitude Corridors | Коридоры, серверная валидация | 🔴 НЕ начата |
| **3** | Wind & Turbulence | Ветер, тряска у Завесы | 🔴 НЕ начата |
| **4** | Module System | ShipModule SO, тир 1 модули | 🔴 НЕ начата |
| **5** | Meziy Thrust | Burst maneuvers | 🔴 НЕ начата |
| **6** | Co-Op + KeyRod | Адаптивный Co-Op, ключи | 🔴 НЕ начата |
| **7** | Docking | Диспетчер, CommPanel | 🔴 НЕ начата |

---

## 🏗️ Архитектура Сетевого Кода (важно для сохранения совместимости)

### Текущий RPC (НЕ ЛОМАТЬ)
```csharp
// Клиент → Сервер
[Rpc(SendTo.Server)]
private void SubmitShipInputRpc(float thrust, float yaw, float pitch, float vertical, bool boost, RpcParams rpcParams = default)

// Сервер → Клиент
[Rpc(SendTo.Everyone)]
private void AddPilotRpc(ulong clientId, RpcParams rpcParams = default)

[Rpc(SendTo.Everyone)]
private void RemovePilotRpc(ulong clientId, RpcParams rpcParams = default)
```

### Серверная логика (FixedUpdate, IsServer)
```
1. Accumulate inputs от всех пилотов в _sum* буферы
2. Усреднить: avg = sum / count
3. Применить физику с smooth Lerp
4. NetworkTransform(ServerAuthority) реплицирует всем
```

### Что можно менять:
- Внутреннюю логику FixedUpdate (Lerp, stabilization, drag)
- Значения параметров (SerializeField)
- Добавлять новые private методы

### Что НЕЛЬЗЯ менять (без причины):
- Сигнатуру SubmitShipInputRpc (без обновления клиента)
- NetworkObject/NetworkTransform конфигурацию
- AddPilotRpc/RemovePilotRpc логику

---

## 🎮 Управление (текущее — сохранить mapping)

| Клавиша | Действие | Описание |
|---------|----------|----------|
| **W/S** | Тяга вперёд/назад | Forward/back thrust |
| **A/D** | Рыскание (Yaw) | Курсовой поворот влево/вправо |
| **Q/E** | Лифт вниз/вверх | Vertical movement |
| **Мышь Y** | Тангаж (Pitch) | Нос вверх/вниз |
| **Left Shift** | Буст | ×2 тяга |
| **F** | Посадка/выход | Enter/exit ship |

---

## 🌍 Городы и Коридоры Высот (для Сессии 2+)

| Город | Высота | Min | Max |
|-------|--------|-----|-----|
| Примум | 4 348 м | 4 100 м | 4 450 м |
| Тертиус | 2 462 м | 2 300 м | 2 600 м |
| Квартус | 1 690 м | 1 500 м | 1 850 м |
| Килиманджаро | 1 395 м | 1 200 м | 1 550 м |
| Секунд | 1 142 м | 1 000 м | 1 250 м |

Глобальный коридор: **1200м — 4450м**

---

## ⚠️ Важные Принципы

1. **Корабли = баржи, НЕ истребители.** Плавность > отзывчивость.
2. **Сохраняй сетевую совместимость.** Не ломай RPC без веской причины.
3. **Тестируй в Unity.** Каждый коммит = проверяй в редакторе.
4. **Пользователь тестирует сам.** Он запускает Unity и проверяет «feel».
5. **Коммить часто.** Маленькие шаги → фидбек → итерация.

---

## 🔧 Быстрый Старт (Пошагово)

```
1. Прочитать: docs/Ships/AGENTS_SHIP_SYSTEM_SUMMARY.md (10 мин)
2. Прочитать: docs/Ships/SHIP_MOVEMENT_IMPLEMENTATION_PLAN.md §3 (15 мин)
3. Открыть: Assets/_Project/Scripts/Player/ShipController.cs
4. Добавить новые поля (smooth times, state variables)
5. Переписать FixedUpdate с Lerp логикой
6. Настроить параметры (yawForce ×0.4, angularDrag ×1.75 и т.д.)
7. Добавить ApplyStabilization() метод
8. Сохранить → Пользователь тестирует в Unity → фидбек
9. Итерировать параметры по фидбеку
10. Создать ShipMovementTests.cs (7 тестов)
11. Коммит → пуш
```

---

## 📊 Статус Репо

- **Ветка:** `qwen-gamestudio-agent-dev`
- **Последний коммит:** Ship docs reorganized (`docs/Ships/`)
- **Upstream:** GitHub `boozzeeboom/project-c`
- **Команда пуша:** `git push upstream qwen-gamestudio-agent-dev`

---

*Этот документ — ЕДИНАЯ ТОЧКА ВХОДА для новой сессии. Все ссылки актуальны, все пути проверены.*
