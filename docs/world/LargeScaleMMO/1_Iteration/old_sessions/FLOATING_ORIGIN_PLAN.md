# FloatingOrigin — Анализ и решение

## Проблема

**Согласно 01_Architecture_Plan.md:**
- WorldRoot сдвигается → горы/облака двигаются
- TradeZones остаётся на месте
- Игрок — отдельный объект НЕ внутри WorldRoot

**Результат:**
- Горы уезжают от игрока
- Игрок видит пустое пространство

## Согласно плану

### 3.2.4. FloatingOriginMP (Multiplayer-Synced)

```
Server инициирует сдвиг:
1. Server проверяет позицию host-камеры
2. Если > threshold → сдвигает мир
3. Рассылает всем: WorldShiftRpc(offset)
4. Все клиенты применяют сдвиг к worldRoot
5. FloatingOrigin.ResetOrigin() вызывается синхронно
```

**Важно (строка 267):** "сдвиг происходит в LateUpdate ДО того как NetworkTransform снимает позицию"

### 3.3.1. Разделение объектов

```
Горы и облака — НЕ NetworkObjects (локальные)
Фермы, сундуки — NetworkObjects
Корабли — NetworkObjects
```

## Текущая реализация

- `mode = Local` — НЕ как в плане!
- Сервер НЕ инициирует сдвиг
- Клиенты НЕ получают синхронизированный сдвиг

## Решения

### Вариант A: Игрок внутри WorldRoot

Игрок создаётся/перемещается внутрь WorldRoot:
```
WorldRoot
├── Mountains
├── Clouds
├── Farms
└── NetworkPlayer ← игрок внутри!
```

**Плюсы:** Игрок движется с миром
**Минусы:** Нужен рефакторинг спавна

### Вариант B: FloatingOrigin как fallback

FloatingOriginMP для >1M, Chunk Streaming для остального:
- Chunk Streaming загружает чанки вокруг игрока
- FloatingOriginMP для краевых случаев (>1M)

**Плюсы:** Не нужен рефакторинг
**Минусы:** Не соответствует плану

### Вариант C: Полная реализация по плану

1. Переключить FloatingOriginMP в ServerAuthority
2. Реализовать RequestWorldShiftRpc
3. Синхронизировать сдвиг между клиентами

## Рекомендация

**Вариант B** — быстрое решение:
1. Отключить FloatingOriginMP (mode = disabled)
2. Положиться на Chunk Streaming
3. FloatingOrigin как fallback для >1M

**Вариант C** — правильное решение для MMO:
1. Реализовать ServerAuthority режим
2. Синхронизировать сдвиг
