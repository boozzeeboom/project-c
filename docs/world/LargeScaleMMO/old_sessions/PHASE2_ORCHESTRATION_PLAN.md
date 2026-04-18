# Phase 2: Agent Orchestration Plan — World Streaming

**Дата:** 16 апреля 2026 г.  
**Проект:** ProjectC_client  
**Статус:** ⚠️ Требуется анализ и обновление

---

## 1. Анализ: ADR-0002 vs Текущая Реализация

### Архитектура из ADR-0002

```
                         ┌──────────────────────────────────┐
                         │     SERVER (Authoritative)        │
                         │  WorldChunkManager                │
                         │  - Chunk Registry (grid-based)    │
                         │  - PlayerChunkTracker  ← MISSING  │
                         │  - Spawn/Despawn Queue    ← MISSING│
                         └───────────────┬───────────────────┘
                                         │ RPC: LoadChunk/UnloadChunk
                       ┌─────────────────┼─────────────────┐
                       ▼                 ▼                 ▼
               ┌──────────────┐  ┌──────────────┐  ┌──────────────┐
               │  Клиент 0    │  │  Клиент 1    │  │  Клиент N    │
               │ ChunkLoader  │  │ ChunkLoader  │  │ ChunkLoader  │
               │ FloatingOrgMP│  │ FloatingOrgMP│  │ FloatingOrgMP│
               └──────────────┘  └──────────────┘  └──────────────┘
```

### Что реализовано ✅

| Компонент | Статус | Комментарий |
|-----------|--------|-------------|
| `WorldChunkManager` | ✅ Работает | Реестр чанков, grid-based lookup |
| `ChunkLoader` | ✅ Работает | Загрузка/выгрузка с fade |
| `ProceduralChunkGenerator` | ✅ Работает | Детерминированная генерация |
| `FloatingOriginMP` | ✅ Работает | Сдвиг мира, graceful disable |

### Что НЕ реализовано ❌ (Critical Gaps)

| # | Компонент | Описание | Влияние |
|---|-----------|----------|---------|
| 1 | **PlayerChunkTracker** | Сервер не знает какой игрок в каком чанке | Нет server-authoritative streaming |
| 2 | **Chunk Commands RPC** | Нет RPC для Load/Unload от сервера к клиентам | Клиенты не получают команды |
| 3 | **NetworkObject Spawn/Despawn** | Существующие NPC/сундуки не спавнятся/деспавнятся с чанками | Синхронизация сломана |
| 4 | **FloatingOriginMP Server Sync** | Клиенты не синхронизируют сдвиг мира | Рассинхронизация координат |
| 5 | **Preload System** | Соседние чанки не загружаются заранее | Hitching при переходе |

---

## 2. Глубокий анализ: Актуальность vs Unity 6.3

### Unity 6 NGO API Changes (Проверено 16.04.2026)

| # | Вопрос | Ответ | Влияние на план |
|---|--------|-------|-----------------|
| 1 | `RequireOwnership = false`? | **Да, требуется** для RPC вызываемых НЕ владельцем | Исправить ContractSystem, TradeMarketServer |
| 2 | `ServerRpcParams.Receive.SenderClientId`? | **Не изменился** | Текущий код актуален |
| 3 | Dedicated Server API? | **Стабилен** | Можно продолжать разработку |
| 4 | Scene Management? | **Через `NetworkManager.Singleton.SceneManager`** | Текущий подход корректен |
| 5 | Floating Origin в Unity? | **Нет нативной поддержки** | Кастомная реализация остаётся |
| 6 | New Optimizations? | **Unity 6.3 имеет улучшения** для server builds | Учесть при оптимизации |

### Сверить с техническим исследованием (02_Technical_Research.md)

**Выводы из исследования (14.04.2026):**
- World Streamer 2 не поддерживает мультиплеер → ❌ Не использовать
- SECTR Complete слишком сложно → ❌ Не использовать
- Кастомная система — лучший выбор → ✅ Подтверждено
- ECS SubScene несовместим с NGO → ✅ Учтено

**Актуальность:** Исследование актуально,結論 не изменились.

---

## 3. Орхестрация агентов для Phase 2

### Команда агентов

```
Оркестратор (Production Lead)
├── network-programmer
│   └── Task: Server-side chunk management + RPC sync
├── gameplay-programmer
│   └── Task: NPC/Chest spawn per chunk + game logic
├── unity-specialist
│   └── Task: FloatingOriginMP sync + scene management
└── qa-tester
    └── Task: Multiplayer test scenarios
```

### Детальное распределение задач

#### Agent 1: network-programmer

**Зона ответственности:** Серверная часть стриминга

**Задачи:**

| # | Задача | Файл | Описание |
|---|--------|------|----------|
| 1 | **PlayerChunkTracker** | Создать новый | Сервер отслеживает позицию каждого игрока → определяет активные чанки |
| 2 | **LoadChunkRpc** | Добавить в `WorldStreamingManager` | Сервер → клиент: загрузить чанк |
| 3 | **UnloadChunkRpc** | Добавить в `WorldStreamingManager` | Сервер → клиент: выгрузить чанк |
| 4 | **WorldShiftRpc** | Добавить в `FloatingOriginMP` | Синхронизация сдвига мира между клиентами |

**Паттерн RPC:**
```csharp
// Сервер → клиент
[ClientRpc]
void LoadChunkClientRpc(ChunkId chunkId, ClientRpcParams rpcParams = default);

// Сервер инициирует сдвиг
[ClientRpc]
void WorldShiftClientRpc(Vector3 offset, ClientRpcParams rpcParams = default);
```

**Оркестрация:** 
- Сначала создать PlayerChunkTracker
- Затем LoadChunkRpc/UnloadChunkRpc
- 最后 WorldShiftClientRpc для синхронизации FloatingOriginMP

---

#### Agent 2: gameplay-programmer

**Зона ответственности:** Игровая логика стриминга

**Задачи:**

| # | Задача | Файл | Описание |
|---|--------|------|----------|
| 1 | **ChunkNetworkSpawner** | Создать новый | Server-side спавн/деспавн NetworkObjects при загрузке/выгрузке чанков |
| 2 | **Chest Per Chunk** | `ChestContainer.cs` | Сундуки спавнятся с чанком |
| 3 | **NPC Per Chunk** | Создать новый | NPC с поведением спавнятся с чанком |
| 4 | **Inventory Persistence** | `NetworkInventory.cs` | Сохранение инвентаря при выгрузке чанка |

**Интеграция с network-programmer:**
```csharp
// В ChunkLoader после загрузки чанка
public void LoadChunk(ChunkId chunkId)
{
    // Загрузить визуальный контент
    LoadChunkVisual(chunkId);
    
    // Запросить у сервера сетевые объекты
    if (IsServer)
    {
        ChunkNetworkSpawner.SpawnForChunk(chunkId);
    }
    else
    {
        // Запросить у сервера
        RequestChunkNetworkObjectsRpc(chunkId);
    }
}
```

**Оркестрация:**
- После создания PlayerChunkTracker — создать ChunkNetworkSpawner
- Проверить существующие сундуки на сцене
- Добавить триггеры для спавна

---

#### Agent 3: unity-specialist

**Зона ответственности:** FloatingOriginMP синхронизация + оптимизация

**Задачи:**

| # | Задача | Файл | Описание |
|---|--------|------|----------|
| 1 | **FloatingOriginMP Sync** | `FloatingOriginMP.cs` | Принимать WorldShiftRpc от сервера |
| 2 | **Preload System** | `WorldStreamingManager.cs` | Загружать соседние чанки заранее |
| 3 | **Memory Budget** | Новое | Мониторинг памяти, выгрузка при превышении |
| 4 | **Scene Section Integration** | N/A | Не требуется (NGO несовместим с ECS) |

**Орхестрация:**
- Сначала FloatingOriginMP sync (приоритет — блокирует мультиплеер)
- Затем Preload System
- 最后 Memory Budget

---

#### Agent 4: qa-tester

**Зона ответственности:** Тестирование мультиплеера

**Тестовые сценарии:**

| # | Сценарий | Описание | Критерий успеха |
|---|----------|----------|-----------------|
| T1 | **2 игрока, разные чанки** | Host в чанке 0,0; Client в чанке 1,1 | Оба видят свой контент |
| T2 | **Игрок переходит в другой чанок** | Host двигается от 0,0 к 1,0 | Соседний чанк загружается |
| T3 | **Host сдвигает мир** | Камера выходит за threshold 100,000 | Оба клиента синхронизируют сдвиг |
| T4 | **Server权威** | Client не может загрузить чанк без команды сервера | Сервер контролирует |

---

## 4. Обновлённый план (с учётом орхестрации)

### Sprint 1: Server-Side Foundation (1 неделя)

```
Day 1-2: network-programmer
├── [ ] Создать PlayerChunkTracker
├── [ ] Добавить LoadChunkRpc в WorldStreamingManager
└── [ ] Добавить UnloadChunkRpc в WorldStreamingManager

Day 3-4: unity-specialist
├── [ ] Добавить WorldShiftClientRpc в FloatingOriginMP
└── [ ] FloatingOriginMP принимает сдвиг от сервера

Day 5: qa-tester
└── [ ] Тест: Host в чанке, Client в другом — что происходит?
```

### Sprint 2: Network Object Spawn (1 неделя)

```
Day 1-3: gameplay-programmer
├── [ ] Создать ChunkNetworkSpawner
├── [ ] Chest спавнится с чанком
├── [ ] NPC спавнится с чанком (placeholder)

Day 4-5: network-programmer
└── [ ] RequestChunkNetworkObjectsRpc

Day 5: qa-tester
└── [ ] Тест: Сундук появляется при входе в чанк
```

### Sprint 3: Preload + Polish (1 неделя)

```
Day 1-3: unity-specialist
├── [ ] Preload System — загрузка соседних чанков
├── [ ] Memory Budget мониторинг
└── [ ] Оптимизация: fade duration, unload delay

Day 4-5: qa-tester
├── [ ] Тест: Hitching при переходе между чанками
└── [ ] Тест: Memory usage при 5 загруженных чанках
```

---

## 5. Обновлённый Session Prompt для Agent

### Промпт для начала новой сессии:

```markdown
# Session Prompt: Phase 2 Agent Orchestration

**Цель:** Завершить Phase 2 — Server-Side Chunk Management

**Приоритеты:**
1. PlayerChunkTracker — сервер отслеживает игроков
2. LoadChunkRpc — команды загрузки чанков
3. FloatingOriginMP sync — синхронизация сдвига мира

**Орхестрация:**
- network-programmer: PlayerChunkTracker + LoadChunkRpc
- unity-specialist: FloatingOriginMP sync
- qa-tester: Multiplayer tests

**Ожидаемый результат:**
- Сервер управляет загрузкой чанков для каждого клиента
- FloatingOriginMP синхронизирован между клиентами
- 2 клиента в разных чанках видят правильный контент

**Тесты:**
- T1: Host + Client в разных чанках
- T2: Переход между чанками
- T3: Сдвиг мира синхронизирован
```

---

## 6. Файлы для создания/модификации

### Новые файлы

| Файл | Агент | Описание |
|------|-------|----------|
| `PlayerChunkTracker.cs` | network-programmer | Серверный трекинг игроков |
| `ChunkNetworkSpawner.cs` | gameplay-programmer | Спавн/деспавн NetworkObjects |
| `ChunkTestScenarios.cs` | qa-tester | Тестовые сценарии |

### Модификация существующих

| Файл | Агент | Изменение |
|------|-------|-----------|
| `WorldStreamingManager.cs` | network-programmer | +LoadChunkRpc, +UnloadChunkRpc |
| `FloatingOriginMP.cs` | unity-specialist | +WorldShiftClientRpc, +ApplyServerShift() |
| `WorldStreamingManager.cs` | unity-specialist | +Preload System |
| `ChestContainer.cs` | gameplay-programmer | +Привязка к чанку |

---

## 7. Критерии завершения Phase 2

| # | Критерий | Тест |
|---|----------|------|
| 1 | Сервер знает какой игрок в каком чанке | T1: PlayerChunkTracker logs |
| 2 | Клиент получает LoadChunkRpc от сервера | T1: Console shows chunk loaded |
| 3 | FloatingOriginMP синхронизирован | T3: Оба клиента в одной позиции мира |
| 4 | Сундуки/NPC спавнятся с чанком | T1: Видны сундуки в загруженном чанке |
| 5 | Preload работает | T2: Нет hitching при переходе |

---

**Следующий шаг:** Перейти к Sprint 1 орхестрации.