# NEXT SESSION CONTEXT — Ship Movement Overhaul «Живые Баржи»

**Дата создания:** Апрель 2026 | **Ветка:** `qwen-gamestudio-agent-dev`
**Проект:** Project C: The Clouds — MMO/Co-Op авиасимулятор над облаками

---

## 🚀 КРАТКАЯ ИНСТРУКЦИЯ ДЛЯ НОВОЙ СЕССИИ

Ты продолжаешь работу над **Project C** — MMO/Co-Op игрой над облаками по книге «Интеграл Пьявица».

**Контекст:** Сессии 1-3 завершены ✅. ShipController v2.2 работает с smooth movement, altitude corridors, wind zones.

**Немедленная задача:** Начать **Сессию 4: Module System Foundation** — создать систему модулей для кораблей.

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
| **ShipController.cs** | `Assets/_Project/Scripts/Player/ShipController.cs` | ✅ v2.2 — Сессии 1-3 завершены |
| **WindZone.cs** | `Assets/_Project/Scripts/Ship/WindZone.cs` | ✅ Сессия 3 |
| **WindZoneData.cs** | `Assets/_Project/Scripts/Ship/WindZoneData.cs` | ✅ Сессия 3 |
| **TurbulenceEffect.cs** | `Assets/_Project/Scripts/Ship/TurbulenceEffect.cs` | ✅ Улучшена в Сессии 3 |
| **AltitudeCorridorSystem.cs** | `Assets/_Project/Scripts/Ship/AltitudeCorridorSystem.cs` | ✅ Сессия 2 |

---

## 🎯 Сессия 4: Module System Foundation — ЧТО ДЕЛАТЬ

### Проблема
Корабли имеют фиксированные характеристики. Нет системы кастомизации через модули.
В ShipRegistry.md описаны 12 модулей (YAW_ENH, PITCH_ENH, MEZIY_* и т.д.) но они не реализованы.

### Решение (подробно в `SHIP_MOVEMENT_IMPLEMENTATION_PLAN.md` §Сессия 4)

#### Новые файлы
```
ShipModule.cs (ScriptableObject) — определение модуля
ModuleSlot.cs (MonoBehaviour) — слот для модуля на корабле
ShipModuleManager.cs (MonoBehaviour) — менеджер модулей
```

#### ShipModule поля
```csharp
string moduleId;              // MODULE_YAW_ENH
string displayName;           // "Улучшенное Рыскание"
ModuleType type;              // Propulsion, Utility, Special
int tier;                     // 1-4
float yawMultiplier = 1f;     // 1.4 для YAW_ENH
float pitchMultiplier = 1f;   // 1.3 для PITCH_ENH
float liftMultiplier = 1f;    // 1.5 для LIFT_ENH
float thrustMultiplier = 1f;
int powerConsumption = 0;     // 5-8 для тир 1
List<ShipFlightClass> compatibleClasses;
List<string> incompatibleModules;
```

#### ShipController изменения
```csharp
// Добавить поля:
[SerializeField] private ShipModuleManager moduleManager;
private float _moduleThrustMult = 1f;
private float _moduleYawMult = 1f;
private float _modulePitchMult = 1f;
private float _moduleLiftMult = 1f;

// В FixedUpdate():
ApplyModuleModifiers();  // после AverageInputs

// Применять в ApplyThrustForce, ApplyRotation и т.д.:
_rb.AddForce(transform.forward * currentThrust * _moduleThrustMult, ForceMode.Force);
```

### Тесты (подробно в Implementation Plan §Сессия 4)
3 Unity теста в `ModuleSystemTests.cs`:
1. InstallModule_AppliesEffects
2. IncompatibleModule_BlocksInstallation
3. RemoveModule_EffectsReturnToBase

### Критерии приёмки
- [ ] ShipModule ScriptableObject создан
- [ ] ModuleSlot показывает occupied/unoccupied
- [ ] ShipModuleManager управляет слотами
- [ ] YAW_ENH модуль ускоряет поворот на 40%
- [ ] Несовместимые модули блокируются
- [ ] Снятие модуля возвращает базовые эффекты
- [ ] Энергия корабля ограничивает установку модулей
- [ ] 3 теста проходят
- [ ] Сетевая совместимость сохранена

---

## 📋 Полный План Сессий

| # | Сессия | Фокус | Статус |
|---|--------|-------|--------|
| **1** | Core Smooth Movement | Переписать ShipController.cs | ✅ ЗАВЕРШЕНА |
| **2** | Altitude Corridors | Коридоры, серверная валидация | ✅ ЗАВЕРШЕНА |
| **3** | Wind & Turbulence | Ветер, тряска у Завесы | ✅ ЗАВЕРШЕНА |
| **4** | Module System | ShipModule SO, тир 1 модули | 🔴 СЛЕДУЮЩАЯ |
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
6. **Конфликт имён с UnityEngine.** Использовать полные имена: `ProjectC.Ship.WindZone` вместо `WindZone`

---

## 🔧 Быстрый Старт (Пошагово)

```
1. Прочитать: docs/Ships/SESSION_3_COMPLETE.md (10 мин) — что готово
2. Прочитать: docs/Ships/SHIP_MOVEMENT_IMPLEMENTATION_PLAN.md §Сессия 4 (15 мин)
3. Прочитать: docs/Ships/ShipRegistry.md §3 — каталог модулей
4. Открыть: Assets/_Project/Scripts/Player/ShipController.cs
5. Создать: ShipModule.cs, ModuleSlot.cs, ShipModuleManager.cs
6. Интегрировать модули в ShipController (ApplyModuleModifiers)
7. Сохранить → Пользователь тестирует в Unity → фидбек
8. Итерировать параметры по фидбеку
9. Создать ModuleSystemTests.cs (3 теста)
10. Коммит → пуш
```

---

## 📊 Статус Репо

- **Ветка:** `qwen-gamestudio-agent-dev`
- **Сессии:** 1-3 ✅ ЗАВЕРШЕНЫ, Сессия 4 🔴 СЛЕДУЮЩАЯ
- **ShipController:** v2.2 (smooth movement + altitude corridors + wind)
- **Последний коммит:** Session 3 — Wind & Environmental Forces ✅
- **Upstream:** GitHub `boozzeeboom/project-c`
- **Команда пуша:** `git push upstream qwen-gamestudio-agent-dev`

---

*Этот документ — ЕДИНАЯ ТОЧКА ВХОДА для новой сессии. Все ссылки актуальны, все пути проверены.*
