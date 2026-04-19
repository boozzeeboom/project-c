# 🎯 MASTER PROMPT: Iteration 4 — Chests, Items, NPCs

**Версия:** 1.0  
**Дата:** 19.04.2026  
**Статус:** АКТИВНАЯ ИТЕРАЦИЯ

---

## 🎯 ЦЕЛЬ ИТЕРАЦИИ 4

Реализация интерактивных объектов мира:
1. **Сундуки (Chests)** — взаимодействие, открытие, лут
2. **Предметы (Items)** — базовая система предметов
3. **NPC** — неигровые персонажи

**ВНИМАНИЕ:** Телепорты и F5-F10 НЕ входят в эту итерацию. Это задача для других итераций.

---

## 📋 ТЕКУЩЕЕ СОСТОЯНИЕ

### Из Iteration 3 (завершено):
- ✅ FloatingOriginMP исправлен (ApplyWorldShift + телепортация)
- ✅ WorldStreamingManager использует позицию игрока, а не камеры
- ✅ Chunk oscillation исправлен

### Для Iteration 4:
- ❌ Сундуки не реализованы
- ❌ Система предметов не реализована
- ❌ NPC не реализованы

---

## 🏗️ АРХИТЕКТУРА СИСТЕМЫ

### СУЩЕСТВУЮЩИЕ СИСТЕМЫ (АДАПТИРОВАНЫ)

#### Item System (уже реализована)
```
Файлы:
- Core/ItemType.cs — ItemType enum (8 типов), ItemData ScriptableObject
- Core/NetworkInventory.cs — сетевая синхронизация через NetworkVariable
- Core/Inventory.cs — локальный инвентарь (без сети)

ItemType enum (8 типов):
- Resources (0)
- Equipment (1)
- Food (2)
- Fuel (3)
- Antigrav (4)
- Meziy (5)
- Medical (6)
- Tech (7)

ItemData структура:
- itemName, itemType, description, icon
- НУЖНО: добавить network sync ID для передачи по сети
```

#### Chest System (НУЖНА АДАПТАЦИЯ)
```
Файлы:
- Core/ChestContainer.cs — НЕ ИМЕЕТ NetworkObject!
- Core/LootTable.cs — ScriptableObject с таблицей добычи

Проблема:
- ChestContainer не имеет NetworkObject
- Нет ServerRpc/ClientRpc для синхронизации
- LootTable.GenerateLoot() не передаётся по сети

Решение: создать NetworkChestContainer
```

#### Interaction System (нужна оптимизация)
```
Файлы:
- Core/IInteractable.cs — базовый интерфейс
- Core/InteractableManager.cs — статический менеджер (НЕ ИСПОЛЬЗУЕТСЯ!)
- Player/ItemPickupSystem.cs — использует FindObjectsByType вместо InteractableManager

Проблема:
- ItemPickupSystem.FindNearestInteractable() использует FindObjectsByType
- InteractableManager.FindNearestChest() существует, но не используется

Решение: адаптировать ItemPickupSystem использовать InteractableManager
```

---

### ПЛАН АДАПТАЦИИ СУЩЕСТВУЮЩИХ СИСТЕМ

#### 1. NetworkChestContainer (НОВЫЙ ФАЙЛ)
```
Создать: Assets/_Project/Scripts/World/Chest/NetworkChestContainer.cs

Структура:
- Наследует NetworkBehaviour
- NetworkVariable<bool> _isOpen — синхронизация состояния
- ServerRpc: RequestOpenChestServerRpc — запрос на открытие (с валидацией)
- ClientRpc: OpenChestClientRpc — анимация открытия у всех

Логика:
1. Игрок нажимает E
2. ItemPickupSystem вызывает Open()
3. Open() отправляет ServerRpc
4. Сервер: валидирует дистанцию, генерирует лут, добавляет в NetworkInventory
5. Сервер: устанавливает _isOpen = true
6. Сервер: отправляет ClientRpc
7. Все клиенты: играют анимацию открытия
```

#### 2. Интеграция с NetworkInventory
```
В RequestOpenChestServerRpc():
- Получить NetworkInventory игрока (GetComponent<NetworkInventory>())
- lootTable.GenerateLoot() для генерации предметов
- Для каждого предмета: NetworkInventory.GetItemId() + AddItem()
```

#### 3. ChunkNetworkSpawner — спавн сундуков
```
Добавить:
- [SerializeField] private NetworkObject chestPrefab;
- [SerializeField] private float chestSpawnChance = 0.3f;

При спавне чанка:
- Random.value < chestSpawnChance → Spawn chestPrefab
- Привязать к чанку через SetChunk()
```

#### 4. ItemPickupSystem оптимизация
```
Изменить FindNearestInteractable():
БЫЛО: FindObjectsByType<ChestContainer>(FindObjectsInactive.Include)
СТАЛО: InteractableManager.FindNearestChest(position, range)
```

### NPC
```
Типы NPC:
- Vendor — продаёт предметы
- QuestGiver — выдаёт квесты
- Dialog — диалоговые NPC

Структура:
- NpcEntity — базовый класс NPC
- NpcData — данные NPC
- NpcInteraction — компонент взаимодействия
```

---

## 📁 СВЯЗАННЫЕ ФАЙЛЫ

### Существующие:
```
Assets/_Project/Scripts/Player/NetworkPlayer.cs — управление игроком
Assets/_Project/Scripts/Player/PlayerController.cs — контроллер
Assets/_Project/Scripts/World/Streaming/FloatingOriginMP.cs — floating origin

Assets/_Project/Scripts/UI/Inventory/ — инвентарь
Assets/_Project/Scripts/UI/Wheel/ — колесо инвентаря
```

### Новые для итерации 4:
```
Assets/_Project/Scripts/World/Chest/ChestEntity.cs
Assets/_Project/Scripts/World/Chest/ChestData.cs
Assets/_Project/Scripts/World/Chest/ChestInteraction.cs

Assets/_Project/Scripts/World/Items/ItemData.cs
Assets/_Project/Scripts/World/Items/ItemDatabase.cs

Assets/_Project/Scripts/World/NPC/NpcEntity.cs
Assets/_Project/Scripts/World/NPC/NpcData.cs
Assets/_Project/Scripts/World/NPC/NpcInteraction.cs

Assets/_Project/Scripts/Core/Interfaces/IInteractable.cs
Assets/_Project/Scripts/Core/Interfaces/IUsable.cs

Assets/_Project/Prefabs/World/Chest.prefab
Assets/_Project/Prefabs/World/NPC.prefab
```

---

## 📝 ЗАДАЧИ ITERATION 4

### 4.1 Система взаимодействия (Core)
- [ ] Интерфейс `IInteractable` — базовый для всех интерактивных объектов
- [ ] `InteractionDetector` — компонент для обнаружения объектов перед игроком
- [ ] `InteractionPrompt` — UI компонент для отображения подсказок
- [ ] `InteractionManager` — управление взаимодействиями

### 4.2 Сундуки (Chest System)
- [ ] `ChestData` — ScriptableObject с содержимым сундука
- [ ] `ChestEntity` — MonoBehaviour сундука в сцене
- [ ] `ChestInteraction` — компонент взаимодействия
- [ ] Логика открытия: проверка, анимация, выдача лута
- [ ] Network синхронизация (кто открыл, кто может открыть)

### 4.3 Система предметов (Item System)
- [ ] `ItemType` enum — типы предметов
- [ ] `Rarity` enum — редкость предметов
- [ ] `ItemData` — базовый класс предмета (ScriptableObject)
- [ ] `EquipmentItemData` — экипировка со статами
- [ ] `ConsumableItemData` — расходуемые предметы
- [ ] `ItemDatabase` — база данных предметов

### 4.4 NPC система
- [ ] `NpcData` — ScriptableObject с информацией о NPC
- [ ] `NpcEntity` — MonoBehaviour NPC в сцене
- [ ] `NpcInteraction` — компонент взаимодействия
- [ ] Базовый диалог (простой текст)
- [ ] NPC сценарий (на будущее)

### 4.5 Интеграция с инвентарём
- [ ] `InventoryManager` — добавление предметов из сундуков
- [ ] UI уведомление о получении предмета
- [ ] Обработка полного инвентаря

---

## 🔍 КЛЮЧЕВЫЕ РЕШЕНИЯ ИЗ ITERATION 3

### НЕ ПОВТОРЯТЬ:
| Решение | Почему не работает |
|---------|-------------------|
| GetWorldPosition() через IsOwner | Может выбрать ghost объект |
| Camera.main как позиция игрока | Камера на origin, не у игрока |
| Distance от TradeZones | Равно magnitude (TradeZones=0) |

### ИСПОЛЬЗОВАТЬ:
| Решение | Причина |
|---------|---------|
| FloatingOriginMP.Instance.positionSource | Правильная позиция игрока |
| NetworkPlayer.IsOwner | Для проверки локального игрока |
| WorldStreamingManager.GetPlayerPosition() | Работает автономно |

---

## 📋 ЖУРНАЛ ПОПЫТОК

### Формат записи попытки:
```
### Попытка #N: [Краткое описание]
**Дата:** DD.MM.YYYY, HH:MM MSK
**Файлы:** [Список файлов]
**Логика:** [1-2 предложения]
**Изменения:** [Ключевые строки кода]
**Результат:** ✅/❌/⚠️ + краткое пояснение
**Детали:** [Ссылка на детальный анализ если есть]
```

### Где вести журнал:
```
docs/world/LargeScaleMMO/combinesessions/iteration_4/SOLUTION_ATTEMPTS_LOG.md
```

---

## 🎯 ПРАВИЛА ДОКУМЕНТАЦИИ

### Что писать в LOG:
```
1. Дата и время
2. Что делал
3. Что ожидалось
4. Что произошло
5. Выводы
```

### Что НЕ писать:
```
- Огромные блоки кода (только ключевые строки)
- Длинные объяснения (краткость!)
- Спекуляции без данных
```

---

## 🚀 БЫСТРЫЙ СТАРТ (для новой сессии)

### 1. Прочитай текущий статус:
```
docs/world/LargeScaleMMO/combinesessions/iteration_4/SOLUTION_ATTEMPTS_LOG.md
```

### 2. Определи следующую задачу:
```
Смотри секцию "СЛЕДУЮЩИЕ ШАГИ" в SOLUTION_ATTEMPTS_LOG.md
```

### 3. Реализуй задачу:
```
1. Создай необходимые скрипты
2. Протестируй в Play Mode
3. Зафиксируй результат в LOG
```

---

## 📞 КОНТАКТЫ (если нужно продолжить)

Ключевые компоненты для взаимодействия:
- `InteractionManager` — централизованное управление
- `ChestEntity` — сундук
- `NpcEntity` — NPC
- `InventoryManager` — инвентарь игрока

---

## 📁 ДОКУМЕНТАЦИЯ ITERATION 4

| Документ | Описание |
|----------|----------|
| `MASTER_PROMPT.md` | Этот документ — обзор итерации |
| `SOLUTION_ATTEMPTS_LOG.md` | Журнал попыток решения |
| `../ITERATION_4_SESSION.md` | Исходное описание задач |

---

## 🔜 СЛЕДУЮЩИЕ ШАГИ

1. [ ] Создать базовые интерфейсы (IInteractable)
2. [ ] Создать InteractionManager
3. [ ] Создать компонент взаимодействия (E) для игрока
4. [ ] Реализовать базовый сундук
5. [ ] Создать систему предметов

---

**Обновлено:** 19.04.2026, 12:20 MSK  
**Автор:** Claude Code  
**Версия:** iteration_4_v1