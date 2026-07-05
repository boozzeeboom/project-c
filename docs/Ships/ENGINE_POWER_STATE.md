# Ship Engine Power State — Дизайн и Реализация

**Дата:** 2026-07-08
**Статус:** ✅ Реализовано
**Задача:** Добавить состояние «двигатель включён/выключен» с топливной логикой

---

## 1. Краткое описание

У корабля появился новый бинарный стейт: **ENGINE ON / ENGINE OFF**. Игрок управляет им через клавишу **Enter** (настраивается через `InputBindingsConfig.GameAction.ShipToggleEngine`).

### Ключевое поведение:

| Состояние | Что происходит |
|-----------|---------------|
| **ENGINE OFF** | AntiGravity НЕ работает → корабль падает под гравитацией. Ввод (thrust/yaw/pitch) игнорируется, даже если пилот в кресле. |
| **ENGINE ON** | AntiGravity работает → корабль висит. Полный силовой конвейер, расход топлива. |
| **ENGINE ON, пилотов нет** | IDLE-режим: antiGravity работает, idle-расход топлива, ветер применяется. Корабль «завис» и ждёт. |
| **NPC-корабль** | Всегда ENGINE ON. Игнорирует новую механику. При возврате управления от игрока — принудительно включает двигатель. |

---

## 2. Пользовательский flow

### 2.1 Посадка (F)
- Подошёл к креслу пилота → **F** → персонаж сел
- Если корабль выключен: лететь нельзя, HUD показывает `ENGINE OFF` (красный)
- Если корабль включён: можно сразу лететь

### 2.2 Запуск двигателя (Enter)
- Только когда игрок в кресле пилота (`_inShip == true`)
- Нажал **Enter** → сервер проверяет топливо (`startEngineConsumption = 10% от maxFuel`)
- Если топлива хватает → `ENGINE ON`, HUD показывает зелёный `ENGINE ON`
- Если топлива недостаточно → двигатель НЕ включается
- При `IsDocked == true`: двигатель включается, но управление не работает до ручной отстыковки через **T → CommPanel → Отстыковка**

### 2.3 Остановка двигателя (Enter)
- В воздухе или на земле — нажал **Enter** → `ENGINE OFF`
- Корабль немедленно начинает падать (antiGravity отключается)
- Можно выключить даже с пилотом внутри — падают вместе

### 2.4 Выход (F)
- Выход разрешён **всегда**, на любой скорости
- Двигатель остаётся в текущем состоянии:
  - Был ON → корабль зависает (IDLE), тратит топливо
  - Был OFF → корабль падает
- Игрок может оставить корабль «зависшим в воздухе»

### 2.5 Топливо и авто-выключение
- При `fuel == 0`: двигатель **автоматически** выключается
- Включённый двигатель тратит топливо всегда:
  - С вводом (thrust/yaw/pitch/vertical): стандартный расход через `ConsumeFuelPerSecond`
  - Без ввода (IDLE): `idleConsumptionRate = 0.05 fuel/s`
- Оставленный включённым корабль со временем потратит топливо, выключится и упадёт

---

## 3. Диаграмма состояний

```
                    ┌──────────────────────────────────────┐
                    │           ENGINE OFF                 │
                    │  (корабль падает / стоит на земле)   │
                    └──────┬───────────────┬───────────────┘
                           │ F (сесть)     │ Enter (включить)
                           ▼               ▼
              ┌──────────────────┐  ┌──────────────────┐
              │ ENGINE OFF       │  │ ENGINE ON        │
              │ + ПИЛОТ в кресле │  │ + ПИЛОТ в кресле │
              │ (лететь нельзя)  │  │ (полный полёт)   │
              └────────┬─────────┘  └────────┬─────────┘
                       │ Enter              │ F (выйти)
                       │ (если есть топливо)│
                       ▼                    ▼
              ┌──────────────────┐  ┌──────────────────┐
              │ ENGINE ON        │  │ ENGINE ON        │
              │ + ПИЛОТ в кресле │  │ IDLE (завис)     │
              └──────────────────┘  │ пилотов нет      │
                                    └────────┬─────────┘
                                             │ топливо = 0
                                             ▼
                                    ┌──────────────────┐
                                    │ ENGINE OFF       │
                                    │ (падает)         │
                                    └──────────────────┘
```

---

## 4. Задействованные файлы

| Файл | Что изменено |
|------|-------------|
| `Assets/_Project/Scripts/Ship/ShipFuelSystem.cs` | + `startEngineConsumption` (0.10), `idleConsumptionRate` (0.05), геттеры |
| `Assets/_Project/Scripts/Player/ShipController.cs` | + `_engineRunning`, `_netEngineRunning` NetworkVariable, `SetEngineRunning()`, `ToggleEngineServerRpc()`, новый FixedUpdate gate, `ApplyAntiGravity` guard, IDLE-расход топлива, авто-выключение при пустом топливе, убран `enabled=false` из `RemovePilotRpc`, guard в `ApplyServerInput` |
| `Assets/_Project/Scripts/Input/InputBindingsConfig.cs` | + `ShipToggleEngine` в `GameAction` enum (индекс 22, конец), + бинд Enter |
| `Assets/_Project/Scripts/Player/PlayerInputReader.cs` | + `OnShipToggleEnginePressed` event, детект `enterKey.wasPressedThisFrame` |
| `Assets/_Project/Scripts/Player/NetworkPlayer.cs` | + обработка Enter → `ToggleEngineServerRpc()`, убран speed-check при выходе (F) |
| `Assets/_Project/Scripts/PeacefulShip/Stations/NpcShipController.cs` | + `SetEngineRunning(true)` при спавне и при возврате управления от игрока |
| `Assets/_Project/Scripts/Ship/UI/ShipHudController.cs` | + `ENGINE ON/OFF` индикатор в колонке K3 (под топливом) |
| `Assets/_Project/Resources/InputBindingsConfig.asset` | + `action: 22, key: 2` (Enter) в YAML |

---

## 5. Сетевая синхронизация

```csharp
private readonly NetworkVariable<bool> _netEngineRunning = new NetworkVariable<bool>(
    false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
```

- Сервер устанавливает значение → реплицируется всем клиентам
- HUD читает `ship.IsEngineRunning` → автоматически получает актуальное значение
- NPC-корабли всегда `IsEngineRunning == true` для клиентов

---

## 6. Инспектор — новые поля

### ShipFuelSystem
| Поле | Дефолт | Описание |
|------|--------|----------|
| `Start Engine Consumption` | 0.10 | Доля от maxFuel при запуске (10%) |
| `Idle Consumption Rate` | 0.05 | fuel/s на холостом ходу |

### InputBindingsConfig
| Действие | Клавиша | Категория |
|----------|---------|-----------|
| `ShipToggleEngine` | Enter | Ship |

---

## 7. NPC — исключение из правил

- NPC-корабли всегда `ENGINE ON` (устанавливается в `NpcShipController.OnNetworkSpawn`)
- Если игрок сел в NPC-корабль и выключил двигатель, а потом вышел — NPC при возврате управления принудительно включает двигатель
- NavTick не затрагивается — NPC управляет Rigidbody напрямую

---

## 8. Что НЕ сломано

- Существующая посадка/выход (F) — работает как прежде, только без speed-check
- NPC-курсирование — `SetEngineRunning(true)` при спавне и возврате
- Стыковка/отстыковка — без изменений
- Топливная система — только добавлены поля, существующая логика не тронута
- CommPanel (T) — без изменений
- HUD — только добавлен индикатор, остальные колонки не тронуты
