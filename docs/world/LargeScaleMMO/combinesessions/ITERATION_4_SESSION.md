# Iteration 4 Session Prompt: Chests, Items, NPCs

**Версия документа:** 2.0 (обновлён 19.04.2026)  
**Папка документации:** `docs/world/LargeScaleMMO/combinesessions/iteration_4/`

**Цель:** Реализовать интерактивные объекты мира — сундуки, предметы, NPC.

**Длительность:** 2-4 сессии

**Критерий приёмки:** 
> Сундук открывается по нажатию E
> Предметы добавляются в инвентарь
> NPC показывает диалог по нажатию E

**ДОКУМЕНТАЦИЯ ИТЕРАЦИИ 4:**
| Документ | Описание |
|----------|----------|
| `iteration_4/MASTER_PROMPT.md` | Мастер-промпт для перезапуска итерации |
| `iteration_4/SOLUTION_ATTEMPTS_LOG.md` | Журнал попыток и результатов |

**ВНИМАНИЕ:** Телепорты и F5-F10 НЕ входят в эту итерацию — это задача для другой итерации.

---

## 📋 ЗАДАЧИ ITERATION 4

### 4.1 Система взаимодействия (Core)

1. Создать `IInteractable.cs` — базовый интерфейс
2. Создать `IUsable.cs` — интерфейс для используемых объектов
3. Создать `InteractionManager.cs` — централизованное управление
4. Создать `InteractionDetector.cs` — компонент на игроке для обнаружения

### 4.2 Система предметов (Item)

1. Создать `ItemType.cs` — enum с 8 типами предметов
2. Создать `Rarity.cs` — enum редкости (Common, Rare, Epic, Legendary)
3. Создать `ItemData.cs` — ScriptableObject базового предмета
4. Создать `ItemDatabase.cs` — база данных всех предметов

### 4.3 Сундуки (Chest)

1. Создать `ChestData.cs` — ScriptableObject с содержимым
2. Создать `ChestEntity.cs` — MonoBehaviour сундука
3. Создать `ChestInteraction.cs` — компонент взаимодействия
4. Добавить анимацию открытия
5. Добавить Network синхронизацию

### 4.4 NPC система

1. Создать `NpcData.cs` — ScriptableObject с информацией
2. Создать `NpcEntity.cs` — MonoBehaviour NPC
3. Создать `NpcInteraction.cs` — компонент взаимодействия
4. Реализовать простой диалог

### 4.5 Интеграция с инвентарём

1. Подключить сундук к `InventoryManager`
2. Добавить UI уведомление о получении предмета
3. Обработать случай полного инвентаря

---

## 🔍 Перед началом

Прочитать (обязательно):
1. `docs/world/LargeScaleMMO/combinesessions/iteration_4/MASTER_PROMPT.md` — полный план работ
2. `docs/world/LargeScaleMMO/combinesessions/iteration_4/SOLUTION_ATTEMPTS_LOG.md` — текущий статус

---

## 📝 Шаги выполнения

### Шаг 1: Система взаимодействия

```
1. Создать папку Assets/_Project/Scripts/Core/Interfaces/
2. Создать IInteractable.cs
3. Создать IUsable.cs
4. Создать папку Assets/_Project/Scripts/Core/Interaction/
5. Создать InteractionManager.cs
6. Создать папку Assets/_Project/Scripts/Player/
7. Создать InteractionDetector.cs (компонент на игроке)
```

### Шаг 2: Система предметов

```
1. Создать папку Assets/_Project/Scripts/World/Items/
2. Создать ItemType.cs
3. Создать Rarity.cs
4. Создать ItemData.cs (ScriptableObject)
5. Создать ItemDatabase.cs
6. Создать тестовые предметы в Resources/
```

### Шаг 3: Сундуки

```
1. Создать папку Assets/_Project/Scripts/World/Chest/
2. Создать ChestData.cs
3. Создать ChestEntity.cs
4. Создать ChestInteraction.cs
5. Создать тестовый prefab в Assets/_Project/Prefabs/World/
6. Протестировать в сцене
```

### Шаг 4: NPC

```
1. Создать папку Assets/_Project/Scripts/World/NPC/
2. Создать NpcData.cs
3. Создать NpcEntity.cs
4. Создать NpcInteraction.cs
5. Создать тестовый prefab
```

---

## ✅ Критерии завершения

### Система взаимодействия:
- [ ] Нажатие E рядом с сундуком/NPC показывает prompt
- [ ] InteractionManager обрабатывает взаимодействие
- [ ] Работает с Network объектами

### Сундуки:
- [ ] Сундук открывается по нажатию E
- [ ] Предметы из сундука добавляются в инвентарь
- [ ] Сундук нельзя открыть повторно (одноразовый)
- [ ] Network синхронизация работает

### Предметы:
- [ ] ItemData создаются как ScriptableObject
- [ ] ItemDatabase содержит ссылки на все предметы
- [ ] Предметы с разными типами (Resource, Equipment, etc.)

### NPC:
- [ ] NPC показывает диалог по нажатию E
- [ ] NPC может иметь имя и описание

---

## 📊 Ожидаемые результаты

| Система | До | После |
|---------|-----|-------|
| Взаимодействие | Нет | Работает с E |
| Сундуки | Нет | Открываются, дают лут |
| Предметы | Нет | Система работает |
| NPC | Нет | Показывают диалог |

---

## 📁 Ключевые файлы для создания

### Core (всегда первыми):
```
Assets/_Project/Scripts/Core/Interfaces/IInteractable.cs
Assets/_Project/Scripts/Core/Interfaces/IUsable.cs
Assets/_Project/Scripts/Core/Interaction/InteractionManager.cs
Assets/_Project/Scripts/Player/InteractionDetector.cs
```

### Items:
```
Assets/_Project/Scripts/World/Items/ItemType.cs
Assets/_Project/Scripts/World/Items/Rarity.cs
Assets/_Project/Scripts/World/Items/ItemData.cs
Assets/_Project/Scripts/World/Items/ItemDatabase.cs
```

### Chest:
```
Assets/_Project/Scripts/World/Chest/ChestData.cs
Assets/_Project/Scripts/World/Chest/ChestEntity.cs
Assets/_Project/Scripts/World/Chest/ChestInteraction.cs
```

### NPC:
```
Assets/_Project/Scripts/World/NPC/NpcData.cs
Assets/_Project/Scripts/World/NPC/NpcEntity.cs
Assets/_Project/Scripts/World/NPC/NpcInteraction.cs
```

---

## 🚨 ВАЖНО

1. **Всегда документируй попытки** — записывай в `SOLUTION_ATTEMPTS_LOG.md`
2. **Тестируй после каждого изменения** — Play Mode, проверяй логи
3. **Не бойся пересмотреть план** — если что-то не работает, ищи альтернативу
4. **Используй существующие системы** — наследуйся от них

---

**Автор:** Claude Code  
**Дата:** 19.04.2026  
**Статус:** АКТИВНАЯ ИТЕРАЦИЯ  
**Версия:** iteration_4_v1
