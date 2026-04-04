# Qwen Code Context — Project C

**Ветка Git:** `qwen-dev`
**Последний коммит:** `ddaad78`
**Версия:** `v0.0.7-chest-system`

---

## 🏷️ Схема версионирования

| Версия | Этап | Описание |
|--------|------|----------|
| `0.0.x.x` | Ранняя преальфа | Текущий этап — базовые механики, прототип |
| `0.0.7` | **v0.0.7-chest-system** | ✅ Сундуки + LootTable (4 апр 2026) |
| `0.0.6` | v0.0.6-inventory | ✅ Система подбора + круговой инвентарь (4 апр 2026) |
| `0.1.x` | Преальфа | Играбельная версия — полноценная игра |
| `0.2.x` | Альфа | Сетевой мультиплеер |
| `0.3.x` | Бета | Контент, баланс, полировка |
| `1.0.0` | Релиз | Публичный релиз |

---

## 🚀 Быстрый старт

### Команда для перезапуска Qwen с правильным контекстом:

```
Продолжи работу над Project C. Прочитай файлы docs/QWEN_CONTEXT.md и docs/MMO_Development_Plan.md
```

### Если что-то сломалось — откатиться:

```bash
git fetch upstream
git reset --hard upstream/qwen-dev
```

### Создать резервную версию перед экспериментами:

```bash
git tag backup/$(date +%Y-%m-%d)
git checkout -b test/new-feature
```

---

## 📜 Принцип разработки

> **Медленнее = Быстрее**

**Правило:** Вносить изменения **маленькими шагами**, тестировать после каждого шага, коммитить только работающее.

**Журнал шагов:** [`STEP_BY_STEP_DEVELOPMENT.md`](STEP_BY_STEP_DEVELOPMENT.md) — **читать перед работой!**

---

## ⚠️ КРИТИЧНО: НЕ ТРОГАТЬ `.meta` ФАЙЛЫ

> **Никогда не создавать и не редактировать `.meta` файлы вручную!**

Unity автоматически создаёт `.meta` файлы для каждого ассета. Ручное создание/редактирование ломает ссылки, вызывает `TypeLoadException` и ошибки компиляции.

**Правило:**
- ✅ Unity создаёт `.meta` автоматически
- ✅ Не создавать, не удалять, не редактировать `.meta`
- ✅ Если случайно создал — удалить и дать Unity пересоздать

---

## 📁 Структура проекта

```
ProjectC_client/          (Unity 6 клиент)
├── Assets/
│   ├── _Project/
│   │   ├── Scripts/
│   │   │   ├── Core/          (WorldGenerator, CloudSystem, Inventory, LootTable, ChestContainer)
│   │   │   ├── Player/        (PlayerController, ShipController, PlayerStateMachine, ItemPickupSystem)
│   │   │   ├── UI/            (InventoryUI, ControlHintsUI, PeakNavigationUI, NetworkUI)
│   │   │   └── Network/       (NetworkManagerController, NetworkPlayer)
│   │   └── Items/             (ItemData и LootTable ScriptableObject ассеты)
│   └── ...
├── docs/                      (вся документация)
├── ProjectSettings/
└── Packages/
```

---

## ✅ Что уже сделано

### 🌍 Мир и генерация
- ✅ `WorldGenerator.cs` — процедурная генерация горных пиков (15 пиков)
- ✅ `CloudSystem.cs`, `CloudLayer.cs`, `CloudLayerConfig.cs` — 3 слоя облаков, 890+ облаков
- ✅ `TestPlatformCreator.cs` — тестовая платформа с коллайдером

### 📷 Камера
- ✅ `WorldCamera.cs` — свободный полёт, телепортация к пикам (N/B/R/H/V)
- ✅ `ThirdPersonCamera.cs` — орбитальная камера от третьего лица (адаптируется к режиму)

### 🎮 Управление
- ✅ `PlayerController.cs` — пеший режим (WASD + Space прыжок + Shift бег)
- ✅ `ShipController.cs` — корабль (Rigidbody + антигравитация, W/S тяга, A/D рыскание, Q/E лифт, мышь тангаж)
- ✅ `PlayerStateMachine.cs` — переключение пешком ↔ корабль (F)

### 📦 Инвентарь и предметы
- ✅ `ItemType.cs` — 8 типов предметов + `ItemData` ScriptableObject
- ✅ `Inventory.cs` — singleton-менеджер, группировка по типам, `AddMultipleItems()`
- ✅ `PickupItem.cs` — подбираемый объект в мире (триггер, покачивание)
- ✅ `ItemPickupSystem.cs` — клавиша E: поиск ближайшего, приоритет сундуку
- ✅ `InventoryUI.cs` — круговое колесо (GL, 8 секторов, hover, подсписки, вспышка)
- ✅ `LootTable.cs` — ScriptableObject таблицы добычи (шансы, min/max, guaranteed)
- ✅ `ChestContainer.cs` — компонент сундука (анимация открытия, LootTable, автоуничтожение)
- ✅ `ControlHintsUI.cs` — подсказки E, Tab, F1

### 🌐 Сеть (базовая)
- ✅ **Netcode for GameObjects** добавлен
- ✅ `NetworkManagerController` — управление подключениями
- ✅ `NetworkPlayer` — синхронизация игрока
- ✅ `NetworkUI` — UI подключения

### 📚 Документация
- ✅ `MMO_Development_Plan.md` — полный план разработки (обновлён, галочки проставлены)
- ✅ `STEP_BY_STEP_DEVELOPMENT.md` — пошаговая разработка + журнал
- ✅ `INVENTORY_SYSTEM.md` — система инвентаря и сундуков
- ✅ `CONTROLS.md` — карта всех клавиш
- ✅ `WORLD_LORE_BOOK.md`, `WORLD_LORE_GRAVITY.md` — лор мира
- ✅ `SHIP_LORE_AND_MECHANICS.md`, `SHIP_SYSTEM_DOCUMENTATION.md` — корабли
- ✅ `INDEX.md` — каталог документации
- ✅ Git workflow: `GIT_WORKFLOW.md`, `GIT_WORKFLOW_ADVANCED.md`, `QUICK_GIT_COMMANDS.md`

---

## 🎮 Управление (кратко)

| Клавиша | Действие | Режим |
|---------|----------|-------|
| WASD | Движение | Пеший + корабль |
| Space | Прыжок | Пеший |
| Shift | Бег / ускорение | Пеший + корабль |
| F | Сесть/выйти из корабля | Оба |
| E | Подобрать предмет / открыть сундук | Пеший |
| Tab | Круговой инвентарь | Пеший |
| Q/E | Лифт вверх/вниз | Корабль |
| Мышь | Камера / тангаж | Оба |
| F1 | Подсказки UI | Оба |

---

## 📋 Текущий статус (из MMO_Development_Plan.md)

### Этап 0: Подготовка окружения ✅ ЗАВЕРШЁН
### Этап 1: Прототип ядра геймплея 🔄 В ПРОЦЕССЕ

**Выполнено:**
- ✅ Мир: генерация пиков + облака
- ✅ Камеры: WorldCamera + ThirdPersonCamera
- ✅ Контроллеры: пеший + корабль + переключение F
- ✅ Инвентарь: подбор, круговое колесо, сундуки, LootTable
- ✅ UI: подсказки, навигация, инвентарь
- ✅ Сеть (базовая): NGO инфраструктура

**Осталось в Этапе 1:**
- [ ] Доработка физики корабля (banking, инерция, плавность)
- [ ] Система топлива (мезий)
- [ ] 3D/2D иконки в секторах инвентаря
- [ ] Слот 9 (центр) для ключевого предмета
- [ ] «Облачный» дизайн колеса (Ghibli)

---

## 📦 Архитектура инвентаря

### Круговой инвентарь (GTA-стиль)
- 8 секторов, закреплённых за типами (Тип 1 → сектор 1)
- Безлимитный, автоматическая раскладка по типам
- Группировка: несколько одного типа → подсписок при наведении
- E подбирает, Tab открывает колесо

### Сундуки (LootTable)
- LootTable — ScriptableObject: entries (шанс, min/max) + guaranteed items
- ChestContainer — компонент сундука: анимация, автоуничтожение
- E рядом с сундуком → все предметы в инвентарь + вспышка секторов
- Приоритет: сундук > обычный предмет

---

## 🔗 Git Remote

```
origin    https://github.com/boozzeeboom/project-c-2026-04-02_14-41-56.git
upstream  https://github.com/boozzeeboom/project-c.git
```

**Ветка:** `qwen-dev`

---

## 📋 Сессия: 4 апреля 2026 г.

### Утро: Система инвентаря (v0.0.6)
| Коммит | Описание |
|--------|----------|
| `524083e` | Базовая система подбора + инвентарь |
| `49121e5` | Hover + подсписок исправлены |
| `1bcb696` | Подсказка Tab в UI |
| `643e549` | Документация + план доработок |

### Вечер: Система сундуков (v0.0.7)
| Коммит | Описание |
|--------|----------|
| `ddaad78` | Система сундуков: LootTable, ChestContainer, AddMultipleItems, приоритет, вспышка |

**Созданные файлы (вечер):**
- `Assets/_Project/Scripts/Core/LootTable.cs`
- `Assets/_Project/Scripts/Core/ChestContainer.cs`

**Изменённые файлы (вечер):**
- `Inventory.cs` → `AddMultipleItems()`
- `ItemPickupSystem.cs` → `FindNearestInteractable()`, приоритет сундуку
- `InventoryUI.cs` → `TriggerSectorFlash()`
- `ControlHintsUI.cs` → подсказка E обновлена
- `docs/INVENTORY_SYSTEM.md`, `STEP_BY_STEP_DEVELOPMENT.md`, `MMO_Development_Plan.md`, `QWEN_CONTEXT.md`

---

**Важно:** После каждого сеанса обновляй этот файл с текущим состоянием!
