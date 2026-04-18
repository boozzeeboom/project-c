# Iteration 4 Session Prompt: Setup & Test

**Цель:** Настроить компоненты в сцене и протестировать F-клавиши.

**Длительность:** 1-2 сессии

**Критерий приёмки:** 
> F5 → телепортация работает
> F6 → телепортация на Far Peak работает без jitter
> F7 → загрузка чанков работает
> F8 → сброс origin работает

---

## 📋 Задачи

### 4.1 Назначить prefabs для ChunkNetworkSpawner
**Файл:** `ChunkNetworkSpawner.cs`

1. Создать prefab для сундука с NetworkObject
2. Создать prefab для NPC с NetworkObject
3. Назначить в инспекторе

### 4.2 Подключить StreamingTest
**Файл:** `StreamingTest.cs`

1. Назначить `positionSource` = NetworkPlayer
2. Назначить `worldStreamingManager` = WorldStreamingManager

### 4.3 Тест F-клавиш

| Клавиша | Действие | Ожидаемый результат |
|---------|----------|-------------------|
| F5 | Телепорт на ближний пик | Телепортация, чанки загружаются |
| F6 | Телепорт на Far Peak | Без jitter, правильная позиция |
| F7 | Загрузка чанков | Console: "Chunk loaded: X,Y" |
| F8 | Сброс FloatingOrigin | Origin сбрасывается |
| F9 | Toggle grid | Grid визуализируется |
| F10 | Toggle debug HUD | HUD показывает |

---

## 🔍 Перед началом

Прочитать:
- `docs/world/LargeScaleMMO/CURRENT_STATE.md` — секции "Prefabs не назначены" и "StreamingTest не подключен"
- `docs/world/LargeScaleMMO/ITERATION_PLAN.md` — Iteration 4

---

## 📝 Шаги выполнения

#### 4.1 Prefabs

1. Открыть сцену `ProjectC_1.unity`
2. Найти объект с `ChunkNetworkSpawner`
3. Создать prefab для сундука (или использовать существующий)
4. Создать prefab для NPC
5. Назначить в инспекторе ChunkNetworkSpawner

#### 4.2 StreamingTest

1. Найти объект с `StreamingTest`
2. Назначить `positionSource` = NetworkPlayer
3. Назначить `worldStreamingManager` = WorldStreamingManager

#### 4.3 Тестирование

1. Запустить Play Mode
2. Последовательно нажать F5, F6, F7, F8, F9, F10
3. Проверить Console на каждом шаге

---

## ✅ Тестирование

```
Шаг 1: F5
  → Телепортация
  → Console: "Teleporting to..."
  
Шаг 2: F6
  → Телепортация на Far Peak
  → Console: "Teleporting to (-250000, ...)"
  → Нет jitter в движении
  
Шаг 3: F7
  → Загрузка чанков
  → Console: "Chunk loaded: X,Y"
  
Шаг 4: F8
  → Сброс origin
  → Console: "Origin reset"
  
Шаг 5: F9
  → Grid визуализируется
  
Шаг 6: F10
  → Debug HUD показывает
```

---

## 📊 Ожидаемые результаты

| Клавиша | До | После |
|---------|-----|-------|
| F5 | Может не работать | Телепортация работает |
| F6 | Jitter | Без jitter |
| F7 | Нет чанков | Чанки загружаются |
| F8 | Origin не сбрасывается | Сбрасывается |
| F9/F10 | Не работает | Работает |

---

**Автор:** Claude Code  
**Дата:** 18.04.2026  
**Статус:** Нужно выполнить (после Iteration 3)