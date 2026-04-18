# Iteration 5 Session Prompt: Multiplayer Test

**Цель:** Протестировать синхронизацию стриминга в мультиплеере.

**Длительность:** 1-2 сессии

**Критерий приёмки:** 
> Host + Client: оба видят одинаковые загруженные чанки.
> Сервер отправляет LoadChunkClientRpc при смене чанка.

---

## 📋 Задачи

### 5.1 Build Settings
1. Добавить сцену в Build
2. Запустить 2 инстанса (Host + Client)

### 5.2 Тест синхронизации

| Шаг | Действие | Ожидаемый результат |
|-----|----------|-------------------|
| 1 | Host: Start as Host | "Host started" в Console |
| 2 | Client: Connect to localhost | "Client connected" в Console |
| 3 | Host: Переместиться | PlayerChunkTracker отслеживает |
| 4 | Host: Сменить чанк | LoadChunkClientRpc отправляется |
| 5 | Client: Получает RPC | Чанк загружается |
| 6 | Client: Видит контент | Тот же контент что Host |

---

## 🔍 Перед началом

Прочитать:
- `docs/world/LargeScaleMMO/CURRENT_STATE.md` — секция "Multiplayer sync"
- `docs/world/LargeScaleMMO/ITERATION_PLAN.md` — Iteration 5

---

## 📝 Шаги выполнения

#### 5.1 Подготовка

1. Открыть Build Settings (File → Build Settings)
2. Добавить сцену `ProjectC_1.unity` если не добавлена
3. Выбрать "Build And Run" для создания билда

#### 5.2 Тест Host

1. Запустить игру (Instance 1)
2. Нажать "Start as Host"
3. Записать client ID
4. Перемещаться, проверить Console

#### 5.3 Тест Client

1. Запустить игру (Instance 2)
2. Ввести "127.0.0.1" и port
3. Нажать "Connect"
4. Проверить синхронизацию

#### 5.4 Проверка синхронизации

```
Host Console:
  [PlayerChunkTracker] Client 1 in chunk X,Y
  [LoadChunkClientRpc] Sending to client 1
  
Client Console:
  [LoadChunkClientRpc] Received: Load chunk X,Y
  [ChunkLoader] Chunk loaded: X,Y
```

---

## ✅ Тестирование

```
Step 1: Запустить Host (Instance 1)
  → Start as Host
  → Console: "Host started"
  → Player ID: 0

Step 2: Запустить Client (Instance 2)
  → Connect to 127.0.0.1
  → Console: "Client connected"
  → Client ID: 1

Step 3: Host нажать F6
  → Телепортация
  → Console: "Broadcasting world shift to all clients"
  → Client Console: "World shift received: (-250000, 0, -250000)"

Step 4: Host нажать F7
  → Загрузка чанков
  → Console: "Sending LoadChunk RPC to client 1"
  → Client Console: "Chunk loaded: X,Y"

Step 5: Проверить контент
  → Host видит горы/облака
  → Client видит те же горы/облака
  → Нет рассинхронизации
```

---

## 📊 Ожидаемые результаты

| Метрика | До | После |
|---------|-----|-------|
| Host + Client | Нет синхронизации | Полная синхронизация |
| LoadChunk RPC | Нет | Отправляется при смене чанка |
| Видимый контент | Host ≠ Client | Host = Client |
| World shift | Host ≠ Client | Host = Client |

---

## ⚠️ Troubleshooting

| Проблема | Решение |
|----------|---------|
| Client не видит чанки | Проверить NetworkPrefabs в Build Settings |
| World shift не синхронизируется | Проверить FloatingOriginMP mode = ServerAuthority |
| RPC не отправляется | Проверить IsServer в PlayerChunkTracker |

---

**Автор:** Claude Code  
**Дата:** 18.04.2026  
**Статус:** Нужно выполнить (после Iteration 4)